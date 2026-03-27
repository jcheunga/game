using System.Collections.Generic;
using System.Linq;
using Godot;

public static class AssetAuditService
{
    private const string UnitSpritePath = "res://assets/units/";
    private const string BattleBackgroundPath = "res://assets/backgrounds/";
    private const string StructurePath = "res://assets/structures/";
    private const string ParticlePath = "res://assets/particles/";
    private const string MusicPath = "res://assets/music/";
    private const string SfxPath = "res://assets/sfx/";
    private const string ScreenBackgroundPath = "res://assets/ui/backgrounds/";
    private const string UnitIconPath = "res://assets/ui/icons/units/";
    private const string SpellIconPath = "res://assets/ui/icons/spells/";
    private const string RelicIconPath = "res://assets/ui/icons/relics/";
    private const string RewardIconPath = "res://assets/ui/icons/rewards/";
    private const string MetaIconPath = "res://assets/ui/icons/meta/";
    private const string CodexIconPath = "res://assets/ui/icons/codex/";
    private const string CodexPortraitPath = "res://assets/ui/portraits/codex/";
    private const string MapBackgroundPath = "res://assets/map/backgrounds/";

    public static string BuildSummary()
    {
        var lines = new List<string>
        {
            "Asset coverage"
        };

        var expectedVisualClasses = GameData.PlayerRosterIds
            .Concat(GameData.EnemyRosterIds)
            .Append(GameData.PlayerSkeletonId)
            .Select(GameData.GetUnit)
            .Select(unit => AssetCoverageCatalog.NormalizeId(unit.VisualClass))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        lines.Add(BuildCoverageLine("Unit sprites", expectedVisualClasses, id => HasPng(UnitSpritePath, id), $"{UnitSpritePath}{{visual_class}}.png"));

        var terrainIds = GameData.Stages
            .Select(stage => AssetCoverageCatalog.NormalizeId(stage.TerrainId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        lines.Add(BuildCoverageLine("Battle backgrounds", terrainIds, id => HasPng(BattleBackgroundPath, id), $"{BattleBackgroundPath}{{terrain_id}}.png"));
        lines.Add(BuildCoverageLine("Structures", AssetCoverageCatalog.StructureIds, id => HasPng(StructurePath, id), $"{StructurePath}{{structure_id}}.png"));
        lines.Add(BuildCoverageLine("Particle textures", AssetCoverageCatalog.ParticleTextureIds, id => HasPng(ParticlePath, id), $"{ParticlePath}{{particle_id}}.png"));

        lines.Add(BuildCoverageLine("Screen backgrounds", AssetCoverageCatalog.ScreenBackgroundIds, id => HasPng(ScreenBackgroundPath, id), $"{ScreenBackgroundPath}{{screen_id}}.png"));
        lines.Add(BuildCoverageLine("District map art", AssetCoverageCatalog.RouteIds, id => HasPng(MapBackgroundPath, id), $"{MapBackgroundPath}{{route_id}}.png"));
        lines.Add(BuildCoverageLine(
            "Unit icons",
            GameData.GetPlayerUnits().Concat(GameData.GetEnemyUnits()).Select(unit => unit.Id).Distinct().OrderBy(id => id).ToArray(),
            id => UiArtLoader.HasUnitIconAsset(TryGetUnit(id)),
            $"{UnitIconPath}{{unit_id}}.png (or {{visual_class}}.png)"));
        lines.Add(BuildCoverageLine(
            "Spell icons",
            GameData.GetPlayerSpells().Select(spell => spell.Id).OrderBy(id => id).ToArray(),
            id => UiArtLoader.HasSpellIconAsset(TryGetSpell(id)),
            $"{SpellIconPath}{{spell_id}}.png (or {{effect_type}}.png)"));
        lines.Add(BuildCoverageLine(
            "Relic icons",
            GameData.GetAllEquipment().Select(relic => relic.Id).OrderBy(id => id).ToArray(),
            id => UiArtLoader.HasRelicIconAsset(TryGetRelic(id)),
            $"{RelicIconPath}{{relic_id}}.png"));
        lines.Add(BuildCoverageLine(
            "Reward icons",
            AssetCoverageCatalog.RewardIconIds,
            id => UiArtLoader.HasRewardIconAsset(id),
            $"{RewardIconPath}{{reward_type}}.png"));
        lines.Add(BuildCoverageLine(
            "Meta icons",
            AssetCoverageCatalog.MetaIconIds,
            id => UiArtLoader.HasMetaIconAsset(id),
            $"{MetaIconPath}{{meta_id}}.png"));
        lines.Add(BuildCoverageLine(
            "Codex icons",
            CodexCatalog.GetAll().Select(entry => entry.Id).OrderBy(id => id).ToArray(),
            id => UiArtLoader.HasCodexIconAsset(TryGetCodexEntry(id)),
            $"{CodexIconPath}{{entry_id}}.png"));
        lines.Add(BuildCoverageLine(
            "Codex portraits",
            CodexCatalog.GetAll().Select(entry => entry.Id).OrderBy(id => id).ToArray(),
            id => UiArtLoader.HasCodexPortraitAsset(TryGetCodexEntry(id)),
            $"{CodexPortraitPath}{{entry_id}}.png"));
        lines.Add(BuildCoverageLine("Music tracks", AssetCoverageCatalog.MusicTrackIds, id => HasAudio(MusicPath, id), $"{MusicPath}{{track_id}}.(ogg|mp3|wav)"));
        lines.Add(BuildCoverageLine("SFX overrides", AssetCoverageCatalog.SfxCueIds, id => HasAudio(SfxPath, id), $"{SfxPath}{{cue_id}}.(ogg|mp3|wav)"));

        var routeVariantCoverage = AssetCoverageCatalog.ScreenBackgroundIds
            .Where(screenId => screenId is "map" or "loadout" or "shop" or "endless" or "multiplayer")
            .ToArray();
        foreach (var screenId in routeVariantCoverage)
        {
            var expectedVariantIds = AssetCoverageCatalog.RouteIds
                .Select(routeId => AssetCoverageCatalog.BuildScreenVariantId(screenId, routeId))
                .ToArray();
            lines.Add(BuildCoverageLine(
                $"{screenId} route overrides",
                expectedVariantIds,
                id => HasPng(ScreenBackgroundPath, id),
                $"{ScreenBackgroundPath}{screenId}_{{route_id}}.png"));
        }

        lines.Add("Use the same IDs in the file names. Missing assets fall back automatically.");
        return string.Join("\n", lines);
    }

    private static string BuildCoverageLine(string label, IReadOnlyList<string> expectedIds, System.Func<string, bool> exists, string pattern)
    {
        if (expectedIds.Count == 0)
        {
            return $"{label}: 0/0";
        }

        var foundIds = expectedIds.Where(exists).ToArray();
        var missingIds = expectedIds.Where(id => !exists(id)).Take(5).ToArray();
        var missingSuffix = missingIds.Length == 0
            ? string.Empty
            : $" | missing: {string.Join(", ", missingIds)}";
        return $"{label}: {foundIds.Length}/{expectedIds.Count} | drop at {pattern}{missingSuffix}";
    }

    private static bool HasPng(string basePath, string id)
    {
        return ResourceLoader.Exists($"{basePath}{id}.png");
    }

    private static bool HasAudio(string basePath, string id)
    {
        return ResourceLoader.Exists($"{basePath}{id}.ogg")
            || ResourceLoader.Exists($"{basePath}{id}.mp3")
            || ResourceLoader.Exists($"{basePath}{id}.wav");
    }

    private static UnitDefinition TryGetUnit(string id)
    {
        return GameData.GetPlayerUnits()
            .Concat(GameData.GetEnemyUnits())
            .FirstOrDefault(unit => unit.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
    }

    private static SpellDefinition TryGetSpell(string id)
    {
        return GameData.GetPlayerSpells()
            .FirstOrDefault(spell => spell.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
    }

    private static EquipmentDefinition TryGetRelic(string id)
    {
        return GameData.GetAllEquipment()
            .FirstOrDefault(relic => relic.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
    }

    private static CodexEntry TryGetCodexEntry(string id)
    {
        return CodexCatalog.GetAll()
            .FirstOrDefault(entry => entry.Id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
    }
}
