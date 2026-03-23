using System;
using System.Collections.Generic;

public sealed class SeasonPassTier
{
	public int Tier { get; }
	public int XPRequired { get; }
	public string FreeRewardType { get; }
	public int FreeRewardAmount { get; }
	public string FreeRewardLabel { get; }
	public string PremiumRewardType { get; }
	public int PremiumRewardAmount { get; }
	public string PremiumRewardLabel { get; }
	public string PremiumRewardItemId { get; }

	public SeasonPassTier(int tier, int xpRequired,
		string freeRewardType, int freeRewardAmount, string freeRewardLabel,
		string premiumRewardType, int premiumRewardAmount, string premiumRewardLabel,
		string premiumRewardItemId = "")
	{
		Tier = tier;
		XPRequired = xpRequired;
		FreeRewardType = freeRewardType;
		FreeRewardAmount = freeRewardAmount;
		FreeRewardLabel = freeRewardLabel;
		PremiumRewardType = premiumRewardType;
		PremiumRewardAmount = premiumRewardAmount;
		PremiumRewardLabel = premiumRewardLabel;
		PremiumRewardItemId = premiumRewardItemId;
	}
}

public static class SeasonPassCatalog
{
	public const int MaxTier = 50;
	public const string CurrentSeasonId = "S1";

	public const int XPPerBattleWin = 10;
	public const int XPPerBounty = 25;
	public const int XPPerDailyChallenge = 50;
	public const int XPPerTowerFloor = 15;
	public const int XPPerArenaWin = 20;
	public const int XPPerExpedition = 10;

	private static readonly SeasonPassTier[] Tiers;

	static SeasonPassCatalog()
	{
		Tiers = new SeasonPassTier[MaxTier];
		for (var i = 0; i < MaxTier; i++)
		{
			var tier = i + 1;
			var xp = tier * 100; // 100, 200, ... 5000

			// Free rewards cycle: gold, food, tomes
			var (freeType, freeAmt, freeLabel) = (tier % 3) switch
			{
				1 => ("gold", 50 + tier * 10, $"{50 + tier * 10} Gold"),
				2 => ("food", 2 + tier / 5, $"{2 + tier / 5} Food"),
				_ => ("tomes", 1 + tier / 10, $"{1 + tier / 10} Tomes")
			};

			// Premium rewards cycle: essence, sigils, special at milestones
			string premType, premLabel, premItemId = "";
			int premAmt;
			if (tier % 10 == 0)
			{
				premType = "sigils";
				premAmt = 2 + tier / 10;
				premLabel = $"{premAmt} Sigils";
			}
			else if (tier % 5 == 0)
			{
				premType = "essence";
				premAmt = 3 + tier / 10;
				premLabel = $"{premAmt} Essence";
			}
			else
			{
				premType = "gold";
				premAmt = 100 + tier * 15;
				premLabel = $"{premAmt} Gold";
			}

			Tiers[i] = new SeasonPassTier(tier, xp, freeType, freeAmt, freeLabel, premType, premAmt, premLabel, premItemId);
		}
	}

	public static SeasonPassTier GetTier(int tier)
	{
		var index = tier - 1;
		return index >= 0 && index < Tiers.Length ? Tiers[index] : null;
	}

	public static IReadOnlyList<SeasonPassTier> GetAll() => Tiers;

	public static int GetTierForXP(int xp)
	{
		var tier = 0;
		var cumulative = 0;
		for (var i = 0; i < Tiers.Length; i++)
		{
			cumulative += Tiers[i].XPRequired;
			if (xp >= cumulative) tier = i + 1;
			else break;
		}
		return tier;
	}

	public static int GetXPForTier(int tier)
	{
		var total = 0;
		for (var i = 0; i < Math.Min(tier, Tiers.Length); i++)
		{
			total += Tiers[i].XPRequired;
		}
		return total;
	}
}
