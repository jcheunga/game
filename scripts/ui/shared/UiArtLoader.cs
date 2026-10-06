using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public static class UiArtLoader
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    private static readonly HashSet<string> Missing = new();
    private static readonly Dictionary<ulong, Texture2D> Trimmed = new();

    public static Texture2D Portrait(Texture2D source)
    {
        if (source == null) return null;
        var key = source.GetInstanceId();
        if (Trimmed.TryGetValue(key, out var cached)) return cached;
        using var pixels = source.GetImage();
        if (pixels == null) return source;
        if (pixels.IsCompressed()) pixels.Decompress();
        var used = pixels.GetUsedRect();
        if (used.Size.X <= 0 || used.Size.Y <= 0) return source;
        return Trimmed[key] = new AtlasTexture { Atlas = source, Region = used, FilterClip = true };
    }

    private const string UnitIconPath = "res://assets/ui/icons/units/";
    private const string RewardIconPath = "res://assets/ui/icons/rewards/";
    private const string MetaIconPath = "res://assets/ui/icons/meta/";
    private const string CodexPortraitPath = "res://assets/ui/portraits/codex/";

    public static Texture2D TryLoadUnitIcon(UnitDefinition unit)
    {
        if (unit == null) return null;
        var icon = TryLoad(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.Id))
            ?? TryLoad(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.VisualClass));
        return Portrait(icon) ?? RealmUi.Icon(AssetCoverageCatalog.NormalizeId(unit.VisualClass) switch
            { "shield" => "shield", "gunner" or "sniper" => "arrow", "support" => "heart", "boss" => "crown", _ => "sword" });
    }

    /// <summary>A spell's painted image (art/royal/items.py), or a plain glyph if one is missing.</summary>
    public static Texture2D TryLoadSpellIcon(SpellDefinition spell)
    {
        if (spell == null) return null;
        if (RoyalItem(spell.Id) is { } painted) return painted;
        var effect = AssetCoverageCatalog.NormalizeId(spell.EffectType) ?? "";
        return RealmUi.Icon(effect.Contains("heal") ? "heart" : effect.Contains("barrier") ? "shield" : effect.Contains("fire") ? "flame" : "bolt");
    }

    /// <summary>A relic's painted image (art/royal/items.py), or null if one is missing.</summary>
    public static Texture2D TryLoadRelicIcon(EquipmentDefinition relic) => relic == null ? null : RoyalItem(relic.Id);

    /// <summary>The painted item image (art/royal/items.py) when one is installed.</summary>
    private static Texture2D RoyalItem(string id)
    {
        var path = $"res://assets/ui/royal/items/{id}.png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    /// <summary>
    /// A codex entry's picture: spells and relics use their painted images, the legacy foes and raid bosses
    /// their own portraits, and units without one their battle icon.
    /// </summary>
    public static Texture2D TryLoadCodexPortrait(CodexEntry entry)
    {
        if (entry == null) return null;
        if (TryResolveSpell(entry, out var spell)) return TryLoadSpellIcon(spell);
        if (TryResolveRelic(entry, out var relic) && TryLoadRelicIcon(relic) is { } painted) return painted;
        return TryLoad(CodexPortraitPath, AssetCoverageCatalog.NormalizeId(entry.Id))
            ?? (TryResolveUnit(entry, out var unit) ? TryLoadUnitIcon(unit) : null);
    }

    public static Texture2D TryLoadRewardIcon(string rewardType, string rewardItemId = "")
    {
        var itemId = AssetCoverageCatalog.NormalizeId(rewardItemId);
        if (!string.IsNullOrWhiteSpace(itemId) && TryLoad(RewardIconPath, itemId) is { } byItemId) return byItemId;
        var typeId = AssetCoverageCatalog.NormalizeId(rewardType);
        // Currency rewards share the painted icons used by the home map.
        if (typeId is "gold" or "food") return HomeMapArt.Icon(typeId);
        if (typeId is "tomes" or "essence") return HomeMapArt.Icon(typeId == "tomes" ? "book" : "flame");
        return string.IsNullOrWhiteSpace(typeId)
            ? null
            : TryLoad(RewardIconPath, typeId) ?? RealmUi.Icon(typeId.Contains("star") ? "star" : "gift");
    }

    public static Texture2D TryLoadMetaIcon(string metaId)
    {
        var normalizedId = AssetCoverageCatalog.NormalizeId(metaId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? null
            : TryLoad(MetaIconPath, normalizedId) ?? RealmUi.Icon(normalizedId.Contains("friend") || normalizedId.Contains("guild") ? "people" : "crown");
    }

    public static bool HasUnitIconAsset(UnitDefinition unit) => unit != null &&
        (HasPng(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.Id)) || HasPng(UnitIconPath, AssetCoverageCatalog.NormalizeId(unit.VisualClass)));

    public static bool HasSpellIconAsset(SpellDefinition spell) => spell != null && HasPng("res://assets/ui/royal/items/", spell.Id);

    public static bool HasRelicIconAsset(EquipmentDefinition relic) => relic != null && HasPng("res://assets/ui/royal/items/", relic.Id);

    /// <summary>Whether a codex entry has a picture of its own (painted image, portrait or battle icon).</summary>
    public static bool HasCodexPictureAsset(CodexEntry entry) => entry != null &&
        (TryResolveSpell(entry, out var spell) ? HasSpellIconAsset(spell)
            : TryResolveRelic(entry, out var relic) && HasRelicIconAsset(relic)
            || HasPng(CodexPortraitPath, AssetCoverageCatalog.NormalizeId(entry.Id))
            || TryResolveUnit(entry, out var unit) && HasUnitIconAsset(unit));

    public static bool HasRewardIconAsset(string rewardType, string rewardItemId = "")
    {
        var itemId = AssetCoverageCatalog.NormalizeId(rewardItemId);
        if (!string.IsNullOrWhiteSpace(itemId) && HasPng(RewardIconPath, itemId)) return true;
        var typeId = AssetCoverageCatalog.NormalizeId(rewardType);
        return typeId is "gold" or "food" or "tomes" or "essence" || HasPng(RewardIconPath, typeId);
    }

    public static bool HasMetaIconAsset(string metaId) => HasPng(MetaIconPath, AssetCoverageCatalog.NormalizeId(metaId));

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
