# ASSETS

Complete asset manifest for replacing procedural placeholder visuals with authored art. The game currently draws everything with Godot `_Draw()` calls — no sprites, textures, or authored audio exist yet.

## Art Direction

Setting: **medieval fantasy siege warfare**. The player commands a **Lantern Caravan** (war wagon, soldiers in field armor, heraldic banners) against the **Rotbound Host** (undead armies: risen corpses, rot hulks, bone juggernauts, grave lords). Battlefields are side-scrolling lane fights with the war wagon on the left and an enemy gatehouse on the right.

Reference: `THEME_BIBLE.md` for full faction/setting details.

---

## 1. Unit Sprite Sheets

All units need a sprite sheet with these animation states. Sheets should be authored at **2x display resolution** for downscale sharpness.

### Animation States Per Unit

| State | Frames | Loop | Notes |
|-------|--------|------|-------|
| Idle | 4-6 | yes | Subtle breathing/bob, weapon resting |
| Walk | 6-8 | yes | Moving toward the enemy side |
| Attack | 4-6 | no | Melee swing, ranged draw/release, or cast |
| Hit | 2-3 | no | Flinch/flash on taking damage |
| Death | 4-6 | no | Collapse, dissolve, or shatter |
| Deploy | 3-4 | no | Materializing onto the field (bright flash in) |

### Facing Direction

All units face **right** in the sheet. Enemy units are flipped horizontally at runtime.

### Frame Size

Base frame: **64x64 px** at 1x scale. Units with VisualScale > 1.0 use proportionally larger frames:

| Scale | Frame Size | Used By |
|-------|-----------|---------|
| 0.92x | 58x58 | Cavalry Rider, Sapper, Ghoul |
| 1.0x | 64x64 | Most units |
| 1.08x | 70x70 | Shield Knight |
| 1.18x | 76x76 | Grave Brute |
| 1.22x | 78x78 | Rot Hulk |
| 1.25x | 80x80 | Bone Juggernaut |
| 1.55x | 100x100 | Grave Lord (boss) |

### Player Units (11)

Each unit has a **primary tint color** used for heraldic accents (tabard, shield, weapon glow). The base armor should be neutral iron/leather so tinting works.

| # | Display Name | Visual Class | Tint | Silhouette Brief |
|---|-------------|-------------|------|-----------------|
| 1 | **Swordsman** | fighter | #f4a261 | Medium build, one-handed sword, round shield on back. Standard infantry. |
| 2 | **Archer** | gunner | #8ecae6 | Lean build, longbow drawn at shoulder height, quiver on back. |
| 3 | **Shield Knight** | shield | #f6bd60 | Stocky build, large kite shield held forward, short sword at hip. Shield dominates the silhouette. |
| 4 | **Spearman** | fighter | #d4a373 | Medium build, long spear extending past frame, small buckler. Taller reach than swordsman. |
| 5 | **Crossbowman** | gunner | #a8dadc | Lean build, heavy crossbow extended forward, bolt case at hip. Longer weapon than archer. |
| 6 | **Cavalry Rider** | skirmisher | #ffb703 | Leaning-forward sprint pose, short sword, light armor. Fastest unit — legs show running stride. |
| 7 | **Siege Engineer** | support | #90be6d | Stocky build, wrench/hammer tool, backpack with repair supplies. Shorter weapon, bulkier torso. |
| 8 | **Mage** | sniper | #e9c46a | Robed figure, tall staff with glowing tip, book or scroll at belt. Longest attack range. |
| 9 | **Halberdier** | fighter | #f3722c | Tall build, two-handed polearm (halberd), heavier plate than swordsman. Gate-breaker. |
| 10 | **Alchemist** | gunner | #f28482 | Medium build, one-handed flask/bomb in throwing pose, satchel of vials. Splash damage. |
| 11 | **Battle Monk** | support | #b7efc5 | Robed figure with staff, faint aura glow ring around them. Buff provider — needs visible aura idle. |

### Enemy Units (11)

Enemies use a **necrotic/undead palette**: grays, purples, sickly greens, bone whites, dark reds. No heraldic tabards — instead use rot marks, exposed bone, spectral glow.

| # | Display Name | Visual Class | Tint | Silhouette Brief |
|---|-------------|-------------|------|-----------------|
| 1 | **Risen** | walker | #84a98c | Shambling humanoid, tattered clothes, one arm reaching forward. Basic undead. |
| 2 | **Ghoul** | runner | #6d597a | Hunched, fast-moving, clawed hands, jaw open. Smaller than Risen. |
| 3 | **Rot Hulk** | bloater | #c77dff | Very round/swollen torso, no visible neck, pustules on belly. Explodes on death. |
| 4 | **Grave Brute** | brute | #9d0208 | Wide shoulders, thick arms, crude bone-plate armor on chest. Heavy hitter. |
| 5 | **Blight Caster** | spitter | #bdb2ff | Hunched figure, one arm extended with glowing pustule/orb for ranged attack. |
| 6 | **Bone Nest** | splitter | #43aa8b | Tangled bone/root mass on legs, glowing core at center. Splits into 2 Risen on death. |
| 7 | **Sapper** | saboteur | #f94144 | Small, fast, hunched, carrying a satchel charge. Explosive pack visible on back. |
| 8 | **Dread Herald** | howler | #f8961e | Standing tall, two horn-like protrusions from head, arms spread. Visible aura ring. |
| 9 | **Hexer** | jammer | #577590 | Thin figure, antenna/staff with glowing tip, ritual symbols on robe. Signal disruptor. |
| 10 | **Bone Juggernaut** | crusher | #7f5539 | Massive frame, full bone-plate armor, slow stance. Heaviest standard enemy. |
| 11 | **Grave Lord** | boss | #5a189a | Towering figure, crown of bone spikes (3 prongs), cape, two escort spawn points at sides. Rally aura visible. |

---

## 2. Structure Sprites

### War Wagon (Player Base)

**Size:** ~130x140 px (including banner mast)

| Component | Description |
|-----------|------------|
| Body | Wood-and-iron plated wagon, rectangular, weathered |
| Cabin | Rear driver's cabin with small window |
| Bumper | Front-left iron ram plate |
| Roof | Triangular canopy over the body with fabric drape |
| Banner mast | Tall vertical pole from roof, pentagonal heraldic banner at top |
| Banner | Route-colored — tinted at runtime. Should have a neutral base with tintable region |
| Wheels | Two large wooden wheels with iron rims |
| Lantern | Small glowing light on cabin, pulses red at low HP |
| Damage states | 3 states: healthy (clean), damaged (<75% HP: cracks, loose boards), critical (<45% HP: fire, heavy damage) |
| Smoke | Rising smoke plume above wagon when damaged — currently procedural particles, could be sprite overlay |

### Enemy Gatehouse (Enemy Base)

**Size:** ~120x160 px (including tower and banner)

| Component | Description |
|-----------|------------|
| Wall | Central stone/bone fortification, dark and imposing |
| Towers | Two flanking tower sections, shorter than main wall |
| Battlements | Crenellated top edge with 4 raised merlons |
| Gate | Central portcullis with 4 vertical iron bars, recessed and dark |
| Banner | Pentagonal enemy banner at top tower, tinted at runtime |
| Brazier | Glowing orange-red fire point at front |
| Damage cracks | Diagonal crack lines appear at <75% and <45% HP |
| Damage states | 3 states matching wagon: healthy, cracked, burning/collapsed |

---

## 3. Battlefield Backgrounds

Each terrain needs a **tiled or stretched background image** fitting the 1280x720 viewport. The battlefield area is approximately 1100x400 px centered vertically.

### Layer Structure

| Layer | Z-Index | Content |
|-------|---------|---------|
| Sky | 0 | Gradient or painted sky (palette.SkyColor) |
| Far background | 5 | Distant terrain silhouettes (mountains, buildings, trees) |
| Ground | 10 | Main battlefield surface |
| Lane stripes | 15 | Subtle horizontal lane markers (6 stripes) |
| Terrain decoration | 20 | Foreground props: barrels, rubble, columns, etc. |
| Units/effects | 50-100 | Gameplay layer |
| Ambient particles | 45-50 | Embers, snow, mist, etc. (already implemented as CpuParticles2D) |

### Terrain Types (31)

Priority tiers for art — do the 10 main route terrains first, then variants.

**Tier 1: One per district (10)**

| Terrain ID | District | Visual Theme | Key Props |
|-----------|---------|-------------|-----------|
| `urban` | King's Road | Cobblestone road, market stalls, bell tower silhouettes | Road markings, building facades |
| `shipyard` | Saltwake Docks | Wooden docks, tide pools, wrecked ship hulls | Water layer at bottom, rope/post pairs |
| `foundry` | Emberforge March | Dark factory interior, furnace glow, molten channels | Pillar columns, glowing ore troughs |
| `checkpoint` | Ashen Ward | Quarantine camp, ritual tent frames, warning stakes | Hazmat tent arcs, ground stakes |
| `pass` | Thornwall Pass | Mountain cliff road, avalanche debris, peak silhouettes | Mountain triangle overlays, cliff edge marks |
| `cathedral` | Hollow Basilica | Ruined nave interior, gothic arches, oil lamp rows | Arch pairs with buttress lines, flame dots |
| `marsh` | Mire of Saints | Boggy causeway, standing water, dead reeds | Bog circles, reed cluster lines |
| `grassland` | Sunfall Steppe | Burned grassland, distant wagons, open sky | Fire streaks at bottom, tent frames |
| `grove` | Gloamwood Verge | Twisted forest path, thick trunk silhouettes | Tree trunk lines, scattered light orbs |
| `bridgefort` | Crownfall Citadel | Stone bridge and fortification, inner ward | Castle tower blocks, bridge parapet line |

**Tier 2: District variants (21)**

| Terrain ID | Variant Of | Visual Difference |
|-----------|-----------|-------------------|
| `highway` | King's Road | Raised stone causeway with yellow lane markers |
| `night` | King's Road | Moonlit version, glowing orbs in corners |
| `industrial` | King's Road | Warehouse blocks, darker palette |
| `swamp` | Mire | Deeper water, fewer structures |
| `railyard` | Emberforge | Rail track lines with crossties |
| `smelter` | Emberforge | Closer furnace, glowing heat bars |
| `decon` | Ashen Ward | Decontamination arcs (half-circles) |
| `lab` | Ashen Ward | Research tables, glowing strip light |
| `blacksite` | Ashen Ward | Tall support pillars, barbed geometry |
| `shrine` | Thornwall | Stone shrine pillars with base platforms |
| `watchfort` | Thornwall | Tower rects with crenellated tops, patrol walkway |
| `ossuary` | Hollow Basilica | Bone pile clusters, narrow pillars |
| `reliquary` | Hollow Basilica | Display cases, golden glow strips |
| `chapel` | Mire | Slightly drier, plant growth, organic |
| `ferry` | Mire | Water crossing, dock fragments |
| `waystation` | Sunfall Steppe | Ruined way-stop buildings, tent roofs |
| `siegecamp` | Sunfall Steppe | Military camp, siege equipment silhouettes |
| `witchcircle` | Gloamwood | Ritual circle ground marks, ember wisps |
| `timberroad` | Gloamwood | Log road, sawmill frames |
| `breachyard` | Crownfall Citadel | Rubble-filled courtyard, collapsed arches |
| `innerkeep` | Crownfall Citadel | Throne room interior, banner rows |

---

## 4. Spell Effect Sprites

Each spell needs a **cast animation** (played at target location) and a **persistent effect** (for duration-based spells). Current effects are procedural geometry — sprites should match the shapes described.

| Spell | Cast Animation | Shape Reference | Frame Count | Size |
|-------|---------------|----------------|-------------|------|
| **Fireball** | Expanding sunburst with 8 radiating lines, core flash | Star/explosion | 6-8 frames | 128x128 |
| **Heal** | Cross pattern with 4 petal circles, soft glow bloom | Plus/flower | 6-8 frames | 128x128 |
| **Frost Burst** | 6-point ice crystal with branching spikes | Snowflake/crystal | 6-8 frames | 128x128 |
| **Lightning Strike** | Zigzag bolt from top, branching forks, bright center | Lightning bolt | 4-6 frames | 96x160 (tall) |
| **Barrier Ward** | Hexagonal sigil with inner/outer rings, 6 corner nodes | Magic circle | 6-8 frames | 128x128 |

---

## 5. Projectile Sprites

| Projectile | Used By | Size | Description |
|-----------|---------|------|-------------|
| **Arrow** | Archer | 16x6 | Wooden shaft, fletching, iron tip |
| **Bolt** | Crossbowman | 14x5 | Shorter, thicker, metal tip |
| **Magic missile** | Mage | 12x12 | Glowing orb with trailing sparkle |
| **Flask** | Alchemist | 10x10 | Glass vial with colored liquid, trailing fumes |
| **Repair bolt** | Siege Engineer | 14x6 | Bolt with wrench-head or gear icon |
| **Battle Monk shot** | Battle Monk | 10x10 | Small glowing aura orb |
| **Blight spit** | Blight Caster | 12x12 | Green/purple glob with drip trail |

Each needs: **flying sprite** (with rotation matching travel direction) and **impact sprite** (2-3 frame burst).

---

## 6. Campaign Map Assets

### Map Background (per district)

**Size:** ~720x480 px panel area. One illustrated background per district showing the regional landscape from an overhead/isometric angle.

### Stage Nodes

| State | Appearance |
|-------|-----------|
| Locked | Grayed out, padlock icon, subtle bob animation |
| Unlocked | Full color, idle glow pulse |
| Selected | Bright highlight ring, stronger glow pulse |
| Completed | Star/checkmark badge overlay |
| Current target | Arrow indicator or beacon effect |

**Node size:** ~62 px diameter (31 px radius drawn as circle currently)

### Route Lines

Connecting lines between stage nodes. Should look like a worn road/path with:
- Solid line for cleared segments
- Dashed/faded line for locked segments
- Glow effect on the selected route

---

## 7. UI Sprites and Skins

### Deploy Cards (Battle HUD)

**Size:** Variable width x 82 px height per card

| Element | Description |
|---------|------------|
| Card background | Parchment/iron frame, tinted per unit color |
| Unit icon | Small portrait (32x32) of the unit |
| Cost badge | Courage cost number in a shield/coin frame |
| Cooldown overlay | Semi-transparent dark fill that shrinks left-to-right |
| State indicators | DEPLOY (green ready), CD (gray cooldown), NEED (red insufficient) |

### Spell Cards (Battle HUD)

**Size:** Variable width x 64 px height

Same structure as deploy cards but with spell icon and magic-themed frame (arcane border instead of iron).

### Resource Bars

| Bar | Color | Size |
|-----|-------|------|
| Courage meter | Gold (#ffd166) fill | 520x16 px |
| Wave progress | Route-accent fill | 520x12 px |
| War wagon HP | Green (#80ed99) fill | 132x8 px |
| Gatehouse HP | Pink (#ff8fab) fill | 124x8 px |
| Unit HP | Green (#80ed99) fill | ~30x5 px per unit |

### Menu Panel Skins

| Panel | Theme |
|-------|-------|
| Title screen | Dark navy (#1d2d44), centered parchment panel |
| Campaign map | Route-colored panels, side info panel |
| Loadout briefing | Route-colored, 3-panel layout |
| Shop/Armory | Route-colored, scrollable unit/upgrade grid |
| Endless prep | Dark blue (#102a43), route selector |
| Multiplayer | Dark blue, code entry, board display |
| Settings | Dark navy, centered form panel |
| Battle end | Semi-transparent overlay, result summary |
| Pause overlay | 55% black overlay, centered text |

---

## 8. Audio Assets

All sounds are currently generated procedurally with sine/square waves. Replace with authored audio.

### Sound Effects

| Cue ID | Category | Duration | Description |
|--------|----------|----------|-------------|
| `ui_hover` | UI | 0.08s | Light click/tap on menu hover |
| `ui_confirm` | UI | 0.12s | Positive selection tone |
| `scene_change` | UI | 0.18s | Whoosh/sweep for scene transitions |
| `deploy` | Battle | 0.18s | Unit materialization thud + shimmer |
| `impact_light` | Battle | 0.12s | Light melee/arrow hit |
| `impact_heavy` | Battle | 0.16s | Heavy melee hit, meatier thud |
| `bus_hit` | Battle | 0.18s | Wood/iron crunch on war wagon |
| `barricade_hit` | Battle | 0.16s | Stone crumble on gatehouse |
| `repair` | Battle | 0.22s | Wrench/hammer repair clink |
| `hazard_warning` | Battle | 0.18s | Alert chime before hazard |
| `hazard_strike` | Battle | 0.22s | Hazard pulse/fire burst |
| `spell_cast_fire` | Battle | 0.24s | Fireball whoosh + crackle |
| `spell_cast_heal` | Battle | 0.28s | Warm chime + shimmer |
| `spell_cast_frost` | Battle | 0.26s | Ice crack + crystalline ring |
| `spell_cast_lightning` | Battle | 0.22s | Electric zap + thunder |
| `spell_cast_ward` | Battle | 0.28s | Deep hum + sigil pulse |
| `boss_spawn` | Battle | 0.72s | Low rumble + horn blast |
| `upgrade_confirm` | Shop | 0.24s | Rising positive chime |
| `victory` | Result | 1.2s | Fanfare: 3-4 note ascending brass |
| `defeat` | Result | 1.0s | Descending somber chord |

### Ambient Loops

Each loop should be **seamlessly loopable** at ~8-15 seconds. The game modulates volume and pitch based on battle pressure.

| Cue ID | District | Mood | Key Sounds |
|--------|---------|------|------------|
| `ambience_route_road` | King's Road | Brisk, hopeful | Wind, distant bells, cart wheels |
| `ambience_route_harbor` | Saltwake Docks | Tidal, somber | Waves, creaking wood, gulls |
| `ambience_route_foundry` | Emberforge March | Industrial, tense | Hammering, furnace roar, hissing steam |
| `ambience_route_quarantine` | Ashen Ward | Unsettling, hollow | Distant coughs, wind through fabric, rattling |
| `ambience_route_thornwall` | Thornwall Pass | High, exposed | Strong wind, distant rockfall, eagle cry |
| `ambience_route_basilica` | Hollow Basilica | Echoing, sacred | Stone echo, dripping water, faint chanting |
| `ambience_route_mire` | Mire of Saints | Wet, oppressive | Bubbling bog, insects, squelching |
| `ambience_route_steppe` | Sunfall Steppe | Open, rolling | Grass rustling, distant thunder, fire crackle |
| `ambience_route_gloamwood` | Gloamwood Verge | Creeping, dark | Branches creaking, owl calls, rustling leaves |
| `ambience_route_citadel` | Crownfall Citadel | Grand, commanding | Stone echo, banners flapping, distant horns |
| `ambience_menu` | Title screen | Warm, anticipatory | Gentle hearth, distant caravan sounds |
| `ambience_shop` | Armory | Industrious | Forge clinking, leather stretching |
| `ambience_battle` | Generic battle | Tense, reactive | Low drone, building pressure |
| `ambience_endless` | Endless mode | Relentless | Accelerating drum pulse, wind |
| `ambience_multiplayer` | Multiplayer | Competitive | Higher energy drone, anticipation |

---

## 9. Particle Effect Textures (Optional)

The game uses Godot `CpuParticles2D` with default circle rendering. For polish, provide small particle textures:

| Texture | Size | Description |
|---------|------|-------------|
| `particle_soft.png` | 16x16 | Soft radial gradient circle (general purpose) |
| `particle_ember.png` | 8x8 | Bright core with orange feathered edge |
| `particle_snow.png` | 8x8 | White soft circle with slight blur |
| `particle_leaf.png` | 12x8 | Asymmetric leaf shape, brown-green |
| `particle_dust.png` | 10x10 | Very soft, low-contrast tan circle |
| `particle_spark.png` | 6x6 | Bright white core, hard falloff |
| `particle_mist.png` | 32x32 | Large, very soft, nearly transparent blob |
| `particle_firefly.png` | 6x6 | Bright green-yellow center, rapid falloff |

---

## 10. File Organization

Suggested directory structure for authored assets:

```
assets/
  sprites/
    units/
      player/
        swordsman.png       (sprite sheet: 6 cols x 6 rows = idle/walk/attack/hit/death/deploy)
        archer.png
        ...
      enemy/
        risen.png
        ghoul.png
        ...
    structures/
      war_wagon.png         (3 damage states side by side)
      gatehouse.png         (3 damage states side by side)
    projectiles/
      arrow.png
      bolt.png
      ...
    spells/
      fireball_cast.png     (animation strip)
      heal_cast.png
      ...
    ui/
      deploy_card_bg.png
      spell_card_bg.png
      courage_bar.png
      hp_bar.png
      ...
    map/
      node_locked.png
      node_unlocked.png
      node_selected.png
      route_line.png
      backgrounds/
        kings_road.png
        saltwake_docks.png
        ...
    particles/
      particle_soft.png
      particle_ember.png
      ...
  terrain/
    urban_bg.png
    shipyard_bg.png
    foundry_bg.png
    ...
  audio/
    sfx/
      ui_hover.wav
      ui_confirm.wav
      deploy.wav
      ...
    ambience/
      route_road.ogg
      route_harbor.ogg
      ...
```

---

## Summary Counts

| Category | Count | Priority |
|----------|-------|----------|
| Player unit sprite sheets | 11 | High |
| Enemy unit sprite sheets | 11 | High |
| Structure sprites | 2 (x3 damage states) | High |
| Projectile sprites | 7 | Medium |
| Spell effect animations | 5 | Medium |
| Terrain backgrounds (tier 1) | 10 | High |
| Terrain backgrounds (tier 2) | 21 | Low |
| Map district backgrounds | 10 | Medium |
| Map node sprites | 4-5 states | Medium |
| UI panel skins | 9 menu types | Low |
| Particle textures | 8 | Low |
| Sound effects | 20 | High |
| Ambient loops | 15 | Medium |
| **Total unique assets** | **~135** | |
