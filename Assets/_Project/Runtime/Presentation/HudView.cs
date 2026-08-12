using AddressablesSample.Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AddressablesSample.Game.Presentation
{
    public sealed class HudView : MonoBehaviour, IGameHud
    {
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _scoreText;

        public string Status => _statusText == null ? string.Empty : _statusText.text;
        public int DisplayedScore { get; private set; }

        public void SetStatus(string text)
        {
            if (_statusText == null)
            {
                throw new MissingReferenceException("HudView requires a status Text reference.");
            }

            _statusText.text = text ?? string.Empty;
        }

        public void SetScore(int score)
        {
            if (_scoreText == null)
            {
                throw new MissingReferenceException("HudView requires a score Text reference.");
            }

            DisplayedScore = score;
            _scoreText.text = $"Score: {score}";
        }

        internal void Configure(Text statusText, Text scoreText)
        {
            _statusText = statusText;
            _scoreText = scoreText;
        }
    }
}
