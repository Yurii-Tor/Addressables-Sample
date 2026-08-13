# AddressablesSample Addressables Test Task

## 1. Project summary

This Unity project implements the supplied one-scene selection game. It loads a target prefab, a retained fallback texture, and an ordered set of ten round textures through Addressables. Clicking or tapping the visible target increments the score and begins the next asynchronous texture request. Missing a target gives a bright red/emissive flash without changing the score or starting a round. Failed round loads apply the retained fallback and leave the game playable. Source alpha is preserved and the supplied images are vertically corrected by the renderer property block.

The implementation includes deterministic project-generation tooling, structural validation, EditMode lifecycle tests, PlayMode input and Addressables tests, and local workflows for both Asset Database emulation and built content.

## 2. Exact versions

- Unity: `6000.3.21f1` (`c02631ffc030`)
- Universal Render Pipeline: `17.3.0`
- Addressables: `4.0.1`
- Input System: `1.20.0`
- Unity UI (UGUI): `2.0.0`
- Unity Test Framework: `1.6.0`
- Scriptable Build Pipeline: `4.0.0` (Addressables dependency)

## 3. Prerequisites

- Install Unity `6000.3.21f1` with the platform support needed for the active build target.
- Install Git LFS and run `git lfs pull` after cloning.
- Let Unity resolve the locked packages before using the project.
- Close every interactive Unity instance that has this project open before running a batch command. Unity must not open the same project twice.
- Run the commands below from the repository root in PowerShell.

```powershell
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
New-Item -ItemType Directory -Force -Path Logs | Out-Null
```

## 4. Generate or repair the project

In the Editor, use **AddressablesSample > Game > Setup Test Task**. This one-click setup creates or reconciles the generated scene, prefab, transparent material, fallback texture, texture import settings, configuration, Build Settings, and Addressables source settings. It is safe to run repeatedly and preserves generated asset GUIDs and scene object identities. Setup intentionally restores the `Local` profile; select the Cloudflare workflow afterward when testing hosted content.

The equivalent batch command is:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine `
  -logFile Logs/Setup.log
```

Validate the generated result with **AddressablesSample > Game > Validate Generated Project** or:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.ProjectValidation.ValidateFromCommandLine `
  -logFile Logs/Validation.log
```

The generated `Assets/_Project/Scenes/Game.unity` scene is the sole enabled Build Settings scene.

## 5. Scene and controls

Open `Assets/_Project/Scenes/Game.unity` and enter Play Mode.

- Click the cube with the primary mouse button, or tap it on a touchscreen, to score and request the next image.
- Click or tap away from the cube to flash it red briefly. A miss does not change the score, texture, or active round request.
- Input is accepted only while the controller is in `Ready`; it is disabled during startup and round loading.

The target is instantiated once from the Addressable prefab. Round changes update its renderer through a `MaterialPropertyBlock`; the shared material is never instantiated or replaced.

## 6. State, status, score, and fallback behavior

The controller progresses through `Uninitialized`, startup fallback loading, startup prefab loading, round loading, `Ready`, and finally either `FatalError` or `Disposed`.

- Startup displays `Loading game...` and initializes the score to `Score: 0`.
- A round request displays `Loading image...` while retaining the currently displayed texture.
- A successful request applies the new texture, returns to `Tap the object!`, and releases the previous round owner.
- A failed round request applies the retained checkerboard fallback, reports one controlled warning, and returns to `Image failed - using fallback. Tap the object!`.
- A hit increments the score exactly once. A miss never increments it.
- Failure to load a required startup asset enters `Unable to start. See Console.` and performs full cleanup.

## 7. Use Asset Database local emulation

Addressables `4.0.1` no longer ships the legacy **Simulate Groups** play-mode script. Its supported local-emulation replacement is **Use Asset Database (fastest)**. This project configures that mode with a `0.25` second simulated load delay so loading is observable. Stale replacement is exercised separately by the delayed-owner PlayMode tests because normal player input is disabled while a round is loading.

Choose **AddressablesSample > Game > Addressables > Use Asset Database**, or run:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.UseAssetDatabaseFromCommandLine `
  -logFile Logs/UseAssetDatabase.log
```

This activates the `Local` profile and is the setup/default development workflow. It does not require a content build or server.

## 8. Fresh build and Use Existing Build

Choose **AddressablesSample > Game > Addressables > Build Local + Use Existing Build**, or run:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine `
  -logFile Logs/AddressablesBuild.log
```

The command restores the `Local` profile, selects the stock schema-driven builder, removes prior player content through the Addressables API, creates fresh catalogs and bundles, verifies the runtime settings/catalog output, and selects **Use Existing Build (requires built groups)**.

After testing built content, restore the committed defaults:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine `
  -logFile Logs/RestoreLocal.log
```

## 9. Automated validation

Run setup twice to verify idempotency, then run the validator:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine -logFile Logs/Setup-1.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine -logFile Logs/Setup-2.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.ProjectValidation.ValidateFromCommandLine -logFile Logs/Validation.log
```

Run EditMode tests:

```powershell
& $unityPath -batchmode -nographics -projectPath $projectPath `
  -runTests -testPlatform EditMode `
  -testResults Logs/EditMode.xml -logFile Logs/EditMode.log
```

Run PlayMode tests in Asset Database mode:

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

Expected results are 35 passing EditMode tests and 4 passing PlayMode tests in each Addressables mode, with zero failures or skips. XML results and full Editor logs are written under the ignored `Logs/` directory. The deliberate invalid-round test asserts its single expected warning; any other unexpected Unity exception/error during the test run fails the suite. Unity licensing-service startup diagnostics may still appear in a successful batch log and should be assessed separately from test output.

`RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable` still requires a valid startup catalog because the target prefab and fallback are genuine Addressables. If it reports `No Location found for Key=game/fallback`, the fallback logic has not failed: no catalog was loaded at all. Re-select **Use Asset Database**, or complete the Cloudflare build/deploy sequence below before rerunning it.

If PlayMode tests are launched in a standalone `PlayerWithTests`, build Addressables content first. The project deliberately uses **Do not Build Addressables content on Player Build** so a player/test build consumes the explicitly validated content instead of silently rebuilding a different remote catalog.

## 10. Architecture and ownership

`GameBootstrapper` is the scene composition root. `GameController` is a plain C# state machine that depends on small interfaces for loading, presentation, target creation, and diagnostics. Unity Addressables access is isolated behind `IAddressableAssetLoader`.

`AddressableLoad<T>` is the sole production owner of a raw `AsyncOperationHandle<T>` and the only production type that calls `Addressables.Release`. One owner represents one acquired handle reference and releases it exactly once. The fallback and prefab owners remain retained for the playable session. The displayed round owner remains retained until a replacement or fallback has been applied. Teardown disables and destroys the instantiated target before releasing the prefab, round, and fallback owners.

The implementation contains no `WaitForCompletion`, `Task.Wait`, synchronous `.Result`, or busy waiting. The only runtime `async void` method is the Unity `Start` lifecycle boundary.

## 11. Cancellation semantics

Addressables does not expose a reliable transport abort for `LoadAssetAsync`. Cancellation therefore means logical invalidation plus prompt ownership cleanup:

- every round receives a generation number and a distinct owner identity;
- starting a replacement round disposes the pending owner immediately;
- pending callbacks are unsubscribed before the handle is released;
- every continuation checks controller state, generation, owner identity, and target validity before mutating presentation;
- a stale completion can never replace the newer texture, change HUD state, or take ownership again.

This is intentionally not described as aborting an underlying download.

## 12. Cloudflare HTTPS remote workflow

The concrete `Cloudflare` profile points to:

```text
https://addressables-sample.pages.dev/[BuildTarget]
```

The target prefab and fallback remain local startup content. The remote catalog and each round texture bundle are written to `ServerData/[BuildTarget]`; round textures use **Pack Separately**, so each new round can make an independent HTTPS request.

After changing code, material settings, texture import settings, platform, or Addressables configuration, rebuild and republish in this exact order:

1. Run **AddressablesSample > Game > Setup Test Task**.
2. Run **AddressablesSample > Game > Addressables > Build Cloudflare Remote + Use Existing Build**.
3. Wait for `Cloudflare Addressables content is ready in ServerData` in the Console.
4. From the project root, publish the complete `ServerData` directory:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Tools\Publish-RemoteContent.ps1
```

The equivalent batch build command is:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildCloudflareAndUseExistingFromCommandLine `
  -logFile Logs/AddressablesCloudflareBuild.log
```

The publish script waits for Cloudflare and verifies every catalog, hash, and bundle URL returns HTTP 200. After it reports a successful deployment, keep **Use Existing Build** active, open `Assets/_Project/Scenes/Game.unity`, and enter Play Mode. Clear the Console first. A successful run downloads the catalog over HTTPS, loads the local prefab/fallback once, and downloads individual round bundles from Cloudflare. Check the Console or the Addressables Event Viewer if proof of the requests is needed.

If the correct remote content is already built in this project's `Library` and is already uploaded, **AddressablesSample > Game > Addressables > Use Uploaded Cloudflare Build** reactivates it without rebuilding or publishing. A freshly extracted checkout does not contain `Library`, so it must run the full build once even when an older deployment exists.

For another HTTPS endpoint, the generic command remains available:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.ConfigureRemoteFromCommandLine `
  -remoteBaseUrl 'https://cdn.example.com/addressablessample' `
  -logFile Logs/AddressablesRemoteBuild.log
```

### Testing fallback behavior

There are two levels of fallback validation:

1. **Deterministic automated test:** select a valid Addressables workflow and run `RealLoader_InvalidRoundKey_AppliesRealFallbackAndRemainsPlayable`. It uses the real Addressables loader with a deliberately absent round key, verifies the retained fallback, verifies that the target remains selectable, and verifies teardown ownership.
2. **Real HTTP 404 test:** exit Play Mode and run **AddressablesSample > Game > Addressables > Clear Download Cache**, then temporarily remove the `ant` bundle from `ServerData/StandaloneWindows64` and deploy that incomplete directory. Keep the catalog and hash unchanged. Start a fresh Play Mode session: the first round requests `ant`, receives a 404, applies `FallbackTexture`, shows `Image failed - using fallback. Tap the object!`, and remains playable. Restore the bundle to `ServerData`, deploy again immediately, and clear the cache before the final smoke test. Prefer doing this against a temporary Cloudflare Pages project because the public endpoint is intentionally incomplete during the probe.

Do not test by taking down the catalog itself. Without a catalog, the startup prefab and fallback keys cannot be resolved, so a startup fatal error is the correct result rather than a round-level fallback.

To return to the offline baseline, run **AddressablesSample > Game > Addressables > Restore Local Defaults**.

## 13. Known limitations and validation scope

- The author supplied the configured Cloudflare endpoint and previously uploaded content. Because this repair changes rendering/import configuration, rebuild, republish, and rerun the remote smoke test before final delivery.
- `ServerData`, the Wrangler publish script, and `.wrangler` project/account cache files are intentionally retained for this delivery. The cache contains account metadata but no authentication token was found; never add a Wrangler API token or environment-secret file to the archive.
- Automated input coverage uses virtual Input System mouse and touchscreen devices in editor-hosted PlayMode. Run the documented prebuild first if executing tests in `PlayerWithTests`.
- Addressables content is platform-specific. Rebuild and upload the matching `[BuildTarget]` directory before testing another platform.

## 14. Asset attribution

The ten animal PNGs in `Assets/Textures` were supplied with the test task, and their filenames use an `icons8-` prefix. The repository, requirements, and source PDF provide no verified creator, source URL, or license terms, so this README makes no additional attribution or redistribution-license claim. Confirm the original asset terms before redistributing them outside this submission.
