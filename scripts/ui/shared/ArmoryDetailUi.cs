using System;
using System.Collections.Generic;
using Godot;

/// <summary>Readable, icon-led profiles shared by the armory and model inspector.</summary>
public static class ArmoryDetailUi
{
    public readonly record struct Stat(string Icon, string Label, string Value);

    public static GridContainer Stats(IEnumerable<Stat> values, int columns = 3)
    {
        var grid = new GridContainer { Name = "ProfileStats", Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var stat in values)
        {
            var tile = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AccessibilityName = $"{stat.Label}: {stat.Value}" };
            tile.SetMeta("modal_unframed", true);
            tile.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 6));
            grid.AddChild(tile);
            var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0); tile.AddChild(stack);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 7); stack.AddChild(row);
            row.AddChild(new TextureRect {
                Texture = RealmUi.Icon(stat.Icon), CustomMinimumSize = new Vector2(23, 23),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color("d6ba80")
            });
            var number = new Label { Text = stat.Value, VerticalAlignment = VerticalAlignment.Center };
            number.AddThemeFontSizeOverride("font_size", 24); number.AddThemeColorOverride("font_color", ModalUi.Cream); row.AddChild(number);
            var label = new Label { Text = stat.Label, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeFontSizeOverride("font_size", 18); label.AddThemeColorOverride("font_color", ModalUi.Muted); stack.AddChild(label);
        }
        return grid;
    }

    public static Stat[] UnitStats(UnitDefinition unit)
    {
        var state = GameState.Instance;
        var stats = unit.IsPlayerSide ? state.BuildPlayerUnitStats(unit) : new UnitStats(unit);
        return new[] {
            new Stat("heart", "Health", $"{stats.MaxHealth:0}"), new Stat("sword", "Damage", $"{stats.AttackDamage:0.#}"),
            new Stat("shield", "Gate damage", $"{stats.BaseDamage}"), new Stat("bolt", "Courage", $"{unit.Cost}"),
            new Stat("eye", "Range", $"{stats.AttackRange:0.#}"),
            new Stat("clock", "Recovery", $"{(unit.IsPlayerSide ? state.ApplyPlayerDeployCooldownUpgrade(unit.DeployCooldown) : unit.DeployCooldown):0.#}s")
        };
    }

    public static List<Stat> SpellStats(ResolvedSpellStats spell)
    {
        var primary = spell.EffectType switch {
            "heal" => new Stat("heart", "Healing", $"{spell.Power:0.#}"),
            "barrier_ward" => new Stat("shield", "Protection", $"{Mathf.RoundToInt((1 - Mathf.Clamp(spell.Power, 0, 1)) * 100)}%"),
            "stone_barricade" => new Stat("shield", "Wall health", $"{spell.Power:0.#}"),
            "war_cry" => new Stat("sword", "Attack boost", $"+{Mathf.RoundToInt((spell.Power - 1) * 100)}%"),
            "resurrect" => new Stat("heart", "Restored health", $"{Mathf.RoundToInt(spell.Power * 100)}%"),
            "polymorph" => new Stat("people", "Targets", "1 enemy"),
            _ => new Stat("sword", "Damage", $"{spell.Power:0.#}")
        };
        var values = new List<Stat> { primary, new("bolt", "Courage", spell.CourageCost.ToString()), new("clock", "Cooldown", $"{spell.Cooldown:0.#}s") };
        if (spell.EffectType is not ("war_cry" or "resurrect" or "stone_barricade")) values.Add(new("eye", "Radius", $"{spell.Radius:0.#}"));
        if (spell.Duration > 0) values.Add(new("clock", "Duration", $"{spell.Duration:0.#}s"));
        if (spell.EffectType == "heal") values.Add(new("hammer", "Wagon repair", $"{spell.SecondaryPower:0.#}"));
        if (spell.EffectType == "war_cry") values.Add(new("arrow", "Speed boost", $"+{Mathf.RoundToInt((spell.SecondaryPower - 1) * 100)}%"));
        return values;
    }

    public static string SpellRole(string effect) => effect switch {
        "heal" or "resurrect" => "Healing", "barrier_ward" or "stone_barricade" => "Protection",
        "frost_burst" or "earthquake" or "polymorph" => "Control", "war_cry" => "Support", _ => "Damage"
    };

    public static string SpellPurpose(string effect) => effect switch {
        "fireball" => "Blast a group of enemies.", "heal" => "Heal allies and repair your wagon.",
        "frost_burst" => "Damage and slow nearby enemies.", "lightning_strike" => "Strike up to three enemies.",
        "barrier_ward" => "Protect nearby allies from damage.", "stone_barricade" => "Block the enemy with a stone wall.",
        "war_cry" => "Boost every ally's attack and speed.", "earthquake" => "Damage and slow enemies over a wide area.",
        "polymorph" => "Turn the strongest enemy into a sheep.", "resurrect" => "Bring back your last fallen ally.", _ => ""
    };

    public static VBoxContainer Disclosure(VBoxContainer host, string title, bool expanded, Action<bool> changed = null)
    {
        var content = new VBoxContainer { Name = "ExtraProfileDetails", Visible = expanded };
        content.AddThemeConstantOverride("separation", 10);
        var toggle = RealmUi.Button("book", title, null);
        toggle.Name = "ProfileDisclosure";
        toggle.ToggleMode = true; toggle.ButtonPressed = expanded;
        toggle.CustomMinimumSize = new Vector2(0, 42);
        toggle.AccessibilityName = title;
        toggle.Pressed += () => { content.Visible = toggle.ButtonPressed; changed?.Invoke(content.Visible); };
        host.AddChild(toggle); host.AddChild(content);
        return content;
    }

    public static void GoldAction(Button button, string label, int amount)
    {
        button.Text = $"{label} {amount:N0}";
        button.Icon = HomeMapArt.Icon("gold"); button.IconAlignment = HorizontalAlignment.Right;
        button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 28);
        button.AddThemeConstantOverride("h_separation", 8);
        if (button is RealmButton realm) realm.CenterIconAndText = true;
        button.SetMeta("realm_primary", true); button.SetMeta("painted_resource_icon", true);
        button.AccessibilityName = $"{label}, {amount:N0} gold";
    }
}
