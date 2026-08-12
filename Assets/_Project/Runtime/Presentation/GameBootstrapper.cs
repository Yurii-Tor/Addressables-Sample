using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Config;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Presentation
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private HudView _hud;
        [SerializeField] private PointerSelectionInput _selectionInput;
        [SerializeField] private Transform _targetSpawn;

        private GameController _controller;

        public GameController Controller => _controller;

        private async void Start()
        {
            if (_controller != null)
            {
                return;
            }

            if (_config == null || _hud == null || _selectionInput == null || _targetSpawn == null)
            {
                Debug.LogError("GameBootstrapper is missing one or more required scene references.", this);
                return;
            }

            _controller = new GameController(
                _config,
                new UnityAddressableAssetLoader(),
                _hud,
                new UnityTargetFactory(_targetSpawn),
                new UnityGameDiagnostics());

            _selectionInput.Selection += OnSelection;
            await _controller.InitializeAsync();
        }

        private void OnDestroy()
        {
            if (_selectionInput != null)
            {
                _selectionInput.Selection -= OnSelection;
            }

            _controller?.Dispose();
            _controller = null;
        }

        internal void Configure(
            GameConfig config,
            HudView hud,
            PointerSelectionInput selectionInput,
            Transform targetSpawn)
        {
            _config = config;
            _hud = hud;
            _selectionInput = selectionInput;
            _targetSpawn = targetSpawn;
        }

        internal void InstallControllerForTests(GameController controller)
        {
            if (_controller != null)
            {
                throw new System.InvalidOperationException("GameBootstrapper already owns a controller.");
            }

            _controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
        }

        private void OnSelection(bool hitTarget)
        {
            _controller?.HandleSelection(hitTarget);
        }
    }
}
