using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AddressablesSample.Game.Editor
{
    internal static class GeneratedProjectFingerprints
    {
        // TestTaskSetup owns these folders. Keep build output and unrelated project assets out of scope.
        private static readonly string[] GeneratedFolders =
        {
            TestTaskPaths.ConfigFolder, TestTaskPaths.MaterialsFolder, TestTaskPaths.MeshesFolder,
            TestTaskPaths.PrefabsFolder, TestTaskPaths.ScenesFolder, TestTaskPaths.TexturesFolder,
            TestTaskPaths.RenderingFolder
        };

        // AddressableAssetSettingsDefaultObject.GetSettings(true) creates the source settings,
        // builders and templates. ConfigureAddressables authors the groups, schemas and profiles.
        private static readonly string[] AddressablesFolders =
        {
            "Assets/AddressableAssetsData/DataBuilders",
            "Assets/AddressableAssetsData/AssetGroupTemplates",
            "Assets/AddressableAssetsData/AssetGroups"
        };

        private static readonly string[] AddressablesRootAssets =
        {
            "Assets/AddressableAssetsData/DefaultObject.asset",
            "Assets/AddressableAssetsData/AddressableAssetSettings.asset",
            "Assets/AddressableAssetsData/AddressableAssetGroupSortSettings.asset",
            "Assets/AddressableAssetsData/ProfileDataSourceSettings.asset"
        };

        internal static SortedDictionary<string, string> CaptureGeneratedProject(string projectRoot)
        {
            var files = new List<string>
            {
                TestTaskPaths.Root + ".meta",
                "Assets/AddressableAssetsData.meta",
                "ProjectSettings/EditorBuildSettings.asset",
                "Assets/AddressableAssetsData/AssetGroups/Schemas.meta"
            };

            foreach (var folder in GeneratedFolders.Concat(AddressablesFolders))
            {
                files.Add(folder + ".meta");
            }

            foreach (var asset in AddressablesRootAssets)
            {
                files.Add(asset);
                files.Add(asset + ".meta");
            }

            foreach (var texture in TestTaskPaths.RoundTexturePaths)
            {
                files.Add(texture + ".meta");
            }

            return Capture(projectRoot, files, GeneratedFolders.Concat(AddressablesFolders));
        }

        // Exact files plus bounded source directories make additions and removals observable.
        internal static SortedDictionary<string, string> Capture(
            string projectRoot, IEnumerable<string> exactFiles, IEnumerable<string> scopedDirectories)
        {
            var root = Path.GetFullPath(projectRoot);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in exactFiles)
            {
                paths.Add(NormalizeRelativePath(root, file));
            }

            foreach (var directory in scopedDirectories)
            {
                var relativeDirectory = NormalizeRelativePath(root, directory);
                var absoluteDirectory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteDirectory))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(absoluteDirectory, "*", SearchOption.AllDirectories))
                {
                    paths.Add(NormalizeRelativePath(root, file));
                }
            }

            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                foreach (var path in paths.OrderBy(path => path, StringComparer.Ordinal))
                {
                    var absolutePath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(absolutePath))
                    {
                        continue;
                    }

                    using (var stream = File.OpenRead(absolutePath))
                    {
                        result.Add(path, BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant());
                    }
                }
            }

            return result;
        }

        internal static IReadOnlyList<string> Compare(
            IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
        {
            var differences = new List<string>();
            foreach (var path in first.Keys.Union(second.Keys).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!first.ContainsKey(path))
                {
                    differences.Add("added: " + path);
                }
                else if (!second.ContainsKey(path))
                {
                    differences.Add("removed: " + path);
                }
                else if (!string.Equals(first[path], second[path], StringComparison.Ordinal))
                {
                    differences.Add("changed: " + path);
                }
            }

            return differences;
        }

        private static string NormalizeRelativePath(string root, string path)
        {
            var absolute = Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Fingerprint path is outside the project: " + path);
            }

            return absolute.Substring(rootPrefix.Length).Replace('\\', '/');
        }
    }
}
