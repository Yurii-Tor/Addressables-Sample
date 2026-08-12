using AddressablesSample.Game.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class GameConfigTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void MissingStartupReference_IsRejected()
        {
            _config.Configure(null, TextureReference(2), new[] { TextureReference(3), TextureReference(4) });

            Assert.That(_config.TryCreateDefinition(out _, out var error), Is.False);
            StringAssert.Contains("Target prefab", error);
        }

        [Test]
        public void FewerThanTwoRounds_IsRejected()
        {
            _config.Configure(PrefabReference(1), TextureReference(2), new[] { TextureReference(3) });

            Assert.That(_config.TryCreateDefinition(out _, out var error), Is.False);
            StringAssert.Contains("At least two", error);
        }

        [Test]
        public void MissingRound_IsRejected()
        {
            _config.Configure(
                PrefabReference(1),
                TextureReference(2),
                new AssetReferenceTexture2D[] { TextureReference(3), null });

            Assert.That(_config.TryCreateDefinition(out _, out var error), Is.False);
            StringAssert.Contains("index 1", error);
        }

        [Test]
        public void DuplicateRound_IsRejected()
        {
            _config.Configure(
                PrefabReference(1),
                TextureReference(2),
                new[] { TextureReference(3), TextureReference(3) });

            Assert.That(_config.TryCreateDefinition(out _, out var error), Is.False);
            StringAssert.Contains("duplicated", error);
        }

        [Test]
        public void FallbackInRoundList_IsRejected()
        {
            _config.Configure(
                PrefabReference(1),
                TextureReference(2),
                new[] { TextureReference(3), TextureReference(2) });

            Assert.That(_config.TryCreateDefinition(out _, out var error), Is.False);
            StringAssert.Contains("fallback", error);
        }

        [Test]
        public void ValidReferences_CreateDefinitionInSerializedOrder()
        {
            var prefab = PrefabReference(1);
            var fallback = TextureReference(2);
            var first = TextureReference(3);
            var second = TextureReference(4);
            _config.Configure(prefab, fallback, new[] { first, second });

            Assert.That(_config.TryCreateDefinition(out var definition, out var error), Is.True, error);
            Assert.That(definition.TargetPrefabKey, Is.EqualTo(prefab.RuntimeKey));
            Assert.That(definition.FallbackTextureKey, Is.EqualTo(fallback.RuntimeKey));
            Assert.That(definition.RoundTextureKeys, Is.EqualTo(new[] { first.RuntimeKey, second.RuntimeKey }));
        }

        private static AssetReferenceGameObject PrefabReference(int value)
        {
            return new AssetReferenceGameObject(Guid(value));
        }

        private static AssetReferenceTexture2D TextureReference(int value)
        {
            return new AssetReferenceTexture2D(Guid(value));
        }

        private static string Guid(int value)
        {
            return value.ToString("x32");
        }
    }
}
