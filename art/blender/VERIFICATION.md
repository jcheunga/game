# Visual asset pass — verification

## Contact-driven combat motion — 2026-09-27

- **53 character sources**, **20 shared fighting-style profiles**, ten attack poses
  per character and **1,696 total character frames**. The existing 1536 × 960 atlas
  size is unchanged; its formerly unused four cells now hold the extra attack poses.
- **429 motion checks**, **122 combat regressions**, **187 headless art checks** and
  **15 headless health-bar checks** passed. Build: **0 errors / 0 warnings**.
- Timing checks cover anticipation, exact contact pose, one-shot damage, recovery,
  render/simulation clock separation, incoming hits during attacks, facing behind,
  reduced-motion presentation, pooling, escaped targets, projectile origin/body
  contact, caravan repair, objective support/damage and canceled objective actions.
- **2,005 decoded images**, **203 native sources**, **0 failures**; all 53 packed
  character atlases have clear silhouette margins. Large weapons are reframed with
  compensated draw scale, and all affected poses are rerendered together.
- The native finish audit reopened **192 scenes / 1,488 material instances** with
  no failures. Original geometry, frame-one poses, surface materials and lighting
  remain intact. Source provenance accepts the recorded finish or motion publish,
  and native clip metadata must match the published frame metadata.
- Live simulation samples: stages 1 and 30 completed with the benchmark squad;
  stage 60 reached its 90-second test limit without a runtime failure. This is
  **not** an all-campaign balance certification. Wind-up/recovery changes combat
  timing even though damage values and catalog cooldowns were retained.
- Recorded controlled duels through the actual engine/attack scheduling path.
  `combat-in-game.gif` is a labeled enlargement of viewport crops, not composited
  hit effects. Existing boss-area telegraphs remain separate from the new basic
  attacks and player-ability release beats. Idle/walk/death/deploy poses are retained;
  this is not a new skinned character-animation system.

Current logs and previews: `artifacts/blender/combat-motion/`. The previous polished
sources, masters and atlases are recoverable in
`artifacts/blender/animation-backup.87etzM/`; its code copies use `.cs.backup` so they
are not compiled. These are local recovery copies, not off-device backups.
The Android editor shutdown notice and exit-only ObjectDB warning in headless
combat/motion harnesses remain recorded. No failed assertions are hidden.
The older finish-only comparison report below is historical; its unchanged-animation
comparison predates this intentional animation revision.

## Material and lighting finish — 2026-09-27

Completed the road-worn miniature surface/lighting pass without changing model
geometry, frame-one poses, animation clip contracts, combat rules, or sprite sizes.

- **192 finished native scenes**, all successfully reopened and audited.
- **1,488 material instances** checked for editable, self-contained node surfaces.
- **53 characters / 1,484 animation frames**, re-rendered at 64 maximum Cycles samples
  with adaptive sampling and denoising. Portraits, icons, aliases and class fallbacks repacked.
- **8 caravans**, **5 mounts**, **1 gatehouse**, **31 battlefields**, **10 maps**,
  **23 menu environments**, and **61 item/icon scenes** refreshed.
- **1,793 decoded images**, **203 native source files**, **0 validation failures**.
- **77 unit metadata files** checked against the before-version: dimensions,
  animation ranges, ground anchors and draw scales unchanged.
- **11 surface-system checks**, including repeatability and separate cloth/paint materials: passed.
- **191 real-engine art/animation checks** and **18 health-bar/accessibility checks**: passed.
- **121 combat regression checks**: passed; the existing exit-only ObjectDB cleanup warning remains.
- Build: **0 warnings / 0 errors**. The pre-existing Android editor shutdown
  notice still appears during headless import; neither final runtime suite logs errors.
- Reviewed real-engine captures at stages 1, 24 and 60, Codex portraits, and high-contrast mode.
- Corrected three single-plane axe heads after visual review so their silhouettes
  stay readable instead of turning black between metallic reflections.

Review: `artifacts/blender/polish-v2/before-after.jpg`, `verification.json`,
`native-audit.json`, and `surface-tests.json`. Logs use the `polish-v2-` prefix.
The before-version is preserved locally in `artifacts/blender/polish-backup.KDwIyC/`.
Review files/backups are ignored by Git and excluded from engine import; they are
local recovery copies, not off-device backups.

The 11 tintable particle effects and existing painted main-menu/map illustrations
are intentionally unchanged. This is a surface/lighting pass, not a replacement
of shared character silhouettes or a full UI/balance review.

## Original visual coverage pass

Verified locally on 2026-09-26 with Blender 5.2.2 LTS and Godot 4.6.1 Mono.

- Required visual catalog: complete in every category; see `coverage.json`.
- Native Blender scenes: **203**, all present and nonempty.
- Image validation: **1,793** decoded images, **0 failures**. This includes all 1,484 character master frames, transparency/framing checks and environment dimensions.
- Packed unit atlases: **53** individual IDs plus **23** shared-class fallbacks; no clipped animation frames.
- Build: **0 errors, 0 warnings**.
- Final real-engine art suite: **191 passed checks, 0 failures**. Includes imported textures, six clips per character, ground anchors, caravan skins, weapon mounts, repeated attacks, movement between physics ticks, deployment, hit and pooled-unit death handling.
- Combat regression suite: **121 passed checks, 0 failures**.
- Menu review: **0 failed checks** across the existing 18-menu review profile. Screenshots were inspected for new-background and icon integration; this is not an exhaustive typography audit.
- Live combat samples: stages 1 and 30 completed with the benchmark squad. Stage 60 ran to its 90-second test limit without a runtime failure; it did **not** complete within that limit. No claim of an all-campaign balance pass.
- Captured the real battle scene with a representative display roster at stages 1, 24 and 60, plus an unlocked Codex detail view. These captures use isolated saves, not player progress.

Review files and logs are under `artifacts/blender/`. Key previews are `units-contact-sheet.jpg`, `caravans-contact-sheet.jpg`, `battlefields-contact-sheet.jpg`, `relics-contact-sheet.jpg`, `animation-review.gif`, `assets-in-battle-1.png` and `codex-in-game.png`.

## Non-blocking engine notices

The editor's headless import completes but prints an Android editor-settings shutdown notice. The existing combat/menu smoke harnesses also print object/RID cleanup warnings at exit. The dedicated final art suite exits with no errors or warnings. These notices are preserved in the logs rather than counted as passing assertions or silently discarded.

## Art status

This is a complete **first visual pass**, not a claim of final production polish. Models are stylized, rigidly articulated miniatures; archetypes are shared across some units and bosses. Native source files support continued silhouette, material and animation refinement. Existing painted/vector UI assets and procedural audio are retained. Optional route-specific screen overrides are deliberately outside required coverage.
