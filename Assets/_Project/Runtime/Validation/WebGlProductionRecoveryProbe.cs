using System;
using System.Collections;
using System.Collections.Generic;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using AddressablesSample.Game.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AddressablesSample.Game.Validation
{
    /// <summary>
    /// Browser-only P01 evidence for the real Addressables and presentation path. The Editor
    /// creates two temporary scenes: the first has a non-addressable first round key, and the
    /// second has a non-addressable startup fallback key. No committed scene or content is used
    /// as a fault switch.
    /// </summary>
    public sealed class WebGlProductionRecoveryProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 30f;
        private const float SuccessEvidenceSeconds = 4f;

        private readonly List<string> _results = new List<string>();
        private bool _awaitingUserClick;
        private bool _userClickObserved;
        private bool _failed;
        private Texture2D _fallbackTexture;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            GameBootstrapper bootstrapper = null;
            HudView hud = null;
            TargetView target = null;
            PointerSelectionInput input = null;
            yield return WaitUntil(
                () =>
                {
                    bootstrapper = FindFirstObjectByType<GameBootstrapper>();
                    hud = FindFirstObjectByType<HudView>();
                    target = FindFirstObjectByType<TargetView>();
                    input = FindFirstObjectByType<PointerSelectionInput>();
                    return bootstrapper != null &&
                           bootstrapper.Controller != null &&
                           bootstrapper.Controller.State == GameState.Ready &&
                           hud != null &&
                           target != null &&
                           input != null;
                },
                "Missing-round scene did not reach Ready.");
            if (_failed)
            {
                yield break;
            }

            _fallbackTexture = target.AppliedTexture;
            if (_fallbackTexture == null ||
                hud.Status != GameStatusText.ReadyWithFallback ||
                target.TargetCollider == null ||
                !target.TargetCollider.enabled ||
                AddressableOwnershipDiagnostics.ActiveOwnerCount != 2)
            {
                Fail("Missing-round fallback was not visibly ready with exactly two real owners.");
                yield break;
            }

            Pass("real missing key shows the production fallback and enables the target");
            _awaitingUserClick = true;
            input.Selection += OnSelection;
            _results.Add("ACTION: Click the visible target normally to load the next round.");

            yield return WaitUntil(() => _userClickObserved, "No hit-target user click was observed.");
            if (_failed)
            {
                input.Selection -= OnSelection;
                yield break;
            }

            yield return WaitUntil(
                () => bootstrapper.Controller.State == GameState.Ready &&
                      bootstrapper.Controller.Score == 1 &&
                      hud.Status == GameStatusText.Ready &&
                      target != null &&
                      target.AppliedTexture != null &&
                      target.AppliedTexture != _fallbackTexture,
                "The normal round after the user click did not succeed.");
            input.Selection -= OnSelection;
            _awaitingUserClick = false;
            if (_failed)
            {
                yield break;
            }

            if (AddressableOwnershipDiagnostics.ActiveOwnerCount != 3)
            {
                Fail("Successful replacement did not retain exactly three real owners.");
                yield break;
            }

            Pass("normal pointer click loads the next Addressable round successfully");
            _results.Add("NEXT: The startup-fatal scene opens after this visible success evidence.");
            yield return new WaitForSecondsRealtime(SuccessEvidenceSeconds);

            var loadFatalScene = SceneManager.LoadSceneAsync(1, LoadSceneMode.Single);
            if (loadFatalScene == null)
            {
                Fail("Unity could not load the temporary startup-fatal scene.");
                yield break;
            }

            yield return loadFatalScene;
            yield return WaitForStartupFatal();
        }

        private void OnSelection(bool hitTarget)
        {
            if (_awaitingUserClick && hitTarget)
            {
                _userClickObserved = true;
                Debug.Log("P01_WEBGL_REAL_USER_CLICK: hit production target.");
            }
        }

        private IEnumerator WaitForStartupFatal()
        {
            GameBootstrapper bootstrapper = null;
            HudView hud = null;
            yield return WaitUntil(
                () =>
                {
                    bootstrapper = FindFirstObjectByType<GameBootstrapper>();
                    hud = FindFirstObjectByType<HudView>();
                    return bootstrapper != null &&
                           bootstrapper.Controller != null &&
                           bootstrapper.Controller.State == GameState.FatalError &&
                           hud != null;
                },
                "Startup-fatal scene did not reach FatalError.");
            if (_failed)
            {
                yield break;
            }

            if (hud.Status != GameStatusText.FatalError ||
                FindFirstObjectByType<TargetView>() != null ||
                AddressableOwnershipDiagnostics.ActiveOwnerCount != 0)
            {
                Fail("Startup failure did not show the fatal UI with every real owner released.");
                yield break;
            }

            Pass("real startup failure shows the fatal UI and releases every owner");
            Debug.Log("P01_WEBGL_REAL_COMPLETE: PASS");
        }

        private IEnumerator WaitUntil(Func<bool> condition, string timeoutMessage)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!condition())
            {
                Fail(timeoutMessage);
            }
        }

        private void Pass(string message)
        {
            _results.Add("PASS: " + message);
            Debug.Log("P01_WEBGL_REAL_PASS: " + message);
        }

        private void Fail(string message)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            _results.Add("FAIL: " + message);
            Debug.LogError("P01_WEBGL_REAL_FAIL: " + message);
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true
            };
            GUI.color = _failed ? Color.red : Color.white;
            GUI.Label(new Rect(32f, Screen.height - 190f, Screen.width - 64f, 170f),
                "P01 production Addressables + presentation probe\n" + string.Join("\n", _results),
                style);
        }
    }
}
