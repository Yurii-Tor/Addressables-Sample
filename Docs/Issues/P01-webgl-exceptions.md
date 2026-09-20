# P01: Enable WebGL exception recovery and verify player failure paths

Priority: P1. Dependencies: none.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#2](https://github.com/Yurii-Tor/Addressables-Sample/issues/2).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
ContinuousIntegration.ConfigureWebGlPlayerSettings sets WebGLExceptionSupport.None.
Runtime recovery in GameController and AddressableLoad<T> uses try/catch.
This is a verified configuration mismatch; a browser crash was not reproduced in the audit.
Unity 6000.3 documents None as stopping execution when an exception is thrown:
https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/webgl/develop/class-player-settings-web-gl

## Read only these entry points first
- Assets/_Project/Editor/ContinuousIntegration.cs: ConfigureWebGlPlayerSettings, BuildWebGl.
- Assets/_Project/Runtime/Core/GameController.cs: InitializeAsync, AwaitLoad, ApplyFallbackRound, EnterFatal.
- Assets/_Project/Tests/PlayMode/GameControllerPlayModeTests.cs: RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable.
- ProjectSettings/ProjectSettings.asset: webGLExceptionSupport (inspect; use Unity API to author).
- Docs/Operations.md: sections 8 and 9a. Requirements REQ-A05, REQ-ASYNC01.

## Implementation
1. Use ExplicitlyThrownExceptionsOnly as the minimum initial policy. If a demonstrated path requires implicit null/bounds checks, choose FullWithoutStacktrace and document the evidence. No package upgrades.
2. Apply the policy via the existing Editor build configuration. Keep serialized defaults consistent through Unity, not manual settings churn. Preserve unrelated player settings.
3. Add a focused Editor assertion that the production WebGL build configuration cannot silently select None. Avoid testing only a new constant disconnected from the build method.
4. Exercise actual thrown-exception recovery in a locally served WebGL player. A failed result object alone does not test exception support.
5. Exercise missing round key -> fallback -> another playable round, and invalid startup -> fatal UI. Use isolated harness/configuration outside the committed scene, or the completed scenario system later; never damage published bundles.
6. Record build size and observed behavior if available, without promising a specific cost/size improvement.

## Acceptance
- [ ] WebGL build config enables required exception handling.
- [ ] A controlled thrown exception reaches the intended recovery code in the player.
- [ ] A failed round retains a visible fallback and permits the next normal interaction.
- [ ] Startup failure cleans up owners and displays a controlled error.
- [ ] No new external dependencies or public deployment.

## Verification and stop
Run relevant Editor configuration tests plus the shared runtime matrix. Build to local Builds/WebGL explicitly and serve over HTTP. Record browser/version, tested build/commit, input, expected/actual result and console evidence. Editor tests do not prove WebGL behavior.
If Web Build Support, licensing or browser execution is unavailable, leave that gate incomplete.
Stop after the focused local patch and evidence; no UI redesign or retry feature in this issue.
