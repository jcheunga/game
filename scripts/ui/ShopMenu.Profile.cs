using System;
using System.Linq;
using Godot;

/// <summary>The selected unit or spell: name, level, role chip, stat tiles and the two actions.</summary>
public partial class ShopMenu
{
    private readonly record struct Action2(string Text, int Cost, bool Enabled, Action Run);

    private string StatIcon(string icon) => icon switch
    {
        "heart" => "stat-health", "sword" => "stat-damage", "bolt" => "stat-courage",
        "eye" => _tab == 1 ? "spellstat-radius" : "stat-range",
        "clock" => _tab == 1 ? "spellstat-cooldown" : "stat-recovery", _ => "stat-recovery"
    };

    private void BuildStats(RoyalSpec spec, ArmoryDetailUi.Stat[] stats)
    {
        for (var i = 0; i < stats.Length && spec.Has($"stat.{i}"); i++)
        {
            var tile = spec.Rect($"stat.{i}");
            _body.AddChild(RoyalKit.Image(StatIcon(stats[i].Icon), spec.Rect($"stat.{i}.icon", new Rect2(tile.Position + new Vector2(11, 17), new Vector2(30, 30)))));
            var caption = spec.Label($"stat.{i}.caption", stats[i].Label.ToUpperInvariant(), tile.End.X - spec.Number($"stat.{i}.caption", "pen_x", tile.Position.X + 60) - 8);
            var value = spec.Label($"stat.{i}.value", stats[i].Value, tile.End.X - spec.Number($"stat.{i}.value", "pen_x", tile.Position.X + 60) - 8);
            _body.AddChild(caption); _body.AddChild(value);
            var name = $"{stats[i].Label}: {stats[i].Value}";
            var hint = new Control { Position = tile.Position, Size = tile.Size, TooltipText = name, MouseFilter = MouseFilterEnum.Pass };
            _body.AddChild(hint);
        }
    }

    /// <summary>The role and status chip beside the name.</summary>
    private void BuildChip(RoyalSpec spec, string role, string status)
    {
        var rect = spec.Rect("detail.chip");
        var chip = new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore, AccessibilityName = $"{role} · {status}" };
        chip.AddThemeStyleboxOverride("panel", RoyalKit.Slice("chip", 16, 6, 16, 6));
        _body.AddChild(chip);
        chip.AddChild(RoyalKit.Image("chip-support", new Rect2(-1, -1, 34, 36)));
        var first = spec.Label("detail.chip.label", role, 100);
        var second = spec.Label("detail.chip.label2", status, 100);
        foreach (var label in new[] { first, second }) { label.Position -= rect.Position; chip.AddChild(label); }
        var gap = first.Position.X + first.TextWidth(first.FontSize);
        var dot = RoyalText.Serif("·", first.FontSize, first.Ink);
        dot.Position = new Vector2(gap + 8, first.Position.Y); dot.Size = new Vector2(10, first.Size.Y); dot.Baseline = first.Baseline;
        chip.AddChild(dot);
        second.Position = new Vector2(gap + 22, second.Position.Y);
        chip.Size = new Vector2(Mathf.Max(rect.Size.X, second.Position.X + second.TextWidth(second.FontSize) + 18), rect.Size.Y);
    }

    private void BuildActions(RoyalSpec spec, Action2 left, Action2 right, bool showLeft)
    {
        var leftKey = spec.First("button.left", "button.back", "button.unequip");
        var rightKey = spec.First("button.right", "button.scribe", "button.upgrade");
        if (showLeft)
        {
            var rect = spec.Rect(leftKey);
            var button = RoyalButton.Over(rect, left.Text, left.Run, 6);
            button.Disabled = !left.Enabled;
            var label = spec.Label(leftKey + ".label", left.Text, rect.Size.X - 24);
            label.Align = HorizontalAlignment.Center; label.Size = new Vector2(rect.Size.X - 24, label.Size.Y);
            label.Position = new Vector2(rect.Position.X + 12, label.Position.Y);
            label.Position -= rect.Position;
            button.SetCaption(label, new Rect2(label.Position, label.Size));
            _body.AddChild(button);
        }
        else
        {
            // The painted button stays as a quiet empty plaque when the left action does not apply.
            var veil = new Panel { Position = spec.Rect(leftKey).Position + new Vector2(3, 3), Size = spec.Rect(leftKey).Size - new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore };
            veil.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(.03f, .04f, .06f, .55f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
            _body.AddChild(veil);
        }
        var actionRect = spec.Rect(rightKey);
        var action = RoyalButton.Over(actionRect, right.Text, right.Run, 6);
        action.Disabled = !right.Enabled;
        _body.AddChild(action);
        // Verb, cost and coin are centred as one group, the way the concept spaces them.
        var verb = spec.Label(rightKey + ".label", right.Text.ToUpperInvariant(), actionRect.Size.X);
        var ink = new Color("2c1a08");
        verb.Ink = ink; verb.ShadowInk = new Color(1, .95f, .8f, .3f);
        var verbWidth = verb.TextWidth(verb.FontSize);
        RoyalLabel cost = null; float costWidth = 0;
        var coinRect = spec.Rect(spec.First(rightKey + ".coin", rightKey + ".icon"), new Rect2(0, 0, 38, 30));
        if (right.Cost > 0)
        {
            cost = spec.Has(rightKey + ".cost") ? spec.Label(rightKey + ".cost", right.Cost.ToString("N0"), 90) : RoyalText.Caps(right.Cost.ToString("N0"), verb.FontSize, ink, 700);
            cost.Ink = ink; cost.ShadowInk = verb.ShadowInk;
            costWidth = cost.TextWidth(cost.FontSize);
        }
        var group = verbWidth + (cost != null ? 12 + costWidth + 10 + coinRect.Size.X : 0);
        var left0 = actionRect.Size.X / 2 - group / 2;
        verb.Align = HorizontalAlignment.Left; verb.ShrinkToFit = false;
        verb.Position = new Vector2(left0, verb.Position.Y - actionRect.Position.Y);
        action.SetCaption(verb, new Rect2(verb.Position, new Vector2(verbWidth + 4, verb.Size.Y)));
        action.Text = right.Cost > 0 ? $"{right.Text} {right.Cost:N0}" : right.Text;
        if (cost != null)
        {
            cost.Align = HorizontalAlignment.Left; cost.ShrinkToFit = false;
            cost.Position = new Vector2(left0 + verbWidth + 12, verb.Position.Y + verb.Baseline - cost.Baseline + (cost.FontSize - verb.FontSize) * 0f);
            cost.Size = new Vector2(costWidth + 4, cost.Size.Y);
            action.AddChild(cost);
            action.AddChild(RoyalKit.Image("coin-pile", new Rect2(new Vector2(left0 + verbWidth + 12 + costWidth + 10, coinRect.Position.Y - actionRect.Position.Y), coinRect.Size)));
        }
    }

    private void Toast(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        RoyalToast.Show(this, message);
    }

    private void BuildUnitProfile(RoyalSpec spec, UnitDefinition unit, Entry entry)
    {
        var state = _state;
        _body.AddChild(RoyalKit.Image(ClassIcon(unit), spec.Rect("detail.icon")));
        _body.AddChild(spec.Label("detail.name", unit.DisplayName, 440));
        var level = entry.Owned ? entry.Level : 0;
        if (spec.Has("detail.level.value") && entry.Owned)
        {
            var word = spec.Label("detail.level", "LEVEL", 90);
            _body.AddChild(word);
            var number = spec.Label("detail.level.value", level.ToString(), 40);
            number.Position = new Vector2(word.Position.X + word.TextWidth(word.FontSize) + 10, number.Position.Y);
            _body.AddChild(number);
        }
        else _body.AddChild(spec.Label("detail.level", entry.Owned ? $"LEVEL {level}" : entry.Available ? "RECRUIT" : $"STAGE {unit.UnlockStage}", 120));
        var pips = spec.Rect("detail.pips");
        _body.AddChild(RoyalKit.Pips(pips.Position, Mathf.Min(level, UnitPips), UnitPips, pips.Size.X / UnitPips + .5f, pips.Size.Y));
        var role = SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag);
        BuildChip(spec, role, entry.Owned ? entry.Equipped ? "Equipped" : "Reserve" : entry.Available ? "Recruit" : "Locked");
        var stats = ArmoryDetailUi.UnitStats(unit);
        // The concept profile: health, damage, courage, range and recovery.
        BuildStats(spec, new[] { stats[0], stats[1], stats[3], stats[4], stats[5] });

        var left = new Action2(entry.Equipped ? "Unequip" : "Equip", 0, entry.Owned, () =>
        {
            state.ToggleDeckUnit(unit.Id, out var message);
            state.SetSelectedStage(state.SelectedStage);
            Toast(message); Refresh();
        });
        Action2 right;
        if (!entry.Available) right = new Action2($"Stage {unit.UnlockStage}", 0, false, null);
        else if (!entry.Owned)
        {
            var cost = state.GetUnitPurchaseCost(unit.Id);
            right = new Action2("Recruit", cost, state.Gold >= cost, () => { AudioDirector.Purchased(state.TryPurchaseUnit(unit.Id, out var message)); Toast(message); Refresh(); });
        }
        else if (level >= state.MaxUnitLevel)
        {
            var promo = UnitPromotionCatalog.TryGet(unit.Id);
            if (promo != null && !state.IsUnitPromoted(unit.Id))
                right = new Action2("Promote", promo.GoldCost, state.CanPromoteUnit(unit.Id), () =>
                {
                    if (state.TryPromoteUnit(unit.Id, out var message)) AudioDirector.Instance?.PlayUpgradeConfirm();
                    Toast(message); Refresh();
                });
            else right = new Action2(state.IsUnitPromoted(unit.Id) ? promo?.PromotedTitle ?? "Promoted" : "Max level", 0, false, null);
        }
        else
        {
            var cost = state.GetUnitUpgradeCost(unit.Id);
            right = new Action2("Upgrade", cost, state.Gold >= cost, () =>
            {
                if (state.TryUpgradeUnit(unit.Id, out var message)) AudioDirector.Instance?.PlayUpgradeConfirm();
                Toast(message); Refresh();
            });
        }
        BuildActions(spec, left, right, entry.Owned);
    }

    private void BuildSpellProfile(RoyalSpec spec, SpellDefinition spell, Entry entry)
    {
        var state = _state;
        var resolved = state.BuildSpellStats(spell);
        _body.AddChild(RoyalKit.Image(SpellIcon(spell), spec.Rect("detail.icon")));
        _body.AddChild(spec.Label("detail.name", spell.DisplayName, 440));
        var level = entry.Owned ? resolved.Level : 0;
        _body.AddChild(spec.Label("detail.level", entry.Owned ? $"Lv {level}" : entry.Available ? "Scribe" : $"Stage {spell.UnlockStage}", 120));
        var pips = spec.Rect("detail.pips");
        _body.AddChild(RoyalKit.Pips(pips.Position, Mathf.Min(level, state.MaxSpellLevel), state.MaxSpellLevel, pips.Size.X / 4 + .5f, pips.Size.Y));
        BuildChip(spec, ArmoryDetailUi.SpellRole(spell.EffectType), entry.Owned ? entry.Equipped ? "Equipped" : "Reserve" : entry.Available ? "Scribe" : "Locked");
        if (spec.Has("detail.desc"))
            _body.AddChild(spec.Label("detail.desc", ArmoryDetailUi.SpellPurpose(spell.EffectType), 470));
        BuildStats(spec, ArmoryDetailUi.SpellStats(resolved).Take(4).ToArray());

        var left = new Action2(entry.Equipped ? "Unequip" : "Equip", 0, entry.Owned, () => { state.ToggleDeckSpell(spell.Id, out var message); Toast(message); Refresh(); });
        Action2 right;
        if (!entry.Available) right = new Action2($"Stage {spell.UnlockStage}", 0, false, null);
        else if (!entry.Owned)
        {
            var cost = state.GetSpellPurchaseCost(spell.Id);
            right = new Action2(cost > 0 ? "Scribe" : "Prepare", cost, state.Gold >= cost, () => { AudioDirector.Purchased(state.TryPurchaseSpell(spell.Id, out var message)); Toast(message); Refresh(); });
        }
        else if (level < state.MaxSpellLevel)
        {
            var cost = state.GetSpellUpgradeCost(spell.Id);
            right = new Action2("Upgrade", cost, state.Gold >= cost, () =>
            {
                if (state.TryUpgradeSpell(spell.Id, out var message)) AudioDirector.Instance?.PlayUpgradeConfirm();
                Toast(message); Refresh();
            });
        }
        else right = new Action2("Max level", 0, false, null);
        BuildActions(spec, left, right, entry.Owned);
    }
}
