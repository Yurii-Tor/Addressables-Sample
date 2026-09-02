using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class GameControllerTests
    {
        private Harness _harness;

        [SetUp]
        public void SetUp()
        {
            _harness = new Harness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test, Timeout(5000)]
        public async Task Initialization_LoadsStartupAssetsOnceThenInitialRound()
        {
            await _harness.ReachReadyAsync();

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(_harness.Controller.Score, Is.Zero);
            Assert.That(_harness.Hud.Score, Is.Zero);
            Assert.That(_harness.Hud.Status, Is.EqualTo(GameStatusText.Ready));
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(3));
            Assert.That(_harness.Loader.Requests[0].Key, Is.EqualTo(Harness.FallbackKey));
            Assert.That(_harness.Loader.Requests[1].Key, Is.EqualTo(Harness.PrefabKey));
            Assert.That(_harness.Loader.Requests[2].Key, Is.EqualTo(Harness.RoundAKey));
            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.RoundA));
            Assert.That(_harness.Target.InteractionEnabled, Is.True);

            await _harness.Controller.InitializeAsync();
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(3));
            Assert.That(_harness.Factory.CreateCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task CorrectSelection_IncrementsOnceAndSwapsBeforeReleasingPrevious()
        {
            await _harness.ReachReadyAsync();
            var initialRound = _harness.Loader.At<Texture2D>(2);
            _harness.Events.Clear();

            _harness.Controller.HandleSelection(true);

            Assert.That(_harness.Controller.Score, Is.EqualTo(1));
            Assert.That(_harness.Hud.Score, Is.EqualTo(1));
            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.LoadingRound));
            Assert.That(_harness.Hud.Status, Is.EqualTo(GameStatusText.LoadingImage));
            Assert.That(_harness.Target.InteractionEnabled, Is.False);
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(4));

            _harness.Controller.HandleSelection(true);
            Assert.That(_harness.Controller.Score, Is.EqualTo(1));
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(4));

            var replacement = _harness.Loader.At<Texture2D>(3);
            replacement.CompleteSuccess(_harness.RoundB);
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.RoundB));
            Assert.That(initialRound.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(IndexOf(_harness.Events, "apply:round-b"),
                Is.LessThan(IndexOf(_harness.Events, "release:round-a")));
        }

        [Test, Timeout(5000)]
        public async Task RoundTelemetry_AdvancesGenerationOnStartAndRecordsDurationOnSettle()
        {
            await _harness.ReachReadyAsync();

            var settledGeneration = _harness.Controller.RoundGeneration;
            var settledDuration = _harness.Controller.LastRoundLoadMilliseconds;
            Assert.That(settledGeneration, Is.GreaterThan(0));

            _harness.Controller.HandleSelection(true);

            // Starting a round must advance the generation immediately -- that is what lets a late
            // completion recognize itself as stale -- but it must not yet report a duration.
            Assert.That(_harness.Controller.RoundGeneration, Is.EqualTo(settledGeneration + 1));
            Assert.That(_harness.Controller.LastRoundLoadMilliseconds, Is.EqualTo(settledDuration));

            _harness.Loader.At<Texture2D>(3).CompleteSuccess(_harness.RoundB);
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(_harness.Controller.RoundGeneration, Is.EqualTo(settledGeneration + 1));
            Assert.That(_harness.Controller.LastRoundLoadMilliseconds, Is.GreaterThan(0d));
        }

        [Test, Timeout(5000)]
        public async Task FailedRound_StillRecordsItsDurationAlongsideTheFallback()
        {
            await _harness.ReachReadyAsync();
            _harness.Controller.HandleSelection(true);

            _harness.Loader.At<Texture2D>(3).CompleteFailure();
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(_harness.Hud.Status, Is.EqualTo(GameStatusText.ReadyWithFallback));
            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.Fallback));
            Assert.That(_harness.Controller.LastRoundLoadMilliseconds, Is.GreaterThan(0d));
        }

        [Test, Timeout(5000)]
        public async Task Miss_PreservesScoreTextureSelectorAndRequestCount()
        {
            await _harness.ReachReadyAsync();
            var requestCount = _harness.Loader.Requests.Count;
            var texture = _harness.Target.Texture;

            _harness.Controller.HandleSelection(false);

            Assert.That(_harness.Controller.Score, Is.Zero);
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(requestCount));
            Assert.That(_harness.Target.Texture, Is.SameAs(texture));
            Assert.That(_harness.Target.FlashCount, Is.EqualTo(1));
            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Ready));

            _harness.Controller.HandleSelection(true);
            Assert.That(_harness.Loader.Requests[3].Key, Is.EqualTo(Harness.RoundBKey));
        }

        [Test, Timeout(5000)]
        public async Task CurrentRoundFailure_AppliesFallbackBeforeReleasingCurrent()
        {
            await _harness.ReachReadyAsync();
            var initialRound = _harness.Loader.At<Texture2D>(2);
            _harness.Events.Clear();

            _harness.Controller.HandleSelection(true);
            var failed = _harness.Loader.At<Texture2D>(3);
            failed.CompleteFailure();
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.Fallback));
            Assert.That(_harness.Hud.Status, Is.EqualTo(GameStatusText.ReadyWithFallback));
            Assert.That(_harness.Diagnostics.Warnings.Count, Is.EqualTo(1));
            Assert.That(failed.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(initialRound.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(IndexOf(_harness.Events, "apply:fallback"),
                Is.LessThan(IndexOf(_harness.Events, "release:round-a")));
        }

        [Test, Timeout(5000)]
        public async Task PendingReplacement_InvalidatesOldSuccessAndFailure()
        {
            await _harness.ReachReadyAsync();
            _harness.Controller.HandleSelection(true);
            var replaced = _harness.Loader.At<Texture2D>(3);

            _harness.Controller.BeginRound();
            var current = _harness.Loader.At<Texture2D>(4);
            Assert.That(replaced.UnderlyingReleaseCount, Is.EqualTo(1));

            replaced.CompleteSuccess(_harness.RoundB);
            current.CompleteSuccess(_harness.RoundC);
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.RoundC));
            Assert.That(_harness.Diagnostics.Warnings, Is.Empty);
            Assert.That(_harness.Events, Does.Not.Contain("apply:round-b"));
        }

        [Test, Timeout(5000)]
        public async Task SuccessQueuedBeforeReplacement_CannotApplyStaleTexture()
        {
            await _harness.ReachReadyAsync();
            _harness.Controller.HandleSelection(true);
            var replaced = _harness.Loader.At<Texture2D>(3);

            replaced.CompleteSuccess(_harness.RoundB);
            _harness.Controller.BeginRound();
            var current = _harness.Loader.At<Texture2D>(4);
            current.CompleteSuccess(_harness.RoundC);
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(replaced.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.RoundC));
            Assert.That(_harness.Events, Does.Not.Contain("apply:round-b"));
        }

        [Test, Timeout(5000)]
        public async Task ThreeOutOfOrderRequests_OnlyLatestCanApply()
        {
            var initialization = await _harness.ReachInitialRoundAsync();
            var first = _harness.Loader.At<Texture2D>(2);
            _harness.Events.Clear();

            _harness.Controller.BeginRound();
            var second = _harness.Loader.At<Texture2D>(3);
            _harness.Controller.BeginRound();
            var third = _harness.Loader.At<Texture2D>(4);

            first.CompleteSuccess(_harness.RoundA);
            second.CompleteFailure();
            third.CompleteSuccess(_harness.RoundC);
            await initialization;
            await WaitUntilAsync(() => _harness.Controller.State == GameState.Ready);

            Assert.That(first.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(second.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Target.Texture, Is.SameAs(_harness.RoundC));
            Assert.That(_harness.Events, Does.Not.Contain("apply:round-a"));
            Assert.That(_harness.Events, Does.Not.Contain("apply:round-b"));
            Assert.That(_harness.Diagnostics.Warnings, Is.Empty);
        }

        [Test, Timeout(5000)]
        public async Task InputOutsideReady_IsIgnored()
        {
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);

            _harness.Controller.HandleSelection(true);
            _harness.Controller.HandleSelection(false);
            Assert.That(_harness.Controller.Score, Is.Zero);
            Assert.That(_harness.Target.FlashCount, Is.Zero);
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(1));

            _harness.Controller.Dispose();
            _harness.Controller.HandleSelection(true);
            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(1));
            await initialization;
        }

        [Test, Timeout(5000)]
        public async Task InvalidConfiguration_EntersFatalWithoutLoading()
        {
            _harness.Configuration.Definition = null;
            _harness.Configuration.Error = "invalid";

            await _harness.Controller.InitializeAsync();

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.FatalError));
            Assert.That(_harness.Loader.Requests, Is.Empty);
            Assert.That(_harness.Hud.Status, Is.EqualTo(GameStatusText.FatalError));
            Assert.That(_harness.Diagnostics.Errors.Count, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task FallbackFailure_ReleasesOwnerAndStopsStartup()
        {
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);
            var fallback = _harness.Loader.At<Texture2D>(0);

            fallback.CompleteFailure();
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.FatalError));
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task PrefabFailure_ReleasesPrefabAndFallback()
        {
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);
            var fallback = _harness.Loader.At<Texture2D>(0);
            fallback.CompleteSuccess(_harness.Fallback);
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 2);
            var prefab = _harness.Loader.At<GameObject>(1);

            prefab.CompleteFailure();
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.FatalError));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task FactoryFailure_ReleasesBothStartupOwners()
        {
            _harness.Factory.ThrowOnCreate = true;
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);
            var fallback = _harness.Loader.At<Texture2D>(0);
            fallback.CompleteSuccess(_harness.Fallback);
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 2);
            var prefab = _harness.Loader.At<GameObject>(1);

            prefab.CompleteSuccess(_harness.Prefab);
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.FatalError));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task TextureApplicationFailure_EntersFatalWithoutLeakingOwners()
        {
            await _harness.ReachReadyAsync();
            var fallback = _harness.Loader.At<Texture2D>(0);
            var prefab = _harness.Loader.At<GameObject>(1);
            var initial = _harness.Loader.At<Texture2D>(2);
            _harness.Target.ThrowWhenApplying = _harness.RoundB;

            _harness.Controller.HandleSelection(true);
            var candidate = _harness.Loader.At<Texture2D>(3);
            candidate.CompleteSuccess(_harness.RoundB);
            await WaitUntilAsync(() => _harness.Controller.State == GameState.FatalError);

            Assert.That(candidate.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(initial.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Factory.DestroyCount, Is.EqualTo(1));
            Assert.That(IndexOf(_harness.Events, "destroy-target"),
                Is.LessThan(IndexOf(_harness.Events, "release:round-b")));
        }

        [Test, Timeout(5000)]
        public async Task FallbackApplicationFailure_EntersFatalWithoutLeakingOwners()
        {
            await _harness.ReachReadyAsync();
            var fallback = _harness.Loader.At<Texture2D>(0);
            var prefab = _harness.Loader.At<GameObject>(1);
            var current = _harness.Loader.At<Texture2D>(2);
            _harness.Target.ThrowWhenApplying = _harness.Fallback;

            _harness.Controller.HandleSelection(true);
            var failed = _harness.Loader.At<Texture2D>(3);
            failed.CompleteFailure();
            await WaitUntilAsync(() => _harness.Controller.State == GameState.FatalError);

            Assert.That(failed.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(current.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Factory.DestroyCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task DisposeDuringPendingLoad_IsIdempotentAndBlocksLateMutation()
        {
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);
            var fallback = _harness.Loader.At<Texture2D>(0);

            _harness.Controller.Dispose();
            _harness.Controller.Dispose();
            fallback.CompleteSuccess(_harness.Fallback);
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Loader.Requests.Count, Is.EqualTo(1));
            Assert.That(_harness.Factory.CreateCount, Is.Zero);
        }

        [Test, Timeout(5000)]
        public async Task DisposeDuringPrefabLoad_ReleasesFallbackAndPendingPrefab()
        {
            var initialization = _harness.Controller.InitializeAsync();
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 1);
            var fallback = _harness.Loader.At<Texture2D>(0);
            fallback.CompleteSuccess(_harness.Fallback);
            await WaitUntilAsync(() => _harness.Loader.Requests.Count == 2);
            var prefab = _harness.Loader.At<GameObject>(1);

            _harness.Controller.Dispose();
            prefab.CompleteSuccess(_harness.Prefab);
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Factory.CreateCount, Is.Zero);
        }

        [Test, Timeout(5000)]
        public async Task DisposeDuringInitialRound_ReleasesAllOwners()
        {
            var initialization = await _harness.ReachInitialRoundAsync();
            var fallback = _harness.Loader.At<Texture2D>(0);
            var prefab = _harness.Loader.At<GameObject>(1);
            var round = _harness.Loader.At<Texture2D>(2);

            _harness.Controller.Dispose();
            round.CompleteSuccess(_harness.RoundA);
            await initialization;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(round.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Factory.DestroyCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task DisposeDuringReplacement_ReleasesPendingAndDisplayedOwners()
        {
            await _harness.ReachReadyAsync();
            var fallback = _harness.Loader.At<Texture2D>(0);
            var prefab = _harness.Loader.At<GameObject>(1);
            var current = _harness.Loader.At<Texture2D>(2);
            _harness.Controller.HandleSelection(true);
            var pending = _harness.Loader.At<Texture2D>(3);

            _harness.Controller.Dispose();
            pending.CompleteSuccess(_harness.RoundB);
            await _harness.Controller.ActiveRoundTask;

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(pending.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(current.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(_harness.Target.Texture, Is.Null);
        }

        [Test, Timeout(5000)]
        public async Task ReadyTeardown_DestroysTargetBeforeReleasingOwnersInOrder()
        {
            await _harness.ReachReadyAsync();
            var fallback = _harness.Loader.At<Texture2D>(0);
            var prefab = _harness.Loader.At<GameObject>(1);
            var current = _harness.Loader.At<Texture2D>(2);
            _harness.Events.Clear();

            _harness.Controller.Dispose();

            Assert.That(_harness.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(current.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(fallback.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(prefab.UnderlyingReleaseCount, Is.EqualTo(1));
            Assert.That(IndexOf(_harness.Events, "destroy-target"),
                Is.LessThan(IndexOf(_harness.Events, "release:round-a")));
            Assert.That(IndexOf(_harness.Events, "release:round-a"),
                Is.LessThan(IndexOf(_harness.Events, "release:fallback")));
            Assert.That(IndexOf(_harness.Events, "release:fallback"),
                Is.LessThan(IndexOf(_harness.Events, "release:target-prefab")));
        }

        private static int IndexOf(IList<string> events, string value)
        {
            var index = events.IndexOf(value);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Missing event: " + value);
            return index;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            {
                await Task.Yield();
            }

            Assert.That(condition(), Is.True, "Timed out waiting for asynchronous controller state.");
        }

        private sealed class Harness : IDisposable
        {
            public const string PrefabKey = "target-prefab";
            public const string FallbackKey = "fallback";
            public const string RoundAKey = "round-a";
            public const string RoundBKey = "round-b";
            public const string RoundCKey = "round-c";

            public Harness()
            {
                Events = new List<string>();
                Fallback = CreateTexture("fallback");
                RoundA = CreateTexture("round-a");
                RoundB = CreateTexture("round-b");
                RoundC = CreateTexture("round-c");
                Prefab = new GameObject("target-prefab");
                Configuration = new FakeConfiguration
                {
                    Definition = new GameDefinition(
                        PrefabKey,
                        FallbackKey,
                        new object[] { RoundAKey, RoundBKey, RoundCKey })
                };
                Loader = new FakeAddressableAssetLoader(Events);
                Hud = new FakeHud(Events);
                Target = new FakeTargetView(Events);
                Factory = new FakeTargetFactory(Target, Events);
                Diagnostics = new FakeDiagnostics(Events);
                Controller = new GameController(Configuration, Loader, Hud, Factory, Diagnostics);
            }

            public List<string> Events { get; }
            public Texture2D Fallback { get; }
            public Texture2D RoundA { get; }
            public Texture2D RoundB { get; }
            public Texture2D RoundC { get; }
            public GameObject Prefab { get; }
            public FakeConfiguration Configuration { get; }
            public FakeAddressableAssetLoader Loader { get; }
            public FakeHud Hud { get; }
            public FakeTargetView Target { get; }
            public FakeTargetFactory Factory { get; }
            public FakeDiagnostics Diagnostics { get; }
            public GameController Controller { get; }

            public async Task<Task> ReachInitialRoundAsync()
            {
                var initialization = Controller.InitializeAsync();
                await WaitUntilAsync(() => Loader.Requests.Count == 1);
                Loader.At<Texture2D>(0).CompleteSuccess(Fallback);
                await WaitUntilAsync(() => Loader.Requests.Count == 2);
                Loader.At<GameObject>(1).CompleteSuccess(Prefab);
                await WaitUntilAsync(() => Loader.Requests.Count == 3);
                return initialization;
            }

            public async Task ReachReadyAsync()
            {
                var initialization = await ReachInitialRoundAsync();
                Loader.At<Texture2D>(2).CompleteSuccess(RoundA);
                await initialization;
                await WaitUntilAsync(() => Controller.State == GameState.Ready);
            }

            public void Dispose()
            {
                Controller.Dispose();
                UnityEngine.Object.DestroyImmediate(Prefab);
                UnityEngine.Object.DestroyImmediate(Fallback);
                UnityEngine.Object.DestroyImmediate(RoundA);
                UnityEngine.Object.DestroyImmediate(RoundB);
                UnityEngine.Object.DestroyImmediate(RoundC);
            }

            private static Texture2D CreateTexture(string name)
            {
                var texture = new Texture2D(2, 2) { name = name };
                return texture;
            }
        }
    }
}
