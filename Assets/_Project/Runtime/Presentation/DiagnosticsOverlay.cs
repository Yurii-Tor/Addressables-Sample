using System.Collections.Generic;
using System.Text;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private Text _contentText;

        private readonly List<string> _trace = new List<string>(MaxTraceEntries);
        private readonly List<TerminalRoundRecord> _unseenRounds = new List<TerminalRoundRecord>(GameController.RoundHistoryCapacity);
        private readonly StringBuilder _builder = new StringBuilder(256);
        private GameController _observedController;
        private long _lastReadSequence;
        private long _missedRounds;
        private string _lastRenderedText;

        internal IReadOnlyList<string> TraceEntries => _trace;
        internal long MissedRounds => _missedRounds;

        internal void Configure(GameBootstrapper bootstrapper, Text contentText)
        {
            _bootstrapper = bootstrapper;
            _contentText = contentText;
        }

        private void Update()
        {
            SampleRoundTrace();
            RefreshText();
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

        private void RefreshText()
        {
            if (_contentText == null)
            {
                return;
            }

            var controller = _bootstrapper == null ? null : _bootstrapper.Controller;
            _builder.Clear();
            _builder.AppendLine("DIAGNOSTICS");

            if (controller == null)
            {
                _builder.AppendLine("state  Starting");
                _builder.AppendLine("score  0");
            }
            else
            {
                _builder.AppendLine($"state  {controller.State}");
                _builder.AppendLine($"score  {controller.Score}");
                _builder.AppendLine($"round gen  #{controller.RoundGeneration}");
                _builder.AppendLine($"last round  {controller.LastRoundLoadMilliseconds:F0} ms");
            }

            _builder.AppendLine($"Active load owners  {AddressableOwnershipDiagnostics.ActiveOwnerCount}");

            var scenarioStatus = _bootstrapper == null ? null : _bootstrapper.ScenarioStatusLabel;
            if (!string.IsNullOrWhiteSpace(scenarioStatus))
            {
                _builder.Append("scenario  ").AppendLine(scenarioStatus);
            }
            else
            {
                _builder.AppendLine("scenario  no simulation has run");
            }

            if (_missedRounds > 0)
            {
                _builder.AppendLine($"history gap  {_missedRounds} older round outcome(s) lost");
            }

            if (_trace.Count > 0)
            {
                _builder.AppendLine();
                _builder.AppendLine("Recent round outcomes");
                for (var index = 0; index < _trace.Count; index++)
                {
                    _builder.AppendLine(_trace[index]);
                }
            }

            var renderedText = _builder.ToString().TrimEnd();
            if (!string.Equals(_lastRenderedText, renderedText, System.StringComparison.Ordinal))
            {
                _contentText.text = renderedText;
                _lastRenderedText = renderedText;
            }
        }
    }
}
