using System.Linq;
using System.Text;
using Godot;

public static class StageMissionEvents
{
    public static StageMissionEventDefinition[] GetCampaignMissionEvents(StageDefinition stage)
    {
        if (HasMissionEvents(stage))
        {
            return stage.MissionEvents
                .Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type))
                .ToArray();
        }

        var fallback = BuildCampaignFallbackMission(stage);
        return fallback == null
            ? System.Array.Empty<StageMissionEventDefinition>()
            : new[] { fallback };
    }

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

    public static string BuildCampaignInlineSummary(StageDefinition stage)
    {
        return BuildInlineSummary(GetCampaignMissionEvents(stage));
    }

    public static string BuildSummaryText(StageDefinition stage)
    {
        if (!HasMissionEvents(stage))
        {
            return "Battlefield events: none";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Battlefield events:");

        foreach (var mission in stage.MissionEvents.Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type)))
        {
            builder.AppendLine(
                $"- {ResolveTitle(mission)}  |  Starts {Mathf.Max(0f, mission.StartTime):0.#}s  |  Radius {Mathf.Max(1f, mission.Radius):0}");
            builder.AppendLine($"  {ResolveSummary(mission)}");
            builder.AppendLine($"  {ResolveRewardSummary(mission)}");
            builder.AppendLine($"  {ResolvePenaltySummary(mission)}");
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildCampaignSummaryText(StageDefinition stage)
    {
        return BuildSummaryText(GetCampaignMissionEvents(stage));
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

    private static string BuildInlineSummary(StageMissionEventDefinition[] missions)
    {
        if (missions == null || missions.Length == 0)
        {
            return "none";
        }

        var labels = missions
            .Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type))
            .Take(2)
            .Select(mission => $"{ResolveTitle(mission)} @ {Mathf.Max(0f, mission.StartTime):0}s")
            .ToArray();

        return labels.Length > 0
            ? string.Join(", ", labels)
            : "none";
    }

    private static string BuildSummaryText(StageMissionEventDefinition[] missions)
    {
        if (missions == null || missions.Length == 0)
        {
            return "Battlefield events: none";
        }

        var builder = new StringBuilder();
        builder.AppendLine("Battlefield events:");

        foreach (var mission in missions.Where(mission => mission != null && !string.IsNullOrWhiteSpace(mission.Type)))
        {
            builder.AppendLine(
                $"- {ResolveTitle(mission)}  |  Starts {Mathf.Max(0f, mission.StartTime):0.#}s  |  Radius {Mathf.Max(1f, mission.Radius):0}");
            builder.AppendLine($"  {ResolveSummary(mission)}");
            builder.AppendLine($"  {ResolveRewardSummary(mission)}");
            builder.AppendLine($"  {ResolvePenaltySummary(mission)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static StageMissionEventDefinition BuildCampaignFallbackMission(StageDefinition stage)
    {
        if (stage == null)
        {
            return null;
        }

        var route = RouteCatalog.Get(stage.MapId);
        var colorHex = route.BannerAccent.ToHtml(false);
        var startTime = Mathf.Clamp(18f + (stage.StageNumber * 0.35f), 18f, 34f);
        var yRatio = ResolveCampaignFallbackYRatio(stage.StageNumber);

        return RouteCatalog.Normalize(stage.MapId) switch
        {
            RouteCatalog.CityId => new StageMissionEventDefinition
            {
                Type = "relic_escort",
                Title = "Lantern Supply Run",
                Summary = "Hold the lane while a levy cart slips supplies back to the war wagon.",
                RewardSummary = "Reward: war wagon repair and a militia escort joins the route.",
                PenaltySummary = "Risk: the supply cart is burned and the wagon takes a direct hit.",
                XRatio = 0.36f,
                YRatio = yRatio,
                Radius = 74f,
                TargetSeconds = 8.5f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.HarborId => new StageMissionEventDefinition
            {
                Type = "gate_breach",
                Title = "Dock Chain Team",
                Summary = "Cover the chain crew long enough to rip open the enemy lane and expose the keep.",
                RewardSummary = "Reward: dock chains slam home and crack the gatehouse.",
                PenaltySummary = "Risk: the crew is lost and the keep regains footing.",
                XRatio = 0.62f,
                YRatio = yRatio,
                Radius = 78f,
                TargetSeconds = 8.2f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.FoundryId => new StageMissionEventDefinition
            {
                Type = "gate_breach",
                Title = "Smelter Bomb Crew",
                Summary = "Hold the demolition lane while forge sappers arm a breach charge on the wall.",
                RewardSummary = "Reward: the smelter charge lands and blasts the gatehouse.",
                PenaltySummary = "Risk: the charge fizzles and the enemy wall resets.",
                XRatio = 0.64f,
                YRatio = yRatio,
                Radius = 80f,
                TargetSeconds = 8.3f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.QuarantineId => new StageMissionEventDefinition
            {
                Type = "ritual_site",
                Title = "Ward Lantern Circle",
                Summary = "Hold the ward circle long enough to purge curse pressure from the route.",
                RewardSummary = "Reward: courage surges and card recovery steadies.",
                PenaltySummary = "Risk: the circle falls and signal pressure worsens.",
                XRatio = 0.48f,
                YRatio = yRatio,
                Radius = 76f,
                TargetSeconds = 8.6f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.ThornwallId => new StageMissionEventDefinition
            {
                Type = "ritual_site",
                Title = "Signal Pyre",
                Summary = "Hold the mountain pyre so the caravan can keep the pass line coordinated.",
                RewardSummary = "Reward: the pyre flares and the convoy regains tempo.",
                PenaltySummary = "Risk: the pyre goes dark and the line loses tempo.",
                XRatio = 0.46f,
                YRatio = yRatio,
                Radius = 78f,
                TargetSeconds = 8.4f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.BasilicaId => new StageMissionEventDefinition
            {
                Type = "relic_escort",
                Title = "Reliquary Procession",
                Summary = "Escort the reliquary train through the lane before the dead collapse on it.",
                RewardSummary = "Reward: relics reach the wagon and sanctified escort aid arrives.",
                PenaltySummary = "Risk: the reliquary is overrun and the wagon takes the blow.",
                XRatio = 0.38f,
                YRatio = yRatio,
                Radius = 74f,
                TargetSeconds = 8.8f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.MireId => new StageMissionEventDefinition
            {
                Type = "ritual_site",
                Title = "Bogfire Marker",
                Summary = "Hold the bogfire post so the caravan can guide enemies into the mire.",
                RewardSummary = "Reward: the marker holds and the route regains tempo.",
                PenaltySummary = "Risk: the bogfire is drowned and the front loses tempo.",
                XRatio = 0.47f,
                YRatio = yRatio,
                Radius = 80f,
                TargetSeconds = 8.3f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.SteppeId => new StageMissionEventDefinition
            {
                Type = "relic_escort",
                Title = "Outrider Relay",
                Summary = "Keep the relay corridor clear while outriders cycle fresh supplies to the wagon.",
                RewardSummary = "Reward: the relay lands and fast escorts join the route.",
                PenaltySummary = "Risk: the relay is cut off and the wagon is struck instead.",
                XRatio = 0.35f,
                YRatio = yRatio,
                Radius = 72f,
                TargetSeconds = 8.1f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.GloamwoodId => new StageMissionEventDefinition
            {
                Type = "ritual_site",
                Title = "Witchlight Cairn",
                Summary = "Hold the cairn while the witchlights bind the lane and expose the enemy push.",
                RewardSummary = "Reward: the cairn flares and battlefield tempo swings back to the caravan.",
                PenaltySummary = "Risk: the cairn is snuffed and the lane falls into confusion.",
                XRatio = 0.49f,
                YRatio = yRatio,
                Radius = 76f,
                TargetSeconds = 8.5f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            RouteCatalog.CitadelId => new StageMissionEventDefinition
            {
                Type = "gate_breach",
                Title = "Siege Spotters",
                Summary = "Hold the signal post long enough for citadel spotters to walk a breach volley onto the keep.",
                RewardSummary = "Reward: spotters lock in and the gatehouse takes a real hit.",
                PenaltySummary = "Risk: the spotters are silenced and the keep regains ground.",
                XRatio = 0.66f,
                YRatio = yRatio,
                Radius = 82f,
                TargetSeconds = 8.4f,
                StartTime = startTime,
                ColorHex = colorHex
            },
            _ => null
        };
    }

    private static float ResolveCampaignFallbackYRatio(int stageNumber)
    {
        return Mathf.Abs(Mathf.Max(1, stageNumber) % 3) switch
        {
            0 => 0.34f,
            1 => 0.5f,
            _ => 0.66f
        };
    }
}
