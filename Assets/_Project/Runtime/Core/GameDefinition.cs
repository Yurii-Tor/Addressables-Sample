using System;
using System.Collections.Generic;

namespace AddressablesSample.Game.Core
{
    public sealed class GameDefinition
    {
        private readonly object[] _roundTextureKeys;

        public GameDefinition(object targetPrefabKey, object fallbackTextureKey, IReadOnlyList<object> roundTextureKeys)
        {
            TargetPrefabKey = targetPrefabKey ?? throw new ArgumentNullException(nameof(targetPrefabKey));
            FallbackTextureKey = fallbackTextureKey ?? throw new ArgumentNullException(nameof(fallbackTextureKey));

            if (roundTextureKeys == null)
            {
                throw new ArgumentNullException(nameof(roundTextureKeys));
            }

            _roundTextureKeys = new object[roundTextureKeys.Count];
            for (var index = 0; index < roundTextureKeys.Count; index++)
            {
                _roundTextureKeys[index] = roundTextureKeys[index] ??
                    throw new ArgumentException("Round texture keys cannot contain null values.", nameof(roundTextureKeys));
            }
        }

        public object TargetPrefabKey { get; }
        public object FallbackTextureKey { get; }
        public IReadOnlyList<object> RoundTextureKeys => _roundTextureKeys;
    }
}
