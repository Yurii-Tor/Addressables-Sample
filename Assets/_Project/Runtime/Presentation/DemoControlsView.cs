using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AddressablesSample.Game.Presentation
{
    /// <summary>Routes generated uGUI controls to the bootstrapper-owned P05 scenario commands.</summary>
    public sealed class DemoControlsView : MonoBehaviour
    {
        [SerializeField] private GameBootstrapper _bootstrapper;
        [SerializeField] private RectTransform _safeAreaRoot;
        [SerializeField] private RectTransform _controlsPanel;
        [SerializeField] private Button _slowLoadButton;
        [SerializeField] private Button _simulateFailureButton;
        [SerializeField] private Button _replaceRequestButton;
        [SerializeField] private Button _toggleDiagnosticsButton;
        [SerializeField] private Text _toggleLabel;

        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
        private bool _hasLayout;
        private bool _diagnosticsVisible = true;

        internal Button SlowLoadButton => _slowLoadButton;
        internal Button SimulateFailureButton => _simulateFailureButton;
        internal Button ReplaceRequestButton => _replaceRequestButton;
        internal Button ToggleDiagnosticsButton => _toggleDiagnosticsButton;
        internal bool DiagnosticsVisible => _diagnosticsVisible;

        internal void Configure(
            GameBootstrapper bootstrapper,
            RectTransform safeAreaRoot,
            RectTransform controlsPanel,
            Button slowLoadButton,
            Button simulateFailureButton,
            Button replaceRequestButton,
            Button toggleDiagnosticsButton,
            Text toggleLabel,
            bool diagnosticsVisible)
        {
            _bootstrapper = bootstrapper;
            _safeAreaRoot = safeAreaRoot;
            _controlsPanel = controlsPanel;
            _slowLoadButton = slowLoadButton;
            _simulateFailureButton = simulateFailureButton;
            _replaceRequestButton = replaceRequestButton;
            _toggleDiagnosticsButton = toggleDiagnosticsButton;
            _toggleLabel = toggleLabel;
            _diagnosticsVisible = diagnosticsVisible;
            ApplyVisibility();
        }

        private void OnEnable()
        {
            if (_slowLoadButton != null)
            {
                _slowLoadButton.onClick.AddListener(OnSlowLoadClicked);
            }

            if (_simulateFailureButton != null)
            {
                _simulateFailureButton.onClick.AddListener(OnFailureClicked);
            }

            if (_replaceRequestButton != null)
            {
                _replaceRequestButton.onClick.AddListener(OnReplacementClicked);
            }

            if (_toggleDiagnosticsButton != null)
            {
                _toggleDiagnosticsButton.onClick.AddListener(OnToggleClicked);
            }

            ApplyVisibility();
            RefreshInteractability();
            ApplyResponsiveLayout(true);
        }

        private void OnDisable()
        {
            if (_slowLoadButton != null)
            {
                _slowLoadButton.onClick.RemoveListener(OnSlowLoadClicked);
            }

            if (_simulateFailureButton != null)
            {
                _simulateFailureButton.onClick.RemoveListener(OnFailureClicked);
            }

            if (_replaceRequestButton != null)
            {
                _replaceRequestButton.onClick.RemoveListener(OnReplacementClicked);
            }

            if (_toggleDiagnosticsButton != null)
            {
                _toggleDiagnosticsButton.onClick.RemoveListener(OnToggleClicked);
            }
        }

        private void Update()
        {
            ReadDiagnosticsShortcut();
            RefreshInteractability();
            ApplyResponsiveLayout(false);
        }

        internal void SetDiagnosticsVisible(bool visible)
        {
            _diagnosticsVisible = visible;
            ApplyVisibility();
        }

        internal void RefreshInteractability()
        {
            var canStartScenario = _bootstrapper != null && _bootstrapper.CanStartDemoScenarios;
            SetInteractable(_slowLoadButton, canStartScenario);
            SetInteractable(_simulateFailureButton, canStartScenario);
            SetInteractable(_replaceRequestButton, canStartScenario);

            // Keep the visibility control available in startup, fatal, and disposed states.
            SetInteractable(_toggleDiagnosticsButton, true);
        }

        private void ReadDiagnosticsShortcut()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame))
            {
                SetDiagnosticsVisible(!_diagnosticsVisible);
            }
        }

        private void ApplyVisibility()
        {
            if (_controlsPanel != null && _controlsPanel.gameObject.activeSelf != _diagnosticsVisible)
            {
                _controlsPanel.gameObject.SetActive(_diagnosticsVisible);
            }

            if (_toggleLabel != null)
            {
                _toggleLabel.text = _diagnosticsVisible ? "Hide diagnostics" : "Show diagnostics";
            }
        }

        private void ApplyResponsiveLayout(bool force)
        {
            if (_safeAreaRoot == null || _controlsPanel == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (!force && _hasLayout && safeArea == _lastSafeArea && resolution == _lastResolution)
            {
                return;
            }

            _hasLayout = true;
            _lastSafeArea = safeArea;
            _lastResolution = resolution;

            _safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            _safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;

            var safeWidth = Mathf.Max(1f, safeArea.width);
            var safeHeight = Mathf.Max(1f, safeArea.height);
            var portrait = safeWidth < safeHeight;
            if (portrait)
            {
                _controlsPanel.anchorMin = new Vector2(0f, 0f);
                _controlsPanel.anchorMax = new Vector2(1f, 0f);
                _controlsPanel.pivot = new Vector2(0.5f, 0f);
                _controlsPanel.sizeDelta = new Vector2(0f, 232f);
                _controlsPanel.anchoredPosition = new Vector2(0f, 12f);
            }
            else
            {
                var panelWidth = Mathf.Min(340f, safeWidth * 0.36f);
                _controlsPanel.anchorMin = new Vector2(0f, 1f);
                _controlsPanel.anchorMax = new Vector2(0f, 1f);
                _controlsPanel.pivot = new Vector2(0f, 1f);
                _controlsPanel.sizeDelta = new Vector2(panelWidth, 232f);
                _controlsPanel.anchoredPosition = new Vector2(12f, -124f);
            }
        }

        private void OnSlowLoadClicked()
        {
            _bootstrapper?.TryStartSlowDemoScenario();
        }

        private void OnFailureClicked()
        {
            _bootstrapper?.TryStartFailureDemoScenario();
        }

        private void OnReplacementClicked()
        {
            _bootstrapper?.TryStartReplacementDemoScenario();
        }

        private void OnToggleClicked()
        {
            SetDiagnosticsVisible(!_diagnosticsVisible);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null && button.interactable != interactable)
            {
                button.interactable = interactable;
            }
        }
    }
}
