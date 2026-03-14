using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CampaignDirectiveDefinition
{
    public CampaignDirectiveDefinition(
        string id,
        string title,
        string summary,
        int bonusGold,
        int bonusFood,
        StageModifierDefinition[] modifiers)
    {
        Id = id;
        Title = title;
        Summary = summary;
        BonusGold = Math.Max(0, bonusGold);
        BonusFood = Math.Max(0, bonusFood);
        Modifiers = modifiers ?? Array.Empty<StageModifierDefinition>();
    }

    public string Id { get; }
    public string Title { get; }
    public string Summary { get; }
    public int BonusGold { get; }
    public int BonusFood { get; }
    public StageModifierDefinition[] Modifiers { get; }
}

public static class CampaignDirectiveCatalog
{
    public static CampaignDirectiveDefinition GetForStage(StageDefinition stage)
    {
        if (stage == null)
        {
            return null;
        }

        var totalEnemies = 0;
        var counts = StageEncounterIntel.BuildUnitCounts(stage, out totalEnemies);
        var jammerCount = ResolveCount(counts, GameData.EnemyJammerId);
        var saboteurCount = ResolveCount(counts, GameData.EnemySaboteurId);
        var spitterCount = ResolveCount(counts, GameData.EnemySpitterId);
        var howlerCount = ResolveCount(counts, GameData.EnemyHowlerId);
        var splitterCount = ResolveCount(counts, GameData.EnemySplitterId);
        var heavyCount =
            ResolveCount(counts, GameData.EnemyBruteId) +
            ResolveCount(counts, GameData.EnemyCrusherId) +
            ResolveCount(counts, GameData.EnemyBossId);
        var hasBusHullObjective = stage.Objectives.Any(objective =>
            objective != null &&
            !string.IsNullOrWhiteSpace(objective.Type) &&
            objective.Type.Equals("bus_hull_ratio", StringComparison.OrdinalIgnoreCase));
        var hasGateBreachEvent = stage.MissionEvents.Any(mission =>
            mission != null &&
            mission.NormalizedType.Equals("gate_breach", StringComparison.OrdinalIgnoreCase));
        var barricadeHeavyStage =
            stage.EnemyBaseHealth >= 680f ||
            stage.Modifiers.Any(modifier =>
                modifier != null &&
                modifier.NormalizedType.Equals("reinforced_barricade", StringComparison.OrdinalIgnoreCase));

        var bonusGold = 8 + Math.Max(1, stage.StageNumber);
        var bonusFood = stage.StageNumber % 5 == 0 ? 1 : 0;

        if (hasGateBreachEvent || barricadeHeavyStage || heavyCount >= 5)
        {
            return new CampaignDirectiveDefinition(
                $"stage_{stage.StageNumber}_iron_gate",
                "Iron Gate Edict",
                "Reinforce the enemy keep and demand a cleaner breach. Expect tougher walls and denser follow-up pressure.",
                bonusGold + 8,
                bonusFood,
                new[]
                {
                    new StageModifierDefinition
                    {
                        Type = "reinforced_barricade",
                        Value = 1.22f,
                        Label = "Directive: Iron gate (+22% enemy hull)"
                    },
                    new StageModifierDefinition
                    {
                        Type = "swarm_density",
                        Value = 1f,
                        Label = "Directive: Escalating escorts (+1 enemy cap)"
                    }
                });
        }

        if (StageHazards.HasHazards(stage) || hasBusHullObjective || saboteurCount > 0)
        {
            return new CampaignDirectiveDefinition(
                $"stage_{stage.StageNumber}_strained_wheels",
                "Strained Wheels Decree",
                "Drive the route with a vulnerable caravan frame. Expect lower war wagon hull while keeping the same objective pressure.",
                bonusGold + 6,
                bonusFood,
                new[]
                {
                    new StageModifierDefinition
                    {
                        Type = "strained_caravan",
                        Value = 0.82f,
                        Label = "Directive: Strained caravan (-18% war wagon hull)"
                    }
                });
        }

        if (jammerCount > 0 || howlerCount > 0 || spitterCount >= 4)
        {
            return new CampaignDirectiveDefinition(
                $"stage_{stage.StageNumber}_ashen_silence",
                "Ashen Silence",
                "Fight through a suffocating ward-hex. Courage recovery slows and the line has to stabilize with fewer safe deploy windows.",
                bonusGold + 5,
                bonusFood,
                new[]
                {
                    new StageModifierDefinition
                    {
                        Type = "drained_courage",
                        Value = 0.84f,
                        Label = "Directive: Drained courage (-16% courage gain)"
                    }
                });
        }

        if (splitterCount >= 3 || totalEnemies >= 22 || saboteurCount >= 2)
        {
            return new CampaignDirectiveDefinition(
                $"stage_{stage.StageNumber}_grave_tide",
                "Grave Tide Oath",
                "Call the route under a flood of reinforcements. Expect faster pressure and a larger active enemy front.",
                bonusGold + 4,
                bonusFood,
                new[]
                {
                    new StageModifierDefinition
                    {
                        Type = "swarm_density",
                        Value = 2f,
                        Label = "Directive: Grave tide (+2 enemy cap)"
                    }
                });
        }

        return new CampaignDirectiveDefinition(
            $"stage_{stage.StageNumber}_lean_rations",
            "Lean Rations",
            "Push the route with fewer reserves. Courage gain runs lean, but the bounty is higher if the caravan still secures the lane.",
            bonusGold + 3,
            bonusFood,
            new[]
            {
                new StageModifierDefinition
                {
                    Type = "drained_courage",
                    Value = 0.9f,
                    Label = "Directive: Lean reserves (-10% courage gain)"
                },
                new StageModifierDefinition
                {
                    Type = "swarm_density",
                    Value = 1f,
                    Label = "Directive: Pressed flanks (+1 enemy cap)"
                }
            });
    }

    public static string BuildRewardSummary(CampaignDirectiveDefinition directive)
    {
        if (directive == null)
        {
            return "Directive reward: none";
        }

        return directive.BonusFood > 0
            ? $"Directive reward: +{directive.BonusGold} gold, +{directive.BonusFood} food"
            : $"Directive reward: +{directive.BonusGold} gold";
    }

    public static string BuildStatusText(CampaignDirectiveDefinition directive, bool unlocked, bool armed, bool completed)
    {
        if (directive == null)
        {
            return "Heroic directive: none";
        }

        var status = completed
            ? "Bounty claimed"
            : armed
                ? "Armed"
                : unlocked
                    ? "Ready"
                    : "Unlock after first clear";
        return
            $"Heroic directive: {directive.Title} ({status})\n" +
            $"{directive.Summary}\n" +
            $"{BuildRewardSummary(directive)}";
    }

    public static StageModifierDefinition[] CombineModifiers(StageDefinition stage, CampaignDirectiveDefinition directive)
    {
        if (directive == null || directive.Modifiers.Length == 0)
        {
            return stage?.Modifiers ?? Array.Empty<StageModifierDefinition>();
        }

        var modifiers = new List<StageModifierDefinition>();
        if (stage?.Modifiers != null)
        {
            modifiers.AddRange(stage.Modifiers.Where(modifier => modifier != null));
        }

        modifiers.AddRange(directive.Modifiers.Where(modifier => modifier != null));
        return modifiers.ToArray();
    }

    private static int ResolveCount(IReadOnlyDictionary<string, int> counts, string unitId)
    {
        return counts != null && counts.TryGetValue(unitId, out var value)
            ? value
            : 0;
    }
}
