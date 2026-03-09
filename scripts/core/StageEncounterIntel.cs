using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public static class StageEncounterIntel
{
    public static string ResolveThreatRating(StageDefinition stage)
    {
        var threatScore = CalculateThreatScore(stage);
        return threatScore switch
        {
            < 1.75f => "Low",
            < 2.4f => "Moderate",
            < 3.15f => "High",
            < 4.0f => "Severe",
            _ => "Extreme"
        };
    }

    public static string BuildCompactSummary(StageDefinition stage)
    {
        if (stage == null)
        {
            return "Encounter intel unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            return
                $"Threat: {ResolveThreatRating(stage)}\n" +
                "Pressure: dynamic infected activity\n" +
                $"Modifiers: {StageModifiers.BuildInlineSummary(stage)}\n" +
                $"Hazards: {StageHazards.BuildInlineSummary(stage)}";
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var firstWave = stage.Waves.OrderBy(wave => wave.TriggerTime).FirstOrDefault();
        var peakWave = stage.Waves
            .OrderByDescending(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            .ThenBy(wave => wave.TriggerTime)
            .FirstOrDefault();
        var bossWave = stage.Waves.FirstOrDefault(
            wave => wave.Entries.Any(entry => entry != null && entry.UnitId == GameData.EnemyBossId));

        return
            $"Threat: {ResolveThreatRating(stage)}  |  Contacts: {totalEnemies}  |  Enemy types: {counts.Count}\n" +
            $"First contact: {(firstWave == null ? "dynamic" : $"{firstWave.TriggerTime:0.#}s")}  |  " +
            $"Peak wave: {(peakWave == null ? "n/a" : $"{peakWave.TriggerTime:0.#}s")}  |  " +
            $"Boss: {(bossWave == null ? "none" : $"{bossWave.TriggerTime:0.#}s")}\n" +
            $"Modifiers: {StageModifiers.BuildInlineSummary(stage)}  |  Hazards: {StageHazards.BuildInlineSummary(stage)}";
    }

    public static string BuildEncounterIntel(StageDefinition stage)
    {
        if (stage == null)
        {
            return "Encounter intel unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            return
                "Encounter intel:\n" +
                $"Threat rating: {ResolveThreatRating(stage)}\n" +
                "Roaming infected pressure with dynamic composition.";
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var topThreats = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => GameData.GetUnit(pair.Key).DisplayName)
            .Take(5)
            .Select(pair => $"{GameData.GetUnit(pair.Key).DisplayName} x{pair.Value}");

        var bossWave = stage.Waves.FirstOrDefault(
            wave => wave.Entries.Any(entry => entry != null && entry.UnitId == GameData.EnemyBossId));
        var peakWave = stage.Waves
            .OrderByDescending(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            .ThenBy(wave => wave.TriggerTime)
            .First();

        var builder = new StringBuilder();
        builder.AppendLine("Encounter intel:");
        builder.AppendLine($"Threat rating: {ResolveThreatRating(stage)}");
        builder.AppendLine(
            $"Scheduled contacts: {totalEnemies}  |  Enemy types: {counts.Count}  |  Peak wave: {peakWave.TriggerTime:0}s");
        builder.AppendLine($"Threat mix: {string.Join(", ", topThreats)}");
        builder.AppendLine($"Hazards: {StageHazards.BuildInlineSummary(stage)}");

        if (bossWave != null)
        {
            builder.Append($"Boss warning: {bossWave.TriggerTime:0}s  |  {bossWave.Label}");
        }
        else
        {
            builder.Append("Boss warning: none on this route.");
        }

        return builder.ToString();
    }

    public static string BuildWaveSummary(StageDefinition stage, int maxEntriesPerWave = 3)
    {
        if (stage == null)
        {
            return "Incoming pressure: unavailable.";
        }

        if (!stage.HasScriptedWaves)
        {
            return "Incoming pressure: dynamic random wave pacing.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Scripted waves: {stage.Waves.Length}");

        for (var i = 0; i < stage.Waves.Length; i++)
        {
            var wave = stage.Waves[i];
            var label = string.IsNullOrWhiteSpace(wave.Label) ? $"Wave {i + 1}" : wave.Label;
            builder.AppendLine($"{i + 1}. {wave.TriggerTime:0}s  |  {label}  |  {BuildWaveEntrySummary(wave, maxEntriesPerWave)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static Dictionary<string, int> BuildUnitCounts(StageDefinition stage, out int totalEnemies)
    {
        var counts = new Dictionary<string, int>();
        totalEnemies = 0;

        if (stage?.Waves == null)
        {
            return counts;
        }

        foreach (var wave in stage.Waves)
        {
            foreach (var entry in wave.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
                {
                    continue;
                }

                var count = Mathf.Max(1, entry.Count);
                totalEnemies += count;
                counts[entry.UnitId] = counts.TryGetValue(entry.UnitId, out var existing)
                    ? existing + count
                    : count;
            }
        }

        return counts;
    }

    private static string BuildWaveEntrySummary(StageWaveDefinition wave, int maxEntries)
    {
        var parts = new List<string>();
        var hiddenEntries = 0;

        for (var i = 0; i < wave.Entries.Length; i++)
        {
            var entry = wave.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.UnitId))
            {
                continue;
            }

            if (parts.Count >= maxEntries)
            {
                hiddenEntries++;
                continue;
            }

            var displayName = GameData.GetUnit(entry.UnitId).DisplayName;
            parts.Add($"{displayName} x{Mathf.Max(1, entry.Count)}");
        }

        if (hiddenEntries > 0)
        {
            parts.Add($"+{hiddenEntries} more");
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : "No enemy composition data.";
    }

    private static float CalculateThreatScore(StageDefinition stage)
    {
        if (stage == null)
        {
            return 0f;
        }

        var counts = BuildUnitCounts(stage, out var totalEnemies);
        var peakWaveCount = stage.HasScriptedWaves
            ? stage.Waves.Max(wave => wave.Entries.Sum(entry => entry == null ? 0 : Mathf.Max(1, entry.Count)))
            : 0;
        var hasBoss = stage.HasScriptedWaves &&
            stage.Waves.Any(wave => wave.Entries.Any(entry => entry != null && entry.UnitId == GameData.EnemyBossId));

        return
            ((stage.EnemyHealthScale + stage.EnemyDamageScale) * 0.72f) +
            (stage.Waves.Length * 0.16f) +
            (counts.Count * 0.11f) +
            (peakWaveCount * 0.08f) +
            (Mathf.Min(totalEnemies, 30) * 0.015f) +
            (StageModifiers.HasModifiers(stage) ? stage.Modifiers.Length * 0.08f : 0f) +
            (StageHazards.HasHazards(stage) ? stage.Hazards.Length * 0.1f : 0f) +
            (StageModifiers.ResolveEnemyCapBonus(stage) * 0.12f) +
            (hasBoss ? 0.55f : 0f);
    }
}
