using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public static class UiArtLoader
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly HashSet<string> Missing = new();

    private const string UnitIconPath = "res://assets/ui/icons/units/";
    private const string SpellIconPath = "res://assets/ui/icons/spells/";
    private const string RelicIconPath = "res://assets/ui/icons/relics/";
    private const string RewardIconPath = "res://assets/ui/icons/rewards/";
    private const string MetaIconPath = "res://assets/ui/icons/meta/";
    private const string CodexIconPath = "res://assets/ui/icons/codex/";
    private const string CodexPortraitPath = "res://assets/ui/portraits/codex/";

    public static Texture2D TryLoadUnitIcon(UnitDefinition unit)
    {
        if (unit == null)
        {
            return null;
        }

        var unitId = AssetCoverageCatalog.NormalizeId(unit.Id);
        if (!string.IsNullOrWhiteSpace(unitId))
        {
            var byUnitId = TryLoad(UnitIconPath, unitId);
            if (byUnitId != null)
            {
                return byUnitId;
            }
        }

        var visualClass = AssetCoverageCatalog.NormalizeId(unit.VisualClass);
        return TryLoad(UnitIconPath, visualClass) ?? LoadPortrait(unit.Id) ?? RealmUi.Icon(visualClass switch
        { "shield" => "shield", "gunner" or "sniper" => "arrow", "support" => "heart", "boss" => "crown", _ => "sword" });
    }

    private static Texture2D LoadPortrait(string unitId)
    {
        var ids = new[] { "player_brawler", "player_shooter", "player_defender", "player_spear",
            "player_ranger", "player_raider", "player_mechanic", "player_marksman",
            "player_breacher", "player_grenadier", "player_coordinator", "enemy_boss" };
        var index = Array.IndexOf(ids, unitId);
        if (index < 0) return null;
        var key = "portrait:" + unitId;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var atlas = ResourceLoader.Load<Texture2D>("res://assets/ui/portraits/warband_atlas.png");
        if (atlas == null) return null;
        var cell = atlas.GetSize() / new Vector2(4, 3);
        return Cache[key] = new AtlasTexture { Atlas = atlas, Region = new Rect2(new Vector2(index % 4, index / 4) * cell, cell) };
    }

    public static Texture2D TryLoadSpellIcon(SpellDefinition spell)
    {
        if (spell == null)
        {
            return null;
        }

        var spellId = AssetCoverageCatalog.NormalizeId(spell.Id);
        if (!string.IsNullOrWhiteSpace(spellId))
        {
            var bySpellId = TryLoad(SpellIconPath, spellId);
            if (bySpellId != null)
            {
                return bySpellId;
            }
        }

        var effectType = AssetCoverageCatalog.NormalizeId(spell.EffectType);
        return string.IsNullOrWhiteSpace(effectType)
            ? null
            : TryLoad(SpellIconPath, effectType) ?? RealmUi.Icon(effectType.Contains("heal") ? "heart" : effectType.Contains("barrier") ? "shield" : effectType.Contains("fire") ? "flame" : "bolt");
    }

    public static Texture2D TryLoadRelicIcon(EquipmentDefinition relic)
    {
        return relic == null
            ? null
            : TryLoad(RelicIconPath, AssetCoverageCatalog.NormalizeId(relic.Id));
    }

    public static Texture2D TryLoadCodexIcon(CodexEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        var codexId = AssetCoverageCatalog.NormalizeId(entry.Id);
        if (!string.IsNullOrWhiteSpace(codexId))
        {
            var codexIcon = TryLoad(CodexIconPath, codexId);
            if (codexIcon != null)
            {
                return codexIcon;
            }
        }

        if (TryResolveUnit(entry, out var unit))
        {
            return TryLoadUnitIcon(unit);
        }

        if (TryResolveSpell(entry, out var spell))
        {
            return TryLoadSpellIcon(spell);
        }

        if (TryResolveRelic(entry, out var relic))
        {
            return TryLoadRelicIcon(relic);
        }

        return null;
    }

    public static Texture2D TryLoadCodexPortrait(CodexEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        var portrait = TryLoad(CodexPortraitPath, AssetCoverageCatalog.NormalizeId(entry.Id));
        return portrait ?? TryLoadCodexIcon(entry);
    }

    public static Texture2D TryLoadRewardIcon(string rewardType, string rewardItemId = "")
    {
        var itemId = AssetCoverageCatalog.NormalizeId(rewardItemId);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            var byItemId = TryLoad(RewardIconPath, itemId);
            if (byItemId != null)
            {
                return byItemId;
            }
        }

        var typeId = AssetCoverageCatalog.NormalizeId(rewardType);
        return string.IsNullOrWhiteSpace(typeId)
            ? null
            : TryLoad(RewardIconPath, typeId) ?? RealmUi.Icon(typeId == "gold" ? "gold" : typeId == "food" ? "food" : typeId.Contains("star") ? "star" : "gift");
    }

    public static Texture2D TryLoadMetaIcon(string metaId)
    {
        var normalizedId = AssetCoverageCatalog.NormalizeId(metaId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? null
            : TryLoad(MetaIconPath, normalizedId) ?? RealmUi.Icon(normalizedId.Contains("friend") || normalizedId.Contains("guild") ? "people" : "crown");
    }

    public static bool HasUnitIconAsset(UnitDefinition unit)
    {
        if (unit == null)
        {
            return false;
        }

        return HasPng(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.Id))
            || HasPng(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.VisualClass));
    }

    public static bool HasSpellIconAsset(SpellDefinition spell)
    {
        if (spell == null)
        {
            return false;
        }

        return HasPng(SpellIconPath, AssetCoverageCatalog.NormalizeId(spell.Id))
            || HasPng(SpellIconPath, AssetCoverageCatalog.NormalizeId(spell.EffectType));
    }

    public static bool HasRelicIconAsset(EquipmentDefinition relic)
    {
        return relic != null && HasPng(RelicIconPath, AssetCoverageCatalog.NormalizeId(relic.Id));
    }

    public static bool HasCodexPortraitAsset(CodexEntry entry)
    {
        return entry != null && HasPng(CodexPortraitPath, AssetCoverageCatalog.NormalizeId(entry.Id));
    }

    public static bool HasCodexIconAsset(CodexEntry entry)
    {
        return entry != null && HasPng(CodexIconPath, AssetCoverageCatalog.NormalizeId(entry.Id));
    }

    public static bool HasRewardIconAsset(string rewardType, string rewardItemId = "")
    {
        var itemId = AssetCoverageCatalog.NormalizeId(rewardItemId);
        if (!string.IsNullOrWhiteSpace(itemId) && HasPng(RewardIconPath, itemId))
        {
            return true;
        }

        return HasPng(RewardIconPath, AssetCoverageCatalog.NormalizeId(rewardType));
    }

    public static bool HasMetaIconAsset(string metaId)
    {
        return HasPng(MetaIconPath, AssetCoverageCatalog.NormalizeId(metaId));
    }

    private static Texture2D TryLoad(string basePath, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var key = $"{basePath}{id}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (Missing.Contains(key))
        {
            return null;
        }

        var path = $"{basePath}{id}.png";
        if (!ResourceLoader.Exists(path))
        {
            Missing.Add(key);
            return null;
        }

        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture == null)
        {
            Missing.Add(key);
            return null;
        }

        Cache[key] = texture;
        return texture;
    }

    private static bool HasPng(string basePath, string id)
    {
        return !string.IsNullOrWhiteSpace(id) && ResourceLoader.Exists($"{basePath}{id}.png");
    }

    private static bool TryResolveUnit(CodexEntry entry, out UnitDefinition unit)
    {
        unit = GameData.GetPlayerUnits()
            .Concat(GameData.GetEnemyUnits())
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase) ||
                candidate.DisplayName.Equals(entry.Title, StringComparison.OrdinalIgnoreCase));
        return unit != null;
    }

    private static bool TryResolveSpell(CodexEntry entry, out SpellDefinition spell)
    {
        spell = GameData.GetPlayerSpells()
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase) ||
                candidate.DisplayName.Equals(entry.Title, StringComparison.OrdinalIgnoreCase));
        return spell != null;
    }

    private static bool TryResolveRelic(CodexEntry entry, out EquipmentDefinition relic)
    {
        relic = GameData.GetAllEquipment()
            .FirstOrDefault(candidate =>
                candidate.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase) ||
                candidate.DisplayName.Equals(entry.Title, StringComparison.OrdinalIgnoreCase));
        return relic != null;
    }
}
