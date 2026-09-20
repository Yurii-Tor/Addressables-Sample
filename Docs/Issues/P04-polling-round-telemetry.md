# P04: Record bounded round outcomes so polling diagnostics cannot miss fast rounds

Priority: P1. Dependencies: none.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#5](https://github.com/Yurii-Tor/Addressables-Sample/issues/5).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
DiagnosticsOverlay.SampleRoundTrace infers settlement from Ready/non-Ready transitions
and matching observed generation. A round completing between two Update calls is skipped.
ActiveOwnerCount is displayed as live handles, which overstates what it measures.

## Read only these entry points first
- Assets/_Project/Runtime/Presentation/DiagnosticsOverlay.cs: SampleRoundTrace, OnGUI.
- Assets/_Project/Runtime/Core/GameController.cs: BeginRound, CaptureRoundDuration, ApplySuccessfulRound, ApplyFallbackRound, EnterFatal, Dispose.
- Assets/_Project/Runtime/Addressables/UnityAddressableAssetLoader.cs: AddressableOwnershipDiagnostics.
- Assets/_Project/Tests/EditMode/GameControllerTests.cs: telemetry and supersession cases.

## Locked small design
Use a bounded history of immutable terminal round facts, capacity 16. Each has monotonic
sequence, requested generation, outcome and elapsed milliseconds. Outcomes: Succeeded,
Fallback, Superseded, Canceled, Fatal. No Unity object/handle references in a record.
Expose non-destructive read-only polling; no events, view callbacks or controller-to-overlay reference.
Keep RoundGeneration as request invalidation token: not every increment represents a request.

## Implementation
1. Add a small value record/history implementation in Runtime/Core only if needed.
2. Record exactly one terminal outcome for each round actually started. Supersession captures the old round time before the stopwatch is restarted.
3. Track the unsettled generation/terminal status separately from _pendingRound, which is cleared before presentation calls. Make terminal recording idempotent so presentation exceptions still produce exactly one Fatal outcome.
4. Record Succeeded/Fallback only after texture application and Ready transition complete.
   Presentation failure ends Fatal, never a successful record. Startup failure has no round record.
5. A stale completion must not append another outcome or overwrite the newest duration.
6. Have overlay keep its own sequence cursor, consume all retained unseen records and show a gap when its cursor predates retained history. No destructive dequeue shared by observers.
7. Reset cursor/display when controller identity changes (needed for later retry).
8. Rename label to Active load owners; optionally show expected steady-state counts. Do not add fake network/cache/memory metrics.

## Acceptance
- [ ] A round beginning/ending between polls is visible.
- [ ] Multiple rounds between polls and A superseded/B completed are displayed in order.
- [ ] Repeated polling produces no duplicate terminal messages.
- [ ] Rollover is bounded and shows a truthful history gap.
- [ ] Disposal/fatal invalidation does not invent a successful round.
- [ ] Existing ownership and score behavior is unchanged.

## Verification and stop
Add deterministic tests for instant success/failure, burst before polling, stale A, rollover,
repeated polls and controller replacement. Avoid wall-clock exact duration assertions.
Run shared runtime matrix. Keep the existing visual overlay style in this issue; P06 owns UI.
