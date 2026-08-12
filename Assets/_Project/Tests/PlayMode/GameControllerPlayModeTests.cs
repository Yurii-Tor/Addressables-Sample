using System;
using System.Collections;
using System.Linq;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using AddressablesSample.Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AddressablesSample.Game.Tests.PlayMode
{
    public sealed class GameControllerPlayModeTests
    {
        private GameController _controllerForCleanup;
        private GameObject _spawnForCleanup;
        private DelayedFixture _fixtureForCleanup;

        [UnitySetUp]
        public IEnumerator PrepareIsolatedScene()
        {
            yield return IsolateScene();
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable()
        {
            var spawnObject = new GameObject("Invalid Round Test Spawn");
            var hud = new RecordingHud();
            var definition = new GameDefinition(
                "game/target",
                "game/fallback",
                new object[] { "game/round/not-found", "game/round/ant" });
            var factory = new CapturingTargetFactory(new UnityTargetFactory(spawnObject.transform));
            var controller = new GameController(
                new DefinitionConfiguration(definition),
                new UnityAddressableAssetLoader(),
                hud,
                factory,
                new UnityGameDiagnostics());
            _controllerForCleanup = controller;
            _spawnForCleanup = spawnObject;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "Round texture load failed; the retained fallback is in use\\."));
            var initialization = controller.InitializeAsync();
            yield return WaitForCondition(
                () => initialization.IsCompleted,
                "The real-loader invalid-key controller did not finish initialization.");

            Assert.That(initialization.IsFaulted, Is.False);
            Assert.That(controller.State, Is.EqualTo(GameState.Ready));
            Assert.That(controller.Score, Is.Zero);
            Assert.That(hud.Status, Is.EqualTo(GameStatusText.ReadyWithFallback));
            var target = UnityEngine.Object.FindFirstObjectByType<TargetView>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.AppliedTexture, Is.Not.Null);
            Assert.That(target.AppliedTexture, Is.SameAs(factory.InitialTexture));
            Assert.That(target.AppliedTexture.name, Is.EqualTo("FallbackTexture"));
            Assert.That(target.TargetCollider.enabled, Is.True);
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(2));

            controller.Dispose();
            UnityEngine.Object.Destroy(spawnObject);
            yield return null;
            yield return null;
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DelayedAAfterB_OnlyBMutatesTheTargetAndEveryOwnerReleasesOnce()
        {
            var fixture = CreateDelayedController();
            _fixtureForCleanup = fixture;
            yield return CompleteInitialization(fixture);

            fixture.Controller.BeginRound();
            var requestA = fixture.Loader.At<Texture2D>(3);
            var taskA = fixture.Controller.ActiveRoundTask;
            fixture.Controller.BeginRound();
            var requestB = fixture.Loader.At<Texture2D>(4);
            Assert.That(requestA.ReleaseCount, Is.EqualTo(1));

            requestB.CompleteSuccess(fixture.RoundB);
            yield return WaitForCondition(
                () => fixture.Controller.State == GameState.Ready,
                "Replacement B did not reach Ready.");
            Assert.That(fixture.Target.Texture, Is.SameAs(fixture.RoundB));
            var mutationsAfterB = fixture.Target.MutationCount + fixture.Hud.MutationCount;

            requestA.CompleteSuccess(fixture.RoundA);
            yield return WaitForCondition(() => taskA.IsCompleted, "Late replacement A did not settle.");
            Assert.That(fixture.Target.Texture, Is.SameAs(fixture.RoundB));
            Assert.That(fixture.Target.MutationCount + fixture.Hud.MutationCount, Is.EqualTo(mutationsAfterB));

            fixture.Controller.Dispose();
            AssertEveryRequestReleasedOnce(fixture.Loader);
            fixture.DestroyAssets();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator DestroyBootstrapperDuringDelayedRound_PreventsLateMutationAndReleasesOnce()
        {
            var fixture = CreateDelayedController();
            _fixtureForCleanup = fixture;
            yield return CompleteInitialization(fixture);

            fixture.Controller.BeginRound();
            var pending = fixture.Loader.At<Texture2D>(3);
            var pendingTask = fixture.Controller.ActiveRoundTask;
            var bootstrapperObject = new GameObject("Bootstrapper Destruction Test");
            var bootstrapper = bootstrapperObject.AddComponent<GameBootstrapper>();
            bootstrapper.InstallControllerForTests(fixture.Controller);

            UnityEngine.Object.Destroy(bootstrapperObject);
            yield return null;
            Assert.That(fixture.Controller.State, Is.EqualTo(GameState.Disposed));
            Assert.That(pending.ReleaseCount, Is.EqualTo(1));
            var mutationsAfterDestroy = fixture.Target.MutationCount + fixture.Hud.MutationCount;

            pending.CompleteSuccess(fixture.RoundA);
            yield return WaitForCondition(() => pendingTask.IsCompleted, "The destroyed bootstrapper's pending task did not settle.");
            Assert.That(fixture.Target.MutationCount + fixture.Hud.MutationCount, Is.EqualTo(mutationsAfterDestroy));
            AssertEveryRequestReleasedOnce(fixture.Loader);
            fixture.DestroyAssets();
            LogAssert.NoUnexpectedReceived();
        }

        private static DelayedFixture CreateDelayedController()
        {
            var fixture = new DelayedFixture();
            fixture.Controller = new GameController(
                new DefinitionConfiguration(new GameDefinition(
                    "prefab",
                    "fallback",
                    new object[] { "initial", "round-a", "round-b" })),
                fixture.Loader,
                fixture.Hud,
                fixture.Factory,
                fixture.Diagnostics);
            return fixture;
        }

        private static IEnumerator CompleteInitialization(DelayedFixture fixture)
        {
            var initialization = fixture.Controller.InitializeAsync();
            fixture.Loader.At<Texture2D>(0).CompleteSuccess(fixture.Fallback);
            yield return WaitForCondition(() => fixture.Loader.Requests.Count >= 2, "Prefab request was not created.");
            fixture.Loader.At<GameObject>(1).CompleteSuccess(fixture.Prefab);
            yield return WaitForCondition(() => fixture.Loader.Requests.Count >= 3, "Initial round request was not created.");
            fixture.Loader.At<Texture2D>(2).CompleteSuccess(fixture.Initial);
            yield return WaitForCondition(
                () => initialization.IsCompleted && fixture.Controller.State == GameState.Ready,
                "Delayed controller initialization did not reach Ready.");
        }

        private static void AssertEveryRequestReleasedOnce(DelayedAddressableLoader loader)
        {
            foreach (var request in loader.Requests)
            {
                Assert.That(request.Owner.ReleaseCount, Is.EqualTo(1), "Release count for " + request.Key);
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            _controllerForCleanup?.Dispose();
            _fixtureForCleanup?.Controller?.Dispose();
            _fixtureForCleanup?.DestroyAssets();
            if (_spawnForCleanup != null)
            {
                UnityEngine.Object.Destroy(_spawnForCleanup);
            }

            yield return null;
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
            _controllerForCleanup = null;
            _spawnForCleanup = null;
            _fixtureForCleanup = null;
        }

        private static IEnumerator IsolateScene()
        {
            var empty = SceneManager.CreateScene("AddressablesSample Controller Test " + Guid.NewGuid());
            SceneManager.SetActiveScene(empty);
            var unloads = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded && scene.path ==
                                "Assets/_Project/Scenes/Game.unity")
                .Select(SceneManager.UnloadSceneAsync)
                .Where(operation => operation != null)
                .ToArray();
            foreach (var unload in unloads)
            {
                yield return unload;
            }

            yield return null;
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, string failureMessage)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, failureMessage);
        }

        private sealed class DelayedFixture
        {
            private bool _assetsDestroyed;
            public readonly DelayedAddressableLoader Loader = new DelayedAddressableLoader();
            public readonly RecordingHud Hud = new RecordingHud();
            public readonly RecordingTarget Target = new RecordingTarget();
            public readonly RecordingDiagnostics Diagnostics = new RecordingDiagnostics();
            public readonly Texture2D Fallback = NewTexture("fallback");
            public readonly Texture2D Initial = NewTexture("initial");
            public readonly Texture2D RoundA = NewTexture("round-a");
            public readonly Texture2D RoundB = NewTexture("round-b");
            public readonly GameObject Prefab = new GameObject("prefab");

            public DelayedFixture()
            {
                Factory = new RecordingTargetFactory(Target);
            }

            public RecordingTargetFactory Factory { get; }
            public GameController Controller { get; set; }

            public void DestroyAssets()
            {
                if (_assetsDestroyed)
                {
                    return;
                }

                _assetsDestroyed = true;
                UnityEngine.Object.DestroyImmediate(Fallback);
                UnityEngine.Object.DestroyImmediate(Initial);
                UnityEngine.Object.DestroyImmediate(RoundA);
                UnityEngine.Object.DestroyImmediate(RoundB);
                UnityEngine.Object.DestroyImmediate(Prefab);
            }

            private static Texture2D NewTexture(string name)
            {
                return new Texture2D(2, 2) { name = name };
            }
        }
    }
}
