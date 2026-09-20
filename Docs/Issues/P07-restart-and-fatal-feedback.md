# P07: Provide understandable fatal feedback and a safe session restart

Priority: P2. Dependencies: P06.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#9](https://github.com/Yurii-Tor/Addressables-Sample/issues/9).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and goal
GameStatusText.FatalError says Unable to start. See Console. There is no recovery control.
A portfolio visitor should understand the failure and restart without browser developer tools.

## Read only these entry points first
- Assets/_Project/Runtime/Core/GameContracts.cs: status text.
- Assets/_Project/Runtime/Presentation/GameBootstrapper.cs: Start, OnDestroy, OnSelection.
- Assets/_Project/Runtime/Core/GameController.cs: Dispose, InitializeAsync and terminal state guard.
- Assets/_Project/Runtime/Presentation/UnityTargetFactory.cs: deferred Object.Destroy.
- P05 scenario lifetime, P06 panel bindings, P04 controller-identity reset.
- Assets/_Project/Tests/PlayMode/GameControllerPlayModeTests.cs: teardown fixtures.

## Locked small design
Restart creates a fresh GameController through GameBootstrapper. Never resurrect a Disposed
or FatalError controller or reset generation in-place. Retain one composition root and scene.
Expose one Restart/Retry control; do not add automatic retry/backoff to every asset request.

## Implementation
1. Replace Console-only user copy with a brief startup/load explanation and Retry guidance;
   keep technical details in diagnostics/logs without dumping stack traces into normal UI.
2. Factor creation/disposal into bounded bootstrapper session methods used by Start/restart/destroy.
3. On restart, disable/reject duplicate restart, cancel scenario orchestration, detach old input,
   dispose controller/owners, await the deferred target destruction boundary if needed, then create one session.
4. Use bootstrapper lifetime/session identity guard so an old startup or restart continuation
   cannot rebind UI/input or construct a session after OnDestroy.
5. Score resets to zero, scenario instructions clear, polling observer resets to new controller.
6. Retry available after fatal; allow intentional restart during a pending round to demonstrate
   cleanup. Guard startup reentry and rapid repeated clicks explicitly.
7. Configure any new references through setup and assert them in ProjectValidation.

## Acceptance
- [ ] Fatal UI offers a meaningful action in a browser with no editor console.
- [ ] Ready, pending-round and fatal restart each produce one fresh session.
- [ ] Repeated clicks cannot produce duplicate targets/input subscriptions.
- [ ] Old owners reach zero before fresh startup; settled new session has its expected owner count.
- [ ] Late old-session work cannot affect new score, texture, status or trace.
- [ ] Destroy during restart/startup produces no resurrected controller.

## Verification and stop
Deterministic lifecycle tests plus real scene repeated restart and pending restart PlayMode
tests; verify deferred destruction and absence of duplicate selection. Browser smoke fatal ->
retry and delayed load -> restart. Simulated round failure is Ready/fallback, not Fatal:
use a test-only local startup-failure seam or isolated content fixture, restore the startup
asset before Retry, and verify a fresh successful session. Do not add a permanent fourth
public scenario or damage hosted content. Shared runtime matrix applies.
No scene reload framework, save state, networking retry policy or new gameplay.
