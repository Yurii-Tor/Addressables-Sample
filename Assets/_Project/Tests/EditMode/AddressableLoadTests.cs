using System;
using System.Reflection;
using System.Threading.Tasks;
using AddressablesSample.Game.AddressableAssets;
using AddressablesSample.Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AddressablesSample.Game.Tests.EditMode
{
    public sealed class AddressableLoadTests
    {
        private ResourceManager _resourceManager;
        private Texture2D _texture;
        private int _baselineOwners;

        [SetUp]
        public void SetUp()
        {
            _resourceManager = new ResourceManager();
            _texture = new Texture2D(2, 2) { name = "owner-test" };
            _baselineOwners = AddressableOwnershipDiagnostics.ActiveOwnerCount;
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(AddressableOwnershipDiagnostics.ActiveOwnerCount, Is.EqualTo(_baselineOwners));
            _resourceManager.Dispose();
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test, Timeout(5000)]
        public async Task Success_IsRetainedUntilDisposeAndReleasedOnce()
        {
            var operation = new ManualOperation<Texture2D>();
            var handle = _resourceManager.StartOperation(operation, default);
            var releaseCount = 0;
            var owner = AddressableLoad<Texture2D>.TakeOwnershipForTests(
                handle,
                ownedHandle =>
                {
                    releaseCount++;
                    ownedHandle.Release();
                });

            operation.Succeed(_texture);
            var result = await owner.Completion;

            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Succeeded));
            Assert.That(result.Asset, Is.SameAs(_texture));
            Assert.That(releaseCount, Is.Zero);

            owner.Dispose();
            owner.Dispose();
            Assert.That(releaseCount, Is.EqualTo(1));
            Assert.That(operation.DestroyCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task Failure_CapturesExceptionAndReleasesOnce()
        {
            var operation = new ManualOperation<Texture2D>();
            var handle = _resourceManager.StartOperation(operation, default);
            var releaseCount = 0;
            var owner = AddressableLoad<Texture2D>.TakeOwnershipForTests(
                handle,
                ownedHandle =>
                {
                    releaseCount++;
                    ownedHandle.Release();
                });

            operation.Fail("expected failure");
            PumpDeferredCallbacks();
            var result = await owner.Completion;

            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Failed));
            StringAssert.Contains("expected failure", result.Exception.Message);
            Assert.That(releaseCount, Is.EqualTo(1));

            owner.Dispose();
            Assert.That(releaseCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task PendingDispose_CancelsAndLateCompletionCannotChangeResult()
        {
            var operation = new ManualOperation<Texture2D>();
            var handle = _resourceManager.StartOperation(operation, default);
            var transportHold = _resourceManager.Acquire(handle);
            var releaseCount = 0;
            var owner = AddressableLoad<Texture2D>.TakeOwnershipForTests(
                handle,
                ownedHandle =>
                {
                    releaseCount++;
                    ownedHandle.Release();
                });

            owner.Dispose();
            var canceled = await owner.Completion;
            operation.Succeed(_texture);
            var stillCanceled = await owner.Completion;

            Assert.That(canceled.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(stillCanceled.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(releaseCount, Is.EqualTo(1));

            transportHold.Release();
            Assert.That(operation.DestroyCount, Is.EqualTo(1));
        }

        [Test, Timeout(5000)]
        public async Task CompletedHandleDisposedBeforeDeferredCallback_ReleasesOnlyOwnerReference()
        {
            var handle = _resourceManager.CreateCompletedOperation(_texture, null);
            var deferredHold = _resourceManager.Acquire(handle);
            var releaseCount = 0;
            var owner = AddressableLoad<Texture2D>.TakeOwnershipForTests(
                handle,
                ownedHandle =>
                {
                    releaseCount++;
                    ownedHandle.Release();
                });

            owner.Dispose();
            PumpDeferredCallbacks();
            var result = await owner.Completion;

            Assert.That(result.Status, Is.EqualTo(AddressableLoadStatus.Canceled));
            Assert.That(releaseCount, Is.EqualTo(1));
            owner.Dispose();
            Assert.That(releaseCount, Is.EqualTo(1));

            deferredHold.Release();
        }

        [Test, Timeout(5000)]
        public void InvalidOwnedHandle_DisposeDoesNotInvokeReleaseOrThrow()
        {
            var operation = new ManualOperation<Texture2D>();
            var handle = _resourceManager.StartOperation(operation, default);
            var releaseCount = 0;
            var owner = AddressableLoad<Texture2D>.TakeOwnershipForTests(
                handle,
                ownedHandle =>
                {
                    releaseCount++;
                    ownedHandle.Release();
                });

            // Simulate Addressables invalidating its internal operation before the
            // scene owner receives OnDestroy. Keep the original test handle only so
            // the ResourceManager operation can be cleaned up after the assertion.
            var handleField = typeof(AddressableLoad<Texture2D>).GetField(
                "_handle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handleField, Is.Not.Null);
            handleField.SetValue(owner, default(AsyncOperationHandle<Texture2D>));

            Assert.DoesNotThrow(owner.Dispose);
            Assert.That(releaseCount, Is.Zero);

            handle.Release();
        }

        private void PumpDeferredCallbacks()
        {
            var update = typeof(ResourceManager).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null, "ResourceManager.Update test hook was not found.");
            update.Invoke(_resourceManager, new object[] { 0f });
        }

        private sealed class ManualOperation<T> : AsyncOperationBase<T>
        {
            public int DestroyCount { get; private set; }

            protected override string DebugName => "AddressablesSample manual test operation";

            protected override void Execute()
            {
                // Tests complete this operation explicitly.
            }

            protected override void Destroy()
            {
                DestroyCount++;
                base.Destroy();
            }

            public void Succeed(T value)
            {
                Complete(value, true, (string)null);
            }

            public void Fail(string message)
            {
                Complete(default, false, message);
            }
        }
    }
}
