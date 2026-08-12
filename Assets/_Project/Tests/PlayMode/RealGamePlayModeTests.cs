using System;
using System.Collections;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using AddressablesSample.Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AddressablesSample.Game.Tests.PlayMode
{
    public sealed class RealGamePlayModeTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private InputTestFixture _inputFixture;

        [UnitySetUp]
        public IEnumerator PrepareInputAndScene()
        {
            yield return UnloadAllGeneratedScenes();
            _inputFixture = new InputTestFixture();
            _inputFixture.Setup();
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RealScene_MouseTouchMissMaterialAndUnload_WorkEndToEnd()
        {
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);

            var loadScene = SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Additive);
            Assert.That(loadScene, Is.Not.Null);
            yield return loadScene;

            GameBootstrapper bootstrapper = null;
            yield return WaitForCondition(
                () =>
                {
                    bootstrapper = UnityEngine.Object.FindFirstObjectByType<GameBootstrapper>();
                    return bootstrapper != null &&
                           bootstrapper.Controller != null &&
                           bootstrapper.Controller.State == GameState.Ready;
                },
                "The real game scene did not reach Ready.");

            var hud = UnityEngine.Object.FindFirstObjectByType<HudView>();
            var input = UnityEngine.Object.FindFirstObjectByType<PointerSelectionInput>();
            var target = UnityEngine.Object.FindFirstObjectByType<TargetView>();
            var camera = Camera.main;
            Assert.That(hud, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(target, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<TargetView>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(bootstrapper.Controller.Score, Is.Zero);
            Assert.That(hud.DisplayedScore, Is.Zero);
            Assert.That(hud.Status, Is.EqualTo(GameStatusText.Ready));
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(3));

            var renderer = target.TargetRenderer;
            var sharedMaterial = renderer.sharedMaterial;
            var firstTexture = target.AppliedTexture;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(firstTexture, Is.Not.Null);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(target.TargetCollider.enabled, Is.True);
            Assert.That(block.GetTexture(Shader.PropertyToID("_BaseMap")), Is.SameAs(firstTexture));

            var selectionObserved = false;
            var observedHit = false;
            var observedState = GameState.Uninitialized;
            var observedStatus = string.Empty;
            var observedInteraction = true;
            var observedFlash = false;
            var observedColor = Color.clear;
            Action<bool> observeSelection = hit =>
            {
                selectionObserved = true;
                observedHit = hit;
                observedState = bootstrapper.Controller.State;
                observedStatus = hud.Status;
                observedInteraction = target.TargetCollider.enabled;
                observedFlash = target.IsFlashing;
                renderer.GetPropertyBlock(block);
                observedColor = block.GetColor(Shader.PropertyToID("_BaseColor"));
            };
            input.Selection += observeSelection;

            var mouse = InputSystem.AddDevice<Mouse>();
            var targetScreenPosition = (Vector2)camera.WorldToScreenPoint(target.transform.position);
            Physics.SyncTransforms();
            var projectedPosition = camera.WorldToScreenPoint(target.TargetCollider.bounds.center);
            Assert.That(projectedPosition.z, Is.GreaterThan(0f));
            targetScreenPosition = projectedPosition;
            _inputFixture.Move(mouse.position, targetScreenPosition);
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            Assert.That(selectionObserved, Is.True);
            Assert.That(observedHit, Is.True);
            Assert.That(observedState, Is.EqualTo(GameState.LoadingRound));
            Assert.That(observedStatus, Is.EqualTo(GameStatusText.LoadingImage));
            Assert.That(observedInteraction, Is.False);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(1));
            _inputFixture.Release(mouse.leftButton);
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready,
                "The mouse-selected round did not return to Ready.");

            var mouseTexture = target.AppliedTexture;
            Assert.That(mouseTexture, Is.Not.Null.And.Not.SameAs(firstTexture));
            Assert.That(renderer.sharedMaterial, Is.SameAs(sharedMaterial));
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetTexture(Shader.PropertyToID("_BaseMap")), Is.SameAs(mouseTexture));
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(3));

            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            selectionObserved = false;
            _inputFixture.BeginTouch(1, targetScreenPosition, screen: touchscreen);
            yield return null;
            Assert.That(selectionObserved, Is.True);
            Assert.That(observedHit, Is.True);
            Assert.That(observedState, Is.EqualTo(GameState.LoadingRound));
            Assert.That(observedStatus, Is.EqualTo(GameStatusText.LoadingImage));
            Assert.That(observedInteraction, Is.False);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(2));
            _inputFixture.EndTouch(1, targetScreenPosition, screen: touchscreen);
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready,
                "The touch-selected round did not return to Ready.");

            var touchTexture = target.AppliedTexture;
            Assert.That(touchTexture, Is.Not.Null.And.Not.SameAs(mouseTexture));
            Assert.That(renderer.sharedMaterial, Is.SameAs(sharedMaterial));
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(3));

            var missPosition = new Vector2(-1000f, -1000f);
            Assert.That(input.EvaluateSelection(missPosition), Is.False);
            var taskBeforeMiss = bootstrapper.Controller.ActiveRoundTask;
            selectionObserved = false;
            _inputFixture.Move(mouse.position, missPosition);
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            Assert.That(selectionObserved, Is.True);
            Assert.That(observedHit, Is.False);
            Assert.That(observedFlash, Is.True);
            Assert.That(observedColor, Is.EqualTo(Color.red));
            _inputFixture.Release(mouse.leftButton);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(2));
            Assert.That(bootstrapper.Controller.ActiveRoundTask, Is.SameAs(taskBeforeMiss));
            Assert.That(target.AppliedTexture, Is.SameAs(touchTexture));
            Assert.That(target.IsFlashing, Is.True);
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(Shader.PropertyToID("_BaseColor")), Is.EqualTo(Color.red));
            Assert.That(block.GetTexture(Shader.PropertyToID("_BaseMap")), Is.SameAs(touchTexture));
            Assert.That(renderer.sharedMaterial, Is.SameAs(sharedMaterial));

            yield return WaitForCondition(() => !target.IsFlashing, "The miss feedback did not finish.");
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor(Shader.PropertyToID("_BaseColor")), Is.EqualTo(Color.white));
            Assert.That(block.GetTexture(Shader.PropertyToID("_BaseMap")), Is.SameAs(touchTexture));

            var gameScene = SceneManager.GetSceneByPath(GameScenePath);
            var retainedController = bootstrapper.Controller;
            var retainedTarget = target;
            input.Selection -= observeSelection;
            var unloadScene = SceneManager.UnloadSceneAsync(gameScene);
            Assert.That(unloadScene, Is.Not.Null);
            yield return unloadScene;
            yield return null;
            yield return null;

            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
            Assert.That(retainedController.State, Is.EqualTo(GameState.Disposed));
            Assert.That(retainedTarget == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator UnloadGeneratedScene()
        {
            foreach (var input in UnityEngine.Object.FindObjectsByType<PointerSelectionInput>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                input.enabled = false;
            }

            yield return UnloadAllGeneratedScenes();
            yield return null;
            yield return WaitForCondition(
                () => AddressableOwnershipDiagnostics.ActiveOwnerCount == 0,
                "Real-scene teardown left an Addressables owner active.");
            _inputFixture?.TearDown();
            _inputFixture = null;
        }

        private static IEnumerator UnloadAllGeneratedScenes()
        {
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded || scene.path != GameScenePath)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
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
    }
}
