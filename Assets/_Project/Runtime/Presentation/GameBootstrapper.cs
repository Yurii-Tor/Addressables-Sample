using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private DemoScenarioAssetLoader _scenarioLoader;
        private DemoScenarioCommands _scenarioCommands;

        public GameController Controller => _controller;
        internal string ScenarioStatusLabel => _scenarioCommands == null ? null : _scenarioCommands.StatusLabel;
        internal bool CanStartDemoScenarios => _scenarioCommands != null && _scenarioCommands.CanStartCommands;

        internal bool TryStartSlowDemoScenario() => _scenarioCommands != null && _scenarioCommands.TryStartSlowRound();
        internal bool TryStartFailureDemoScenario() => _scenarioCommands != null && _scenarioCommands.TryStartFailure();
        internal bool TryStartReplacementDemoScenario() => _scenarioCommands != null && _scenarioCommands.TryStartReplacement();

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

            _scenarioCommands = gameObject.AddComponent<DemoScenarioCommands>();
            _scenarioLoader = new DemoScenarioAssetLoader(
                new UnityAddressableAssetLoader(),
                GetConfiguredRoundKeys(_config),
                _scenarioCommands.DelayAsync,
                () => _controller != null && _controller.CanAcceptScenarioCommands);
            _scenarioCommands.Configure(this, _scenarioLoader);
            _controller = new GameController(
                _config,
                _scenarioLoader,
                _hud,
                new UnityTargetFactory(_targetSpawn),
                new UnityGameDiagnostics());

            _selectionInput.Selection += OnSelection;
            try
            {
                await _controller.InitializeAsync();
            }
            finally
            {
                // No startup request can consume a command. Clear any stale one-shot at the
                // session boundary so a future initialization path cannot inherit it.
                _scenarioLoader?.ClearInstruction();
            }
        }

        private void OnDestroy()
        {
            if (_selectionInput != null)
            {
                _selectionInput.Selection -= OnSelection;
            }

            _scenarioCommands?.StopAndCancel();
            _scenarioLoader?.ClearInstruction();
            _controller?.Dispose();
            _controller = null;
            _scenarioCommands = null;
            _scenarioLoader = null;
        }

        internal bool TryBeginScenarioRound(DemoScenarioInstruction instruction, out Task roundTask)
        {
            roundTask = Task.CompletedTask;
            if (_scenarioLoader == null || _controller == null || !_controller.CanAcceptScenarioCommands)
            {
                _scenarioLoader?.ClearInstruction();
                return false;
            }

            if (instruction != DemoScenarioInstruction.None && !_scenarioLoader.TryArm(instruction))
            {
                _scenarioLoader.ClearInstruction();
                return false;
            }

            if (!_controller.TryBeginScenarioRound(out roundTask))
            {
                _scenarioLoader.ClearInstruction();
                return false;
            }

            return true;
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
                throw new InvalidOperationException("GameBootstrapper already owns a controller.");
            }

            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        internal void InstallScenarioServicesForTests(
            GameController controller,
            DemoScenarioAssetLoader scenarioLoader,
            DemoScenarioCommands scenarioCommands)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _scenarioLoader = scenarioLoader ?? throw new ArgumentNullException(nameof(scenarioLoader));
            _scenarioCommands = scenarioCommands ?? throw new ArgumentNullException(nameof(scenarioCommands));
            _scenarioCommands.Configure(this, _scenarioLoader);
        }

        private static IReadOnlyList<object> GetConfiguredRoundKeys(GameConfig config)
        {
            var keys = new List<object>();
            var references = config == null ? null : config.RoundTextures;
            if (references == null)
            {
                return keys;
            }

            for (var index = 0; index < references.Count; index++)
            {
                var reference = references[index];
                if (reference != null && reference.RuntimeKeyIsValid())
                {
                    keys.Add(reference.RuntimeKey);
                }
            }

            return keys;
        }

        private void OnSelection(bool hitTarget)
        {
            _controller?.HandleSelection(hitTarget);
        }
    }
}
