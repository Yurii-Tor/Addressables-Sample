# AddressablesSample Addressables Test Task

## 1. Project summary

This Unity project implements the supplied one-scene selection game. It loads a target prefab, a retained fallback texture, and an ordered set of ten round textures through Addressables. Clicking or tapping the visible target increments the score and begins the next asynchronous texture request. Missing a target gives brief red feedback without changing the score or starting a round. Failed round loads apply the retained fallback and leave the game playable.

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

In the Editor, use **AddressablesSample > Game > Setup Test Task**. This one-click setup creates or reconciles the generated scene, prefab, material, fallback texture, configuration, Build Settings, and Addressables source settings. It is safe to run repeatedly and preserves generated asset GUIDs and scene object identities.

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

This activates the `Local` profile and is the committed/default development workflow. It does not require a content build or server.

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

## 12. Optional HTTPS remote workflow

Remote delivery is optional. Supply a real public HTTPS base URL with no credentials, query, fragment, placeholder, or `[BuildTarget]` segment:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath `
  -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.ConfigureRemoteFromCommandLine `
  -remoteBaseUrl 'https://cdn.example.com/addressablessample' `
  -logFile Logs/AddressablesRemoteBuild.log
```

The command activates `RemoteTemplate`, maps the round-group and remote-catalog build paths to `ServerData/[BuildTarget]`, maps their load paths to the supplied HTTPS URL, performs a fresh build, and selects Use Existing Build. Upload the complete generated `ServerData/<BuildTarget>` contents to the matching HTTPS path while preserving filenames. Confirm the catalog, hash, and bundles return HTTP 200 and run multiple rounds. To verify fallback without a cache false-positive, make an as-yet-unrequested round bundle unavailable before its first load, or clear the Addressables/bundle cache before the failure run.

Restore local content and defaults afterward:

```powershell
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine -logFile Logs/AddressablesBuild-LocalRestore.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine -logFile Logs/RestoreLocal.log
```

Do not commit hosted content, build outputs, URLs containing secrets, or credentials.

## 13. Known limitations and validation scope

- Live remote HTTPS delivery was not validated because no endpoint was supplied. The guarded workflow is implemented and its inactive `RemoteTemplate` baseline is structurally validated, but a configured remote build, upload, and load were not executed.
- Automated input coverage uses virtual Input System mouse and touchscreen devices in editor-hosted PlayMode. Physical-device input and a standalone player build were not part of the supplied mandatory scope.
- Local validation was performed on Windows with the exact Unity version listed above. Addressables content must be rebuilt for another active build target.

## 14. Asset attribution

The ten animal PNGs in `Assets/Textures` were supplied with the test task, and their filenames use an `icons8-` prefix. The repository, requirements, and source PDF provide no verified creator, source URL, or license terms, so this README makes no additional attribution or redistribution-license claim. Confirm the original asset terms before redistributing them outside this submission.
