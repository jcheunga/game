# Crownroad Progression and Power Budget

Version 3 implemented balance prototype · 9 October 2026

Keep early recruits useful throughout the campaign. Later discoveries offer a modest strength increase, a specialized skill, or another way to answer a threat, paid for by courage, deployment time, attack recovery, lower stats elsewhere, or a real weakness. Recruitment stage and rarity do not grant an extra stat tier. This design supersedes the earlier compounded-growth proposal and retunes all 32 core units. The core stat curves and shared permanent caps are implemented. Enemy base profiles have received additional encounter tuning; [the playable catalog](catalogs/CURRENT_CONTENT_STATS.md) is the current numeric reference.

## Shallow level growth

Every player card can level from 1 to 20 on the same curve. Health adds 1.5% of level 1 base per level gained; ordinary attack and structure damage add 1% of base per level gained. The totals at level 20 are **+28.5% health and +19% damage**. Armor, magical resistance, critical chance, critical damage bonus, movement, range, and attack/deployment timing do not increase with unit level.

The earlier proposal multiplied level 20 health by 4.316 and attack damage by 3.026 before other upgrades. Those gains are retired for the current design. Defense and critical growth are also retired; they remain visible authored stats influenced by bounded equipment choices.

| Level | HP multiplier | Damage multiplier |
| --- | --- | --- |
| 1 | 1.000 | 1.000 |
| 5 | 1.060 | 1.040 |
| 10 | 1.135 | 1.090 |
| 15 | 1.210 | 1.140 |
| 20 | 1.285 | 1.190 |

No recruit starts with a free level advantage merely because it appears in a later zone. Essential counters and intrinsic roles work when the unit is recruited. If an extra skill unlocks at a level milestone, early and late cards share that milestone. Training never turns a Peasant card automatically into a knight card.

## Core comparisons

Paired HP and attack values are level 1 / level 20 per body. Militia pays once for two bodies. Ordinary level 20 DPS includes baseline critical probability and body count, before defenses and special skills. A heavy weapon can deal a larger individual hit while attacking less frequently.

| Unit | Courage | Bodies | HP 1 / 20 | Attack 1 / 20 | Attack interval s | Ordinary DPS at 20 per card |
| --- | --- | --- | --- | --- | --- | --- |
| Peasant | 8 | 1 | 30 / 39 | 5 / 5.95 | 1.2 | 4.96 |
| Militia | 14 | 2 | 24 / 31 | 4.5 / 5.35 | 1.15 | 9.46 |
| Swordsman | 20 | 1 | 64 / 82 | 10 / 11.9 | 0.9 | 13.75 |
| Man-at-Arms | 24 | 1 | 72 / 93 | 11 / 13.09 | 1.1 | 12.38 |
| Shield Knight | 28 | 1 | 76 / 98 | 8 / 9.52 | 1.15 | 8.49 |
| Warhammer Knight | 32 | 1 | 80 / 103 | 17 / 20.23 | 1.4 | 14.93 |
| Poleaxe Knight | 34 | 1 | 84 / 108 | 18 / 21.42 | 1.5 | 14.91 |

At the same level, Poleaxe Knight has about 31% more base HP than Swordsman and about 8% more ordinary expected DPS per body, but costs 70% more courage and deploys twice as slowly. Its physical protection and specialized thrust are the reasons to select it. Swordsman remains the steady inexpensive damage option. Peasant buys an emergency body for 8 courage, and Militia offers two modest bodies for 14; neither becomes a compulsory stepping stone to discard.

For standard foot infantry, aim for similar usefulness at equal courage and investment. Late professionals generally stay within about 15% of basic steady DPS per body, with stronger physical protection or utility paid elsewhere. Specialists such as Greatswordsman and Banner Knight deliberately trade ordinary DPS for a cleave or formation benefit. Judge effectiveness against repeated authored encounters rather than one universal power score.

## One shared permanent bonus budget

Add like bonuses across relics, enchantments, promotion, mastery, awakening, guild/account perks, and passive squad stat bonuses. Apply the level curve once, then apply this permanent layer once. These systems do not each receive their own independent multiplier. Promotion can open the second equipment slot, and mastery/awakening can offer cosmetic or tactical rewards; neither can bypass the shared stat limits.

| Permanent bonus from all sources combined | Implemented limit |
| --- | --- |
| Health | +10% of level-scaled base |
| Ordinary damage | +10% of level-scaled base |
| Armor rating | +10 |
| Direct physical resistance | +0.5 percentage points beyond armor |
| Magical resistance | +8 percentage points |
| Critical chance | +5 percentage points |
| Critical damage bonus | +10 percentage points |
| Attack interval reduction | 5% of the bare interval |
| Movement speed and reach | +10% each |
| Structure-only damage | +10% for already eligible attackers |
| Deployment cooldown reduction | 10% of the bare deployment cooldown |

Keep permanent builds near a maximum of +25% ordinary expected DPS and +25% effective health against either damage type compared with the same bare unit at the same level. The exhaustive 32-unit, 20-level all-caps preview gives maximum ratios of 1.209 for DPS, 1.251 for physical effective HP, and 1.234 for magical effective HP. Whole-health rounding has a tolerance of one percentage point; these are arithmetic checks, not evidence of encounter balance. Temporary skills and conditional procs require their own practical budget and playtests.

The version 3 reference retunes all 100 relic stat packages and all 10 enchantments to this smaller budget. Item attack recovery and range bonuses are now relative percentages rather than flat seconds or distance, and structure bonuses are structure-only percentages. Rarity mostly buys a bounded specialized effect; it does not guarantee much larger universal stats. Gear for early cards has the same caps and effect access as gear for late cards. The armory now provides a battle-stat comparison before equipping or removing a relic, including supplied/applied bonuses and excess above a cap. Both promoted relic slots use the same preview. The Forge also compares enchantment application, replacement and removal using the same shared bonus calculations. Extending the comparison to level upgrades remains a follow-up.

## Skills and weaknesses

A skill occupies part of the unit’s budget. Maceman has lower ordinary DPS to pay for exposing physical armor for allies. Billman pays for a short hook pull through a weaker direct fight. Shield Knight pays for physical protection and projectile interception through lower ordinary DPS. Rogue has stronger attack pressure but much less health and protection. Every return consumes the same allowance; Gravebound Knight’s one return restores only 25% HP and lowers movement speed. A gear return cannot add another full life.

Budget continuous damage or healing effects by their practical average output. A narrow counter can be stronger in its intended matchup, with limits, recovery, and a meaningful weakness elsewhere. Apply cooldown and eligibility limits to crowd control, summons, refunds, and item triggers. Costs and restrictions must be visible to the player. Avoid permanent skill damage that grows independently with unit level, skill rank, equipment rarity, and awakening stars.

## Enemy difficulty and early-unit checks

Ordinary enemies and bosses use the same shallow level HP and damage curve. Their base type armor, magical resistance, and critical profiles remain fixed. The normal campaign retains zone levels 1–2 through 19–20, and returning foes use the current encounter level. The encounter-tuned level 20 Risen has 48 HP and 5.891 attack. The curve is applied once; normal campaign health and damage multipliers are 1. The enemy profiles in the version 3 policy JSON preserve the pre-encounter authoring budgets; use the generated playable catalog for current combat numbers.

Increase difficulty through known enemy combinations, timing, flanks, target priorities, positioning, and bounded boss behavior. Do not use automatic resistance inflation or large hidden zone multipliers to invalidate early troops. Large boss base profiles remain encounter budgets, not a comparable player rarity tier. Retune an elite type when its existing base values break an encounter budget; shallow level growth alone cannot fix every base profile.

Before accepting the new roster, playtest Peasant, Militia, Spearman, Swordsman, and Archer at comparable levels and equipment investment in zones 8–10. Each needs several meaningful uses. Swordsman and Spearman should still fight useful matchups, rather than all early infantry surviving only as disposable sacrifices. Compare equal-courage groups, protect weak troops appropriately, and test both grouped splash and isolated attackers. Measure contribution, casualties, courage spent, deployment timing, and useful choices against specialist alternatives; no unperformed playtest is claimed here.

## Current and previous references

Use [the melee roster](/Users/jason/side/game/docs/MELEE_ROSTER_DESIGN.md) and [its core numeric profiles](/Users/jason/side/game/docs/design/melee-roster-revision.json) for the 32 core cards. [The complete version 3 policy reference](/Users/jason/side/game/docs/design/progression-balance-policy.json) contains all 74 enemy/optional encounter profiles with 20 levels each, 100 revised relic packages, 10 enchantments, and courage-normalized core comparison metrics. Reserve player concepts need the same base-stat and ability-budget review before entering the core.

The earlier Word catalogs and balance-stat-catalog.json are historical snapshots of the larger first-pass pool. Their compounded level curves and larger proposed gear packages are superseded. The live game now caps units at 20 and routes permanent unit bonuses through shared caps. Existing ownership and unit levels are preserved; new saves use Peasant first. See [implementation and validation](CONTENT_IMPLEMENTATION.md) for the current rollout and remaining balance work.
