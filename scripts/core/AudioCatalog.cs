using System.Collections.Generic;

/// <summary>
/// Which sound-effect cue each unit, weapon, projectile, death and ability uses. Cue ids are the files rendered by
/// art/audio/build_sfx.py into assets/sfx (see sfx.json); this class only chooses between them.
/// </summary>
public static class AudioCatalog
{
	// Sprite motion profile -> the whoosh of the swing and the cue for the blow landing.
	private static readonly Dictionary<string, (string Swing, string Hit)> MeleeByProfile = new()
	{
		["sword-cut"] = ("swing_light", "hit_blade"),
		["guard-cut"] = ("swing_light", "hit_blade"),
		["royal-cleave"] = ("swing_heavy", "hit_blade"),
		["axe-cleave"] = ("swing_heavy", "hit_blade"),
		["heavy-smash"] = ("swing_heavy", "hit_heavy"),
		["spear-thrust"] = ("thrust", "hit_pierce"),
		["mounted-lance"] = ("thrust", "hit_lance"),
		["blade-stab"] = ("swing_dagger", "hit_blade"),
		["claw-rake"] = ("swing_claw", "hit_claw"),
		["pounce"] = ("swing_claw", "hit_bite"),
		["staff-strike"] = ("swing_heavy", "hit_blunt"),
		["kick-smash"] = ("swing_light", "hit_blunt"),
		["stock-thrust"] = ("thrust", "hit_blunt"),
		["staff-cast"] = ("swing_heavy", "hit_blunt"),
		["hammer-command"] = ("swing_heavy", "hit_blunt"),
		["bow-draw"] = ("swing_light", "hit_blunt"),
		["crossbow"] = ("thrust", "hit_blunt"),
		["ballista"] = ("swing_heavy", "hit_blunt"),
		["bombard"] = ("swing_heavy", "hit_blunt"),
		["flask-toss"] = ("swing_light", "hit_blunt"),
		["nest-pulse"] = ("", "hit_bone"),
		["siege-deploy"] = ("", "hit_heavy"),
	};

	// Projectile style id -> (launch cue, impact cue).
	private static readonly Dictionary<string, (string Launch, string Impact)> ByStyle = new()
	{
		["arrow"] = ("bow_release", "impact_arrow"),
		["bolt"] = ("crossbow_release", "impact_arrow"),
		["ballista_bolt"] = ("ballista_release", "impact_heavy_bolt"),
		["bone_bolt"] = ("bone_bolt_release", "impact_heavy_bolt"),
		["harpoon"] = ("harpoon_release", "impact_heavy_bolt"),
		["frost_shard"] = ("cast_frost", "impact_frost"),
		["flask"] = ("flask_throw", "impact_glass"),
		["plague_pot"] = ("pot_throw", "impact_splash"),
		["firepot"] = ("pot_throw", "impact_fire"),
		["cog"] = ("cog_throw", "impact_metal"),
		["blight_glob"] = ("cast_blight", "impact_splash"),
		["soul_skull"] = ("cast_soul", "impact_soul"),
		["necrotic_skull"] = ("cast_soul", "impact_soul"),
		["arcane"] = ("cast_arcane", "impact_arcane"),
		["holy"] = ("cast_holy", "impact_holy"),
		["hex"] = ("cast_hex", "impact_hex"),
		["water"] = ("cast_water", "impact_water"),
		["relic"] = ("cast_relic", "impact_fire"),
		["storm"] = ("cast_storm", "impact_lightning"),
		["orb"] = ("cast_arcane", "impact_arcane"),
	};

	// Enemy voices heard when they take the field (and now and then while they advance).
	private static readonly Dictionary<string, string> EnemyVoice = new()
	{
		["enemy_walker"] = "risen_groan", ["enemy_runner"] = "ghoul_shriek", ["enemy_tunneler"] = "ghoul_shriek",
		["enemy_bloater"] = "hulk_gurgle", ["enemy_brute"] = "brute_roar", ["enemy_crusher"] = "giant_roar",
		["enemy_catacomb_giant"] = "giant_roar", ["enemy_spitter"] = "caster_hiss", ["enemy_plague_engine"] = "plague_engine",
		["enemy_splitter"] = "bone_nest_crack", ["enemy_saboteur"] = "sapper_fuse", ["enemy_howler"] = "herald_howl",
		["enemy_revenant_captain"] = "captain_shout", ["enemy_jammer"] = "hex_whisper", ["enemy_lich"] = "lich_wail",
		["enemy_siegetower"] = "siege_tower_roll", ["enemy_mirror"] = "mirror_hum", ["enemy_shieldwall"] = "deploy_heavy",
		["enemy_boneballista"] = "deploy_engine",
	};

	// Each grave lord roars in its lineage's voice.
	private static readonly Dictionary<string, string> BossRoar = new()
	{
		["enemy_boss"] = "boss_roar_grave", ["enemy_boss_docks"] = "boss_roar_tide", ["enemy_boss_tidemaster"] = "boss_roar_tide",
		["enemy_boss_forge"] = "boss_roar_iron", ["enemy_boss_ward"] = "boss_roar_plague", ["enemy_boss_plague_monarch"] = "boss_roar_plague",
		["enemy_boss_pass"] = "boss_roar_beast", ["enemy_boss_mire"] = "boss_roar_beast", ["enemy_boss_basilica"] = "boss_roar_pontiff",
		["enemy_boss_reliquary"] = "boss_roar_pontiff", ["enemy_boss_steppe"] = "boss_roar_warlord", ["enemy_boss_verge"] = "boss_roar_witch",
		["enemy_boss_citadel"] = "boss_roar_sovereign", ["enemy_boss_ashen_regent"] = "boss_roar_sovereign",
	};

	// Player active abilities -> the cues that announce them (played together).
	private static readonly Dictionary<string, string[]> AbilityCues = new()
	{
		["swordsman_cleave"] = new[] { "swing_heavy", "ability_flourish" },
		["archer_volley"] = new[] { "base_arrows" },
		["shield_knight_wall"] = new[] { "ability_shield" },
		["spearman_thrust"] = new[] { "thrust", "ability_flourish" },
		["crossbow_snipe"] = new[] { "crossbow_release", "ability_flourish" },
		["cavalry_charge"] = new[] { "ability_charge" },
		["mage_beam"] = new[] { "ability_beam" },
		["halberdier_sweep"] = new[] { "swing_heavy", "ability_flourish" },
		["alchemist_bomb"] = new[] { "flask_throw" },
		["monk_blessing"] = new[] { "ability_blessing" },
		["hound_howl"] = new[] { "hound_howl" },
		["banner_inspire"] = new[] { "ability_inspire" },
		["rogue_vanish"] = new[] { "ability_vanish" },
		["berserker_frenzy"] = new[] { "berserker_roar", "ability_flourish" },
		["lantern_guard_bulwark"] = new[] { "ability_shield" },
		["ballista_anchor_shot"] = new[] { "ballista_release" },
		["stormcaller_overcharge"] = new[] { "ability_overcharge" },
	};

	private static readonly HashSet<string> SkeletalClasses = new() { "walker", "splitter", "crusher", "howler", "necromancer", "runner", "brute", "saboteur", "jammer", "mirror", "shield" };

	public static string Swing(string motionProfile) => MeleeByProfile.TryGetValue(motionProfile ?? "", out var cues) ? cues.Swing : "swing_light";

	public static string Hit(string motionProfile) => MeleeByProfile.TryGetValue(motionProfile ?? "", out var cues) ? cues.Hit : "hit_blunt";

	public static string Launch(string styleId) => ByStyle.TryGetValue(styleId ?? "", out var cues) ? cues.Launch : "cast_arcane";

	public static string Impact(string styleId) => ByStyle.TryGetValue(styleId ?? "", out var cues) ? cues.Impact : "impact_arcane";

	public static string EnemyVoiceFor(string definitionId) => EnemyVoice.TryGetValue(definitionId ?? "", out var cue) ? cue : "";

	public static string BossRoarFor(string definitionId)
	{
		if (BossRoar.TryGetValue(definitionId ?? "", out var cue)) return cue;
		return "boss_roar_grave";
	}

	public static bool IsFinalBoss(string definitionId) => definitionId is "enemy_boss_citadel" or "enemy_boss_ashen_regent";

	public static string[] Ability(string abilityId) => AbilityCues.TryGetValue(abilityId ?? "", out var cues) ? cues : new[] { "ability_flourish" };

	/// <summary>Undead whose blows should rattle bone when struck.</summary>
	public static bool IsSkeletal(bool enemy, string visualClass) => enemy && SkeletalClasses.Contains(visualClass ?? "");

	/// <summary>Deploy foley for a caravan unit: kit, footing and voice that fit the soldier.</summary>
	public static string[] Deploy(string definitionId, string visualClass) => definitionId switch
	{
		"player_shooter" or "player_ranger" => new[] { "deploy_archer" },
		"player_grenadier" => new[] { "deploy_infantry" },
		"player_raider" => new[] { "deploy_cavalry" },
		"player_rogue" => new[] { "deploy_rogue" },
		"player_mechanic" or "player_ballista" => new[] { "deploy_engine" },
		"player_coordinator" => new[] { "deploy_holy" },
		"player_stormcaller" or "player_marksman" or "player_necromancer" => new[] { "deploy_caster" },
		"player_hound" => new[] { "deploy_hound" },
		"player_berserker" => new[] { "deploy_infantry", "berserker_roar" },
		_ => visualClass is "shield" or "banner" ? new[] { "deploy_heavy" } : new[] { "deploy_infantry" },
	};

	/// <summary>The moment of death, from the death performance's effect and the fallen unit.</summary>
	public static string[] Death(string fx, bool playerSide, string definitionId, string visualClass) => fx switch
	{
		"bones" => new[] { "death_bones" },
		"ash" => new[] { "death_risen" },
		"gas" => new[] { "death_gas" },
		"embers" => new[] { "death_embers" },
		"water" => new[] { "death_water" },
		"glass" => new[] { "death_glass" },
		"debris" => new[] { "death_debris" },
		"alchemy" => new[] { "death_alchemy" },
		"storm" => new[] { playerSide ? "death_soldier" : "death_risen", "impact_lightning" },
		"arcane" => new[] { playerSide ? "death_soldier" : "death_risen", "impact_soul" },
		"holy" => new[] { playerSide ? "death_soldier" : "death_bones", "impact_holy" },
		_ when definitionId == "player_hound" => new[] { "death_hound" },
		_ when !playerSide => new[] { "death_risen" },
		_ when visualClass is "shield" or "banner" or "berserker" => new[] { "death_heavy" },
		_ => new[] { "death_soldier" },
	};

	/// <summary>The body reaching the ground on the death clip's impact frame.</summary>
	public static string BodyFall(string fx, bool heavy) => fx is "bones" or "ash" ? "body_fall_bones" : heavy ? "body_fall_heavy" : "body_fall_light";

	/// <summary>Ambience profile key for a scene (battle and loadout use the zone's own soundscape).</summary>
	public static string AmbienceContext(string scenePath, string routeId) => scenePath switch
	{
		SceneRouter.BattleScene or SceneRouter.LoadoutScene when !string.IsNullOrEmpty(routeId) => routeId,
		SceneRouter.ShopScene or SceneRouter.CashShopScene => "shop",
		SceneRouter.EndlessScene => "endless",
		SceneRouter.MultiplayerScene => "multiplayer",
		_ => "home",
	};
}
