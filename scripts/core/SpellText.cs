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
            "stone_barricade" =>
                $"Raise a wall with {spell.Power:0.#} durability for {spell.Duration:0.#}s.",
            "war_cry" =>
                $"Rally all allies: +{Mathf.RoundToInt((spell.Power - 1f) * 100f)}% attack and +{Mathf.RoundToInt((spell.SecondaryPower - 1f) * 100f)}% speed for {spell.Duration:0.#}s.",
            "earthquake" =>
                $"Shake {spell.Power:0.#} damage in {spell.Radius:0.#}r and slow for {spell.Duration:0.#}s.",
            "polymorph" =>
                $"Transform the toughest enemy in {spell.Radius:0.#}r into a sheep for {spell.Duration:0.#}s.",
            "resurrect" =>
                $"Restore the last fallen ally at {Mathf.RoundToInt(spell.Power * 100f)}% health.",
            _ =>
                spell.Description
        };
    }

    public static string BuildResolvedEffectSummary(ResolvedSpellStats spell)
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
            "stone_barricade" =>
                $"Raise a wall with {spell.Power:0.#} durability for {spell.Duration:0.#}s.",
            "war_cry" =>
                $"Rally all allies: +{Mathf.RoundToInt((spell.Power - 1f) * 100f)}% attack and +{Mathf.RoundToInt((spell.SecondaryPower - 1f) * 100f)}% speed for {spell.Duration:0.#}s.",
            "earthquake" =>
                $"Shake {spell.Power:0.#} damage in {spell.Radius:0.#}r and slow for {spell.Duration:0.#}s.",
            "polymorph" =>
                $"Transform the toughest enemy in {spell.Radius:0.#}r into a sheep for {spell.Duration:0.#}s.",
            "resurrect" =>
                $"Restore the last fallen ally at {Mathf.RoundToInt(spell.Power * 100f)}% health.",
            _ =>
                spell.DisplayName
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

    public static string BuildTooltipSummary(SpellDefinition spell, ResolvedSpellStats resolved, bool isReady, float cooldownRemaining)
    {
        var status = isReady ? "Ready to cast" : $"Cooldown: {cooldownRemaining:0.0}s";
        return
            $"Lv{resolved.Level} {spell.DisplayName}\n" +
            $"{status}\n" +
            $"Cost {resolved.CourageCost} courage  |  Cooldown {resolved.Cooldown:0.#}s\n" +
            $"{BuildResolvedEffectSummary(resolved)}";
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
