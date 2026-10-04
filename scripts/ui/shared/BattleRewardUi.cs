using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Reward-only results, using the amounts and items actually granted.</summary>
public static class BattleRewardUi
{
    private static int Rows(int count, bool compact) => Mathf.Max(1, Mathf.CeilToInt(count / (compact ? 5f : 4f)));

    // Captions name each reward while they fit; a full squad's haul keeps the compact cards.
    private static bool Captioned(int count, bool compact) => !compact && Rows(count, compact) <= 2;

    public static int PanelHeight(int count, bool compact)
    {
        var rows = Rows(count, compact);
        if (compact) return Mathf.Min(460, 374 + (rows - 1) * 60);
        return Captioned(count, compact) ? 470 + (rows - 1) * 150 : Mathf.Min(680, 440 + (rows - 1) * 120);
    }

    public static List<BattleReward> Earned(GameSaveData before, GameSaveData after)
    {
        var rewards = new List<BattleReward>();
        void Currency(string kind, int previous, int current)
        {
            if (current > previous) rewards.Add(new(kind, "", current - previous));
        }
        Currency("gold", before.Gold, after.Gold);
        Currency("food", before.Food, after.Food);
        Currency("sigils", before.Sigils, after.Sigils);
        Currency("tomes", before.Tomes, after.Tomes);
        Currency("shards", before.RelicShards, after.RelicShards);
        Currency("essence", before.Essence, after.Essence);
        Currency("season_xp", before.SeasonPassXP, after.SeasonPassXP);
        foreach (var id in after.OwnedEquipmentIds.Except(before.OwnedEquipmentIds, StringComparer.OrdinalIgnoreCase))
            rewards.Add(new("relic", id, 1));
        foreach (var id in after.OwnedPlayerUnitIds.Except(before.OwnedPlayerUnitIds, StringComparer.OrdinalIgnoreCase))
            rewards.Add(new("unit", id, 1));
        foreach (var id in after.OwnedPlayerSpellIds.Except(before.OwnedPlayerSpellIds, StringComparer.OrdinalIgnoreCase))
            rewards.Add(new("spell", id, 1));
        foreach (var id in after.OwnedPlayerUnitIds.Intersect(before.OwnedPlayerUnitIds, StringComparer.OrdinalIgnoreCase))
        {
            var level = after.UnitLevels.GetValueOrDefault(id, 1);
            if (level > before.UnitLevels.GetValueOrDefault(id, 1)) rewards.Add(new("training", id, level));
        }
        foreach (var (id, xp) in after.UnitMasteryXP)
        {
            var gained = xp - before.UnitMasteryXP.GetValueOrDefault(id);
            if (gained > 0 && GameData.PlayerRosterIds.Contains(id)) rewards.Add(new("mastery", id, gained));
        }
        return rewards;
    }

    public static VBoxContainer Cards(IReadOnlyCollection<BattleReward> rewards, bool compact = false)
    {
        var captioned = Captioned(rewards.Count, compact);
        var root = new VBoxContainer { Name = "BattleRewards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var grid = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", compact ? 10 : 14);
        grid.AddThemeConstantOverride("v_separation", compact ? 10 : 14);
        root.AddChild(grid);
        foreach (var reward in rewards.Where(reward => reward.Amount > 0 && (reward.Kind != "mastery" || GameData.PlayerRosterIds.Contains(reward.ItemId))))
        {
            var (texture, name) = Art(reward);
            var suffix = reward.Kind is "season_xp" or "mastery" ? " XP" : "";
            var value = reward.Kind == "training" ? $"Lv {reward.Amount}" : $"+{reward.Amount:N0}{suffix}";
            var hint = $"{name}: {value}";
            var card = new PanelContainer { Name = "Reward" + reward.Kind + reward.ItemId,
                CustomMinimumSize = new Vector2(compact ? 100 : 150, 0), TooltipText = hint, AccessibilityName = hint };
            card.SetMeta("modal_unframed", true);
            card.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, compact ? 8 : 12));
            grid.AddChild(card);
            var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", compact ? 4 : 8); card.AddChild(stack);
            stack.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(0, compact ? 40 : 64),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore });
            var amount = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            amount.AddThemeFontSizeOverride("font_size", compact ? 22 : 28);
            amount.AddThemeColorOverride("font_color", ModalUi.Cream);
            stack.AddChild(amount);
            if (!captioned) continue;
            var caption = new Label { Text = Caption(reward, name), HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
            caption.AddThemeFontSizeOverride("font_size", 18);
            caption.AddThemeColorOverride("font_color", ModalUi.Muted);
            stack.AddChild(caption);
        }
        root.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        return root;
    }

    // The portrait already names the ally, so experience cards say what was gained.
    private static string Caption(BattleReward reward, string name) => reward.Kind switch
    {
        "season_xp" => "Season",
        "mastery" => "Mastery",
        "training" => "Training",
        _ => name
    };

    private static (Texture2D Texture, string Name) Art(BattleReward reward) => reward.Kind switch
    {
        "relic" => (UiArtLoader.TryLoadRelicIcon(GameData.GetEquipment(reward.ItemId)), GameData.GetEquipment(reward.ItemId).DisplayName),
        "unit" => (UiArtLoader.TryLoadUnitIcon(GameData.GetUnit(reward.ItemId)), GameData.GetUnit(reward.ItemId).DisplayName),
        "spell" => (UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(reward.ItemId)), GameData.GetSpell(reward.ItemId).DisplayName),
        "training" => (UiArtLoader.TryLoadUnitIcon(GameData.GetUnit(reward.ItemId)), GameData.GetUnit(reward.ItemId).DisplayName + " training"),
        "mastery" => (UiArtLoader.TryLoadUnitIcon(GameData.GetUnit(reward.ItemId)), GameData.GetUnit(reward.ItemId).DisplayName + " mastery"),
        "season_xp" => (HomeMapArt.Icon("star"), "Season experience"),
        "food" => (HomeMapArt.Icon("food"), "Rations"),
        _ => (UiArtLoader.TryLoadRewardIcon(reward.Kind), char.ToUpperInvariant(reward.Kind[0]) + reward.Kind[1..])
    };
}
