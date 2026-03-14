using System.Linq;
using Godot;

public static class StageMissionEvents
{
    public static bool HasMissionEvents(StageDefinition stage)
    {
        return stage?.MissionEvents != null && stage.MissionEvents.Length > 0;
    }

    public static StageMissionEventDefinition GetPrimaryEvent(StageDefinition stage)
    {
        return stage?.MissionEvents?
            .FirstOrDefault(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type));
    }

    public static string BuildInlineSummary(StageDefinition stage)
    {
        if (!HasMissionEvents(stage))
        {
            return "none";
        }

        var labels = stage.MissionEvents
            .Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type))
            .Take(2)
            .Select(mission => $"{ResolveTitle(mission)} @ {Mathf.Max(0f, mission.StartTime):0}s")
            .ToArray();

        return labels.Length > 0
            ? string.Join(", ", labels)
            : "none";
    }

    public static string ResolveTitle(StageMissionEventDefinition mission)
    {
        if (mission == null)
        {
            return "Battlefield objective";
        }

        if (!string.IsNullOrWhiteSpace(mission.Title))
        {
            return mission.Title.Trim();
        }

        return mission.NormalizedType switch
        {
            "ritual_site" => "Ritual Site",
            "relic_escort" => "Relic Escort",
            "gate_breach" => "Gate Breach",
            _ => "Battlefield objective"
        };
    }

    public static string ResolveSummary(StageMissionEventDefinition mission)
    {
        if (mission == null)
        {
            return "No battlefield event authored.";
        }

        if (!string.IsNullOrWhiteSpace(mission.Summary))
        {
            return mission.Summary.Trim();
        }

        return mission.NormalizedType switch
        {
            "ritual_site" => "Hold the ritual site long enough to break the enemy's shrine pressure.",
            "relic_escort" => "Hold the lane while the relic convoy crosses to the caravan.",
            "gate_breach" => "Keep the breach crew on the wall long enough to crack the gatehouse.",
            _ => "Secure the authored battlefield objective."
        };
    }

    public static string ResolveRewardSummary(StageMissionEventDefinition mission)
    {
        if (mission == null)
        {
            return "Reward: none.";
        }

        if (!string.IsNullOrWhiteSpace(mission.RewardSummary))
        {
            return mission.RewardSummary.Trim();
        }

        return mission.NormalizedType switch
        {
            "ritual_site" => "Reward: courage burst and cleaner card recovery.",
            "relic_escort" => "Reward: war wagon repair and escort reinforcement.",
            "gate_breach" => "Reward: direct gatehouse damage when the breach lands.",
            _ => "Reward: battlefield momentum."
        };
    }

    public static string ResolvePenaltySummary(StageMissionEventDefinition mission)
    {
        if (mission == null)
        {
            return "Risk: none.";
        }

        if (!string.IsNullOrWhiteSpace(mission.PenaltySummary))
        {
            return mission.PenaltySummary.Trim();
        }

        return mission.NormalizedType switch
        {
            "ritual_site" => "Risk: lose courage and slow card recovery.",
            "relic_escort" => "Risk: the war wagon takes a direct hit.",
            "gate_breach" => "Risk: the gatehouse regains footing and hull.",
            _ => "Risk: lose battlefield tempo."
        };
    }
}
