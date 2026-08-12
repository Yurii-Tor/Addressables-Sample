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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AddressablesSample.Game.Editor
{
    public static class ProjectValidation
    {
        [MenuItem("AddressablesSample/Game/Validate Generated Project")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Debug.Log("AddressablesSample generated-project validation passed.");
        }

        public static void ValidateFromCommandLine()
        {
            try
            {
                ValidateOrThrow();
                Debug.Log("AddressablesSample generated-project validation passed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateAssets(errors);
            ValidateConfig(errors);
            ValidatePrefab(errors);
            ValidateScene(errors);
            ValidateBuildSettings(errors);
            ValidateAddressables(errors);

            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    "AddressablesSample generated-project validation failed:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void ValidateAssets(ICollection<string> errors)
        {
            RequireAsset<GameConfig>(TestTaskPaths.ConfigAsset, errors);
            var material = RequireAsset<Material>(TestTaskPaths.MaterialAsset, errors);
            RequireAsset<GameObject>(TestTaskPaths.TargetPrefab, errors);
            RequireAsset<SceneAsset>(TestTaskPaths.GameScene, errors);
            var fallback = RequireAsset<Texture2D>(TestTaskPaths.FallbackTexture, errors);

            if (material != null && (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit"))
            {
                errors.Add("Target material must use Universal Render Pipeline/Lit.");
            }

            if (fallback != null &&
                (fallback.width != 8 || fallback.height != 8 ||
                 fallback.filterMode != FilterMode.Point || fallback.wrapMode != TextureWrapMode.Repeat))
            {
                errors.Add("Fallback texture must be the generated 8x8 checkerboard asset.");
            }
            else if (fallback != null && fallback.isReadable)
            {
                var pixels = fallback.GetPixels32();
                var expectedMagenta = new Color32(255, 0, 255, 255);
                var expectedBlack = new Color32(16, 16, 16, 255);
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        var expected = ((x / 2) + (y / 2)) % 2 == 0 ? expectedMagenta : expectedBlack;
                        if (!pixels[y * 8 + x].Equals(expected))
                        {
                            errors.Add("Fallback texture pixels do not match the generated checkerboard.");
                            y = 8;
                            break;
                        }
                    }
                }
            }
            else if (fallback != null)
            {
                errors.Add("Fallback texture must remain readable for deterministic validation.");
            }

            foreach (var path in TestTaskPaths.RoundTexturePaths)
            {
                RequireAsset<Texture2D>(path, errors);
            }
        }

        private static void ValidateConfig(ICollection<string> errors)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TestTaskPaths.ConfigAsset);
            if (config == null)
            {
                return;
            }

            if (!config.TryCreateDefinition(out _, out var validationError))
            {
                errors.Add("GameConfig validation failed: " + validationError);
                return;
            }

            AssertReferenceGuid(
                config.TargetPrefab == null ? null : config.TargetPrefab.AssetGUID,
                TestTaskPaths.TargetPrefab,
                "target prefab",
                errors);
            AssertReferenceGuid(
                config.FallbackTexture == null ? null : config.FallbackTexture.AssetGUID,
                TestTaskPaths.FallbackTexture,
                "fallback texture",
                errors);

            if (config.RoundTextures.Count != TestTaskPaths.RoundStems.Length)
            {
                errors.Add($"GameConfig must contain exactly {TestTaskPaths.RoundStems.Length} round textures.");
                return;
            }

            for (var index = 0; index < config.RoundTextures.Count; index++)
            {
                var reference = config.RoundTextures[index];
                AssertReferenceGuid(
                    reference == null ? null : reference.AssetGUID,
                    TestTaskPaths.RoundTexturePaths[index],
                    "round texture at index " + index,
                    errors);
            }
        }

        private static void ValidatePrefab(ICollection<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TestTaskPaths.TargetPrefab) == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(TestTaskPaths.TargetPrefab);
            try
            {
                ValidateNoMissingScripts(root, "Target prefab", errors);
                var targets = root.GetComponentsInChildren<TargetView>(true);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                var colliders = root.GetComponentsInChildren<BoxCollider>(true);
                var filters = root.GetComponentsInChildren<MeshFilter>(true);

                RequireCount(targets.Length, 1, "Target prefab TargetView count", errors);
                RequireCount(renderers.Length, 1, "Target prefab MeshRenderer count", errors);
                RequireCount(colliders.Length, 1, "Target prefab BoxCollider count", errors);
                RequireCount(filters.Length, 1, "Target prefab MeshFilter count", errors);
                if (root.transform.childCount != 0)
                {
                    errors.Add("Target prefab must have one root and no child objects.");
                }

                if (targets.Length == 1 && renderers.Length == 1 && targets[0].TargetRenderer != renderers[0])
                {
                    errors.Add("TargetView renderer reference is incorrect.");
                }

                if (targets.Length == 1 && colliders.Length == 1 && targets[0].TargetCollider != colliders[0])
                {
                    errors.Add("TargetView collider reference is incorrect.");
                }

                var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TestTaskPaths.MaterialAsset);
                if (renderers.Length == 1 && renderers[0].sharedMaterial != expectedMaterial)
                {
                    errors.Add("Target prefab must use Target.mat as its shared material.");
                }

                if (filters.Length == 1 && filters[0].sharedMesh == null)
                {
                    errors.Add("Target prefab cube mesh is missing.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateScene(ICollection<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestTaskPaths.GameScene) == null)
            {
                return;
            }

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(TestTaskPaths.GameScene, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var expectedRootNames = new[] { "Directional Light", "Game Systems", "HUD", "Main Camera" };
                if (!roots.Select(root => root.name).OrderBy(name => name, StringComparer.Ordinal)
                        .SequenceEqual(expectedRootNames))
                {
                    errors.Add("Game scene must contain exactly the four generated root objects.");
                }

                foreach (var root in roots)
                {
                    ValidateNoMissingScripts(root, "Game scene", errors);
                }

                var bootstrapper = Components<GameBootstrapper>(roots);
                var hud = Components<HudView>(roots);
                var input = Components<PointerSelectionInput>(roots);
                var cameras = Components<Camera>(roots);
                var canvases = Components<Canvas>(roots);
                var texts = Components<Text>(roots);
                var lights = Components<Light>(roots).Where(light => light.type == LightType.Directional).ToArray();

                RequireCount(bootstrapper.Length, 1, "Game scene GameBootstrapper count", errors);
                RequireCount(hud.Length, 1, "Game scene HudView count", errors);
                RequireCount(input.Length, 1, "Game scene PointerSelectionInput count", errors);
                RequireCount(cameras.Length, 1, "Game scene Camera count", errors);
                RequireCount(canvases.Length, 1, "Game scene Canvas count", errors);
                RequireCount(texts.Length, 2, "Game scene Text count", errors);
                RequireCount(lights.Length, 1, "Game scene directional Light count", errors);

                var systemsRoot = roots.SingleOrDefault(root => root.name == "Game Systems");
                var hudRoot = roots.SingleOrDefault(root => root.name == "HUD");
                if (systemsRoot == null || !DirectChildNames(systemsRoot.transform).SequenceEqual(new[] { "Target Spawn" }))
                {
                    errors.Add("Game Systems must contain only the Target Spawn child.");
                }

                var expectedHudChildren = new[] { "Score", "Status" };
                if (hudRoot == null || !DirectChildNames(hudRoot.transform).SequenceEqual(expectedHudChildren))
                {
                    errors.Add("HUD must contain only the Score and Status children.");
                }

                if (Components<TargetView>(roots).Length != 0 ||
                    Components<MeshRenderer>(roots).Length != 0 ||
                    Components<Collider>(roots).Length != 0)
                {
                    errors.Add("Game scene must not contain a baked target, renderer, or collider.");
                }

                if (cameras.Length == 1 && !cameras[0].CompareTag("MainCamera"))
                {
                    errors.Add("Generated camera must have the MainCamera tag.");
                }

                if (bootstrapper.Length == 1 && hud.Length == 1 && input.Length == 1)
                {
                    var config = AssetDatabase.LoadAssetAtPath<GameConfig>(TestTaskPaths.ConfigAsset);
                    AssertObjectReference(bootstrapper[0], "_config", config, "GameBootstrapper config", errors);
                    AssertObjectReference(bootstrapper[0], "_hud", hud[0], "GameBootstrapper HUD", errors);
                    AssertObjectReference(bootstrapper[0], "_selectionInput", input[0], "GameBootstrapper input", errors);
                    var spawn = ObjectReference(bootstrapper[0], "_targetSpawn") as Transform;
                    if (spawn == null || spawn.name != "Target Spawn" || spawn.position != Vector3.zero)
                    {
                        errors.Add("GameBootstrapper target spawn is missing or not at the origin.");
                    }
                }

                if (input.Length == 1 && cameras.Length == 1)
                {
                    AssertObjectReference(input[0], "_camera", cameras[0], "PointerSelectionInput camera", errors);
                }

                if (hud.Length == 1)
                {
                    var status = ObjectReference(hud[0], "_statusText") as Text;
                    var score = ObjectReference(hud[0], "_scoreText") as Text;
                    if (status == null || score == null || status == score ||
                        !texts.Contains(status) || !texts.Contains(score))
                    {
                        errors.Add("HudView must reference the two distinct Text components in the generated scene.");
                    }
                }
            }
            finally
            {
                if (previousSetup.Any(item => item.isLoaded))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        private static void ValidateBuildSettings(ICollection<string> errors)
        {
            var gameEntries = EditorBuildSettings.scenes.Count(scene => scene.path == TestTaskPaths.GameScene);
            if (gameEntries != 1)
            {
                errors.Add("Build Settings must contain Game.unity exactly once.");
            }

            var enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (enabled.Length != 1 || enabled[0] != TestTaskPaths.GameScene)
            {
                errors.Add("Game.unity must be the sole enabled Build Settings scene.");
            }
        }

        private static void ValidateAddressables(ICollection<string> errors)
        {
            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                errors.Add("Addressables settings do not exist.");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                errors.Add("Addressables settings could not be loaded.");
                return;
            }

            var startupGroups = settings.groups.Where(group => group != null && group.Name == TestTaskPaths.StartupGroup).ToArray();
            var roundsGroups = settings.groups.Where(group => group != null && group.Name == TestTaskPaths.RoundsGroup).ToArray();
            RequireCount(startupGroups.Length, 1, "Addressables Game-Startup group count", errors);
            RequireCount(roundsGroups.Length, 1, "Addressables Game-RoundTextures group count", errors);

            if (startupGroups.Length == 1)
            {
                ValidateGroup(
                    settings,
                    startupGroups[0],
                    BundledAssetGroupSchema.BundlePackingMode.PackTogether,
                    AddressableAssetSettings.kLocalBuildPath,
                    AddressableAssetSettings.kLocalLoadPath,
                    errors);
                ValidateEntries(settings, startupGroups[0], new Dictionary<string, string>
                {
                    { TestTaskPaths.TargetPrefab, "game/target" },
                    { TestTaskPaths.FallbackTexture, "game/fallback" }
                }, false, errors);
            }

            if (roundsGroups.Length == 1)
            {
                ValidateGroup(
                    settings,
                    roundsGroups[0],
                    BundledAssetGroupSchema.BundlePackingMode.PackSeparately,
                    TestTaskPaths.RoundBuildPathVariable,
                    TestTaskPaths.RoundLoadPathVariable,
                    errors);
                var expected = TestTaskPaths.RoundTexturePaths
                    .Select((path, index) => new { path, address = "game/round/" + TestTaskPaths.RoundStems[index] })
                    .ToDictionary(item => item.path, item => item.address);
                ValidateEntries(settings, roundsGroups[0], expected, true, errors);
            }

            var allExpectedPaths = new[] { TestTaskPaths.TargetPrefab, TestTaskPaths.FallbackTexture }
                .Concat(TestTaskPaths.RoundTexturePaths);
            foreach (var path in allExpectedPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                var count = settings.groups.Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .Count(entry => entry.guid == guid);
                if (count != 1)
                {
                    errors.Add($"Addressable asset {path} must appear in exactly one group; found {count}.");
                }
            }

            var expectedAddresses = new[] { "game/target", "game/fallback" }
                .Concat(TestTaskPaths.RoundStems.Select(stem => "game/round/" + stem))
                .ToArray();
            var allEntries = settings.groups.Where(group => group != null)
                .SelectMany(group => group.entries)
                .Where(entry => entry != null)
                .ToArray();
            foreach (var address in expectedAddresses)
            {
                RequireCount(allEntries.Count(entry => entry.address == address), 1,
                    "Addressables entry count for " + address, errors);
            }

            foreach (var entry in allEntries.Where(entry =>
                         entry.address != null &&
                         entry.address.StartsWith("game/", StringComparison.Ordinal) &&
                         !expectedAddresses.Contains(entry.address)))
            {
                errors.Add("Unexpected reserved game Addressables address: " + entry.address + ".");
            }

            var profiles = settings.profileSettings;
            var profileNames = profiles.GetAllProfileNames();
            RequireCount(profileNames.Count(name => name == TestTaskPaths.LocalProfile), 1,
                "Addressables Local profile count", errors);
            RequireCount(profileNames.Count(name => name == TestTaskPaths.RemoteProfile), 1,
                "Addressables RemoteTemplate profile count", errors);
            var localId = profiles.GetProfileId(TestTaskPaths.LocalProfile);
            var remoteId = profiles.GetProfileId(TestTaskPaths.RemoteProfile);
            if (string.IsNullOrEmpty(localId) || settings.activeProfileId != localId)
            {
                errors.Add("Local Addressables profile must exist and be active.");
            }
            else
            {
                AssertProfileValue(profiles, localId, TestTaskPaths.RoundBuildPathVariable,
                    "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]", errors);
                AssertProfileValue(profiles, localId, TestTaskPaths.RoundLoadPathVariable,
                    "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]", errors);
                AssertProfileValue(profiles, localId, AddressableAssetSettings.kLocalBuildPath,
                    AddressableAssetSettings.kLocalBuildPathValue, errors);
                AssertProfileValue(profiles, localId, AddressableAssetSettings.kLocalLoadPath,
                    AddressableAssetSettings.kLocalLoadPathValue, errors);
            }

            if (string.IsNullOrEmpty(remoteId))
            {
                errors.Add("RemoteTemplate Addressables profile must exist.");
            }
            else
            {
                AssertProfileValue(profiles, remoteId, TestTaskPaths.RoundBuildPathVariable,
                    "ServerData/[BuildTarget]", errors);
                AssertProfileValue(profiles, remoteId, TestTaskPaths.RoundLoadPathVariable,
                    "https://YOUR_HOST/[BuildTarget]", errors);
            }

            if (settings.BuildRemoteCatalog)
            {
                errors.Add("Remote catalog building must be disabled in the local baseline.");
            }

            if (settings.RemoteCatalogBuildPath.GetName(settings) != AddressableAssetSettings.kLocalBuildPath ||
                settings.RemoteCatalogLoadPath.GetName(settings) != AddressableAssetSettings.kLocalLoadPath)
            {
                errors.Add("The local Addressables catalog must use Local.BuildPath and Local.LoadPath.");
            }

            RequireCount(settings.GetLabels().Count(label => label == TestTaskPaths.RoundLabel), 1,
                "Addressables round-texture label count", errors);

            if (Math.Abs(settings.SimulatedLoadDelay - 0.25f) > 0.001f)
            {
                errors.Add("Addressables simulated load delay must be 0.25 seconds.");
            }

            if (settings.buildSettings.LogResourceManagerExceptions)
            {
                errors.Add("ResourceManager exception logging must be disabled so controlled failures log once.");
            }

            if (!(settings.ActivePlayModeDataBuilder is BuildScriptFastMode))
            {
                errors.Add("Use Asset Database (fastest) must be the active play-mode builder.");
            }
        }

        private static void ValidateGroup(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            BundledAssetGroupSchema.BundlePackingMode packingMode,
            string buildVariable,
            string loadVariable,
            ICollection<string> errors)
        {
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            var update = group.GetSchema<ContentUpdateGroupSchema>();
            if (bundle == null || update == null)
            {
                errors.Add(group.Name + " must contain bundled and content-update schemas.");
                return;
            }

            if (group.Schemas.Count != 2 || group.Schemas.Any(schema => schema == null))
            {
                errors.Add(group.Name + " must contain exactly the bundled and content-update schemas.");
            }

            if (!bundle.IsEnabled || !update.IsEnabled ||
                bundle.UseDefaultSchemaSettings ||
                update.StaticContent ||
                bundle.Compression != BundledAssetGroupSchema.BundleCompressionMode.LZ4 ||
                bundle.BundleMode != packingMode ||
                !bundle.IncludeInBuild ||
                !bundle.IncludeGUIDInCatalog ||
                !bundle.IncludeAddressInCatalog ||
                !bundle.IncludeLabelsInCatalog)
            {
                errors.Add(group.Name + " has incorrect packing, compression, or inclusion settings.");
            }

            if (bundle.BuildPath.GetName(settings) != buildVariable ||
                bundle.LoadPath.GetName(settings) != loadVariable)
            {
                errors.Add(group.Name + " has incorrect build/load path mappings.");
            }
        }

        private static void ValidateEntries(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            IReadOnlyDictionary<string, string> expected,
            bool expectRoundLabel,
            ICollection<string> errors)
        {
            if (group.entries.Count != expected.Count)
            {
                errors.Add($"{group.Name} must contain exactly {expected.Count} direct entries.");
            }

            foreach (var pair in expected)
            {
                var guid = AssetDatabase.AssetPathToGUID(pair.Key);
                var entry = group.entries.FirstOrDefault(candidate => candidate.guid == guid);
                if (entry == null)
                {
                    errors.Add($"{group.Name} is missing Addressable entry {pair.Key}.");
                    continue;
                }

                if (entry.address != pair.Value)
                {
                    errors.Add($"Addressable {pair.Key} has address {entry.address}, expected {pair.Value}.");
                }

                var labels = entry.labels.OrderBy(label => label, StringComparer.Ordinal).ToArray();
                var expectedLabels = expectRoundLabel ? new[] { TestTaskPaths.RoundLabel } : Array.Empty<string>();
                if (!labels.SequenceEqual(expectedLabels))
                {
                    errors.Add($"Addressable {pair.Key} has incorrect labels.");
                }

                if (settings.FindAssetEntry(guid) != entry)
                {
                    errors.Add($"Addressable lookup for {pair.Key} is not unique.");
                }
            }
        }

        private static void AssertProfileValue(
            AddressableAssetProfileSettings profiles,
            string profileId,
            string variable,
            string expected,
            ICollection<string> errors)
        {
            var actual = profiles.GetValueByName(profileId, variable);
            if (actual != expected)
            {
                errors.Add($"Profile {profiles.GetProfileName(profileId)} variable {variable} is {actual}, expected {expected}.");
            }
        }

        private static T RequireAsset<T>(string path, ICollection<string> errors) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                errors.Add($"Required {typeof(T).Name} asset is missing at {path}.");
            }

            return asset;
        }

        private static void AssertReferenceGuid(
            string actualGuid,
            string expectedPath,
            string description,
            ICollection<string> errors)
        {
            var expectedGuid = AssetDatabase.AssetPathToGUID(expectedPath);
            if (actualGuid != expectedGuid)
            {
                errors.Add($"GameConfig {description} GUID is incorrect.");
            }
        }

        private static void RequireCount(int actual, int expected, string description, ICollection<string> errors)
        {
            if (actual != expected)
            {
                errors.Add($"{description} is {actual}, expected {expected}.");
            }
        }

        private static T[] Components<T>(IEnumerable<GameObject> roots) where T : Component
        {
            return roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static string[] DirectChildNames(Transform parent)
        {
            var names = new string[parent.childCount];
            for (var index = 0; index < parent.childCount; index++)
            {
                names[index] = parent.GetChild(index).name;
            }

            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        private static void ValidateNoMissingScripts(GameObject root, string context, ICollection<string> errors)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (count > 0)
                {
                    errors.Add($"{context} object {transform.name} has {count} missing script reference(s).");
                }

                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null &&
                            property.objectReferenceInstanceIDValue != 0)
                        {
                            errors.Add($"{context} object {transform.name} has a broken reference at " +
                                       property.propertyPath + ".");
                        }
                    }
                }
            }
        }

        private static UnityEngine.Object ObjectReference(UnityEngine.Object owner, string field)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(field);
            return property == null ? null : property.objectReferenceValue;
        }

        private static void AssertObjectReference(
            UnityEngine.Object owner,
            string field,
            UnityEngine.Object expected,
            string description,
            ICollection<string> errors)
        {
            if (ObjectReference(owner, field) != expected)
            {
                errors.Add(description + " reference is incorrect.");
            }
        }
    }
}
