using System;

public static class LoadingTipCatalog
{
	private static readonly string[] Tips =
	{
		"Units deploy from the wagon. Keep an affordable melee card ready for early rushes.",
		"Magic runs on mana, earned by felling enemies. Fill the bar to open the war wagon's Arcane Barrage.",
		"The Shield Knight periodically intercepts a projectile aimed at a nearby ally.",
		"Equip relics in the Armory to boost unit stats before battle.",
		"Combo bonuses trigger when matching unit pairs deploy near each other.",
		"The Rogue threatens exposed support troops and gains a strike bonus after Vanish.",
		"Stone Barricade buys time for nearby troops and cooldowns.",
		"War Cry buffs every deployed unit at once. Save it for big pushes.",
		"Earthquake deals more damage than Fireball across a wider area.",
		"The Field Medic heals one injured ally at a time. Protect its patient and its channel.",
		"Banner Knight's aura boosts attack and speed for all nearby allies.",
		"Boss stages appear at the end of each district. Bring your strongest squad.",
		"Daily Challenges change every day — check Multiplayer for today's board.",
		"Travel and map supplies are free. Starting or restarting a campaign battle costs 4 food.",
		"Gold funds unit purchases, level-ups, spell upgrades, and war wagon improvements.",
		"The Berserker deals more damage as its health drops — high risk, high reward.",
		"Militia deploys two bodies with one payment. Keep another melee card ready behind them.",
		"Longbowman delivers a strong shot while stationary; protect its firing position.",
		"Upload your save to the cloud in Settings to protect against data loss.",
		"Cycle battle speed with Space or the speed button to fast-forward easy waves.",
		"Endless mode gets harder every 15 waves. Boss checkpoints offer draft upgrades.",
		"Mage has extreme range but costs more courage than other units.",
		"The Caravan Engineer repairs the wagon and maintains a temporary bolt emplacement.",
		"Each district has a unique boss with special abilities and escort spawns.",
		"Frost Burst slows enemies in a choke point — great for buying time.",
		"Resurrect brings back your last fallen unit at half health.",
		"Polymorph briefly disables the toughest enemy in range. Bosses resist repeated control.",
		"Barrier Ward reduces incoming damage for allies in the warded area.",
		"Check the Loadout briefing for enemy composition before deploying.",
	};

	private static readonly Random Rng = new();

	public static string GetRandom()
	{
		return Tips[Rng.Next(Tips.Length)];
	}
}
