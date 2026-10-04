using System;
using Godot;

public partial class ShopMenu
{
    private bool _profileExpanded;
    private PanelContainer DetailShell(string title, string status, string id, bool spell, out VBoxContainer stack)
    {
        var panel = new PanelContainer
        {
            Name = "RosterDetail",
            SelfModulate = Colors.White
        };
        panel.SetMeta("modal_unframed", true);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var padding = new MarginContainer();
        foreach (var side in new[]
        {
            "left",
            "right",
            "top",
            "bottom"
        }

        )
            padding.AddThemeConstantOverride("margin_" + side, 0);
        panel.AddChild(padding);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        padding.AddChild(row);
        stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 6);
        row.AddChild(stack);
        var heading = RealmUi.Heading(title, 28);
        heading.AddThemeColorOverride("font_color", new Color("ffd47d"));
        stack.AddChild(heading);
        var state = RealmUi.Label(status, 18, true);
        state.AddThemeFontSizeOverride("font_size", 18);
        stack.AddChild(state);
        return panel;
    }

    private VBoxContainer UnitExtraDetails(VBoxContainer stack, UnitDefinition unit, bool owned, int level, bool max)
    {
        var extra = ArmoryDetailUi.Disclosure(stack, "Traits & training", _profileExpanded, value => _profileExpanded = value);
        var stats = GameState.Instance.BuildPlayerUnitStats(unit);
        extra.AddChild(ArmoryDetailUi.Stats(new[] { new ArmoryDetailUi.Stat("arrow", "Move speed", $"{stats.Speed:0.#}"), new ArmoryDetailUi.Stat("clock", "Attack interval", $"{stats.AttackCooldown:0.##}s") }, 2));
        if (owned && !max)
        {
            var next = GameState.Instance.BuildPlayerUnitStatsAtLevel(unit, level + 1);
            extra.AddChild(RealmUi.Label($"Next level · {level + 1}", 18, true));
            extra.AddChild(ArmoryDetailUi.Stats(new[] { new ArmoryDetailUi.Stat("heart", "Health", $"+{next.MaxHealth - stats.MaxHealth:0}"), new ArmoryDetailUi.Stat("sword", "Damage", $"+{next.AttackDamage - stats.AttackDamage:0.#}"), new ArmoryDetailUi.Stat("shield", "Gate damage", $"+{next.BaseDamage - stats.BaseDamage}") }));
        }

        var traits = UnitStatText.BuildInlineTraits(stats).Trim(' ', '·');
        if (traits.Length > 0)
            extra.AddChild(RealmUi.Label(traits, 18));
        var ability = UnitActiveAbilityCatalog.GetForUnit(unit.Id);
        if (ability != null && level >= ability.UnlockLevel - 1)
        {
            extra.AddChild(RealmUi.Heading(ability.Title, 18));
            extra.AddChild(RealmUi.Label(level < ability.UnlockLevel ? $"Unlocks at level {ability.UnlockLevel} · {ability.CooldownSeconds:0.#}s cooldown" : $"{ability.CooldownSeconds:0.#}s cooldown", 18, true));
            extra.AddChild(RealmUi.Label(ability.Description, 18));
        }

        if (owned && !GameState.Instance.IsUnitDoctrineUnlocked(unit.Id))
            extra.AddChild(RealmUi.Label($"Doctrines · level {GameState.Instance.UnitDoctrineUnlockLevel}", 18, true));
        return extra;
    }
}
