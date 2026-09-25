# Adventure map

The campaign is a freely explorable medieval atlas. All ten districts are available to explore, with 50 regular leaders and ten regional bosses. Five terrain paintings, six rival portraits, and a miniature atlas supply its artwork.

## Playing

- Click terrain to move the caravan anywhere on the map, including into fog. Drag to pan; scroll or use the zoom controls to zoom.
- Use Explore to travel toward a nearby undiscovered location, or the district selector to explore another region. All travel is free.
- Challenge regular leaders in any order. A defeat or retreat does not block other encounters. Battle entry still costs food.
- Each district's final boss requires victories over its five regular leaders. The boss marker shows a lock and the number defeated. Visits and repeat victories do not replace missing victories.
- Select chests and wagons to collect their resources once. Passing them does not automatically collect them.
- Watchtowers reveal more terrain and hidden treasure. Each visited shrine adds three starting courage in campaign battles in its district.

Fog clears along the caravan's actual movement and remains explored. Movement goes directly to the chosen destination without forcing visits to intervening leaders. Higher-stage victories advance existing armory unlocks. Post-campaign prestige and hard mode require defeating the final campaign boss.

## Saves

Save version 41 records the caravan's world position and a bounded grid of explored terrain, as well as claimed sites. Older saves retain progress, claims, and their previously revealed road. Bosses already defeated in older saves remain replayable. Leaving the map during travel saves the actual position and grants no uncollected destination reward.

## Validation

Run `bash scripts/smoke/adventure_map_smoke.sh` with Godot Mono and .NET on the path. The real-engine suite covers free movement, fog discovery, independent encounters, defeat/retreat alternatives, boss gates, real regular/boss battle entry, shrine effects, reward protection, interrupted travel, full save reloads, older saves, and layout across all districts. It uses an isolated test save.

Artwork was generated with the built-in image generation tool. Exact prompts and asset paths are recorded in [adventure-art.json](adventure-art.json).
