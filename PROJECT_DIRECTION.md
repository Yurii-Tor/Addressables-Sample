# Project direction

## Product boundary

A small, one-scene Unity portfolio demo: one selectable cube demonstrates asynchronous
Addressables loading, explicit resource ownership, fallback and request invalidation.
A visitor should understand these behaviors in about one minute.

Keep the controller/adapters/composition-root architecture. No extra levels, economy,
inventory, DI framework, async package, analytics backend or generic scenario framework.
Normal play continues to accept hits only in Ready.

## Authority and current scope

The 2026-09-20 audit produced a planned backlog, not implemented features.
Start at [AgentStart](Docs/AgentStart.md), then select one brief in [WorkQueue](Docs/WorkQueue.md).

The original [requirements](Docs/Requirements.md) and PDF remain authoritative for baseline
gameplay. Portfolio additions are optional demonstration/presentation features; do not
silently replace baseline acceptance criteria. Escalate an actual specification conflict.

AGENTS.md owns engineering rules. The machine-local status marker, when present, owns
checkout preservation constraints; it is not a portable implementation specification.
GitHub Issues own live discussion/scheduling. Checked-in briefs are the detailed execution
specification mirrored in the initial issue body; update both when scope changes.
An open issue does not authorize implementing the whole backlog.

## Delivery boundary

The scaffold task authorizes documentation and issue creation only. Future implementation
starts when the owner selects an issue. Local commits follow AGENTS.md; no push, merge,
release, deployment or modification of the separate demos-site is implied.

Do not move, rename, clean or restructure this retained checkout. New documentation is
intentional; historical evidence stays in place. Preserve unrelated user changes.

## Sequence

Build/correctness fixes, observable scenarios, presentation/recovery, original artwork
and accurate showcase documentation, then final integrated validation. WorkQueue contains
exact dependencies and optional follow-ups.
