# P12: Verify the finished demo across editor workflows and a local WebGL player

Priority: P2. Dependencies: P01, P02, P03, P04, P05, P06, P07, P08, P09, P11.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#13](https://github.com/Yurii-Tor/Addressables-Sample/issues/13).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Goal
Produce one reproducible evidence record for the final integrated code state. Existing
historical test counts and desktop PlayMode success do not establish browser recovery.
This task is local validation and documentation, not deployment authorization.

## Read only these entry points first
- Docs/TaskExecution.md; Docs/Operations.md: 2, 6-8, 9a.
- Docs/WorkQueue.md and completed issue evidence; verify actual dependency code is present.
- Assets/_Project/Tests/PlayMode/RealGamePlayModeTests.cs; GameControllerPlayModeTests.cs.
- Assets/_Project/Editor/ContinuousIntegration.cs; Tools/Build-WebGlDemo.ps1.
- .github/workflows/tests.yml: CI currently does not run an Existing Build matrix.

## Execution
1. Record source commit, clean/dirty state, Unity version, platforms and availability.
2. Run generated output fingerprint/structural validation, complete EditMode, PlayMode
   Asset Database, fresh desktop Addressables build, PlayMode Existing Build.
3. Restore local defaults and validate again. Inspect diffs; preserve unrelated edits.
4. Build WebGL to an explicit local Builds/WebGL path and serve via an existing local HTTP
   server. Do not invoke the publish script or write to the demos-site by default.
5. Browser checklist: startup/hit/miss; slow load; injected failure and next success;
   A superseded/B displayed; restart in Ready/pending/fatal; repeated clicks; show/hide;
   UI-over-cube; narrow portrait/landscape; console errors; retained owner behavior.
6. Distinguish simulation from real WebGL failure tests. Include P01 thrown-exception
   recovery plus isolated missing-key/asset failure. Never make a public endpoint incomplete.
7. If P10 was selected, include lost-target completion verification.
8. Record results in a concise Docs/Validation.md table with commit, command/route,
   expected/actual, platform/workflow, artifact/log path and blockers.
9. Evaluate adding Existing Build coverage to CI only as a separately scoped follow-up
   after measuring local behavior; do not expand workflows automatically in this issue.

## Acceptance
- [ ] Evidence identifies the exact tested code state and each workflow.
- [ ] All selected feature criteria have passed or are explicitly blocked, with no invented results.
- [ ] Owners stay bounded over repeated rounds/restarts and teardown reaches zero; this is
      ownership evidence, not a comprehensive memory profiler result.
- [ ] Browser behavior and responsive layout were inspected in a real local player.
- [ ] Project is left in documented local development mode.
- [ ] README/Operations describe what was actually validated.

## Verification and stop
This issue executes the full shared validation matrix. If a gate fails, report/reproduce
and link a bounded fix; do not silently redesign features or mark the gate passed.
Stop with local evidence and a handoff. Deploy, push, release and issue closure require
their own authorization. Logs/builds remain ignored.
