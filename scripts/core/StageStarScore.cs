/// <summary>Mission ratings depend only on completion and caravan health.</summary>
public static class StageStarScore
{
    public const string RulesText = "3 stars · Complete without caravan damage\n2 stars · Complete with at least 70% caravan health\n1 star · Complete the mission\n0 stars · Not completed";

    public static int Evaluate(bool completed, float health, float maxHealth, bool tookDamage)
    {
        if (!completed) return 0;
        if (maxHealth <= 0f) return 1;
        if (!tookDamage && health >= maxHealth) return 3;
        return health / maxHealth >= 0.7f ? 2 : 1;
    }
}
