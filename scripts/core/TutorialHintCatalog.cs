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
		},
		new()
		{
			Id = "codex_hint",
			Title = "Codex",
			Body = "The Codex records every enemy, unit, spell, and relic you encounter. Defeat enemies to fill kill counts and unlock lore.",
			TriggerContext = "first_codex"
		},
		new()
		{
			Id = "skill_tree_hint",
			Title = "Skill Trees",
			Body = "Each unit has a talent tree with 5 nodes. Unlock nodes with gold and tomes to gain permanent stat bonuses.",
			TriggerContext = "first_skill_tree"
		},
		new()
		{
			Id = "arena_hint",
			Title = "PvP Arena",
			Body = "Challenge other players' squads in asynchronous PvP. Win to climb the ranks from Bronze to Diamond.",
			TriggerContext = "first_arena"
		},
		new()
		{
			Id = "guild_hint",
			Title = "Warband",
			Body = "Join or create a guild. Guild perks provide stat bonuses, expedition speed, and relic luck for all members.",
			TriggerContext = "first_guild"
		},
		new()
		{
			Id = "hard_mode_hint",
			Title = "Hard Mode",
			Body = "Replay campaign stages with harder enemies and forced modifiers. Earn +50% rewards and exclusive hardened relics at milestones.",
			TriggerContext = "hard_mode_unlock"
		},
		new()
		{
			Id = "enchantment_hint",
			Title = "Enchantments",
			Body = "Apply enchantments to relics for bonus combat effects. Essence is earned from arena wins, guild contributions, and hard mode.",
			TriggerContext = "forge_enchant"
		},
		new()
		{
			Id = "raid_hint",
			Title = "Weekly Raid",
			Body = "A community-wide boss rotates each week. All players contribute damage. Hit milestones to earn rewards for everyone.",
			TriggerContext = "first_raid"
		},
		new()
		{
			Id = "bounty_hint",
			Title = "Bounty Board",
			Body = "Complete 3 daily bounties for gold, food, tomes, and essence. Bounties reset each day.",
			TriggerContext = "first_bounty"
		},
		new()
		{
			Id = "tower_hint",
			Title = "Challenge Tower",
			Body = "Climb 100 floors of increasing difficulty. Each floor has unique modifiers and rewards. Exclusive relics await at milestones.",
			TriggerContext = "first_tower"
		},
		new()
		{
			Id = "friends_hint",
			Title = "Friends",
			Body = "Add friends by profile ID. Send up to 3 daily gifts of gold and food.",
			TriggerContext = "first_friends"
		},
		new()
		{
			Id = "mastery_hint",
			Title = "Mastery",
			Body = "Units earn mastery XP from deploys and kills. Higher mastery ranks grant small permanent stat bonuses.",
			TriggerContext = "first_mastery"
		},
		new()
		{
			Id = "login_calendar_hint",
			Title = "Login Calendar",
			Body = "Claim a daily reward from the login calendar. Rewards escalate over the month. Resets each month.",
			TriggerContext = "first_login_calendar"
		},
		new()
		{
			Id = "wagon_skin_hint",
			Title = "War Wagon Skins",
			Body = "Unlock cosmetic war wagon skins through milestones. Equip them in Settings to change your caravan's appearance in battle.",
			TriggerContext = "first_wagon_skin"
		},
		new()
		{
			Id = "notification_hint",
			Title = "Notifications",
			Body = "Check the badge counts on menu buttons for unclaimed rewards, completed expeditions, and available bounties.",
			TriggerContext = "first_notification"
		},
		new()
		{
			Id = "awakening_hint",
			Title = "Unit Awakening",
			Body = "Spend unit tokens and gold to star-upgrade units. Each star grants a permanent stat bonus. Tokens drop from bosses and expeditions.",
			TriggerContext = "first_awakening"
		},
		new()
		{
			Id = "season_pass_hint",
			Title = "Season Pass",
			Body = "Earn Season XP from battles, bounties, and challenges. Claim free and premium rewards as you climb 50 tiers.",
			TriggerContext = "first_season_pass"
		},
		new()
		{
			Id = "mutator_hint",
			Title = "Battle Mutators",
			Body = "Toggle mutators before battle to change the rules. Harder mutators increase gold rewards. Stack them for maximum risk and reward.",
			TriggerContext = "first_mutator"
		},
		new()
		{
			Id = "accessibility_hint",
			Title = "Accessibility",
			Body = "Colorblind modes, reduced motion, auto-battle, and large text are available in Settings.",
			TriggerContext = "first_accessibility"
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
