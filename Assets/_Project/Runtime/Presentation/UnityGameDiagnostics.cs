using System;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Presentation
{
    public sealed class UnityGameDiagnostics : IGameDiagnostics
    {
        public void LogWarning(string message, Exception exception = null)
        {
            Debug.LogWarning(Format(message, exception));
        }

        public void LogError(string message, Exception exception = null)
        {
            Debug.LogError(Format(message, exception));
        }

        private static string Format(string message, Exception exception)
        {
            return exception == null ? message : $"{message}\n{exception}";
        }
    }
}
