# P06: Build touch-friendly scenario controls and block UI clicks from world selection

Priority: P2. Dependencies: P03, P05.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#8](https://github.com/Yurii-Tor/Addressables-Sample/issues/8).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Problem and goal
Overlay is fixed-size IMGUI, no wrapping, keyboard-only toggle. PointerSelectionInput
raycasts the world for every press. Adding buttons without input arbitration would trigger
a cube hit or red miss through UI. Generator currently has strict roots and two HUD texts.

## Read only these entry points first
- Assets/_Project/Runtime/Presentation/PointerSelectionInput.cs: OnPressPerformed/EvaluateSelection.
- Assets/_Project/Runtime/Presentation/DiagnosticsOverlay.cs; HudView.cs.
- Assets/_Project/Editor/TestTaskSetup.cs: scene/HUD creation and RemoveUnexpectedChildren.
- Assets/_Project/Editor/ProjectValidation.cs: ValidateScene strict root/child/component assertions.
- Assets/_Project/Tests/PlayMode/RealGamePlayModeTests.cs: mouse/touch fixture.
- P05 commands and P04 polling records.

## Locked small design
Use existing uGUI for all interactive controls and diagnostics. One generated EventSystem
with InputSystemUIInputModule, one responsive panel, one show/hide button.
Keep a single target and scene. Retain keyboard shortcuts as optional convenience.
Do not add UI Toolkit/TMP migration or third-party UI packages to this task.

## Implementation
1. Generate controls for Slow load, Simulate failure, Replace request and a diagnostics toggle. Bind commands through presentation, never diagnostic mutations of controller internals.
2. Move the diagnostic rendering to the generated panel while retaining polling semantics.
   Remove obsolete IMGUI texture/style lifetime only after replacement works.
3. Before physics selection, perform current-screen-position UI raycast against intended
   interactive graphics. A blocked press returns without Selection invocation; false would cause a miss.
4. Do not assume IsPointerOverGameObject in an InputAction callback has current mouse/touch state.
   Share deliberate UI blocking rules; decorative full-screen labels/backgrounds must not consume everything.
5. Set raycastTarget intentionally. Buttons consume the pointer even when disabled.
   Hidden panels cannot block world input. Preserve hit and empty-space miss behavior elsewhere.
6. Use CanvasScaler, anchors/layout and bounded/scrolling history. Support safe areas as practical.
   Validate 360x640 portrait, 640x360 landscape and 1280x720; readable text, no target/control overlap that prevents play.
7. Update setup idempotency and validator expectations together, including the EventSystem,
   references and intentional extra roots/children. Never hand-edit scene/prefab/.meta YAML.

## Acceptance
- [ ] Mouse and touch can run all three scenarios and toggle diagnostics.
- [ ] Pressing UI over cube or empty space causes no world selection or red flash.
- [ ] Hidden panel permits world input; normal miss/hit still work.
- [ ] Startup/fatal states disable inappropriate commands.
- [ ] Narrow viewports show readable, accessible controls without horizontal clipping.
- [ ] Repeated setup produces no duplicate EventSystem, handlers or controls.

## Verification and stop
PlayMode tests use actual UI and virtual mouse/touch, including disabled controls, hidden
panel and button-over-cube. Inspect local WebGL at the listed dimensions; record screenshots
as local evidence, not claims inferred from anchors. Run shared runtime/idempotency matrix.
Restart button/recovery behavior belongs to P07.
