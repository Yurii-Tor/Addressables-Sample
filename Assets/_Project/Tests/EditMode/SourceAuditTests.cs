using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class SourceAuditTests
    {
        [Test]
        public void RuntimeSource_HasNoForbiddenWaitsOrUnownedAddressablesRelease()
        {
            var runtimeRoot = Path.Combine(Application.dataPath, "_Project", "Runtime");
            var files = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                var relativePath = file.Substring(Application.dataPath.Length + 1);
                StringAssert.DoesNotContain("WaitForCompletion(", source, relativePath);
                StringAssert.DoesNotContain("Task.Wait(", source, relativePath);
                StringAssert.DoesNotContain(".Completion.Result", source, relativePath);

                if (!file.EndsWith("UnityAddressableAssetLoader.cs"))
                {
                    StringAssert.DoesNotContain("Addressables.Release(", source, relativePath);
                    StringAssert.DoesNotContain("UnityAddressables.Release(", source, relativePath);
                }
            }
        }
    }
}
