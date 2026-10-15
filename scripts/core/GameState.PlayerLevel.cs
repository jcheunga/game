using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>The player's level, its claimable rewards and caps, and the nickname chosen on a new caravan.</summary>
public partial class GameState
{
    public const int MinCallsignLength = 3;
    public const int MaxCallsignLength = 18;
    private readonly HashSet<int> _claimedLevelRewards = new();

    public int PlayerXP { get; private set; }
    public int PlayerLevel => PlayerLevelCatalog.LevelForXP(PlayerXP);
    public bool IsMaxPlayerLevel => PlayerLevel >= PlayerLevelCatalog.MaxLevel;
    public int PlayerXPIntoLevel => PlayerXP - PlayerLevelCatalog.TotalXPForLevel(PlayerLevel);
    public int PlayerXPForNextLevel => IsMaxPlayerLevel ? 0 : PlayerLevelCatalog.XPToNext(PlayerLevel);
    /// <summary>How far units can be trained at the current level.</summary>
    public int UnitLevelCap => PlayerLevelCatalog.UnitLevelCap(PlayerLevel);
    /// <summary>How far each war wagon upgrade can go at the current level.</summary>
    public int WagonRankCap => Math.Min(MaxPersistentBaseUpgradeLevel, PlayerLevelCatalog.WagonRankCap(PlayerLevel));
    public int ClaimableLevelRewardCount => PlayerLevelCatalog.RewardLevels.Count(CanClaimLevelReward);
    public bool HasChosenCallsign { get; private set; }
    /// <summary>A new caravan names itself once. Isolated test saves skip the prompt so drivers reach the map.</summary>
    public bool ShouldPromptForNickname => !HasChosenCallsign && !(SaveSystem.Instance?.UsesIsolatedSave ?? false);

    public bool HasClaimedLevelReward(int level) => _claimedLevelRewards.Contains(level);
    public bool CanClaimLevelReward(int level) => level <= PlayerLevel && PlayerLevelCatalog.Reward(level) != null && !_claimedLevelRewards.Contains(level);

    /// <summary>Adds XP and returns how many levels it gained.</summary>
    public int AddPlayerXP(int amount)
    {
        if (amount <= 0 || PlayerXP >= PlayerLevelCatalog.MaxXP) return 0;
        var before = PlayerLevel;
        PlayerXP = Math.Min(PlayerLevelCatalog.MaxXP, PlayerXP + amount);
        Persist();
        return PlayerLevel - before;
    }

    /// <summary>
    /// XP for a finished battle. Battles call this before applying their own results, so a campaign stage
    /// with no stars yet still counts as a first clear.
    /// </summary>
    public int AwardBattleXP(BattleRunMode mode, bool won, int stage, int progress)
    {
        var firstClear = mode == BattleRunMode.Campaign && won && GetStageStars(stage) == 0;
        var before = PlayerXP;
        AddPlayerXP(PlayerLevelCatalog.BattleXP(mode, won, stage, firstClear, progress));
        return PlayerXP - before;
    }

    public bool TryClaimLevelReward(int level, out string message)
    {
        var reward = PlayerLevelCatalog.Reward(level);
        if (reward == null) { message = "That level has no reward."; return false; }
        if (level > PlayerLevel) { message = $"Reach level {level} to claim {reward.Label}."; return false; }
        if (!_claimedLevelRewards.Add(level)) { message = $"Level {level} reward already claimed."; return false; }
        GrantLevelReward(reward);
        Persist();
        message = $"Level {level} reward: {reward.Label}.";
        LastResultMessage = message;
        return true;
    }

    /// <summary>Claims every reward the current level has reached; returns how many were claimed.</summary>
    public int ClaimAllLevelRewards()
    {
        var claimed = 0;
        foreach (var level in PlayerLevelCatalog.RewardLevels.Where(CanClaimLevelReward).ToArray())
        {
            _claimedLevelRewards.Add(level);
            GrantLevelReward(PlayerLevelCatalog.Reward(level));
            claimed++;
        }
        if (claimed > 0) { LastResultMessage = $"Claimed {claimed} level reward{(claimed == 1 ? "" : "s")}."; Persist(); }
        return claimed;
    }

    private void GrantLevelReward(PlayerLevelReward reward)
    {
        switch (reward.Type)
        {
            case "gold": Gold += reward.Amount; break;
            case "food": Food += reward.Amount; FoodChanged?.Invoke(); break;
            case "shards": RelicShards += reward.Amount; break;
            case "essence": Essence += reward.Amount; break;
            case "sigils": Sigils += reward.Amount; break;
        }
    }

    public static bool IsValidCallsign(string callsign, out string reason)
    {
        var name = callsign?.Trim() ?? "";
        reason = name.Length < MinCallsignLength ? $"Use at least {MinCallsignLength} characters."
            : name.Length > MaxCallsignLength ? $"Use {MaxCallsignLength} characters or fewer."
            : name.Any(c => !char.IsLetterOrDigit(c) && c is not (' ' or '-' or '_')) ? "Use letters, numbers, spaces, hyphens or underscores."
            : "";
        return reason.Length == 0;
    }

    /// <summary>Sets the nickname other players see, rejecting names the server would rewrite.</summary>
    public bool TryChooseCallsign(string callsign, out string reason)
    {
        if (!IsValidCallsign(callsign, out reason)) return false;
        SetPlayerCallsign(callsign);
        return true;
    }

    private void ResetPlayerLevel()
    {
        PlayerXP = 0;
        _claimedLevelRewards.Clear();
        HasChosenCallsign = false;
    }

    private void LoadPlayerLevel(GameSaveData saved)
    {
        ResetPlayerLevel();
        if (saved.Version >= 48)
        {
            PlayerXP = Math.Clamp(saved.PlayerXP, 0, PlayerLevelCatalog.MaxXP);
            HasChosenCallsign = saved.HasChosenCallsign;
            foreach (var level in saved.ClaimedLevelRewards ?? Array.Empty<int>())
                if (PlayerLevelCatalog.Reward(level) != null) _claimedLevelRewards.Add(level);
            return;
        }
        // Caravans from before player levels start at the level their campaign has earned; the rewards
        // for those levels wait on the level track. A name other than the default was a choice.
        PlayerXP = PlayerLevelCatalog.LegacyXP(saved.StageStars ?? Array.Empty<int>());
        HasChosenCallsign = saved.Version >= 16 && NormalizePlayerCallsign(saved.PlayerCallsign) != DefaultPlayerCallsign;
    }
}
