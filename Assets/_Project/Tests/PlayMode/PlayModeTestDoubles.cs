using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Tests.PlayMode
{
    internal interface IReleaseTracked
    {
        int ReleaseCount { get; }
    }

    internal sealed class DefinitionConfiguration : IGameConfiguration
    {
        public DefinitionConfiguration(GameDefinition definition)
        {
            Definition = definition;
        }

        public GameDefinition Definition { get; }

        public bool TryCreateDefinition(out GameDefinition definition, out string error)
        {
            definition = Definition;
            error = null;
            return true;
        }
    }

    internal sealed class RecordingHud : IGameHud
    {
        public string Status { get; private set; }
        public int Score { get; private set; }
        public int MutationCount { get; private set; }

        public void SetStatus(string text)
        {
            Status = text;
            MutationCount++;
        }

        public void SetScore(int score)
        {
            Score = score;
            MutationCount++;
        }
    }

    internal sealed class DelayedAddressableLoad<T> : IAddressableLoad<T>, IReleaseTracked
        where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<AddressableLoadResult<T>> _completion =
            new TaskCompletionSource<AddressableLoadResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _released;

        public DelayedAddressableLoad(object key)
        {
            Key = key;
        }

        public object Key { get; }
        public Task<AddressableLoadResult<T>> Completion => _completion.Task;
        public int DisposeCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public void CompleteSuccess(T asset)
        {
            _completion.TrySetResult(AddressableLoadResult<T>.Succeeded(asset));
        }

        public void CompleteFailure(Exception exception = null)
        {
            _completion.TrySetResult(AddressableLoadResult<T>.Failed(
                exception ?? new InvalidOperationException("Synthetic delayed failure.")));
        }

        public void Dispose()
        {
            DisposeCount++;
            if (_released)
            {
                return;
            }

            _released = true;
            ReleaseCount++;
        }
    }

    internal sealed class DelayedAddressableLoader : IAddressableAssetLoader
    {
        internal sealed class Request
        {
            public Request(object key, IReleaseTracked owner)
            {
                Key = key;
                Owner = owner;
            }

            public object Key { get; }
            public IReleaseTracked Owner { get; }
        }

        public List<Request> Requests { get; } = new List<Request>();

        public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
        {
            var owner = new DelayedAddressableLoad<T>(runtimeKey);
            Requests.Add(new Request(runtimeKey, owner));
            return owner;
        }

        public DelayedAddressableLoad<T> At<T>(int index) where T : UnityEngine.Object
        {
            return (DelayedAddressableLoad<T>)Requests[index].Owner;
        }
    }

    internal sealed class RecordingTarget : ITargetView
    {
        public bool IsValid { get; private set; } = true;
        public bool InteractionEnabled { get; private set; }
        public Texture2D Texture { get; private set; }
        public int ApplyCount { get; private set; }
        public int FlashCount { get; private set; }
        public int MutationCount { get; private set; }

        public void ApplyTexture(Texture2D texture)
        {
            Texture = texture;
            ApplyCount++;
            MutationCount++;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            InteractionEnabled = enabled;
            MutationCount++;
        }

        public void FlashError()
        {
            FlashCount++;
            MutationCount++;
        }

        public void StopFeedback()
        {
            MutationCount++;
        }

        public void Clear()
        {
            Texture = null;
            InteractionEnabled = false;
            MutationCount++;
        }

        public void MarkDestroyed()
        {
            IsValid = false;
        }
    }

    internal sealed class RecordingTargetFactory : ITargetFactory
    {
        public RecordingTargetFactory(RecordingTarget target)
        {
            Target = target;
        }

        public RecordingTarget Target { get; }
        public int DestroyCount { get; private set; }

        public ITargetView Create(GameObject prefab, Texture2D initialTexture)
        {
            Target.ApplyTexture(initialTexture);
            return Target;
        }

        public void Destroy(ITargetView target)
        {
            DestroyCount++;
            Target.MarkDestroyed();
        }
    }

    internal sealed class CapturingTargetFactory : ITargetFactory
    {
        private readonly ITargetFactory _inner;

        public CapturingTargetFactory(ITargetFactory inner)
        {
            _inner = inner;
        }

        public Texture2D InitialTexture { get; private set; }
        public ITargetView Target { get; private set; }

        public ITargetView Create(GameObject prefab, Texture2D initialTexture)
        {
            InitialTexture = initialTexture;
            Target = _inner.Create(prefab, initialTexture);
            return Target;
        }

        public void Destroy(ITargetView target)
        {
            _inner.Destroy(target);
        }
    }

    internal sealed class RecordingDiagnostics : IGameDiagnostics
    {
        public int WarningCount { get; private set; }
        public int ErrorCount { get; private set; }

        public void LogWarning(string message, Exception exception = null)
        {
            WarningCount++;
        }

        public void LogError(string message, Exception exception = null)
        {
            ErrorCount++;
        }
    }
}
