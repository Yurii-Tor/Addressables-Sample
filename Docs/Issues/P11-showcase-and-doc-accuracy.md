# P11: Make README concise and synchronize operational claims with code

Priority: P2. Dependencies: P01, P02, P03, P04, P05, P06, P07, P08, P09.
Status at authoring: planned. Audit baseline: 45fcc696f4005780d06db1bd8e5b28876c1dd604.
GitHub issue: [#12](https://github.com/Yurii-Tor/Addressables-Sample/issues/12).

Read [AgentStart](../AgentStart.md) and [TaskExecution](../TaskExecution.md) before implementation.
The body below is mirrored in GitHub. Update both if scope changes.
Only this selected issue is in scope; preserve the one-scene architecture and retained checkout.

## Goal and audit evidence
Keep README a portfolio entry point: playable link, representative media, three strong
engineering facts, quick start and routes to deeper docs. Correct current overstatements:
- GameController is plain C# but still uses Texture2D/GameObject; it is not Unity-independent.
- Pack Separately does not mean every round downloads from the network.
- Source text scans do not prove absence of every forbidden wait or all leaks.
- tests.yml has branch/path filters; tests do not run on every push.
- Operations section 9 still describes GitHub Pages/uncompressed output, while actual
  workflow uploads an artifact and BuildWebGl uses Brotli/decompression fallback.
- Historical remote-validation notes are not current live service evidence.

## Read only these entry points first
- README.md; Docs/Operations.md: sections 8, 9, 9a, 12-13.
- .github/workflows/tests.yml; .github/workflows/webgl-demo.yml.
- Assets/_Project/Editor/ContinuousIntegration.cs: build settings.
- Assets/_Project/Runtime/Core/GameController.cs; Tests/EditMode/SourceAuditTests.cs.
- Docs/ThirdPartyNotices.md; Docs/media/README.md; completed feature briefs.
- Do not load the entire historical ImplementationPlan to rewrite the showcase.

## Implementation
1. Rewrite README around observable demo behavior and supported technical claims. Retain
   AI provenance transparently in a short factual paragraph; do not invent personal work claims.
2. Link architecture/ownership details to the small source map and relevant code, and
   operations to Operations.md. Keep essential first-run steps accurate.
3. Update Operations section 9 to actual YAML triggers, artifact-only build workflow and
   separate demos-site deployment. Remove contradictory Pages/uncompressed instructions,
   preserving historical evidence in the historical plan rather than presenting it as current.
4. Clarify local browser assets versus desktop remote path as a project choice; do not imply
   WebGL fundamentally cannot use remotely hosted WebGL bundles.
5. Describe simulation labels, measured load duration and owner counter accurately.
   Mention idempotency comparison only after P03 is present.
6. Refresh media instructions/recording to show hit, miss, slow load, fallback and supersession
   in a short sequence. Capture the real implementation; no fabricated screenshot/video.
7. Update only current-state navigation; leave historical session records intact.

## Acceptance
- [ ] README first screen communicates demo purpose and a playable entry point.
- [ ] Technical/CI/hosting claims match checked source.
- [ ] No claims that simulation is a real network fault, owners are bytes, or local test
      results prove browser behavior.
- [ ] Relative links/media references resolve; provenance and notices remain truthful.
- [ ] Planned features are not described as shipped until present and verified.

## Verification and stop
Read actual final feature code/YAML; check Markdown links and render README/diagrams/media.
Media can remain explicitly pending if capture is unavailable; do not mark that criterion done.
No code/CI trigger changes, publication, upload to hosting or GitHub release as part of this task.
Documentation/showcase changes remain on their task branch unless owner authorizes merging.
