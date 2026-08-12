using System;
using System.Threading.Tasks;
using UnityEngine;

namespace AddressablesSample.Game.Core
{
    public enum AddressableLoadStatus
    {
        Succeeded,
        Failed,
        Canceled
    }

    public readonly struct AddressableLoadResult<T> where T : UnityEngine.Object
    {
        private AddressableLoadResult(AddressableLoadStatus status, T asset, Exception exception)
        {
            Status = status;
            Asset = asset;
            Exception = exception;
        }

        public AddressableLoadStatus Status { get; }
        public T Asset { get; }
        public Exception Exception { get; }

        public static AddressableLoadResult<T> Succeeded(T asset)
        {
            return new AddressableLoadResult<T>(AddressableLoadStatus.Succeeded, asset, null);
        }

        public static AddressableLoadResult<T> Failed(Exception exception)
        {
            return new AddressableLoadResult<T>(AddressableLoadStatus.Failed, null, exception);
        }

        public static AddressableLoadResult<T> Canceled()
        {
            return new AddressableLoadResult<T>(AddressableLoadStatus.Canceled, null, null);
        }
    }

    public interface IAddressableLoad<T> : IDisposable where T : UnityEngine.Object
    {
        Task<AddressableLoadResult<T>> Completion { get; }
    }

    public interface IAddressableAssetLoader
    {
        IAddressableLoad<T> StartLoad<T>(object runtimeKey) where T : UnityEngine.Object;
    }
}
