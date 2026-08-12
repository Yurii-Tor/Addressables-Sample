using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AddressablesSample.Game.Presentation
{
    public sealed class PointerSelectionInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _raycastLayers = ~0;
        [SerializeField, Min(1f)] private float _maxDistance = 100f;

        private InputAction _pressAction;

        public event Action<bool> Selection;

        private void OnEnable()
        {
            EnsureActions();
            _pressAction.Enable();
            _pressAction.performed += OnPressPerformed;
        }

        private void OnDisable()
        {
            if (_pressAction != null)
            {
                _pressAction.performed -= OnPressPerformed;
                _pressAction.Disable();
            }
        }

        private void OnDestroy()
        {
            _pressAction?.Dispose();
            _pressAction = null;
        }

        internal void Configure(Camera raycastCamera, LayerMask raycastLayers, float maxDistance)
        {
            _camera = raycastCamera;
            _raycastLayers = raycastLayers;
            _maxDistance = Mathf.Max(1f, maxDistance);
        }

        internal bool EvaluateSelection(Vector2 screenPosition)
        {
            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out var hit, _maxDistance, _raycastLayers, QueryTriggerInteraction.Ignore) &&
                   hit.collider.GetComponentInParent<TargetView>() != null;
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            if (context.control.device is Pointer pointer)
            {
                Selection?.Invoke(EvaluateSelection(pointer.position.ReadValue()));
                return;
            }

            Selection?.Invoke(false);
        }

        private void EnsureActions()
        {
            if (_pressAction != null)
            {
                return;
            }

            _pressAction = new InputAction(
                "Pointer Press",
                InputActionType.Button,
                "<Pointer>/press");
        }
    }
}
