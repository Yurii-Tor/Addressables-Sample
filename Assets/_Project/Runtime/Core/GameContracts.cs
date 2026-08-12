using System;
using UnityEngine;

namespace AddressablesSample.Game.Core
{
    public interface IGameConfiguration
    {
        bool TryCreateDefinition(out GameDefinition definition, out string error);
    }

    public interface IGameHud
    {
        void SetStatus(string text);
        void SetScore(int score);
    }

    public interface ITargetView
    {
        bool IsValid { get; }
        void ApplyTexture(Texture2D texture);
        void SetInteractionEnabled(bool enabled);
        void FlashError();
        void StopFeedback();
        void Clear();
    }

    public interface ITargetFactory
    {
        ITargetView Create(GameObject prefab, Texture2D initialTexture);
        void Destroy(ITargetView target);
    }

    public interface IGameDiagnostics
    {
        void LogWarning(string message, Exception exception = null);
        void LogError(string message, Exception exception = null);
    }

    public static class GameStatusText
    {
        public const string LoadingGame = "Loading game...";
        public const string LoadingImage = "Loading image...";
        public const string Ready = "Tap the object!";
        public const string ReadyWithFallback = "Image failed - using fallback. Tap the object!";
        public const string FatalError = "Unable to start. See Console.";
    }
}
