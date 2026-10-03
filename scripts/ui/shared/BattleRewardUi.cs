using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Reward-only results, using the amounts and items actually granted.</summary>
public static class BattleRewardUi
{
    public static int PanelHeight(int count, bool compact)
    {
        var rows = Mathf.Max(1, Mathf.CeilToInt(count / (compact ? 5f : 4f)));
        return compact ? Mathf.Min(430, 340 + (rows - 1) * 60) : Mathf.Min(640, 400 + (rows - 1) * 120);
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

    public static VBoxContainer Cards(IEnumerable<BattleReward> rewards, bool compact = false)
    {
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
                CustomMinimumSize = new Vector2(compact ? 100 : 136, 0), TooltipText = hint, AccessibilityName = hint };
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
        }
        root.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        return root;
    }

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
