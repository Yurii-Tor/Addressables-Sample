AddressablesSample Test — Decision-Complete Implementation Plan
1. Summary and locked decisions
•
Retain Unity 6000.3.21f1, URP 17.3.0, Input System 1.20.0, UGUI 2.0.0, and Test Framework 1.6.0.
•
Add the currently missing com.unity.addressables package at exactly 4.0.1; Unity’s registry confirms this version supports Unity 6000.0+.
•
Generate a new Game.unity scene and leave SampleScene.unity untouched.
•
Use one Addressable cube prefab, one generated Addressable checkerboard fallback texture, and all ten supplied animal textures as deterministic round-robin round images.
•
Load the prefab once with Addressables.LoadAssetAsync<GameObject>, retain that handle, instantiate it once with Object.Instantiate, destroy the instance before releasing the prefab handle.
•
Accept player input only in Ready. Keep a reentrant internal BeginRound operation so tests and future callers can replace a pending request.
•
Preserve the current texture while a new round loads. Apply the replacement or fallback before releasing the formerly displayed texture.
•
Treat Addressables cancellation as request invalidation plus early handle release, not guaranteed transport abort. Addressables documents that pending loads cannot generally be canceled, but releasing their handle ensures the eventual result is released and cannot remain authoritative. Addressables 4.0 async guidance
•
Addressables 4.0.1 no longer contains the legacy Simulate Groups play-mode script. Validate its current replacement, Use Asset Database (fastest) with a configured simulated delay, plus Use Existing Build. Do not port or recreate the obsolete virtual-mode builder. Addressables 4.0 Groups window
•
Remote delivery remains optional. Prepare an inactive remote profile and validation workflow, but do not claim live remote validation without a supplied HTTPS endpoint.
•
Before batch Unity work, require the currently open interactive Unity editor to be closed; never terminate it automatically.
2. Requirements and acceptance criteria
Requirement
Implementation and acceptance
REQ-F01 Target object
After successful startup, exactly one visible cube exists. It comes from the Addressable prefab, contains one TargetView, MeshRenderer, and BoxCollider, and is the only object recognized by the selection raycast. No target is baked into Game.unity.
REQ-F02 Correct selection
One pointer press hitting the target while Ready increments score exactly once, disables further selection, enters LoadingRound, shows Loading image..., and starts exactly one new Addressables texture request. Only the latest valid completion may apply a texture and restore Ready.
REQ-F03 Incorrect selection
A miss while Ready leaves score, round index, current texture, and Addressables request count unchanged; it triggers one visible red flash and remains Ready.
REQ-F04 Status UI
Startup shows Loading game...; every round load shows Loading image...; normal readiness shows Tap the object!; fallback readiness shows Image failed - using fallback. Tap the object!; fatal startup shows Unable to start. See Console.
REQ-F05 Score UI
Initial text is Score: 0. Score increases by one immediately on each accepted target hit and never changes on misses, ignored input, load failure, cancellation, stale completion, or teardown.
REQ-F06 Error feedback
A miss flashes the cube red for a fixed short duration using the existing MaterialPropertyBlock. Repeated misses restart the flash cleanly. The texture property is preserved and no material instance is created.
REQ-A01 Addressable prefab
game/target is requested once per controller lifetime through LoadAssetAsync<GameObject>. Its successful operation owner remains retained while the ordinary instantiated target exists. Teardown disables and destroys the target before releasing the prefab operation exactly once.
REQ-A02 Addressable fallback
game/fallback is requested once, before the prefab. Its successful operation owner remains retained for the entire playable session and is released exactly once during fatal cleanup or teardown. It is never retried automatically.
REQ-A03 Round textures
The initial round and every accepted correct hit issue one asynchronous Texture2D load. Each successful displayed texture retains its operation owner until replacement, fallback, fatal cleanup, or teardown.
REQ-A04 Local baseline
Run the complete smoke flow in Addressables 4.0.1 Use Asset Database (fastest) with simulated latency. Then build fresh local content and repeat in Use Existing Build (requires built groups), without a server. README documents both.
REQ-A05 Load fallback
A current-generation round failure is caught, logged once as a controlled warning, and releases its failed handle. The retained fallback is applied before releasing any formerly displayed round texture. Play returns to Ready without an unhandled exception.
REQ-ASYNC01 Nonblocking
Controller flow awaits IAddressableLoad<T>.Completion. Production code contains no WaitForCompletion, Task.Wait, synchronous .Result, spin loop, or busy wait. Loading UI remains responsive.
REQ-ASYNC02 Cancellation
Starting generation B increments the generation and clears/disposes A before creating B. A releases its handle exactly once. Every continuation checks controller state, generation, and request identity before any texture, UI, score, or state mutation.
REQ-LIFE01 Ownership
Every raw handle returned to application code is immediately transferred into exactly one AddressableLoad<T> owner. A successful owner is held by one controller slot; failure/cancellation releases internally. No raw handle escapes the infrastructure class.
REQ-LIFE02 Teardown
Teardown is synchronous and idempotent: mark disposed, invalidate generations, stop input/feedback, release pending load, clear/destroy target, release current round, fallback, then prefab. Late continuations cannot mutate Unity objects or release twice.
REQ-Q01 Code quality
GameBootstrapper is the composition root; GameController is plain C#; Addressables are behind interfaces; Unity-facing views contain presentation/input behavior only; expected failures are explicit; normal operation has no unexpected Console errors.
REQ-OPT01 Remote delivery
Create a non-active RemoteTemplate profile and exact configuration/build instructions. If an HTTPS base URL is later supplied, host catalog and bundles, run the smoke flow, then deliberately make one round asset unavailable and verify fallback. Otherwise record “not validated; no endpoint supplied.”
Architecture constraints
Add no third-party runtime dependency, DI container, or UniTask. Use MaterialPropertyBlock. Restrict async void to GameBootstrapper.Start, the Unity lifecycle boundary.
Generated assets
Scene, prefab, material, fallback, config, Addressables settings/groups/profiles, Build Settings, and metas are created or modified only through Unity Editor APIs. A second setup run produces no duplicate or semantic changes.
Definition of done
All checks above pass, EditMode and PlayMode tests pass in both local modes, Addressables ownership returns to baseline after scene teardown, README is complete, LFS is valid, and all unvalidated items are explicitly reported.
3. Project structure, assemblies, and APIs
Assets/_Project/
├── Runtime/
│   ├── Core/               GameController, state/result types, selector, interfaces
│   ├── Addressables/       Unity loader and the sole raw-handle owner
│   ├── Presentation/       Bootstrapper, HUD, target, input/raycast, target factory
│   ├── Config/             GameConfig type
│   └── AddressablesSample.Game.Runtime.asmdef
├── Editor/
│   ├── TestTaskSetup
│   ├── ProjectValidation
│   ├── AddressablesWorkflow
│   └── AddressablesSample.Game.Editor.asmdef
├── Tests/
│   ├── EditMode/
│   └── PlayMode/
├── Config/GameConfig.asset
├── Materials/Target.mat
├── Prefabs/Target.prefab
├── Scenes/Game.unity
└── Textures/FallbackTexture.asset

Assets/AddressableAssetsData/       Generated/maintained by Addressables APIs
Docs/ImplementationPlan.md          Living execution record
README.md                           Final setup and validation guide
Assemblies:
•
AddressablesSample.Game.Runtime: references Unity.Addressables, Unity.ResourceManager, Unity.InputSystem, and Unity.UGUI.
•
AddressablesSample.Game.Editor: Editor-only; references Runtime and Unity.Addressables.Editor.
•
AddressablesSample.Game.EditModeTests: Editor test assembly referencing Runtime and Editor.
•
AddressablesSample.Game.PlayModeTests: Editor-hosted PlayMode test assembly referencing Runtime, Addressables, and Input System.
•
Leave template Assembly-CSharp and Assembly-CSharp-Editor content untouched.
Core public/internal contracts:
public enum GameState
{
    Uninitialized,
    LoadingStartupFallback,
    LoadingStartupPrefab,
    LoadingRound,
    Ready,
    FatalError,
    Disposed
}

public interface IAddressableAssetLoader
{
    IAddressableLoad<T> StartLoad<T>(object runtimeKey)
        where T : UnityEngine.Object;
}

public interface IAddressableLoad<T> : IDisposable
    where T : UnityEngine.Object
{
    Task<AddressableLoadResult<T>> Completion { get; }
}

public readonly struct AddressableLoadResult<T>
{
    public AddressableLoadStatus Status { get; } // Succeeded, Failed, Canceled
    public T Asset { get; }                      // borrowed while owner remains live
    public Exception Exception { get; }
}

public sealed class GameController : IDisposable
{
    public GameState State { get; }
    public int Score { get; }
    public Task InitializeAsync();
    public void HandleSelection(bool hitTarget);
    internal void BeginRound(); // reentrant replacement seam
}
Supporting responsibilities:
•
GameConfig: direct scene reference containing AssetReferenceGameObject TargetPrefab, AssetReferenceTexture2D FallbackTexture, and an ordered array of ten unique round AssetReferenceTexture2D values. It validates non-null references, at least two unique rounds, and exclusion of the fallback from the round list.
•
RoundTextureSelector: deterministic round-robin; advances when a request starts, wraps after the tenth texture, and never performs an Addressables location query.
•
AddressableLoad<T>: only class storing an AsyncOperationHandle<T> and only class calling Addressables.Release(handle).
•
IGameHud: sets status and score.
•
ITargetView: applies texture, enables interaction, flashes red, stops feedback, clears its property block, and exposes no Addressables ownership.
•
ITargetFactory: instantiates and validates one target from the retained prefab asset and destroys it during teardown.
•
IGameDiagnostics: routes controlled warnings/errors to Unity logging and permits assertions in tests.
•
GameBootstrapper: creates concrete services, constructs GameController, subscribes selection input, calls InitializeAsync from async void Start, and disposes synchronously from OnDestroy.
•
PointerSelectionInput: creates runtime Input System actions for <Pointer>/press and pointer position, raycasts from the configured camera, and reports hit/miss. It performs no game-state changes.
•
HudView: owns legacy UGUI Text references.
•
TargetView: owns the renderer, collider, one persistent MaterialPropertyBlock, _BaseMap and _BaseColor IDs, and the miss-flash coroutine.
•
UnityTargetFactory: instantiates the prefab normally, immediately disables its renderer/collider, validates required components, applies fallback, then reveals it.
4. Complete game state machine
Current state
Event
Actions
Next state
Uninitialized
InitializeAsync
Validate config, reset score/UI, start exactly one fallback load
LoadingStartupFallback
Uninitialized
Invalid config
Log error; make UI noninteractive; issue no loads
FatalError
LoadingStartupFallback
Success/current
Retain fallback owner; start exactly one prefab load
LoadingStartupPrefab
LoadingStartupFallback
Failure
Failed owner releases; show fatal status
FatalError
LoadingStartupPrefab
Success/current
Retain prefab owner; instantiate/validate target; apply fallback while hidden; reveal target; begin initial round
LoadingRound
LoadingStartupPrefab
Failure
Failed prefab owner releases; release fallback; show fatal status
FatalError
LoadingStartupPrefab
Instance/validation failure
Disable/destroy partial instance; release prefab then fallback; log error
FatalError
LoadingRound
Current success
Apply candidate, promote its owner to current, release previous current after the swap, enable target, show ready
Ready
LoadingRound
Current failure
Failed owner is already released; apply fallback, release previous current afterward, log warning, enable target
Ready
LoadingRound
Replacement request
Increment generation; clear/dispose old pending request; create/install new pending request
LoadingRound
LoadingRound
Player press
Ignore completely; no score, feedback, request, or state change
LoadingRound
Ready
Hit
Increment score once; reset feedback; disable interaction; begin next round
LoadingRound
Ready
Miss
Keep score/image/request count; restart red flash
Ready
FatalError
Any input/load continuation
Ignore
FatalError
Any active state
Unexpected texture/view exception
Disable/clear target, release all owners, log error
FatalError
Any non-disposed state
Teardown
Mark disposed first, invalidate all generations, release in prescribed order
Disposed
Disposed
Any call/continuation/repeated teardown
No-op
Disposed
The mandatory completion gate, applied equally to success, failure, and cancellation, is:
state is active
AND controller is not disposed/fatal
AND captured generation equals current generation
AND operation identity equals _pendingRound
AND target is still valid
5. Addressables ownership and exact-once release proof
AddressableLoad<T> owns this internal state machine:
Pending, owns handle
├─ success ────────────────> SucceededRetained, owns handle
├─ failure ──ReleaseOnce──> Released
└─ Dispose ──ReleaseOnce──> Released

SucceededRetained, owns handle
└─ Dispose ──ReleaseOnce──> Released

Released
└─ callback/Dispose ──────> Released, no-op
Implementation rules:
•
StartLoad calls Addressables.LoadAssetAsync<T>(runtimeKey) and transfers the returned handle immediately into AddressableLoad<T>. If owner construction throws, the local method releases its still-local handle.
•
Completion is bridged to a Task<AddressableLoadResult<T>>; controller logic uses await.
•
A pending Dispose unsubscribes the completion callback, marks the result canceled, and calls ReleaseOnce. It never reads the now-invalid handle again.
•
Failure captures OperationException before release, calls ReleaseOnce, then returns Failed.
•
Success returns the borrowed asset but retains the handle until the owner is disposed.
•
ReleaseOnce has a one-way guard. All creation, callback, cancellation, and disposal actions occur on the Unity main thread.
•
No direct AssetReference.LoadAssetAsync() is used; the loader passes AssetReference.RuntimeKey to Addressables.LoadAssetAsync<T> so overlapping references remain legal.
•
No explicit InitializeAsync or LoadResourceLocationsAsync calls are added, avoiding additional application-owned handles.
Controller owner slots:
_fallbackLoad     fallback owner, pending then session-retained
_prefabLoad       prefab owner, pending then session-retained
_pendingRound     zero or one pending/current-completion owner
_currentRound     zero or one displayed round owner
_targetInstance   ordinary Unity instance; no Addressables instance handle
_roundGeneration  incremented for every request and teardown
Exact terminal paths:
Path
Exact terminal behavior
Fallback succeeds
Transfer owner to _fallbackLoad; release once during fatal cleanup or teardown.
Fallback fails
Owner captures error and releases once; prefab/round are never requested.
Teardown while fallback pending
Dispose pending owner once; canceled continuation sees Disposed.
Prefab succeeds
Transfer owner to _prefabLoad; retain until target is disabled/destroyed.
Prefab fails
Failed owner releases once; retained fallback releases once.
Teardown after prefab success but before continuation
Field owner releases once; lifetime check prevents instantiation.
Target construction fails
Destroy partial ordinary instance; release prefab and fallback once each.
Current round succeeds
Candidate remains owned locally/pending while applied; transfer it to _currentRound; then dispose previous current once.
Current round fails
Failed request releases once internally; apply retained fallback; then dispose previous current once.
Pending A replaced by B
Increment generation and clear A’s slot before disposing A once; A’s canceled/stale continuation changes nothing.
A completed successfully just before replacement
A still resides in the pending slot; replacement disposes it once. Its continuation fails identity/generation checks and does not use the borrowed asset.
Stale A failure after B
A is already released; it cannot apply fallback, alter status, or log a current-round warning.
A → B → C out of order
A and B release once each; only C passes generation and identity checks.
B succeeds after A was displayed
Apply B before releasing A, so every applied texture always has a live owner.
B fails while A is displayed
Apply fallback before releasing A.
Texture application throws
Candidate releases once; fatal cleanup disables/clears target, releases prior current, fallback, and prefab once each.
Teardown with pending and current
Pending releases once; target is disabled/cleared/destroyed; current, fallback, and prefab release once each.
Repeated teardown/dispose
State and release guards make all later calls no-ops.
Formal invariant: every application-created raw handle is contained in one owner; every owning state has one terminal transition through ReleaseOnce; a successful owner is always in a controller field or guarded local until transfer. Therefore every returned handle receives exactly one application release call on success, failure, cancellation, replacement, fatal cleanup, or teardown.
Teardown order:
1.
Set Disposed and increment generation.
2.
Unsubscribe and disable selection input.
3.
Clear and dispose _pendingRound.
4.
Stop target feedback; disable collider and renderer; clear its property block.
5.
Destroy the ordinary target instance.
6.
Clear and dispose _currentRound.
7.
Clear and dispose _fallbackLoad.
8.
Clear and dispose _prefabLoad.
9.
Clear borrowed asset/view references; never touch HUD afterward.
6. Generated Unity and Addressables content
Scene and prefab:
•
Game.unity contains only the generated camera, directional light, canvas/HUD, spawn transform, input bridge, and GameBootstrapper; it contains no target.
•
Camera is centered on a spawn point at the origin; cube framing is deterministic across common aspect ratios.
•
Target.prefab contains a cube mesh, MeshRenderer, shared Target.mat, BoxCollider, and TargetView.
•
Target.mat uses Universal Render Pipeline/Lit; all runtime texture/color changes use MaterialPropertyBlock, never renderer.material.
•
Generated fallback is a conspicuous magenta/black checkerboard Texture2D asset distinct from all rounds.
•
The target is instantiated only after fallback and prefab both succeed, hidden while fallback is applied, then visible throughout round loading.
•
Game.unity becomes the sole enabled Build Settings scene; SampleScene.unity remains on disk and unchanged.
Round catalog order:
1.
ant
2.
budgie
3.
bull
4.
crab
5.
cute-hamster
6.
dolphin
7.
dragon
8.
fish
9.
jellyfish
10.
phoenix
Addressables entries:
Group
Address
Label
Packing/path
Game-Startup
game/target
none
Packed Together, LZ4, Local.BuildPath / Local.LoadPath
Game-Startup
game/fallback
none
Same startup bundle
Game-RoundTextures
game/round/<stem>
round-texture
Packed Separately, LZ4, Round.BuildPath / Round.LoadPath
Profiles:
•
Local is active by default.
◦
Round.BuildPath = [UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]
◦
Round.LoadPath = {UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]
•
RemoteTemplate is inactive.
◦
Round.BuildPath = ServerData/[BuildTarget]
◦
Round.LoadPath = https://YOUR_HOST/[BuildTarget]
•
Remote catalog building is disabled in the local baseline.
•
AddressableAssetSettings.SimulatedLoadDelay is set to a visible but short value, such as 0.25s, for Use Asset Database.
•
Final committed play-mode builder is Use Asset Database (fastest) and final active profile is Local.
Editor commands:
•
AddressablesSample/Game/Setup Test Task
•
AddressablesSample/Game/Validate Generated Project
•
Batch entry points:
◦
AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine
◦
AddressablesSample.Game.Editor.ProjectValidation.ValidateFromCommandLine
◦
AddressablesSample.Game.Editor.AddressablesWorkflow.UseAssetDatabaseFromCommandLine
◦
AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine
◦
AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine
◦
AddressablesSample.Game.Editor.AddressablesWorkflow.ConfigureRemoteFromCommandLine
Idempotency rules:
•
Resolve assets by stable path and Addressables entries by GUID.
•
Load and update existing material, fallback, config, scene, and prefab instead of recreating their files.
•
Reconcile tool-owned roots/components and remove only duplicates owned by the setup tool.
•
Reconcile exact task groups/profiles/addresses/labels while leaving unrelated groups and profiles untouched.
•
The tool owns membership of Game-Startup and Game-RoundTextures; unexpected entries in those groups are removed, but no other group is altered.
•
Use AssetDatabase, Texture2D asset APIs, PrefabUtility, EditorSceneManager, EditorBuildSettings, SerializedObject, and Addressables Editor APIs. Never write Unity YAML or .meta files manually.
•
Run setup twice. The second run must retain asset GUIDs, scene object identities, one entry per GUID, one profile per name, and produce no owned-file diff.
•
Validation fails on missing scripts/references, duplicate objects/groups/profiles, invalid config, scene-baked target, wrong addresses/labels, wrong schemas/paths, or anything other than one enabled game scene.
7. Test and validation plan
EditMode
Use a manually completable fake loader whose operations record request key, completion status, dispose count, and simulated underlying release count, plus fake HUD/target/factory/diagnostics event logs.
Required tests:
•
Config rejects null, empty, duplicate, fallback-as-round, or fewer-than-two round references.
•
Selector follows the fixed order, advances on request, and wraps.
•
Initialization requests fallback, prefab, and initial round in sequence; startup assets are each requested once.
•
Initial score/status values and all legal state transitions.
•
Correct selection increments once and requests exactly one round.
•
Miss preserves score, texture identity, selector position, and request count while invoking feedback once.
•
Input is ignored during startup, loading, fatal, and disposed states.
•
Successful swap applies the candidate before releasing the previous owner.
•
Current failure applies fallback before releasing the previous current.
•
Pending replacement releases the old operation once.
•
Stale success and stale failure cannot mutate texture, UI, score, or state.
•
A → B → C out-of-order completion applies only C and releases A/B once each.
•
Fallback failure, prefab failure, target-factory failure, and texture-application exception leak no owner.
•
Teardown while fallback pending, prefab pending, initial round pending, replacement pending, and ready/current.
•
Success-completion versus replacement and success-completion versus teardown races.
•
Double teardown and repeated operation disposal do not increase release counts.
•
Raw owner tests cover success-retained, failure, pending cancel, callback-after-dispose, and repeated dispose.
•
Source audit finds no forbidden synchronous waits and no Addressables.Release outside AddressableLoad<T>.
•
Generated-project validator passes after two setup runs.
PlayMode
•
Load Game.unity with real Addressables; reach Ready; assert score zero, correct status, and exactly one target.
•
Verify the target’s property block has a loaded texture and its shared material reference never changes.
•
Drive a virtual mouse to the target center and press: score increments, loading appears, next texture is applied, ready returns.
•
Drive a touch press over the target where supported by the Input System test API.
•
Press empty screen: unchanged score/texture and visible red flash.
•
Construct a real-loader controller with an invalid round key: controlled failure applies the real fallback and play continues.
•
Use delayed fake operations in PlayMode to complete A after B and prove only B applies.
•
Destroy the bootstrapper during a delayed request, complete it later, and assert no Unity mutation and exact release counts.
•
Unload the game scene after several rounds and confirm all task-owned operations report released/zero active owners.
•
Use LogAssert so cancellation and expected round failure are accepted, while all unexpected exceptions/errors fail the suite.
Mandatory workflow runs
Run only after the interactive editor is closed.
$unityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
1.
Setup and idempotency:
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine -logFile Logs/Setup-1.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.TestTaskSetup.RunFromCommandLine -logFile Logs/Setup-2.log
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.ProjectValidation.ValidateFromCommandLine -logFile Logs/Validation.log
2.
EditMode:
& $unityPath -batchmode -nographics -projectPath $projectPath -runTests -testPlatform EditMode -testResults Logs/EditMode.xml -logFile Logs/EditMode.log
3.
Addressables 4.0.1 local emulation and PlayMode:
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.UseAssetDatabaseFromCommandLine -logFile Logs/UseAssetDatabase.log
& $unityPath -batchmode -nographics -projectPath $projectPath -runTests -testPlatform PlayMode -testResults Logs/PlayMode-AssetDatabase.xml -logFile Logs/PlayMode-AssetDatabase.log
4.
Fresh local build and Use Existing Build:
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.BuildLocalAndUseExistingFromCommandLine -logFile Logs/AddressablesBuild.log
& $unityPath -batchmode -nographics -projectPath $projectPath -runTests -testPlatform PlayMode -testResults Logs/PlayMode-ExistingBuild.xml -logFile Logs/PlayMode-ExistingBuild.log
5.
Restore committed defaults:
& $unityPath -batchmode -nographics -quit -projectPath $projectPath -executeMethod AddressablesSample.Game.Editor.AddressablesWorkflow.RestoreLocalDefaultsFromCommandLine -logFile Logs/RestoreLocal.log
Inspect every log for compiler errors, unhandled exceptions, invalid/double handles, leaked owners, missing catalog/bundle messages, and unexpected test logs.
Optional remote validation
ConfigureRemoteFromCommandLine reads an explicit -remoteBaseUrl https://host/path argument, rejects non-HTTPS or placeholder URLs, activates RemoteTemplate, maps the remote catalog to the Round path pair, and enables remote catalog building.
If a host is supplied:
1.
Configure the URL and build content.
2.
Upload the complete ServerData/<BuildTarget> output.
3.
Confirm catalog, hash, and bundles return HTTPS 200 responses.
4.
Run in Use Existing Build and complete multiple rounds.
5.
Make one round bundle unavailable or configure one invalid round address and confirm fallback.
6.
Restore Local, disable remote catalog building, and rebuild local content.
Without a host, do not run this path and record it as intentionally unvalidated.
8. Git milestones and commit boundaries
At every milestone: update Docs/ImplementationPlan.md, inspect status/diff, compile, run relevant tests, inspect logs, stage explicit paths only, inspect the staged diff, and commit only after validation passes.
1.
chore: establish implementation baseline
◦
Adopt the related task inputs: AGENTS.md, source requirements/PDF, and Docs/ImplementationPlan.md.
◦
Add Addressables 4.0.1 to the manifest and allow Unity to update the package lock.
◦
Validate package resolution and clean compilation.
2.
feat: add gameplay core and owned Addressables loading
◦
Add Runtime assembly, controller/state machine, config type, loader/owner, selector, interfaces, and EditMode fakes/tests.
◦
Run complete EditMode tests and the banned-wait/raw-release audit.
3.
feat: generate game scene and Addressables content
◦
Add Editor tooling and generated scene/prefab/material/fallback/config/Addressables settings.
◦
Adopt the ten supplied textures and metas as round content.
◦
Update Build Settings through the tool.
◦
Run setup twice, project validation, compile, and EditMode tests.
4.
test: cover gameplay and Addressables workflows
◦
Add PlayMode tests and workflow automation.
◦
Pass PlayMode in Use Asset Database and a fresh Use Existing Build.
◦
Restore Local/Use Asset Database defaults before committing.
5.
docs: add setup, testing, and submission guide
◦
Add final README and complete the implementation plan’s outcome/validation records.
◦
Repeat the full validation matrix and final ownership audit.
Git safeguards:
•
Never use git add .; stage explicit pathspecs.
•
Before each commit inspect git diff --cached --name-only and git diff --cached.
•
Do not stage Library, Temp, Logs, obj, build outputs, .idea, local AI Assistant settings, or generated Addressables content.
•
Preserve the pre-existing deletions/changes to template editor files, URP settings, cloud project settings, and the LFS-normalization-only URP.png change.
•
PNG/PDF inputs are LFS-managed; finish with git lfs status and git lfs fsck.
•
Do not amend, reset, rebase, push, or alter branches.
9. Exact README and final submission checks
README sections:
1.
Project summary and implemented requirements.
2.
Exact Unity/package versions.
3.
Prerequisites: Unity 6000.3.21f1, Git LFS, and closing other editors before batch runs.
4.
One-command Editor setup plus batch setup command.
5.
Generated scene and controls: mouse/touch target selection.
6.
State/status/score/fallback behavior.
7.
Use Asset Database local-emulation steps, including why it replaces legacy Simulate Groups in Addressables 4.0.1.
8.
Fresh Addressables content build and Use Existing Build steps.
9.
EditMode and PlayMode commands, result/log locations, and expected success.
10.
Architecture and explicit handle-ownership summary.
11.
Cancellation semantics: invalidation and pending-handle release, without claiming transport abort.
12.
Optional HTTPS remote profile/build/upload/validation instructions.
13.
Known limitations and any validation that could not be performed.
14.
Asset attribution note for the supplied animal icons, using only provenance/license information actually supplied or verified.
Final checks:
•
Setup command succeeds twice without duplicates or a second-run owned-file diff.
•
Generated-project validator succeeds.
•
Project compiles with no errors.
•
EditMode passes.
•
PlayMode passes in Use Asset Database with simulated delay.
•
A fresh Addressables content build succeeds.
•
PlayMode passes in Use Existing Build without a server.
•
Final state is Local profile plus Use Asset Database.
•
Correct hit, miss, round failure, replacement, stale completion, and teardown behavior are demonstrated.
•
Every fake and concrete task-owned operation reaches one release; no invalid/double-handle warnings occur.
•
No WaitForCompletion, Task.Wait, synchronous .Result, material instantiation, or raw releases outside the owner.
•
Console/logs contain no unexpected exceptions or errors.
•
git diff --check, git lfs status, and git lfs fsck succeed.
•
No caches, logs, Addressables build outputs, credentials, active placeholder URL, or unrelated dirty files are staged.
•
Final report lists the local commits, test results, both validated local workflows, pre-existing untouched changes, and remote delivery as either validated with its URL or not validated because no endpoint was supplied.

10. Living execution record

Progress

•
2026-08-12 — Pre-implementation audit completed. AGENTS.md, Docs/Requirements.md, this plan, the authoritative PDF, Git status/history, project version, package manifest/lock, repository assets, and Git LFS state were inspected. The working tree and index were clean at a3cab8b on main before implementation. The PDF and normalized requirements are consistent; no architecture change is required.
•
2026-08-12 — Milestone 1, “chore: establish implementation baseline,” completed. Addressables 4.0.1 resolved with its lock-file dependencies and the project completed a clean Unity batch compile. The manifest change is preserved in the user-created local commit fcbfc31; the resolved package lock and this execution record are committed separately without rewriting history.
•
2026-08-12 — Milestone 2, “feat: add gameplay core and owned Addressables loading,” completed. The runtime assembly now contains the plain-C# controller/state machine, validated config projection, deterministic selector, Addressables abstraction, sole raw-handle owner, and presentation contracts. EditMode fakes and tests cover startup, hit/miss, failure, replacement, stale completion, teardown, and exact-once release paths.

Decisions

•
2026-08-12 — Addressables 4.0.1 provides Use Asset Database (fastest), not the legacy Simulate Groups play-mode script named generically in AGENTS.md and the PDF. The mandatory local-emulation validation will therefore use Use Asset Database with simulated latency, plus Use Existing Build, exactly as approved in this plan.
•
2026-08-12 — The Git safeguard against committing “generated Addressables content” is interpreted as excluding built catalogs, bundles, ServerData, and transient build output. Assets/AddressableAssetsData settings are milestone-3 source assets and will be committed because the approved deliverable explicitly requires them.
•
2026-08-12 — Milestone 2 will run all then-available core/ownership EditMode tests. Generated-project validation will be added and run in milestone 3, when its tooling and assets exist. Addressables workflow switching/build automation remains a milestone-4 deliverable.
•
2026-08-12 — The installed UGUI 2.0.0 runtime assembly is named UnityEngine.UI, not the Unity.UGUI label written in the plan. The Runtime asmdef uses the installed assembly name; this is a compile-level naming correction and does not change the approved architecture.
•
2026-08-12 — AddressableLoad<T> owns both raw handle acquisition and construction-failure release so every production Addressables.Release call remains in that one owner class. Its typed Completed callback is unsubscribed before pending release, and Task continuations run asynchronously to make field-clear-before-dispose race handling deterministic.

Validation

•
2026-08-12 — Baseline confirmed: Unity 6000.3.21f1; URP 17.3.0; Input System 1.20.0; UGUI 2.0.0; Test Framework 1.6.0; Addressables absent as expected before milestone 1. git lfs status was clean and git lfs fsck passed.
•
2026-08-12 — Milestone 1 package resolution confirmed com.unity.addressables 4.0.1, com.unity.scriptablebuildpipeline 4.0.0, and com.unity.profiling.core 1.0.3 in Packages/packages-lock.json. Logs/Milestone1Compile.log records successful script compilation, batch-mode shutdown, and process return code 0; targeted compiler/package/error scans found no failures.
•
2026-08-12 — Milestone 2 clean compilation passed in Logs/Milestone2Compile-4.log. The final EditMode run passed 34/34 tests with zero failures or skips in Logs/Milestone2-EditMode-4.xml. Source audits found no WaitForCompletion, Task.Wait, synchronous Completion.Result, runtime async void, material instantiation, or raw Addressables release outside UnityAddressableAssetLoader.cs. Targeted log scans found no compiler error, unhandled exception, invalid/double-handle, or test-failure signature.

Blockers

•
2026-08-12 — Resolved: the interactive Unity editor was closed before batch validation. Three unrelated Unity-generated settings changes remain unstaged and will be preserved outside task commits: Assets/Settings/Mobile_RPAsset.asset, ProjectSettings/EditorBuildSettings.asset, and ProjectSettings/ProjectSettings.asset.
•
2026-08-12 — Optional live HTTPS delivery is intentionally unvalidated unless an actual endpoint is supplied; this does not block the mandatory local implementation.
