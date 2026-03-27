using System.Linq;
using Godot;

public static class UiBadgeFactory
{
    public static Control CreateUnitBadge(UnitDefinition unit, Vector2 size)
    {
        var tint = unit?.GetTint() ?? new Color("64748b");
        return CreateBadge(UiArtLoader.TryLoadUnitIcon(unit), tint, BuildInitials(unit?.DisplayName), size);
    }

    public static Control CreateSpellBadge(SpellDefinition spell, Vector2 size)
    {
        var tint = spell?.GetTint() ?? new Color("64748b");
        return CreateBadge(UiArtLoader.TryLoadSpellIcon(spell), tint, BuildInitials(spell?.DisplayName), size);
    }

    public static Control CreateRelicBadge(EquipmentDefinition relic, Vector2 size)
    {
        return CreateBadge(UiArtLoader.TryLoadRelicIcon(relic), ResolveRelicTint(relic), BuildInitials(relic?.DisplayName), size);
    }

    public static Control CreateCodexBadge(CodexEntry entry, Vector2 size)
    {
        return CreateBadge(UiArtLoader.TryLoadCodexIcon(entry), ResolveCodexTint(entry), BuildInitials(entry?.Title), size);
    }

    public static Control CreateCodexPortrait(CodexEntry entry, Vector2 size)
    {
        return CreateBadge(UiArtLoader.TryLoadCodexPortrait(entry), ResolveCodexTint(entry), BuildInitials(entry?.Title), size, true);
    }

    public static Control CreateRewardBadge(string rewardType, string rewardItemId, string label, Vector2 size)
    {
        var normalizedType = AssetCoverageCatalog.NormalizeId(rewardType);
        var hasItemId = !string.IsNullOrWhiteSpace(AssetCoverageCatalog.NormalizeId(rewardItemId));
        return normalizedType switch
        {
            "unit" when hasItemId => CreateUnitBadge(TryGetUnit(rewardItemId), size),
            "spell" when hasItemId => CreateSpellBadge(TryGetSpell(rewardItemId), size),
            "relic" when hasItemId => CreateRelicBadge(TryGetRelic(rewardItemId), size),
            _ => CreateBadge(
                UiArtLoader.TryLoadRewardIcon(normalizedType, rewardItemId),
                ResolveRewardTint(normalizedType),
                ResolveRewardFallbackText(normalizedType, label),
                size)
        };
    }

    public static Control CreateMetaBadge(string metaId, string label, Vector2 size)
    {
        var normalizedId = AssetCoverageCatalog.NormalizeId(metaId);
        return CreateBadge(
            UiArtLoader.TryLoadMetaIcon(normalizedId),
            ResolveMetaTint(normalizedId),
            ResolveMetaFallbackText(normalizedId, label),
            size);
    }

    public static Control CreateMysteryBadge(Vector2 size)
    {
        return CreateBadge(null, new Color("475569"), "?", size);
    }

    public static Control CreateRewardMetric(string rewardType, string rewardItemId, string text, Vector2 badgeSize)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(CreateRewardBadge(rewardType, rewardItemId, text, badgeSize));

        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        row.AddChild(label);
        return row;
    }

    public static Control CreateMetaMetric(string metaId, string text, Vector2 badgeSize)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(CreateMetaBadge(metaId, text, badgeSize));

        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color("e2e8f0"));
        row.AddChild(label);
        return row;
    }

    public static VBoxContainer CreateStackWithLeadingBadge(Control parent, Control badge, int separation = 12, int stackSpacing = 8)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", separation);
        parent.AddChild(row);

        badge.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.AddChild(badge);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", stackSpacing);
        row.AddChild(stack);
        return stack;
    }

    private static Control CreateBadge(Texture2D texture, Color tint, string fallbackText, Vector2 size, bool large = false)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = size
        };
        frame.SelfModulate = tint.Darkened(0.12f);

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", 4);
        inner.AddThemeConstantOverride("margin_right", 4);
        inner.AddThemeConstantOverride("margin_top", 4);
        inner.AddThemeConstantOverride("margin_bottom", 4);
        frame.AddChild(inner);

        if (texture != null)
        {
            var textureRect = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = large
                    ? TextureRect.StretchModeEnum.KeepAspectCovered
                    : TextureRect.StretchModeEnum.KeepAspectCentered
            };
            textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            inner.AddChild(textureRect);
            return frame;
        }

        var fallback = new ColorRect
        {
            Color = tint.Lerp(new Color("0f172a"), 0.28f)
        };
        fallback.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        inner.AddChild(fallback);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        inner.AddChild(center);

        var label = new Label
        {
            Text = string.IsNullOrWhiteSpace(fallbackText) ? "?" : fallbackText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", Colors.White);
        center.AddChild(label);
        return frame;
    }

    private static Color ResolveRelicTint(EquipmentDefinition relic)
    {
        return relic?.Rarity?.ToLowerInvariant() switch
        {
            "legendary" => new Color("ffd166"),
            "epic" => new Color("c084fc"),
            "rare" => new Color("60a5fa"),
            _ => new Color("94a3b8")
        };
    }

    private static Color ResolveCodexTint(CodexEntry entry)
    {
        if (entry == null)
        {
            return new Color("64748b");
        }

        return entry.Category.ToLowerInvariant() switch
        {
            "boss" => new Color("ef4444"),
            "enemy" => new Color("a855f7"),
            "spell" => new Color("38bdf8"),
            "relic" => new Color("f59e0b"),
            "unit" => new Color("22c55e"),
            _ => new Color("64748b")
        };
    }

    private static Color ResolveRewardTint(string rewardType)
    {
        return rewardType switch
        {
            "gold" => new Color("eab308"),
            "food" => new Color("22c55e"),
            "tomes" => new Color("38bdf8"),
            "essence" => new Color("14b8a6"),
            "sigils" => new Color("f97316"),
            "shards" => new Color("a78bfa"),
            "relic" => new Color("f59e0b"),
            "season_xp" => new Color("38bdf8"),
            "unit" => new Color("22c55e"),
            "spell" => new Color("60a5fa"),
            _ => new Color("64748b")
        };
    }

    private static Color ResolveMetaTint(string metaId)
    {
        return metaId switch
        {
            "arena_rating" => new Color("f97316"),
            "tower_floor" => new Color("38bdf8"),
            "endless_wave" => new Color("a855f7"),
            "daily_streak" => new Color("22c55e"),
            "guild" => new Color("f59e0b"),
            "friends" => new Color("f472b6"),
            "challenge" => new Color("60a5fa"),
            "members" => new Color("94a3b8"),
            _ => new Color("64748b")
        };
    }

    private static string ResolveRewardFallbackText(string rewardType, string label)
    {
        return rewardType switch
        {
            "gold" => "G",
            "food" => "F",
            "tomes" => "T",
            "essence" => "E",
            "sigils" => "S",
            "shards" => "SH",
            "relic" => "R",
            "season_xp" => "XP",
            "unit" => "U",
            "spell" => "SP",
            _ => BuildInitials(label)
        };
    }

    private static string ResolveMetaFallbackText(string metaId, string label)
    {
        return metaId switch
        {
            "arena_rating" => "AR",
            "tower_floor" => "TW",
            "endless_wave" => "EW",
            "daily_streak" => "DS",
            "guild" => "GW",
            "friends" => "FR",
            "challenge" => "CH",
            "members" => "MB",
            _ => BuildInitials(label)
        };
    }

    private static UnitDefinition TryGetUnit(string unitId)
    {
        try
        {
            return GameData.GetUnit(unitId);
        }
        catch
        {
            return null;
        }
    }

    private static SpellDefinition TryGetSpell(string spellId)
    {
        try
        {
            return GameData.GetSpell(spellId);
        }
        catch
        {
            return null;
        }
    }

    private static EquipmentDefinition TryGetRelic(string relicId)
    {
        try
        {
            return GameData.GetEquipment(relicId);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildInitials(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        var parts = text
            .Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part[0].ToString().ToUpperInvariant())
            .Take(2)
            .ToArray();

        return parts.Length == 0 ? "?" : string.Join("", parts);
    }
}
