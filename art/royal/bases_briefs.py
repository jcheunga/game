"""Design briefs for the painted enemy strongholds (art/royal/bases.py): one look per zone, one stronghold per stage.

A zone's look keeps its ten strongholds related; each stage's brief takes its form from the stage's own name and
theme. Size grows through the zone: outposts first, the boss stage's stronghold grandest of all.
"""

ZONE_LOOK = {
    "city": "King's Road, a sunlit kingdom of farms and market towns at golden hour: honey-coloured limestone, blue-grey "
            "slate cone roofs, half-timbering, red clay tiles, oak doors with black iron straps.",
    "harbor": "Saltwake Docks, a storm-worn harbour: weathered grey-green sea-wall stone streaked with salt, tarred black "
              "timber piers and pilings, rope rigging, fishing nets, barnacles, rusted iron chains, verdigris copper.",
    "foundry": "Emberforge March, a land of forges under a burning sky: soot-blackened brick, riveted dark iron plates, "
               "copper pipes, smoking chimney stacks, furnace mouths glowing molten orange, coal and slag heaps.",
    "quarantine": "Ashen Ward, a walled plague district under grey skies: cold grey stone and lime-washed timber, boarded "
                  "and shuttered windows, chained doors, yellow warning flags, painted warning crosses, beaked "
                  "plague-mask carvings, drifting ash and censer smoke.",
    "thornwall": "Thornwall Pass, a snowbound mountain pass in clear cold light: pale granite, heavy snow on steep timber "
                 "roofs, pine-log palisades, icicles, frost, banks of drifted snow.",
    "basilica": "Hollow Basilica, a ruined cathedral district at violet dusk: pale weathered marble and dark slate, "
                "pointed gothic arches, flying buttresses, stained glass glowing violet and gold, candles, carved "
                "saints, ossuary bone ornament.",
    "mire": "Mire of Saints, a drowned marsh in hazy golden-green light: rotting dark timber on stilts, moss-covered grey "
            "stone, hanging moss and willow, reeds, plank boardwalks, murky water at the footings, hanging lanterns.",
    "steppe": "Sunfall Steppe, open grassland under a vast red sunset: sun-bleached timber, stretched hide and canvas, "
              "earthen ramparts, wagons, horse-skull standards, long war banners, dry golden grass.",
    "gloamwood": "Gloamwood Verge, a haunted forest at blue moonlit dusk: gnarled black-barked oak, woven thorn brambles, "
                 "mossy timber, carved totems, hanging bone charms, glowing blue fungi.",
    "citadel": "Crownfall Citadel, the fallen royal capital under a crimson storm sky: dark grey granite, red-tiled conical "
               "turrets, massive bastions, royal statuary, fire and drifting ash.",
}

# Tallest point of the stronghold, as a fraction of the image height from the top, by its place in the zone.
SIZE = {
    "small": "It is a modest outpost: its tallest point is about 35-40% of the image height below the top, and its "
             "main building is about 25% of the image width wide.",
    "medium": "It is a sturdy fort: its tallest point is about 25-30% of the image height below the top, and its main "
              "building is about 30% of the image width wide.",
    "large": "It is a strong fortress: its tallest point is about 15-20% of the image height below the top, and its "
             "main building is about 35% of the image width wide.",
    "grand": "It is the zone's final, grandest stronghold: its tallest point is about 5-8% of the image height below "
             "the top, and its main building is about 40% of the image width wide; imposing and magnificent.",
}


def size_of(stage):
    place = (stage - 1) % 10 + 1
    return "small" if place <= 3 else "medium" if place <= 6 else "large" if place <= 9 else "grand"


# stage -> (title, brief). Every stronghold has a gate or door at road level, where its troops come out.
BASES = {
    # King's Road
    1: ("Far Gate", "the kingdom's outer road gate: a squat two-storey limestone gate tower with an oak double gate, a "
                    "slate cone-roofed corner turret, a short crenellated curtain wall trailing back, hay carts and "
                    "barrels heaped into a barricade at its foot; the Host's banner hangs from the parapet."),
    2: ("Stone Causeway", "a causeway blockhouse: a low limestone blockhouse with a slate roof over a fortified archway "
                          "gate, timber hoardings along its top, a row of sharpened stakes along the causeway's edge, "
                          "a few milestones and a toppled road shrine."),
    3: ("Lamplighters Row", "a row of tall narrow half-timbered townhouses barricaded into a fort: windows boarded, a "
                            "gate built from stacked doors and an upturned cart, iron street lanterns relit with green "
                            "witch-fire, a lamplighter's tower with a glass lantern cupola."),
    4: ("Market Ward", "the market ward's fortified guildhall: a broad stone-and-timber market hall with a stepped clock "
                       "gable, its arcaded ground floor walled up except for one heavy gate, market stalls torn down "
                       "and nailed into barricades, striped awnings shredded and hung as the Host's banners."),
    5: ("Tollhouse Square", "the old tollhouse: a stout octagonal limestone toll tower with a slate roof, a striped "
                            "barrier pole swung across the gate, iron-banded strongboxes, a weighing beam, a "
                            "crenellated wall with arrow slits."),
    6: ("Lantern Requiem", "a funeral lantern shrine turned fort: a tall slender limestone lantern tower (a lantern of "
                           "the dead) rising behind a walled churchyard gate, hundreds of candles and green lanterns, "
                           "mourning wreaths, wrought-iron grave railings."),
    7: ("Chapel Steps", "a fortified chapel at the top of broad stone steps: a round-arched limestone chapel with a rose "
                        "window and a bell gable, its iron-reinforced doors as the gate, grave markers beside the "
                        "steps, the Host's banners draped over the porch."),
    8: ("Bellgrave Siege", "a breached town wall patched by the Host: a battered limestone curtain wall and a squat drum "
                           "tower, timber hoardings, siege ladders lashed into spiked barricades, a broken trebuchet "
                           "arm, a cemetery's iron gate set in as the gate."),
    9: ("Belfry Approach", "the outer barbican of the bell tower: a tall limestone barbican with machicolations and a "
                           "portcullis gate, two pepper-pot turrets, a small belfry with a hanging bronze bell on a "
                           "side tower, chains across the walls."),
    10: ("Bell Tower Gate", "the King's Road's great bell-tower gatehouse, the zone's final stronghold: a tall limestone "
                            "gate fortress with a soaring belfry crowned by a pointed slate spire, a huge cracked "
                            "bronze bell visible in its open arcade, an arched portcullis gate, twin flanking towers "
                            "with cone roofs, long tattered skull banners, green witch-fire lanterns along its "
                            "battlements; imposing and grand."),
    # Saltwake Docks
    11: ("Mooring Ring", "a dockside watch house: a squat stone harbour watch house on a timber pier, a giant iron "
                         "mooring ring and bollards, coils of rope, crates and barrels stacked into a barricade, a tall "
                         "lantern pole."),
    12: ("Drowned Quay", "a half-drowned quay warehouse: a sagging stone-and-timber warehouse with a seaweed-draped "
                         "loading door as the gate, waterline stains, rotten pilings, fishing nets strung up like "
                         "banners, crab pots piled into a wall."),
    13: ("Netmenders Wharf", "a fortified net loft: a tall tarred-timber net loft on stilts with drying nets hung like "
                             "curtains, a gate of lashed oars and an upturned boat hull, fish-drying racks, a barnacled "
                             "stone footing."),
    14: ("Chainlift Yard", "a chain-lift tower: a massive grey stone tower housing a giant capstan, enormous rusted "
                           "harbour-chain links draped across its front, a riveted iron gate, a timber crane boom "
                           "with hooks."),
    15: ("Wreck Admiral", "a fort built from a beached warship: the broken hull of a great galleon hauled ashore, its "
                          "carved stern castle facing the road with gun ports, a gate cut into the hull, a broken "
                          "mast with torn sails as banners, a skull figurehead."),
    16: ("Ropewalk", "a ropewalk fort: a long low stone ropewalk shed with a tiled roof ending in a stout gatehouse, "
                     "giant rope spools and hemp bales stacked into walls, hanging rope nooses, a hoist."),
    17: ("Deadwake Armada", "a sea fort of lashed ships: rotting ship hulls lashed together against a stone quay into "
                            "a bastion, gangplanks and boarding nets, ship's lanterns burning green, tattered sails "
                            "daubed with the skull."),
    18: ("Gull Point", "a lighthouse fort: a stout stone lighthouse with a green witch-fire lantern room at its top, a "
                       "walled keeper's yard with a gate, gull-spattered rocks, a ship's bell on a post."),
    19: ("Salt Chains", "a salt-works fort: salt-crusted stone storehouses with chained gates, great hanging chains and "
                        "cargo hooks, white salt heaps and evaporation pans, a timber winch tower."),
    20: ("Leviathan Wake", "the Saltwake sea fortress, the zone's final stronghold: a grand grey-stone sea-gate fortress "
                           "with tall round towers capped in verdigris copper roofs, a huge portcullis water gate, the "
                           "colossal jawbone and ribs of a leviathan arching over the gate, chains and harpoons, green "
                           "lanterns; imposing and grand."),
    # Emberforge March
    21: ("Forge Siding", "a fortified rail siding depot: a sooty brick engine shed with an arched riveted iron door as "
                         "the gate, ore carts on rails piled into a barricade, an iron water tower, a coal tipple."),
    22: ("Smelter Row", "a smelter house: a squat soot-black brick smelter with a tall smoking chimney, glowing furnace "
                        "mouths, slag heaps, an iron-plated gate."),
    23: ("Coal Yard", "a coal yard stockade: heaped mountains of coal behind a timber-and-iron stockade, a coal chute "
                      "tower, mine carts, a gate of riveted plates, black coal dust everywhere."),
    24: ("Cinder Causeway", "a furnace gatehouse on a slag causeway: a black basalt-and-iron gatehouse, braziers heaped "
                            "with orange cinders, iron grilles glowing red-hot, steam venting from cracks."),
    25: ("Bellows Hall", "the bellows hall: a large brick-and-timber hall with enormous leather forge bellows on its "
                         "facade, iron pipes, smoking vents, a hammered-iron gate."),
    26: ("Forge Crown", "a forge fort crowned with chimneys: a brick keep whose battlements are a ring of smoking "
                        "chimney stacks, channels of glowing molten metal, anvils and chains at its foot."),
    27: ("Slag Run", "a slag-wall fortress: walls of black glassy slag blocks veined with glowing orange cracks, a "
                     "crucible hoist, slag carts on rails, a heavy iron gate."),
    28: ("Molten Ascension", "a tiered foundry: stepped brick terraces climbing upward with a furnace on each tier, "
                             "molten metal pouring from level to level, iron stairs, an iron gate at its base."),
    29: ("Crucible Gate", "the crucible gate: a massive iron gate flanked by two giant crucibles tilted on chains, "
                          "spilling glowing metal into channels, riveted iron towers, plumes of steam."),
    30: ("Furnace Crown", "the Emberforge furnace citadel, the zone's final stronghold: a towering iron-and-brick "
                          "fortress built around a colossal blast furnace glowing white-orange at its heart, a crown "
                          "of chimneys belching smoke and sparks, riveted iron towers, a huge portcullis of iron bars, "
                          "molten channels; imposing and grand."),
    # Ashen Ward
    31: ("Outer Ward", "a ward checkpoint: a timber gatehouse with a lifting barrier, yellow warning flags, lime-washed "
                       "walls painted with warning crosses, a beaked-mask signboard, stacked coffins as a barricade."),
    32: ("Purge Cloister", "a purge cloister: a stone cloister wall with its arches bricked up, censers and smoke pots "
                           "burning along the top, iron braziers, scorched stone, a cloister gate."),
    33: ("Lime Pits", "a lime works: low pale-stone lime kilns with smoking vents, sacks of quicklime piled into walls, "
                      "everything dusted white, a timber gate with chained boards."),
    34: ("Leechcourt", "a fortified physician's house: a tall narrow stone-and-timber apothecary house with glass leech "
                       "jars glowing green in its windows, a copper alchemical still on the roof, a gate of iron bars."),
    35: ("Black Vault Seal", "a sealed vault gate: a squat black-stone vault entrance with a huge round iron door "
                             "covered in wax seals and chains, carved warding sigils, rows of candles."),
    36: ("Cordon Line", "a cordon wall: a timber palisade wall with two watchtowers, yellow warning flags, a barred "
                        "gate, lime-washed warning crosses, spiked barricades."),
    37: ("Ashen Remnant", "a burnt-out ward hall: a fire-gutted stone hall with charred roof beams, drifts of ash, "
                          "still-smoking ruins, its blackened door barred as the gate."),
    38: ("Fever Wards", "a fortified infirmary: a long grey stone infirmary with rows of shuttered windows, a bell "
                        "turret, sheets hanging from a line, a heavy iron-banded oak door, sickly green lamplight."),
    39: ("Sealed Infirmary", "a sealed hospice: a stone hospice with every window bricked up, its doors nailed shut "
                             "with planks and chains, warding candles, a cracked plague-doctor statue by the gate."),
    40: ("Pale Crown", "the Plague Monarch's pale citadel, the zone's final stronghold: a tall pale-grey stone fortress "
                       "with bone-white domes and spires, yellow-green plague smoke pouring from censers on its "
                       "battlements, a huge beaked plague-mask carving over the gate, chains, sickly green windows; "
                       "imposing and grand."),
    # Thornwall Pass
    41: ("Narrow Ascent", "a pass blockhouse: a small log-and-granite blockhouse wedged against a rock outcrop, a log "
                          "gate, snow drifts, sharpened pine stakes."),
    42: ("Rime Switchback", "a frost watch post: a granite tower with a snow-covered timber hoarding, long icicles, a "
                            "rimed timber gate, a pine-log fence."),
    43: ("Frost Cairns", "a cairn wall: a dry-stone wall of frosted granite cairns and standing stones, a timber gate "
                         "hung with antler trophies, small skull cairns."),
    44: ("Avalanche Shrine", "a fortified mountain shrine: a granite shrine with a carved stone guardian, strings of "
                             "prayer flags, a heavy snow-shed roof, an iron gate."),
    45: ("Ice Bridge", "an ice bridge gatehouse: granite towers overgrown with blue ice crystals, a gate at the end of a "
                       "bridge of frozen ice, icicles and frost."),
    46: ("High Watch", "a high watch fort: a tall granite watchtower with a signal fire platform, timber galleries, a "
                       "snow-laden roof, a gate at its foot."),
    47: ("Signal Ridge", "a signal beacon fort: a stone fort with a great beacon brazier burning green on a tall pyre "
                         "tower, war-horn stands, snow on every ledge."),
    48: ("Whiteout Bastion", "a snow bastion: thick frost-white stone bastion walls half-buried in snowdrifts, a pointed "
                             "gate, blown snow streaming off its parapets."),
    49: ("Gatehouse Scree", "a gatehouse on scree: a granite gatehouse built into a slope of loose scree, "
                            "boulder-filled log cribs as walls, a log gate."),
    50: ("Thornwall Gate", "the Thornwall itself, the zone's final stronghold: a great fortified wall-gate spanning the "
                           "pass, massive granite towers with steep snow-covered roofs, a huge timber-and-iron gate, "
                           "frozen thorn vines and icicles, a giant war horn mounted on the wall, green beacon fires; "
                           "imposing and grand."),
    # Hollow Basilica
    51: ("Outer Nave", "a ruined church front: a broken gothic facade with a tall pointed doorway as the gate, a cracked "
                       "rose window, collapsed buttresses, rubble at its foot."),
    52: ("Ossuary Court", "an ossuary gate: a gateway wall patterned with stacked skulls and long bones, bone "
                          "chandeliers, an iron gate."),
    53: ("Bone Choir", "a bone choir loft: a chapel wall faced with organ pipes made of bones, a choir balcony, skull "
                       "niches, violet candlelight."),
    54: ("Choir Ruin", "a ruined choir: half-collapsed gothic choir arches with tall lancet windows, ivy, a doorway "
                       "barricaded with broken pews."),
    55: ("Sepulcher Crown", "a sepulcher: a domed marble mausoleum topped with a crown-shaped lantern, bronze doors, "
                            "sarcophagi, angels with broken wings."),
    56: ("Candle Vault", "a candle vault: a crypt entrance crusted with mountains of melted wax and thousands of burning "
                         "candles, an iron gate."),
    57: ("Reliquary Steps", "a reliquary stair: a grand marble stair rising to a gilded reliquary chapel door, reliquary "
                            "caskets, saint statues on plinths."),
    58: ("Crypt Gallery", "a crypt gallery: a long arcade of gothic arches opening onto tombs behind iron grilles, a "
                          "gate in the arcade."),
    59: ("Reliquary Nave", "a reliquary nave: a tall gothic nave front with gilded reliquary shrines, glowing stained "
                           "glass, skull-adorned buttresses."),
    60: ("Reliquary Cataclysm", "the Reliquary Tyrant's cathedral, the zone's final stronghold: a towering ruined "
                                "gothic cathedral fortress with twin spires, a huge glowing violet rose window, flying "
                                "buttresses hung with chains and bones, colossal pointed bronze doors as the gate, "
                                "gilded reliquaries; imposing and grand."),
    # Mire of Saints
    61: ("Bog Causeway", "a causeway gate: a rotting dark-timber gate tower on stilts where the plank causeway leaves "
                         "the bog, sagging walkways, mossy logs lashed into a wall, hanging lanterns, a timber gate."),
    62: ("Drowned Chapel", "a half-sunken chapel: a small mossy stone chapel tilted and half sunk in the marsh, a bell "
                           "gable with a rusted bell, reeds round its walls, its iron-studded door as the gate."),
    63: ("Reedbank", "a reed fort: a palisade of bundled reeds and sharpened stakes, a thatched watch hut on stilts, "
                     "eel traps and nets, a gate of woven reed panels."),
    64: ("Plague Ferry", "a ferry landing: a fortified timber ferry house on pilings with a big barge winch and ropes, "
                         "yellow plague flags, a rotting ferry barge moored at its side, a gate onto the landing."),
    65: ("Lantern Ferry", "a lantern ferry: a timber ferry house hung with dozens of glowing lanterns, a pier of "
                          "lantern posts, a slipway gate, ferry poles and ropes."),
    66: ("Saints' Mire", "a saints' shrine: moss-covered stone statues of saints around a walled shrine with a "
                         "pointed gate, guttering candles, a weeping willow draped over the wall."),
    67: ("Sunken Graves", "a sunken graveyard keep: tilted gravestones and a stone crypt house sinking into the mud, "
                          "rusted iron grave fences, a crypt door as the gate, lanterns on poles."),
    68: ("Blackwater Procession", "a processional gate: a tall timber archway hung with censers and black funeral "
                                  "banners over a boardwalk, coffins raised on stilts, candles."),
    69: ("Bellwater", "a bell keep: a mossy stone tower rising from the water with a big bell in its open top, a rope "
                      "bridge, a timber landing and gate at its foot."),
    70: ("Mire Bell", "the Mire Bell sanctuary, the zone's final stronghold: a great moss-draped stone cathedral-keep "
                      "rising out of the swamp on massive stilts and roots, a huge cracked bell in an open belfry, "
                      "hanging moss and willow, green lanterns, a heavy drowned gate; imposing and grand."),
    # Sunfall Steppe
    71: ("Burned Waystation", "a burned waystation: a charred timber coaching inn with a scorched sign and a "
                              "stable-yard wall, smoke still rising, a gate of blackened planks."),
    72: ("Ash Grass", "an earthwork post: a raised earthen rampart topped by a sun-bleached timber stockade, "
                      "horse-skull totems on poles, a gate between two log towers."),
    73: ("Dry Wells", "a well fort: a stone well house with a big wooden windlass inside a low dry-stone wall, water "
                      "barrels and troughs, a gate."),
    74: ("Siege Ring", "a siege camp ring: war wagons chained together into a laager with hide-covered sides, tents "
                       "behind, siege-engine parts, a wagon pulled across the gap as the gate."),
    75: ("Tent Lines", "a tent-line fort: large hide-and-canvas war tents behind a stake fence, long war banners, a "
                       "gate of crossed spears and shields."),
    76: ("Sunfall Redoubt", "a redoubt: an earthen redoubt with a timber revetment, gabions and a timber gate tower "
                            "flying long banners."),
    77: ("Horse Barrows", "a barrow fort: grassy burial mounds with a stone-lined barrow entrance as the gate, horse "
                          "skulls on poles, weathered standing stones."),
    78: ("Warhorn Expanse", "a warhorn tower: a tall lashed-timber tower with a giant war horn mounted on top, long "
                            "banners, a stockade gate at its foot."),
    79: ("Warhorn Ridge", "a ridge fort: a palisaded camp with great war drums, a wheeled siege tower and hide "
                          "awnings, a gate of lashed logs."),
    80: ("Sunfall Warcamp", "the Steppe Warlord's war camp, the zone's final stronghold: a colossal mobile fortress of "
                            "lashed siege towers and giant war wagons with hide-covered timber walls, a towering war "
                            "horn, horse-skull standards, great banners, a spiked palisade, a huge timber gate; "
                            "imposing and grand."),
    # Gloamwood Verge
    81: ("Thorn Verge", "a thorn hedge gate: a high wall of woven black thorn brambles with a gnarled timber gate, "
                        "bone charms and skulls hung in the thorns."),
    82: ("Witch Circle", "a witch circle: mossy standing stones linked by thorny vines, a crooked witch hut with a "
                         "sagging roof, a bubbling cauldron, a gate between two stones."),
    83: ("Fungal Hollow", "a hollow tree fort: a huge hollow tree stump crusted with glowing blue mushrooms, a round "
                          "door in the trunk, shelf fungi stairs."),
    84: ("Blackbark Road", "a blackbark gate: a log gatehouse of black-barked timber across the road, carved animal "
                           "totems, hanging bone charms."),
    85: ("Charcoal Camp", "a charcoal burners' camp: smoking earth-covered charcoal mounds, timber huts, a stockade "
                          "with a gate, stacked cordwood."),
    86: ("Snare Grove", "a snare grove fort: trees lashed together with ropes and snares, hanging iron cages, a gate "
                        "of bound saplings."),
    87: ("Hanging Oaks", "an oak fortress: platforms and huts built into gnarled oaks, rope bridges, hanging "
                         "lanterns, a gate between the roots."),
    88: ("Moonless Snare", "a witch-thorn tower: a crooked timber tower wrapped in black thorns, witch lanterns, a "
                           "gate of thorn-wrapped planks."),
    89: ("Heartwood Path", "a heartwood gate: two giant entwined trees forming an arched gate, runes glowing in the "
                           "bark, roots like walls."),
    90: ("Gloamwood Heart", "the Gloamwood Witch's heart-tree, the zone's final stronghold: a colossal ancient black oak "
                            "grown into a fortress, its roots forming walls and a great gate, witch houses in its "
                            "branches, glowing blue fungi and green witch lanterns, thorn vines, skull charms; imposing "
                            "and grand."),
    # Crownfall Citadel
    91: ("Bridge Bastion", "a bridge bastion: an angular dark-granite bastion where a great stone bridge reaches the "
                           "citadel, a raised drawbridge gate on chains, red-tiled turret."),
    92: ("Breach Yard", "a breach repair: a breached citadel wall plugged with rubble, timber and broken siege "
                        "engines, a gate set in the gap, scorch marks."),
    93: ("Barbican Steps", "a barbican: a tall dark-granite barbican above a flight of broad steps, machicolations, a "
                           "portcullis gate, red-tiled pepper-pot turrets."),
    94: ("Crownward Gate", "the crownward gate: a gatehouse crowned with carved stone crowns, a portcullis, twin "
                           "round towers with red conical roofs."),
    95: ("Crownfall Keep", "a keep: a massive square dark-granite keep with corner turrets, arrow slits and a heavy "
                           "gate, banners of the Host."),
    96: ("Siege Yard", "a siege yard: a gatehouse with siege engines (a trebuchet arm and a ballista) looming over "
                       "the wall behind it."),
    97: ("Inner Ring", "the inner ring wall: a tall curtain wall with round towers under red conical roofs and a "
                       "deep gate arch."),
    98: ("Crownward Wall", "the crownward wall: a huge wall lined with headless royal statues in niches, a gate, "
                           "burning braziers."),
    99: ("Throne Approach", "the throne approach: a grand ceremonial gate at the top of broad stairs, flanked by giant "
                            "statues of kings, long crimson banners."),
    100: ("Ashen Throne", "the Ashen Throne, the final stronghold of the campaign: the burning royal palace-keep, a "
                          "towering dark granite fortress with soaring spires, a giant iron crown atop it, falling "
                          "ash, crimson banners, a huge gate; the grandest of all."),
}
