using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The reward a level pays once it is claimed from the level track.</summary>
public sealed record PlayerLevelReward(int Level, string Type, int Amount)
{
    public string Label => Type switch
    {
        "gold" => $"{Amount:N0} Gold",
        "food" => $"{Amount} Food",
        "shards" => $"{Amount} Shards",
        "essence" => $"{Amount} Essence",
        "sigils" => $"{Amount} Sigil{(Amount == 1 ? "" : "s")}",
        _ => $"{Amount} {Type}"
    };
}

/// <summary>A cap a level lifts: how far units can be trained, or the war wagon's upgrade rank.</summary>
public sealed record PlayerLevelUnlock(int Level, string Kind, int Value)
{
    public bool IsWagon => Kind == PlayerLevelCatalog.WagonUnlock;
    public string Label => IsWagon ? $"Wagon rank {Value}" : $"Units to level {Value}";
}

/// <summary>
/// The player's own level, 1 to 100. Every finished battle pays XP and every level has a reward to claim.
/// Some levels also lift a cap: every second level until 37 raises how far units can be trained, and
/// levels 5, 12, 20 and 30 raise how far the war wagon's upgrades go. The caps keep saved-up gold from
/// out-levelling the early campaign. Clearing each stage once keeps the unit cap at or (mostly two or
/// three) above <see cref="CampaignProgressionCatalog.SuggestedLevel"/> and ends the campaign around
/// level 46; the rest of the track comes from replays and the other modes.
/// </summary>
public static class PlayerLevelCatalog
{
    public const int MaxLevel = 100;
    public const string UnitUnlock = "units", WagonUnlock = "wagon";
    public const int StartingUnitCap = 2, StartingWagonRank = 1;
    private static readonly int[] WagonRankLevels = { 5, 12, 20, 30 };
    private static readonly int[] TotalXP = BuildTotals();

    private static int[] BuildTotals()
    {
        var totals = new int[MaxLevel + 1];
        for (var level = 2; level <= MaxLevel; level++) totals[level] = totals[level - 1] + XPToNext(level - 1);
        return totals;
    }

    /// <summary>XP needed to go from <paramref name="level"/> to the next one: 90, then 5 more each level.</summary>
    public static int XPToNext(int level) => 90 + 5 * (Math.Clamp(level, 1, MaxLevel) - 1);
    /// <summary>Total XP at which <paramref name="level"/> is reached.</summary>
    public static int TotalXPForLevel(int level) => TotalXP[Math.Clamp(level, 1, MaxLevel)];
    public static int MaxXP => TotalXP[MaxLevel];

    public static int LevelForXP(int xp)
    {
        var level = 1;
        while (level < MaxLevel && xp >= TotalXP[level + 1]) level++;
        return level;
    }

    public static int WagonRankCap(int level) => StartingWagonRank + WagonRankLevels.Count(at => at <= level);

    public static int UnitLevelCap(int level) =>
        Math.Min(UnitBalance.MaxPlayerLevel, StartingUnitCap + (Math.Clamp(level, 1, MaxLevel) - 1) / 2);

    /// <summary>The first player level that allows training a unit to <paramref name="unitLevel"/>.</summary>
    public static int LevelForUnitCap(int unitLevel)
    {
        for (var level = 1; level < MaxLevel; level++) if (UnitLevelCap(level) >= unitLevel) return level;
        return MaxLevel;
    }

    /// <summary>The first player level that allows wagon upgrades of <paramref name="rank"/>.</summary>
    public static int LevelForWagonRank(int rank)
    {
        for (var level = 1; level < MaxLevel; level++) if (WagonRankCap(level) >= rank) return level;
        return MaxLevel;
    }

    /// <summary>The caps this level lifts (level 1 lists the starting caps); most levels lift none.</summary>
    public static IReadOnlyList<PlayerLevelUnlock> Unlocks(int level)
    {
        if (level < 1 || level > MaxLevel) return Array.Empty<PlayerLevelUnlock>();
        var unlocks = new List<PlayerLevelUnlock>();
        var cap = UnitLevelCap(level);
        if (level == 1 || cap > UnitLevelCap(level - 1)) unlocks.Add(new(level, UnitUnlock, cap));
        if (level == 1 || WagonRankLevels.Contains(level)) unlocks.Add(new(level, WagonUnlock, WagonRankCap(level)));
        return unlocks;
    }

    /// <summary>
    /// Every level pays: a welcome purse at level 1, sigils every tenth level, relic shards every other
    /// fifth, essence every other fourth, food every other third and gold on the rest.
    /// </summary>
    public static PlayerLevelReward Reward(int level)
    {
        if (level < 1 || level > MaxLevel) return null;
        if (level == 1) return new(level, "gold", 100);
        if (level % 10 == 0) return new(level, "sigils", 1 + level / 25);
        if (level % 5 == 0) return new(level, "shards", 2 + level / 10);
        if (level % 4 == 0) return new(level, "essence", 2 + level / 25);
        if (level % 3 == 0) return new(level, "food", 6 + level / 10);
        return new(level, "gold", 40 + 10 * level);
    }

    public static IEnumerable<int> RewardLevels => Enumerable.Range(1, MaxLevel);

    /// <summary>A campaign stage pays most on its first win; replays and defeats still move the bar.</summary>
    public static int CampaignXP(int stage, bool won, bool firstClear) =>
        won ? firstClear ? 40 + stage : 15 + stage / 3 : 8 + stage / 10;

    /// <summary>
    /// XP for a finished battle. <paramref name="progress"/> is the wave reached (endless), commanders
    /// defeated (boss rush) or floor (tower); campaign battles use <paramref name="stage"/>.
    /// </summary>
    public static int BattleXP(BattleRunMode mode, bool won, int stage, bool firstClear, int progress) => mode switch
    {
        BattleRunMode.Campaign => CampaignXP(stage, won, firstClear),
        BattleRunMode.Endless => Math.Min(250, 10 + 4 * Math.Max(0, progress)),
        BattleRunMode.BossRush => Math.Min(220, 10 + 15 * Math.Max(0, progress)),
        BattleRunMode.Tower => won ? 40 + Math.Max(0, progress) : 12,
        BattleRunMode.Arena => won ? 60 : 20,
        BattleRunMode.SeasonalEvent => won ? 60 : 12,
        _ => won ? 40 : 12
    };

    /// <summary>XP for a save from before player levels: every won campaign stage counts as a first clear.</summary>
    public static int LegacyXP(IReadOnlyList<int> stageStars)
    {
        var xp = 0;
        for (var i = 0; i < stageStars.Count; i++) if (stageStars[i] > 0) xp += CampaignXP(i + 1, true, true);
        return Math.Min(xp, MaxXP);
    }
}
