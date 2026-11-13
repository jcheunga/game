using System;

/// <summary>
/// The grudge bounty: each loss on a campaign stage the player has not cleared yet makes replays of cleared stages pay
/// more gold, until that stage falls. It shortens the grind at a wall in proportion to how stuck the player is without
/// making the wall itself any weaker.
/// </summary>
public partial class GameState
{
    /// <summary>Gold added to a replay per loss on the grudge stage, and the most it can add.</summary>
    public const float GrudgeGoldPerLoss = .1f, GrudgeGoldCap = .6f;

    private int _grudgeStage, _grudgeLosses;

    /// <summary>The uncleared stage the bounty is held against, or 0.</summary>
    public int GrudgeStage => _grudgeLosses > 0 && GetStageStars(_grudgeStage) <= 0 ? _grudgeStage : 0;

    /// <summary>The share of extra gold replays pay now (0 to GrudgeGoldCap).</summary>
    public float GrudgeGoldBonus => GrudgeStage > 0 ? Math.Min(GrudgeGoldCap, _grudgeLosses * GrudgeGoldPerLoss) : 0f;

    /// <summary>Drops the grudge without a win (tests).</summary>
    public void ClearGrudge() { _grudgeStage = 0; _grudgeLosses = 0; }

    private void RecordGrudge(int stage)
    {
        if (GetStageStars(stage) > 0) return;
        if (_grudgeStage != stage) { _grudgeStage = stage; _grudgeLosses = 0; }
        _grudgeLosses++;
    }

    /// <summary>Ends the grudge on the stage's first clear; otherwise pays its bonus on a replay's gold.</summary>
    private int SettleGrudge(int stage, bool firstClear, int rewardGold)
    {
        if (firstClear)
        {
            if (stage == _grudgeStage) { _grudgeStage = 0; _grudgeLosses = 0; }
            return rewardGold;
        }
        return Godot.Mathf.RoundToInt(rewardGold * (1f + GrudgeGoldBonus));
    }
}
