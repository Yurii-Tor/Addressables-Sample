# Test Task Requirements

## 1. Source of Truth

The authoritative source for this project is:

`Docs/TestTask.pdf`

This document is a normalized and traceable summary of the PDF. If this
document conflicts with the original PDF, the PDF takes precedence.

## 2. Project Goal

Develop a Unity game scene containing one selectable 3D object.

At the beginning of every round, an image must be loaded asynchronously through
Unity Addressables and applied to the object as its texture.

The player must be able to select the object by tapping or clicking it.

## 3. Functional Requirements

### REQ-F01 — Target Object

The game scene must display exactly one selectable target object.

The target can be a sphere or a cube.

Acceptance criteria:

- One target object is present during gameplay.
- The object is visible to the player.
- The object can be detected by the selection system.

### REQ-F02 — Correct Selection

A tap or click that hits the target object must count as a correct selection.

Acceptance criteria:

- The score increases by one.
- A new round begins.
- A new image is requested asynchronously.
- The status UI changes to the loading state.
- After successful loading, the new image is applied to the target.

### REQ-F03 — Incorrect Selection

A tap or click that misses the target object must count as an incorrect
selection.

Acceptance criteria:

- The score does not change.
- The currently displayed image does not change.
- No new round image is requested.
- A negative visual effect or error message is displayed.

### REQ-F04 — Status UI

The UI must display the current game status.

Required states include:

- loading an image;
- waiting for the player to tap the object.

Acceptance criteria:

- A loading message is visible while a round image is loading.
- A ready message is visible when the target can be selected.

Example text:

- `Loading image...`
- `Tap the object!`

### REQ-F05 — Score UI

The UI must display the current score.

Acceptance criteria:

- The initial score is zero.
- The score increases only after a correct selection.
- Incorrect selections do not change the score.

### REQ-F06 — Error Feedback

An incorrect selection must produce negative feedback.

The feedback can be:

- a red flash on the target;
- an error text message;
- another clearly visible negative effect.

Acceptance criteria:

- The feedback is visible after a missed tap.
- The target retains its current image.
- The feedback does not start a new round.

## 4. Addressables Requirements

### REQ-A01 — Addressable Target Prefab

The target object must be loaded from an Addressable prefab when the game
starts.

Acceptance criteria:

- The target prefab is marked as Addressable.
- The prefab is loaded through the Addressables API.
- The prefab is loaded exactly once during startup.
- The loaded prefab handle remains valid while required.
- The prefab handle is released during teardown.

### REQ-A02 — Addressable Fallback Texture

The fallback texture must be an Addressable asset.

Acceptance criteria:

- The fallback texture is marked as Addressable.
- It is loaded exactly once during startup.
- It remains available throughout the game session.
- Its Addressables handle is released during teardown.

### REQ-A03 — Round Image Loading

Every round image must be loaded at runtime through Addressables.

Acceptance criteria:

- A new image is requested after every correct selection.
- Loading is asynchronous.
- The loaded texture is applied only after the operation succeeds.
- The Addressables lifecycle of every loaded image is managed explicitly.

### REQ-A04 — Local Addressables Baseline

The mandatory baseline must work using an Addressables local development
workflow.

Supported baseline workflows include:

- Simulate Groups;
- Use Existing Build.

Acceptance criteria:

- The project works in at least one documented local Addressables workflow.
- The README explains how to select and run that workflow.
- The Addressables content can be built and loaded without a remote server.

### REQ-A05 — Load Failure Fallback

If a round image fails to load, the Addressable fallback texture must be
applied.

Acceptance criteria:

- The target is never intentionally left without a texture.
- A failed round image does not produce an unhandled exception.
- The failed operation is cleaned up correctly.
- The fallback texture is displayed after the failure.

## 5. Asynchronous Execution Requirements

### REQ-ASYNC01 — Non-Blocking Loading

Addressables loading must use `async` and `await`.

Acceptance criteria:

- The main thread is not synchronously blocked.
- The implementation does not use `WaitForCompletion()`.
- The implementation does not use `Task.Wait()`.
- The implementation does not access `.Result` to synchronously wait.
- The UI and Unity player remain responsive during loading.

### REQ-ASYNC02 — Previous Request Cancellation

If a newer round starts while an older image request is still pending, the
older request must be canceled or invalidated.

Acceptance criteria:

- Starting request B invalidates pending request A.
- A late completion of request A cannot overwrite the result of request B.
- Canceled operations are cleaned up correctly.
- Cancellation does not produce an unhandled exception.
- The latest valid round is the only round allowed to update the target.

## 6. Resource Lifecycle Requirements

### REQ-LIFE01 — Explicit Ownership

Every Addressables operation must have one clearly identifiable owner.

Acceptance criteria:

- Startup assets are retained until teardown.
- A round texture remains loaded while the target uses it.
- Replaced textures are released when they are no longer used.
- Failed and canceled operations are cleaned up.
- No operation is released more than once.

### REQ-LIFE02 — Teardown

All retained Addressables resources must be released when the game controller
or scene is destroyed.

Acceptance criteria:

- The startup prefab handle is released.
- The fallback texture handle is released.
- The current round texture is released.
- Any pending round load is canceled or invalidated.
- Teardown does not produce double-release errors.

## 7. Code Quality Requirements

### REQ-Q01 — Clean Implementation

The code must be readable, maintainable, and appropriately structured for the
size of the task.

Acceptance criteria:

- Responsibilities are clearly separated.
- Async errors and cancellation are handled explicitly.
- Resource ownership can be understood from the code.
- The implementation avoids unnecessary complexity.
- Public and serialized fields have clear purposes.
- The Unity Console contains no unhandled exceptions during the normal flow.

## 8. Optional Requirement

### REQ-OPT01 — Real Network Delivery

Real network delivery is optional but recommended.

The Addressables catalog and bundles may be hosted on a static HTTPS server or
another lightweight endpoint.

Acceptance criteria, if implemented:

- Remote Addressables content is hosted over HTTPS.
- The remote load path points to the hosted content.
- The project can load round images from the remote host.
- Setup and deployment steps are documented.
- Failure of remote delivery still results in the fallback texture.

This requirement is optional and must not block completion of the mandatory
local Addressables implementation.

## 9. Implementation Baseline

The following values are project decisions and do not originate from the PDF:

- Unity: `6000.3.21f1`
- Rendering pipeline: URP
- Addressables: `4.0.1`
- Input: Unity Input System
- Tests: Unity Test Framework

Changing these values requires documenting the reason.

## 10. Definition of Done

The task is complete when:

- [ ] The target prefab is loaded through Addressables.
- [ ] The fallback texture is loaded through Addressables.
- [ ] Both startup assets are loaded exactly once.
- [ ] A round image loads asynchronously at startup.
- [ ] A correct tap increases the score.
- [ ] A correct tap starts a new round.
- [ ] A missed tap does not change the score.
- [ ] A missed tap does not change the current texture.
- [ ] A missed tap shows negative feedback.
- [ ] A failed round load applies the fallback texture.
- [ ] Starting a newer load invalidates the previous pending load.
- [ ] A stale result cannot overwrite a newer result.
- [ ] Every retained Addressables asset has an explicit owner.
- [ ] All Addressables resources are released during teardown.
- [ ] No synchronous Addressables waiting is used.
- [ ] The project works in the documented local Addressables mode.
- [ ] The Unity Console contains no unexpected errors.
- [ ] Relevant EditMode tests pass.
- [ ] Relevant PlayMode tests pass, if included.
- [ ] The README explains setup, execution, testing, and Addressables build steps.