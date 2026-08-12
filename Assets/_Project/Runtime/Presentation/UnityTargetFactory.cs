using System;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Presentation
{
    public sealed class UnityTargetFactory : ITargetFactory
    {
        private readonly Transform _spawn;

        public UnityTargetFactory(Transform spawn)
        {
            _spawn = spawn == null ? throw new ArgumentNullException(nameof(spawn)) : spawn;
        }

        public ITargetView Create(GameObject prefab, Texture2D initialTexture)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (initialTexture == null)
            {
                throw new ArgumentNullException(nameof(initialTexture));
            }

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(
                    prefab,
                    _spawn.position,
                    _spawn.rotation,
                    _spawn);
                instance.name = "Target";

                var target = instance.GetComponent<TargetView>();
                if (target == null || !target.IsValid)
                {
                    throw new InvalidOperationException("The Addressable target prefab is missing a valid TargetView.");
                }

                target.SetPresentationEnabled(false);
                target.ApplyTexture(initialTexture);
                target.SetPresentationEnabled(true);
                target.SetInteractionEnabled(false);
                return target;
            }
            catch
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                throw;
            }
        }

        public void Destroy(ITargetView target)
        {
            if (target is Component component && component != null)
            {
                UnityEngine.Object.Destroy(component.gameObject);
            }
        }
    }
}
