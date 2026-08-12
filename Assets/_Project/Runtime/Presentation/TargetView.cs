using System.Collections;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Presentation
{
    public sealed class TargetView : MonoBehaviour, ITargetView
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _renderer;
        [SerializeField] private Collider _collider;
        [SerializeField, Min(0.05f)] private float _errorFlashDuration = 0.25f;

        private MaterialPropertyBlock _propertyBlock;
        private Texture2D _texture;
        private Color _color = Color.white;
        private Coroutine _flashCoroutine;

        public bool IsValid => this != null && _renderer != null && _collider != null;
        public bool IsFlashing => _flashCoroutine != null;
        public Texture2D AppliedTexture => _texture;
        public Renderer TargetRenderer => _renderer;
        public Collider TargetCollider => _collider;

        private void Awake()
        {
            EnsureValidReferences();
            _renderer.enabled = false;
            _collider.enabled = false;
            EnsurePropertyBlock();
            ApplyPropertyBlock();
        }

        public void ApplyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                throw new System.ArgumentNullException(nameof(texture));
            }

            EnsureValidReferences();
            _texture = texture;
            ApplyPropertyBlock();
        }

        public void SetInteractionEnabled(bool enabled)
        {
            EnsureValidReferences();
            _collider.enabled = enabled;
        }

        public void FlashError()
        {
            EnsureValidReferences();
            StopFeedback();
            _flashCoroutine = StartCoroutine(FlashErrorRoutine());
        }

        public void StopFeedback()
        {
            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            _color = Color.white;
            if (IsValid)
            {
                ApplyPropertyBlock();
            }
        }

        public void Clear()
        {
            StopFeedback();
            _texture = null;

            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
                _renderer.enabled = false;
            }

            if (_collider != null)
            {
                _collider.enabled = false;
            }
        }

        internal void Configure(Renderer targetRenderer, Collider targetCollider, float flashDuration)
        {
            _renderer = targetRenderer;
            _collider = targetCollider;
            _errorFlashDuration = Mathf.Max(0.05f, flashDuration);
        }

        internal void SetPresentationEnabled(bool enabled)
        {
            EnsureValidReferences();
            _renderer.enabled = enabled;
            if (!enabled)
            {
                _collider.enabled = false;
            }
        }

        private IEnumerator FlashErrorRoutine()
        {
            _color = Color.red;
            ApplyPropertyBlock();
            yield return new WaitForSecondsRealtime(_errorFlashDuration);
            _color = Color.white;
            ApplyPropertyBlock();
            _flashCoroutine = null;
        }

        private void EnsurePropertyBlock()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }
        }

        private void ApplyPropertyBlock()
        {
            EnsureValidReferences();
            EnsurePropertyBlock();
            _propertyBlock.Clear();
            if (_texture != null)
            {
                _propertyBlock.SetTexture(BaseMapId, _texture);
            }

            _propertyBlock.SetColor(BaseColorId, _color);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        private void EnsureValidReferences()
        {
            if (_renderer == null || _collider == null)
            {
                throw new MissingReferenceException("TargetView requires Renderer and Collider references.");
            }
        }
    }
}
