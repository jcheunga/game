using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// Alternate campaign waves for testing. Standard is the waves in data/stages.json; each file in data/waves/ is
/// another version, listed by zone and stage, whose stages replace the standard waves. A stage a version leaves
/// out keeps its standard waves. Developer tools pick the active version (GameState.ActiveWaveVersionId).
/// </summary>
public static class WaveVersions
{
    public const string StandardId = "standard";
    private const string Folder = "res://data/waves";

    private static WaveVersion[] _all;
    private static StageWaveDefinition[][] _standardWaves;

    public static string ActiveId { get; private set; } = StandardId;

    public static IReadOnlyList<WaveVersion> All => _all ??= Load();

    public static WaveVersion Find(string id) =>
        All.FirstOrDefault(version => version.Id.Equals(id ?? "", StringComparison.OrdinalIgnoreCase));

    public static WaveVersion Standard => Find(StandardId);
    public static WaveVersion Active => Find(ActiveId) ?? Standard;

    /// <summary>Puts a version's waves on the campaign stages; an unknown id falls back to standard.</summary>
    public static void Apply(string id)
    {
        var version = Find(id) ?? Standard;
        if (GameData.LoadFailed) return;
        var stages = GameData.Stages;
        // The first change records the standard waves, so every later switch can restore them.
        _standardWaves ??= stages.Select(stage => stage.Waves).ToArray();
        var replaced = version.Zones
            .SelectMany(zone => zone.Stages ?? Array.Empty<WaveVersionStage>())
            .Where(stage => stage.Waves is { Length: > 0 })
            .GroupBy(stage => stage.StageNumber)
            .ToDictionary(group => group.Key, group => group.Last().Waves);
        for (var i = 0; i < stages.Length; i++)
            stages[i].Waves = replaced.TryGetValue(stages[i].StageNumber, out var waves) ? waves : _standardWaves[i];
        ActiveId = version.Id;
    }

    private static WaveVersion[] Load()
    {
        var versions = new List<WaveVersion>
        {
            new()
            {
                Id = StandardId, Name = "Standard", Order = 1, Path = "data/stages.json",
                Description = "The campaign's own waves in data/stages.json, rebuilt by scripts/tools/revamp_content.py."
            }
        };
        // A broken install plays nothing; its error screen should not drown in warnings about these files.
        if (GameData.LoadFailed) return versions.ToArray();
        foreach (var file in DirAccess.GetFilesAt(Folder).Where(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Order())
        {
            var path = $"{Folder}/{file}";
            try
            {
                var version = JsonSerializer.Deserialize(FileAccess.GetFileAsString(path), DataJson.Default.WaveVersion);
                if (version == null || string.IsNullOrWhiteSpace(version.Id) || versions.Any(known => known.Id.Equals(version.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    GD.PushWarning($"Wave version {path} needs a unique Id; skipped.");
                    continue;
                }
                version.Path = path.Replace("res://", "");
                if (string.IsNullOrWhiteSpace(version.Name)) version.Name = version.Id;
                version.Zones ??= Array.Empty<WaveVersionZone>();
                foreach (var stage in version.Zones.SelectMany(zone => zone.Stages ?? Array.Empty<WaveVersionStage>()))
                    stage.Waves = KeepPlayableWaves(path, stage);
                versions.Add(version);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Wave version {path} could not be read and was skipped: {ex.Message}");
            }
        }
        return versions.OrderBy(version => version.Order).ThenBy(version => version.Name).ToArray();
    }

    /// <summary>Drops entries naming units that do not exist, and waves left empty, so a typo cannot break a battle.</summary>
    private static StageWaveDefinition[] KeepPlayableWaves(string path, WaveVersionStage stage)
    {
        var waves = new List<StageWaveDefinition>();
        foreach (var wave in stage.Waves ?? Array.Empty<StageWaveDefinition>())
        {
            var entries = (wave.Entries ?? Array.Empty<StageWaveEntryDefinition>())
                .Where(entry => entry != null && entry.Count > 0 && GameData.TryGetUnit(entry.UnitId) != null).ToArray();
            if (entries.Length < (wave.Entries?.Length ?? 0))
                GD.PushWarning($"Wave version {path}: stage {stage.StageNumber} '{wave.Label}' names a unit that does not exist; that entry was skipped.");
            if (entries.Length == 0) continue;
            wave.Entries = entries;
            waves.Add(wave);
        }
        return waves.OrderBy(wave => wave.TriggerTime).ToArray();
    }
}

public sealed class WaveVersion
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Order { get; set; } = 99;
    public string Description { get; set; } = "";
    public WaveVersionZone[] Zones { get; set; } = Array.Empty<WaveVersionZone>();
    /// <summary>The project file it came from, shown in developer tools.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public string Path { get; set; } = "";
    public int StageCount => Zones.Sum(zone => zone.Stages?.Count(stage => stage.Waves is { Length: > 0 }) ?? 0);
}

public sealed class WaveVersionZone
{
    public string MapId { get; set; } = "";
    public WaveVersionStage[] Stages { get; set; } = Array.Empty<WaveVersionStage>();
}

public sealed class WaveVersionStage
{
    public int StageNumber { get; set; }
    public StageWaveDefinition[] Waves { get; set; } = Array.Empty<StageWaveDefinition>();
}
