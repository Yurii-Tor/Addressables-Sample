using System;
using System.Text.RegularExpressions;
using AddressablesSample.Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class WebGlPlayerSettingsTests
    {
        [Test]
        public void ProductionWebGlConfiguration_EnablesExplicitlyThrownExceptionRecovery()
        {
            var compressionFormat = PlayerSettings.WebGL.compressionFormat;
            var decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            var dataCaching = PlayerSettings.WebGL.dataCaching;
            var exceptionSupport = PlayerSettings.WebGL.exceptionSupport;
            var runInBackground = PlayerSettings.runInBackground;
            var scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.WebGL);
            var compilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL);

            try
            {
                // BuildWebGl invokes this production method immediately before BuildPipeline.BuildPlayer.
                ContinuousIntegration.ConfigureWebGlPlayerSettings();

                Assert.That(
                    PlayerSettings.WebGL.exceptionSupport,
                    Is.EqualTo(WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly));
                Assert.That(PlayerSettings.WebGL.exceptionSupport, Is.Not.EqualTo(WebGLExceptionSupport.None));
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compressionFormat;
                PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
                PlayerSettings.WebGL.dataCaching = dataCaching;
                PlayerSettings.WebGL.exceptionSupport = exceptionSupport;
                PlayerSettings.runInBackground = runInBackground;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, scriptingBackend);
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, compilerConfiguration);
            }
        }
    }

    public sealed class WebGlBuildWorkflowTests
    {
        [SetUp]
        public void SetUp()
        {
            AddressablesWorkflow.UseAssetDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            AddressablesWorkflow.UseAssetDatabase();
        }

        [Test]
        public void BuildWebGl_SuccessRestoresLocalAndUseAssetDatabase()
        {
            ContinuousIntegration.ExecuteWebGlBuildWithCleanup(
                () => { },
                SimulateExistingBuildWorkflow,
                AssertExistingBuildWorkflow,
                AddressablesWorkflow.UseAssetDatabase);

            AssertLocalAndUseAssetDatabase();
        }

        [Test]
        public void BuildWebGl_ContentBuildFailureRestoresLocalAndPreservesBuildFailure()
        {
            var buildFailure = new BuildFailedException("Injected content build failure.");
            var playerBuildStarted = false;

            var actual = Assert.Throws<BuildFailedException>(() =>
                ContinuousIntegration.ExecuteWebGlBuildWithCleanup(
                    () => { },
                    () =>
                    {
                        SimulateExistingBuildWorkflow();
                        throw buildFailure;
                    },
                    () => playerBuildStarted = true,
                    AddressablesWorkflow.UseAssetDatabase));

            Assert.That(playerBuildStarted, Is.False);
            Assert.That(actual, Is.SameAs(buildFailure));
            AssertLocalAndUseAssetDatabase();
        }

        [Test]
        public void BuildWebGl_PlayerBuildFailureRestoresLocalAndPreservesBuildFailure()
        {
            var buildFailure = new BuildFailedException("Injected player build failure.");
            var contentBuildCompleted = false;

            var actual = Assert.Throws<BuildFailedException>(() =>
                ContinuousIntegration.ExecuteWebGlBuildWithCleanup(
                    () => { },
                    () =>
                    {
                        SimulateExistingBuildWorkflow();
                        contentBuildCompleted = true;
                    },
                    () =>
                    {
                        throw buildFailure;
                    },
                    AddressablesWorkflow.UseAssetDatabase));

            Assert.That(contentBuildCompleted, Is.True);
            Assert.That(actual, Is.SameAs(buildFailure));
            AssertLocalAndUseAssetDatabase();
        }

        [Test]
        public void BuildWebGl_CleanupFailureIsPropagatedAndRestoresActualSettings()
        {
            var cleanupFailure = new BuildFailedException("Injected cleanup failure.");

            var actual = Assert.Throws<BuildFailedException>(() =>
                ContinuousIntegration.ExecuteWebGlBuildWithCleanup(
                    () => { },
                    SimulateExistingBuildWorkflow,
                    () => { },
                    () =>
                    {
                        AddressablesWorkflow.UseAssetDatabase();
                        throw cleanupFailure;
                    }));

            Assert.That(actual, Is.SameAs(cleanupFailure));
            AssertLocalAndUseAssetDatabase();
        }

        [Test]
        public void BuildWebGl_WhenBuildAndCleanupFail_PreservesBuildFailureAndLogsCleanupFailure()
        {
            var buildFailure = new BuildFailedException("Injected player build failure.");
            var cleanupFailure = new BuildFailedException("Injected cleanup failure.");
            LogAssert.Expect(LogType.Exception, new Regex("Injected cleanup failure\\."));

            var actual = Assert.Throws<BuildFailedException>(() =>
                ContinuousIntegration.ExecuteWebGlBuildWithCleanup(
                    () => { },
                    SimulateExistingBuildWorkflow,
                    () =>
                    {
                        throw buildFailure;
                    },
                    () =>
                    {
                        AddressablesWorkflow.UseAssetDatabase();
                        throw cleanupFailure;
                    }));

            Assert.That(actual, Is.SameAs(buildFailure));
            AssertLocalAndUseAssetDatabase();
        }

        private static void SimulateExistingBuildWorkflow()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var remoteProfileId = settings.profileSettings.GetProfileId("RemoteTemplate");
            Assert.That(remoteProfileId, Is.Not.Empty);
            var packedPlayModeBuilderIndex = settings.DataBuilders.FindIndex(
                builder => builder is BuildScriptPackedPlayMode);
            Assert.That(packedPlayModeBuilderIndex, Is.GreaterThanOrEqualTo(0));

            settings.activeProfileId = remoteProfileId;
            settings.ActivePlayModeDataBuilderIndex = packedPlayModeBuilderIndex;
            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssertExistingBuildWorkflow();
        }

        private static void AssertExistingBuildWorkflow()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            Assert.That(
                settings.profileSettings.GetProfileName(settings.activeProfileId),
                Is.EqualTo("RemoteTemplate"));
            Assert.That(
                settings.DataBuilders[settings.ActivePlayModeDataBuilderIndex],
                Is.TypeOf<BuildScriptPackedPlayMode>());
            Assert.That(
                settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer));
        }

        private static void AssertLocalAndUseAssetDatabase()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            Assert.That(
                settings.profileSettings.GetProfileName(settings.activeProfileId),
                Is.EqualTo("Local"));
            Assert.That(
                settings.DataBuilders[settings.ActivePlayModeDataBuilderIndex],
                Is.TypeOf<BuildScriptFastMode>());
            Assert.That(
                settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer));
        }
    }
}
