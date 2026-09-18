using System.Collections.Generic;
using System.Text;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AddressablesSample.Game.Presentation
{
    /// <summary>
    /// Renders the runtime facts that the architecture is built around but that are otherwise
    /// invisible: the controller state machine, the round generation counter, and the number of
    /// live Addressables handle owners. It only observes; it never mutates game state, and it
    /// reads the controller through polling so the controller keeps no presentation coupling.
    /// </summary>
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        private const int MaxTraceEntries = 6;

        [SerializeField] private GameBootstrapper _bootstrapper;
        [SerializeField] private bool _visible = true;

        private readonly List<string> _trace = new List<string>(MaxTraceEntries);
        private readonly StringBuilder _builder = new StringBuilder(256);
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private Texture2D _panelTexture;
        private int _lastObservedGeneration = -1;
        private GameState _lastObservedState = GameState.Uninitialized;

        internal void Configure(GameBootstrapper bootstrapper, bool visible)
        {
            _bootstrapper = bootstrapper;
            _visible = visible;
        }

        private void Update()
        {
            ReadToggle();
            SampleRoundTrace();
        }

        private void OnDestroy()
        {
            if (_panelTexture != null)
            {
                Destroy(_panelTexture);
                _panelTexture = null;
            }
        }

        private void ReadToggle()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.backquoteKey.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        /// <summary>
        /// Detects a settled round by watching the generation counter and the state machine, so the
        /// trace stays accurate without the controller having to publish presentation events.
        /// </summary>
        private void SampleRoundTrace()
        {
            var controller = _bootstrapper == null ? null : _bootstrapper.Controller;
            if (controller == null)
            {
                return;
            }

            var generation = controller.RoundGeneration;
            var state = controller.State;

            var settled = state == GameState.Ready &&
                          _lastObservedState != GameState.Ready &&
                          generation == _lastObservedGeneration;
            if (settled)
            {
                AppendTrace($"round #{generation} settled in {controller.LastRoundLoadMilliseconds:F0} ms");
            }
            else if (generation != _lastObservedGeneration && state == GameState.LoadingRound)
            {
                AppendTrace($"round #{generation} requested");
            }

            _lastObservedGeneration = generation;
            _lastObservedState = state;
        }

        private void AppendTrace(string entry)
        {
            _trace.Add(entry);
            if (_trace.Count > MaxTraceEntries)
            {
                _trace.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureStyles();

            var controller = _bootstrapper == null ? null : _bootstrapper.Controller;
            _builder.Clear();
            _builder.AppendLine("ADDRESSABLES DIAGNOSTICS   [~] or [F1] to hide");
            _builder.AppendLine();

            if (controller == null)
            {
                _builder.AppendLine("controller      not constructed yet");
            }
            else
            {
                _builder.AppendLine($"state           {controller.State}");
                _builder.AppendLine($"score           {controller.Score}");
                _builder.AppendLine($"round gen       #{controller.RoundGeneration}");
                _builder.AppendLine($"last load       {controller.LastRoundLoadMilliseconds:F0} ms");
            }

            _builder.AppendLine($"live handles    {AddressableOwnershipDiagnostics.ActiveOwnerCount}");

            if (_trace.Count > 0)
            {
                _builder.AppendLine();
                for (var index = 0; index < _trace.Count; index++)
                {
                    _builder.AppendLine(_trace[index]);
                }
            }

            var content = new GUIContent(_builder.ToString().TrimEnd());
            var size = _labelStyle.CalcSize(content);
            var rect = new Rect(16f, Screen.height - size.y - 32f, size.x + 24f, size.y + 20f);
            GUI.Box(rect, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, size.x, size.y), content, _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_panelTexture == null)
            {
                _panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _panelTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.66f));
                _panelTexture.Apply();
                _panelStyle = null;
            }

            _panelStyle ??= new GUIStyle(GUIStyle.none)
            {
                normal = { background = _panelTexture }
            };

            // The built-in skin font is used deliberately: an OS font lookup is not portable to
            // WebGL or mobile players, which is exactly where this overlay has to keep working.
            _labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                richText = false,
                wordWrap = false,
                normal = { textColor = new Color(0.72f, 0.94f, 0.78f, 1f) }
            };
        }
    }
}
