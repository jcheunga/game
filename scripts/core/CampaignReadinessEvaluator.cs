using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public sealed class CampaignReadinessReport
{
    public CampaignReadinessReport(
        int score,
        string rating,
        string summary,
        int doctrineSelections,
        int doctrineEligibleCount,
        IReadOnlyList<string> gaps)
    {
        Score = Mathf.Clamp(score, 0, 100);
        Rating = rating;
        Summary = summary;
        DoctrineSelections = Math.Max(0, doctrineSelections);
        DoctrineEligibleCount = Math.Max(0, doctrineEligibleCount);
        Gaps = gaps ?? Array.Empty<string>();
    }

    public int Score { get; }
    public string Rating { get; }
    public string Summary { get; }
    public int DoctrineSelections { get; }
    public int DoctrineEligibleCount { get; }
    public IReadOnlyList<string> Gaps { get; }
}

public static class CampaignReadinessEvaluator
{
    public static CampaignReadinessReport Evaluate(
        StageDefinition stage,
        IEnumerable<UnitDefinition> deckUnits,
        IEnumerable<SpellDefinition> deckSpells)
    {
        var resolvedUnits = deckUnits?
            .Where(unit => unit != null)
            .ToArray() ?? Array.Empty<UnitDefinition>();
        var resolvedSpells = deckSpells?
            .Where(spell => spell != null)
            .ToArray() ?? Array.Empty<SpellDefinition>();

        if (stage == null)
        {
            return new CampaignReadinessReport(
                0,
                "Unknown",
                "No stage data available.",
                0,
                0,
                Array.Empty<string>());
        }

        var score = 34;
        var gaps = new List<string>();
        var counts = StageEncounterIntel.BuildUnitCounts(stage, out var totalEnemies);
        var frontlineCount = resolvedUnits.Count(unit =>
            SquadSynergyCatalog.NormalizeTag(unit.SquadTag).Equals(SquadSynergyCatalog.FrontlineTag, StringComparison.OrdinalIgnoreCase));
        var breachCount = resolvedUnits.Count(unit =>
            SquadSynergyCatalog.NormalizeTag(unit.SquadTag).Equals(SquadSynergyCatalog.BreachTag, StringComparison.OrdinalIgnoreCase));
        var supportCount = resolvedUnits.Count(unit =>
            SquadSynergyCatalog.NormalizeTag(unit.SquadTag).Equals(SquadSynergyCatalog.SupportTag, StringComparison.OrdinalIgnoreCase));
        var reconCount = resolvedUnits.Count(unit =>
            SquadSynergyCatalog.NormalizeTag(unit.SquadTag).Equals(SquadSynergyCatalog.ReconTag, StringComparison.OrdinalIgnoreCase));
        var rangedCount = resolvedUnits.Count(unit => unit.UsesProjectile || unit.AttackRange >= 120f);
        var repairCount = resolvedUnits.Count(unit => unit.BusRepairAmount > 0.05f);
        var splashCount = resolvedUnits.Count(unit => unit.AttackSplashRadius > 0.05f);
        var fastCount = resolvedUnits.Count(unit => unit.Speed >= 95f);
        var auraCount = resolvedUnits.Count(unit => unit.AuraRadius > 0.05f);
        var spellIds = new HashSet<string>(resolvedSpells.Select(spell => spell.Id), StringComparer.OrdinalIgnoreCase);
        var doctrineEligibleCount = resolvedUnits.Count(unit => GameState.Instance.IsUnitDoctrineUnlocked(unit.Id));
        var doctrineSelections = resolvedUnits.Count(unit => !string.IsNullOrWhiteSpace(GameState.Instance.GetUnitDoctrineId(unit.Id)));
        var synergyCount = SquadSynergyCatalog.ResolveActive(resolvedUnits).Count;
        var averageDeployCost = resolvedUnits.Length == 0
            ? 99f
            : resolvedUnits.Average(unit => unit.Cost);
        var walkerCount = ResolveCount(counts, GameData.EnemyWalkerId);
        var runnerCount = ResolveCount(counts, GameData.EnemyRunnerId);
        var saboteurCount = ResolveCount(counts, GameData.EnemySaboteurId);
        var howlerCount = ResolveCount(counts, GameData.EnemyHowlerId);
        var jammerCount = ResolveCount(counts, GameData.EnemyJammerId);
        var spitterCount = ResolveCount(counts, GameData.EnemySpitterId);
        var splitterCount = ResolveCount(counts, GameData.EnemySplitterId);
        var heavyCount =
            ResolveCount(counts, GameData.EnemyBruteId) +
            ResolveCount(counts, GameData.EnemyCrusherId) +
            ResolveCount(counts, GameData.EnemyBossId);
        var hazardHeavy = StageHazards.HasHazards(stage);
        var supportPressure = jammerCount > 0 || howlerCount > 0 || spitterCount > 0;
        var crowdPressure = splitterCount >= 2 || walkerCount >= 8 || totalEnemies >= 24 || StageModifiers.ResolveEnemyCapBonus(stage) > 0;
        var rushPressure = runnerCount >= 3 || saboteurCount > 0;
        var breachPressure =
            heavyCount > 0 ||
            stage.EnemyBaseHealth >= 680f ||
            stage.Modifiers.Any(modifier =>
                modifier != null &&
                modifier.NormalizedType.Equals("reinforced_barricade", StringComparison.OrdinalIgnoreCase)) ||
            stage.MissionEvents.Any(mission =>
                mission != null &&
                mission.NormalizedType.Equals("gate_breach", StringComparison.OrdinalIgnoreCase));
        var hullSensitive = hazardHeavy || stage.Objectives.Any(objective =>
            objective != null &&
            !string.IsNullOrWhiteSpace(objective.Type) &&
            objective.Type.Equals("bus_hull_ratio", StringComparison.OrdinalIgnoreCase));
        var leanEconomy =
            stage.Modifiers.Any(modifier =>
                modifier != null &&
                modifier.NormalizedType.Equals("drained_courage", StringComparison.OrdinalIgnoreCase)) ||
            jammerCount > 0;

        score += Math.Min(12, resolvedUnits.Length * 4);
        score += Math.Min(6, resolvedSpells.Length * 3);
        score += Math.Min(8, synergyCount * 4);
        score += Math.Min(10, doctrineSelections * 3);

        if (frontlineCount > 0)
        {
            score += 6;
        }
        else
        {
            gaps.Add("Add a frontline card so the lane can actually hold contact.");
            score -= 12;
        }

        if (rangedCount > 0)
        {
            score += 5;
        }

        if (supportCount > 0 || repairCount > 0 || auraCount > 0)
        {
            score += 4;
        }

        if (hullSensitive)
        {
            if (repairCount > 0 || spellIds.Contains(GameData.SpellHealId) || spellIds.Contains(GameData.SpellBarrierWardId))
            {
                score += 10;
            }
            else
            {
                gaps.Add("Bring sustain for hull-sensitive pressure: Heal, Barrier Ward, or Siege Engineer.");
                score -= 10;
            }
        }

        if (supportPressure)
        {
            if (rangedCount > 0 || spellIds.Contains(GameData.SpellLightningStrikeId))
            {
                score += 10;
            }
            else
            {
                gaps.Add("Add ranged or strike pressure to remove hexers, heralds, and blight casters early.");
                score -= 9;
            }
        }

        if (crowdPressure)
        {
            if (splashCount > 0 || spellIds.Contains(GameData.SpellFireballId) || spellIds.Contains(GameData.SpellFrostBurstId))
            {
                score += 9;
            }
            else
            {
                gaps.Add("Bring crowd control for swarm spikes: Alchemist, Fireball, or Frost Burst.");
                score -= 8;
            }
        }

        if (rushPressure)
        {
            if (frontlineCount > 0 && (fastCount > 0 || reconCount > 0))
            {
                score += 8;
            }
            else
            {
                gaps.Add("Fast rush stages need an early intercept card to catch runners and sappers.");
                score -= 7;
            }
        }

        if (breachPressure)
        {
            if (breachCount > 0 || spellIds.Contains(GameData.SpellFireballId))
            {
                score += 10;
            }
            else
            {
                gaps.Add("Reinforced routes need dedicated breach damage like Halberdier or burst support.");
                score -= 10;
            }
        }

        if (leanEconomy)
        {
            if (averageDeployCost <= 30f || GameState.Instance.GetBaseUpgradeLevel(BaseUpgradeCatalog.PantryId) > 0)
            {
                score += 7;
            }
            else
            {
                gaps.Add("Thin-courage pressure favors a cheaper curve or stronger Caravan Stores.");
                score -= 6;
            }
        }

        if (doctrineEligibleCount > doctrineSelections)
        {
            gaps.Add("Forge doctrines for veteran squad cards so level 3 units stop leaving free power on the table.");
            score -= Math.Min(8, (doctrineEligibleCount - doctrineSelections) * 2);
        }

        if (GameState.Instance.IsCampaignDirectiveArmed(stage.StageNumber))
        {
            score -= 4;
        }

        var rating = score switch
        {
            < 45 => "Fragile",
            < 60 => "Strained",
            < 78 => "Ready",
            _ => "Strong"
        };

        var summary = rating switch
        {
            "Fragile" => "Current squad is under-tuned for the selected route.",
            "Strained" => "The caravan can deploy, but there are visible coverage gaps.",
            "Ready" => "The squad answers the main threats on this route.",
            _ => "The caravan is broadly equipped for this route and its current pressure profile."
        };

        return new CampaignReadinessReport(
            score,
            rating,
            summary,
            doctrineSelections,
            doctrineEligibleCount,
            gaps.Take(3).ToArray());
    }

    public static string BuildInlineSummary(CampaignReadinessReport report)
    {
        if (report == null)
        {
            return "Readiness: unavailable";
        }

        return $"Readiness: {report.Rating} ({report.Score}/100)";
    }

    public static string BuildDetailedSummary(CampaignReadinessReport report)
    {
        if (report == null)
        {
            return "Readiness: unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{BuildInlineSummary(report)}  |  Doctrines {report.DoctrineSelections}/{Math.Max(1, report.DoctrineEligibleCount)} forged");
        builder.AppendLine(report.Summary);
        if (report.Gaps.Count == 0)
        {
            builder.Append("No major response gaps detected.");
        }
        else
        {
            builder.Append("Priority gaps: ");
            builder.Append(string.Join("  |  ", report.Gaps));
        }

        return builder.ToString();
    }

    private static int ResolveCount(IReadOnlyDictionary<string, int> counts, string unitId)
    {
        return counts != null && counts.TryGetValue(unitId, out var value)
            ? value
            : 0;
    }
}
