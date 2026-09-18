using System.Collections;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Presentation
{
    public sealed class TargetView : MonoBehaviour, ITargetView
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Vector4 TextureTransform = new Vector4(1f, 1f, 0f, 0f);
        private static readonly Color MissEmission = new Color(3f, 0f, 0f, 1f);

        [SerializeField] private Renderer _renderer;
        [SerializeField] private Collider _collider;
        [SerializeField, Min(0.05f)] private float _errorFlashDuration = 0.25f;
        [SerializeField] private float _idleSpinDegreesPerSecond = 22f;
        [SerializeField, Min(1f)] private float _punchRecoverySpeed = 6f;

        private MaterialPropertyBlock _propertyBlock;
        private Texture2D _texture;
        private Color _color = Color.white;
        private Color _emission = Color.black;
        private Coroutine _flashCoroutine;
        private float _punchScale = 1f;

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

        private void Update()
        {
            if (_renderer == null || _collider == null || !_renderer.enabled)
            {
                return;
            }

            var delta = Time.deltaTime;
            transform.Rotate(Vector3.up, _idleSpinDegreesPerSecond * delta, Space.World);

            if (!Mathf.Approximately(_punchScale, 1f))
            {
                _punchScale = Mathf.Lerp(_punchScale, 1f, Mathf.Clamp01(_punchRecoverySpeed * delta));
                if (Mathf.Abs(_punchScale - 1f) < 0.002f)
                {
                    _punchScale = 1f;
                }

                transform.localScale = Vector3.one * _punchScale;
            }
        }

        public void ApplyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                throw new System.ArgumentNullException(nameof(texture));
            }

            EnsureValidReferences();
            var isReplacement = _texture != null && _texture != texture;
            _texture = texture;
            ApplyPropertyBlock();

            if (isReplacement)
            {
                _punchScale = 1.18f;
            }
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
            _emission = Color.black;
            if (IsValid)
            {
                ApplyPropertyBlock();
            }
        }

        public void Clear()
        {
            StopFeedback();
            _texture = null;
            _punchScale = 1f;
            transform.localScale = Vector3.one;

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

        internal void Configure(
            Renderer targetRenderer,
            Collider targetCollider,
            float flashDuration,
            float idleSpinDegreesPerSecond,
            float punchRecoverySpeed)
        {
            _renderer = targetRenderer;
            _collider = targetCollider;
            _errorFlashDuration = Mathf.Max(0.05f, flashDuration);
            _idleSpinDegreesPerSecond = idleSpinDegreesPerSecond;
            _punchRecoverySpeed = Mathf.Max(1f, punchRecoverySpeed);
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
            _emission = MissEmission;
            ApplyPropertyBlock();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.4f, _errorFlashDuration));
            _color = Color.white;
            _emission = Color.black;
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

            _propertyBlock.SetVector(BaseMapStId, TextureTransform);
            _propertyBlock.SetColor(BaseColorId, _color);
            _propertyBlock.SetColor(EmissionColorId, _emission);
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
