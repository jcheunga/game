"""Menu background scene recipes. Each recipe(scene) builds the scene and returns post-grade options."""
from . import interior, kit_test, landmark, road, town, undead, wild

SCENES = {
    'kit_trees': kit_test.kit_trees,
    'season_pass': road.season_pass,
    'shop': town.shop,
    'bounty': town.bounty,
    'login_calendar': town.login_calendar,
    'event': town.event,
    'forge': interior.forge,
    'cash_shop': interior.cash_shop,
    'codex': interior.codex,
    'guild': interior.guild,
    'leaderboard': interior.leaderboard,
    'loadout': interior.loadout,
    'profile': interior.profile,
    'settings': interior.settings,
    'arena': landmark.arena,
    'tower': landmark.tower,
    'skill_tree': landmark.skill_tree,
    'expedition': wild.expedition,
    'friends': wild.friends,
    'endless': undead.endless,
    'raid': undead.raid,
    'battle_summary': undead.battle_summary,
    'multiplayer': road.multiplayer,
    'lan_race': road.lan_race,
}
