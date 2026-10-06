using System;
using System.Collections;
using System.Collections.Generic;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using AddressablesSample.Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AddressablesSample.Game.Tests.PlayMode
{
    public sealed class DemoControlsPlayModeTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private InputTestFixture _inputFixture;

        [UnitySetUp]
        public IEnumerator PrepareInputAndScene()
        {
            yield return UnloadGeneratedSceneIfLoaded();
            _inputFixture = new InputTestFixture();
            _inputFixture.Setup();
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.Zero);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator GeneratedButtons_MouseAndTouchRunEveryScenarioAndDisabledControlsBlockWorldInput()
        {
            var loadScene = SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Additive);
            Assert.That(loadScene, Is.Not.Null);
            yield return loadScene;

            var bootstrapper = UnityEngine.Object.FindFirstObjectByType<GameBootstrapper>();
            var controls = UnityEngine.Object.FindFirstObjectByType<DemoControlsView>();
            var overlay = UnityEngine.Object.FindFirstObjectByType<DiagnosticsOverlay>();
            var input = UnityEngine.Object.FindFirstObjectByType<PointerSelectionInput>();
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            var uiModule = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(controls, Is.Not.Null);
            Assert.That(overlay, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(uiModule, Is.Not.Null);
            Assert.That(uiModule.gameObject, Is.SameAs(eventSystem.gameObject));
            Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None), Has.Length.EqualTo(1));

            Assert.That(controls.SlowLoadButton.GetComponentInChildren<Text>().text, Does.Contain("Slow load").And.Contain("[1]").And.Contain("2s simulated delivery"));
            Assert.That(controls.SimulateFailureButton.GetComponentInChildren<Text>().text, Does.Contain("Simulate failure").And.Contain("[2]").And.Contain("fallback"));
            Assert.That(controls.ReplaceRequestButton.GetComponentInChildren<Text>().text, Does.Contain("Replace request").And.Contain("[3]").And.Contain("supersedes"));
            Assert.That(controls.ToggleDiagnosticsButton.GetComponentInChildren<Text>().text, Is.EqualTo("Hide diagnostics"));
            Assert.That(controls.DiagnosticsVisible, Is.True);

            yield return WaitForCondition(
                () => bootstrapper.Controller != null && bootstrapper.Controller.State == GameState.Ready,
                "The generated controls scene did not reach Ready.");
            var target = UnityEngine.Object.FindFirstObjectByType<TargetView>();
            Assert.That(target, Is.Not.Null);
            yield return WaitForCondition(
                () => controls.SlowLoadButton.interactable,
                "Scenario controls were not enabled after startup.");

            var mouse = InputSystem.AddDevice<Mouse>();
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var score = bootstrapper.Controller.Score;
            var selectionCount = 0;
            var lastSelectionWasHit = false;
            Action<bool> observeSelection = hit =>
            {
                selectionCount++;
                lastSelectionWasHit = hit;
            };
            input.Selection += observeSelection;

            var generation = bootstrapper.Controller.RoundGeneration;
            yield return ClickMouse(controls.SlowLoadButton.GetComponent<RectTransform>(), mouse);
            Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1));
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score));
            Assert.That(selectionCount, Is.Zero, "A scenario button over empty world space must not create a world miss.");
            Assert.That(target.IsFlashing, Is.False);
            Assert.That(controls.SlowLoadButton.interactable, Is.False);
            Assert.That(controls.SimulateFailureButton.interactable, Is.False);
            Assert.That(controls.ReplaceRequestButton.interactable, Is.False);
            Assert.That(OverlayText(controls), Does.Contain("Simulated 2-second unscaled delivery delay; this is not network timing."));

            // A disabled button still consumes the press even though its location is empty world space.
            yield return ClickTouch(1, controls.SimulateFailureButton.GetComponent<RectTransform>(), touchscreen);
            Assert.That(selectionCount, Is.Zero);
            Assert.That(target.IsFlashing, Is.False);
            Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1));
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready && controls.SlowLoadButton.interactable,
                "The first delayed scenario did not settle.");

            generation = bootstrapper.Controller.RoundGeneration;
            yield return ClickTouch(2, controls.SlowLoadButton.GetComponent<RectTransform>(), touchscreen);
            Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1));
            Assert.That(selectionCount, Is.Zero);
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready && controls.SlowLoadButton.interactable,
                "The touch-delayed scenario did not settle.");

            for (var attempt = 0; attempt < 2; attempt++)
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "Round texture load failed; the retained fallback is in use\\."));
                generation = bootstrapper.Controller.RoundGeneration;
                if (attempt == 0)
                {
                    yield return ClickMouse(controls.SimulateFailureButton.GetComponent<RectTransform>(), mouse);
                }
                else
                {
                    yield return ClickTouch(3, controls.SimulateFailureButton.GetComponent<RectTransform>(), touchscreen);
                }

                yield return WaitForCondition(
                    () => bootstrapper.Controller.State == GameState.Ready && controls.SimulateFailureButton.interactable,
                    "The synthetic-failure scenario did not return to Ready.");
                Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1));
                Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score));
                Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(2));
                Assert.That(selectionCount, Is.Zero);
                Assert.That(OverlayText(controls), Does.Contain("Simulated failure injected; no transport request failed."));
            }

            var traceCursor = LatestSequence(bootstrapper.Controller);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                generation = bootstrapper.Controller.RoundGeneration;
                if (attempt == 0)
                {
                    yield return ClickMouse(controls.ReplaceRequestButton.GetComponent<RectTransform>(), mouse);
                }
                else
                {
                    yield return ClickTouch(4, controls.ReplaceRequestButton.GetComponent<RectTransform>(), touchscreen);
                }

                yield return WaitForCondition(
                    () => bootstrapper.Controller.State == GameState.Ready && controls.ReplaceRequestButton.interactable,
                    "The A-to-B replacement sequence did not settle.");
                Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 2), "A and B are distinct requests.");
                Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score));
                Assert.That(selectionCount, Is.Zero);
                Assert.That(OverlayText(controls), Does.Contain("Transport cancellation is not measured."));

                var outcomes = new List<TerminalRoundRecord>();
                Assert.That(bootstrapper.Controller.ReadTerminalRounds(traceCursor, outcomes), Is.Zero);
                Assert.That(outcomes, Has.Count.EqualTo(2));
                Assert.That(outcomes[0].Outcome, Is.EqualTo(RoundOutcome.Superseded));
                Assert.That(outcomes[1].Outcome, Is.EqualTo(RoundOutcome.Succeeded));
                traceCursor = outcomes[outcomes.Count - 1].Sequence;
            }

            Assert.That(overlay.TraceEntries.Count, Is.GreaterThan(0));
            Assert.That(OverlayText(controls), Does.Contain("Recent round outcomes").And.Contain("Superseded").And.Contain("Succeeded"));
            Assert.That(selectionCount, Is.Zero);
            Assert.That(lastSelectionWasHit, Is.False);
            input.Selection -= observeSelection;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ToggleHidesPanelWorldInputStillWorksAndInteractiveGraphicsBlockCubeHit()
        {
            var loadScene = SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Additive);
            Assert.That(loadScene, Is.Not.Null);
            yield return loadScene;

            var bootstrapper = UnityEngine.Object.FindFirstObjectByType<GameBootstrapper>();
            var controls = UnityEngine.Object.FindFirstObjectByType<DemoControlsView>();
            var input = UnityEngine.Object.FindFirstObjectByType<PointerSelectionInput>();
            var camera = Camera.main;
            Assert.That(bootstrapper, Is.Not.Null);
            Assert.That(controls, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            yield return WaitForCondition(
                () => bootstrapper.Controller != null && bootstrapper.Controller.State == GameState.Ready,
                "The generated controls scene did not reach Ready.");
            var target = UnityEngine.Object.FindFirstObjectByType<TargetView>();
            Assert.That(target, Is.Not.Null);

            var mouse = InputSystem.AddDevice<Mouse>();
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var selectionCount = 0;
            var lastSelectionWasHit = false;
            Action<bool> observeSelection = hit =>
            {
                selectionCount++;
                lastSelectionWasHit = hit;
            };
            input.Selection += observeSelection;

            // A disable/enable cycle must leave exactly one runtime click handler on each control.
            controls.enabled = false;
            controls.enabled = true;
            controls.enabled = false;
            controls.enabled = true;
            yield return null;

            yield return ClickMouse(controls.ToggleDiagnosticsButton.GetComponent<RectTransform>(), mouse);
            Assert.That(controls.DiagnosticsVisible, Is.False);
            Assert.That(controls.transform.Find("Safe Area/Demo Controls Panel").gameObject.activeSelf, Is.False);
            Assert.That(controls.ToggleDiagnosticsButton.GetComponentInChildren<Text>().text, Is.EqualTo("Show diagnostics"));

            var generation = bootstrapper.Controller.RoundGeneration;
            var score = bootstrapper.Controller.Score;
            var worldPosition = camera.WorldToScreenPoint(target.TargetCollider.bounds.center);
            yield return ClickTouch(1, worldPosition, touchscreen);
            Assert.That(selectionCount, Is.EqualTo(1));
            Assert.That(lastSelectionWasHit, Is.True);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score + 1));
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready,
                "The hidden-panel world hit did not settle.");
            Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1));

            var missPosition = new Vector2(-80f, -80f);
            yield return ClickMouse(missPosition, mouse);
            Assert.That(selectionCount, Is.EqualTo(2));
            Assert.That(lastSelectionWasHit, Is.False);
            Assert.That(target.IsFlashing, Is.True);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score + 1));
            yield return WaitForCondition(() => !target.IsFlashing, "Normal miss feedback did not finish.");

            yield return ClickTouch(2, controls.ToggleDiagnosticsButton.GetComponent<RectTransform>(), touchscreen);
            Assert.That(controls.DiagnosticsVisible, Is.True);
            Assert.That(controls.transform.Find("Safe Area/Demo Controls Panel").gameObject.activeSelf, Is.True);
            Assert.That(controls.ToggleDiagnosticsButton.GetComponentInChildren<Text>().text, Is.EqualTo("Hide diagnostics"));

            var backdrop = controls.transform.Find("Safe Area/Backdrop").GetComponent<Image>();
            var status = controls.transform.Find("Safe Area/Status").GetComponent<Text>();
            Assert.That(backdrop.raycastTarget, Is.False);
            Assert.That(status.raycastTarget, Is.False);

            // The full-screen decorative backdrop and open controls panel cannot cover the target.
            worldPosition = camera.WorldToScreenPoint(target.TargetCollider.bounds.center);
            yield return ClickMouse(worldPosition, mouse);
            Assert.That(selectionCount, Is.EqualTo(3));
            Assert.That(lastSelectionWasHit, Is.True);
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score + 2));
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready,
                "A normal hit with the panel open did not settle.");

            // Put the generated interactive graphic directly over the cube and verify the press
            // is consumed before PointerSelectionInput can report a hit or miss.
            var buttonRect = controls.SlowLoadButton.GetComponent<RectTransform>();
            var buttonPosition = UiScreenPosition(buttonRect);
            var originalTargetPosition = target.transform.position;
            var depth = Vector3.Distance(camera.transform.position, originalTargetPosition);
            target.transform.position = camera.ScreenToWorldPoint(new Vector3(buttonPosition.x, buttonPosition.y, depth));
            Physics.SyncTransforms();
            var projectedTargetPosition = camera.WorldToScreenPoint(target.TargetCollider.bounds.center);
            Assert.That(Vector2.Distance(
                    new Vector2(projectedTargetPosition.x, projectedTargetPosition.y),
                    buttonPosition),
                Is.LessThan(1f));
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(buttonPosition), out var hit, 100f), Is.True);
            Assert.That(hit.collider.GetComponentInParent<TargetView>(), Is.SameAs(target));

            generation = bootstrapper.Controller.RoundGeneration;
            score = bootstrapper.Controller.Score;
            var selectionsBeforeUiOverCube = selectionCount;
            yield return ClickTouch(3, buttonRect, touchscreen);
            Assert.That(selectionCount, Is.EqualTo(selectionsBeforeUiOverCube));
            Assert.That(bootstrapper.Controller.Score, Is.EqualTo(score));
            Assert.That(target.IsFlashing, Is.False);
            Assert.That(bootstrapper.Controller.RoundGeneration, Is.EqualTo(generation + 1), "Only the visible scenario command may start a request.");
            target.transform.position = originalTargetPosition;
            Physics.SyncTransforms();
            yield return WaitForCondition(
                () => bootstrapper.Controller.State == GameState.Ready,
                "The scenario button over the cube did not settle.");

            input.Selection -= observeSelection;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator TearDownGeneratedScene()
        {
            foreach (var input in UnityEngine.Object.FindObjectsByType<PointerSelectionInput>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                input.enabled = false;
            }

            yield return UnloadGeneratedSceneIfLoaded();
            yield return null;
            yield return WaitForCondition(
                () => AddressableOwnershipDiagnostics.ActiveOwnerCount == 0,
                "Generated controls teardown left an Addressables owner active.");
            _inputFixture?.TearDown();
            _inputFixture = null;
        }

        private IEnumerator ClickMouse(RectTransform target, Mouse mouse)
        {
            yield return ClickMouse(UiScreenPosition(target), mouse);
        }

        private IEnumerator ClickMouse(Vector2 screenPosition, Mouse mouse)
        {
            _inputFixture.Move(mouse.position, screenPosition);
            _inputFixture.Press(mouse.leftButton);
            yield return null;
            _inputFixture.Release(mouse.leftButton);
            yield return null;
        }

        private IEnumerator ClickTouch(int touchId, RectTransform target, Touchscreen touchscreen)
        {
            yield return ClickTouch(touchId, UiScreenPosition(target), touchscreen);
        }

        private IEnumerator ClickTouch(int touchId, Vector2 screenPosition, Touchscreen touchscreen)
        {
            _inputFixture.BeginTouch(touchId, screenPosition, screen: touchscreen);
            yield return null;
            _inputFixture.EndTouch(touchId, screenPosition, screen: touchscreen);
            yield return null;
        }

        private static Vector2 UiScreenPosition(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            return RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
        }

        private static string OverlayText(DemoControlsView controls)
        {
            return controls.transform.Find("Safe Area/Demo Controls Panel/Diagnostics Viewport/Diagnostics Text")
                .GetComponent<Text>().text;
        }

        private static long LatestSequence(GameController controller)
        {
            var records = new List<TerminalRoundRecord>();
            controller.ReadTerminalRounds(0, records);
            return records.Count == 0 ? 0 : records[records.Count - 1].Sequence;
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, string failureMessage)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, failureMessage);
        }

        private static IEnumerator UnloadGeneratedSceneIfLoaded()
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
    }
}
