using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AddressablesSample.Game.AddressableAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AddressablesSample.Game.Presentation
{
    /// <summary>
    /// Development commands for the visible P05 scenarios. The bootstrapper owns this component
    /// and its frame-driven unscaled delay queue; presentation controls do not change its behavior.
    /// </summary>
    public sealed class DemoScenarioCommands : MonoBehaviour
    {
        private const float ReplacementLeadSeconds = 0.15f;

        private enum ReplacementPhase
        {
            None,
            WaitingToStartB,
            WaitingForSettlement
        }

        private sealed class DelayWaiter
        {
            public DelayWaiter(TimeSpan delay)
            {
                RemainingSeconds = (float)delay.TotalSeconds;
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public float RemainingSeconds { get; set; }
            public TaskCompletionSource<bool> Completion { get; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }
        }

        private readonly List<DelayWaiter> _delayWaiters = new List<DelayWaiter>(2);
        private GameBootstrapper _bootstrapper;
        private DemoScenarioAssetLoader _scenarioLoader;
        private Task _singleRoundTask;
        private Task _replacementATask;
        private Task _replacementBTask;
        private ReplacementPhase _replacementPhase;
        private float _replacementSecondsRemaining;
        private bool _disposed;

        internal string StatusLabel { get; private set; }
        internal bool IsReplacementSequenceActive => _replacementPhase != ReplacementPhase.None;
        internal int PendingDelayCount => _delayWaiters.Count;
        internal bool CanStartCommands =>
            CanStartCommand() &&
            _bootstrapper.Controller != null &&
            _bootstrapper.Controller.CanAcceptScenarioCommands &&
            _replacementPhase == ReplacementPhase.None &&
            (_singleRoundTask == null || _singleRoundTask.IsCompleted);

        internal void Configure(GameBootstrapper bootstrapper, DemoScenarioAssetLoader scenarioLoader)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DemoScenarioCommands));
            }

            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
            _scenarioLoader = scenarioLoader ?? throw new ArgumentNullException(nameof(scenarioLoader));
        }

        internal Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (_disposed)
            {
                return Task.FromCanceled(new CancellationToken(true));
            }

            if (delay == TimeSpan.Zero)
            {
                return Task.CompletedTask;
            }

            var waiter = new DelayWaiter(delay);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.CancellationRegistration = cancellationToken.Register(
                    () => waiter.Completion.TrySetCanceled());
            }

            _delayWaiters.Add(waiter);
            return waiter.Completion.Task;
        }

        internal bool TryStartSlowRound()
        {
            return TryStartSingleRound(
                DemoScenarioInstruction.DelaySuccessfulDelivery,
                "Simulated 2-second unscaled delivery delay; this is not network timing.");
        }

        internal bool TryStartFailure()
        {
            return TryStartSingleRound(
                DemoScenarioInstruction.FailNextRound,
                "Simulated failure injected; no transport request failed.");
        }

        internal bool TryStartReplacement()
        {
            if (!CanStartCommand())
            {
                return false;
            }

            if (_replacementPhase != ReplacementPhase.None ||
                (_singleRoundTask != null && !_singleRoundTask.IsCompleted))
            {
                return false;
            }

            if (!_bootstrapper.TryBeginScenarioRound(
                    DemoScenarioInstruction.DelaySuccessfulDelivery,
                    out _replacementATask))
            {
                StatusLabel = "A->B simulation rejected; the game is not ready for commands.";
                return false;
            }

            _replacementSecondsRemaining = ReplacementLeadSeconds;
            _replacementPhase = ReplacementPhase.WaitingToStartB;
            StatusLabel = "Simulated A->B replacement running; transport cancellation is not measured.";
            return true;
        }

        internal void AdvanceTime(float unscaledDeltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            var delta = Math.Max(0f, unscaledDeltaSeconds);
            AdvanceDelayWaiters(delta);
            AdvanceReplacement(delta);
            ObserveSingleRound();
        }

        internal void StopAndCancel()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scenarioLoader?.ClearInstruction();
            for (var index = 0; index < _delayWaiters.Count; index++)
            {
                var waiter = _delayWaiters[index];
                waiter.CancellationRegistration.Dispose();
                waiter.Completion.TrySetCanceled();
            }

            _delayWaiters.Clear();
            _replacementPhase = ReplacementPhase.None;
            _singleRoundTask = null;
            _replacementATask = null;
            _replacementBTask = null;
            _bootstrapper = null;
            _scenarioLoader = null;
        }

        private void Update()
        {
            AdvanceTime(Time.unscaledDeltaTime);
            ReadCommands();
        }

        private bool TryStartSingleRound(DemoScenarioInstruction instruction, string status)
        {
            if (!CanStartCommand())
            {
                return false;
            }

            if (_replacementPhase != ReplacementPhase.None ||
                (_singleRoundTask != null && !_singleRoundTask.IsCompleted))
            {
                return false;
            }

            if (!_bootstrapper.TryBeginScenarioRound(instruction, out _singleRoundTask))
            {
                StatusLabel = "Scenario command rejected; the game is not ready for commands.";
                return false;
            }

            StatusLabel = status;
            return true;
        }

        private bool CanStartCommand()
        {
            return !_disposed && _bootstrapper != null && _scenarioLoader != null;
        }

        private void AdvanceDelayWaiters(float delta)
        {
            for (var index = _delayWaiters.Count - 1; index >= 0; index--)
            {
                var waiter = _delayWaiters[index];
                if (!waiter.Completion.Task.IsCompleted)
                {
                    waiter.RemainingSeconds -= delta;
                    if (waiter.RemainingSeconds <= 0f)
                    {
                        waiter.Completion.TrySetResult(true);
                    }
                }

                if (waiter.Completion.Task.IsCompleted)
                {
                    waiter.CancellationRegistration.Dispose();
                    _delayWaiters.RemoveAt(index);
                }
            }
        }

        private void AdvanceReplacement(float delta)
        {
            if (_replacementPhase == ReplacementPhase.WaitingToStartB)
            {
                if (_bootstrapper == null || _bootstrapper.Controller == null ||
                    _bootstrapper.Controller.State == AddressablesSample.Game.Core.GameState.Disposed ||
                    _bootstrapper.Controller.State == AddressablesSample.Game.Core.GameState.FatalError)
                {
                    FinishReplacement("A->B simulation canceled before B could start.");
                    return;
                }

                _replacementSecondsRemaining -= delta;
                if (_replacementSecondsRemaining > 0f)
                {
                    return;
                }

                if (!_bootstrapper.TryBeginScenarioRound(DemoScenarioInstruction.None, out _replacementBTask))
                {
                    FinishReplacement("A->B simulation stopped because B was rejected.");
                    return;
                }

                _replacementPhase = ReplacementPhase.WaitingForSettlement;
            }

            if (_replacementPhase == ReplacementPhase.WaitingForSettlement &&
                IsSettled(_replacementATask) && IsSettled(_replacementBTask))
            {
                FinishReplacement(
                    "A->B simulation finished; inspect the trace for outcomes. Transport cancellation is not measured.");
            }
        }

        private void ObserveSingleRound()
        {
            if (_singleRoundTask == null || !_singleRoundTask.IsCompleted)
            {
                return;
            }

            ObserveException(_singleRoundTask);
            _singleRoundTask = null;
        }

        private void FinishReplacement(string status)
        {
            ObserveException(_replacementATask);
            ObserveException(_replacementBTask);
            _replacementPhase = ReplacementPhase.None;
            _replacementATask = null;
            _replacementBTask = null;
            _replacementSecondsRemaining = 0f;
            StatusLabel = status;
        }

        private static bool IsSettled(Task task)
        {
            if (task == null || !task.IsCompleted)
            {
                return task == null;
            }

            ObserveException(task);
            return true;
        }

        private static void ObserveException(Task task)
        {
            if (task != null && task.IsFaulted)
            {
                _ = task.Exception;
            }
        }

        private void ReadCommands()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                TryStartSlowRound();
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                TryStartFailure();
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                TryStartReplacement();
            }
        }

        private void OnDestroy()
        {
            StopAndCancel();
        }
    }
}
