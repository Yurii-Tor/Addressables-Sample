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
                    "Addressables local content build failed: " + (result == null ? "no result" : result.Error));
            }

            var packedSettingsPath = Path.Combine(Addressables.BuildPath, "settings.json");
            if (result.LocationCount <= 0 ||
                string.IsNullOrEmpty(result.OutputPath) ||
                !File.Exists(result.OutputPath) ||
                !File.Exists(packedSettingsPath))
            {
                throw new BuildFailedException("Addressables local content build produced no usable catalog.");
            }

            SelectPlayModeBuilder<BuildScriptPackedPlayMode>(settings);
            var profileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            Debug.Log($"Addressables {profileName} content build completed with {result.LocationCount} locations. " +
                      "Use Existing Build is active.");
        }

        public static void BuildLocalAndUseExistingFromCommandLine()
        {
            RunCommandLine(BuildLocalAndUseExisting);
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
            var remoteId = profiles.GetProfileId(TestTaskPaths.RemoteProfile);
            if (string.IsNullOrEmpty(remoteId))
            {
                throw new BuildFailedException("RemoteTemplate Addressables profile is missing.");
            }

            var normalizedBase = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            var expectedLoadPath = normalizedBase + "/[BuildTarget]";
            profiles.SetValue(
                remoteId,
                TestTaskPaths.RoundLoadPathVariable,
                expectedLoadPath);
            if (profiles.GetValueByName(remoteId, TestTaskPaths.RoundLoadPathVariable) != expectedLoadPath)
            {
                throw new BuildFailedException("Unable to store the RemoteTemplate load path.");
            }
            settings.activeProfileId = remoteId;
            settings.BuildRemoteCatalog = true;
            if (!settings.RemoteCatalogBuildPath.SetVariableByName(settings, TestTaskPaths.RoundBuildPathVariable) ||
                !settings.RemoteCatalogLoadPath.SetVariableByName(settings, TestTaskPaths.RoundLoadPathVariable))
            {
                throw new BuildFailedException("Unable to map the remote Addressables catalog paths.");
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("RemoteTemplate configured for " + normalizedBase + ".");
            return settings;
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
