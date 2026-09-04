using System;
using System.Collections.Generic;
using System.Linq;
using AddressablesSample.Game.Config;
using AddressablesSample.Game.Presentation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AddressablesSample.Game.Editor
{
    public static class TestTaskSetup
    {
        private const string RoundLocalBuildPath =
            "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]";
        private const string RoundLocalLoadPath =
            "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]";
        private const string RoundRemoteBuildPath = "ServerData/[BuildTarget]";
        private const string RoundRemoteLoadPath = "https://YOUR_HOST/[BuildTarget]";

        // Presentation constants live here so the generated prefab and scene stay deterministic
        // and the validator can assert them instead of trusting whatever the Inspector holds.
        private const float IdleSpinDegreesPerSecond = 22f;
        private const float PunchRecoverySpeed = 6f;

        [MenuItem("AddressablesSample/Game/Setup Test Task")]
        public static void Run()
        {
            EnsureFolders();
            ConfigureRoundTextureImporters();
            var fallback = CreateOrUpdateFallback();
            var material = CreateOrUpdateMaterial();
            CreateOrUpdateTargetPrefab(material);
            var config = CreateOrUpdateConfig();
            var volumeProfile = CreateOrUpdateVolumeProfile();
            ConfigureAddressables();
            CreateOrUpdateScene(config, volumeProfile);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TestTaskPaths.GameScene, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("AddressablesSample test-task setup completed successfully.");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                ProjectValidation.ValidateOrThrow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        private static void EnsureFolders()
        {
            TestTaskPaths.EnsureFolder(TestTaskPaths.ConfigFolder);
            TestTaskPaths.EnsureFolder(TestTaskPaths.MaterialsFolder);
            TestTaskPaths.EnsureFolder(TestTaskPaths.PrefabsFolder);
            TestTaskPaths.EnsureFolder(TestTaskPaths.ScenesFolder);
            TestTaskPaths.EnsureFolder(TestTaskPaths.TexturesFolder);
            TestTaskPaths.EnsureFolder(TestTaskPaths.RenderingFolder);
        }

        private static void ConfigureRoundTextureImporters()
        {
            foreach (var path in TestTaskPaths.RoundTexturePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new BuildFailedException("Missing supplied round texture: " + path);
                }

                var changed = importer.textureType != TextureImporterType.Default ||
                              !importer.sRGBTexture ||
                              importer.mipmapEnabled ||
                              importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                              !importer.alphaIsTransparency ||
                              importer.wrapMode != TextureWrapMode.Clamp ||
                              importer.filterMode != FilterMode.Bilinear;
                if (!changed)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D CreateOrUpdateFallback()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TestTaskPaths.FallbackTexture);
            if (texture == null)
            {
                texture = new Texture2D(8, 8, TextureFormat.RGBA32, false, false)
                {
                    name = "FallbackTexture"
                };
                AssetDatabase.CreateAsset(texture, TestTaskPaths.FallbackTexture);
            }
            else if (!texture.Reinitialize(8, 8, TextureFormat.RGBA32, false))
            {
                throw new BuildFailedException("Unable to reinitialize the fallback texture asset.");
            }

            var pixels = new Color32[64];
            var magenta = new Color32(255, 0, 255, 255);
            var black = new Color32(16, 16, 16, 255);
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] = ((x / 2) + (y / 2)) % 2 == 0 ? magenta : black;
                }
            }

            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            return texture;
        }

        private static Material CreateOrUpdateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new BuildFailedException("The URP/Lit shader is unavailable.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(TestTaskPaths.MaterialAsset);
            if (material == null)
            {
                material = new Material(shader) { name = "Target" };
                AssetDatabase.CreateAsset(material, TestTaskPaths.MaterialAsset);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);

            // URP recomputes the _EMISSION keyword from the material's own emission colour on
            // every import. A black authored colour therefore strips the keyword the miss flash
            // depends on, silently disabling it. The authored value below stays imperceptible and
            // is overridden per-renderer by TargetView's MaterialPropertyBlock on the first frame,
            // so it never renders -- it exists purely to keep the shader variant compiled in.
            material.SetColor("_EmissionColor", new Color(0.08f, 0f, 0f, 1f));
            material.SetTextureScale("_BaseMap", new Vector2(1f, -1f));
            material.SetTextureOffset("_BaseMap", new Vector2(0f, 1f));
            material.SetFloat("_Smoothness", 0.2f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);

            // URP's "preserve specular lighting" path rewrites alpha blending into premultiplied
            // blending and enables _ALPHAPREMULTIPLY_ON. The supplied PNGs carry straight alpha,
            // so the option is turned off to keep the authored blend.
            if (material.HasProperty("_BlendModePreserveSpecular"))
            {
                material.SetFloat("_BlendModePreserveSpecular", 0f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_EMISSION");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>
        /// Builds the post-processing profile the demo scene renders through. The overrides are
        /// created as sub-assets so the profile stays a single deterministic file, and existing
        /// components are reused so repeated setup runs never duplicate them.
        /// </summary>
        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(TestTaskPaths.VolumeProfile);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "GameVolumeProfile";
                AssetDatabase.CreateAsset(profile, TestTaskPaths.VolumeProfile);
            }

            var bloom = GetOrAddVolumeComponent<UnityEngine.Rendering.Universal.Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.65f);

            var vignette = GetOrAddVolumeComponent<UnityEngine.Rendering.Universal.Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.4f);

            var tonemapping = GetOrAddVolumeComponent<UnityEngine.Rendering.Universal.Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);

            var colorAdjustments =
                GetOrAddVolumeComponent<UnityEngine.Rendering.Universal.ColorAdjustments>(profile);
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0.15f);
            colorAdjustments.contrast.Override(12f);
            colorAdjustments.saturation.Override(6f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing) && existing != null)
            {
                return existing;
            }

            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void CreateOrUpdateTargetPrefab(Material material)
        {
            var prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(TestTaskPaths.TargetPrefab) != null;
            var root = prefabExists
                ? PrefabUtility.LoadPrefabContents(TestTaskPaths.TargetPrefab)
                : new GameObject("Target");

            try
            {
                root.name = "Target";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                RemoveChildObjects(root.transform);

                var meshFilter = GetOrAddSingle<MeshFilter>(root);
                var renderer = GetOrAddSingle<MeshRenderer>(root);
                var collider = GetOrAddSingle<BoxCollider>(root);
                var target = GetOrAddSingle<TargetView>(root);

                var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (cube == null)
                {
                    throw new BuildFailedException("Unity's built-in cube mesh is unavailable.");
                }

                meshFilter.sharedMesh = cube;
                renderer.sharedMaterial = material;
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
                target.Configure(renderer, collider, 0.4f, IdleSpinDegreesPerSecond, PunchRecoverySpeed);

                if (PrefabUtility.SaveAsPrefabAsset(root, TestTaskPaths.TargetPrefab, out var success) == null || !success)
                {
                    throw new BuildFailedException("Unable to save the generated target prefab.");
                }
            }
            finally
            {
                if (prefabExists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static GameConfig CreateOrUpdateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TestTaskPaths.ConfigAsset);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.name = "GameConfig";
                AssetDatabase.CreateAsset(config, TestTaskPaths.ConfigAsset);
            }

            var targetGuid = RequireGuid(TestTaskPaths.TargetPrefab);
            var fallbackGuid = RequireGuid(TestTaskPaths.FallbackTexture);
            var rounds = TestTaskPaths.RoundTexturePaths
                .Select(path => new AssetReferenceTexture2D(RequireGuid(path)))
                .ToArray();
            config.Configure(
                new AssetReferenceGameObject(targetGuid),
                new AssetReferenceTexture2D(fallbackGuid),
                rounds);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            return config;
        }

        internal static AddressableAssetSettings ConfigureAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new BuildFailedException("Unable to create Addressables settings.");
            }

            var profiles = settings.profileSettings;
            profiles.CreateValue(TestTaskPaths.RoundBuildPathVariable, RoundLocalBuildPath);
            profiles.CreateValue(TestTaskPaths.RoundLoadPathVariable, RoundLocalLoadPath);

            var localId = profiles.GetProfileId(TestTaskPaths.LocalProfile);
            if (string.IsNullOrEmpty(localId))
            {
                localId = profiles.AddProfile(TestTaskPaths.LocalProfile, settings.activeProfileId);
            }

            profiles.SetValue(localId, TestTaskPaths.RoundBuildPathVariable, RoundLocalBuildPath);
            profiles.SetValue(localId, TestTaskPaths.RoundLoadPathVariable, RoundLocalLoadPath);
            profiles.SetValue(localId, AddressableAssetSettings.kLocalBuildPath,
                AddressableAssetSettings.kLocalBuildPathValue);
            profiles.SetValue(localId, AddressableAssetSettings.kLocalLoadPath,
                AddressableAssetSettings.kLocalLoadPathValue);

            var remoteId = profiles.GetProfileId(TestTaskPaths.RemoteProfile);
            if (string.IsNullOrEmpty(remoteId))
            {
                remoteId = profiles.AddProfile(TestTaskPaths.RemoteProfile, localId);
            }

            profiles.SetValue(remoteId, TestTaskPaths.RoundBuildPathVariable, RoundRemoteBuildPath);
            profiles.SetValue(remoteId, TestTaskPaths.RoundLoadPathVariable, RoundRemoteLoadPath);

            var hostedId = profiles.GetProfileId(TestTaskPaths.HostedProfile);
            if (string.IsNullOrEmpty(hostedId))
            {
                hostedId = profiles.AddProfile(TestTaskPaths.HostedProfile, localId);
            }

            profiles.SetValue(hostedId, TestTaskPaths.RoundBuildPathVariable, RoundRemoteBuildPath);
            profiles.SetValue(
                hostedId,
                TestTaskPaths.RoundLoadPathVariable,
                TestTaskPaths.HostedBaseUrl + "/[BuildTarget]");
            settings.activeProfileId = localId;

            var startup = GetOrCreateGroup(settings, TestTaskPaths.StartupGroup);
            var rounds = GetOrCreateGroup(settings, TestTaskPaths.RoundsGroup);
            ConfigureGroup(settings, startup, BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                AddressableAssetSettings.kLocalBuildPath, AddressableAssetSettings.kLocalLoadPath);
            ConfigureGroup(settings, rounds, BundledAssetGroupSchema.BundlePackingMode.PackSeparately,
                TestTaskPaths.RoundBuildPathVariable, TestTaskPaths.RoundLoadPathVariable);

            ReconcileGroupEntries(settings, startup, new[]
            {
                new EntryDefinition(TestTaskPaths.TargetPrefab, "game/target", false),
                new EntryDefinition(TestTaskPaths.FallbackTexture, "game/fallback", false)
            });

            settings.AddLabel(TestTaskPaths.RoundLabel, false);
            var roundEntries = TestTaskPaths.RoundTexturePaths
                .Select((path, index) => new EntryDefinition(
                    path,
                    "game/round/" + TestTaskPaths.RoundStems[index],
                    true))
                .ToArray();
            ReconcileGroupEntries(settings, rounds, roundEntries);

            settings.BuildRemoteCatalog = false;
            if (!settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath) ||
                !settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath))
            {
                throw new BuildFailedException("Unable to map the local Addressables catalog paths.");
            }
            // Addressables 4.x defaults to a binary catalog. JSON is chosen deliberately: the
            // catalog is part of what this project demonstrates, and a reviewer can open the
            // published catalog URL and read the locations, providers and dependencies directly.
            // The cost is a larger, slower-to-parse catalog, which is irrelevant at twelve bundles.
            settings.EnableJsonCatalog = true;
            settings.SimulatedLoadDelay = 0.25f;
            settings.buildSettings.LogResourceManagerExceptions = false;
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            var fastMode = settings.DataBuilders.FindIndex(builder => builder is BuildScriptFastMode);
            if (fastMode < 0)
            {
                throw new BuildFailedException("Use Asset Database Addressables builder is missing.");
            }

            settings.ActivePlayModeDataBuilderIndex = fastMode;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(startup);
            EditorUtility.SetDirty(rounds);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static AddressableAssetGroup GetOrCreateGroup(
            AddressableAssetSettings settings,
            string groupName)
        {
            var groups = settings.groups.Where(group => group != null && group.Name == groupName).ToList();
            var group = groups.FirstOrDefault();
            if (group == null)
            {
                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            for (var index = 1; index < groups.Count; index++)
            {
                settings.RemoveGroup(groups[index]);
            }

            return group;
        }

        private static void ConfigureGroup(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            BundledAssetGroupSchema.BundlePackingMode packingMode,
            string buildVariable,
            string loadVariable)
        {
            foreach (var unexpected in group.Schemas
                         .Where(schema => schema != null &&
                                          !(schema is BundledAssetGroupSchema) &&
                                          !(schema is ContentUpdateGroupSchema))
                         .ToArray())
            {
                group.RemoveSchema(unexpected.GetType(), false);
            }

            var bundle = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>(false);
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>(false);
            bundle.IsEnabled = true;
            update.IsEnabled = true;
            bundle.UseDefaultSchemaSettings = false;
            bundle.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundle.BundleMode = packingMode;
            bundle.IncludeInBuild = true;
            bundle.IncludeGUIDInCatalog = true;
            bundle.IncludeAddressInCatalog = true;
            bundle.IncludeLabelsInCatalog = true;
            update.StaticContent = false;
            if (!bundle.BuildPath.SetVariableByName(settings, buildVariable) ||
                !bundle.LoadPath.SetVariableByName(settings, loadVariable))
            {
                throw new BuildFailedException("Unable to map build/load paths for Addressables group " + group.Name + ".");
            }

            EditorUtility.SetDirty(bundle);
            EditorUtility.SetDirty(update);
        }

        private static void ReconcileGroupEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            IReadOnlyList<EntryDefinition> definitions)
        {
            var expectedGuids = new HashSet<string>(definitions.Select(definition => RequireGuid(definition.Path)));
            foreach (var unexpected in group.entries.Where(entry => !expectedGuids.Contains(entry.guid)).ToArray())
            {
                settings.RemoveAssetEntry(unexpected.guid, false);
            }

            foreach (var definition in definitions)
            {
                var guid = RequireGuid(definition.Path);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.SetAddress(definition.Address, false);
                foreach (var label in entry.labels.ToArray())
                {
                    entry.SetLabel(label, false, false, false);
                }

                if (definition.HasRoundLabel)
                {
                    entry.SetLabel(TestTaskPaths.RoundLabel, true, false, false);
                }
            }
        }

        private static void CreateOrUpdateScene(GameConfig config, VolumeProfile volumeProfile)
        {
            var sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestTaskPaths.GameScene) != null;
            var scene = !sceneExists
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(TestTaskPaths.GameScene, OpenSceneMode.Single);

            var expectedRootNames = new HashSet<string>
            {
                "Main Camera",
                "Directional Light",
                "Game Systems",
                "HUD"
            };
            foreach (var unexpected in scene.GetRootGameObjects().Where(root => !expectedRootNames.Contains(root.name)))
            {
                UnityEngine.Object.DestroyImmediate(unexpected);
            }

            var cameraObject = GetOrCreateRoot(scene, "Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -6f), Quaternion.identity);
            var camera = GetOrAddSingle<Camera>(cameraObject);
            GetOrAddSingle<AudioListener>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.1f, 1f);
            camera.fieldOfView = 60f;
            var cameraData = GetOrAddSingle<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(cameraObject);
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // Ambient light is authored here rather than left at the scene default so the target
            // reads clearly against the solid background on every machine that opens the project.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.34f, 0.46f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.16f, 0.18f, 0.26f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);

            var lightObject = GetOrCreateRoot(scene, "Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = GetOrAddSingle<Light>(lightObject);
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);

            var systems = GetOrCreateRoot(scene, "Game Systems");
            var spawnObject = GetOrCreateChild(systems.transform, "Target Spawn");
            var postFxObject = GetOrCreateChild(systems.transform, "Post FX");
            RemoveUnexpectedChildren(systems.transform, new HashSet<string> { "Target Spawn", "Post FX" });

            var volume = GetOrAddSingle<Volume>(postFxObject);
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = volumeProfile;
            var spawn = spawnObject.transform;
            spawn.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spawn.localScale = Vector3.one;

            var canvasObject = GetOrCreateRoot(
                scene,
                "HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(HudView));
            var canvas = GetOrAddSingle<Canvas>(canvasObject);
            GetOrAddSingle<GraphicRaycaster>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAddSingle<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var status = GetOrCreateText("Status", canvasObject.transform, TextAnchor.UpperCenter, 34);
            SetRect(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-500f, -100f), new Vector2(500f, -30f));
            status.text = "Loading game...";

            var score = GetOrCreateText("Score", canvasObject.transform, TextAnchor.UpperLeft, 32);
            SetRect(score.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -100f), new Vector2(400f, -30f));
            score.text = "Score: 0";
            RemoveUnexpectedChildren(canvasObject.transform, new HashSet<string> { "Status", "Score" });

            var hud = GetOrAddSingle<HudView>(canvasObject);
            hud.Configure(status, score);
            var input = GetOrAddSingle<PointerSelectionInput>(systems);
            input.Configure(camera, ~0, 100f);
            var bootstrapper = GetOrAddSingle<GameBootstrapper>(systems);
            bootstrapper.Configure(config, hud, input, spawn);
            var overlay = GetOrAddSingle<DiagnosticsOverlay>(systems);
            overlay.Configure(bootstrapper, true);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, TestTaskPaths.GameScene))
            {
                throw new BuildFailedException("Unable to save the generated game scene.");
            }
        }

        private static Text GetOrCreateText(
            string name,
            Transform parent,
            TextAnchor alignment,
            int fontSize)
        {
            var matches = DirectChildren(parent).Where(child => child.name == name).ToList();
            var gameObject = matches.FirstOrDefault();
            for (var index = 1; index < matches.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(matches[index]);
            }

            if (gameObject == null || gameObject.GetComponent<RectTransform>() == null)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }

                gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                gameObject.transform.SetParent(parent, false);
            }

            GetOrAddSingle<CanvasRenderer>(gameObject);
            var text = GetOrAddSingle<Text>(gameObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(
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

        private static T GetOrAddSingle<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponents<T>();
            var component = components.FirstOrDefault();
            for (var index = 1; index < components.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(components[index]);
            }

            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static GameObject GetOrCreateRoot(Scene scene, string name, params Type[] components)
        {
            var matches = scene.GetRootGameObjects().Where(root => root.name == name).ToList();
            var root = matches.FirstOrDefault();
            for (var index = 1; index < matches.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(matches[index]);
            }

            if (root != null && components.Contains(typeof(RectTransform)) && !(root.transform is RectTransform))
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }

            if (root == null)
            {
                root = new GameObject(name, components);
            }

            return root;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var matches = DirectChildren(parent).Where(child => child.name == name).ToList();
            var child = matches.FirstOrDefault();
            for (var index = 1; index < matches.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(matches[index]);
            }

            if (child == null)
            {
                child = new GameObject(name);
                child.transform.SetParent(parent, false);
            }

            return child;
        }

        private static IEnumerable<GameObject> DirectChildren(Transform parent)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                yield return parent.GetChild(index).gameObject;
            }
        }

        private static void RemoveUnexpectedChildren(Transform parent, ISet<string> expectedNames)
        {
            foreach (var child in DirectChildren(parent).Where(child => !expectedNames.Contains(child.name)).ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        private static void RemoveChildObjects(Transform root)
        {
            for (var index = root.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(index).gameObject);
            }
        }

        private static string RequireGuid(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                throw new BuildFailedException("Missing required asset at " + path + ".");
            }

            return guid;
        }

        private readonly struct EntryDefinition
        {
            public EntryDefinition(string path, string address, bool hasRoundLabel)
            {
                Path = path;
                Address = address;
                HasRoundLabel = hasRoundLabel;
            }

            public string Path { get; }
            public string Address { get; }
            public bool HasRoundLabel { get; }
        }
    }
}
