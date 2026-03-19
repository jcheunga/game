using System.Collections.Generic;
using System.Linq;

public sealed class TutorialHint
{
	public string Id;
	public string Title;
	public string Body;
	public string TriggerContext; // "first_battle", "first_shop", "first_spell_unlock", "first_boss", etc.
}

public static class TutorialHintCatalog
{
	private static readonly TutorialHint[] Hints =
	{
		new()
		{
			Id = "courage_basics",
			Title = "Courage",
			Body = "Courage fills over time. Spend it to deploy units and cast spells. Don't hoard \u2014 deploy early to hold the line.",
			TriggerContext = "first_battle"
		},
		new()
		{
			Id = "cooldown_basics",
			Title = "Cooldowns",
			Body = "Each unit card has a cooldown after deployment. The dark overlay shows remaining time.",
			TriggerContext = "first_battle"
		},
		new()
		{
			Id = "spell_basics",
			Title = "Spells",
			Body = "Click a spell button then click the battlefield to cast. Spells cost courage and have cooldowns.",
			TriggerContext = "first_spell_unlock"
		},
		new()
		{
			Id = "deck_building",
			Title = "Squad Building",
			Body = "Pick 3 units and up to 3 spells. Matching squad tags (Frontline, Recon, etc.) activates synergy bonuses.",
			TriggerContext = "first_loadout"
		},
		new()
		{
			Id = "shop_basics",
			Title = "The Armory",
			Body = "Buy and upgrade units, spells, and base upgrades here. Gold funds everything.",
			TriggerContext = "first_shop"
		},
		new()
		{
			Id = "food_costs",
			Title = "Food",
			Body = "Entering a stage costs food. Earn food from victories and exploration.",
			TriggerContext = "first_map"
		},
		new()
		{
			Id = "relic_basics",
			Title = "Relics",
			Body = "Equip relics on units in the Armory to boost their stats. Earn relics from boss kills and district completion.",
			TriggerContext = "first_relic"
		},
		new()
		{
			Id = "combo_basics",
			Title = "Combo Pairs",
			Body = "Some unit pairs get bonus stats when deployed near each other. Check loadout for active combos.",
			TriggerContext = "first_combo"
		},
		new()
		{
			Id = "active_ability",
			Title = "Active Abilities",
			Body = "Units at level 4+ gain a special ability that triggers automatically in combat.",
			TriggerContext = "first_ability_unlock"
		},
		new()
		{
			Id = "boss_warning",
			Title = "Boss Encounter",
			Body = "Bosses rally nearby undead and spawn escorts. Focus fire the boss or deal with the adds first.",
			TriggerContext = "first_boss"
		},
		new()
		{
			Id = "endless_basics",
			Title = "Endless Mode",
			Body = "Survive escalating waves. Choose boons at the start, draft upgrades at checkpoints, pick route forks for risk vs reward.",
			TriggerContext = "first_endless"
		},
		new()
		{
			Id = "daily_challenge",
			Title = "Daily Challenge",
			Body = "A new challenge rotates every day. Compete for the best score on a shared board.",
			TriggerContext = "first_multiplayer"
		},
		new()
		{
			Id = "difficulty_hint",
			Title = "Difficulty",
			Body = "Change difficulty in Settings. Higher difficulty means tougher enemies but better gold rewards.",
			TriggerContext = "first_settings"
		},
		new()
		{
			Id = "barricade_hint",
			Title = "Stone Barricade",
			Body = "The barricade blocks enemy movement but crumbles after a few seconds. Place it to buy time.",
			TriggerContext = "first_barricade_spell"
		},
		new()
		{
			Id = "polymorph_hint",
			Title = "Polymorph",
			Body = "Transforms the toughest enemy in range. Use it on bosses, crushers, or mirror knights to neutralize threats.",
			TriggerContext = "first_polymorph_spell"
		}
	};

	public static IReadOnlyList<TutorialHint> GetAll() => Hints;

	public static TutorialHint GetById(string id)
	{
		foreach (var hint in Hints)
		{
			if (hint.Id == id)
			{
				return hint;
			}
		}

		return null;
	}

	public static TutorialHint[] GetByContext(string triggerContext)
	{
		return Hints.Where(h => h.TriggerContext == triggerContext).ToArray();
	}
}
