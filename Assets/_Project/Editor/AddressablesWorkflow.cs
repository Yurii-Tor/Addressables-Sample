using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AddressablesSample.Game.Editor
{
    public static class AddressablesWorkflow
    {
        [MenuItem("AddressablesSample/Game/Addressables/Use Asset Database")]
        public static void UseAssetDatabase()
        {
            var settings = RestoreLocalSettings();
            SelectPlayModeBuilder<BuildScriptFastMode>(settings);
            Debug.Log("Addressables configured for Local + Use Asset Database (fastest).");
        }

        public static void UseAssetDatabaseFromCommandLine()
        {
            RunCommandLine(UseAssetDatabase);
        }

        [MenuItem("AddressablesSample/Game/Addressables/Build Local + Use Existing Build")]
        public static void BuildLocalAndUseExisting()
        {
            var settings = RestoreLocalSettings();
            FreshBuildAndUseExisting(settings);
        }

        private static void FreshBuildAndUseExisting(AddressableAssetSettings settings)
        {
            var playerBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptSchemaDriven);
            if (playerBuilderIndex < 0)
            {
                throw new BuildFailedException("The Addressables schema-driven player builder is unavailable.");
            }

            settings.ActivePlayerDataBuilderIndex = playerBuilderIndex;
            var playerBuilder = settings.ActivePlayerDataBuilder;
            if (playerBuilder == null || !playerBuilder.CanBuildData<AddressablesPlayerBuildResult>())
            {
                throw new BuildFailedException("The active Addressables player-content builder is unavailable.");
            }

            AddressableAssetSettings.CleanPlayerContent(playerBuilder);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error))
            {
                throw new BuildFailedException(
                    "Addressables content build failed: " + (result == null ? "no result" : result.Error));
            }

            var packedSettingsPath = Path.Combine(Addressables.BuildPath, "settings.json");
            if (result.LocationCount <= 0 ||
                string.IsNullOrEmpty(result.OutputPath) ||
                !File.Exists(result.OutputPath) ||
                !File.Exists(packedSettingsPath))
            {
                throw new BuildFailedException("Addressables content build produced no usable catalog.");
            }

            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            SelectPlayModeBuilder<BuildScriptPackedPlayMode>(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var profileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            Debug.Log($"Addressables {profileName} content build completed with {result.LocationCount} locations. " +
                      "Use Existing Build is active.");
        }

        public static void BuildLocalAndUseExistingFromCommandLine()
        {
            RunCommandLine(BuildLocalAndUseExisting);
        }

        [MenuItem("AddressablesSample/Game/Addressables/Build Cloudflare Remote + Use Existing Build")]
        public static void BuildCloudflareAndUseExisting()
        {
            try
            {
                var settings = ConfigureRemote(TestTaskPaths.HostedBaseUrl);
                FreshBuildAndUseExisting(settings);
                ValidateRemoteOutput();
                Debug.Log("Cloudflare Addressables content is ready in ServerData. Publish it before testing remote loads.");
            }
            catch
            {
                RestoreLocalSettings();
                throw;
            }
        }

        public static void BuildCloudflareAndUseExistingFromCommandLine()
        {
            RunCommandLine(BuildCloudflareAndUseExisting);
        }

        [MenuItem("AddressablesSample/Game/Addressables/Use Uploaded Cloudflare Build")]
        public static void UseUploadedCloudflareBuild()
        {
            try
            {
                var settings = ConfigureRemote(TestTaskPaths.HostedBaseUrl);
                EnsurePackedCloudflareBuildExists();
                SelectPlayModeBuilder<BuildScriptPackedPlayMode>(settings);
                Debug.Log("Cloudflare + Use Existing Build is active. No content was rebuilt or uploaded.");
            }
            catch
            {
                RestoreLocalSettings();
                throw;
            }
        }

        public static void UseUploadedCloudflareBuildFromCommandLine()
        {
            RunCommandLine(UseUploadedCloudflareBuild);
        }

        [MenuItem("AddressablesSample/Game/Addressables/Clear Download Cache")]
        public static void ClearDownloadCache()
        {
            if (!Caching.ClearCache())
            {
                throw new BuildFailedException(
                    "Unity could not clear the AssetBundle cache. Stop Play Mode and try again.");
            }

            Debug.Log("Unity AssetBundle download cache cleared.");
        }

        [MenuItem("AddressablesSample/Game/Addressables/Restore Local Defaults")]
        public static void RestoreLocalDefaults()
        {
            UseAssetDatabase();
        }

        public static void RestoreLocalDefaultsFromCommandLine()
        {
            RunCommandLine(RestoreLocalDefaults);
        }

        public static void ConfigureRemoteFromCommandLine()
        {
            RunCommandLine(() =>
            {
                try
                {
                    var settings = ConfigureRemote(RequireCommandLineValue("-remoteBaseUrl"));
                    FreshBuildAndUseExisting(settings);
                    ValidateRemoteOutput();
                }
                catch
                {
                    try
                    {
                        TestTaskSetup.ConfigureAddressables();
                    }
                    catch (Exception restorationException)
                    {
                        Debug.LogException(restorationException);
                    }

                    throw;
                }
            });
        }

        internal static AddressableAssetSettings ConfigureRemote(string remoteBaseUrl)
        {
            var trimmedUrl = remoteBaseUrl == null ? string.Empty : remoteBaseUrl.Trim();
            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                trimmedUrl.IndexOf("YOUR_HOST", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmedUrl.IndexOf("[BuildTarget]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new BuildFailedException("-remoteBaseUrl must be a real absolute HTTPS URL.");
            }

            var settings = TestTaskSetup.ConfigureAddressables();
            var profiles = settings.profileSettings;
            var remoteId = profiles.GetProfileId(TestTaskPaths.HostedProfile);
            if (string.IsNullOrEmpty(remoteId))
            {
                throw new BuildFailedException("Cloudflare Addressables profile is missing.");
            }

            var normalizedBase = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            var expectedLoadPath = normalizedBase + "/[BuildTarget]";
            profiles.SetValue(
                remoteId,
                TestTaskPaths.RoundLoadPathVariable,
                expectedLoadPath);
            if (profiles.GetValueByName(remoteId, TestTaskPaths.RoundLoadPathVariable) != expectedLoadPath)
            {
                throw new BuildFailedException("Unable to store the hosted Addressables load path.");
            }
            settings.activeProfileId = remoteId;
            settings.BuildRemoteCatalog = true;
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            if (!settings.RemoteCatalogBuildPath.SetVariableByName(settings, TestTaskPaths.RoundBuildPathVariable) ||
                !settings.RemoteCatalogLoadPath.SetVariableByName(settings, TestTaskPaths.RoundLoadPathVariable))
            {
                throw new BuildFailedException("Unable to map the remote Addressables catalog paths.");
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(TestTaskPaths.HostedProfile + " configured for " + normalizedBase + ".");
            return settings;
        }

        private static void EnsurePackedCloudflareBuildExists()
        {
            var packedSettingsPath = Path.Combine(Addressables.BuildPath, "settings.json");
            if (!File.Exists(packedSettingsPath))
            {
                throw new BuildFailedException(
                    "No packed Addressables build exists for the active platform. " +
                    "Run Build Cloudflare Remote + Use Existing Build first.");
            }

            var packedSettings = File.ReadAllText(packedSettingsPath);
            if (packedSettings.IndexOf(TestTaskPaths.HostedBaseUrl, StringComparison.Ordinal) < 0)
            {
                throw new BuildFailedException(
                    "The existing packed data was not built for the configured Cloudflare URL. " +
                    "Run Build Cloudflare Remote + Use Existing Build first.");
            }
        }

        private static void ValidateRemoteOutput()
        {
            var remoteFolder = Path.Combine("ServerData", EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!Directory.Exists(remoteFolder) ||
                Directory.GetFiles(remoteFolder, "catalog*.json").Length == 0 ||
                Directory.GetFiles(remoteFolder, "catalog*.hash").Length == 0 ||
                Directory.GetFiles(remoteFolder, "*.bundle").Length == 0)
            {
                throw new BuildFailedException(
                    "The remote build did not produce a catalog, hash, and bundles under " + remoteFolder + ".");
            }
        }

        private static AddressableAssetSettings RestoreLocalSettings()
        {
            return TestTaskSetup.ConfigureAddressables();
        }

        private static void SelectPlayModeBuilder<TBuilder>(AddressableAssetSettings settings)
            where TBuilder : ScriptableObject
        {
            var index = settings.DataBuilders.FindIndex(builder => builder is TBuilder);
            if (index < 0)
            {
                throw new BuildFailedException("Required Addressables play-mode builder is missing: " +
                                               typeof(TBuilder).Name + ".");
            }

            settings.ActivePlayModeDataBuilderIndex = index;
        }

        private static string RequireCommandLineValue(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.FindIndex(arguments, argument =>
                string.Equals(argument, argumentName, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new BuildFailedException("Missing required command-line argument " + argumentName + ".");
            }

            return arguments[index + 1];
        }

        private static void RunCommandLine(Action action)
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
