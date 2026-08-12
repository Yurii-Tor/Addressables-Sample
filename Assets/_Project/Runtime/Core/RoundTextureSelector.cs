using System;
using System.Collections.Generic;

namespace AddressablesSample.Game.Core
{
    public sealed class RoundTextureSelector
    {
        private readonly object[] _keys;
        private int _nextIndex;

        public RoundTextureSelector(IReadOnlyList<object> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (keys.Count < 2)
            {
                throw new ArgumentException("At least two round texture keys are required.", nameof(keys));
            }

            _keys = new object[keys.Count];
            for (var index = 0; index < keys.Count; index++)
            {
                _keys[index] = keys[index] ??
                    throw new ArgumentException("Round texture keys cannot contain null values.", nameof(keys));
            }
        }

        public int Count => _keys.Length;
        public int NextIndex => _nextIndex;

        public object Next()
        {
            var key = _keys[_nextIndex];
            _nextIndex = (_nextIndex + 1) % _keys.Length;
            return key;
        }
    }
}
