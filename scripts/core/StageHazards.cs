using System.Linq;

public static class StageHazards
{
    public static bool HasHazards(StageDefinition stage)
    {
        return stage?.Hazards != null && stage.Hazards.Length > 0;
    }

    public static string BuildInlineSummary(StageDefinition stage)
    {
        if (!HasHazards(stage))
        {
            return "none";
        }

        var labels = stage.Hazards
            .Where(hazard => hazard != null)
            .Take(2)
            .Select(BuildShortLabel)
            .ToArray();
        return string.Join(", ", labels);
    }

    public static string BuildSummaryText(StageDefinition stage)
    {
        if (!HasHazards(stage))
        {
            return "Stage hazards: none";
        }

        var lines = stage.Hazards
            .Where(hazard => hazard != null)
            .Select(hazard =>
                $"- {BuildFullLabel(hazard)}  |  {hazard.Damage:0.#} dmg  |  every {hazard.Interval:0.#}s  |  warning {hazard.WarningDuration:0.#}s");

        return "Stage hazards:\n" + string.Join("\n", lines);
    }

    private static string BuildShortLabel(StageHazardDefinition hazard)
    {
        var label = string.IsNullOrWhiteSpace(hazard.Label) ? hazard.Type : hazard.Label;
        return $"{label} ({hazard.Interval:0.#}s)";
    }

    private static string BuildFullLabel(StageHazardDefinition hazard)
    {
        var label = string.IsNullOrWhiteSpace(hazard.Label) ? hazard.Type : hazard.Label;
        return $"{label} at {hazard.StartTime:0.#}s";
    }
}
