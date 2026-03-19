using System;

public static class LoadingTipCatalog
{
	private static readonly string[] Tips =
	{
		"Deploy units near the front to intercept early rushes.",
		"Spells don't cost courage at higher levels — upgrade them in the Armory.",
		"The Shield Knight blocks damage for nearby allies with its Shield Wall ability.",
		"Equip relics in the Armory to boost unit stats before battle.",
		"Combo bonuses trigger when matching unit pairs deploy near each other.",
		"The Rogue bypasses the frontline to target rear enemies directly.",
		"Stone Barricade blocks an entire lane — use it to buy time for cooldowns.",
		"War Cry buffs every deployed unit at once. Save it for big pushes.",
		"Earthquake deals more damage than Fireball across a wider area.",
		"The Necromancer raises skeletons from enemy corpses — more enemies means more minions.",
		"Banner Knight's aura boosts attack and speed for all nearby allies.",
		"Boss stages appear at the end of each district. Bring your strongest squad.",
		"Heroic Directives offer bonus rewards for replaying stages with extra challenge.",
		"Daily Challenges change every day — check Multiplayer for today's board.",
		"Food is spent to enter stages and explore new districts.",
		"Gold funds unit purchases, level-ups, spell upgrades, and war wagon improvements.",
		"The Berserker deals more damage as its health drops — high risk, high reward.",
		"War Hounds are cheap and fast. Deploy them to scout or swarm.",
		"Crossbowman has the longest range of any physical unit.",
		"Upload your save to the cloud in Settings to protect against data loss.",
		"Cycle battle speed with Space or the speed button to fast-forward easy waves.",
		"Endless mode gets harder every 15 waves. Boss checkpoints offer draft upgrades.",
		"Mage has extreme range but costs more courage than other units.",
		"The Siege Engineer can repair the war wagon during battle.",
		"Each district has a unique boss with special abilities and escort spawns.",
		"Frost Burst slows enemies in a choke point — great for buying time.",
		"Resurrect brings back your last fallen unit at half health.",
		"Polymorph turns the toughest enemy into a harmless creature for 4 seconds.",
		"Barrier Ward reduces incoming damage for allies in the warded area.",
		"Check the Loadout briefing for enemy composition before deploying.",
	};

	private static readonly Random Rng = new();

	public static string GetRandom()
	{
		return Tips[Rng.Next(Tips.Length)];
	}
}
