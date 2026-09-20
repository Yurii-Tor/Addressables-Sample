# Portfolio improvement roadmap

## Outcome
Turn the existing one-scene Unity Addressables sample into a small, observable portfolio demo:
correct browser recovery/build cleanup, trustworthy diagnostics, three controlled scenarios,
touch-friendly controls, safe restart, original imagery and accurate documentation.

Audit baseline: `45fcc696f4005780d06db1bd8e5b28876c1dd604` (2026-09-20).
Findings were established from source; Unity/browser validation was not rerun for this audit.
This epic tracks implementation, not a claim the planned features are shipped.

## Context scaffold
Published scaffold: [codex/portfolio-issue-scaffold](https://github.com/Yurii-Tor/Addressables-Sample/tree/codex/portfolio-issue-scaffold).
The owner subsequently authorized pushing and merging this documentation into main.
Every child issue contains the complete task brief and essential shared constraints.
After integration, new implementation tasks should start from updated main.

When available, read in this order:
1. `AGENTS.md`, local status marker (if present), `PROJECT_DIRECTION.md`.
2. `Docs/AgentStart.md` (source map and invariants).
3. `Docs/WorkQueue.md`, then only the selected `Docs/Issues/Pxx-*.md`.
4. `Docs/TaskExecution.md` (test routes and handoff).

The historical `Docs/ImplementationPlan.md` is preserved; only its current milestone header
is required for routine work. Do not reload its historical 45 KB for every task.

## Implementation checklist
- [ ] P01: [Enable WebGL exception recovery and verify player failure paths](https://github.com/Yurii-Tor/Addressables-Sample/issues/2)
- [ ] P02: [Restore the local Addressables workflow after WebGL build failures](https://github.com/Yurii-Tor/Addressables-Sample/issues/3)
- [ ] P03: [Verify generator idempotency by comparing owned output fingerprints](https://github.com/Yurii-Tor/Addressables-Sample/issues/4)
- [ ] P04: [Record bounded round outcomes so polling diagnostics cannot miss fast rounds](https://github.com/Yurii-Tor/Addressables-Sample/issues/5)
- [ ] P05: [Add controlled slow, failed and superseded round scenarios](https://github.com/Yurii-Tor/Addressables-Sample/issues/7)
- [ ] P06: [Build touch-friendly scenario controls and block UI clicks from world selection](https://github.com/Yurii-Tor/Addressables-Sample/issues/8)
- [ ] P07: [Provide understandable fatal feedback and a safe session restart](https://github.com/Yurii-Tor/Addressables-Sample/issues/9)
- [ ] P08: [Replace demo imagery with a small original, documented texture set](https://github.com/Yurii-Tor/Addressables-Sample/issues/10)
- [ ] P09: [Cover synchronous loader throws, faulted completion and startup reentry](https://github.com/Yurii-Tor/Addressables-Sample/issues/6)
- [ ] P10: [Handle externally destroyed targets when a current round completes](https://github.com/Yurii-Tor/Addressables-Sample/issues/11) (optional)
- [ ] P11: [Make README concise and synchronize operational claims with code](https://github.com/Yurii-Tor/Addressables-Sample/issues/12)
- [ ] P12: [Verify the finished demo across editor workflows and a local WebGL player](https://github.com/Yurii-Tor/Addressables-Sample/issues/13)

## Limits
One scene, one target, existing composition root/controller/adapters. No added DI/async
libraries, gameplay expansion, generic scenario framework or new backend. Diagnostics poll.
Keep one raw handle owner; never use a released asset to stage a fake late completion.
Normal input remains Ready-only. Commands demonstrating scenarios do not increase score.
Preserve the retained checkout; no moving, deleting, cleaning or restructuring it.

## Delivery policy
Owner selects one issue at a time. Dependencies must exist in the selected checkout.
Follow repository branch/local-commit rules. No push, merge, public remote failure probe,
deployment or release is authorized by this epic. Docs/showcase remain on their task branch
unless merging is separately authorized.

## Completion
Core child tasks complete with source-linked evidence, both editor workflows validated,
local WebGL recovery/UI smoke verified, documentation accurate. Optional P10 can be deferred
explicitly. Historical test reports cannot satisfy current gates.
