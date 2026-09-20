using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Validation
{
    /// <summary>
    /// A build-only P01 probe for the controller's exception-recovery paths. The Editor creates
    /// a temporary scene containing this component, builds it with the same production WebGL
    /// settings as the demo, then removes that scene again. It is not attached to Game.unity.
    /// </summary>
    public sealed class WebGlExceptionRecoveryHarness : MonoBehaviour
    {
        private const string FallbackKey = "harness/fallback";
        private const string PrefabKey = "harness/target";
        private const string ThrowingRoundKey = "harness/round/explicit-throw";
        private const string MissingRoundKey = "harness/round/missing";
        private const string SuccessfulRoundKey = "harness/round/success";
        private const string UnusedRoundKey = "harness/round/unused";

        private readonly List<string> _results = new List<string>();
        private bool _finished;
        private bool _passed;

        private async void Start()
        {
            var passed = true;
            passed &= await RunProbe("explicit throw reaches round recovery", VerifyExplicitThrowRecoveryAsync);
            passed &= await RunProbe("missing key uses fallback then next round succeeds", VerifyMissingKeyRecoveryAsync);
            passed &= await RunProbe("startup throw reaches fatal UI and cleanup", VerifyStartupFailureAsync);

            _passed = passed;
            _finished = true;
            Debug.Log(passed
                ? "P01_WEBGL_HARNESS_COMPLETE: PASS"
                : "P01_WEBGL_HARNESS_COMPLETE: FAIL");
        }

        private async Task<bool> RunProbe(string name, Func<Task> probe)
        {
            try
            {
                await probe();
                _results.Add("PASS: " + name);
                Debug.Log("P01_WEBGL_HARNESS_PASS: " + name);
                return true;
            }
            catch (Exception exception)
            {
                _results.Add("FAIL: " + name + " — " + exception.Message);
                Debug.LogError("P01_WEBGL_HARNESS_FAIL: " + name + "\n" + exception);
                return false;
            }
        }

        private static async Task VerifyExplicitThrowRecoveryAsync()
        {
            var fallback = CreateTexture("explicit-throw-fallback", Color.yellow);
            var prefab = CreatePrefab("explicit-throw-prefab");
            var loader = new ScriptedLoader(
                ScriptedLoad.Succeeded<Texture2D>(FallbackKey, fallback),
                ScriptedLoad.Succeeded<GameObject>(PrefabKey, prefab),
                ScriptedLoad.Throws<Texture2D>(ThrowingRoundKey, "P01 controlled explicit round exception."));
            var hud = new RecordingHud();
            var target = new RecordingTarget();
            var factory = new RecordingTargetFactory(target);
            var controller = CreateController(loader, hud, factory, ThrowingRoundKey, UnusedRoundKey);

            await controller.InitializeAsync();

            Require(controller.State == GameState.Ready, "The thrown round did not return to Ready.");
            Require(hud.Status == GameStatusText.ReadyWithFallback, "The thrown round did not report fallback readiness.");
            Require(ReferenceEquals(target.Texture, fallback), "The fallback texture was not applied after the explicit throw.");
            Require(target.InteractionEnabled, "The target did not become selectable after the explicit throw.");

            controller.Dispose();
            Require(factory.DestroyCount == 1, "The recovered target was not destroyed during cleanup.");
            Require(loader.AllOwnersReleasedOnce, "Explicit-throw recovery leaked or double-released an owner.");
            DestroyAssets(fallback, prefab);
        }

        private static async Task VerifyMissingKeyRecoveryAsync()
        {
            var fallback = CreateTexture("missing-key-fallback", Color.yellow);
            var successfulRound = CreateTexture("next-round-success", Color.green);
            var prefab = CreatePrefab("missing-key-prefab");
            var loader = new ScriptedLoader(
                ScriptedLoad.Succeeded<Texture2D>(FallbackKey, fallback),
                ScriptedLoad.Succeeded<GameObject>(PrefabKey, prefab),
                ScriptedLoad.Failed<Texture2D>(MissingRoundKey, "No location found for Key=harness/round/missing."),
                ScriptedLoad.Succeeded<Texture2D>(SuccessfulRoundKey, successfulRound));
            var hud = new RecordingHud();
            var target = new RecordingTarget();
            var factory = new RecordingTargetFactory(target);
            var controller = CreateController(loader, hud, factory, MissingRoundKey, SuccessfulRoundKey);

            await controller.InitializeAsync();

            Require(controller.State == GameState.Ready, "The missing-key round did not settle.");
            Require(hud.Status == GameStatusText.ReadyWithFallback, "The missing-key round did not show fallback readiness.");
            Require(ReferenceEquals(target.Texture, fallback), "The missing-key round did not display the fallback texture.");
            Require(target.InteractionEnabled, "The target was not selectable after the missing-key fallback.");

            controller.HandleSelection(true);
            await controller.ActiveRoundTask;

            Require(controller.State == GameState.Ready, "The normal round after the missing key did not return to Ready.");
            Require(hud.Status == GameStatusText.Ready, "The normal round after the missing key did not clear fallback status.");
            Require(ReferenceEquals(target.Texture, successfulRound), "The next normal round did not apply its texture.");
            Require(target.InteractionEnabled, "The next normal round did not restore interaction.");
            Require(controller.Score == 1, "The next normal interaction did not register exactly once.");

            controller.Dispose();
            Require(factory.DestroyCount == 1, "The recovered target was not destroyed during cleanup.");
            Require(loader.AllOwnersReleasedOnce, "Missing-key recovery leaked or double-released an owner.");
            DestroyAssets(fallback, successfulRound, prefab);
        }

        private static async Task VerifyStartupFailureAsync()
        {
            var loader = new ScriptedLoader(
                ScriptedLoad.Throws<Texture2D>(FallbackKey, "P01 controlled explicit startup exception."));
            var hud = new RecordingHud();
            var factory = new RecordingTargetFactory(new RecordingTarget());
            var controller = CreateController(loader, hud, factory, ThrowingRoundKey, UnusedRoundKey);

            await controller.InitializeAsync();

            Require(controller.State == GameState.FatalError, "The startup throw did not enter FatalError.");
            Require(hud.Status == GameStatusText.FatalError, "The startup throw did not show the controlled fatal UI status.");
            Require(factory.CreateCount == 0, "Startup failure created a target after the fallback threw.");
            Require(factory.DestroyCount == 0, "Startup failure destroyed a target that was never created.");
            Require(loader.AllOwnersReleasedOnce, "Startup failure leaked or double-released an owner.");

            controller.Dispose();
        }

        private static GameController CreateController(
            ScriptedLoader loader,
            RecordingHud hud,
            RecordingTargetFactory factory,
            params object[] roundKeys)
        {
            return new GameController(
                new HarnessConfiguration(new GameDefinition(PrefabKey, FallbackKey, roundKeys)),
                loader,
                hud,
                factory,
                new HarnessDiagnostics());
        }

        private static Texture2D CreateTexture(string name, Color color)
        {
            var texture = new Texture2D(2, 2) { name = name };
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static GameObject CreatePrefab(string name)
        {
            return new GameObject(name);
        }

        private static void DestroyAssets(params UnityEngine.Object[] assets)
        {
            foreach (var asset in assets)
            {
                if (asset != null)
                {
                    Destroy(asset);
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true
            };
            var color = _finished && _passed ? Color.green : Color.white;
            GUI.color = color;
            GUI.Label(
                new Rect(32f, 28f, Screen.width - 64f, 52f),
                "P01 WebGL exception-recovery harness" + (_finished ? (_passed ? " — PASS" : " — FAIL") : " — running"),
                style);

            style.fontSize = 17;
            GUI.color = Color.white;
            for (var index = 0; index < _results.Count; index++)
            {
                GUI.Label(new Rect(32f, 96f + index * 40f, Screen.width - 64f, 34f), _results[index], style);
            }
        }

        private sealed class HarnessConfiguration : IGameConfiguration
        {
            private readonly GameDefinition _definition;

            public HarnessConfiguration(GameDefinition definition)
            {
                _definition = definition;
            }

            public bool TryCreateDefinition(out GameDefinition definition, out string error)
            {
                definition = _definition;
                error = null;
                return true;
            }
        }

        private sealed class RecordingHud : IGameHud
        {
            public string Status { get; private set; }

            public void SetStatus(string text)
            {
                Status = text;
            }

            public void SetScore(int score)
            {
            }
        }

        private sealed class RecordingTarget : ITargetView
        {
            public bool IsValid { get; private set; } = true;
            public bool InteractionEnabled { get; private set; }
            public Texture2D Texture { get; private set; }

            public void ApplyTexture(Texture2D texture)
            {
                Texture = texture;
            }

            public void SetInteractionEnabled(bool enabled)
            {
                InteractionEnabled = enabled;
            }

            public void FlashError()
            {
            }

            public void StopFeedback()
            {
            }

            public void Clear()
            {
                Texture = null;
                InteractionEnabled = false;
            }

            public void MarkDestroyed()
            {
                IsValid = false;
            }
        }

        private sealed class RecordingTargetFactory : ITargetFactory
        {
            private readonly RecordingTarget _target;

            public RecordingTargetFactory(RecordingTarget target)
            {
                _target = target;
            }

            public int CreateCount { get; private set; }
            public int DestroyCount { get; private set; }

            public ITargetView Create(GameObject prefab, Texture2D initialTexture)
            {
                CreateCount++;
                _target.ApplyTexture(initialTexture);
                return _target;
            }

            public void Destroy(ITargetView target)
            {
                DestroyCount++;
                _target.MarkDestroyed();
            }
        }

        private sealed class HarnessDiagnostics : IGameDiagnostics
        {
            public void LogWarning(string message, Exception exception = null)
            {
                Debug.LogWarning("P01_WEBGL_CONTROLLED_WARNING: " + message + " " + exception?.Message);
            }

            public void LogError(string message, Exception exception = null)
            {
                Debug.LogError("P01_WEBGL_CONTROLLED_FATAL: " + message + " " + exception?.Message);
            }
        }

        private sealed class ScriptedLoader : IAddressableAssetLoader
        {
            private readonly Queue<ScriptedLoad> _loads;
            private readonly List<ITrackedLoad> _owners = new List<ITrackedLoad>();

            public ScriptedLoader(params ScriptedLoad[] loads)
            {
                _loads = new Queue<ScriptedLoad>(loads);
            }

            public bool AllOwnersReleasedOnce
            {
                get
                {
                    if (_loads.Count != 0 || _owners.Count == 0)
                    {
                        return false;
                    }

                    foreach (var owner in _owners)
                    {
                        if (owner.DisposeCount != 1)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
            {
                if (_loads.Count == 0)
                {
                    throw new InvalidOperationException("The P01 harness received an unexpected load for " + runtimeKey + ".");
                }

                var scriptedLoad = _loads.Dequeue();
                if (!Equals(scriptedLoad.Key, runtimeKey) || scriptedLoad.AssetType != typeof(T))
                {
                    throw new InvalidOperationException(
                        "The P01 harness expected " + scriptedLoad.AssetType.Name + " at " + scriptedLoad.Key +
                        " but received " + typeof(T).Name + " at " + runtimeKey + ".");
                }

                IAddressableLoad<T> owner;
                switch (scriptedLoad.Kind)
                {
                    case ScriptedLoadKind.Succeeded:
                        owner = new ImmediateLoad<T>(AddressableLoadResult<T>.Succeeded((T)scriptedLoad.Asset));
                        break;
                    case ScriptedLoadKind.Failed:
                        owner = new ImmediateLoad<T>(AddressableLoadResult<T>.Failed(
                            new InvalidOperationException(scriptedLoad.Message)));
                        break;
                    case ScriptedLoadKind.Throws:
                        owner = new ThrowingLoad<T>(scriptedLoad.Message);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _owners.Add((ITrackedLoad)owner);
                return owner;
            }
        }

        private interface ITrackedLoad
        {
            int DisposeCount { get; }
        }

        private sealed class ImmediateLoad<T> : IAddressableLoad<T>, ITrackedLoad where T : UnityEngine.Object
        {
            private readonly Task<AddressableLoadResult<T>> _completion;

            public ImmediateLoad(AddressableLoadResult<T> result)
            {
                _completion = Task.FromResult(result);
            }

            public int DisposeCount { get; private set; }
            public Task<AddressableLoadResult<T>> Completion => _completion;

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class ThrowingLoad<T> : IAddressableLoad<T>, ITrackedLoad where T : UnityEngine.Object
        {
            private readonly string _message;

            public ThrowingLoad(string message)
            {
                _message = message;
            }

            public int DisposeCount { get; private set; }

            public Task<AddressableLoadResult<T>> Completion
            {
                get
                {
                    Debug.Log("P01_WEBGL_EXPLICIT_THROW: " + _message);
                    throw new InvalidOperationException(_message);
                }
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private enum ScriptedLoadKind
        {
            Succeeded,
            Failed,
            Throws
        }

        private sealed class ScriptedLoad
        {
            private ScriptedLoad(ScriptedLoadKind kind, object key, Type assetType, UnityEngine.Object asset, string message)
            {
                Kind = kind;
                Key = key;
                AssetType = assetType;
                Asset = asset;
                Message = message;
            }

            public ScriptedLoadKind Kind { get; }
            public object Key { get; }
            public Type AssetType { get; }
            public UnityEngine.Object Asset { get; }
            public string Message { get; }

            public static ScriptedLoad Succeeded<T>(object key, T asset) where T : UnityEngine.Object
            {
                return new ScriptedLoad(ScriptedLoadKind.Succeeded, key, typeof(T), asset, null);
            }

            public static ScriptedLoad Failed<T>(object key, string message) where T : UnityEngine.Object
            {
                return new ScriptedLoad(ScriptedLoadKind.Failed, key, typeof(T), null, message);
            }

            public static ScriptedLoad Throws<T>(object key, string message) where T : UnityEngine.Object
            {
                return new ScriptedLoad(ScriptedLoadKind.Throws, key, typeof(T), null, message);
            }
        }
    }
}
