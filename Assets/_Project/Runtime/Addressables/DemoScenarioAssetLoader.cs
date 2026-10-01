using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.AddressableAssets
{
    internal enum DemoScenarioInstruction
    {
        None,
        DelaySuccessfulDelivery,
        FailNextRound
    }

    /// <summary>
    /// Adds one explicitly armed, one-shot simulation to configured round texture requests.
    /// Startup assets and non-round keys always go straight to the production loader.
    /// </summary>
    internal sealed class DemoScenarioAssetLoader : IAddressableAssetLoader
    {
        internal const double SlowDeliverySeconds = 2.0;

        private readonly IAddressableAssetLoader _inner;
        private readonly HashSet<string> _roundKeys;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly Func<bool> _canConsumeInstruction;
        private DemoScenarioInstruction _nextInstruction;

        internal DemoScenarioAssetLoader(
            IAddressableAssetLoader inner,
            IEnumerable<object> configuredRoundKeys,
            Func<TimeSpan, CancellationToken, Task> delayAsync,
            Func<bool> canConsumeInstruction)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
            _canConsumeInstruction = canConsumeInstruction ?? throw new ArgumentNullException(nameof(canConsumeInstruction));
            _roundKeys = new HashSet<string>(StringComparer.Ordinal);

            if (configuredRoundKeys == null)
            {
                return;
            }

            foreach (var key in configuredRoundKeys)
            {
                if (key != null)
                {
                    _roundKeys.Add(key.ToString());
                }
            }
        }

        internal bool HasArmedInstruction => _nextInstruction != DemoScenarioInstruction.None;

        internal bool TryArm(DemoScenarioInstruction instruction)
        {
            if (instruction == DemoScenarioInstruction.None || HasArmedInstruction)
            {
                return false;
            }

            _nextInstruction = instruction;
            return true;
        }

        internal void ClearInstruction()
        {
            _nextInstruction = DemoScenarioInstruction.None;
        }

        public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
        {
            if (_nextInstruction == DemoScenarioInstruction.None || !IsConfiguredRoundTexture<T>(runtimeKey))
            {
                return _inner.StartLoad<T>(runtimeKey);
            }

            // The first configured round request during startup is not a visitor command. Drop
            // any stale instruction instead of letting it leak into the first playable round.
            if (!_canConsumeInstruction())
            {
                ClearInstruction();
                return _inner.StartLoad<T>(runtimeKey);
            }

            var instruction = _nextInstruction;
            ClearInstruction();

            if (instruction == DemoScenarioInstruction.FailNextRound)
            {
                return new SimulatedFailureLoad<T>();
            }

            var innerLoad = _inner.StartLoad<T>(runtimeKey);
            if (innerLoad == null)
            {
                throw new InvalidOperationException("The Addressables loader returned no owner for the simulated slow round.");
            }

            try
            {
                return new DelayedAddressableLoad<T>(
                    innerLoad,
                    _delayAsync,
                    TimeSpan.FromSeconds(SlowDeliverySeconds));
            }
            catch
            {
                innerLoad.Dispose();
                throw;
            }
        }

        private bool IsConfiguredRoundTexture<T>(object runtimeKey) where T : UnityEngine.Object
        {
            return typeof(T) == typeof(Texture2D) &&
                   runtimeKey != null &&
                   _roundKeys.Contains(runtimeKey.ToString());
        }

        private sealed class SimulatedFailureLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
        {
            private readonly Task<AddressableLoadResult<T>> _completion = Task.FromResult(
                AddressableLoadResult<T>.Failed(new InvalidOperationException(
                    "Simulated failure: this demo intentionally failed a configured round request; no transport failure was measured.")));

            public Task<AddressableLoadResult<T>> Completion => _completion;

            public void Dispose()
            {
            }
        }

        private sealed class DelayedAddressableLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
        {
            private readonly TaskCompletionSource<AddressableLoadResult<T>> _completion =
                new TaskCompletionSource<AddressableLoadResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
            private readonly TimeSpan _delay;
            private readonly CancellationTokenSource _disposeCancellation = new CancellationTokenSource();
            private readonly CancellationToken _disposeToken;
            private IAddressableLoad<T> _inner;
            private int _disposed;
            private int _innerDisposed;

            public DelayedAddressableLoad(
                IAddressableLoad<T> inner,
                Func<TimeSpan, CancellationToken, Task> delayAsync,
                TimeSpan delay)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
                _delay = delay;
                _disposeToken = _disposeCancellation.Token;
                _ = DeliverAsync();
            }

            public Task<AddressableLoadResult<T>> Completion => _completion.Task;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                try
                {
                    _disposeCancellation.Cancel();
                }
                finally
                {
                    _completion.TrySetResult(AddressableLoadResult<T>.Canceled());
                    try
                    {
                        DisposeInnerOnce();
                    }
                    finally
                    {
                        _disposeCancellation.Dispose();
                    }
                }
            }

            private async Task DeliverAsync()
            {
                try
                {
                    var inner = Volatile.Read(ref _inner);
                    var result = await inner.Completion;
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        return;
                    }

                    if (result.Status != AddressableLoadStatus.Succeeded || result.Asset == null)
                    {
                        _completion.TrySetResult(result);
                        return;
                    }

                    await _delayAsync(_delay, _disposeToken);
                    if (Volatile.Read(ref _disposed) == 0)
                    {
                        _completion.TrySetResult(result);
                    }
                }
                catch (OperationCanceledException)
                {
                    _completion.TrySetResult(AddressableLoadResult<T>.Canceled());
                }
                catch (Exception exception)
                {
                    if (Volatile.Read(ref _disposed) == 0)
                    {
                        _completion.TrySetResult(AddressableLoadResult<T>.Failed(exception));
                    }
                }
            }

            private void DisposeInnerOnce()
            {
                if (Interlocked.Exchange(ref _innerDisposed, 1) != 0)
                {
                    return;
                }

                var inner = Interlocked.Exchange(ref _inner, null);
                inner?.Dispose();
            }
        }
    }
}
