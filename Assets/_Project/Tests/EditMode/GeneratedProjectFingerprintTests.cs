using System;
using System.IO;
using System.Linq;
using AddressablesSample.Game.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class GeneratedProjectFingerprintTests
    {
        private string root;
        private string fixtureParent;

        [SetUp]
        public void SetUp()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            fixtureParent = Path.GetFullPath(Path.Combine(projectRoot, "Temp/P03FingerprintTests"));
            root = Path.GetFullPath(Path.Combine(fixtureParent, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.Combine(root, "Assets/Generated"));
        }

        [TearDown]
        public void TearDown()
        {
            var parentPrefix = fixtureParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(root).StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Fixture cleanup escaped its task-owned directory.");
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void EqualManifestsHaveNoDifferences()
        {
            Write("Assets/Generated/Game.asset", "same");
            var first = Capture();
            var second = Capture();
            Assert.That(GeneratedProjectFingerprints.Compare(first, second), Is.Empty);
            Assert.That(first.Values.Single(), Has.Length.EqualTo(64));
        }

        [Test]
        public void AdditionAndRemovalReportRelativePaths()
        {
            Write("Assets/Generated/Old.asset", "old");
            var first = Capture();
            File.Delete(Path.Combine(root, "Assets/Generated/Old.asset"));
            Write("Assets/Generated/New.asset", "new");
            Assert.That(GeneratedProjectFingerprints.Compare(first, Capture()), Is.EqualTo(new[]
            {
                "added: Assets/Generated/New.asset", "removed: Assets/Generated/Old.asset"
            }));
        }

        [Test]
        public void ChangedContentsAndGuidMetaDriftAreDetected()
        {
            Write("Assets/Generated/Game.asset", "first");
            Write("Assets/Generated/Game.asset.meta", "guid: 1111");
            var first = Capture();
            Write("Assets/Generated/Game.asset", "second");
            Write("Assets/Generated/Game.asset.meta", "guid: 2222");
            Assert.That(GeneratedProjectFingerprints.Compare(first, Capture()), Is.EqualTo(new[]
            {
                "changed: Assets/Generated/Game.asset",
                "changed: Assets/Generated/Game.asset.meta"
            }));
        }

        [Test]
        public void PathsAreSortedAndSlashesAreNormalized()
        {
            Write("Assets/Generated/Z.asset", "z");
            Write("Assets/Generated/A.asset", "a");
            var result = GeneratedProjectFingerprints.Capture(root,
                new[] { "Assets\\Generated\\Z.asset", "Assets/Generated/A.asset" },
                Array.Empty<string>());
            Assert.That(result.Keys, Is.EqualTo(new[]
            {
                "Assets/Generated/A.asset", "Assets/Generated/Z.asset"
            }));
        }

        [Test]
        public void OutputNoiseOutsideOwnedScopeIsIgnored()
        {
            Write("Assets/Generated/Game.asset", "source");
            var first = Capture();
            foreach (var folder in new[] { "Library", "Temp", "Logs", "obj", "ServerData", "Builds" })
            {
                Write(folder + "/noise.txt", folder);
            }

            Assert.That(GeneratedProjectFingerprints.Compare(first, Capture()), Is.Empty);
        }

        private System.Collections.Generic.SortedDictionary<string, string> Capture()
        {
            return GeneratedProjectFingerprints.Capture(root, Array.Empty<string>(),
                new[] { "Assets/Generated" });
        }

        private void Write(string relativePath, string value)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, value);
        }
    }
}
