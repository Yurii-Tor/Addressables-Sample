# Operations

Build, deploy, and validation procedures. For what the project *is*, see the
[README](../README.md).

For implementing a portfolio backlog task, start at [AgentStart](AgentStart.md) and
[TaskExecution](TaskExecution.md); select its brief through [WorkQueue](WorkQueue.md).
The 2026-09-20 audit identified stale CI/hosting claims in section 9 below. Until P11
is implemented, use the actual workflow YAML and section 9a for build/hosting behavior.

## 1. Exact versions

- Unity: `6000.3.21f1` (`c02631ffc030`)
- Universal Render Pipeline: `17.3.0`
- Addressables: `4.0.1`
- Input System: `1.20.0`
- Unity UI (UGUI): `2.0.0`
- Unity Test Framework: `1.6.0`
- Scriptable Build Pipeline: `4.0.0` (Addressables dependency)

## 2. Prerequisites

- Install Unity `6000.3.21f1` with platform support for the active build target.
- Clone normally. **Git LFS is not used** — all assets are stored directly in Git.
- Let Unity resolve the locked packages before using the project.
- Close every interactive Unity instance that has this project open before running a batch
  command. Unity must not open the same project twice.
- Run the commands below from the repository root in PowerShell.

```powershell
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Force -Path Logs | Out-Null
```

## 3. Generate or repair the project

**AddressablesSample > Game > Setup Test Task** creates or reconciles the generated scene, prefab,
upright-UV cube mesh, opaque light-background material, fallback texture, post-processing profile, texture import settings,
configuration, Build Settings, and Addressables source settings. It is safe to run
repeatedly and preserves generated asset GUIDs and scene object identities. Setup
intentionally restores the `Local` profile; select the Cloudflare workflow afterward when
testing hosted content.

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine `
  -logFile Logs/Setup.log
```

Validate with **AddressablesSample > Game > Validate Generated Project** or:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.ProjectValidation.ValidateFromCommandLine `
  -logFile Logs/Validation.log
```

`Assets/_Project/Scenes/Game.unity` is the sole enabled Build Settings scene.

### Material invariants the validator enforces

The generated material uses the URP-compatible `AddressablesSample/Target Surface` shader. It renders
opaque geometry and composites each PNG's transparent pixels over a light blue cube colour,
so the cube remains visible against the dark scene while the animal artwork stays crisp.
`TargetView` still supplies the round texture, tint, and miss emission through one
`MaterialPropertyBlock`.

The generated `TargetCube.asset` has independent vertices per face. Each vertical face maps
UV bottom to world bottom and UV top to world top; no global texture flip is applied. Shader,
render type, background colour, texture transform, mesh topology, mesh assignment, and all
four world-up UV mappings are asserted by `ProjectValidation`, so regressions fail CI.

## 4. Scene and controls

Open `Assets/_Project/Scenes/Game.unity` and enter Play Mode.

- Click the cube with the primary mouse button, or tap it on a touchscreen, to score and
  request the next image.
- Click or tap away from the cube to flash it red briefly. A miss does not change the score,
  texture, or active round request.
- Press <kbd>~</kbd> or <kbd>F1</kbd> to toggle the diagnostics overlay.
- Input is accepted only while the controller is in `Ready`; it is disabled during startup
  and round loading.

The target is instantiated once from the Addressable prefab. Round changes update its
renderer through a `MaterialPropertyBlock`; the shared material is never instantiated or
replaced.

## 5. State, status, score, and fallback behaviour

The controller progresses through `Uninitialized`, startup fallback loading, startup prefab
loading, round loading, `Ready`, and finally either `FatalError` or `Disposed`.

- Startup displays `Loading game...` and initializes the score to `Score: 0`.
- A round request displays `Loading image...` while retaining the currently displayed texture.
- A successful request applies the new texture, returns to `Tap the object!`, and releases
  the previous round owner.
- A failed round request applies the retained checkerboard fallback, reports one controlled
  warning, and returns to `Image failed - using fallback. Tap the object!`.
- A hit increments the score exactly once. A miss never increments it.
- Failure to load a required startup asset enters `Unable to start. See Console.` and
  performs full cleanup.

## 6. Use Asset Database local emulation

Addressables `4.0.1` no longer ships the legacy **Simulate Groups** play-mode script. Its
supported local-emulation replacement is **Use Asset Database (fastest)**. This project
configures that mode with a `0.25` second simulated load delay so loading is observable.
Stale replacement is exercised separately by the delayed-owner PlayMode tests because normal
player input is disabled while a round is loading.

Choose **AddressablesSample > Game > Addressables > Use Asset Database**, or run:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.UseAssetDatabaseFromCommandLine `
  -logFile Logs/UseAssetDatabase.log
```

This activates the `Local` profile and is the default development workflow. It requires no
content build and no server.

## 7. Fresh build and Use Existing Build

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine `
  -logFile Logs/AddressablesBuild.log
```

The command restores the `Local` profile, selects the stock schema-driven builder, removes
prior player content through the Addressables API, creates fresh catalogs and bundles,
verifies the runtime settings/catalog output, and selects **Use Existing Build (requires
built groups)**.

Restore the committed defaults afterwards:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine `
  -logFile Logs/RestoreLocal.log
```

## 8. Automated validation

Run setup twice to verify idempotency, then run the validator. `ContinuousIntegration.VerifyGeneratedProject`
does exactly this in one invocation, and is what CI calls:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.ContinuousIntegration.VerifyGeneratedProject `
  -logFile Logs/Verify.log
```

EditMode tests:

```powershell
& $unityPath -batchmode -nographics -projectPath $projectPath `
  -runTests -testPlatform EditMode `
  -testResults Logs/EditMode.xml -logFile Logs/EditMode.log
```

PlayMode tests in Asset Database mode:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.UseAssetDatabaseFromCommandLine -logFile Logs/UseAssetDatabase.log
& $unityPath -batchmode -nographics -projectPath $projectPath -runTests -testPlatform PlayMode -testResults Logs/PlayMode-AssetDatabase.xml -logFile Logs/PlayMode-AssetDatabase.log
```

Then build fresh content and run the same PlayMode suite against it:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine -logFile Logs/AddressablesBuild.log
& $unityPath -batchmode -nographics -projectPath $projectPath -runTests -testPlatform PlayMode -testResults Logs/PlayMode-ExistingBuild.xml -logFile Logs/PlayMode-ExistingBuild.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine -logFile Logs/RestoreLocal.log
```

XML results and full Editor logs are written under the ignored `Logs/` directory. The
deliberate invalid-round test asserts its single expected warning; any other unexpected
Unity exception or error during the run fails the suite. Unity licensing-service startup
diagnostics may still appear in a successful batch log and should be assessed separately
from test output.

`RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable` still requires a valid
startup catalog because the target prefab and fallback are genuine Addressables. If it
reports `No Location found for Key=game/fallback`, the fallback logic has not failed: no
catalog was loaded at all. Re-select **Use Asset Database**, or complete the Cloudflare
sequence below, before rerunning it.

If PlayMode tests are launched in a standalone `PlayerWithTests`, build Addressables content
first. The project deliberately uses **Do not Build Addressables content on Player Build**
so a player/test build consumes explicitly validated content instead of silently rebuilding
a different remote catalog.

## 9. Continuous integration

Two workflows live in `.github/workflows/`:

| Workflow | Trigger | What it does |
|---|---|---|
| `tests.yml` | push, PR | EditMode + PlayMode suites, plus double-setup idempotency and structural validation |
| `webgl-demo.yml` | push to `main` | Builds the WebGL player and publishes it to GitHub Pages |

Both need three repository secrets under **Settings > Secrets and variables > Actions**:

| Secret | Value |
|---|---|
| `UNITY_LICENSE` | full contents of `Unity_lic.ulf` (personal licence) |
| `UNITY_EMAIL` | Unity account e-mail |
| `UNITY_PASSWORD` | Unity account password |

To obtain `Unity_lic.ulf` for a personal licence, follow the
[game-ci activation guide](https://game.ci/docs/github/activation). For the WebGL workflow,
also set **Settings > Pages > Source** to **GitHub Actions**.

The WebGL player is built uncompressed on purpose: GitHub Pages serves static files without
the `Content-Encoding` headers that Brotli or gzip Unity builds require.

## 9a. WebGL demo build and hosting

The playable demo is a WebGL player served as a static site. Its Addressables content is
**local**: the content build writes into the Addressables build path and Unity copies it into
`StreamingAssets` during the player build. Nothing about the hosting URL is baked in, so the
same output can be served from any origin and any sub-path without rebuilding.

Requires the **Web Build Support** module for the editor. Install it from Unity Hub
(Installs > the editor's gear icon > Add modules), or from an **Administrator** PowerShell:

```powershell
& "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules `
  --version 6000.3.21f1 --module webgl --childModules
```

Then build straight into the static site directory:

```powershell
.\Tools\Build-WebGlDemo.ps1 -OutputPath '<demos-site>\public\addressables-selection'
```

The script switches the project to WebGL, regenerates and validates the project, builds the
WebGL Addressables content, and produces the player. The first target switch reimports every
asset and takes a while.

Deploy the static site from its own directory:

```powershell
npx wrangler deploy
```

Note that Addressables content is platform specific. The `addressables-sample` deployment
hosts `StandaloneWindows64` bundles for the desktop build; a WebGL player cannot load them
and needs its own set, which is why the demo carries its content locally.

### The JSON catalog, and an Addressables 4.0.1 bug worth knowing about

The project publishes a **JSON** catalog rather than the binary default, so the catalog can
be opened and read at its published URL. `TestTaskSetup` sets it, and `ProjectValidation`
asserts it.

The validator deliberately checks the **persisted** `m_CatalogProviderType.m_ClassName`
rather than the `AddressableAssetSettings.EnableJsonCatalog` property, because that property
cannot be trusted in Addressables 4.0.1:

- `m_CatalogProviderType` is a `SerializedType` **struct**, declared with the field
  initializer `new SerializedType { Value = typeof(BinaryCatalogProvider) }`.
- That initializer seeds `m_CachedType`, which is **not** a serialized field.
- Deserialization replaces only the serialized `m_AssemblyName` / `m_ClassName` strings, so
  the stale cache survives, and the getter returns it in preference to the saved strings.

The practical consequence: in any freshly loaded editor session the property reports
`BinaryCatalogProvider` even when the asset on disk clearly says `JsonCatalogProvider`.
Assigning the property repairs the cache for the rest of that session, which is why every
content build in this project runs **Setup Test Task** first -- and why building content
without running setup would silently produce a binary catalog.

### State left behind by a content build

An Addressables content build switches the play-mode script to **Use Existing Build**, and
the content it produces belongs to the active build target. Both the structural validator
and the PlayMode suite reject that combination in the editor -- correctly, since editor play
mode cannot load WebGL bundles. `ContinuousIntegration.BuildWebGl` therefore restores
**Use Asset Database** before returning, so a demo build never leaves the repository in a
state its own checks fail.

## 10. Architecture and ownership

`GameBootstrapper` is the scene composition root. `GameController` is a plain C# state
machine that depends on small interfaces for loading, presentation, target creation, and
diagnostics. Unity Addressables access is isolated behind `IAddressableAssetLoader`.

`AddressableLoad<T>` is the sole production owner of a raw `AsyncOperationHandle<T>` and the
only production type that calls `Addressables.Release`. One owner represents one acquired
handle reference and releases it exactly once. The fallback and prefab owners remain
retained for the playable session. The displayed round owner remains retained until a
replacement or fallback has been applied. Teardown disables and destroys the instantiated
target before releasing the prefab, round, and fallback owners.

The implementation contains no `WaitForCompletion`, `Task.Wait`, synchronous `.Result`, or
busy waiting. The only runtime `async void` method is the Unity `Start` lifecycle boundary.

`DiagnosticsOverlay` observes the controller by polling and never mutates it, so the
controller carries no presentation coupling for the sake of the overlay.

## 11. Cancellation semantics

Addressables does not expose a reliable transport abort for `LoadAssetAsync`. Cancellation
therefore means logical invalidation plus prompt ownership cleanup:

- every round receives a generation number and a distinct owner identity;
- starting a replacement round disposes the pending owner immediately;
- pending callbacks are unsubscribed before the handle is released;
- every continuation checks controller state, generation, owner identity, and target
  validity before mutating presentation;
- a stale completion can never replace the newer texture, change HUD state, or take
  ownership again.

This is intentionally not described as aborting an underlying download.

## 12. Cloudflare HTTPS remote workflow

The concrete `Cloudflare` profile points to:

```text
https://addressables-sample.pages.dev/[BuildTarget]
```

The target prefab and fallback remain local startup content. The remote catalog and each
round texture bundle are written to `ServerData/[BuildTarget]`; round textures use **Pack
Separately**, so each new round can make an independent HTTPS request.

After changing code, material settings, texture import settings, platform, or Addressables
configuration, rebuild and republish in this exact order:

1. Run **AddressablesSample > Game > Setup Test Task**.
2. Run **AddressablesSample > Game > Addressables > Build Cloudflare Remote + Use Existing Build**.
3. Wait for `Cloudflare Addressables content is ready in ServerData` in the Console.
4. From the project root, publish the complete `ServerData` directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Publish-RemoteContent.ps1
```

The equivalent batch build command:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildCloudflareAndUseExistingFromCommandLine `
  -logFile Logs/AddressablesCloudflareBuild.log
```

The publish script waits for Cloudflare and verifies every catalog, hash, and bundle URL
returns HTTP 200. After a successful deployment, keep **Use Existing Build** active, open
`Assets/_Project/Scenes/Game.unity`, clear the Console, and enter Play Mode. A successful
run downloads the catalog over HTTPS, loads the local prefab/fallback once, and downloads
individual round bundles from Cloudflare.

If the correct remote content is already built in this project's `Library` and already
uploaded, **AddressablesSample > Game > Addressables > Use Uploaded Cloudflare Build** reactivates it
without rebuilding or publishing. A freshly extracted checkout has no `Library`, so it must
run the full build once even when an older deployment exists.

For another HTTPS endpoint:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.ConfigureRemoteFromCommandLine `
  -remoteBaseUrl 'https://cdn.example.com/addressablessample' `
  -logFile Logs/AddressablesRemoteBuild.log
```

### Testing fallback behaviour

1. **Deterministic automated test:** select a valid Addressables workflow and run
   `RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable`. It uses the real
   Addressables loader with a deliberately absent round key, verifies the retained fallback,
   verifies that the target remains selectable, and verifies teardown ownership.
2. **Real HTTP 404 test:** exit Play Mode, run **AddressablesSample > Game > Addressables > Clear
   Download Cache**, then temporarily remove the `ant` bundle from
   `ServerData/StandaloneWindows64` and deploy that incomplete directory. Keep the catalog
   and hash unchanged. Start a fresh Play Mode session: the first round requests `ant`,
   receives a 404, applies `FallbackTexture`, shows `Image failed - using fallback. Tap the
   object!`, and remains playable. Restore the bundle, deploy again immediately, and clear
   the cache before the final smoke test. Prefer a temporary Cloudflare Pages project,
   because the public endpoint is intentionally incomplete during the probe.

Do not test by taking down the catalog itself. Without a catalog the startup prefab and
fallback keys cannot be resolved, so a startup fatal error is the correct result rather than
a round-level fallback.

To return to the offline baseline, run **AddressablesSample > Game > Addressables > Restore Local
Defaults**.

## 13. Known limitations and validation scope

- Automated input coverage uses virtual Input System mouse and touchscreen devices in
  editor-hosted PlayMode. Run the documented prebuild first if executing tests in
  `PlayerWithTests`.
- Addressables content is platform specific. Rebuild and upload the matching `[BuildTarget]`
  directory before testing another platform.
- The Cloudflare Wrangler cache (`.wrangler/`) is no longer tracked; it contains account
  metadata and belongs in local state only. `Tools/Publish-RemoteContent.ps1` recreates
  whatever it needs. No API token has ever been committed.
