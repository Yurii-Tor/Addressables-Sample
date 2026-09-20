# P02: Restore the local Addressables workflow after WebGL build failures

Priority: P1. Dependencies: none.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#3](https://github.com/Yurii-Tor/Addressables-Sample/issues/3).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
ContinuousIntegration.BuildWebGl calls BuildLocalAndUseExisting before BuildPipeline.BuildPlayer.
UseAssetDatabase is invoked only at the end of the success path. A build exception leaves
the checkout configured for Existing Build with platform-specific WebGL content.

## Read only these entry points first
- Assets/_Project/Editor/ContinuousIntegration.cs: BuildWebGl, Run.
- Assets/_Project/Editor/AddressablesWorkflow.cs: BuildLocalAndUseExisting, UseAssetDatabase, RestoreLocalSettings.
- Assets/_Project/Editor/TestTaskSetup.cs: ConfigureAddressables.
- Assets/_Project/Tests/EditMode/GeneratedProjectValidationTests.cs.
- Docs/Operations.md: State left behind by a content build.

## Implementation
1. Define the postcondition: once this method begins mutating setup/workflow state, restore Local + Use Asset Database on success and failure. The existing precondition for active target WebGL remains.
2. Enclose the mutating build section in try/finally and restore using the existing workflow API.
3. Keep the original build error as the primary reported failure. A cleanup failure must be visible too; do not accidentally mask the original exception with finally.
4. Use a small internal test seam for the build action/restoration if needed. Do not introduce a general build framework or require a real failing player build for every unit test.
5. Do not automatically switch platform back, clear all caches, delete outputs or revert arbitrary ProjectSettings. Document the precise restored state.

## Acceptance
- [ ] Success restores the intended profile and play-mode builder.
- [ ] Failure during content build and during player build attempts restoration.
- [ ] Build failure remains a failure even if restoration succeeds.
- [ ] A restoration failure is reported without losing the original build failure.
- [ ] No claim that restoration also removes or rebuilds platform-specific bundles.

## Verification and stop
Use injected failures for both build stages and cleanup failure; assert outcome and final settings, not just that a delegate ran.
Run shared runtime validation, generated validation and a successful local WebGL build where available.
Update Operations with exact postconditions. Stop after this build-lifetime fix.
