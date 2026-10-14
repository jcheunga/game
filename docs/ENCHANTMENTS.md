# Forge enchantments

Implemented · 10 October 2026

Open **Forge → Enchantments**. The selected inventory relic opens first; **Choose relic** lets you select another owned item. Every relic holds one enchantment, which stays with it when changing bearer or equipment slot.

The screen shows available Gold and Essence, all ten choices, and the full price before applying. Selecting a choice spends nothing. For an equipped relic, the comparison shows its wearer's current and resulting battle stats, including the other relic slot, current enchantment, squad bonuses and shared permanent limits. Changed stats appear first. A stored relic shows its future effect without claiming to change a current unit.

Replacing an enchantment pays the selected enchantment's full price and removes the old effect. Selecting the already-applied effect cannot charge again. Removal is free, refunds no materials, and leaves the relic owned and equipped. Both application and removal survive saving and reload. Enchanted relics carry a crystal marker in the Forge inventory.

| Enchantment | Gold | Essence | Effect |
| --- | ---: | ---: | --- |
| Flame Touch | 180 | 2 | +2% damage, +2 critical bonus points; periodic burn. |
| Lifesteal | 220 | 2 | +1% damage; heal 3% of direct troop damage. |
| Thorns | 220 | 2 | +3 armor; reflect 5% of direct melee damage. |
| Haste | 160 | 1 | +3% movement; 1% faster attack recovery. |
| Shielding | 160 | 1 | +3% health; +2 magical resistance points. |
| Vampiric | 240 | 3 | +1% damage, +2 critical bonus points; 2% lifesteal. |
| Crit Strike | 220 | 2 | +2 critical chance points; +4 critical bonus points. |
| Poison | 180 | 2 | +1% damage, +1 magical resistance point; periodic healing reduction. |
| Fortify | 180 | 2 | +3% health, +4 armor; −3% movement. |
| Arcane Echo | 240 | 3 | +2% damage, +2 magical resistance points; a limited ability echo. |

Essence already comes from Arena wins, Tower rewards, bounties and reward tracks. The Forge uses those existing sources. Insufficient Gold or Essence disables application with a reason; previewing remains available.

Enchantments share the same permanent bonus budget as relics and other progression. Their permanent stats cannot bypass the health/damage, armor, resistance or critical limits. Conditional effects retain their own triggers and cooldowns. Enchantments grant neither extra unit levels nor an independent growth multiplier.

## Validation

The content review passed **692 checks**, including every enchantment's cost, actual wearer in the second slot, stat preview against applied battle stats, read-only selection, duplicate-charge prevention, replacement tradeoffs, shared caps, insufficient resources, stored relics and saved removal. The enchantment UI review passed **37 checks** across desktop and phone layouts; the existing equipment flow passed its **29 checks** again. Full project verification passed with zero build warnings/errors, 11,502 data checks and 86 backend tests. Physical device playtesting remains part of the wider presentation work.

```sh
godot --headless --path . scenes/tests/ContentRevampSmoke.tscn -- --save-suffix=content-review-enchantments
godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-enchantments --enchantments
```
