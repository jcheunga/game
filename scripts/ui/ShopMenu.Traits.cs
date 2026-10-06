using System;
using System.Linq;
using Godot;

/// <summary>
/// Traits & training: everything beyond the concept profile (next level, abilities, doctrines,
/// prestige colours, talents and promotion details) opens from the role chip as an inspector.
/// </summary>
public partial class ShopMenu
{
    private VBoxContainer OpenTraits(string title)
    {
        var layer = new CanvasLayer { Layer = 40, Name = "TraitsLayer" };
        AddChild(layer);
        var modal = RealmModal.OpenInspector(layer, title, "warband", 760, 520);
        modal.Closed = modal.Back = () => { layer.QueueFree(); Refresh(); };
        var stack = RealmUi.Scroll(modal.Content);
        ((ScrollContainer)stack.GetParent()).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddThemeConstantOverride("separation", 12);
        return stack;
    }

    private void ShowUnitTraits(UnitDefinition unit)
    {
        var state = _state;
        var stack = OpenTraits(unit.DisplayName);
        var owned = state.IsUnitOwned(unit.Id);
        var level = state.GetUnitLevel(unit.Id);
        var stats = state.BuildPlayerUnitStats(unit);
        stack.AddChild(ArmoryDetailUi.Stats(new[] {
            new ArmoryDetailUi.Stat("shield", "Gate damage", $"{stats.BaseDamage}"),
            new ArmoryDetailUi.Stat("arrow", "Move speed", $"{stats.Speed:0.#}"),
            new ArmoryDetailUi.Stat("clock", "Attack interval", $"{stats.AttackCooldown:0.##}s") }, 3));
        if (owned && level < state.MaxUnitLevel)
        {
            var next = state.BuildPlayerUnitStatsAtLevel(unit, level + 1);
            stack.AddChild(RealmUi.SectionTitle($"Next level · {level + 1}"));
            stack.AddChild(ArmoryDetailUi.Stats(new[] {
                new ArmoryDetailUi.Stat("heart", "Health", $"+{next.MaxHealth - stats.MaxHealth:0}"),
                new ArmoryDetailUi.Stat("sword", "Damage", $"+{next.AttackDamage - stats.AttackDamage:0.#}"),
                new ArmoryDetailUi.Stat("shield", "Gate damage", $"+{next.BaseDamage - stats.BaseDamage}") }, 3));
        }
        var traits = UnitStatText.BuildInlineTraits(stats).Trim(' ', '·');
        if (traits.Length > 0) stack.AddChild(RealmUi.Label(traits, 18));
        var ability = UnitActiveAbilityCatalog.GetForUnit(unit.Id);
        if (ability != null && level >= ability.UnlockLevel - 1)
        {
            stack.AddChild(RealmUi.SectionTitle(ability.Title));
            stack.AddChild(RealmUi.Label(level < ability.UnlockLevel ? $"Unlocks at level {ability.UnlockLevel} · {ability.CooldownSeconds:0.#}s cooldown" : $"{ability.CooldownSeconds:0.#}s cooldown", 18, true));
            stack.AddChild(RealmUi.Label(ability.Description, 18));
        }
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 10); row.AddThemeConstantOverride("v_separation", 10);
        stack.AddChild(row);
        if (owned)
        {
            var variants = PrestigeColorCatalog.GetUnlockedVariants(unit.Id);
            if (variants.Count > 0)
            {
                var current = state.GetUnitPrestigeIndex(unit.Id);
                var name = current > 0 ? PrestigeColorCatalog.GetVariant(unit.Id, current)?.Title ?? "Default" : "Default";
                row.AddChild(RealmUi.Button("star", $"Colour: {name}", () =>
                {
                    var unlocked = PrestigeColorCatalog.GetUnlockedVariants(unit.Id);
                    var index = state.GetUnitPrestigeIndex(unit.Id);
                    var next = 0; var found = index == 0;
                    foreach (var variant in unlocked) { if (found) { next = variant.PrestigeIndex; break; } if (variant.PrestigeIndex == index) found = true; }
                    state.SetUnitPrestigeIndex(unit.Id, next);
                    GetNode<CanvasLayer>("TraitsLayer").QueueFree(); ShowUnitTraits(unit);
                }));
            }
            var tree = UnitSkillTreeCatalog.GetTree(unit.Id);
            if (tree != null && level >= state.MaxUnitLevel)
                row.AddChild(RealmUi.Button("book", $"Talents ({state.GetUnlockedSkillNodes(unit.Id).Count}/{tree.Nodes.Length})", () => SceneRouter.Instance.GoToSkillTree()));
            var promo = UnitPromotionCatalog.TryGet(unit.Id);
            if (promo != null && !state.IsUnitPromoted(unit.Id) && level >= state.MaxUnitLevel)
                stack.AddChild(RealmUi.Label($"Promotion · {promo.GoldCost} gold and {promo.SigilCost} sigil", 18, true));
        }
        if (owned && !state.IsUnitDoctrineUnlocked(unit.Id))
            stack.AddChild(RealmUi.Label($"Doctrines · level {state.UnitDoctrineUnlockLevel}", 18, true));
        var options = state.GetUnitDoctrineOptions(unit.Id);
        if (owned && state.IsUnitDoctrineUnlocked(unit.Id) && options.Count > 0)
        {
            stack.AddChild(RealmUi.SectionTitle("Doctrine"));
            var doctrines = new HBoxContainer(); doctrines.AddThemeConstantOverride("separation", 8); stack.AddChild(doctrines);
            var current = state.GetUnitDoctrineId(unit.Id);
            var retrain = state.GetUnitDoctrineRetrainCost(unit.Id);
            foreach (var doctrine in options)
            {
                var chosen = current.Equals(doctrine.Id, StringComparison.OrdinalIgnoreCase);
                var label = chosen ? $"{doctrine.Title} ✓" : string.IsNullOrWhiteSpace(current) ? $"Choose {doctrine.Title}" : $"{doctrine.Title} · {retrain} gold";
                var button = RealmUi.Button("shield", label, () =>
                {
                    state.TrySelectUnitDoctrine(unit.Id, doctrine.Id, out var message);
                    var open = GetNode<CanvasLayer>("TraitsLayer"); RemoveChild(open); open.QueueFree();
                    Toast(message); ShowUnitTraits(unit);
                });
                button.Disabled = chosen || (retrain > 0 && state.Gold < retrain);
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                doctrines.AddChild(button);
            }
        }
        RealmModal.Polish(stack);
    }

    private void ShowSpellTraits(SpellDefinition spell)
    {
        var state = _state;
        var stack = OpenTraits(spell.DisplayName);
        var resolved = state.BuildSpellStats(spell);
        stack.AddChild(RealmUi.Label(spell.Description, 18));
        var all = ArmoryDetailUi.SpellStats(resolved);
        if (all.Count > 4) stack.AddChild(ArmoryDetailUi.Stats(all.Skip(4), 2));
        if (state.IsSpellOwned(spell.Id) && resolved.Level < state.MaxSpellLevel)
        {
            stack.AddChild(RealmUi.SectionTitle($"Next level · {resolved.Level + 1}"));
            var next = ArmoryDetailUi.SpellStats(new ResolvedSpellStats(spell, resolved.Level + 1));
            stack.AddChild(ArmoryDetailUi.Stats(next, next.Count <= 4 ? 2 : 3));
        }
        RealmModal.Polish(stack);
    }
}
