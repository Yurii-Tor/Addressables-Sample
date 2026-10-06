using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AddressablesSample.Game.Presentation
{
    public sealed class PointerSelectionInput : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GraphicRaycaster _uiRaycaster;
        [SerializeField] private LayerMask _raycastLayers = ~0;
        [SerializeField, Min(1f)] private float _maxDistance = 100f;

        private InputAction _pressAction;
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(8);

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

        internal void Configure(
            Camera raycastCamera,
            GraphicRaycaster uiRaycaster,
            LayerMask raycastLayers,
            float maxDistance)
        {
            _camera = raycastCamera;
            _uiRaycaster = uiRaycaster;
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

        internal bool IsOverInteractiveUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (_uiRaycaster == null || eventSystem == null)
            {
                return false;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };
            _uiRaycastResults.Clear();
            _uiRaycaster.Raycast(eventData, _uiRaycastResults);
            for (var index = 0; index < _uiRaycastResults.Count; index++)
            {
                var hit = _uiRaycastResults[index].gameObject;
                if (hit.GetComponentInParent<Button>() != null ||
                    hit.GetComponentInParent<ScrollRect>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            if (context.control.device is Pointer pointer)
            {
                var screenPosition = GetCurrentPosition(context.control.device, pointer);
                if (IsOverInteractiveUI(screenPosition))
                {
                    return;
                }

                Selection?.Invoke(EvaluateSelection(screenPosition));
                return;
            }

            Selection?.Invoke(false);
        }

        private static Vector2 GetCurrentPosition(InputDevice device, Pointer pointer)
        {
            if (device is Touchscreen touchscreen)
            {
                return touchscreen.primaryTouch.position.ReadValue();
            }

            return pointer.position.ReadValue();
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
