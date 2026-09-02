using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                {
                    throw new BuildFailedException(
                        "The active build target must be WebGL before building the demo. " +
                        "Actual target: " + EditorUserBuildSettings.activeBuildTarget + ".");
                }

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
            });
        }

        private static void ConfigureWebGlPlayerSettings()
        {
            // GitHub Pages serves static files without the Content-Encoding headers that Brotli or
            // gzip Unity builds require, and the decompression fallback costs load time for no
            // benefit at this size. Uncompressed output just works on any static host.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
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
