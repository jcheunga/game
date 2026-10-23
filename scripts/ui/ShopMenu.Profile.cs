using System;
using System.Linq;
using Godot;

/// <summary>The selected unit or spell: name, level, role chip, stat tiles and the two actions.</summary>
public partial class ShopMenu
{
    private readonly record struct Action2(string Text, int Cost, bool Enabled, Action Run);

    /// <summary>
    /// The stats as one compact sheet where the concept painted a plaque per stat (painted out of the plate by
    /// art/royal/statsheets.py): two columns across the tiles' span, as tall as its rows, leaving room below.
    /// </summary>
    private void BuildStats(RoyalSpec spec, ArmoryDetailUi.Stat[] stats, float below = 0)
    {
        var first = spec.Rect("stat.0");
        var top = Mathf.Max(first.Position.Y, below);
        _body.AddChild(new StatSheet(stats).Place(new Vector2(first.Position.X, top), spec.Rect("stat.1").End.X - first.Position.X));
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

    private bool LoadoutFull => _state.LoadoutCount >= _state.LoadoutSizeLimit;

    private bool CanToggleEquip(Entry entry) => entry.Owned && (entry.Equipped || !LoadoutFull);

    private string EquipLabel(Entry entry) => entry.Equipped ? "Unequip" : entry.Owned && LoadoutFull ? "Loadout full" : "Equip";

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
        // Verb, divider, coin and cost as one centred row, like every priced action.
        var verb = spec.Label(rightKey + ".label", right.Text, actionRect.Size.X);
        verb.Ink = new Color("2c1a08");
        RoyalLabel cost = null;
        if (right.Cost > 0)
        {
            cost = spec.Has(rightKey + ".cost") ? spec.Label(rightKey + ".cost", right.Cost.ToString("N0"), 90) : RoyalText.Caps(right.Cost.ToString("N0"), verb.FontSize, verb.Ink, 700);
            cost.Ink = verb.Ink; cost.FontSize = verb.FontSize;
        }
        var coin = spec.Rect(spec.First(rightKey + ".coin", rightKey + ".icon"), new Rect2(0, 0, 38, 30)).Size * .85f;
        action.Arrange(verb, price: cost, priceIcon: cost == null ? null : RoyalKit.Texture("coin-pile"), priceIconSize: coin, rule: 16);
        action.Text = right.Cost > 0 ? $"{right.Text} {right.Cost:N0}" : right.Text;
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
        var role = SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag);
        BuildChip(spec, role, entry.Owned ? entry.Equipped ? "Equipped" : "Reserve" : entry.Available ? "Recruit" : "Locked");
        var stats = ArmoryDetailUi.UnitStats(unit);
        // Health, damage, courage, range, recovery and armor: three even rows.
        BuildStats(spec, new[] { stats[0], stats[1], stats[3], stats[4], stats[5], stats[6] });
        if (spec.Has("detail.desc"))
        {
            var resolved=state.BuildPlayerUnitStats(unit);
            _body.AddChild(spec.Label("detail.desc", $"Armor {resolved.Armor:0.#} · Physical {resolved.PhysicalResistance:0.#}% · Magic {resolved.MagicalResistance:0.#}% · Crit {resolved.CriticalChance:0.#}% / +{resolved.CriticalDamageBonus:0.#}%", 470));
        }

        var left = new Action2(EquipLabel(entry), 0, CanToggleEquip(entry), () =>
        {
            state.ToggleDeckUnit(unit.Id, out _);
            state.SetSelectedStage(state.SelectedStage);
            Refresh();
        });
        Action2 right;
        if (!entry.Available) right = new Action2($"Stage {unit.UnlockStage}", 0, false, null);
        else if (!entry.Owned)
        {
            var cost = state.GetUnitPurchaseCost(unit.Id);
            right = new Action2("Recruit", cost, state.Gold >= cost, () => { AudioDirector.Purchased(state.TryPurchaseUnit(unit.Id, out _)); Refresh(); });
        }
        else if (level >= state.MaxUnitLevel)
        {
            var promo = UnitPromotionCatalog.TryGet(unit.Id);
            if (promo != null && !state.IsUnitPromoted(unit.Id))
                right = new Action2("Promote", promo.GoldCost, state.CanPromoteUnit(unit.Id), () =>
                {
                    if (state.TryPromoteUnit(unit.Id, out _)) AudioDirector.Instance?.PlayUpgradeConfirm();
                    Refresh();
                });
            else right = new Action2(state.IsUnitPromoted(unit.Id) ? promo?.PromotedTitle ?? "Promoted" : "Max level", 0, false, null);
        }
        // Training past the player's level cap waits for the level that lifts it.
        else if (level >= state.UnitLevelCap) right = new Action2($"Player level {PlayerLevelCatalog.LevelForUnitCap(level + 1)}", 0, false, null);
        else
        {
            var cost = state.GetUnitUpgradeCost(unit.Id);
            right = new Action2("Upgrade", cost, state.Gold >= cost, () =>
            {
                if (state.TryUpgradeUnit(unit.Id, out _)) AudioDirector.Instance?.PlayUpgradeConfirm();
                Refresh();
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
        var levelLabel = spec.Label("detail.level", entry.Owned ? $"Lv {level}" : entry.Available ? "Scribe" : $"Stage {spell.UnlockStage}", 120);
        _body.AddChild(levelLabel);
        // Level pips only for a spell the player has, set after the level so a longer one never runs into them.
        if (entry.Owned)
        {
            var pips = spec.Rect("detail.pips");
            var x = Mathf.Max(pips.Position.X, levelLabel.Position.X + levelLabel.TextWidth(levelLabel.FittedSize()) + 12);
            _body.AddChild(RoyalKit.Pips(new Vector2(x, pips.Position.Y), Mathf.Min(level, state.MaxSpellLevel), state.MaxSpellLevel, pips.Size.X / 4 + .5f, pips.Size.Y));
        }
        BuildChip(spec, ArmoryDetailUi.SpellRole(spell.EffectType), entry.Owned ? entry.Equipped ? "Equipped" : "Reserve" : entry.Available ? "Scribe" : "Locked");
        // The purpose wraps on the wood above the sheet at the reading size, and the sheet follows it down
        // (on one line, a long purpose shrank below the phone floor and ran out over the parchment).
        var below = 0f;
        if (spec.Has("detail.desc"))
        {
            var x = spec.Number("detail.desc", "pen_x", 756);
            var purpose = RoyalText.Paragraph(ArmoryDetailUi.SpellPurpose(spell.EffectType), ReadingText.PhoneBody, new Color("ede2cf"), 600);
            purpose.AddThemeConstantOverride("line_spacing", -1);
            var font = purpose.GetThemeFont("font"); var size = purpose.GetThemeFontSize("font_size");
            var width = spec.Rect("stat.1").End.X - x;
            purpose.Position = new Vector2(x, spec.Number("detail.desc", "baseline", 382) - font.GetAscent(size));
            purpose.Size = new Vector2(width, font.GetHeight(size) * 3);
            RoyalText.FitLines(purpose, 3);
            _body.AddChild(purpose);
            size = purpose.GetThemeFontSize("font_size");
            var lines = Mathf.Clamp(Mathf.RoundToInt(font.GetMultilineStringSize(purpose.Text, HorizontalAlignment.Left, width, size).Y / font.GetHeight(size)), 1, 3);
            below = purpose.Position.Y + lines * font.GetHeight(size) + (lines - 1) * purpose.GetThemeConstant("line_spacing") + 12;
        }
        BuildStats(spec, ArmoryDetailUi.SpellStats(resolved).ToArray(), below);

        var left = new Action2(EquipLabel(entry), 0, CanToggleEquip(entry), () => { state.ToggleDeckSpell(spell.Id, out _); Refresh(); });
        Action2 right;
        if (!entry.Available) right = new Action2($"Stage {spell.UnlockStage}", 0, false, null);
        else if (!entry.Owned)
        {
            var cost = state.GetSpellPurchaseCost(spell.Id);
            right = new Action2(cost > 0 ? "Scribe" : "Prepare", cost, state.Gold >= cost, () => { AudioDirector.Purchased(state.TryPurchaseSpell(spell.Id, out _)); Refresh(); });
        }
        else if (level < state.MaxSpellLevel)
        {
            var cost = state.GetSpellUpgradeCost(spell.Id);
            right = new Action2("Upgrade", cost, state.Gold >= cost, () =>
            {
                if (state.TryUpgradeSpell(spell.Id, out _)) AudioDirector.Instance?.PlayUpgradeConfirm();
                Refresh();
            });
        }
        else right = new Action2("Max level", 0, false, null);
        BuildActions(spec, left, right, entry.Owned);
    }
}
