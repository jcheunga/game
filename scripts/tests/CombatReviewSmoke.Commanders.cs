using System;
using System.Linq;
using Godot;

public partial class CombatReviewSmoke
{
    /// <summary>Commander signatures, stage hazards and the grudge bounty are wired to data the game can use.</summary>
    private void CheckCommandersAndHazards()
    {
        var bosses = GameData.GetEnemyUnits().Where(u => u.IsBoss).ToArray();
        Check(bosses.Count(u => !string.IsNullOrEmpty(u.SignatureAbilityId)) == bosses.Length - 1 &&
            bosses.Where(u => !string.IsNullOrEmpty(u.SignatureAbilityId)).All(u => BattleController.SignatureName(u.SignatureAbilityId) != ""),
            "Every commander but the first has a signature move the battle knows");
        Check(bosses.Select(u => u.SignatureAbilityId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count() == bosses.Length - 1,
            "No two commanders share a signature move");

        var hazardous = GameData.Stages.Where(StageHazards.HasHazards).ToArray();
        Check(hazardous.Length == 12 && hazardous.All(s => s.StageNumber > 60 && s.Modifiers.Length == 0 &&
            s.Hazards.All(h => h.Damage is > 0 and <= 25 && h.WarningDuration >= 1.5f && h.Interval >= 10)),
            $"Twelve late stages carry a hazard with a fair warning and no second rule ({hazardous.Length})");

        var state = GameState.Instance;
        var stage = Enumerable.Range(1, GameData.MaxStage).First(n => state.GetStageStars(n) <= 0);
        state.ClearGrudge();
        var mode = state.CurrentBattleMode;
        state.PrepareCampaignBattle();
        for (var i = 0; i < 9; i++) state.ApplyDefeat(stage);
        Check(state.GrudgeStage == stage && Mathf.IsEqualApprox(state.GrudgeGoldBonus, GameState.GrudgeGoldCap),
            $"Losses on an uncleared stage raise the replay bounty to its cap ({state.GrudgeGoldBonus:0.00})");
        state.ClearGrudge();
        Check(state.GrudgeStage == 0 && state.GrudgeGoldBonus == 0f, "A cleared grudge pays nothing");
    }
}
