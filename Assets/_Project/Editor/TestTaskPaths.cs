using System;
using System.Collections.Generic;
using System.IO;

namespace AddressablesSample.Game.Editor
{
    internal static class TestTaskPaths
    {
        public const string Root = "Assets/_Project";
        public const string ConfigFolder = Root + "/Config";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string ScenesFolder = Root + "/Scenes";
        public const string TexturesFolder = Root + "/Textures";
        public const string RenderingFolder = Root + "/Rendering";

        public const string ConfigAsset = ConfigFolder + "/GameConfig.asset";
        public const string MaterialAsset = MaterialsFolder + "/Target.mat";
        public const string TargetPrefab = PrefabsFolder + "/Target.prefab";
        public const string GameScene = ScenesFolder + "/Game.unity";
        public const string FallbackTexture = TexturesFolder + "/FallbackTexture.asset";
        public const string VolumeProfile = RenderingFolder + "/GameVolumeProfile.asset";

        public const string StartupGroup = "Game-Startup";
        public const string RoundsGroup = "Game-RoundTextures";
        public const string RoundLabel = "round-texture";
        public const string LocalProfile = "Local";
        public const string RemoteProfile = "RemoteTemplate";
        public const string HostedProfile = "Cloudflare";
        public const string HostedBaseUrl = "https://addressables-sample.pages.dev";
        public const string RoundBuildPathVariable = "Round.BuildPath";
        public const string RoundLoadPathVariable = "Round.LoadPath";

        public static readonly string[] RoundStems =
        {
            "ant",
            "budgie",
            "bull",
            "crab",
            "cute-hamster",
            "dolphin",
            "dragon",
            "fish",
            "jellyfish",
            "phoenix"
        };

        public static IReadOnlyList<string> RoundTexturePaths
        {
            get
            {
                var paths = new string[RoundStems.Length];
                for (var index = 0; index < RoundStems.Length; index++)
                {
                    paths[index] = "Assets/Textures/icons8-" + RoundStems[index] + "-96.png";
                }

                return paths;
            }
        }

        public static void EnsureFolder(string assetFolder)
        {
            var normalized = assetFolder.Replace('\\', '/');
            if (UnityEditor.AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            var name = Path.GetFileName(normalized);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Invalid Unity asset folder: " + assetFolder);
            }

            EnsureFolder(parent);
            UnityEditor.AssetDatabase.CreateFolder(parent, name);
        }
    }
}
