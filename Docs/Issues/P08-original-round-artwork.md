# P08: Replace demo imagery with a small original, documented texture set

Priority: P2. Dependencies: P03.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#10](https://github.com/Yurii-Tor/Addressables-Sample/issues/10).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Goal and evidence
The current ten icons8-prefixed PNGs came from the original task; ThirdPartyNotices says
their applicable terms/source were not established. Give the public demo a coherent original
visual identity without making a legal claim about the old files.

## Read only these entry points first
- Docs/ThirdPartyNotices.md and Docs/Requirements.md: source brief/round-image requirements.
- Assets/_Project/Editor/TestTaskPaths.cs: RoundStems, RoundTexturePaths.
- Assets/_Project/Editor/TestTaskSetup.cs: ConfigureRoundTextureImporters, config/addressables creation.
- Assets/_Project/Editor/ProjectValidation.cs: expected round entries/importer checks.
- Assets/_Project/Runtime/Presentation/TargetSurface.shader: alpha composition and upright UVs.
- Assets/Textures: supplied inputs (inspect inventory; do not move/delete originals).

## Bounded approach
Create ten simple original geometric motifs using deterministic Editor generation: a small
palette and distinct shapes, legible on every cube face. Use code-native drawing rather
than copying stock art. Raster/image generation is optional only if owner chooses it later;
it is not necessary for this task.

## Implementation
1. Confirm the source PDF does not require the exact supplied icons; if it does, preserve a
   baseline option and document the optional portfolio set instead of replacing requirements.
2. Add original generated textures under an owned generated folder. Keep existing source
   assets untouched in this retained checkout; remove them only from active config/groups,
   never from disk as part of this issue.
3. Define deterministic names/order/palette. Keep ten entries and round-robin behavior so
   image count/order semantics stay simple; prefer preserving existing address keys where practical.
4. Reconcile assets in place via AssetDatabase to preserve new GUIDs on repeat setup.
   Author .meta/import settings through Unity only. Update P03 inventory for new generated files.
5. Update config/group mappings, validation and assumptions in tests that assert animal names.
   Keep prefab/fallback startup ownership and Pack Separately unchanged.
6. Document authorship/generation provenance; distinguish inactive original task assets from
   images now shipped in the demo. Do not say historical third-party files became MIT.
7. No visual-effects overhaul; existing shader/property-block path stays.

## Acceptance
- [ ] Ten distinguishable original motifs render clearly, upright, with correct alpha/background.
- [ ] Runtime/Addressables references select the new set; unused originals are not player dependencies.
- [ ] Original supplied files remain preserved, with truthful notices.
- [ ] Repeated generation preserves paths/GUIDs/bytes as defined by P03.
- [ ] No runtime texture generation or new external asset dependency.

## Verification and stop
Inspect bundle/build dependency evidence and every motif on the real rotating cube.
Run config/structural tests, shared runtime/idempotency matrix and local WebGL smoke.
Stop at local assets/docs; publishing new media or removing original task files is separate.
