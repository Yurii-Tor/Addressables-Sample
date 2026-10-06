using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using AddressablesSample.Game.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class DemoScenarioTests
    {
        private Fixture _fixture;
        private float _previousTimeScale;

        [SetUp]
        public void SetUp() => _previousTimeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            _fixture?.Dispose();
        }

        [Test, Timeout(5000)]
        public async Task Loader_FiltersConfiguredRoundKeysAndConsumesOneShotAfterDispatch()
        {
            var texture = new Texture2D(1, 1) { name = "round-a" };
            var inner = new ControlledLoader();
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            Assert.That(loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery), Is.True);

            var fallback = loader.StartLoad<Texture2D>("fallback");
            var wrongType = loader.StartLoad<GameObject>("round-a");
            var otherTexture = loader.StartLoad<Texture2D>("startup-texture");
            Assert.That(inner.Requests.Count, Is.EqualTo(3));
            Assert.That(loader.HasArmedInstruction, Is.True);
            Assert.That(clock.Requests.Count, Is.Zero);
            fallback.Dispose();
            wrongType.Dispose();
            otherTexture.Dispose();

            var delayed = loader.StartLoad<Texture2D>("round-a");
            var rawOwner = inner.At<Texture2D>(3);
            Assert.That(loader.HasArmedInstruction, Is.False);
            Assert.That(inner.ActiveOwners, Is.EqualTo(1), "the wrapper must not add a second owner");
            Assert.That(delayed, Is.Not.SameAs(rawOwner));
            rawOwner.CompleteSuccess(texture);
            await WaitUntilAsync(() => clock.PendingCount == 1);
            Assert.That(clock.Requests[0].Delay.TotalSeconds, Is.GreaterThanOrEqualTo(1.5));
            Assert.That(delayed.Completion.IsCompleted, Is.False);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Assert.That(delayed.Completion.IsCompleted, Is.False);
            clock.Advance(TimeSpan.FromSeconds(0.5));
            var result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Succeeded));
            Assert.That(result.Asset, Is.SameAs(texture));
            Assert.That(inner.ActiveOwners, Is.EqualTo(1), "the inner owner stays alive through delivery");
            delayed.Dispose();
            Assert.That(rawOwner.DisposeCount, Is.EqualTo(1));
            Assert.That(inner.ActiveOwners, Is.Zero);

            var next = loader.StartLoad<Texture2D>("round-b");
            Assert.That(next, Is.SameAs(inner.Requests[4].Owner), "the instruction must not leak to another round");
            next.Dispose();
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test, Timeout(5000)]
        public async Task DelayedLoad_DisposeBeforeInnerCompletionSettlesCanceledAndReleasesOnce()
        {
            var texture = new Texture2D(1, 1) { name = "late-a" };
            var inner = new ControlledLoader();
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery);
            var delayed = loader.StartLoad<Texture2D>("round-a");
            var rawOwner = inner.At<Texture2D>(0);

            delayed.Dispose();
            delayed.Dispose();
            var result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(result.Asset, Is.Null);
            Assert.That(rawOwner.DisposeCount, Is.EqualTo(1));
            Assert.That(inner.ActiveOwners, Is.Zero);
            Assert.That(clock.Requests.Count, Is.Zero);

            rawOwner.CompleteSuccess(texture); // hostile late success after release
            await Task.Yield();
            result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(result.Asset, Is.Null);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test, Timeout(5000)]
        public async Task DelayedLoad_DisposeDuringDeliveryDelaySettlesCanceledAndKeepsNoLateAsset()
        {
            var texture = new Texture2D(1, 1) { name = "held-a" };
            var inner = new ControlledLoader();
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery);
            var delayed = loader.StartLoad<Texture2D>("round-a");
            var rawOwner = inner.At<Texture2D>(0);
            rawOwner.CompleteSuccess(texture);
            await WaitUntilAsync(() => clock.PendingCount == 1);

            Assert.That(delayed.Completion.IsCompleted, Is.False);
            Assert.That(rawOwner.DisposeCount, Is.Zero);
            Assert.That(inner.ActiveOwners, Is.EqualTo(1));
            delayed.Dispose();
            delayed.Dispose();

            var result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(result.Asset, Is.Null);
            Assert.That(rawOwner.DisposeCount, Is.EqualTo(1));
            Assert.That(inner.ActiveOwners, Is.Zero);
            clock.Advance(TimeSpan.FromSeconds(5));
            result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(result.Asset, Is.Null);
            UnityEngine.Object.DestroyImmediate(texture);
        }
        [Test, Timeout(5000)]
        public async Task DelayedLoad_AlreadyCompletedInnerSuccessStillWaitsForInjectedClock()
        {
            var texture = new Texture2D(1, 1) { name = "already-loaded" };
            var immediate = new ImmediateLoader(texture);
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(immediate, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery);

            var delayed = loader.StartLoad<Texture2D>("round-a");
            await WaitUntilAsync(() => clock.PendingCount == 1);
            Assert.That(delayed.Completion.IsCompleted, Is.False);
            Assert.That(immediate.LastOwnerDisposeCount, Is.Zero);
            clock.Advance(TimeSpan.FromSeconds(DemoScenarioAssetLoader.SlowDeliverySeconds));
            var result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Succeeded));
            Assert.That(result.Asset, Is.SameAs(texture));
            delayed.Dispose();
            Assert.That(immediate.LastOwnerDisposeCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test, Timeout(5000)]
        public async Task DelayedLoad_CompletedFailureDoesNotWaitAndFaultedTaskIsObserved()
        {
            var inner = new ControlledLoader();
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery);
            var delayed = loader.StartLoad<Texture2D>("round-a");
            var rawOwner = inner.At<Texture2D>(0);
            var expected = new InvalidOperationException("Synthetic faulted completion.");
            rawOwner.FaultCompletion(expected);

            var result = await delayed.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(expected));
            Assert.That(clock.Requests.Count, Is.Zero);
            Assert.That(rawOwner.DisposeCount, Is.Zero, "the controller still owns the failed operation");
            delayed.Dispose();
            Assert.That(rawOwner.DisposeCount, Is.EqualTo(1));
            Assert.That(inner.ActiveOwners, Is.Zero);
        }

        [Test, Timeout(5000)]
        public async Task SyntheticFailure_IsExplicitAndAcquiresNoInnerOwner()
        {
            var inner = new ControlledLoader();
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.FailNextRound);

            var failure = loader.StartLoad<Texture2D>("round-a");
            var result = await failure.Completion;
            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Failed));
            Assert.That(result.Exception.Message, Does.Contain("Simulated failure"));
            Assert.That(result.Exception.Message, Does.Contain("no transport failure was measured"));
            Assert.That(loader.HasArmedInstruction, Is.False);
            Assert.That(inner.Requests.Count, Is.Zero);
            Assert.That(inner.ActiveOwners, Is.Zero);
            failure.Dispose();
            failure.Dispose();

            var next = loader.StartLoad<Texture2D>("round-b");
            Assert.That(next, Is.SameAs(inner.Requests[0].Owner));
            next.Dispose();
            Assert.That(inner.ActiveOwners, Is.Zero);
        }

        [Test, Timeout(5000)]
        public void SynchronousStartLoadThrow_ConsumesInstructionWithoutLeakingIt()
        {
            var inner = new ControlledLoader { ThrowForKey = "round-a" };
            var clock = new ManualDelayQueue();
            var loader = CreateLoader(inner, clock.DelayAsync, () => true);
            loader.TryArm(DemoScenarioInstruction.DelaySuccessfulDelivery);

            Assert.Throws<InvalidOperationException>(() => loader.StartLoad<Texture2D>("round-a"));
            Assert.That(loader.HasArmedInstruction, Is.False);
            Assert.That(inner.Requests.Count, Is.Zero);
            Assert.That(clock.Requests.Count, Is.Zero);

            inner.ThrowForKey = null;
            var ordinary = loader.StartLoad<Texture2D>("round-b");
            Assert.That(ordinary, Is.SameAs(inner.Requests[0].Owner));
            ordinary.Dispose();
        }
        [Test, Timeout(5000)]
        public async Task StartupAndRejectedCommand_DoNotConsumeOrLeakInstruction()
        {
            _fixture = new Fixture();
            Assert.That(_fixture.Commands.TryStartFailure(), Is.False, "commands are rejected before the first Ready state");
            Assert.That(_fixture.ScenarioLoader.HasArmedInstruction, Is.False);

            // A stale slot still cannot affect the configured first round during startup.
            _fixture.ScenarioLoader.TryArm(DemoScenarioInstruction.FailNextRound);
            await _fixture.ReachReadyAsync();
            Assert.That(_fixture.ScenarioLoader.HasArmedInstruction, Is.False);
            Assert.That(_fixture.Inner.Requests.Count, Is.EqualTo(3));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.InitialRound));

            _fixture.Controller.HandleSelection(true);
            var next = _fixture.Inner.At<Texture2D>(3);
            var completion = _fixture.Controller.ActiveRoundTask;
            next.CompleteSuccess(_fixture.RoundB);
            await completion;
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(_fixture.Controller.Score, Is.EqualTo(1));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.RoundB));
            Assert.That(_fixture.Commands.PendingDelayCount, Is.Zero);
        }

        [Test, Timeout(5000)]
        public async Task SlowCommand_KeepsPreviousTextureAndUsesUnscaledManualTime()
        {
            _fixture = new Fixture();
            await _fixture.ReachReadyAsync();
            var displayedOwner = _fixture.Inner.At<Texture2D>(2);
            Assert.That(_fixture.Commands.TryStartSlowRound(), Is.True);
            var roundTask = _fixture.Controller.ActiveRoundTask;
            var delayedInner = _fixture.Inner.At<Texture2D>(3);

            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.LoadingRound));
            Assert.That(_fixture.Hud.Status, Is.EqualTo(GameStatusText.LoadingImage));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.InitialRound));
            Assert.That(displayedOwner.UnderlyingReleaseCount, Is.Zero);
            Assert.That(_fixture.Controller.Score, Is.Zero);
            Assert.That(_fixture.Commands.TryStartFailure(), Is.False, "a second command cannot overlap the held delivery");
            delayedInner.CompleteSuccess(_fixture.RoundB);
            await WaitUntilAsync(() => _fixture.Commands.PendingDelayCount == 1);
            Assert.That(_fixture.Commands.TryStartSlowRound(), Is.False, "repeated commands are rejected while active");

            Time.timeScale = 0f;
            _fixture.Commands.AdvanceTime(1.5f);
            Assert.That(roundTask.IsCompleted, Is.False);
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.LoadingRound));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.InitialRound));
            Assert.That(displayedOwner.UnderlyingReleaseCount, Is.Zero);

            _fixture.Commands.AdvanceTime(0.5f);
            await roundTask;
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.RoundB));
            Assert.That(displayedOwner.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_fixture.Controller.Score, Is.Zero);
            Assert.That(_fixture.Commands.StatusLabel, Does.Contain("not network timing"));
        }

        [Test, Timeout(5000)]
        public async Task SimulatedFailure_AppliesFallbackBeforePreviousOwnerReleaseThenNormalRoundRecovers()
        {
            _fixture = new Fixture();
            await _fixture.ReachReadyAsync();
            var displayedOwner = _fixture.Inner.At<Texture2D>(2);
            var prior = ReadRecords(_fixture.Controller, 0);
            var cursor = prior[prior.Count - 1].Sequence;
            _fixture.Events.Clear();

            Assert.That(_fixture.Commands.TryStartFailure(), Is.True);
            Assert.That(_fixture.Inner.Requests.Count, Is.EqualTo(3), "synthetic failure acquires no inner owner");
            var failureTask = _fixture.Controller.ActiveRoundTask;
            await failureTask;
            await WaitUntilAsync(() => _fixture.Controller.State == GameState.Ready);

            Assert.That(_fixture.Controller.Score, Is.Zero);
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.Fallback));
            Assert.That(_fixture.Hud.Status, Is.EqualTo(GameStatusText.ReadyWithFallback));
            Assert.That(displayedOwner.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(IndexOf(_fixture.Events, "apply:fallback"), Is.LessThan(IndexOf(_fixture.Events, "release:round-a")));
            Assert.That(_fixture.Commands.StatusLabel, Does.Contain("no transport request failed"));
            var outcomes = ReadRecords(_fixture.Controller, cursor);
            Assert.That(outcomes.Count, Is.EqualTo(1), "assert only the current failure phase, excluding startup fallback");
            Assert.That(outcomes[0].Outcome, Is.EqualTo(RoundOutcome.Fallback));

            _fixture.Controller.HandleSelection(true);
            var recovery = _fixture.Inner.At<Texture2D>(3);
            var recoveryTask = _fixture.Controller.ActiveRoundTask;
            recovery.CompleteSuccess(_fixture.RoundC);
            await recoveryTask;
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(_fixture.Controller.Score, Is.EqualTo(1));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.RoundC));
        }
        [Test, Timeout(5000)]
        public async Task Replacement_AIsSupersededBeforeBTerminalAndScoreStaysUnchanged()
        {
            _fixture = new Fixture();
            await _fixture.ReachReadyAsync();
            var prior = ReadRecords(_fixture.Controller, 0);
            var cursor = prior[prior.Count - 1].Sequence;
            Assert.That(_fixture.Commands.TryStartReplacement(), Is.True);
            var taskA = _fixture.Controller.ActiveRoundTask;
            var loadA = _fixture.Inner.At<Texture2D>(3);
            Assert.That(_fixture.Commands.TryStartReplacement(), Is.False, "a second sequence is rejected");
            Assert.That(_fixture.Inner.Requests.Count, Is.EqualTo(4));

            loadA.CompleteSuccess(_fixture.RoundB);
            await WaitUntilAsync(() => _fixture.Commands.PendingDelayCount == 1);
            _fixture.Commands.AdvanceTime(0.2f);
            Assert.That(_fixture.Inner.Requests.Count, Is.EqualTo(5));
            Assert.That(_fixture.Inner.At<Texture2D>(4).Key, Is.EqualTo(Fixture.RoundCKey));
            Assert.That(loadA.DisposeCallCount, Is.EqualTo(1));
            Assert.That(loadA.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.LoadingRound));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.InitialRound));
            Assert.That(_fixture.Controller.Score, Is.Zero);

            var taskB = _fixture.Controller.ActiveRoundTask;
            _fixture.Inner.At<Texture2D>(4).CompleteSuccess(_fixture.RoundC);
            await taskB;
            await taskA;
            _fixture.Commands.AdvanceTime(0f);
            Assert.That(_fixture.Commands.IsReplacementSequenceActive, Is.False);
            Assert.That(_fixture.Controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.RoundC));
            Assert.That(_fixture.Controller.Score, Is.Zero);
            Assert.That(_fixture.Commands.StatusLabel, Does.Contain("Transport cancellation is not measured"));

            var outcomes = ReadRecords(_fixture.Controller, cursor);
            Assert.That(outcomes.Count, Is.EqualTo(2));
            Assert.That(outcomes[0].Outcome, Is.EqualTo(RoundOutcome.Superseded));
            Assert.That(outcomes[1].Outcome, Is.EqualTo(RoundOutcome.Succeeded));
            Assert.That(outcomes[0].Sequence, Is.LessThan(outcomes[1].Sequence));
            _fixture.Commands.AdvanceTime(10f);
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.RoundC), "A cannot publish after its owner was released");

            _fixture.Controller.HandleSelection(true);
            var normalRound = _fixture.Inner.At<Texture2D>(5);
            var normalTask = _fixture.Controller.ActiveRoundTask;
            normalRound.CompleteSuccess(_fixture.InitialRound);
            await normalTask;
            Assert.That(_fixture.Controller.Score, Is.EqualTo(1));
            Assert.That(_fixture.Target.Texture, Is.SameAs(_fixture.InitialRound));
        }

        private static DemoScenarioAssetLoader CreateLoader(
            IAddressableAssetLoader inner,
            Func<TimeSpan, CancellationToken, Task> delay,
            Func<bool> canConsume)
        {
            return new DemoScenarioAssetLoader(
                inner,
                new object[] { "round-a", "round-b" },
                delay,
                canConsume);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 512; attempt++)
            {
                if (condition())
                {
                    return;
                }

                await Task.Yield();
            }

            Assert.Fail("The deterministic scenario operation did not settle.");
        }

        private static List<TerminalRoundRecord> ReadRecords(GameController controller, long afterSequence)
        {
            var records = new List<TerminalRoundRecord>();
            controller.ReadTerminalRounds(afterSequence, records);
            return records;
        }

        private static int IndexOf(IList<string> values, string value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
        private sealed class Fixture : IDisposable
        {
            public const string FallbackKey = "fallback";
            public const string PrefabKey = "prefab";
            public const string RoundAKey = "round-a";
            public const string RoundBKey = "round-b";
            public const string RoundCKey = "round-c";
            private bool _disposed;

            public Fixture()
            {
                Events = new List<string>();
                RootObject = new GameObject("P05 Scenario Test Bootstrapper");
                Bootstrapper = RootObject.AddComponent<GameBootstrapper>();
                Commands = RootObject.AddComponent<DemoScenarioCommands>();
                Inner = new FakeAddressableAssetLoader(Events);
                Fallback = CreateTexture("fallback");
                InitialRound = CreateTexture("round-a");
                RoundB = CreateTexture("round-b");
                RoundC = CreateTexture("round-c");
                Prefab = new GameObject("P05 Test Prefab");
                Target = new FakeTargetView(Events);
                Factory = new FakeTargetFactory(Target, Events);
                Hud = new FakeHud(Events);
                Diagnostics = new FakeDiagnostics(Events);

                var definition = new GameDefinition(
                    PrefabKey,
                    FallbackKey,
                    new object[] { RoundAKey, RoundBKey, RoundCKey });
                ScenarioLoader = new DemoScenarioAssetLoader(
                    Inner,
                    new object[] { RoundAKey, RoundBKey, RoundCKey },
                    Commands.DelayAsync,
                    () => Controller != null && Controller.CanAcceptScenarioCommands);
                Controller = new GameController(
                    new FakeConfiguration { Definition = definition },
                    ScenarioLoader,
                    Hud,
                    Factory,
                    Diagnostics);
                Bootstrapper.InstallScenarioServicesForTests(Controller, ScenarioLoader, Commands);
            }

            public List<string> Events { get; }
            public GameObject RootObject { get; }
            public GameBootstrapper Bootstrapper { get; }
            public DemoScenarioCommands Commands { get; }
            public FakeAddressableAssetLoader Inner { get; }
            public DemoScenarioAssetLoader ScenarioLoader { get; }
            public GameController Controller { get; }
            public FakeHud Hud { get; }
            public FakeTargetView Target { get; }
            public FakeTargetFactory Factory { get; }
            public FakeDiagnostics Diagnostics { get; }
            public Texture2D Fallback { get; }
            public Texture2D InitialRound { get; }
            public Texture2D RoundB { get; }
            public Texture2D RoundC { get; }
            public GameObject Prefab { get; }

            public async Task ReachReadyAsync()
            {
                var initialization = Controller.InitializeAsync();
                Inner.At<Texture2D>(0).CompleteSuccess(Fallback);
                await WaitUntilAsync(() => Inner.Requests.Count >= 2);
                Inner.At<GameObject>(1).CompleteSuccess(Prefab);
                await WaitUntilAsync(() => Inner.Requests.Count >= 3);
                Inner.At<Texture2D>(2).CompleteSuccess(InitialRound);
                await initialization;
                Assert.That(Controller.State, Is.EqualTo(GameState.Ready));
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (RootObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(RootObject);
                }

                Controller.Dispose();
                if (Prefab != null) UnityEngine.Object.DestroyImmediate(Prefab);
                if (Fallback != null) UnityEngine.Object.DestroyImmediate(Fallback);
                if (InitialRound != null) UnityEngine.Object.DestroyImmediate(InitialRound);
                if (RoundB != null) UnityEngine.Object.DestroyImmediate(RoundB);
                if (RoundC != null) UnityEngine.Object.DestroyImmediate(RoundC);
            }

            private static Texture2D CreateTexture(string name) => new Texture2D(1, 1) { name = name };
        }

        private sealed class ManualDelayQueue
        {
            internal sealed class Request
            {
                public Request(TimeSpan delay)
                {
                    Delay = delay;
                    Remaining = delay;
                    Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                public TimeSpan Delay { get; }
                public TimeSpan Remaining { get; set; }
                public TaskCompletionSource<bool> Completion { get; }
                public CancellationTokenRegistration Registration { get; set; }
            }

            private readonly List<Request> _requests = new List<Request>();
            public IReadOnlyList<Request> Requests => _requests;
            public int PendingCount => _requests.FindAll(request => !request.Completion.Task.IsCompleted).Count;

            public Task DelayAsync(TimeSpan delay, CancellationToken token)
            {
                if (token.IsCancellationRequested) return Task.FromCanceled(token);
                var request = new Request(delay);
                if (token.CanBeCanceled)
                {
                    request.Registration = token.Register(() => request.Completion.TrySetCanceled());
                }

                _requests.Add(request);
                return request.Completion.Task;
            }

            public void Advance(TimeSpan elapsed)
            {
                foreach (var request in _requests)
                {
                    if (!request.Completion.Task.IsCompleted)
                    {
                        request.Remaining -= elapsed;
                        if (request.Remaining <= TimeSpan.Zero) request.Completion.TrySetResult(true);
                    }

                    if (request.Completion.Task.IsCompleted) request.Registration.Dispose();
                }
            }
        }
        private sealed class ControlledLoader : IAddressableAssetLoader
        {
            internal sealed class Request
            {
                public Request(object key, Type assetType, object owner)
                {
                    Key = key;
                    AssetType = assetType;
                    Owner = owner;
                }

                public object Key { get; }
                public Type AssetType { get; }
                public object Owner { get; }
            }

            public List<Request> Requests { get; } = new List<Request>();
            public object ThrowForKey { get; set; }
            public int ActiveOwners { get; private set; }

            public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
            {
                if (Equals(runtimeKey, ThrowForKey))
                {
                    throw new InvalidOperationException("Synthetic StartLoad failure for " + runtimeKey + ".");
                }

                var owner = new ControlledLoad<T>(runtimeKey, () => ActiveOwners++, () => ActiveOwners--);
                Requests.Add(new Request(runtimeKey, typeof(T), owner));
                return owner;
            }

            public ControlledLoad<T> At<T>(int index) where T : UnityEngine.Object =>
                (ControlledLoad<T>)Requests[index].Owner;
        }

        private sealed class ControlledLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
        {
            private readonly TaskCompletionSource<AddressableLoadResult<T>> _completion =
                new TaskCompletionSource<AddressableLoadResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly Action _release;
            private bool _released;

            public ControlledLoad(object key, Action acquire, Action release)
            {
                Key = key;
                _release = release;
                acquire();
            }

            public object Key { get; }
            public Task<AddressableLoadResult<T>> Completion => _completion.Task;
            public int DisposeCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public void CompleteSuccess(T asset) => _completion.TrySetResult(AddressableLoadResult<T>.Succeeded(asset));
            public void CompleteFailure() => _completion.TrySetResult(AddressableLoadResult<T>.Failed(new InvalidOperationException("Synthetic failure.")));
            public void FaultCompletion(Exception exception) => _completion.TrySetException(exception);

            public void Dispose()
            {
                DisposeCount++;
                if (_released) return;
                _released = true;
                ReleaseCount++;
                _release();
                // Deliberately remain pending so a hostile late result can be completed after release.
            }
        }

        private sealed class ImmediateLoader : IAddressableAssetLoader
        {
            private readonly UnityEngine.Object _asset;
            private int _lastOwnerDisposeCount;

            public ImmediateLoader(UnityEngine.Object asset) => _asset = asset;
            public int LastOwnerDisposeCount => _lastOwnerDisposeCount;

            public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
            {
                return new ImmediateLoad<T>(_asset as T, () => _lastOwnerDisposeCount++);
            }
        }

        private sealed class ImmediateLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
        {
            private readonly Action _onDispose;

            public ImmediateLoad(T asset, Action onDispose)
            {
                _onDispose = onDispose;
                Completion = Task.FromResult(AddressableLoadResult<T>.Succeeded(asset));
            }

            public Task<AddressableLoadResult<T>> Completion { get; }
            public void Dispose() => _onDispose();
        }
    }
}
