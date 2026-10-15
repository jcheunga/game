using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Player levels: the XP curve, the unit and wagon caps they lift, claims, saves and nicknames.
public partial class CombatReviewSmoke
{
    private void CheckPlayerLevels()
    {
        var max = PlayerLevelCatalog.MaxLevel;
        Check(Enumerable.Range(1, max).All(level => PlayerLevelCatalog.LevelForXP(PlayerLevelCatalog.TotalXPForLevel(level)) == level)
              && Enumerable.Range(2, max - 1).All(level => PlayerLevelCatalog.LevelForXP(PlayerLevelCatalog.TotalXPForLevel(level) - 1) == level - 1),
            "Each level begins exactly at its XP total");
        Check(PlayerLevelCatalog.UnitLevelCap(1) == 2 && PlayerLevelCatalog.WagonRankCap(1) == 1
              && PlayerLevelCatalog.UnitLevelCap(max) == UnitBalance.MaxPlayerLevel && PlayerLevelCatalog.WagonRankCap(max) == 5,
            "Caps start low and open fully before the last level");
        Check(Enumerable.Range(2, max - 1).All(level => PlayerLevelCatalog.UnitLevelCap(level) >= PlayerLevelCatalog.UnitLevelCap(level - 1)
              && PlayerLevelCatalog.WagonRankCap(level) >= PlayerLevelCatalog.WagonRankCap(level - 1)),
            "Caps never fall as the level rises");
        Check(Enumerable.Range(2, max - 1).All(level => PlayerLevelCatalog.Unlocks(level).Count ==
              (PlayerLevelCatalog.UnitLevelCap(level) > PlayerLevelCatalog.UnitLevelCap(level - 1) ? 1 : 0) + (PlayerLevelCatalog.WagonRankCap(level) > PlayerLevelCatalog.WagonRankCap(level - 1) ? 1 : 0)),
            "Every level lists exactly the caps it lifts");
        Check(max == 100 && PlayerLevelCatalog.RewardLevels.Count() == max && PlayerLevelCatalog.RewardLevels.All(level => PlayerLevelCatalog.Reward(level) is { Amount: > 0 }),
            "All hundred levels pay a reward");
        // Clearing each stage once must always allow the suggested core level for the next stage.
        var xp = 0;
        var blocked = new List<int>();
        for (var stage = 1; stage <= GameData.MaxStage; stage++)
        {
            if (PlayerLevelCatalog.UnitLevelCap(PlayerLevelCatalog.LevelForXP(xp)) < CampaignProgressionCatalog.SuggestedLevel(stage)) blocked.Add(stage);
            xp += PlayerLevelCatalog.CampaignXP(stage, true, true);
        }
        Check(blocked.Count == 0, $"First clears keep the unit cap at or above every stage's suggested level{(blocked.Count > 0 ? $" (blocked: {string.Join(", ", blocked.Take(5))})" : "")}");
        var finalLevel = PlayerLevelCatalog.LevelForXP(xp);
        Check(finalLevel >= 40 && finalLevel < 60, $"A single campaign clear reaches level {finalLevel}, leaving the rest of the track for other modes");

        var state = GameState.Instance;
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        Check(state.PlayerLevel == 1 && state.PlayerXP == 0 && !state.HasChosenCallsign && state.ClaimableLevelRewardCount == 1,
            "A new caravan starts at level 1 with no name chosen and its welcome reward to claim");
        var rich = state.BuildSaveData();
        rich.Gold = 10000;
        Invoke(state, "ApplySavedData", rich);
        var unit = GameData.PlayerPeasantId;
        Check(state.TryUpgradeUnit(unit, out _) && state.GetUnitLevel(unit) == 2, "Level 1 can train a unit to level 2");
        var gold = state.Gold;
        Check(!state.TryUpgradeUnit(unit, out var unitReason) && state.GetUnitLevel(unit) == 2 && state.Gold == gold && unitReason.Contains("player level 3"),
            "The unit cap stops training without spending gold and names the level needed");
        var archers = BaseUpgradeCatalog.ArcherCrewId;
        Check(state.TryUpgradeBase(archers, out _) && !state.TryUpgradeBase(archers, out var wagonReason) && state.GetBaseUpgradeLevel(archers) == 1
              && wagonReason.Contains("player level 5"), "The wagon rank cap stops the second rank until level 5");

        Check(state.AwardBattleXP(BattleRunMode.Campaign, true, 1, 0) == 41, "A first campaign clear pays the full stage XP");
        state.PrepareCampaignBattle();
        state.ApplyVictory(1, 0, 0, 1);
        Check(state.AwardBattleXP(BattleRunMode.Campaign, true, 1, 0) == 15 && state.AwardBattleXP(BattleRunMode.Campaign, false, 1, 0) == 8,
            "Replays and defeats still pay smaller amounts");
        state.AddPlayerXP(PlayerLevelCatalog.TotalXPForLevel(5) - state.PlayerXP);
        Check(state.PlayerLevel == 5 && state.TryUpgradeUnit(unit, out _) && state.GetUnitLevel(unit) == 3 && state.TryUpgradeBase(archers, out _),
            "Reaching a level lifts its caps");
        Check(state.ClaimableLevelRewardCount == 5 && !state.TryClaimLevelReward(6, out _), "Rewards wait for their level");
        gold = state.Gold;
        Check(state.TryClaimLevelReward(2, out _) && state.Gold == gold + PlayerLevelCatalog.Reward(2).Amount && !state.TryClaimLevelReward(2, out _),
            "A level reward pays once");
        var food = state.Food; var essence = state.Essence; var shards = state.RelicShards;
        Check(state.ClaimAllLevelRewards() == 4 && state.Food == food + PlayerLevelCatalog.Reward(3).Amount && state.Essence == essence + PlayerLevelCatalog.Reward(4).Amount
              && state.RelicShards == shards + PlayerLevelCatalog.Reward(5).Amount && state.ClaimableLevelRewardCount == 0,
            "Claim all collects what is left, whatever each reward is");
        var before = state.BuildSaveData();
        state.AddPlayerXP(35);
        Check(BattleRewardUi.Earned(before, state.BuildSaveData()).Any(reward => reward.Kind == "player_xp" && reward.Amount == 35),
            "The result board sees XP a battle paid");

        Check(!GameState.IsValidCallsign("ab", out _) && !GameState.IsValidCallsign("Bad!Name", out _) && !GameState.IsValidCallsign(new string('x', 19), out _)
              && GameState.IsValidCallsign("Ser Rowan-2", out _), "Nicknames need 3 to 18 letters, numbers, spaces, hyphens or underscores");
        Check(!state.TryChooseCallsign("ab", out _) && !state.HasChosenCallsign && state.TryChooseCallsign("Ser Rowan", out _)
              && state.PlayerCallsign == "Ser Rowan" && state.HasChosenCallsign, "Choosing a valid nickname records the choice");

        var levels = Read<Dictionary<string, int>>(state, "_unitUpgradeLevels");
        levels[unit] = 15;
        var saved = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(state.BuildSaveData()))!;
        var xpBefore = state.PlayerXP;
        Invoke(state, "ApplySavedData", saved);
        Check(state.PlayerXP == xpBefore && state.HasClaimedLevelReward(2) && state.HasClaimedLevelReward(3) && state.HasChosenCallsign && state.GetUnitLevel(unit) == 15,
            "Level, claims and the nickname survive a save round trip, and earlier training above the cap is kept");

        var legacy = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(saved))!;
        legacy.Version = 47;
        legacy.PlayerXP = 0; legacy.ClaimedLevelRewards = Array.Empty<int>(); legacy.HasChosenCallsign = false;
        legacy.StageStars = Enumerable.Range(1, GameData.MaxStage).Select(stage => stage <= 30 ? 2 : 0).ToArray();
        legacy.PlayerCallsign = "Lantern";
        Invoke(state, "ApplySavedData", legacy);
        Check(state.PlayerXP == PlayerLevelCatalog.LegacyXP(legacy.StageStars) && state.PlayerLevel > 5 && state.ClaimableLevelRewardCount == state.PlayerLevel && !state.HasChosenCallsign,
            $"A save from before levels starts at the level its campaign earned (level {state.PlayerLevel}) with those rewards to claim");
        legacy.PlayerCallsign = "Ser Rowan";
        Invoke(state, "ApplySavedData", legacy);
        Check(state.HasChosenCallsign, "A save that already had its own name is not asked again");

        state.ResetProgress();
        state.SetAnalyticsConsent(false);
    }
}
