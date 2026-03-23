using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class RaidBossMilestone
{
	public int DamageThreshold { get; }
	public string RewardType { get; }
	public string RewardItemId { get; }
	public int RewardAmount { get; }
	public string Label { get; }

	public RaidBossMilestone(int damageThreshold, string rewardType, string rewardItemId, int rewardAmount, string label)
	{
		DamageThreshold = damageThreshold;
		RewardType = rewardType;
		RewardItemId = rewardItemId;
		RewardAmount = rewardAmount;
		Label = label;
	}
}

public sealed class RaidBossDefinition
{
	public string Id { get; }
	public string BossName { get; }
	public string BossUnitId { get; }
	public long TotalHealthPool { get; }
	public RaidBossMilestone[] Milestones { get; }

	public RaidBossDefinition(string id, string bossName, string bossUnitId,
		long totalHealthPool, RaidBossMilestone[] milestones)
	{
		Id = id;
		BossName = bossName;
		BossUnitId = bossUnitId;
		TotalHealthPool = totalHealthPool;
		Milestones = milestones;
	}
}

public static class RaidBossCatalog
{
	private static readonly RaidBossDefinition[] Bosses =
	{
		new("raid_grave_lord", "Grave Lord Ascendant", "boss_grave_lord", 10_000_000,
			new[]
			{
				new RaidBossMilestone(2_000_000, "gold", "", 500, "20% — Outer defenses breached"),
				new RaidBossMilestone(4_000_000, "essence", "", 3, "40% — Inner sanctum reached"),
				new RaidBossMilestone(6_000_000, "gold", "", 1000, "60% — Throne room exposed"),
				new RaidBossMilestone(8_000_000, "essence", "", 5, "80% — Grave Lord weakened"),
				new RaidBossMilestone(10_000_000, "relic", "relic_raid_grave_crown", 1, "100% — Grave Lord vanquished"),
			}),
		new("raid_iron_warden", "Iron Warden Eternal", "boss_iron_warden", 12_000_000,
			new[]
			{
				new RaidBossMilestone(2_400_000, "gold", "", 600, "20% — Forge gates sundered"),
				new RaidBossMilestone(4_800_000, "essence", "", 3, "40% — Anvil shattered"),
				new RaidBossMilestone(7_200_000, "gold", "", 1200, "60% — Iron skin cracking"),
				new RaidBossMilestone(9_600_000, "essence", "", 6, "80% — Core exposed"),
				new RaidBossMilestone(12_000_000, "relic", "relic_raid_iron_heart", 1, "100% — Iron Warden destroyed"),
			}),
		new("raid_dread_sovereign", "Dread Sovereign Reborn", "boss_dread_sovereign", 15_000_000,
			new[]
			{
				new RaidBossMilestone(3_000_000, "gold", "", 800, "20% — Citadel walls breached"),
				new RaidBossMilestone(6_000_000, "essence", "", 4, "40% — Honor guard fallen"),
				new RaidBossMilestone(9_000_000, "gold", "", 1500, "60% — Crown cracked"),
				new RaidBossMilestone(12_000_000, "essence", "", 8, "80% — Sovereign faltering"),
				new RaidBossMilestone(15_000_000, "relic", "relic_raid_sovereign_mantle", 1, "100% — Dread Sovereign vanquished"),
			}),
		new("raid_plague_archon", "Plague Archon Supreme", "boss_plague_archon", 11_000_000,
			new[]
			{
				new RaidBossMilestone(2_200_000, "gold", "", 550, "20% — Plague ward breached"),
				new RaidBossMilestone(4_400_000, "essence", "", 3, "40% — Pestilence contained"),
				new RaidBossMilestone(6_600_000, "gold", "", 1100, "60% — Archon's shield broken"),
				new RaidBossMilestone(8_800_000, "essence", "", 5, "80% — Corruption purged"),
				new RaidBossMilestone(11_000_000, "relic", "relic_raid_plague_censer", 1, "100% — Plague Archon cleansed"),
			}),
	};

	public static IReadOnlyList<RaidBossDefinition> GetAll() => Bosses;

	public static string GetCurrentWeekId()
	{
		var now = DateTime.UtcNow;
		var cal = CultureInfo.InvariantCulture.Calendar;
		var week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
		return $"{now.Year}-W{week:D2}";
	}

	public static RaidBossDefinition GetForWeek(string weekId)
	{
		if (string.IsNullOrWhiteSpace(weekId))
		{
			return Bosses[0];
		}

		var hash = 0;
		foreach (var c in weekId)
		{
			hash = hash * 31 + c;
		}

		return Bosses[Math.Abs(hash) % Bosses.Length];
	}

	public static RaidBossDefinition GetCurrentBoss()
	{
		return GetForWeek(GetCurrentWeekId());
	}
}
