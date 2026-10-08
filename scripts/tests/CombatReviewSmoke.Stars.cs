using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CheckStageStars()
    {
        var state = GameState.Instance;
        var checkpoint = (GameSaveData)Invoke(state, "BuildSaveData");
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        state.SetShowHints(false);
        state.PrepareCampaignBattle();
        foreach (var stage in GameData.Stages)
        {
            foreach (var (health, damaged, won, expected) in new[] {
                (100f, false, true, 3), (100f, true, true, 2),
                (99.99f, true, true, 2), (70f, true, true, 2),
                (69.99f, true, true, 1), (1f, true, true, 1),
                (100f, false, false, 0), (0f, true, false, 0) })
            {
                var result = new StageBattleResult {
                    PlayerBaseHealth = health, PlayerBaseMaxHealth = 100f,
                    PlayerBaseTookDamage = damaged, Elapsed = 99999,
                    PlayerDeployments = 999, FailedMissionEvents = 99
                };
                Check(StageObjectives.EvaluateBattle(stage, result, won).StarsEarned == expected,
                    $"Stage {stage.StageNumber}: health {health}%, damaged {damaged}, victory {won} awards {expected} stars regardless of battle objectives");
            }
        }

        var battle = await OpenBattle(1);
        Check(!((StageBattleResult)Invoke(battle, "BuildStageBattleResult")).PlayerBaseTookDamage,
            "A new battle starts eligible for a no-damage clear");
        Invoke(battle, "DamageBusByRatio", .00001f, Colors.White);
        Invoke(battle, "RepairBusByRatio", 1f);
        var repaired = (StageBattleResult)Invoke(battle, "BuildStageBattleResult");
        Check(repaired.PlayerBaseTookDamage && repaired.PlayerBaseHealth == repaired.PlayerBaseMaxHealth,
            "Tiny caravan damage remains recorded after a full repair");
        Invoke(battle, "EndBattle", true);
        Check(state.GetStageStars(1) == 2, "Actual victory saves two stars after damage and repair");
        await CloseBattle(battle);

        battle = await OpenBattle(1);
        Invoke(battle, "EndBattle", true);
        Check(state.GetStageStars(1) == 3, "A fresh no-damage replay improves the saved rating to three stars");
        await CloseBattle(battle);

        battle = await OpenBattle(1);
        Invoke(battle, "DamageBusByRatio", .8f, Colors.White);
        Invoke(battle, "EndBattle", true);
        Check(state.GetStageStars(1) == 3, "A lower-scoring replay preserves the best rating");
        await CloseBattle(battle);

        battle = await OpenBattle(2);
        Invoke(battle, "EndBattle", false);
        Check(state.GetStageStars(2) == 0, "An unsuccessful first attempt saves no stars");
        await CloseBattle(battle);
        state.ApplyDefeat(1);
        state.ApplyRetreat(1);
        state.ReloadFromDisk();
        Check(state.GetStageStars(1) == 3 && state.GetStageStars(2) == 0,
            "Save reload, defeat and retreat preserve previous bests and uncleared stages");
        Invoke(state, "ApplySavedData", checkpoint);
    }
}
