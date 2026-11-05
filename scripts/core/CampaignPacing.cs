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
    /// their first clears allow (s_cap) and when they are four levels and two wagon ranks behind (s_lag). Leaders sit
    /// 40% of the way from s_lag to s_cap, mid-bosses 65% and zone bosses 75%. A player who never pays sits at about
    /// s_lag, so most stages ask for training they must grind gold for; a squad trained to its caps (gold a purchase
    /// buys at once) wins them all. Leaders stop at 2.25 and at 0.6 above their zone's median leader (a fit far above
    /// its neighbours is usually two lucky seeds), and a stage can be capped where the playthrough finds a cliff.
    /// The first zone ramps in from s_lag. Campaign battles use it, and so do tower floors and event stages, which are
    /// built on the campaign stage as tuned and add their own fitted strength on top. See docs/PLAYTHROUGH_SIM.md.
    /// </summary>
    private static readonly float[] Strength =
    {
        0.188f, 0.688f, 0.188f, 0.260f, 1.269f, 1.288f, 1.288f, 0.420f, 1.288f, 0.929f,
        0.938f, 1.267f, 0.402f, 0.411f, 1.986f, 0.178f, 1.499f, 1.446f, 0.806f, 0.969f,
        1.276f, 0.901f, 0.838f, 0.588f, 1.438f, 0.401f, 0.651f, 0.088f, 0.838f, 0.500f,
        0.401f, 0.838f, 0.588f, 0.214f, 0.557f, 1.088f, 0.401f, 0.063f, 0.838f, 0.031f,
        0.714f, 0.088f, 0.964f, 0.151f, 0.776f, 0.500f, 0.338f, 0.401f, 1.100f, 0.594f,
        0.588f, 0.776f, 0.088f, 0.464f, 0.119f, 0.964f, -0.162f, 1.376f, 1.214f, 0.594f,
        -0.099f, 0.838f, 1.088f, 1.376f, 0.475f, 1.026f, 0.776f, -0.224f, 0.088f, 0.906f,
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
