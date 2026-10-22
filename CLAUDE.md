# Crownroad: Siege of Ash

Godot 4.6 C# game. Art pipelines live in `art/` (unit art is in `art/remaster`, documented in
`art/remaster/README.md`).

## Unit animations

Any work on how units or enemies look in motion (walk, attack, cast, death, stance) or on their sprites follows the
`unit-animation` skill (`.claude/skills/unit-animation/SKILL.md`). In short:
- **Measure, don't eyeball.** `art/remaster/puppet/review/check.sh all` runs the per-frame motion check: smear,
  ground contact, foot slip, and gait (squat and crab walk). It must report `0 of 111 units fail` before anything
  ships.
- **Review tools:** contact sheets, zoomed frames, the joint view, smear overlays, rig probes and before/after
  stills are in `art/remaster/puppet/review/`.
- **Pipeline:** render with `review/render.sh`, then pack (`art/remaster/pack.py stage` / `apply`). Then run
  `BlenderAssetSmoke` and `CombatReviewSmoke --regressions`; both must report 0 failures.
- **New sprites:** Codex repaints go through `art/remaster/puppet/stride.py`. Joints are checked by eye
  (`review/landmarks_view.py`) and set with `setlm.py`.
