# Store listing text

Paste-ready listing text for Google Play and the App Store, written on
2026-10-28 from main at `a7e1362b`, and updated the same day for the fixed
Adventurer's Kit troop and the website fixes (uncommitted in the checkout at
the time). Copy each field from its `text` block. The count beside each label
was measured by the script at the end of this file, not by eye. Rerun it after
any edit.

Placeholders in square brackets, such as `[publisher name]`, can't be filled
from the repo.

## What the copy is based on

Every number in the copy comes from the data files or the code:

| Claim | Value | Source |
| --- | --- | --- |
| Stages and zones | 100 stages, 10 zones of 10 | `data/stages.json` (100 entries, 10 `MapId` values, 10 each) |
| Troops to recruit | 32 (the Peasant you start with, plus 31) | `data/units.json`: `Side` = `Player`, `LegacyRecruit` = false. The 5 legacy recruits have `UnlockStage` 101 and can't be earned in the campaign. The Adventurer's Kit never gives one |
| Foes | 73: 59 ordinary types and 14 bosses | `data/units.json`: `Side` = `Enemy`, without the non-combatant Ash Pilgrim |
| Spells and tactical tools | 30 spells, 12 tactical tools | `data/spells.json` (`spell_*` and `tactic_*`) |
| Relics | 100 | `data/equipment.json` |
| Troop levels | 1 to 20 | `docs/catalogs/CURRENT_CONTENT_STATS.md` |
| Loadout | Six slots shared by troops and spells, at least one troop | `MaxLoadoutSize` in `scripts/core/GameState.cs` |
| Arcane Barrage | Costs 100 mana, the mana cap | `data/combat_config.json` (`CaravanSpell`) |
| Challenge Tower | 100 floors | `ChallengeTowerCatalog.MaxFloor` |
| Endless | Opening boon, a checkpoint every 5 waves, a boss every 15 | `README.md`, `EndlessBossCheckpointCatalog.cs` |
| Wagon | Plating, stores, march drum, rune beacon, archer crew, ballista, firepot launcher, 8 looks | `scripts/core/BaseUpgradeDefinition.cs`, `scripts/core/WagonSkinCatalog.cs` |
| Music | 19 original loops, one per zone battle plus scene and boss themes | `ROADMAP.md`, `art/audio` |
| Languages | English only | `data/locale/` holds only `en.json` |
| Ads | None | No ad SDK in `Game.csproj`, `addons/`, `android/plugins/`, `ios/plugins/` |

Left out on purpose:

- **Live multiplayer.** Internet rooms stay in closed testing until the relay
  is connected (`docs/DEPLOYMENT.md`, "Remaining public-launch blockers"). LAN
  races are also left out.
- **Guilds, friends, the arena and the weekly raid.** They run on the device
  only. The arena's rivals are generated (`ArenaCatalog`), the guild is a
  local "New Warband" and friends are a local list, so none of them involve
  other players.
- **Rankings.** The Rankings screen always shows "Rankings are offline"
  (`scripts/ui/LeaderboardMenu.cs`).
- **The season pass.** Its premium track can't be unlocked. See the findings
  at the end of this file.
- **"Free".** The price isn't set in the repo. Add "free to play" only if the
  app is free to download.

## Google Play

<!-- field: play.title | limit: 30 -->
**App name** · 23 / 30 characters
```text
Crownroad: Siege of Ash
```

<!-- field: play.short | limit: 80 -->
**Short description** · 73 / 80 characters
```text
Escort a war wagon through 100 lane-battle sieges against an undead host.
```

<!-- field: play.full | limit: 4000 -->
**Full description** · 2171 / 4000 characters
```text
The kingdom fell to the Rotbound Host. Its roads still stand.

Crownroad: Siege of Ash is a side-on lane-battle strategy game. Your war wagon holds one end of the road and a Rotbound stronghold holds the other. Courage builds as the fight goes on. Spend it to send troops out of the wagon's door, cast spells with the mana each kill earns, and break the gate before the dead break your wagon.

THE ROAD TO THE CITADEL
• 100 stages in ten zones, from the King's Road and the Saltwake Docks to the Crownfall Citadel
• A painted map for every zone. Open it tile by tile, defeat the zone's nine leaders, then face the boss at its gate
• 73 kinds of foe, 14 of them bosses, from the Grave Lord to the Ashen Regent
• Later stages bring hazards, cursed ground and battlefield events

YOUR WARBAND
• 32 troops to recruit, from Peasants and Spearmen to Paladins, Longbowmen and Ballista Crews
• Level each troop to 20, promote it, and equip relics from a collection of 100
• 30 spells and 12 tactical tools, among them Fireball, Frost Burst, a palisade kit and a bolt turret
• Six loadout slots shared by troops and spells, so every battle asks what to bring
• When mana is full, the wagon's lantern looses Arcane Barrage across the whole field

THE WAR WAGON
• Upgrade its plating and stores, its march drum and its rune beacon
• Train its archer crew, then mount a ballista and a firepot launcher
• Earn new looks for the wagon as you play

MORE WAYS TO PLAY
• Endless: pick a zone and an opening boon, then hold out against waves that keep getting harder. Draft upgrades and choose forks in the road at each checkpoint
• The Challenge Tower's 100 floors, boss rush and daily bounties
• Seeded challenge codes to share, with your best run replayed as a ghost

PLAY YOUR WAY
• Play as a guest. No account needed
• Sign in when you like, with Google Play Games, Google, Apple or an email code, to back up your progress and carry it to another device
• The campaign plays offline
• Reduced motion, high contrast and larger text options
• An original score, with music for each zone

Optional in-app purchases sell gold, food and bundles. The game has no ads.

Available in English.
```

<!-- field: play.release-notes | limit: 500 -->
**Release notes for 1.0.0** · 131 / 500 characters
```text
The Lantern Caravan sets out: 100 stages in ten zones, 32 troops, 30 spells and 12 tactical tools, Endless and the Challenge Tower.
```

**Category:** Game → Strategy. Play allows one category.

**Tags:** choose up to five from the console's list. Suggested: Strategy,
Tower defense, Fantasy, Medieval, Offline. **[Check]** the exact tag names in
Play Console → Store presence → Store settings.

**Contact details:** email `[support email]`, website
`https://crownroad.game/`. A phone number is optional.

## App Store

<!-- field: apple.name | limit: 30 -->
**Name** · 23 / 30 characters
```text
Crownroad: Siege of Ash
```

<!-- field: apple.subtitle | limit: 30 -->
**Subtitle** · 27 / 30 characters
```text
Undead lane-battle strategy
```

<!-- field: apple.promo | limit: 170 -->
**Promotional text** · 146 / 170 characters
```text
Escort the Lantern Caravan's war wagon through 100 stages in ten zones. Send out troops, cast spells and break every gate the Rotbound Host holds.
```

<!-- field: apple.description | limit: 4000 -->
**Description** · 2186 / 4000 characters
```text
The kingdom fell to the Rotbound Host. Its roads still stand.

Crownroad: Siege of Ash is a side-on lane-battle strategy game. Your war wagon holds one end of the road and a Rotbound stronghold holds the other. Courage builds as the fight goes on. Spend it to send troops out of the wagon's door, cast spells with the mana each kill earns, and break the gate before the dead break your wagon.

THE ROAD TO THE CITADEL
• 100 stages in ten zones, from the King's Road and the Saltwake Docks to the Crownfall Citadel
• A painted map for every zone. Open it tile by tile, defeat the zone's nine leaders, then face the boss at its gate
• 73 kinds of foe, 14 of them bosses, from the Grave Lord to the Ashen Regent
• Later stages bring hazards, cursed ground and battlefield events

YOUR WARBAND
• 32 troops to recruit, from Peasants and Spearmen to Paladins, Longbowmen and Ballista Crews
• Level each troop to 20, promote it, and equip relics from a collection of 100
• 30 spells and 12 tactical tools, among them Fireball, Frost Burst, a palisade kit and a bolt turret
• Six loadout slots shared by troops and spells, so every battle asks what to bring
• When mana is full, the wagon's lantern looses Arcane Barrage across the whole field

THE WAR WAGON
• Upgrade its plating and stores, its march drum and its rune beacon
• Train its archer crew, then mount a ballista and a firepot launcher
• Earn new looks for the wagon as you play

MORE WAYS TO PLAY
• Endless: pick a zone and an opening boon, then hold out against waves that keep getting harder. Draft upgrades and choose forks in the road at each checkpoint
• The Challenge Tower's 100 floors, boss rush and daily bounties
• Seeded challenge codes to share, with your best run replayed as a ghost

PLAY YOUR WAY
• Play as a guest. No account needed
• Sign in when you like, with Game Center, Apple, Google or an email code, to back up your progress and carry it to another device
• The campaign plays offline
• Reduced motion, high contrast and larger text options
• An original score, with music for each zone

Optional in-app purchases sell gold, food and bundles. The game has no ads.

Terms of use: https://crownroad.game/terms
```

<!-- field: apple.keywords | limit: 100 bytes -->
**Keywords** · 100 characters, 100 bytes / 100 bytes
```text
tower,defense,td,medieval,fantasy,castle,knight,wagon,caravan,war,army,spell,magic,kingdom,roguelite
```

The keywords leave out the words already in the name (Crownroad, Siege, Ash)
and the subtitle (undead, lane, battle, strategy), because Apple indexes those
already. They also leave out "game", plurals of words already present, and
other games' names. "zombie" would reach more searches, but the theme bible
keeps the host undead rather than zombie, so it is left out. Swap it in for
`kingdom` if you disagree.

<!-- field: apple.whats-new | limit: 4000 -->
**What's New in This Version (1.0)** · 131 / 4000 characters
```text
The Lantern Caravan sets out: 100 stages in ten zones, 32 troops, 30 spells and 12 tactical tools, Endless and the Challenge Tower.
```

App Store Connect doesn't ask for What's New on an app's first version. Keep
this text for the first update if the field isn't shown.

**Categories:**

- Primary: Games, with the subcategories Strategy and Action.
- Secondary: optional. Entertainment, or leave it empty.

**Copyright:** `© 2026 [publisher name]`

**License agreement:** Apple's standard EULA, or paste `/terms` as a custom
EULA. The description above links the terms either way. **[Legal]**

## URLs

The website in `site/src/` has four pages, served without the `.html` ending
(`docs/WEBSITE.md`), and these anchors on the support and privacy pages:
`#purchases`, `#progress`, `#online`, `#delete-data`, `#security`,
`#refund-requests`, `#your-rights`, `#delete-account`.

| Field | Store | URL |
| --- | --- | --- |
| Marketing URL | App Store | `https://crownroad.game/` |
| Website | Google Play | `https://crownroad.game/` |
| Support URL | App Store | `https://crownroad.game/support` |
| Privacy policy URL | Both | `https://crownroad.game/privacy` |
| Privacy choices URL (optional) | App Store | `https://crownroad.game/privacy#your-rights` |
| Delete account URL (Data safety) | Google Play | `https://crownroad.game/support#delete-data` |
| Terms of use | App Store custom EULA, or the description | `https://crownroad.game/terms` |

The game opens the same three pages from Settings → Account → **Privacy
policy**, **Terms of use** and **Help and support** (`scripts/core/SiteLinks.cs`).

`site/site.json` already has `"domain": "crownroad.game"`. The pages can't go
live until `legal_name`, `support_email`, `governing_law` and
`effective_date` are filled in.

## In-app products

All ten products come from `data/shop_products.json`. The grants are the
catalog's amount plus its bonus, which matches what the server credits
(`ProductRewards` in `server/Endpoints.cs`). Prices are the catalog's
reference US dollar prices. Prices in other currencies: `[set in each
console]`. Apple has a price point at every one of these amounts, and Play
converts from the US price unless you set local prices.

Both stores:

- **Apple type:** Consumable, except the Adventurer's Kit (below). Apple
  doesn't let you change a product's type after you create it, so decide the
  kit before creating it.
- **Play type:** one-time product. Play has no consumable setting: the game
  consumes each purchase after the server's grant is saved
  (`NativeIAPService`), which makes it consumable.
- **Family Sharing (Apple):** off. Consumables can't use it.
- **Review screenshot (Apple):** one per product, for example the Royal
  storehouse page that shows the pack.
- **Reference name (Apple, internal):** use the product ID, such as
  `gold_pouch`.

What the currencies do, for the descriptions: gold recruits and levels
troops, scribes spells, upgrades the war wagon and pays for the forge. Food
starts battles (2 each) and opens map tiles (1 each), and refills by itself, 2
every 5 minutes up to 24 (`data/stages.json`, `GameState.Food.cs`).

### Pouch of Gold

- IDs: Apple `com.crownroad.gold_pouch` · Play `gold_pouch`
- Grants: 500 gold
- Reference price: US$0.99
- Type: consumable on both stores

<!-- field: iap.gold_pouch.apple-name | limit: 30 -->
**Apple display name** · 13 / 30 characters
```text
Pouch of Gold
```

<!-- field: iap.gold_pouch.apple-description | limit: 45 -->
**Apple description** · 40 / 45 characters
```text
500 gold for troops, spells and upgrades
```

<!-- field: iap.gold_pouch.play-name | limit: 55 -->
**Play name** · 24 / 55 characters
```text
Pouch of Gold (500 gold)
```

<!-- field: iap.gold_pouch.play-description | limit: 200 -->
**Play description** · 122 / 200 characters
```text
A small purse of 500 gold. Gold recruits and levels troops, scribes spells, upgrades the war wagon and pays for the forge.
```

### Chest of Gold

- IDs: Apple `com.crownroad.gold_chest` · Play `gold_chest`
- Grants: 2,200 gold (2,000 plus a 200 bonus)
- Reference price: US$3.99
- Type: consumable on both stores

<!-- field: iap.gold_chest.apple-name | limit: 30 -->
**Apple display name** · 13 / 30 characters
```text
Chest of Gold
```

<!-- field: iap.gold_chest.apple-description | limit: 45 -->
**Apple description** · 34 / 45 characters
```text
2,200 gold: 2,000 plus a 200 bonus
```

<!-- field: iap.gold_chest.play-name | limit: 55 -->
**Play name** · 26 / 55 characters
```text
Chest of Gold (2,200 gold)
```

<!-- field: iap.gold_chest.play-description | limit: 200 -->
**Play description** · 142 / 200 characters
```text
A chest of 2,200 gold: 2,000 plus a 200 bonus. Gold recruits and levels troops, scribes spells, upgrades the war wagon and pays for the forge.
```

### War Chest

- IDs: Apple `com.crownroad.gold_warchest` · Play `gold_warchest`
- Grants: 6,900 gold (6,000 plus a 900 bonus)
- Reference price: US$9.99
- Type: consumable on both stores

<!-- field: iap.gold_warchest.apple-name | limit: 30 -->
**Apple display name** · 9 / 30 characters
```text
War Chest
```

<!-- field: iap.gold_warchest.apple-description | limit: 45 -->
**Apple description** · 34 / 45 characters
```text
6,900 gold: 6,000 plus a 900 bonus
```

<!-- field: iap.gold_warchest.play-name | limit: 55 -->
**Play name** · 22 / 55 characters
```text
War Chest (6,900 gold)
```

<!-- field: iap.gold_warchest.play-description | limit: 200 -->
**Play description** · 152 / 200 characters
```text
A heavy war chest of 6,900 gold: 6,000 plus a 900 bonus. Gold recruits and levels troops, scribes spells, upgrades the war wagon and pays for the forge.
```

### King's Treasury

- IDs: Apple `com.crownroad.gold_treasury` · Play `gold_treasury`
- Grants: 18,000 gold (15,000 plus a 3,000 bonus)
- Reference price: US$19.99
- Type: consumable on both stores

<!-- field: iap.gold_treasury.apple-name | limit: 30 -->
**Apple display name** · 15 / 30 characters
```text
King's Treasury
```

<!-- field: iap.gold_treasury.apple-description | limit: 45 -->
**Apple description** · 38 / 45 characters
```text
18,000 gold: 15,000 plus a 3,000 bonus
```

<!-- field: iap.gold_treasury.play-name | limit: 55 -->
**Play name** · 29 / 55 characters
```text
King's Treasury (18,000 gold)
```

<!-- field: iap.gold_treasury.play-description | limit: 200 -->
**Play description** · 155 / 200 characters
```text
The royal treasury: 18,000 gold, of which 3,000 is a bonus. Gold recruits and levels troops, scribes spells, upgrades the war wagon and pays for the forge.
```

### Field Rations

- IDs: Apple `com.crownroad.food_rations` · Play `food_rations`
- Grants: 20 food
- Reference price: US$0.99
- Type: consumable on both stores

<!-- field: iap.food_rations.apple-name | limit: 30 -->
**Apple display name** · 13 / 30 characters
```text
Field Rations
```

<!-- field: iap.food_rations.apple-description | limit: 45 -->
**Apple description** · 43 / 45 characters
```text
20 food to start battles and open map tiles
```

<!-- field: iap.food_rations.play-name | limit: 55 -->
**Play name** · 23 / 55 characters
```text
Field Rations (20 food)
```

<!-- field: iap.food_rations.play-description | limit: 200 -->
**Play description** · 116 / 200 characters
```text
20 food for the road. Food starts battles and opens new tiles on the zone maps. It also refills by itself over time.
```

### Caravan Provisions

- IDs: Apple `com.crownroad.food_provisions` · Play `food_provisions`
- Grants: 65 food (60 plus a 5 bonus)
- Reference price: US$2.99
- Type: consumable on both stores

<!-- field: iap.food_provisions.apple-name | limit: 30 -->
**Apple display name** · 18 / 30 characters
```text
Caravan Provisions
```

<!-- field: iap.food_provisions.apple-description | limit: 45 -->
**Apple description** · 26 / 45 characters
```text
65 food: 60 plus a 5 bonus
```

<!-- field: iap.food_provisions.play-name | limit: 55 -->
**Play name** · 28 / 55 characters
```text
Caravan Provisions (65 food)
```

<!-- field: iap.food_provisions.play-description | limit: 200 -->
**Play description** · 122 / 200 characters
```text
65 food: 60 plus a 5 bonus. Food starts battles and opens new tiles on the zone maps. It also refills by itself over time.
```

### Siege Stockpile

- IDs: Apple `com.crownroad.food_stockpile` · Play `food_stockpile`
- Grants: 170 food (150 plus a 20 bonus)
- Reference price: US$6.99
- Type: consumable on both stores

<!-- field: iap.food_stockpile.apple-name | limit: 30 -->
**Apple display name** · 15 / 30 characters
```text
Siege Stockpile
```

<!-- field: iap.food_stockpile.apple-description | limit: 45 -->
**Apple description** · 29 / 45 characters
```text
170 food: 150 plus a 20 bonus
```

<!-- field: iap.food_stockpile.play-name | limit: 55 -->
**Play name** · 26 / 55 characters
```text
Siege Stockpile (170 food)
```

<!-- field: iap.food_stockpile.play-description | limit: 200 -->
**Play description** · 149 / 200 characters
```text
A large supply cache of 170 food: 150 plus a 20 bonus. Food starts battles and opens new tiles on the zone maps. It also refills by itself over time.
```

### Royal Granary

- IDs: Apple `com.crownroad.food_granary` · Play `food_granary`
- Grants: 460 food (400 plus a 60 bonus)
- Reference price: US$14.99
- Type: consumable on both stores

<!-- field: iap.food_granary.apple-name | limit: 30 -->
**Apple display name** · 13 / 30 characters
```text
Royal Granary
```

<!-- field: iap.food_granary.apple-description | limit: 45 -->
**Apple description** · 29 / 45 characters
```text
460 food: 400 plus a 60 bonus
```

<!-- field: iap.food_granary.play-name | limit: 55 -->
**Play name** · 24 / 55 characters
```text
Royal Granary (460 food)
```

<!-- field: iap.food_granary.play-description | limit: 200 -->
**Play description** · 151 / 200 characters
```text
The kingdom's granary: 460 food, of which 60 is a bonus. Food starts battles and opens new tiles on the zone maps. It also refills by itself over time.
```

### Adventurer's Kit

- IDs: Apple `com.crownroad.starter_kit` · Play `starter_kit`
- Grants: 800 gold, 30 food and the player's next troop. The server allows one
  kit per player (`server/Endpoints.cs`), and the card says "Once per
  account".
- The troop is fixed, not random. `GameState.NextKitUnitId()` picks the first
  troop the player doesn't own, by lowest unlock stage and then roster order,
  even before the player reaches its stage. On a fresh save that is the
  Militia. The card names it before **Buy** ("Recruits Militia"), and the shop
  says "+ Militia recruited" afterwards. It never picks a legacy recruit, so
  a player who already owns all 32 troops sees "Every troop recruited" and
  gets gold and food only.
- Reference price: US$4.99
- Type: **undecided** (`docs/LAUNCH_CHECKLIST.md`, section 1). The
  recommendation is consumable:
  - **Consumable** (what the game does today): Apple Consumable, Play
    one-time product that the game consumes. A player can pay twice, for
    example after restoring an old save. The server refuses the second grant
    and the game leaves that purchase unfinished. Google refunds an
    unacknowledged purchase after 3 days. Apple keeps the payment until the
    player asks Apple for a refund.
  - **Non-consumable:** Apple Non-Consumable, Play one-time product that the
    game acknowledges instead of consuming. The store then stops a second
    payment. This needs code: acknowledge rather than consume on Google, and
    a **Restore purchases** control on iOS, which App Review requires for any
    non-consumable. The game has no restore control today.

The descriptions say "your next troop" rather than a name, because the troop
depends on what the player already owns. The card in the game names it.

<!-- field: iap.starter_kit.apple-name | limit: 30 -->
**Apple display name** · 16 / 30 characters
```text
Adventurer's Kit
```

<!-- field: iap.starter_kit.apple-description | limit: 45 -->
**Apple description** · 45 / 45 characters
```text
Once only: 800 gold, 30 food, your next troop
```

<!-- field: iap.starter_kit.play-name | limit: 55 -->
**Play name** · 35 / 55 characters
```text
Adventurer's Kit (once per account)
```

<!-- field: iap.starter_kit.play-description | limit: 200 -->
**Play description** · 152 / 200 characters
```text
A starter bundle, once per account: 800 gold, 30 food and your next troop, recruited at once. The kit's card in the game names the troop before you buy.
```

### Campaign Resupply

- IDs: Apple `com.crownroad.campaign_resupply` · Play `campaign_resupply`
- Grants: 3,000 gold and 80 food
- Reference price: US$7.99
- Type: consumable on both stores

<!-- field: iap.campaign_resupply.apple-name | limit: 30 -->
**Apple display name** · 17 / 30 characters
```text
Campaign Resupply
```

<!-- field: iap.campaign_resupply.apple-description | limit: 45 -->
**Apple description** · 36 / 45 characters
```text
3,000 gold and 80 food in one bundle
```

<!-- field: iap.campaign_resupply.play-name | limit: 55 -->
**Play name** · 39 / 55 characters
```text
Campaign Resupply (3,000 gold, 80 food)
```

<!-- field: iap.campaign_resupply.play-description | limit: 200 -->
**Play description** · 155 / 200 characters
```text
A mid-campaign bundle of 3,000 gold and 80 food. Gold pays for troops, spells and wagon upgrades. Food starts battles and opens new tiles on the zone maps.
```

## Findings that affect the listing

1. **The season pass's Unlock premium button leads nowhere.** It opens the
   Royal storehouse, but no product sells the premium pass and nothing in the
   game sets it (`SetPremiumPass` has no caller). A reviewer who taps it
   finds no way to buy it. Hide the button or the premium track for launch.
2. **Internet rooms aren't hidden.** The multiplayer screen opens on its
   Rooms board, with Match and Host, and nothing in the code turns it off.
   The copy above doesn't mention them. If they stay in closed testing,
   the release build needs a change to hide them.
3. **Parts of the website are still out of date.** The hero numbers, the
   siege section and the support page's device-move answer are fixed. Still
   stale in `site/src/index.html`: the heading "Nineteen squads and ten
   battlefield rites"; the muster roll, which lists 19 squads with the old
   unlock stages and costs, including the War Hound, Alchemist, Necromancer,
   Lantern Guard and Stormcaller that can no longer be recruited; "Rites" with
   a courage column, though spells cost mana; "Take up to six squads and five
   rites", though six slots are shared; "nineteen kinds of Rotbound soldier",
   though there are 59; and "host a LAN race", which the store copy leaves
   out. The challenge share link (`https://crownroad.game/challenge/<code>`,
   `DeepLinkHandler`) still has no page on the site, so it shows the 404
   page.

## How the counts were measured

Each field above is a marker comment, a label line and a `text` block. This
script measures every block, writes the count onto the label line and fails
if a field is over its limit. Characters are Unicode code points. The
keywords are also measured in UTF-8 bytes, which is how Apple counts them.

```python
import re, sys

PATTERN = re.compile(
    r"(<!-- field: (?P<id>[\w.\-]+) \| limit: (?P<limit>\d+)(?P<bytes> bytes)? -->\n"
    r"\*\*(?P<label>[^*]+)\*\* · )(?P<count>[^\n]*)\n"
    r"```text\n(?P<text>.*?)\n```",
    re.S,
)

def measure(path):
    source = open(path, encoding="utf-8").read()
    over = []
    def replace(m):
        text, limit = m.group("text"), int(m.group("limit"))
        chars, size = len(text), len(text.encode("utf-8"))
        used = size if m.group("bytes") else chars
        count = f"{chars} characters, {size} bytes / {limit} bytes" if m.group("bytes") else f"{chars} / {limit} characters"
        if used > limit:
            over.append(m.group("id"))
        print(f"{m.group('id')}: {used}/{limit}")
        return f"{m.group(1)}{count}\n```text\n{text}\n```"
    open(path, "w", encoding="utf-8").write(PATTERN.sub(replace, source))
    return over

sys.exit(1 if any(measure(p) for p in sys.argv[1:]) else 0)
```

Save it as a file and run it with `python3 <file> docs/store/LISTING.md`.
