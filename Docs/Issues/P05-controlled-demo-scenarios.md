# P05: Add controlled slow, failed and superseded round scenarios

Priority: P2. Dependencies: P04, P09.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#7](https://github.com/Yurii-Tor/Addressables-Sample/issues/7).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Goal and baseline
Make async behavior observable in one scene: slow replacement, retained fallback on failure,
and pending A superseded by B. Normal HandleSelection stays Ready-only and score-on-hit.
BeginRound already supports replacement, but ordinary visitors cannot trigger it.

## Read only these entry points first
- Assets/_Project/Runtime/Core/AddressableLoadContracts.cs and GameController.cs: BeginRound.
- Assets/_Project/Runtime/Addressables/UnityAddressableAssetLoader.cs: AddressableLoad<T>.Dispose.
- Assets/_Project/Runtime/Presentation/GameBootstrapper.cs: composition/lifetime.
- Assets/_Project/Tests/PlayMode/PlayModeTestDoubles.cs; GameControllerPlayModeTests.cs: DelayedAAfterB test.
- P04 telemetry contract and P09 failure tests. Requirements REQ-ASYNC02 and REQ-LIFE01.

## Locked small design
Bootstrapper composes one optional loader decorator plus a scenario command component.
Proposed new files: Runtime/Addressables/DemoScenarioAssetLoader.cs and
Runtime/Presentation/DemoScenarioCommands.cs; names may change with a recorded reason.
No scenario framework, DI container or alternate game controller.
P06 adds visible buttons; this issue supplies testable commands and temporary development access.

## Implementation
1. Commands: start a slow round (minimum visible delay about 1.5 s), fail the next requested round, run A-then-B replacement. Commands do not increment score.
2. Consume an armed instruction once, only for configured round keys. Fallback is also Texture2D: filtering by asset type alone is wrong. Startup assets always use the normal loader.
   Validate command eligibility before arming; consume/reset at dispatch and clear on rejected dispatch, synchronous StartLoad throw, startup failure, disposal and restart. A rejected command must not affect a later ordinary round.
3. Decorator owns exactly one inner IAddressableLoad<T>; controller owns the decorator owner. Only the inner production owner accesses/releases raw handles. Do not count wrappers as extra Addressables owners.
4. Delay successful delivery while retaining its inner owner. Dispose before completion or during delay settles outer Completion as Canceled, disposes inner once and prevents later asset publication.
5. Synthetic failure may return an already-failed disposable operation without acquiring a handle. Mark it explicitly as Simulated failure.
6. Expose a narrow command route to the existing BeginRound; no reflection, fake click or score mutation. Scenario commands allowed only in Ready/LoadingRound; reject startup, fatal and disposed.
7. For replacement, hold A delivery, start B, then let the canceled schedule settle. Show A superseded/canceled and B displayed. Production Dispose already cancels A; NEVER manufacture a late usable texture from a released owner.
8. Keep hostile late-success coverage in existing test doubles. Any optional simulated late signal contains metadata only and is labeled simulated, not real transport cancellation.
9. Bootstrapper owns/cancels scenario orchestration. Reject a second scenario sequence while one runs; normal play can resume once settled. Use an injectable/manual timing seam in tests and Unity-main-thread continuations in production.

## Acceptance
- [ ] Slow round keeps the previous texture and responsive UI.
- [ ] Injected failure uses fallback, then the next normal round succeeds.
- [ ] Replacement shows A superseded/B selected; score unchanged.
- [ ] Startup never consumes a scenario instruction.
- [ ] Normal next round is unaffected after a one-shot scenario.
- [ ] Disposal and repeated commands cannot start a late round or release twice.
- [ ] Simulation labels do not claim measured network behavior.

## Verification and stop
Test cancellation before inner completion and during delivery delay; one-shot behavior;
A/B sequencing; unscaled timing/pause policy; teardown; normal hit/miss regression;
rejected command then normal round; synchronous StartLoad failure without leaked instruction.
No real sleeps in unit tests. Use existing adversarial A-after-B tests unchanged.
Run shared runtime matrix; expose commands without a permanent debug-only dependency.
No UI styling, public remote failure probe or new remote content pipeline.
