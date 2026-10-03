"""Battlefield fallback recipes, one per TerrainId (data/stages.json). recipe(scene) -> post-grade options."""
from . import routes_a, routes_b, routes_c, steppe

TERRAINS = {
    # King's Road
    'urban': routes_a.urban,
    'highway': routes_a.highway,
    'night': routes_a.night_town,
    # Saltwake Docks
    'industrial': routes_a.industrial,
    'shipyard': routes_a.shipyard,
    'swamp': routes_a.swamp,
    # Emberforge March
    'railyard': routes_a.railyard,
    'smelter': routes_a.smelter,
    'foundry': routes_a.foundry,
    # Ashen Ward
    'checkpoint': routes_b.checkpoint,
    'decon': routes_b.decon,
    'lab': routes_b.lab,
    'blacksite': routes_b.blacksite,
    # Thornwall Pass
    'pass': routes_b.pass_,
    'shrine': routes_b.shrine,
    'watchfort': routes_b.watchfort,
    # Hollow Basilica
    'cathedral': routes_b.cathedral,
    'ossuary': routes_b.ossuary,
    'reliquary': routes_b.reliquary,
    # Mire of Saints
    'marsh': routes_c.marsh,
    'chapel': routes_c.chapel,
    'ferry': routes_c.ferry,
    # Sunfall Steppe
    'grassland': steppe.grassland,
    'siegecamp': steppe.siegecamp,
    'waystation': steppe.waystation,
    # Gloamwood Verge
    'grove': routes_c.grove,
    'timberroad': routes_c.timberroad,
    'witchcircle': routes_c.witchcircle,
    # Crownfall Citadel
    'bridgefort': routes_c.bridgefort,
    'breachyard': routes_c.breachyard,
    'innerkeep': routes_c.innerkeep,
}
