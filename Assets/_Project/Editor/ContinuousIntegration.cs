using System;
using System.IO;
using AddressablesSample.Game.Config;
using AddressablesSample.Game.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AddressablesSample.Game.Editor
{
    /// <summary>
    /// Entry points invoked by GitHub Actions. They exist so the workflow files stay declarative
    /// and every build decision that matters lives in version-controlled C# instead of YAML.
    /// </summary>
    public static class ContinuousIntegration
    {
        private const string CustomBuildPathArgument = "-customBuildPath";
        private const string DefaultWebGlBuildPath = "Builds/WebGL";
        private const string WebGlExceptionRecoveryHarnessScenePath =
            "Assets/_Project/WebGlExceptionRecoveryHarness.unity";
        private const string WebGlProductionRecoveryHarnessFolder =
            "Assets/_Project/WebGlProductionRecoveryHarness";
        private const string WebGlProductionRecoveryMissingScenePath =
            WebGlProductionRecoveryHarnessFolder + "/MissingRound.unity";
        private const string WebGlProductionRecoveryStartupScenePath =
            WebGlProductionRecoveryHarnessFolder + "/StartupFatal.unity";
        private const string WebGlProductionRecoveryMissingRoundTexturePath =
            WebGlProductionRecoveryHarnessFolder + "/MissingRoundTexture.asset";
        private const string WebGlProductionRecoveryMissingStartupTexturePath =
            WebGlProductionRecoveryHarnessFolder + "/MissingStartupTexture.asset";
        private const string WebGlProductionRecoveryMissingRoundConfigPath =
            WebGlProductionRecoveryHarnessFolder + "/MissingRoundConfig.asset";
        private const string WebGlProductionRecoveryStartupConfigPath =
            WebGlProductionRecoveryHarnessFolder + "/StartupFatalConfig.asset";

        /// <summary>
        /// Runs generation twice to prove it is idempotent, then asserts the structural validator.
        /// A second run that produced different content would fail the validator on the first.
        /// </summary>
        public static void VerifyGeneratedProject()
        {
            Run(() =>
            {
                TestTaskSetup.Run();
                TestTaskSetup.Run();
                ProjectValidation.ValidateOrThrow();
                Debug.Log("Generated project is reproducible and structurally valid.");
            });
        }

        /// <summary>
        /// Produces the playable WebGL demo. The project deliberately does not build Addressables
        /// during a player build, so the content build is explicit and happens after the active
        /// build target is already WebGL -- Addressables content is platform specific.
        /// </summary>
        public static void BuildWebGl()
        {
            Run(() =>
            {
                RequireWebGlBuildTarget();

                TestTaskSetup.Run();
                ProjectValidation.ValidateOrThrow();
                AddressablesWorkflow.BuildLocalAndUseExisting();
                ConfigureWebGlPlayerSettings();

                var outputPath = ResolveWebGlOutputPath();
                Directory.CreateDirectory(outputPath);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { TestTaskPaths.GameScene },
                    locationPathName = outputPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None
                });

                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "WebGL player build failed with result: " +
                        (report == null ? "no report" : report.summary.result.ToString()));
                }

                Debug.Log($"WebGL demo built into {outputPath} " +
                          $"({report.summary.totalSize / (1024 * 1024)} MB).");

                // The content build switches Addressables to Use Existing Build, and the content
                // it just produced is WebGL content. Leaving the project that way makes both the
                // structural validator and the PlayMode suite fail in the editor, so the committed
                // baseline is restored before returning.
                AddressablesWorkflow.UseAssetDatabase();
            });
        }

        /// <summary>
        /// Builds only the isolated P01 browser probe. It deliberately creates and removes its
        /// scene through Editor APIs, so the committed demo scene and public Addressables content
        /// are never used as a failure switch.
        /// </summary>
        public static void BuildWebGlExceptionRecoveryHarness()
        {
            Run(() =>
            {
                RequireWebGlBuildTarget();
                TestTaskSetup.Run();
                ProjectValidation.ValidateOrThrow();
                ConfigureWebGlPlayerSettings();

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WebGlExceptionRecoveryHarnessScenePath) != null)
                {
                    throw new BuildFailedException(
                        "The temporary WebGL exception-recovery harness scene already exists. " +
                        "Resolve it before running this isolated probe.");
                }

                try
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("WebGL Exception Recovery Harness")
                        .AddComponent<Validation.WebGlExceptionRecoveryHarness>();
                    if (!EditorSceneManager.SaveScene(scene, WebGlExceptionRecoveryHarnessScenePath))
                    {
                        throw new BuildFailedException("Unity could not save the temporary WebGL exception-recovery harness scene.");
                    }

                    var outputPath = ResolveWebGlOutputPath();
                    Directory.CreateDirectory(outputPath);
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { WebGlExceptionRecoveryHarnessScenePath },
                        locationPathName = outputPath,
                        target = BuildTarget.WebGL,
                        targetGroup = BuildTargetGroup.WebGL,
                        options = BuildOptions.None
                    });

                    if (report == null || report.summary.result != BuildResult.Succeeded)
                    {
                        throw new BuildFailedException(
                            "WebGL exception-recovery harness build failed with result: " +
                            (report == null ? "no report" : report.summary.result.ToString()));
                    }

                    Debug.Log($"WebGL exception-recovery harness built into {outputPath} " +
                              $"({report.summary.totalSize / (1024 * 1024)} MB).");
                }
                finally
                {
                    AssetDatabase.DeleteAsset(WebGlExceptionRecoveryHarnessScenePath);
                    AddressablesWorkflow.UseAssetDatabase();
                }
            });
        }

        /// <summary>
        /// Builds the P01 production-path browser probe. Unlike the explicit-throw harness, this
        /// player uses the real GameBootstrapper, Addressables loader, UI, target factory and
        /// pointer input. Its only faults are temporary non-addressable AssetReference GUIDs.
        /// </summary>
        public static void BuildWebGlProductionRecoveryHarness()
        {
            Run(() =>
            {
                RequireWebGlBuildTarget();
                TestTaskSetup.Run();
                ProjectValidation.ValidateOrThrow();
                ConfigureWebGlPlayerSettings();

                if (AssetDatabase.IsValidFolder(WebGlProductionRecoveryHarnessFolder))
                {
                    throw new BuildFailedException(
                        "The temporary production-recovery harness folder already exists. " +
                        "Resolve it before running this isolated probe.");
                }

                try
                {
                    CreateProductionRecoveryHarnessScenes();
                    AddressablesWorkflow.BuildLocalAndUseExisting();

                    var outputPath = ResolveWebGlOutputPath();
                    Directory.CreateDirectory(outputPath);
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[]
                        {
                            WebGlProductionRecoveryMissingScenePath,
                            WebGlProductionRecoveryStartupScenePath
                        },
                        locationPathName = outputPath,
                        target = BuildTarget.WebGL,
                        targetGroup = BuildTargetGroup.WebGL,
                        options = BuildOptions.None
                    });

                    if (report == null || report.summary.result != BuildResult.Succeeded)
                    {
                        throw new BuildFailedException(
                            "WebGL production-recovery harness build failed with result: " +
                            (report == null ? "no report" : report.summary.result.ToString()));
                    }

                    Debug.Log($"WebGL production-recovery harness built into {outputPath} " +
                              $"({report.summary.totalSize / (1024 * 1024)} MB).");
                }
                finally
                {
                    AssetDatabase.DeleteAsset(WebGlProductionRecoveryHarnessFolder);
                    AddressablesWorkflow.UseAssetDatabase();
                }
            });
        }

        public static void ConfigureWebGlPlayerSettings()
        {
            // Uncompressed output is not an option: the IL2CPP WebAssembly module builds to about
            // 46 MiB, and static hosts cap individual assets well below that (Cloudflare Workers
            // and Pages both refuse anything over 25 MiB). Brotli brings it to roughly a fifth.
            //
            // The decompression fallback is enabled deliberately. Without it the player only works
            // when the host returns "Content-Encoding: br", which is a per-host configuration this
            // build cannot verify; with it, the loader decompresses in JavaScript whenever the
            // bytes arrive still compressed. That makes the same output correct on any static
            // host, which is the property that matters for a portfolio demo.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
        }

        private static void RequireWebGlBuildTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new BuildFailedException(
                    "The active build target must be WebGL before building the demo. " +
                    "Actual target: " + EditorUserBuildSettings.activeBuildTarget + ".");
            }
        }

        private static void CreateProductionRecoveryHarnessScenes()
        {
            TestTaskPaths.EnsureFolder(WebGlProductionRecoveryHarnessFolder);

            var targetPrefabGuid = RequireAssetGuid(TestTaskPaths.TargetPrefab);
            var fallbackGuid = RequireAssetGuid(TestTaskPaths.FallbackTexture);
            var firstSuccessfulRoundGuid = RequireAssetGuid(TestTaskPaths.RoundTexturePaths[0]);
            var secondSuccessfulRoundGuid = RequireAssetGuid(TestTaskPaths.RoundTexturePaths[1]);
            var missingRoundGuid = CreateNonAddressableTexture(
                WebGlProductionRecoveryMissingRoundTexturePath,
                "P01 Missing Round Key");
            var missingStartupGuid = CreateNonAddressableTexture(
                WebGlProductionRecoveryMissingStartupTexturePath,
                "P01 Missing Startup Key");

            var missingRoundConfig = CreateHarnessConfig(
                WebGlProductionRecoveryMissingRoundConfigPath,
                "P01 Missing Round Config",
                targetPrefabGuid,
                fallbackGuid,
                new[] { missingRoundGuid, firstSuccessfulRoundGuid, secondSuccessfulRoundGuid });
            var startupFailureConfig = CreateHarnessConfig(
                WebGlProductionRecoveryStartupConfigPath,
                "P01 Startup Fatal Config",
                targetPrefabGuid,
                missingStartupGuid,
                new[] { firstSuccessfulRoundGuid, secondSuccessfulRoundGuid });

            CreateProductionRecoveryScene(
                WebGlProductionRecoveryMissingScenePath,
                missingRoundConfig,
                true);
            CreateProductionRecoveryScene(
                WebGlProductionRecoveryStartupScenePath,
                startupFailureConfig,
                false);
            AssetDatabase.SaveAssets();
        }

        private static string CreateNonAddressableTexture(string assetPath, string assetName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = assetName
            };
            texture.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta });
            texture.Apply();
            AssetDatabase.CreateAsset(texture, assetPath);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new BuildFailedException("Unity did not assign a GUID to " + assetPath + ".");
            }

            return guid;
        }

        private static GameConfig CreateHarnessConfig(
            string assetPath,
            string assetName,
            string targetPrefabGuid,
            string fallbackGuid,
            string[] roundTextureGuids)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.name = assetName;
            config.Configure(
                new AssetReferenceGameObject(targetPrefabGuid),
                new AssetReferenceTexture2D(fallbackGuid),
                Array.ConvertAll(roundTextureGuids, guid => new AssetReferenceTexture2D(guid)));
            AssetDatabase.CreateAsset(config, assetPath);
            return config;
        }

        private static void CreateProductionRecoveryScene(string scenePath, GameConfig config, bool addProbe)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -6f), Quaternion.identity);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.1f, 1f);
            camera.fieldOfView = 60f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

            var lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);

            var systems = new GameObject("Game Systems");
            var spawn = new GameObject("Target Spawn").transform;
            spawn.SetParent(systems.transform, false);
            var input = systems.AddComponent<PointerSelectionInput>();
            input.Configure(camera, ~0, 100f);

            var canvasObject = new GameObject(
                "HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var status = CreateHarnessText("Status", canvasObject.transform, TextAnchor.UpperCenter, 34);
            SetHarnessRect(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-500f, -100f), new Vector2(500f, -30f));
            var score = CreateHarnessText("Score", canvasObject.transform, TextAnchor.UpperLeft, 32);
            SetHarnessRect(score.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -100f), new Vector2(400f, -30f));

            var hud = canvasObject.AddComponent<HudView>();
            hud.Configure(status, score);
            var bootstrapper = systems.AddComponent<GameBootstrapper>();
            bootstrapper.Configure(config, hud, input, spawn);

            if (addProbe)
            {
                new GameObject("P01 Production Recovery Probe")
                    .AddComponent<Validation.WebGlProductionRecoveryProbe>();
            }

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new BuildFailedException("Unity could not save the temporary production-recovery scene " + scenePath + ".");
            }
        }

        private static Text CreateHarnessText(string name, Transform parent, TextAnchor alignment, int fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetHarnessRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string RequireAssetGuid(string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new BuildFailedException("Missing required harness asset: " + assetPath + ".");
            }

            return guid;
        }

        /// <summary>
        /// game-ci passes the destination as <c>-customBuildPath</c>. Falling back to a repository
        /// relative path keeps the same method usable from a local command line.
        /// </summary>
        private static string ResolveWebGlOutputPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(
                arguments,
                argument => string.Equals(argument, CustomBuildPathArgument, StringComparison.Ordinal));

            if (index >= 0 && index + 1 < arguments.Length && !string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                return arguments[index + 1];
            }

            return Path.Combine(Directory.GetCurrentDirectory(), DefaultWebGlBuildPath);
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }
    }
}
