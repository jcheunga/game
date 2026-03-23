using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GuildPerkDefinition
{
	public string Id { get; }
	public string Title { get; }
	public string Description { get; }
	public float HealthScale { get; }
	public float GoldBonusScale { get; }
	public float FoodBonusScale { get; }
	public float ExpeditionSpeedScale { get; }
	public float RelicLuckScale { get; }
	public int TierRequired { get; }

	public GuildPerkDefinition(string id, string title, string description,
		float healthScale, float goldBonusScale, float foodBonusScale,
		float expeditionSpeedScale, float relicLuckScale, int tierRequired)
	{
		Id = id;
		Title = title;
		Description = description;
		HealthScale = healthScale;
		GoldBonusScale = goldBonusScale;
		FoodBonusScale = foodBonusScale;
		ExpeditionSpeedScale = expeditionSpeedScale;
		RelicLuckScale = relicLuckScale;
		TierRequired = tierRequired;
	}
}

public sealed class GuildTierDefinition
{
	public int Tier { get; }
	public string Title { get; }
	public int ExperienceRequired { get; }
	public int MaxMembers { get; }

	public GuildTierDefinition(int tier, string title, int experienceRequired, int maxMembers)
	{
		Tier = tier;
		Title = title;
		ExperienceRequired = experienceRequired;
		MaxMembers = maxMembers;
	}
}

public sealed class GuildSnapshot
{
	public string GuildId { get; set; } = "";
	public string Name { get; set; } = "";
	public string LeaderProfileId { get; set; } = "";
	public int Tier { get; set; } = 1;
	public int Experience { get; set; }
	public int MemberCount { get; set; }
	public string[] ActivePerkIds { get; set; } = Array.Empty<string>();
	public int WeeklyGoalProgress { get; set; }
	public int WeeklyGoalTarget { get; set; }
	public string WeeklyGoalType { get; set; } = "";
}

public sealed class GuildMemberInfo
{
	public string ProfileId { get; set; } = "";
	public string Callsign { get; set; } = "";
	public int ContributionPoints { get; set; }
}

public readonly struct GuildBonus
{
	public float HealthScale { get; }
	public float GoldBonusScale { get; }
	public float FoodBonusScale { get; }
	public float ExpeditionSpeedScale { get; }
	public float RelicLuckScale { get; }

	public GuildBonus(float healthScale, float goldBonusScale, float foodBonusScale,
		float expeditionSpeedScale, float relicLuckScale)
	{
		HealthScale = healthScale;
		GoldBonusScale = goldBonusScale;
		FoodBonusScale = foodBonusScale;
		ExpeditionSpeedScale = expeditionSpeedScale;
		RelicLuckScale = relicLuckScale;
	}

	public static readonly GuildBonus None = new(1f, 1f, 1f, 1f, 1f);
}

public static class GuildCatalog
{
	private static readonly GuildPerkDefinition[] Perks =
	{
		new("guild_vitality", "Guild Vitality", "All units gain +5% max health.", 1.05f, 1f, 1f, 1f, 1f, 1),
		new("guild_prosperity", "Guild Prosperity", "Earn +10% gold from all sources.", 1f, 1.10f, 1f, 1f, 1f, 2),
		new("guild_provisions", "Guild Provisions", "Earn +10% food from all sources.", 1f, 1f, 1.10f, 1f, 1f, 3),
		new("guild_haste", "Guild Haste", "Expeditions complete 15% faster.", 1f, 1f, 1f, 0.85f, 1f, 4),
		new("guild_fortune", "Guild Fortune", "Relic drop chance increased by 10%.", 1f, 1f, 1f, 1f, 1.10f, 5),
	};

	private static readonly GuildTierDefinition[] Tiers =
	{
		new(1, "Warband", 0, 5),
		new(2, "Company", 500, 10),
		new(3, "Battalion", 2000, 20),
		new(4, "Legion", 5000, 30),
		new(5, "Order", 10000, 50),
	};

	public static IReadOnlyList<GuildPerkDefinition> GetAllPerks() => Perks;
	public static IReadOnlyList<GuildTierDefinition> GetAllTiers() => Tiers;

	public static GuildTierDefinition GetTier(int tier)
	{
		return Tiers.FirstOrDefault(t => t.Tier == tier) ?? Tiers[0];
	}

	public static GuildTierDefinition GetTierByExperience(int experience)
	{
		GuildTierDefinition best = Tiers[0];
		foreach (var t in Tiers)
		{
			if (experience >= t.ExperienceRequired)
			{
				best = t;
			}
		}
		return best;
	}

	public static GuildBonus ResolveBonus(GuildSnapshot guild)
	{
		if (guild == null || guild.ActivePerkIds == null || guild.ActivePerkIds.Length == 0)
		{
			return GuildBonus.None;
		}

		var h = 1f;
		var g = 1f;
		var f = 1f;
		var e = 1f;
		var r = 1f;
		foreach (var perkId in guild.ActivePerkIds)
		{
			var perk = Perks.FirstOrDefault(p => string.Equals(p.Id, perkId, StringComparison.OrdinalIgnoreCase));
			if (perk != null && guild.Tier >= perk.TierRequired)
			{
				h *= perk.HealthScale;
				g *= perk.GoldBonusScale;
				f *= perk.FoodBonusScale;
				e *= perk.ExpeditionSpeedScale;
				r *= perk.RelicLuckScale;
			}
		}

		return new GuildBonus(h, g, f, e, r);
	}
}
