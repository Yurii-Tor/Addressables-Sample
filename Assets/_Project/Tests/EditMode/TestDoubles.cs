using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using UnityEngine;

namespace AddressablesSample.Game.Tests.EditMode
{
    internal sealed class FakeConfiguration : IGameConfiguration
    {
        public GameDefinition Definition { get; set; }
        public string Error { get; set; }

        public bool TryCreateDefinition(out GameDefinition definition, out string error)
        {
            definition = Definition;
            error = Error;
            return Definition != null;
        }
    }

    internal sealed class ManualAddressableLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<AddressableLoadResult<T>> _completion =
            new TaskCompletionSource<AddressableLoadResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IList<string> _events;
        private bool _released;

        public ManualAddressableLoad(object key, IList<string> events)
        {
            Key = key;
            _events = events;
        }

        public object Key { get; }
        public Task<AddressableLoadResult<T>> Completion => _completion.Task;
        public int DisposeCallCount { get; private set; }
        public int UnderlyingReleaseCount { get; private set; }

        public void CompleteSuccess(T asset)
        {
            _completion.TrySetResult(AddressableLoadResult<T>.Succeeded(asset));
        }

        public void CompleteFailure(Exception exception = null)
        {
            ReleaseOnce();
            _completion.TrySetResult(AddressableLoadResult<T>.Failed(
                exception ?? new InvalidOperationException("Synthetic load failure.")));
        }

        public void Dispose()
        {
            DisposeCallCount++;
            ReleaseOnce();
            _completion.TrySetResult(AddressableLoadResult<T>.Canceled());
        }

        private void ReleaseOnce()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            UnderlyingReleaseCount++;
            _events?.Add("release:" + Key);
        }
    }

    internal sealed class FakeAddressableAssetLoader : IAddressableAssetLoader
    {
        internal sealed class Request
        {
            public Request(object key, Type assetType, object owner)
            {
                Key = key;
                AssetType = assetType;
                Owner = owner;
            }

            public object Key { get; }
            public Type AssetType { get; }
            public object Owner { get; }
        }

        private readonly IList<string> _events;

        public FakeAddressableAssetLoader(IList<string> events)
        {
            _events = events;
        }

        public List<Request> Requests { get; } = new List<Request>();
        public object ThrowForKey { get; set; }

        public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
        {
            if (Equals(runtimeKey, ThrowForKey))
            {
                throw new InvalidOperationException("Synthetic StartLoad failure for " + runtimeKey + ".");
            }

            var owner = new ManualAddressableLoad<T>(runtimeKey, _events);
            Requests.Add(new Request(runtimeKey, typeof(T), owner));
            _events?.Add("request:" + runtimeKey);
            return owner;
        }

        public ManualAddressableLoad<T> At<T>(int requestIndex) where T : UnityEngine.Object
        {
            return (ManualAddressableLoad<T>)Requests[requestIndex].Owner;
        }

        public ManualAddressableLoad<T> Latest<T>() where T : UnityEngine.Object
        {
            for (var index = Requests.Count - 1; index >= 0; index--)
            {
                if (Requests[index].Owner is ManualAddressableLoad<T> typed)
                {
                    return typed;
                }
            }

            throw new InvalidOperationException("No request exists for " + typeof(T).Name + ".");
        }
    }

    internal sealed class FakeHud : IGameHud
    {
        private readonly IList<string> _events;

        public FakeHud(IList<string> events)
        {
            _events = events;
        }

        public string Status { get; private set; }
        public int Score { get; private set; }

        public void SetStatus(string text)
        {
            Status = text;
            _events?.Add("status:" + text);
        }

        public void SetScore(int score)
        {
            Score = score;
            _events?.Add("score:" + score);
        }
    }

    internal sealed class FakeTargetView : ITargetView
    {
        private readonly IList<string> _events;

        public FakeTargetView(IList<string> events)
        {
            _events = events;
        }

        public bool IsValid { get; set; } = true;
        public bool InteractionEnabled { get; private set; }
        public int FlashCount { get; private set; }
        public Texture2D Texture { get; private set; }
        public Texture2D ThrowWhenApplying { get; set; }
        public bool ThrowWhenEnablingInteraction { get; set; }

        public void ApplyTexture(Texture2D texture)
        {
            _events?.Add("apply:" + (texture == null ? "null" : texture.name));
            if (ReferenceEquals(texture, ThrowWhenApplying))
            {
                throw new InvalidOperationException("Synthetic texture application failure.");
            }

            Texture = texture;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (enabled && ThrowWhenEnablingInteraction)
            {
                throw new InvalidOperationException("Synthetic Ready transition failure.");
            }

            InteractionEnabled = enabled;
            _events?.Add("interaction:" + enabled);
        }

        public void FlashError()
        {
            FlashCount++;
            _events?.Add("flash");
        }

        public void StopFeedback()
        {
            _events?.Add("stop-feedback");
        }

        public void Clear()
        {
            Texture = null;
            InteractionEnabled = false;
            _events?.Add("clear-target");
        }
    }

    internal sealed class FakeTargetFactory : ITargetFactory
    {
        private readonly IList<string> _events;

        public FakeTargetFactory(FakeTargetView target, IList<string> events)
        {
            Target = target;
            _events = events;
        }

        public FakeTargetView Target { get; }
        public bool ThrowOnCreate { get; set; }
        public int CreateCount { get; private set; }
        public int DestroyCount { get; private set; }

        public ITargetView Create(GameObject prefab, Texture2D initialTexture)
        {
            CreateCount++;
            _events?.Add("create-target");
            if (ThrowOnCreate)
            {
                throw new InvalidOperationException("Synthetic target creation failure.");
            }

            Target.ApplyTexture(initialTexture);
            return Target;
        }

        public void Destroy(ITargetView target)
        {
            DestroyCount++;
            Target.IsValid = false;
            _events?.Add("destroy-target");
        }
    }

    internal sealed class FakeDiagnostics : IGameDiagnostics
    {
        private readonly IList<string> _events;

        public FakeDiagnostics(IList<string> events)
        {
            _events = events;
        }

        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public void LogWarning(string message, Exception exception = null)
        {
            Warnings.Add(message);
            _events?.Add("warning:" + message);
        }

        public void LogError(string message, Exception exception = null)
        {
            Errors.Add(message);
            _events?.Add("error:" + message);
        }
    }
}
