using System;
using System.Collections.Generic;
using AddressablesSample.Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AddressablesSample.Game.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "AddressablesSample/Game Config")]
    public sealed class GameConfig : ScriptableObject, IGameConfiguration
    {
        [SerializeField] private AssetReferenceGameObject _targetPrefab;
        [SerializeField] private AssetReferenceTexture2D _fallbackTexture;
        [SerializeField] private AssetReferenceTexture2D[] _roundTextures = Array.Empty<AssetReferenceTexture2D>();

        public AssetReferenceGameObject TargetPrefab => _targetPrefab;
        public AssetReferenceTexture2D FallbackTexture => _fallbackTexture;
        public IReadOnlyList<AssetReferenceTexture2D> RoundTextures => _roundTextures;

        public bool TryCreateDefinition(out GameDefinition definition, out string error)
        {
            definition = null;

            if (!IsValid(_targetPrefab))
            {
                error = "Target prefab Addressable reference is missing or invalid.";
                return false;
            }

            if (!IsValid(_fallbackTexture))
            {
                error = "Fallback texture Addressable reference is missing or invalid.";
                return false;
            }

            if (_roundTextures == null || _roundTextures.Length < 2)
            {
                error = "At least two round texture Addressable references are required.";
                return false;
            }

            var fallbackKey = _fallbackTexture.RuntimeKey.ToString();
            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
            var roundKeys = new object[_roundTextures.Length];

            for (var index = 0; index < _roundTextures.Length; index++)
            {
                var roundTexture = _roundTextures[index];
                if (!IsValid(roundTexture))
                {
                    error = $"Round texture reference at index {index} is missing or invalid.";
                    return false;
                }

                var roundKey = roundTexture.RuntimeKey.ToString();
                if (string.Equals(roundKey, fallbackKey, StringComparison.Ordinal))
                {
                    error = $"Round texture reference at index {index} duplicates the fallback texture.";
                    return false;
                }

                if (!uniqueKeys.Add(roundKey))
                {
                    error = $"Round texture reference at index {index} is duplicated.";
                    return false;
                }

                roundKeys[index] = roundTexture.RuntimeKey;
            }

            definition = new GameDefinition(_targetPrefab.RuntimeKey, _fallbackTexture.RuntimeKey, roundKeys);
            error = null;
            return true;
        }

        internal void Configure(
            AssetReferenceGameObject targetPrefab,
            AssetReferenceTexture2D fallbackTexture,
            AssetReferenceTexture2D[] roundTextures)
        {
            _targetPrefab = targetPrefab;
            _fallbackTexture = fallbackTexture;
            _roundTextures = roundTextures ?? Array.Empty<AssetReferenceTexture2D>();
        }

        private static bool IsValid(AssetReference reference)
        {
            return reference != null && reference.RuntimeKeyIsValid() && !string.IsNullOrWhiteSpace(reference.AssetGUID);
        }
    }
}
