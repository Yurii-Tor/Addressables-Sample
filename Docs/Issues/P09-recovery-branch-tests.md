# P09: Cover synchronous loader throws, faulted completion and startup reentry

Priority: P2. Dependencies: none.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#6](https://github.com/Yurii-Tor/Addressables-Sample/issues/6).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
Controller tests already cover many failure results. FakeAddressableAssetLoader.ThrowForKey
exists but is not exercised by GameControllerTests at the audit baseline. AwaitLoad catches
faulted tasks, another distinct path from an AddressableLoadResult.Failed result.

## Read only these entry points first
- Assets/_Project/Tests/EditMode/TestDoubles.cs: FakeAddressableAssetLoader, ManualAddressableLoad.
- Assets/_Project/Tests/EditMode/GameControllerTests.cs: fixture and failure/teardown cases.
- Assets/_Project/Runtime/Core/GameController.cs: InitializeAsync, StartStartupLoad, BeginRound, AwaitLoad.
- Assets/_Project/Tests/EditMode/SourceAuditTests.cs (limitations only).

## Implementation
1. Reuse ThrowForKey to cover synchronous fallback start throw, prefab start throw after
   fallback retention, and round start throw after a displayed round exists.
2. Add the smallest test-double method for faulting Completion via TaskCompletionSource;
   distinguish fault from a successful task containing Failed.
3. Verify startup faults become Fatal and clean up retained owners; round faults apply
   fallback before previous displayed owner release and return Ready.
4. Call InitializeAsync again while fallback/prefab startup is pending; assert no duplicate
   loads or score/UI reinitialization. Do not redesign its current return-task semantics without evidence.
5. Assert stable public behavior and release events/counts, not private method execution.
6. Record that SourceAuditTests is a narrow pattern check. Do not add a Roslyn dependency
   or claim it catches all instance task.Wait()/Result variations.

## Acceptance
- [ ] Synchronous start failure covered at each asset stage.
- [ ] A genuinely faulted completion covered for startup and round recovery.
- [ ] Repeated startup initialization does not acquire duplicate owners.
- [ ] Tests settle deterministically without sleeps or unobserved task exceptions.
- [ ] Existing async behavior remains; production changes only for defects actually revealed.

## Verification and stop
Run targeted GameControllerTests then full EditMode and shared applicable runtime gates.
Test names should state behavior. No quota-driven test inflation, loader rewrite or
source-analyzer project. If a test exposes a wider defect, document scope before expansion.
