# Project

Unity 6000.3.21f1, URP, Addressables 4.0.1, Input System.
The PDF and Docs/Requirements.md are the source of truth.

# Architecture

- Keep the architecture proportional to this one-scene test task.
- Do not add Zenject, Extenject, UniTask, or other third-party dependencies.
- Use GameBootstrapper as the composition root.
- Keep game flow in a plain C# controller.
- Wrap Addressables access behind an interface.
- Every AsyncOperationHandle must have one explicit owner.
- Never use WaitForCompletion, Task.Wait, .Result, or synchronous busy waiting.
- Use MaterialPropertyBlock instead of instantiating materials.
- Use async void only for Unity lifecycle or event boundaries.
- Diagnostics and overlays observe the controller by polling. Never add presentation
  events, callbacks, or references to the controller for their benefit.

# Unity assets

- Do not hand-edit scene, prefab, Addressables, or .meta YAML.
- Create idempotent Editor tooling for generated project content.
- Re-running the setup command must not duplicate objects, groups, labels, or assets.
- Do not modify unrelated ProjectSettings or package versions.
- URP recomputes material keywords on import. Author the inputs URP derives them from,
  never the keywords directly, and assert the result in ProjectValidation.

# Planning

- For portfolio backlog work, read `PROJECT_DIRECTION.md` and `Docs/AgentStart.md`,
  then only the selected brief from `Docs/WorkQueue.md` and its source map.
- Keep the current milestone at the top of `Docs/ImplementationPlan.md`; its historical
  implementation record is reference material, not a new task list.
- `Docs/TaskExecution.md` routes verification and handoff. GitHub Issues track live
  progress; keep their execution brief and `Docs/Issues/` counterpart aligned.

For multi-file implementation, maintain an ExecPlan in
Docs/ImplementationPlan.md with progress, decisions, validation, and blockers.

# Execution

- Continue through safe local implementation steps without asking between them.
- At each milestone: compile, run relevant tests, inspect logs, update the plan,
  and create a focused Git commit if validation passes.
- Delegate read-only specification review, test analysis, and code review to
  subagents when useful.
- Only the main agent may edit production files.

# Git

- Start every substantive change from a dedicated task branch (normally `codex/<short-purpose>`); do not develop directly on `main`.
- Merge into `main` only for changes that affect the playable game, generated build output, or required repository/CI configuration. Documentation, README, media, and other showcase-only changes stay on their task branch unless the owner explicitly asks to merge them.
- Preserve unrelated user changes.
- Never use git add .
- Stage only files belonging to the current milestone.
- Do not commit Library, Temp, Logs, obj, Build, Builds, .idea, APKs, or local logs.
- Do not amend, rebase, reset, delete branches, or push.
- Local milestone commits are authorized.

# Completion

For documentation-only work, verify links, referenced paths/symbols, scope and whitespace;
report that Unity was not run. Do not claim this validates runtime behavior.

Before finishing runtime, generated-content, or build-behavior changes:
- run EditMode and PlayMode tests;
- verify the Addressables Use Asset Database and Use Existing Build workflows
  (Simulate Groups no longer exists in Addressables 4.0.1);
- run ContinuousIntegration.VerifyGeneratedProject for setup idempotency and validation;
- audit every load and release path;
- verify cancellation prevents stale texture application;
- update Docs/Operations.md with exact setup and testing instructions, and keep the
  README a showcase rather than a manual;
- report anything that could not be validated in Unity.
