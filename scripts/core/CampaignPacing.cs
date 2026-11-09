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
        0.188f, 0.688f, 0.188f, 0.268f, 1.284f, 1.288f, 1.288f, 0.459f, 1.288f, 0.929f,
        1.103f, 1.337f, 0.479f, 0.495f, 2.012f, 0.278f, 1.607f, 1.561f, 1.239f, 0.969f,
        1.406f, 1.031f, 0.969f, 0.719f, 1.569f, 0.531f, 0.781f, 0.218f, 0.969f, 0.500f,
        0.531f, 0.969f, 0.719f, 0.344f, 0.594f, 1.219f, 0.531f, 0.172f, 0.969f, 0.031f,
        0.844f, 0.218f, 1.095f, 0.281f, 0.906f, 0.719f, 0.469f, 0.531f, 1.319f, 0.594f,
        0.719f, 0.906f, 0.218f, 0.594f, 0.156f, 1.095f, -0.032f, 1.506f, 1.345f, 0.594f,
        0.031f, 0.969f, 1.219f, 1.506f, 0.562f, 1.156f, 0.906f, -0.094f, 0.218f, 0.906f,
        0.088f, -0.036f, 0.564f, 0.276f, -0.349f, -0.187f, -0.400f, 0.564f, -0.349f, -0.312f,
        -0.099f, -0.162f, 0.088f, 0.088f, 0.688f, 0.276f, -0.224f, 0.276f, -0.224f, 0.781f,
        0.026f, -0.388f, 0.151f, 0.338f, 0.412f, -0.099f, 0.464f, 0.526f, 0.338f, -0.312f,
    };

    /// <summary>Test hook: when set, replaces the table (the --campaign-fit search and --playthrough-sim tuning use it).</summary>
    internal static System.Func<int, float> StrengthOverride;

    public static float EnemyStrength(int stage) => StrengthOverride?.Invoke(stage) ?? EnemyStrengthFromTable(stage);
    public static float EnemyStrengthFromTable(int stage) => Strength[System.Math.Clamp(stage, 1, Strength.Length) - 1];
    public static float EnemyHealthScale(int stage) => 1f + EnemyStrength(stage);
    public static float EnemyDamageScale(int stage) => 1f + EnemyStrength(stage) / 2f;
}
