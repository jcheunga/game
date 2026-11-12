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
        0.188f, 0.188f, 0.188f, 0.359f, 1.035f, 1.159f, 1.038f, 0.569f, 1.169f, 0.763f,
        1.380f, 1.146f, 0.562f, 1.396f, 0.544f, 0.966f, 0.532f, 1.089f, 1.366f, 0.875f,
        0.666f, 0.619f, 1.246f, 1.250f, 0.992f, 0.414f, 0.687f, 0.406f, 1.287f, 0.602f,
        0.511f, 0.863f, 0.168f, 0.617f, -0.242f, -0.196f, 0.126f, 0.406f, 0.039f, -0.211f,
        0.310f, 0.206f, 0.827f, 0.305f, 0.852f, 0.899f, 0.446f, 0.500f, 0.125f, 0.109f,
        0.588f, 0.237f, -0.364f, 0.258f, -0.109f, 0.837f, -0.344f, -0.039f, -0.359f, 0.898f,
        -0.238f, 0.906f, 0.347f, 0.516f, 0.148f, 1.000f, 1.078f, 1.116f, 0.203f, 0.336f,
        0.587f, -0.177f, 0.347f, 0.139f, -0.438f, 0.019f, 0.199f, 0.799f, 0.724f, -0.336f,
        -0.439f, -0.206f, -0.102f, 0.233f, 0.606f, 0.606f, 0.562f, -0.111f, 0.006f, 0.281f,
        -0.362f, -0.403f, -0.311f, -0.041f, -0.284f, 0.108f, 0.312f, -0.049f, -0.197f, -0.359f,
    };

    /// <summary>Test hook: when set, replaces the table (the --campaign-fit search and --playthrough-sim tuning use it).</summary>
    internal static System.Func<int, float> StrengthOverride;

    public static float EnemyStrength(int stage) => StrengthOverride?.Invoke(stage) ?? EnemyStrengthFromTable(stage);
    public static float EnemyStrengthFromTable(int stage) => Strength[System.Math.Clamp(stage, 1, Strength.Length) - 1];
    public static float EnemyHealthScale(int stage) => 1f + EnemyStrength(stage);
    public static float EnemyDamageScale(int stage) => 1f + EnemyStrength(stage) / 2f;
}
