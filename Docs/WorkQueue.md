# Portfolio improvement work queue

Navigation only; live progress belongs to GitHub Issues. Briefs contain detailed execution
specifications mirrored in issue bodies. All implementation work is planned at authoring.

Start at [AgentStart](AgentStart.md); common execution rules: [TaskExecution](TaskExecution.md).
Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604, 2026-09-20.

Roadmap: [#1](https://github.com/Yurii-Tor/Addressables-Sample/issues/1) / [local brief](Issues/P00-roadmap.md).

| ID | Priority | Task / local brief | GitHub | Prerequisites |
|---|---|---|---|---|
| P01 | P1 | [Enable WebGL exception recovery and verify player failure paths](Issues/P01-webgl-exceptions.md) | [#2](https://github.com/Yurii-Tor/Addressables-Sample/issues/2) | None |
| P02 | P1 | [Restore the local Addressables workflow after WebGL build failures](Issues/P02-build-workflow-cleanup.md) | [#3](https://github.com/Yurii-Tor/Addressables-Sample/issues/3) | None |
| P03 | P1 | [Verify generator idempotency by comparing owned output fingerprints](Issues/P03-generation-fingerprints.md) | [#4](https://github.com/Yurii-Tor/Addressables-Sample/issues/4) | P02 |
| P04 | P1 | [Record bounded round outcomes so polling diagnostics cannot miss fast rounds](Issues/P04-polling-round-telemetry.md) | [#5](https://github.com/Yurii-Tor/Addressables-Sample/issues/5) | None |
| P05 | P2 | [Add controlled slow, failed and superseded round scenarios](Issues/P05-controlled-demo-scenarios.md) | [#7](https://github.com/Yurii-Tor/Addressables-Sample/issues/7) | P04, P09 |
| P06 | P2 | [Build touch-friendly scenario controls and block UI clicks from world selection](Issues/P06-responsive-demo-controls.md) | [#8](https://github.com/Yurii-Tor/Addressables-Sample/issues/8) | P03, P05 |
| P07 | P2 | [Provide understandable fatal feedback and a safe session restart](Issues/P07-restart-and-fatal-feedback.md) | [#9](https://github.com/Yurii-Tor/Addressables-Sample/issues/9) | P06 |
| P08 | P2 | [Replace demo imagery with a small original, documented texture set](Issues/P08-original-round-artwork.md) | [#10](https://github.com/Yurii-Tor/Addressables-Sample/issues/10) | P03 |
| P09 | P2 | [Cover synchronous loader throws, faulted completion and startup reentry](Issues/P09-recovery-branch-tests.md) | [#6](https://github.com/Yurii-Tor/Addressables-Sample/issues/6) | None |
| P10 | P3 | [Handle externally destroyed targets when a current round completes](Issues/P10-destroyed-target-cleanup.md) | [#11](https://github.com/Yurii-Tor/Addressables-Sample/issues/11) | P04 |
| P11 | P2 | [Make README concise and synchronize operational claims with code](Issues/P11-showcase-and-doc-accuracy.md) | [#12](https://github.com/Yurii-Tor/Addressables-Sample/issues/12) | P01, P02, P03, P04, P05, P06, P07, P08, P09 |
| P12 | P2 | [Verify the finished demo across editor workflows and a local WebGL player](Issues/P12-integrated-portfolio-validation.md) | [#13](https://github.com/Yurii-Tor/Addressables-Sample/issues/13) | P01, P02, P03, P04, P05, P06, P07, P08, P09, P11 |

P1 = correctness/reliability; P2 = portfolio value; P3 = optional robustness.
These are planning judgments, not claims of reproduced shipped failures.

## Delivery order and conflicts

1. P01, P02, P03: exception support, build cleanup, generation checks.
2. P04 and P09: telemetry and recovery tests.
3. P05 -> P06 -> P07: scenarios, controls, restart.
4. P08 and P11: original artwork and accurate showcase.
5. P12: final integrated validation.
6. P10 is optional; preferably before P07 if selected.

Dependencies are minimum prerequisites. P01/P02/P03 edit ContinuousIntegration.cs;
P04/P05/P07/P10 touch controller/lifetime; P05/P06/P07/P08 touch setup/validation.
Prefer sequential tasks for shared files. This is not authorization for parallel coding.

## Launch one task

```text
Implement only the selected GitHub issue and matching Docs/Issues/Pxx brief.
Read AGENTS.md, the local status marker if present, PROJECT_DIRECTION.md,
Docs/AgentStart.md and Docs/TaskExecution.md. Confirm dependency code exists here.
Follow the brief's source map, invariants, steps, acceptance criteria and checks.
Keep a current milestone at the top of Docs/ImplementationPlan.md.
Preserve unrelated work and this retained checkout. Do not push, merge, deploy or
close the issue. Finish with a validated local change and evidence-based handoff.
If a required check is unavailable, report it explicitly rather than claim completion.
```

Choose the issue/brief before sending the prompt. Do not ask a small model to implement
the whole roadmap. Choose model/effort in Codex setup, outside permanent requirements.
