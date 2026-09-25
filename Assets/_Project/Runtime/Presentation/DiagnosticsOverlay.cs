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
    /// invisible: the controller state machine, terminal round history, and the number of
    /// active Addressables load owners. It only observes; it never mutates game state, and it
    /// reads the controller through polling so the controller keeps no presentation coupling.
    /// </summary>
    public sealed class DiagnosticsOverlay : MonoBehaviour
    {
        private const int MaxTraceEntries = 6;

        [SerializeField] private GameBootstrapper _bootstrapper;
        [SerializeField] private bool _visible = true;

        private readonly List<string> _trace = new List<string>(MaxTraceEntries);
        private readonly List<TerminalRoundRecord> _unseenRounds = new List<TerminalRoundRecord>(GameController.RoundHistoryCapacity);
        private readonly StringBuilder _builder = new StringBuilder(256);
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private Texture2D _panelTexture;
        private GameController _observedController;
        private long _lastReadSequence;
        private long _missedRounds;

        internal IReadOnlyList<string> TraceEntries => _trace;
        internal long MissedRounds => _missedRounds;

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
        /// Reads every retained terminal record since this overlay's last poll.
        /// </summary>
        private void SampleRoundTrace()
        {
            var controller = _bootstrapper == null ? null : _bootstrapper.Controller;
            SampleRoundTrace(controller);
        }

        internal void SampleRoundTrace(GameController controller)
        {
            if (!ReferenceEquals(controller, _observedController))
            {
                _observedController = controller;
                _lastReadSequence = 0;
                _missedRounds = 0;
                _trace.Clear();
            }

            if (controller == null)
            {
                return;
            }

            _unseenRounds.Clear();
            _missedRounds += controller.ReadTerminalRounds(_lastReadSequence, _unseenRounds);
            foreach (var round in _unseenRounds)
            {
                AppendTrace($"round #{round.RequestedGeneration} {round.Outcome} in {round.ElapsedMilliseconds:F0} ms");
                _lastReadSequence = round.Sequence;
            }
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
                _builder.AppendLine($"last round      {controller.LastRoundLoadMilliseconds:F0} ms");
            }

            _builder.AppendLine($"Active load owners  {AddressableOwnershipDiagnostics.ActiveOwnerCount}");

            if (_missedRounds > 0)
            {
                _builder.AppendLine($"history gap     {_missedRounds} older round outcome(s) lost");
            }

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
