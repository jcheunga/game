/// <summary>Campaign-wide stage thresholds. Ten zones of ten stages: zone k holds stages 10k+1..10k+10 and
/// its tenth stage is the boss. Late-campaign systems switch on at zone boundaries.</summary>
public static class CampaignPacing
{
    public const int StagesPerZone = 10;
    // Hollow Basilica onward: a timed district condition presses the caravan.
    public const int LateConditionStage = 51;
    // Mire of Saints onward: adaptive wave reads, faster boss pressure and bonus-objective pressure.
    public const int VeteranStage = 61;
    // Gloamwood Verge onward: elite versions of the above.
    public const int EliteStage = 81;
    // Commendations start adding food from Ashen Ward, and more from Gloamwood.
    public const int LateFoodStage = 31;
    public const int EliteFoodStage = 81;

    public static int Zone(int stage) => (System.Math.Max(1, stage) - 1) / StagesPerZone;
    public static int Slot(int stage) => (System.Math.Max(1, stage) - 1) % StagesPerZone + 1;
}
