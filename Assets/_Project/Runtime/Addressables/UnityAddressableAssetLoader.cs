using System;
using System.Threading;
using System.Threading.Tasks;
using AddressablesSample.Game.Core;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace AddressablesSample.Game.AddressableAssets
{
    public sealed class UnityAddressableAssetLoader : IAddressableAssetLoader
    {
        public IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object
        {
            if (runtimeKey == null)
            {
                throw new ArgumentNullException(nameof(runtimeKey));
            }

            return AddressableLoad<T>.Create(runtimeKey);
        }
    }

    internal static class AddressableOwnershipDiagnostics
    {
        private static int _activeOwnerCount;

        public static int ActiveOwnerCount => Volatile.Read(ref _activeOwnerCount);

        public static void OwnerAcquired()
        {
            Interlocked.Increment(ref _activeOwnerCount);
        }

        public static void OwnerReleased()
        {
            Interlocked.Decrement(ref _activeOwnerCount);
        }
    }

    internal sealed class AddressableLoad<T> : IAddressableLoad<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<AddressableLoadResult<T>> _completionSource;
        private readonly Action<AsyncOperationHandle<T>> _release;
        private AsyncOperationHandle<T> _handle;
        private bool _subscribed;
        private bool _released;

        private AddressableLoad(
            AsyncOperationHandle<T> handle,
            Action<AsyncOperationHandle<T>> release)
        {
            _handle = handle;
            _release = release ?? throw new ArgumentNullException(nameof(release));
            _completionSource = new TaskCompletionSource<AddressableLoadResult<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _handle.Completed += OnCompleted;
            _subscribed = true;
            AddressableOwnershipDiagnostics.OwnerAcquired();
        }

        public Task<AddressableLoadResult<T>> Completion => _completionSource.Task;

        public static AddressableLoad<T> Create(object runtimeKey)
        {
            var handle = UnityAddressables.LoadAssetAsync<T>(runtimeKey);
            try
            {
                return new AddressableLoad<T>(handle, ReleaseAddressableHandle);
            }
            catch
            {
                if (handle.IsValid())
                {
                    UnityAddressables.Release(handle);
                }

                throw;
            }
        }

        internal static AddressableLoad<T> TakeOwnershipForTests(
            AsyncOperationHandle<T> handle,
            Action<AsyncOperationHandle<T>> release)
        {
            return new AddressableLoad<T>(handle, release);
        }

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            ReleaseOnce();
            _completionSource.TrySetResult(AddressableLoadResult<T>.Canceled());
        }

        private void OnCompleted(AsyncOperationHandle<T> completed)
        {
            if (_released)
            {
                return;
            }

            Unsubscribe(completed);

            if (completed.Status == AsyncOperationStatus.Succeeded && completed.Result != null)
            {
                _completionSource.TrySetResult(AddressableLoadResult<T>.Succeeded(completed.Result));
                return;
            }

            var exception = completed.OperationException ??
                new InvalidOperationException("The Addressables operation failed without an exception.");
            ReleaseOnce();
            _completionSource.TrySetResult(AddressableLoadResult<T>.Failed(exception));
        }

        private void Unsubscribe(AsyncOperationHandle<T> handle)
        {
            if (!_subscribed)
            {
                return;
            }

            if (handle.IsValid())
            {
                handle.Completed -= OnCompleted;
            }

            _subscribed = false;
        }

        private void ReleaseOnce()
        {
            if (_released)
            {
                return;
            }

            if (_subscribed)
            {
                Unsubscribe(_handle);
            }

            var handle = _handle;
            _handle = default;
            _released = true;

            try
            {
                // Addressables may invalidate all outstanding operations before a scene
                // bootstrapper receives OnDestroy while exiting Play Mode. In that case the
                // subsystem already owns the cleanup and releasing the stale handle would throw.
                if (handle.IsValid())
                {
                    _release(handle);
                }
            }
            finally
            {
                AddressableOwnershipDiagnostics.OwnerReleased();
            }
        }

        private static void ReleaseAddressableHandle(AsyncOperationHandle<T> handle)
        {
            UnityAddressables.Release(handle);
        }
    }
}
