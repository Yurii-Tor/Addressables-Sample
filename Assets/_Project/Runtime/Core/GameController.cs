using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace AddressablesSample.Game.Core
{
    public enum RoundOutcome
    {
        Succeeded,
        Fallback,
        Superseded,
        Canceled,
        Fatal
    }

    public readonly struct TerminalRoundRecord
    {
        public TerminalRoundRecord(long sequence, int requestedGeneration, RoundOutcome outcome,
            double elapsedMilliseconds)
        {
            Sequence = sequence;
            RequestedGeneration = requestedGeneration;
            Outcome = outcome;
            ElapsedMilliseconds = elapsedMilliseconds;
        }

        public long Sequence { get; }
        public int RequestedGeneration { get; }
        public RoundOutcome Outcome { get; }
        public double ElapsedMilliseconds { get; }
    }

    public sealed class GameController : IDisposable
    {
        public const int RoundHistoryCapacity = 16;
        private readonly IGameConfiguration _configuration;
        private readonly IAddressableAssetLoader _loader;
        private readonly IGameHud _hud;
        private readonly ITargetFactory _targetFactory;
        private readonly IGameDiagnostics _diagnostics;

        private GameState _state = GameState.Uninitialized;
        private GameDefinition _definition;
        private RoundTextureSelector _selector;
        private IAddressableLoad<Texture2D> _fallbackLoad;
        private IAddressableLoad<GameObject> _prefabLoad;
        private IAddressableLoad<Texture2D> _pendingRound;
        private IAddressableLoad<Texture2D> _currentRound;
        private Texture2D _fallbackTexture;
        private ITargetView _target;
        private int _roundGeneration;
        private Task _activeRoundTask = Task.CompletedTask;
        private readonly Stopwatch _roundStopwatch = new Stopwatch();
        private readonly TerminalRoundRecord[] _roundHistory = new TerminalRoundRecord[RoundHistoryCapacity];
        private int _roundHistoryCount;
        private long _terminalSequence;
        private int _unsettledRoundGeneration;

        public GameController(
            IGameConfiguration configuration,
            IAddressableAssetLoader loader,
            IGameHud hud,
            ITargetFactory targetFactory,
            IGameDiagnostics diagnostics)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _targetFactory = targetFactory ?? throw new ArgumentNullException(nameof(targetFactory));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public GameState State => _state;
        public int Score { get; private set; }
        internal Task ActiveRoundTask => _activeRoundTask;

        /// <summary>
        /// Monotonic identifier of the newest round request. Every pending load carries the
        /// generation it was started with, so a stale completion can recognize itself.
        /// </summary>
        public int RoundGeneration => _roundGeneration;

        /// <summary>Elapsed time from request start to the most recent terminal round outcome.</summary>
        public double LastRoundLoadMilliseconds { get; private set; }

        /// <summary>
        /// Appends retained records newer than the caller's cursor in sequence order. Returns the
        /// number already evicted before the oldest retained record. Readers own their cursors.
        /// </summary>
        public long ReadTerminalRounds(long afterSequence, List<TerminalRoundRecord> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var oldest = _terminalSequence - _roundHistoryCount + 1;
            var missed = _roundHistoryCount == 0 ? 0 : Math.Max(0, oldest - afterSequence - 1);
            for (var sequence = Math.Max(afterSequence + 1, oldest); sequence <= _terminalSequence; sequence++)
            {
                destination.Add(_roundHistory[(int)((sequence - 1) % RoundHistoryCapacity)]);
            }

            return missed;
        }

        public async Task InitializeAsync()
        {
            if (_state != GameState.Uninitialized)
            {
                return;
            }

            try
            {
                Score = 0;
                _hud.SetScore(Score);
                _hud.SetStatus(GameStatusText.LoadingGame);

                if (!_configuration.TryCreateDefinition(out _definition, out var validationError))
                {
                    EnterFatal("Game configuration is invalid: " + validationError);
                    return;
                }

                _selector = new RoundTextureSelector(_definition.RoundTextureKeys);
                await LoadFallbackAsync();
            }
            catch (Exception exception)
            {
                EnterFatal("Unexpected startup failure.", exception);
            }
        }

        public void HandleSelection(bool hitTarget)
        {
            if (_state != GameState.Ready || !HasValidTarget())
            {
                return;
            }

            try
            {
                if (!hitTarget)
                {
                    _target.FlashError();
                    return;
                }

                Score++;
                _hud.SetScore(Score);
                _target.StopFeedback();
                BeginRound();
            }
            catch (Exception exception)
            {
                EnterFatal("Selection handling failed.", exception);
            }
        }

        internal void BeginRound()
        {
            if ((_state != GameState.Ready && _state != GameState.LoadingRound) || !HasValidTarget())
            {
                return;
            }

            RecordTerminalRound(RoundOutcome.Superseded);
            var generation = ++_roundGeneration;
            var replaced = _pendingRound;
            _pendingRound = null;
            replaced?.Dispose();

            _unsettledRoundGeneration = generation;
            _roundStopwatch.Restart();

            try
            {
                _state = GameState.LoadingRound;
                _target.SetInteractionEnabled(false);
                _hud.SetStatus(GameStatusText.LoadingImage);

                var runtimeKey = _selector.Next();
                var load = _loader.StartLoad<Texture2D>(runtimeKey);
                if (load == null)
                {
                    throw new InvalidOperationException("The Addressables loader returned no round operation owner.");
                }

                _pendingRound = load;
                _activeRoundTask = CompleteRoundAsync(load, generation);
            }
            catch (Exception exception)
            {
                HandleRoundStartFailure(generation, exception);
            }
        }

        public void Dispose()
        {
            if (_state == GameState.Disposed)
            {
                return;
            }

            _state = GameState.Disposed;
            RecordTerminalRound(RoundOutcome.Canceled);
            _roundGeneration++;

            DisposeAndClear(ref _pendingRound);
            DestroyTarget();
            DisposeAndClear(ref _currentRound);
            _fallbackTexture = null;
            DisposeAndClear(ref _fallbackLoad);
            DisposeAndClear(ref _prefabLoad);
            _definition = null;
            _selector = null;
        }

        private async Task LoadFallbackAsync()
        {
            _state = GameState.LoadingStartupFallback;
            var load = StartStartupLoad<Texture2D>(_definition.FallbackTextureKey, "fallback texture");
            if (load == null)
            {
                return;
            }

            _fallbackLoad = load;
            var result = await AwaitLoad(load, "Fallback texture load");

            if (_state != GameState.LoadingStartupFallback || !ReferenceEquals(_fallbackLoad, load))
            {
                return;
            }

            if (result.Status != AddressableLoadStatus.Succeeded || result.Asset == null)
            {
                DisposeAndClear(ref _fallbackLoad);
                EnterFatal("Unable to load the fallback texture.", result.Exception);
                return;
            }

            _fallbackTexture = result.Asset;
            await LoadPrefabAsync();
        }

        private async Task LoadPrefabAsync()
        {
            _state = GameState.LoadingStartupPrefab;
            var load = StartStartupLoad<GameObject>(_definition.TargetPrefabKey, "target prefab");
            if (load == null)
            {
                return;
            }

            _prefabLoad = load;
            var result = await AwaitLoad(load, "Target prefab load");

            if (_state != GameState.LoadingStartupPrefab || !ReferenceEquals(_prefabLoad, load))
            {
                return;
            }

            if (result.Status != AddressableLoadStatus.Succeeded || result.Asset == null)
            {
                DisposeAndClear(ref _prefabLoad);
                EnterFatal("Unable to load the target prefab.", result.Exception);
                return;
            }

            try
            {
                _target = _targetFactory.Create(result.Asset, _fallbackTexture);
                if (!HasValidTarget())
                {
                    throw new InvalidOperationException("The target factory returned an invalid target.");
                }
            }
            catch (Exception exception)
            {
                EnterFatal("Unable to create the target object.", exception);
                return;
            }

            _state = GameState.LoadingRound;
            BeginRound();
            await _activeRoundTask;
        }

        private IAddressableLoad<T> StartStartupLoad<T>(object runtimeKey, string description)
            where T : UnityEngine.Object
        {
            try
            {
                var load = _loader.StartLoad<T>(runtimeKey);
                if (load == null)
                {
                    throw new InvalidOperationException($"The Addressables loader returned no owner for the {description}.");
                }

                return load;
            }
            catch (Exception exception)
            {
                EnterFatal($"Unable to start the {description} load.", exception);
                return null;
            }
        }

        private async Task<AddressableLoadResult<T>> AwaitLoad<T>(IAddressableLoad<T> load, string description)
            where T : UnityEngine.Object
        {
            try
            {
                return await load.Completion;
            }
            catch (Exception exception)
            {
                return AddressableLoadResult<T>.Failed(
                    new InvalidOperationException(description + " threw unexpectedly.", exception));
            }
        }

        private async Task CompleteRoundAsync(IAddressableLoad<Texture2D> load, int generation)
        {
            var result = await AwaitLoad(load, "Round texture load");

            if (!IsCurrentRound(load, generation))
            {
                return;
            }

            if (result.Status == AddressableLoadStatus.Succeeded && result.Asset != null)
            {
                ApplySuccessfulRound(load, result.Asset);
                return;
            }

            ApplyFallbackRound(load, result.Exception);
        }

        private void ApplySuccessfulRound(IAddressableLoad<Texture2D> load, Texture2D texture)
        {
            _pendingRound = null;

            try
            {
                _target.ApplyTexture(texture);
                var previous = _currentRound;
                _currentRound = load;
                previous?.Dispose();
                EnterReady(false);
                RecordTerminalRound(RoundOutcome.Succeeded);
            }
            catch (Exception exception)
            {
                EnterFatal("Unable to apply the loaded round texture.", exception);
                load.Dispose();
            }
        }

        private void ApplyFallbackRound(IAddressableLoad<Texture2D> load, Exception loadException)
        {
            _pendingRound = null;
            load?.Dispose();

            try
            {
                _target.ApplyTexture(_fallbackTexture);
                DisposeAndClear(ref _currentRound);
                _diagnostics.LogWarning("Round texture load failed; the retained fallback is in use.", loadException);
                EnterReady(true);
                RecordTerminalRound(RoundOutcome.Fallback);
            }
            catch (Exception exception)
            {
                EnterFatal("Unable to apply the fallback texture.", exception);
            }
        }

        private void HandleRoundStartFailure(int generation, Exception exception)
        {
            if (_state != GameState.LoadingRound || generation != _roundGeneration || !HasValidTarget())
            {
                return;
            }

            ApplyFallbackRound(null, exception);
        }

        private void EnterReady(bool fallbackInUse)
        {
            _hud.SetStatus(fallbackInUse ? GameStatusText.ReadyWithFallback : GameStatusText.Ready);
            _state = GameState.Ready;
            _target.SetInteractionEnabled(true);
        }

        private bool IsCurrentRound(IAddressableLoad<Texture2D> load, int generation)
        {
            return _state == GameState.LoadingRound &&
                   generation == _roundGeneration &&
                   ReferenceEquals(_pendingRound, load) &&
                   HasValidTarget();
        }

        private bool HasValidTarget()
        {
            return _target != null && _target.IsValid;
        }

        private void RecordTerminalRound(RoundOutcome outcome)
        {
            if (_unsettledRoundGeneration == 0)
            {
                return;
            }

            _roundStopwatch.Stop();
            var duration = _roundStopwatch.Elapsed.TotalMilliseconds;
            LastRoundLoadMilliseconds = duration;
            var sequence = ++_terminalSequence;
            _roundHistory[(int)((sequence - 1) % RoundHistoryCapacity)] =
                new TerminalRoundRecord(sequence, _unsettledRoundGeneration, outcome, duration);
            _roundHistoryCount = Math.Min(_roundHistoryCount + 1, RoundHistoryCapacity);
            _unsettledRoundGeneration = 0;
        }

        private void EnterFatal(string message, Exception exception = null)
        {
            if (_state == GameState.Disposed || _state == GameState.FatalError)
            {
                return;
            }

            _state = GameState.FatalError;
            RecordTerminalRound(RoundOutcome.Fatal);
            _roundGeneration++;
            DisposeAndClear(ref _pendingRound);
            DestroyTarget();
            DisposeAndClear(ref _currentRound);
            _fallbackTexture = null;
            DisposeAndClear(ref _fallbackLoad);
            DisposeAndClear(ref _prefabLoad);

            try
            {
                _diagnostics.LogError(message, exception);
            }
            catch
            {
                // Diagnostics must never prevent deterministic cleanup.
            }

            try
            {
                _hud.SetStatus(GameStatusText.FatalError);
            }
            catch
            {
                // A broken view cannot recover, but resources are already released.
            }
        }

        private void DestroyTarget()
        {
            var target = _target;
            _target = null;
            if (target == null)
            {
                return;
            }

            try
            {
                if (target.IsValid)
                {
                    target.StopFeedback();
                    target.SetInteractionEnabled(false);
                    target.Clear();
                }
            }
            catch (Exception exception)
            {
                try
                {
                    _diagnostics.LogWarning("Target cleanup encountered an error.", exception);
                }
                catch
                {
                    // Continue to factory destruction and owner release.
                }
            }

            try
            {
                _targetFactory.Destroy(target);
            }
            catch (Exception exception)
            {
                try
                {
                    _diagnostics.LogWarning("Target destruction encountered an error.", exception);
                }
                catch
                {
                    // Teardown remains non-throwing.
                }
            }
        }

        private static void DisposeAndClear<T>(ref IAddressableLoad<T> load) where T : UnityEngine.Object
        {
            var ownedLoad = load;
            load = null;
            ownedLoad?.Dispose();
        }
    }
}
