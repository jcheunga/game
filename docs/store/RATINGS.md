# Age ratings and content declarations

Answers for Apple's age rating questionnaire, Google Play's IARC content
rating questionnaire, Play's target audience and its other App content
declarations. Written on 2026-10-28 from the game's code and data, and
updated the same day once the Adventurer's Kit's troop became fixed
(`GameState.NextKitUnitId`, uncommitted at the time). The
questionnaires change, so **[Check]** marks an answer whose exact console
wording couldn't be confirmed from the repo. This is a draft for your
judgement, not legal advice.

## What the game contains

Each answer below rests on these facts:

| Topic | What the game does | Where |
| --- | --- | --- |
| Violence | Every battle is melee and ranged combat between the player's human soldiers and an undead host, with swords, spears, axes, bows, crossbows, ballistas, firepots and spells. Painted 2D figures, side on, at phone size | `scripts/combat/`, `data/units.json` |
| Blood | Yes. Living troops bleed red and the risen dark red. A hit throws a short spray and droplets that leave small stains. A death adds a gush, a spatter on the ground and a pool under the body, and a body knocked back leaves a smear. Plague creatures ooze green, skeletons shed bone chips, wooden engines splinter. Stains fade and are capped at 90. Reduced motion drops the flying droplets but keeps stains. There is no setting that turns blood off | `scripts/combat/BattleGore.cs` |
| Death | Every unit has its own death animation: it falls, rests, then dissolves. Skeletons can scatter bones. No severed limbs or heads | `UnitDeathVisual`, `BattleDeathEffects`, `art/remaster/roster/deaths.py` |
| Horror themes | Risen dead, ghouls, liches, plague creatures, ossuaries and witch circles, in a painted medieval style. No jump scares, torture or body horror | `THEME_BIBLE.md` |
| Language | None crude. Battle shouts such as "For the caravan!" and "RAAAGH!" | `DeployQuote` in `data/units.json`, `data/locale/en.json` |
| Sexual content, drugs, alcohol | None | Text search of `data/` and the catalogs |
| Player-written text | The nickname only: 3 to 18 letters, numbers, spaces, hyphens or underscores. No word filter on the device or the server. Guild names aren't player-written (a local guild is always "New Warband"). Room titles are made from the host's nickname ("<nickname> Relay"). The friends list stores typed player IDs on the device only | `GameState.PlayerLevel.cs`, `server/Endpoints.cs`, `GuildMenu.cs`, `FriendsMenu.cs` |
| Where other players see a nickname | Only in internet rooms: the room list, room titles and room scoreboards. Challenge and daily boards show player IDs, not nicknames. The Rankings screen shows only the player's own row ("Rankings are offline"). The first-run prompt says "Other players see it on rankings and in rooms", which overstates it | `server/Endpoints.cs`, `MultiplayerMenu.cs`, `LeaderboardMenu.cs`, `NicknameDialog.cs` |
| Chat or messaging | None. Rooms have ready, launch, leave and report controls, no messages | `MultiplayerMenu.cs` |
| Reporting | In a room: a reason (Suspicious score, Offensive name, Harassment, Room abuse) and **Submit report**. Stored 90 days in `room_reports`. No block control | `OnlineRoomReportReasonCatalog.cs`, `server/StaleDataCleanup.cs` |
| Ads | None. No ad SDK and no advertising ID | `docs/STORE_PRIVACY_ANSWERS.md` |
| Gambling or simulated gambling | None. No wagers, slot machines, card games or wheels | Search of `scripts/` |
| Paid random items | **None.** The Adventurer's Kit (US$4.99) recruits a fixed troop: the first one the player doesn't own, by lowest unlock stage, then roster order. The card names it before **Buy** ("Recruits Militia" on a fresh save) | `NextKitUnitId` in `scripts/core/GameState.cs`, `StorePackCard.cs` |
| Other random rewards | Free only: relic fusion (3 relics in, 1 random relic of the next rarity out, no currency), expedition relic drops (no cost to start), bounty templates. None is sold. The relics that go into a fusion can be crafted with shards and gold, so a player can reach a random relic through gold they bought. That indirect path is read as No below **[Legal]** | `TryFuseRelics`, `RelicForgeCatalog`, `ExpeditionCatalog`, `BountyBoardCatalog` |
| Purchases | Ten packs of gold, food or both. Gold and food are also earned in play and can't be cashed out | `data/shop_products.json` |
| Contests | Seeded challenge boards and a daily board with score tables (player IDs). Arena tiers against generated rivals on the device. Tower and Endless bests | `MultiplayerMenu.cs`, `ArenaCatalog.cs` |
| Web access | None in the app. The system browser opens only Apple's and Google's sign-in pages, and three pages of our website from Settings → Account (**Privacy policy**, **Terms of use**, **Help and support**) | `AccountDialog.cs`, `SettingsMenu.cs`, `SiteLinks.cs` |
| Location | Not used | `export_presets.cfg` permissions |

## Apple: age rating

App Store Connect → the app → App Information → Age Ratings → Set Up Age
Ratings. The sections below follow the questionnaire's order. Ratings come
from Apple's "Age ratings values and definitions" page (iOS 26 age ranges:
4+, 9+, 13+, 16+, 18+; older systems show 4+, 9+, 12+, 17+).

### In-app controls

| Question | Answer | Why |
| --- | --- | --- |
| Parental controls | No | The game has none |
| Age assurance | No | No age check or Declared Age Range API |

### Capabilities

| Question | Answer | Why |
| --- | --- | --- |
| Unrestricted web access | No | No in-app browser. The system browser opens only the sign-in pages and the website's privacy, terms and support pages |
| User-generated content | **Depends on rooms.** No if internet rooms are hidden at launch. Yes if they are visible | Apple: "broad distribution of content created by users". The nickname is the only player-written text, and only rooms show it to others. Yes doesn't raise the rating, but brings guideline 1.2: a filter, a report tool, blocking and published contact details. The game has reporting only |
| Messaging and chat | No | None |
| Advertising | No | None |
| Social media | No | None |

### Mature themes

| Question | Answer | Why |
| --- | --- | --- |
| Profanity or crude humor | None | No swearing or crude jokes |
| Horror/fear themes | Infrequent | Undead and plague foes throughout, but painted fantasy with no frightening sequences. Frequent would give 13+, the same result |
| Alcohol, tobacco or drug use or references | None | The Alchemist throws flasks, nothing more |

### Medical or wellness

| Question | Answer |
| --- | --- |
| Medical or treatment information | None |
| Health or wellness topics | None |

### Sexuality or nudity

| Question | Answer | Why |
| --- | --- | --- |
| Mature or suggestive themes | None | Apple's definition lists "war or political strife". The game's war is against fantasy undead, and the fighting is answered under violence. If you read it as war, answer Infrequent (9+). Not Frequent, which gives 16+ |
| Sexual content or nudity | None | |
| Graphic sexual content and nudity | None | |

### Violence

| Question | Answer | Why |
| --- | --- | --- |
| Cartoon or fantasy violence | **Frequent** | Combat is the whole game. 13+ |
| Realistic violence | None | Apple: "humans in lifelike situations". These are painted figures fighting the risen dead. The human troops do bleed red, so this is a judgement call. Infrequent gives 13+, the same result |
| Prolonged graphic or sadistic realistic violence | None | Short sprays and pools, no lingering gore |
| Guns or other weapons | **Frequent** | Apple's definition includes swords. Every battle has them. 13+ |

### Chance-based activities

| Question | Answer | Why |
| --- | --- | --- |
| Simulated gambling | None | |
| Contests | Infrequent | Challenge and daily boards are side modes. Frequent gives 13+, the same result |
| Gambling | No | |
| Loot boxes | **No** | Apple: "virtual containers that give players randomized virtual items for purchase". The Adventurer's Kit names its troop before purchase, and nothing random is sold |

### Age categories and override

**Not Applicable.** Not Made for Kids, and no override.

### Expected Apple rating

| Global | Australia | Korea |
| --- | --- | --- |
| **13+** (12+ before iOS 26) | Follows the global rating | 12+, unless the GRAC issues another |

13+ also matches the privacy policy, which says the game isn't directed at
children under 13.

If a later version sells anything random, Apple's guideline 3.1.1 requires
the odds of each item before purchase, the loot-box answer becomes Yes, and
Australia shows a higher regional rating.

## Google Play: IARC content rating

Play Console → App content → Content rating → Start questionnaire.

- **Email address:** `[support email]`. IARC sends the certificate there.
- **Category:** Game.

The answers follow the questionnaire's topics. **[Check]** each question's
exact wording in the console: IARC words them by topic, not as Apple does.

### Violence

| Question | Answer | Why |
| --- | --- | --- |
| Does the game contain violence? | Yes | Every battle |
| Kind of violence | Fantasy violence against fantasy and human-like characters. Not realistic | Painted 2D figures. Foes are undead, plague creatures and constructs. The player's troops are human |
| Is the violence directed at realistic humans? | No | See above. The human troops are stylised painted figures |
| Is the player rewarded for killing? | Yes | Each kill earns mana, and fallen foes may drop courage packs |
| Blood | Yes: red and dark red, unrealistic in style. Sprays, droplets, small stains, pools under fallen bodies, smears | `BattleGore.cs`. Answer it as present, not as absent or minimal |
| Gore, dismemberment, severed body parts | No | Bodies fall and dissolve. Skeletons scatter bones |
| Depictions of death | Yes | Death animation for every unit |
| Violence against animals | Fantasy creatures only (plague beasts, ash hounds, grave rats, eels) | `data/units.json`. The player's War Hound is a legacy unit that can't be recruited |
| Sexual violence | No | |

### Fear

| Question | Answer | Why |
| --- | --- | --- |
| Frightening or horror content | Mild: undead and plague imagery in a fantasy setting | No jump scares or disturbing sequences |

### Sexuality, language, substances, humour

| Topic | Answer |
| --- | --- |
| Sexual content or nudity | No |
| Profanity, slurs or crude language | No |
| Drugs, alcohol or tobacco | No |
| Crude humour | No |
| Discriminatory content | No |

### Gambling

| Question | Answer |
| --- | --- |
| Real-money gambling | No |
| Simulated gambling | No |

### Miscellaneous and interactive elements

| Question | Answer | Why |
| --- | --- | --- |
| Can users interact or exchange content? | **Depends on rooms.** No if internet rooms are hidden. Yes if visible | Rooms show nicknames and scores. No chat |
| Does the app share the user's current location? | No | |
| Does the app share personal information with third parties? | No | `docs/STORE_PRIVACY_ANSWERS.md` |
| Digital purchases | Yes | Ten packs |
| Purchases with random items (loot boxes) | **No** | IARC counts items bought with real money, or with currency bought with real money, whose contents the player doesn't know beforehand. The Adventurer's Kit names its troop on the card, and no random item costs gold or food (relic fusion is free; see the facts table) |
| Unrestricted internet access | No | No in-app browser |
| Is the app a web browser or search engine? | No | |

### Expected IARC ratings

IARC sets each rating from the answers, so these are estimates:

| Authority | Expected | Notes |
| --- | --- | --- |
| ESRB (Americas) | **Teen** (Violence, Blood) | E10+ (Fantasy Violence, Mild Blood) is possible. The pools and sprays in every battle make Teen the safer expectation |
| PEGI (Europe) | **12** (Violence) | Violence towards fantasy characters that is a little graphic, with red blood |
| USK (Germany) | **12** | |
| ClassInd (Brazil) | **12** | |
| ACB (Australia) | **M** (likely) | Frequent fantasy violence with blood. PG is possible if IARC reads the violence as mild |
| Others (IARC generic) | **12+** | |

Interactive elements shown on the listing: **In-Game Purchases**. Add
**Users Interact** if rooms are visible.

## Google Play: target audience and content

Play Console → App content → Target audience and content.

| Question | Answer | Why |
| --- | --- | --- |
| Target age groups | **13–15, 16–17, 18 and over** | Matches the expected ratings and the privacy policy ("isn't directed at children under 13"). Leave out every group under 13 |
| Could the store listing unintentionally appeal to children? | No **[Check]** | Painted medieval fantasy with undead foes and blood. No cartoon mascots or child-directed wording |

### If the target audience included children

Choosing any group under 13 puts the app under Google Play's **Families
policy**. For this game that would mean:

1. **Content.** Everything in the app and listing must suit children. The
   blood sprays, pools and death animations in every battle would need an off
   switch for children or would have to go.
2. **Children's privacy law** (COPPA in the US, GDPR's child provisions in
   Europe). Today every player, guests included, is registered on the server
   with a player ID and nickname as soon as the server answers
   (`AccountSignIn.OnServerReachable`). The nickname is shown to others in
   rooms, email sign-in collects an address, and analytics and crash reports
   are offered. For a mixed audience the game would need a neutral age screen
   and, for children, either verifiable parental consent or those features
   switched off.
3. **SDKs.** Every SDK must be allowed in child-directed apps. **[Check]**
   Play Billing and Play Games Services against Google's Families
   requirements.
4. **Purchases.** Purchase prompts must not pressure children. **[Check]**
   the current Families policy text.
5. **Declarations.** The Data safety form, the privacy policy's children
   section and the Apple rating (Made for Kids is offered only at 4+ or 9+)
   would all have to change.

Not recommended for this release.

## Google Play: other App content declarations

| Declaration | Answer | Why |
| --- | --- | --- |
| Privacy policy | `https://crownroad.game/privacy` | |
| App access | All functionality available without special access | See [REVIEW_NOTES.md](REVIEW_NOTES.md) |
| Ads | No, the app contains no ads | No ad SDK |
| Content rating | IARC, above | |
| Target audience and content | 13 and over, above | |
| Data safety | See [PRIVACY_FORMS.md](PRIVACY_FORMS.md) | |
| Advertising ID | No, the app doesn't use it | **[Check]** the release AAB's merged manifest has no `com.google.android.gms.permission.AD_ID`. Google libraries can add it. If one does, remove it in the manifest |
| Government app | No | |
| Financial features | None. In-app packs of game currency aren't financial features | |
| Health apps | None | |
| News app | No | |
| Account deletion | `https://crownroad.game/support#delete-data`, plus in the app | Settings → Account → Delete account |
| Real-money gambling, games and contests | Not applicable | |

## Findings

1. **Nicknames have no filter and no moderation tool.** There is no word
   filter on the device or the server, no block control, and no operator tool
   to rename or ban: the `players` table's `can_join_rooms` and related flags
   are never checked, and the device sends its nickname again at each profile
   sync, overwriting any server-side change. If rooms are visible at launch,
   Apple's guideline 1.2 and IARC's "Users Interact" both apply. See
   [SUPPORT_PLAYBOOK.md](SUPPORT_PLAYBOOK.md#abuse-report-about-a-nickname).
2. **The first-run prompt overstates where nicknames appear.** "Other players
   see it on rankings and in rooms": rankings never show other players.
