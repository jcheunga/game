/// <summary>Campaign-wide stage thresholds. Ten zones of ten stages: zone k holds stages 10k+1..10k+10 and
/// its tenth stage is the boss. Late-campaign systems switch on at zone boundaries.</summary>
public static class CampaignPacing
{
    public const int StagesPerZone = 10;
    // Hollow Basilica onward: a timed district condition presses the caravan.
    // Mire of Saints onward: adaptive wave reads, faster boss pressure and bonus-objective pressure.
    public const int VeteranStage = 61;
    // Gloamwood Verge onward: elite versions of the above.
    public const int EliteStage = 81;
    // Commendations start adding food from Ashen Ward, and more from Gloamwood.

    public static int Zone(int stage) => (System.Math.Max(1, stage) - 1) / StagesPerZone;
    public static int Slot(int stage) => (System.Math.Max(1, stage) - 1) % StagesPerZone + 1;

    /// <summary>
    /// Each campaign stage's extra enemy strength s, on top of the enemies' level: health ×(1 + s), damage ×(1 + s/2).
    /// Written by scripts/analysis/campaign_fit.py from CombatReviewSmoke --campaign-fit, which measures the strongest
    /// enemies a player who just reached the stage beats when their troops, spell and wagon are trained to the caps
    /// their first clears allow (s_cap) and when they are four levels and two wagon ranks behind (s_lag). Stages 1-70 sit
    /// 75% of the way from s_lag to s_cap (since 9 November 2026, as hard as a squad trained to its caps still beats;
    /// leaders were at 40% and mid-bosses at 65%). From stage 71 a grinding player's troops are at or near level 20,
    /// so grinding can't lift the squad further, and those stages keep leaders at 40%, mid-bosses at 65% and bosses at
    /// 75%. A player who never pays sits at about s_lag, so every stage asks for training they must grind gold for; a
    /// squad trained to its caps (gold a purchase buys at once) wins them all. Leaders stop at 2.25 and at 0.6 above
    /// their zone's median leader (a fit far above its neighbours is usually two lucky seeds), and a stage can be
    /// capped where the playthrough finds a cliff. The first zone ramps in from s_lag. Campaign battles use it, and so do tower floors and event stages, which are
    /// built on the campaign stage as tuned and add their own fitted strength on top. See docs/PLAYTHROUGH_SIM.md.
    /// </summary>
    private static readonly float[] Strength =
    {
        0.188f, 0.188f, 0.188f, 0.605f, 1.112f, 1.154f, 1.050f, 0.826f, 1.468f, 0.427f,
        2.057f, 2.007f, 1.197f, 2.220f, 0.526f, 1.548f, 1.568f, 1.462f, 1.689f, 0.289f,
        1.426f, 2.200f, 1.063f, 1.711f, 2.250f, 1.562f, 1.781f, 0.766f, 2.250f, 0.750f,
        1.584f, 1.644f, 1.286f, 1.594f, 0.141f, 0.813f, 0.539f, 0.914f, 0.969f, 0.156f,
        1.777f, 1.157f, 1.204f, 1.094f, 2.171f, 1.976f, 1.492f, 1.571f, 1.758f, 1.289f,
        1.516f, 1.363f, 0.492f, 1.742f, 0.578f, 1.250f, 0.469f, 0.758f, 0.422f, 0.836f,
        0.463f, 1.982f, 0.483f, 1.000f, 1.438f, 2.038f, 1.742f, 2.038f, 1.211f, 0.641f,
        1.575f, 1.167f, 1.202f, 0.525f, 0.837f, 1.108f, 1.031f, 1.767f, 1.767f, 0.797f,
        0.131f, 0.376f, 0.556f, 1.133f, 1.501f, 1.501f, 1.292f, 0.901f, 0.604f, 0.375f,
        0.302f, 0.520f, -0.019f, 0.790f, 0.564f, 1.130f, 1.333f, 0.806f, 0.322f, -0.094f,
    };

    /// <summary>Test hook: when set, replaces the table (the --campaign-fit search and --playthrough-sim tuning use it).</summary>
    internal static System.Func<int, float> StrengthOverride;

    public static float EnemyStrength(int stage) => StrengthOverride?.Invoke(stage) ?? EnemyStrengthFromTable(stage);
    public static float EnemyStrengthFromTable(int stage) => Strength[System.Math.Clamp(stage, 1, Strength.Length) - 1];
    public static float EnemyHealthScale(int stage) => 1f + EnemyStrength(stage);
    public static float EnemyDamageScale(int stage) => 1f + EnemyStrength(stage) / 2f;
}
