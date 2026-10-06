#nullable enable
using System;
using System.Linq;
using Godot;

/// <summary>
/// The Crownroad codex as the approved open book: category tabs, a page of nine discovered
/// portraits on the left, and the selected entry's portrait, stats and lore on the right.
/// </summary>
public partial class CodexMenu : RoyalScreen
{
    private static readonly string[] Categories = { "All", "Enemies", "Bosses", "Units", "Spells", "Relics" };
    private const int PageSize = 9;
    private string _activeCategory = "All";
    private string? _selectedEntryId;
    private int _page;
    private Control _layer = null!;
    private static Shader? _vignette;

    public CodexMenu() { PlateName = "codex"; }

    private static RoyalSpec Spec => RoyalSpec.For("codex");
    private static string CategoryKey(string category) => category switch { "Enemies" => "enemy", "Bosses" => "boss", "Units" => "unit", "Spells" => "spell", "Relics" => "relic", _ => category };

    protected override void Build()
    {
        _layer = Layer("Live");
        RefreshBook();
    }

    private void RefreshBook()
    {
        RoyalUiTools.Clear(_layer);
        var spec = Spec;
        var state = GameState.Instance;
        _layer.AddChild(spec.Label("title", "The Crownroad Codex", 440));
        _layer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", Close, 6));
        for (var i = 0; i < Categories.Length; i++)
        {
            var category = Categories[i]; var key = "tab." + category.ToLowerInvariant();
            var rect = spec.Rect(key);
            var tab = RoyalButton.Over(rect, category, () => { _activeCategory = category; _selectedEntryId = null; _page = 0; RefreshBook(); }, 4);
            var selected = category == _activeCategory;
            if (selected) tab.SetStates(RoyalKit.Slice("codex-tab-selected", 10), 4);
            tab.MarkTab(selected);
            var icon = spec.Rect(key + ".icon");
            tab.SetGlyph(RoyalKit.Texture("codextab-" + category.ToLowerInvariant()), new Rect2(icon.Position - rect.Position, icon.Size));
            var label = spec.Label(key + ".label", category, rect.Size.X - 6, selected ? new Color("f8ea9d") : new Color("d2cbca"));
            label.Position -= rect.Position;
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
            _layer.AddChild(tab);
        }

        var entries = (_activeCategory == "All" ? CodexCatalog.GetAll() : CodexCatalog.GetByCategory(CategoryKey(_activeCategory)))
            .Select((entry, order) => (entry, order))
            .OrderByDescending(item => state.IsCodexEntryDiscovered(item.entry.Id)).ThenBy(item => item.order).Select(item => item.entry).ToArray();
        var pages = Math.Max(1, (entries.Length + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        var slice = entries.Skip(_page * PageSize).Take(PageSize).ToArray();
        _selectedEntryId ??= slice.FirstOrDefault(entry => state.IsCodexEntryDiscovered(entry.Id))?.Id;

        var ink = new Color("22150e");
        var heading = spec.Label("list.heading", $"DISCOVERED {entries.Count(entry => state.IsCodexEntryDiscovered(entry.Id))} / {entries.Length}", 150, ink);
        heading.ShadowOffset = Vector2.Zero; _layer.AddChild(heading);
        // The ornamental rule fills the gap between the heading and the page count.
        var rule = spec.Rect("list.heading.rule");
        var ruleLeft = Mathf.Max(rule.Position.X, heading.Position.X + heading.TextWidth(heading.FontSize) + 12);
        var ruleRight = spec.Number("list.page", "pen_x", rule.End.X + 20) - 12;
        if (ruleRight - ruleLeft > 30) _layer.AddChild(RoyalKit.Image("codex-rule", new Rect2(ruleLeft, rule.Position.Y, ruleRight - ruleLeft, rule.Size.Y)));
        var pageLabel = spec.Label("list.page", $"PAGE {_page + 1} OF {pages}", 100, new Color("1b0f07"));
        pageLabel.ShadowOffset = Vector2.Zero; _layer.AddChild(pageLabel);
        for (var side = 0; side < 2; side++)
        {
            var key = side == 0 ? "page.prev" : "page.next";
            var direction = side == 0 ? -1 : 1;
            var button = RoyalButton.Over(spec.Rect(key), side == 0 ? "Previous codex page" : "Next codex page", () => { _page += direction; RefreshBook(); }, 4);
            button.SetStates(RoyalKit.Slice("codex-page-button", 8), 4);
            var icon = spec.Rect(key + ".icon");
            button.SetGlyph(RoyalKit.Texture("codex-arrow"), new Rect2(icon.Position - spec.Rect(key).Position, icon.Size));
            if (side == 1) button.Glyph.FlipH = true;
            button.Disabled = side == 0 ? _page == 0 : _page >= pages - 1;
            _layer.AddChild(button);
        }
        _layer.AddChild(RoyalKit.Image("codex-ornament", spec.Rect("list.footer.ornament")));
        for (var i = 0; i < slice.Length; i++) _layer.AddChild(Cell(spec, i + 1, slice[i]));

        var chosen = entries.FirstOrDefault(entry => entry.Id == _selectedEntryId && state.IsCodexEntryDiscovered(entry.Id));
        if (chosen == null) BuildEmptyPage(spec);
        else BuildDetail(spec, chosen);
        var back = RoyalButton.Over(spec.Rect("button.back"), "Back to map", Close, 6);
        back.SetStates(RoyalKit.Slice("codex-back", 16), 6);
        back.SetGlyph(RoyalKit.Texture("icon-book-map"), new Rect2(spec.Rect("button.back.icon").Position - spec.Rect("button.back").Position, spec.Rect("button.back.icon").Size));
        var backLabel = spec.Label("button.back.label", "BACK TO MAP", 160);
        backLabel.Position -= spec.Rect("button.back").Position;
        back.SetCaption(backLabel, new Rect2(backLabel.Position, backLabel.Size));
        _layer.AddChild(back);
    }

    private Control Cell(RoyalSpec spec, int index, CodexEntry entry)
    {
        var state = GameState.Instance;
        var key = $"cell.{index}";
        var rect = spec.Rect(key);
        var known = state.IsCodexEntryDiscovered(entry.Id);
        var selected = entry.Id == _selectedEntryId;
        var cell = new RoyalButton { Position = rect.Position, Size = rect.Size, AccessibilityName = known ? entry.Title : "Undiscovered codex entry",
            TooltipText = known ? entry.Title : "Encounter this creature or acquire this item to discover it", Disabled = !known, MouseDefaultCursorShape = CursorShape.PointingHand };
        cell.SetStates(RoyalKit.Slice(selected ? "codex-cell-selected" : "codex-cell", 10), 4, null, RoyalKit.Slice("codex-cell", 10));
        var art = new Rect2(spec.Rect(key + ".art").Position - rect.Position, spec.Rect(key + ".art").Size);
        var picture = Portrait(entry);
        if (picture != null)
            cell.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = picture, Position = art.Position + new Vector2(6, 4), Size = art.Size - new Vector2(12, 6), MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps, SelfModulate = known ? Colors.White : new Color(.06f, .05f, .05f, .82f) });
        if (!known)
        {
            var mark = RoyalText.Serif("?", 54, new Color("8d8172"), 600);
            mark.Align = HorizontalAlignment.Center;
            mark.Position = new Vector2(0, rect.Size.Y / 2 - 30); mark.Size = new Vector2(rect.Size.X, 60);
            cell.AddChild(mark);
        }
        cell.AddChild(RoyalKit.Image("codex-corner", new Rect2(spec.Rect(key + ".corner").Position - rect.Position, spec.Rect(key + ".corner").Size)));
        cell.Pressed += () => { _selectedEntryId = entry.Id; RefreshBook(); };
        return cell;
    }

    /// <summary>Units and enemies stand as their battle figures; spells and relics use their painted art.</summary>
    private static Texture2D? Portrait(CodexEntry entry) =>
        GameData.TryGetUnit(entry.Id) is { } unit ? UnitFigure.For(unit) : UiArtLoader.TryLoadCodexPortrait(entry);

    private (string Role, string Icon, (string Caption, string Value, string Icon)[] Stats) Facts(CodexEntry entry)
    {
        if (GameData.TryGetUnit(entry.Id) is { } unit)
        {
            var stats = unit.IsPlayerSide ? GameState.Instance.BuildPlayerUnitStats(unit) : new UnitStats(unit);
            var role = entry.Category == "boss" ? "Boss" : unit.IsPlayerSide ? SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag) : "Rotbound";
            return (role, "codex-ribbon-shield", new[] { ("HEALTH", $"{stats.MaxHealth:0}", "codexstat-health"), ("DAMAGE", $"{stats.AttackDamage:0.#}", "codexstat-damage") });
        }
        if (GameData.TryGetSpell(entry.Id) is { } spell)
        {
            var resolved = GameState.Instance.BuildSpellStats(spell);
            return (ArmoryDetailUi.SpellRole(spell.EffectType), "codex-ribbon-shield", new[] { ("POWER", $"{resolved.Power:0.#}", "codexstat-damage"), ("COOLDOWN", $"{resolved.Cooldown:0.#}s", "codexstat-health") });
        }
        if (GameData.GetAllEquipment().FirstOrDefault(r => r.Id == entry.Id) is { } relic)
            return (char.ToUpperInvariant(relic.Rarity[0]) + relic.Rarity[1..], "codex-ribbon-shield", new[] { ("HEALTH", $"×{relic.HealthScale:0.##}", "codexstat-health"), ("DAMAGE", $"×{relic.DamageScale:0.##}", "codexstat-damage") });
        return ("Lore", "codex-ribbon-shield", Array.Empty<(string, string, string)>());
    }

    private void BuildDetail(RoyalSpec spec, CodexEntry entry)
    {
        var ink = new Color("140b05");
        var name = spec.Label("detail.name", entry.Title, 480, ink);
        name.ShadowOffset = Vector2.Zero; _layer.AddChild(name);
        var (role, roleIcon, stats) = Facts(entry);
        var ribbon = spec.Rect("detail.role");
        var roleLabel = spec.Label("detail.role.label", role, 200, ink);
        roleLabel.ShadowOffset = Vector2.Zero;
        var ribbonWidth = Mathf.Max(ribbon.Size.X, roleLabel.Position.X - ribbon.Position.X + roleLabel.TextWidth(roleLabel.FontSize) + 30);
        _layer.AddChild(new Panel { Position = ribbon.Position, Size = new Vector2(ribbonWidth, ribbon.Size.Y), MouseFilter = MouseFilterEnum.Ignore }
            .With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("codex-ribbon", 50, 8, 26, 8))));
        _layer.AddChild(RoyalKit.Image(roleIcon, spec.Rect("detail.role.icon")));
        _layer.AddChild(roleLabel);
        var sentences = entry.LoreText.Split(". ", 2);
        var blurb = spec.Label("detail.blurb", sentences[0].TrimEnd('.') + ".", 470, new Color("17110a"));
        blurb.ShadowOffset = Vector2.Zero; _layer.AddChild(blurb);

        var portrait = spec.Rect("detail.portrait");
        _vignette ??= ResourceLoader.Load<Shader>("res://assets/shaders/royal_vignette.gdshader");
        var room = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = RoyalArt.Cut("warband", new Rect2(430, 300, 260, 330)), Position = portrait.Position, Size = portrait.Size, MouseFilter = MouseFilterEnum.Ignore };
        var material = new ShaderMaterial { Shader = _vignette };
        material.SetShaderParameter("size", portrait.Size);
        room.Material = material;
        _layer.AddChild(room);
        var picture = Portrait(entry);
        var figure = GameData.TryGetUnit(entry.Id) != null;
        _layer.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = picture, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Position = portrait.Position + (figure ? new Vector2(24, 16) : new Vector2(40, 50)), Size = portrait.Size - (figure ? new Vector2(48, 58) : new Vector2(80, 120)) });

        for (var i = 0; i < stats.Length && i < 2; i++)
        {
            var key = $"stat.{i + 1}";
            var tile = spec.Rect(key);
            _layer.AddChild(new Panel { Position = tile.Position, Size = tile.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("codex-stat", 12))));
            _layer.AddChild(RoyalKit.Image(stats[i].Icon, spec.Rect(key + ".icon")));
            _layer.AddChild(spec.Label(key + ".caption", stats[i].Caption, 120));
            _layer.AddChild(spec.Label(key + ".value", stats[i].Value, 120));
        }
        _layer.AddChild(RoyalKit.Image("codex-divider", spec.Rect("detail.divider")));
        var loreRect = spec.Rect("detail.lore");
        var rest = sentences.Length > 1 ? sentences[1] : entry.LoreText;
        var defeats = GameState.Instance.GetCodexKillCount(entry.Id);
        if (defeats > 0) rest += $" Defeated {defeats:N0} times.";
        var lore = RoyalText.Paragraph(rest, 14, new Color("30211a"), 500);
        lore.RemoveThemeColorOverride("font_shadow_color"); lore.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        lore.Position = new Vector2(loreRect.Position.X, loreRect.Position.Y - 2);
        lore.Size = new Vector2(loreRect.Size.X + 6, spec.Rect("button.back").Position.Y - loreRect.Position.Y - 8);
        lore.AddThemeConstantOverride("line_spacing", -1);
        RoyalText.FitLines(lore, 5, 12);
        _layer.AddChild(lore);
    }

    private void BuildEmptyPage(RoyalSpec spec)
    {
        var heading = RoyalText.Serif("A world waiting to be discovered", 30, new Color("140b05"), 800);
        heading.ShadowOffset = Vector2.Zero;
        RoyalText.Place(_layer, heading, 713, 180, 470, 50);
        var hint = RoyalText.Paragraph("Meet enemies and collect allies, spells and relics to fill these pages.", 18, new Color("30211a"), 500);
        hint.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        RoyalText.Place(_layer, hint, 716, 240, 450, 80);
    }
}
