# P03: Verify generator idempotency by comparing owned output fingerprints

Priority: P1. Dependencies: P02.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#4](https://github.com/Yurii-Tor/Addressables-Sample/issues/4).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and evidence
ContinuousIntegration.VerifyGeneratedProject currently runs TestTaskSetup.Run twice and
ProjectValidation.ValidateOrThrow once. Structural validity does not establish that the
second setup leaves files, GUIDs and identities unchanged.

## Read only these entry points first
- Assets/_Project/Editor/ContinuousIntegration.cs: VerifyGeneratedProject.
- Assets/_Project/Editor/TestTaskSetup.cs: Run and reconciliation helpers.
- Assets/_Project/Editor/TestTaskPaths.cs: all generated paths.
- Assets/_Project/Editor/ProjectValidation.cs: ValidateOrThrow.
- Assets/_Project/Tests/EditMode/GeneratedProjectValidationTests.cs.
- .github/workflows/tests.yml: validate job. Requirements REQ-Q01 and generation rule in AGENTS.md.

## Implementation
1. Derive an explicit owned-output inventory from actual generator writes: generated asset folders/files and their .meta, generated config, Addressables source settings/groups/profiles, supplied texture importer .meta, EditorBuildSettings and any other settings the generator actually authors. Inspect Run; do not guess a broad recursive project glob.
2. Flush Unity asset/scene writes before snapshots. Capture normalized relative paths plus SHA-256 bytes after setup pass 1.
3. Run setup pass 2, flush again, capture the same scope. Compare path set and hashes; added/removed/changed paths fail with concise actionable diagnostics.
4. Keep structural validation too. Byte equality alone cannot prove the first result correct.
5. Exclude Library, Temp, Logs, obj, ServerData and player/build output. Do not include timestamps or hash remote content.
6. If a generated source is nondeterministic, fix the authored value or document a narrowly justified canonical comparison; do not silently exclude a failing scene/.meta.
7. Keep comparison code small and testable with temporary fixture files inside a safe test directory. Never recursively delete a computed path without verifying containment.

## Acceptance
- [ ] Two unchanged setup passes produce identical scoped manifests.
- [ ] Added, removed and modified scoped files are detected with their relative paths.
- [ ] GUID drift is detected through .meta and/or explicit GUID checks.
- [ ] Comparison order and slash normalization are deterministic.
- [ ] Structural validation still runs; CI exits nonzero on mismatch.

## Verification and stop
Tests cover equal manifests, addition, removal, content change and excluded output noise.
Run actual VerifyGeneratedProject twice and inspect repository diff, then shared runtime matrix.
Do not claim full cross-machine reproducible builds: this gate proves consecutive setup
idempotency for the defined source scope. Document that distinction in Operations.
