# Agent start

Read this once at task entry. Do not load the entire Docs directory.

## Minimum reading route

1. Read AGENTS.md, the machine-local status marker if present, and PROJECT_DIRECTION.md.
2. Inspect git status --short --branch, current commit and genuine product tags.
   Follow repository branching rules; preserve unrelated work.
3. Open [WorkQueue](WorkQueue.md), then only the brief selected by the owner.
4. Read its live GitHub issue and scope-changing discussion. Confirm dependencies exist
   in this checkout, not just that their issues are closed.
5. Open only the listed files/symbols and relevant requirements section.
6. Use [TaskExecution](TaskExecution.md) for verification. Maintain the current milestone
   at the top of [ImplementationPlan](ImplementationPlan.md).

The historical plan is about 45 KB at scaffold creation. It is evidence, not a fresh task
list. Search headings/symbols and read historical passages only for a concrete question.
Old session/local branch notes may be stale; Git owns checkout facts. Historical test
reports are not current validation.

## Verified baseline snapshot

Audit source: 45fcc696f4005780d06db1bd8e5b28876c1dd604, 2026-09-20.
No local release tags were present; re-check before future changes.
Unity 6000.3.21f1; Addressables 4.0.1; URP 17.3.0; Input System 1.20.0.
The audit did not execute Unity or revalidate the deployed player.

Implemented: hit/miss/score, round-robin textures, retained startup assets, async replacement
guards, diagnostics overlay, generated assets, tests, WebGL/local/remote desktop workflows.
Planned only: scenario controls, responsive diagnostics, retry, outcome history, build fixes
and stronger idempotency verification.

## Source map

Paths start at Assets/_Project/ unless explicitly stated. After a semicolon, a bare
filename shares the preceding file's directory; a path with slashes uses the table root.

| Concern | Entry point | Symbols / adjacent verification |
|---|---|---|
| State machine | Runtime/Core/GameController.cs | InitializeAsync, HandleSelection, BeginRound, CompleteRoundAsync, IsCurrentRound, EnterFatal, Dispose |
| Load contract | Runtime/Core/AddressableLoadContracts.cs | IAddressableAssetLoader, IAddressableLoad<T>, AddressableLoadResult<T> |
| Raw handle ownership | Runtime/Addressables/UnityAddressableAssetLoader.cs | AddressableLoad<T>, ReleaseOnce, AddressableOwnershipDiagnostics |
| Wiring/lifetime | Runtime/Presentation/GameBootstrapper.cs | Start, OnDestroy |
| Target | Runtime/Presentation/UnityTargetFactory.cs; TargetView.cs | Create/Destroy, ApplyTexture, Clear |
| Input | Runtime/Presentation/PointerSelectionInput.cs | OnPressPerformed, EvaluateSelection |
| UI | Runtime/Presentation/HudView.cs; DiagnosticsOverlay.cs | SampleRoundTrace, OnGUI; polling only |
| Config/order | Runtime/Config/GameConfig.cs; Runtime/Core/RoundTextureSelector.cs | TryCreateDefinition, Next |
| Generator | Editor/TestTaskSetup.cs; TestTaskPaths.cs | Run, ConfigureAddressables, scene reconciliation, RoundTexturePaths |
| Validation | Editor/ProjectValidation.cs | ValidateScene, ValidateAddressables, ValidateAssets |
| Builds | Editor/ContinuousIntegration.cs; AddressablesWorkflow.cs | VerifyGeneratedProject, BuildWebGl, BuildLocalAndUseExisting |
| Core tests | Tests/EditMode/GameControllerTests.cs; AddressableLoadTests.cs; TestDoubles.cs | manual completion, release order |
| Integration tests | Tests/PlayMode/RealGamePlayModeTests.cs; GameControllerPlayModeTests.cs; PlayModeTestDoubles.cs | real input/missing key, hostile late result |
| CI | .github/workflows/tests.yml; .github/workflows/webgl-demo.yml | actual triggers and artifacts |
| Commands | Docs/Operations.md | sections 2, 6-8, 9a, 12 |

## Async invariants

- Startup loads fallback/prefab once and retains their owners until session teardown.
- Displayed texture remains owned until replacement/fallback has been applied.
- Clear a pending owner field before disposal to prevent reentrant confusion.
- Production Dispose releases once and completes pending work as Canceled. It does not
  abort transport or deliver usable Unity assets after release.
- Continuations check state, generation, owner identity and target validity.
- Teardown invalidates requests, clears/destroys target, then releases displayed/startup owners.
- ActiveOwnerCount counts application owners, not memory, requests or all ResourceManager
  references. Ready success normally has 3; fallback 2; teardown 0.
- GameController is a plain class without MonoBehaviour but uses Unity asset types.
- Polling views never cause controller events/callbacks/references to presentation.

## Efficient search

Run from repository root:

```powershell
rg -n 'BeginRound|IsCurrentRound|EnterFatal' Assets/_Project/Runtime/Core/GameController.cs
rg -n 'public (async Task|IEnumerator|void)' Assets/_Project/Tests
rg -n 'ValidateScene|expectedHudChildren|expectedRootNames' Assets/_Project/Editor/ProjectValidation.cs
rg -n '^##' Docs/Operations.md
rg --files Assets/_Project/Runtime Assets/_Project/Tests -g '*.cs'
```

Read the relevant caller/cleanup paths too. Prefer symbols to brittle line numbers.
Avoid Library, Temp, builds, package caches and media unless needed by the selected issue.

## Traps

- Local modes: Use Asset Database and Use Existing Build; no Simulate Groups in 4.0.1.
- Bundles are platform-specific. WebGL cannot use desktop bundles.
- Tools/Build-WebGlDemo.ps1 defaults outside this repository into demos-site. Always pass
  an explicit local Builds/WebGL output for implementation validation.
- Setup reconciles strict roots/children. New UI needs generator AND validator updates.
  Never hand-edit scene, prefab, Addressables or .meta YAML.
- SourceAuditTests is a limited text-pattern guard, not a semantic analyzer/leak proof.
- Do not take down public bundles/catalogs for failure tests. Use local isolated content
  or explicitly labeled simulation.
- Missing Unity/license/module means a stated validation blocker, never invented pass counts.
