# P10: Handle externally destroyed targets when a current round completes

Priority: P3. Dependencies: P04.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#11](https://github.com/Yurii-Tor/Addressables-Sample/issues/11).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
CompleteRoundAsync returns when IsCurrentRound is false; that guard combines stale identity
with target validity. If the current target is externally destroyed during the load, state
can remain LoadingRound and the pending owner stays retained until later Dispose.
This is an audit finding for a nonstandard lifetime path, not a normal-play reproduced leak.

## Read only these entry points first
- Assets/_Project/Runtime/Core/GameController.cs: CompleteRoundAsync, IsCurrentRound, HasValidTarget, EnterFatal.
- Assets/_Project/Runtime/Presentation/UnityTargetFactory.cs; TargetView.cs: Unity destroyed-object semantics.
- Assets/_Project/Tests/EditMode/TestDoubles.cs: FakeTargetView.IsValid.
- Assets/_Project/Tests/EditMode/GameControllerTests.cs: stale and disposal cases.

## Implementation
1. Add regression: current pending request + target becomes invalid + completion -> bounded
   fatal cleanup rather than permanently LoadingRound.
2. Separate authoritative state/generation/owner identity check from target liveness.
   First reject stale/disposed work; only a still-current completion may diagnose lost target.
3. For current work with invalid target, enter existing fatal cleanup path, release pending,
   displayed and startup owners once, and show controlled failure.
4. Preserve stale A behavior: A must not dispose B or enter fatal for the current session.
5. Align terminal telemetry with P04 (Fatal once for the current started round).
6. Do not add an every-frame global object scan or a generic lifetime watchdog.

## Acceptance
- [ ] Current completion with lost target performs cleanup and leaves LoadingRound.
- [ ] Stale completion after replacement/teardown cannot trigger new cleanup or warnings.
- [ ] Success/failure completion variants and double Dispose release once.
- [ ] No mutation of a destroyed Unity renderer/collider.

## Verification and stop
Deterministic invalid-target tests plus one real PlayMode destroy-while-loading case.
Shared runtime matrix; preserve existing stale-completion assertions.
This optional robustness task does not add automatic target respawn or session retry.
