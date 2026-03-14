using Godot;

public static class SpellText
{
    public static string BuildEffectSummary(SpellDefinition spell)
    {
        return spell.EffectType switch
        {
            "fireball" =>
                $"Impact {spell.Power:0.#} in {spell.Radius:0.#}r.",
            "heal" =>
                $"Heal {spell.Power:0.#} allies in {spell.Radius:0.#}r and repair {spell.SecondaryPower:0.#} war wagon hull.",
            "frost_burst" =>
                $"Burst {spell.Power:0.#} in {spell.Radius:0.#}r and slow enemies for {spell.Duration:0.#}s.",
            "lightning_strike" =>
                $"Strike up to 3 enemies in {spell.Radius:0.#}r for {spell.Power:0.#} opening damage.",
            "barrier_ward" =>
                $"Ward allies in {spell.Radius:0.#}r for {spell.Duration:0.#}s and cut incoming damage by {BuildReductionPercent(spell.Power)}%.",
            _ =>
                spell.Description
        };
    }

    public static string BuildInlineSummary(SpellDefinition spell)
    {
        return
            $"{BuildCostSummary(spell)}  |  {BuildEffectSummary(spell)}";
    }

    public static string BuildTooltipSummary(SpellDefinition spell, bool isReady, float cooldownRemaining)
    {
        var status = isReady ? "Ready to cast" : $"Cooldown: {cooldownRemaining:0.0}s";
        return
            $"{spell.DisplayName}\n" +
            $"{status}\n" +
            $"{BuildCostSummary(spell)}\n" +
            $"{BuildEffectSummary(spell)}";
    }

    private static string BuildCostSummary(SpellDefinition spell)
    {
        return $"Cost {spell.CourageCost} courage  |  Cooldown {spell.Cooldown:0.#}s";
    }

    private static int BuildReductionPercent(float damageTakenScale)
    {
        return Mathf.RoundToInt((1f - Mathf.Clamp(damageTakenScale, 0f, 1f)) * 100f);
    }
}
