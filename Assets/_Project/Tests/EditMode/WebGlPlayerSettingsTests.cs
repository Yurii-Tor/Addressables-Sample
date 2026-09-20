using AddressablesSample.Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

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
}
