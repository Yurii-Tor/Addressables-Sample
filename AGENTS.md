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

# Unity assets

- Do not hand-edit scene, prefab, Addressables, or .meta YAML.
- Create idempotent Editor tooling for generated project content.
- Re-running the setup command must not duplicate objects, groups, labels, or assets.
- Do not modify unrelated ProjectSettings or package versions.

# Planning

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

- Preserve unrelated user changes.
- Never use git add .
- Stage only files belonging to the current milestone.
- Do not commit Library, Temp, Logs, obj, Build, Builds, .idea, APKs, or local logs.
- Do not amend, rebase, reset, delete branches, or push.
- Local milestone commits are authorized.

# Completion

Before finishing:
- run EditMode and PlayMode tests;
- verify Addressables Simulate Groups and Use Existing Build workflows;
- audit every load and release path;
- verify cancellation prevents stale texture application;
- update README with exact setup and testing instructions;
- report anything that could not be validated in Unity.