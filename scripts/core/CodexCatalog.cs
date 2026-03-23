using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CodexEntry
{
	public string Id { get; }
	public string Category { get; }
	public string Title { get; }
	public string LoreText { get; }
	public string StatSummary { get; }

	public CodexEntry(string id, string category, string title, string loreText, string statSummary = "")
	{
		Id = id;
		Category = category;
		Title = title;
		LoreText = loreText;
		StatSummary = statSummary;
	}
}

public static class CodexCatalog
{
	private static readonly CodexEntry[] Entries =
	{
		// ── Player Units (16) ──
		new("player_brawler", "unit", "Swordsman", "A steadfast blade from the king's levy. Reliable in the vanguard, unyielding under pressure."),
		new("player_shooter", "unit", "Archer", "Keen-eyed bowmen trained in the royal marches. They thin the horde before it reaches the wall."),
		new("player_defender", "unit", "Shield Knight", "Sworn protectors of the caravan. Their tower shields turn aside even siege bolts."),
		new("player_spear", "unit", "Spearman", "Long-reach infantry who anchor the line. A wall of steel points no ghoul can simply rush through."),
		new("player_ranger", "unit", "Crossbowman", "Methodical marksmen who trade speed for stopping power. Each bolt is placed with surgical precision."),
		new("player_raider", "unit", "Cavalry Rider", "Mounted lancers who ride hard and strike deep. They exploit gaps in the enemy formation."),
		new("player_breacher", "unit", "Halberdier", "Heavy-armed soldiers who cleave through armoured foes. Their reach and power make them ideal siege-breakers."),
		new("player_coordinator", "unit", "Battle Monk", "Warrior-priests who mend the wounded and bolster morale. Their chants carry the light of the old faith."),
		new("player_marksman", "unit", "Mage", "Arcane scholars who channel raw elemental force. Devastating at range, fragile in melee."),
		new("player_mechanic", "unit", "Siege Engineer", "Tinkers and sappers who deploy war machines. Their turrets extend the caravan's reach."),
		new("player_grenadier", "unit", "Alchemist", "Volatile mixers of fire and acid. Their flasks shatter ranks and leave burning ground."),
		new("player_hound", "unit", "War Hound", "Fast, disposable scouts released ahead of the column. They harry stragglers and buy precious seconds."),
		new("player_banner", "unit", "Banner Knight", "Standard-bearers whose aura rallies allies. Nearby troops fight harder and move faster."),
		new("player_necromancer", "unit", "Necromancer", "Forbidden casters who raise the fallen. Each enemy corpse becomes a shambling ally."),
		new("player_rogue", "unit", "Rogue", "Shadow-stepping assassins who bypass the frontline. They eliminate priority targets before vanishing."),
		new("player_berserker", "unit", "Berserker", "Frenzy warriors who grow stronger as they bleed. High risk, devastating reward."),

		// ── Enemies (16) ──
		new("enemy_walker", "enemy", "Risen", "Shambling corpses dragged from shallow graves. Slow and witless, but endless in number."),
		new("enemy_runner", "enemy", "Ghoul", "Fleet-footed carrion eaters driven by hunger. They sprint past the vanguard to claw at the wagon."),
		new("enemy_heavy", "enemy", "Grave Brute", "Bloated undead packed with necrotic mass. Each swing from their bone clubs can stagger a shield wall."),
		new("enemy_exploder", "enemy", "Rot Hulk", "Walking pustules that detonate on death. Keep them at range or pay the price in flesh."),
		new("enemy_ranged", "enemy", "Blight Caster", "Skeletal mages who hurl bolts of decay. Their volleys corrode armour and sap morale."),
		new("enemy_splitter", "enemy", "Bone Nest", "A rattling ossuary that bursts into a swarm of minions when struck down. Kill it fast or be overwhelmed."),
		new("enemy_rusher", "enemy", "Sapper", "Fast suicide bombers that sprint for the war wagon. A single breach can end the march."),
		new("enemy_buffer", "enemy", "Dread Herald", "Dark standard-bearers whose aura emboldens nearby undead. Prioritise these or face a hardened horde."),
		new("enemy_hexer", "enemy", "Hexer", "Cursed witches who jam deploy signals and scramble card cooldowns. Silence them quickly."),
		new("enemy_tank", "enemy", "Bone Juggernaut", "Armoured colossi sheathed in layered bone plate. They shrug off arrows and require concentrated fire."),
		new("enemy_shieldwall", "enemy", "Shield Wall", "Skeletal phalanxes that absorb projectiles for nearby allies. Break them in melee or flank."),
		new("enemy_lich", "enemy", "Lich", "Ancient sorcerers who periodically reanimate the fallen. Every second they stand, the dead rise again."),
		new("enemy_siegetower", "enemy", "Siege Tower", "Lumbering bone structures that deploy a wave of enemies directly at the caravan. Must be destroyed en route."),
		new("enemy_mirror", "enemy", "Mirror Knight", "Cursed knights that reflect 30% of incoming damage. Strike carefully or wound yourself."),
		new("enemy_tunneler", "enemy", "Tunneler", "Burrowing horrors that surface behind the front line. They tear into rear units and support casters."),
		new("enemy_crusher", "enemy", "Crusher", "Massive undead spawned only by the Dread Sovereign. They pulverise anything in their path."),

		// ── Bosses (10) ──
		new("boss_grave_lord", "boss", "Grave Lord", "Ruler of King's Road necropolis. Commands legions of risen from a throne of bone."),
		new("boss_tidecaller", "boss", "Tidecaller", "A drowned sorcerer who commands the tides of Saltwake. Water and death obey in equal measure."),
		new("boss_iron_warden", "boss", "Iron Warden", "The immortal guardian of Emberforge. Encased in living iron, he endures all punishment."),
		new("boss_plague_archon", "boss", "Plague Archon", "High priest of the Ashen Ward. His plagues melt armour and rot courage."),
		new("boss_thornwall", "boss", "Thornwall Chieftain", "Warlord of the pass. His thorn-wrapped warriors are half beast, half briar."),
		new("boss_bone_pontiff", "boss", "Bone Pontiff", "Undead cardinal of the Hollow Basilica. His sermons raise cathedrals of bone."),
		new("boss_mire_behemoth", "boss", "Mire Behemoth", "A swamp titan festooned with rot and moss. Each step shakes the earth."),
		new("boss_steppe_warlord", "boss", "Steppe Warlord", "Horse-lord of the Sunfall steppe. Lightning reflexes and brutal mounted charges."),
		new("boss_gloamwood_witch", "boss", "Gloamwood Witch", "Mistress of the twilight forest. She weaves illusions that drive men mad."),
		new("boss_dread_sovereign", "boss", "Dread Sovereign", "The final enemy. Lord of Crownfall Citadel. All undead answer to his will."),

		// ── Spells (10) ──
		new("spell_fireball", "spell", "Fireball", "A roaring sphere of flame that detonates on impact. The simplest and most reliable war spell."),
		new("spell_heal", "spell", "Heal", "Channel restorative light into wounded allies. A Battle Monk's staple incantation."),
		new("spell_frost", "spell", "Frost Burst", "A freezing wave that slows all enemies in its radius. Buys critical seconds for the vanguard."),
		new("spell_lightning", "spell", "Lightning Strike", "A bolt from the heavens that chains between clustered foes. Devastating against packed formations."),
		new("spell_barrier", "spell", "Barrier Ward", "An arcane shield that absorbs incoming damage. A moment of invulnerability for the front line."),
		new("spell_barricade", "spell", "Stone Barricade", "Raise a wall of conjured stone that blocks the enemy advance. Temporary but tactically vital."),
		new("spell_warcry", "spell", "War Cry", "Rally all deployed units with a burst of speed and fury. A tide-turning battle shout."),
		new("spell_earthquake", "spell", "Earthquake", "Crack the earth beneath the horde. Wide-area damage and crippling slow."),
		new("spell_polymorph", "spell", "Polymorph", "Transform the toughest foe into a harmless creature. A surgical answer to elite threats."),
		new("spell_resurrect", "spell", "Resurrect", "Bring back the last fallen ally at half health. Death is not always the end."),

		// ── Relics (20) ──
		new("relic_iron_pendant", "relic", "Iron Pendant", "A simple talisman of hammered iron. Steady and dependable."),
		new("relic_sharpened_edge", "relic", "Sharpened Edge", "A whetstone charm that keeps blades keen. Subtle but deadly."),
		new("relic_swift_boots", "relic", "Swift Boots", "Enchanted greaves that quicken the wearer's stride."),
		new("relic_battle_drum", "relic", "Battle Drum", "A miniature war drum that steadies the heartbeat in combat."),
		new("relic_wolftooth_charm", "relic", "Wolftooth Charm", "A fang on a leather cord. The beast's fury lives within."),
		new("relic_guardian_shield", "relic", "Guardian Shield", "A buckler blessed by the Temple Guard. Turns aside lethal blows."),
		new("relic_war_brand", "relic", "War Brand", "A sigil-marked blade fragment. Its edge never dulls."),
		new("relic_windrunner_cloak", "relic", "Windrunner Cloak", "A cloak woven from gale threads. The wearer moves like the wind."),
		new("relic_sages_ring", "relic", "Sage's Ring", "A silver band inscribed with formulae. Spells flow faster through its wearer."),
		new("relic_tombstone_shard", "relic", "Tombstone Shard", "A fragment of consecrated gravestone. It repels the unquiet dead."),
		new("relic_stormcaller_sigil", "relic", "Stormcaller Sigil", "A crackling rune that channels storm energy into every strike."),
		new("relic_siege_hammer", "relic", "Siege Hammer", "A dwarven-forged war mallet. It shatters bone plate like kindling."),
		new("relic_crown_of_valor", "relic", "Crown of Valor", "A circlet worn by fallen kings. Its wearer fights with royal fury."),
		new("relic_blade_of_ruin", "relic", "Blade of Ruin", "A cursed sword that trades the wielder's blood for devastating power."),
		new("relic_phantom_mantle", "relic", "Phantom Mantle", "A ghostly shroud that lets attacks pass through — sometimes."),
		new("relic_dragon_heart", "relic", "Dragon Heart", "A petrified dragon's heart. It pulses with ancient fire."),
		new("relic_spectral_lantern", "relic", "Spectral Lantern", "A lantern lit with ghostfire. Nearby allies resist dark magic."),
		new("relic_immortal_wreath", "relic", "Immortal Wreath", "A crown of undying vines. The wearer endures beyond mortal limits."),
		new("relic_frostbound_crown", "relic", "Frostbound Crown", "A circlet of eternal ice. Chills foes and steels the bearer."),
		new("relic_moonfire_talisman", "relic", "Moonfire Talisman", "A blood-red charm that burns brightest under the harvest moon."),
	};

	private static readonly Dictionary<string, CodexEntry> ById;

	static CodexCatalog()
	{
		ById = new Dictionary<string, CodexEntry>(StringComparer.OrdinalIgnoreCase);
		foreach (var e in Entries)
		{
			ById[e.Id] = e;
		}
	}

	public static IReadOnlyList<CodexEntry> GetAll() => Entries;

	public static CodexEntry GetById(string id)
	{
		return ById.TryGetValue(id, out var entry) ? entry : null;
	}

	public static IReadOnlyList<CodexEntry> GetByCategory(string category)
	{
		return Entries.Where(e => string.Equals(e.Category, category, StringComparison.OrdinalIgnoreCase)).ToArray();
	}

	public static int TotalEntries => Entries.Length;
}
