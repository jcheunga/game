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
        // The page count ("12 / 29", read with the arrows beside it) ends just short of the prev arrow, the
        // discovered count takes the room before it, and the ornamental rule fills any gap between them.
        var pageLabel = spec.Label("list.page", $"{_page + 1} / {pages}", 100, new Color("1b0f07"));
        pageLabel.ShadowOffset = Vector2.Zero; pageLabel.ShrinkToFit = false;
        var pageWidth = pageLabel.TextWidth(pageLabel.FontSize);
        var pageLeft = Mathf.Min(pageLabel.Position.X, spec.Rect("page.prev").Position.X - 10 - pageWidth);
        pageLabel.Position = new Vector2(pageLeft, pageLabel.Position.Y); pageLabel.Size = new Vector2(pageWidth, pageLabel.Size.Y);
        _layer.AddChild(pageLabel);
        // The plate paints the concept's rule where the longer phone-size count now runs, so clean parchment from
        // just left of it covers it; the live rule below fills whatever gap the count leaves.
        var painted = spec.Rect("list.heading.rule").Grow(2);
        _layer.AddChild(new TextureRect { Texture = RoyalArt.Cut("codex", new Rect2(painted.Position - new Vector2(painted.Size.X, 0), painted.Size)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = painted.Position, Size = painted.Size, MouseFilter = MouseFilterEnum.Ignore });
        var headingLeft = spec.Number("list.heading", "pen_x", spec.Number("list.heading", "x"));
        var heading = spec.Label("list.heading", $"DISCOVERED {entries.Count(entry => state.IsCodexEntryDiscovered(entry.Id))} / {entries.Length}", pageLeft - 12 - headingLeft, ink);
        heading.ShadowOffset = Vector2.Zero; _layer.AddChild(heading);
        var rule = spec.Rect("list.heading.rule");
        var ruleLeft = Mathf.Max(rule.Position.X, heading.Position.X + heading.TextWidth(heading.FittedSize()) + 12);
        var ruleRight = pageLeft - 12;
        if (ruleRight - ruleLeft > 30) _layer.AddChild(RoyalKit.Image("codex-rule", new Rect2(ruleLeft, rule.Position.Y, ruleRight - ruleLeft, rule.Size.Y)));
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

    /// <summary>The entry's role and up to four stats: a unit's essentials, a spell's sheet, a relic's bonuses.</summary>
    private (string Role, string Icon, ArmoryDetailUi.Stat[] Stats) Facts(CodexEntry entry)
    {
        if (GameData.TryGetUnit(entry.Id) is { } unit)
        {
            var stats = ArmoryDetailUi.UnitStats(unit);
            var role = entry.Category == "boss" ? "Boss" : unit.IsPlayerSide ? SquadSynergyCatalog.GetTagDisplayName(unit.SquadTag) : "Rotbound";
            // Health, damage, range and armor.
            return (role, "codex-ribbon-shield", new[] { stats[0], stats[1], stats[4], stats[6] });
        }
        if (GameData.TryGetSpell(entry.Id) is { } spell)
            return (ArmoryDetailUi.SpellRole(spell.EffectType), "codex-ribbon-shield", ArmoryDetailUi.SpellStats(GameState.Instance.BuildSpellStats(spell)).Take(4).ToArray());
        if (GameData.GetAllEquipment().FirstOrDefault(r => r.Id == entry.Id) is { } relic)
            return (char.ToUpperInvariant(relic.Rarity[0]) + relic.Rarity[1..], "codex-ribbon-shield", RelicStatText.Stats(relic).Take(4).ToArray());
        return ("Lore", "codex-ribbon-shield", Array.Empty<ArmoryDetailUi.Stat>());
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
        // The first sentence heads the page in up to two lines at the reading floor (a longer one keeps its first
        // clause there and the rest joins the lore); whatever it takes pushes the portrait and stats down.
        var sentences = entry.LoreText.Split(". ", 2);
        var opening = sentences[0].TrimEnd('.') + ".";
        var rest = sentences.Length > 1 ? sentences[1] : "";
        var blurb = RoyalText.Paragraph("", ReadingText.PhoneBody, new Color("17110a"), 600);
        blurb.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        blurb.AddThemeConstantOverride("line_spacing", -3);
        var blurbTop = spec.Number("detail.blurb", "baseline", 286) - 20;
        var blurbWidth = spec.Rect("stat.1").End.X - spec.Number("detail.blurb", "x", 716);
        var font = blurb.GetThemeFont("font");
        int Lines(string text) => Mathf.RoundToInt(font.GetMultilineStringSize(text, HorizontalAlignment.Left, blurbWidth, ReadingText.PhoneBody).Y / font.GetHeight(ReadingText.PhoneBody));
        var cut = opening.IndexOfAny(new[] { ';', ',' });
        if (Lines(opening) > 2 && cut > 0)
        {
            var tail = opening[(cut + 1)..].Trim().TrimEnd('.');
            if (tail.Length > 0) rest = (char.ToUpperInvariant(tail[0]) + tail[1..] + ". " + rest).Trim();
            opening = opening[..cut] + ".";
        }
        blurb.Text = opening;
        var blurbLines = Mathf.Clamp(Lines(opening), 1, 2);
        RoyalText.Place(_layer, blurb, new Rect2(spec.Number("detail.blurb", "x", 716), blurbTop, blurbWidth, blurbLines * 25 + 4));
        RoyalText.FitLines(blurb, 2);
        var drop = (blurbLines - 1) * 24f;

        var portrait = spec.Rect("detail.portrait");
        portrait = new Rect2(portrait.Position + new Vector2(0, drop), portrait.Size - new Vector2(0, drop));
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

        // One column of compact rows where the concept had two tall tiles; the divider and lore follow its height.
        var top = spec.Rect("stat.1");
        top.Position += new Vector2(0, drop);
        var shift = drop;
        if (stats.Length > 0)
        {
            var sheet = new StatSheet(stats, 1).Place(top.Position, spec.Rect("detail.lore").End.X + 3 - top.Position.X);
            _layer.AddChild(sheet);
            shift = sheet.Position.Y + sheet.Size.Y - spec.Rect("stat.2").End.Y;
        }
        var divider = spec.Rect("detail.divider");
        _layer.AddChild(RoyalKit.Image("codex-divider", new Rect2(divider.Position + new Vector2(0, shift), divider.Size)));
        var loreRect = spec.Rect("detail.lore");
        loreRect.Position += new Vector2(0, shift);
        var defeats = GameState.Instance.GetCodexKillCount(entry.Id);
        if (defeats > 0) rest = (rest + $" Defeated {defeats:N0} times.").Trim();
        // The lore reads at the phone floor and scrolls in its column when it runs longer than the page.
        var loreArea = new Rect2(loreRect.Position.X, loreRect.Position.Y - 2, loreRect.Size.X + 10, spec.Rect("button.back").End.Y - loreRect.Position.Y - 6);
        var scroll = new ScrollContainer { Name = "CodexLore", Position = loreArea.Position, Size = loreArea.Size,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        _layer.AddChild(scroll);
        var lore = RoyalText.Paragraph(rest, ReadingText.PhoneBody, new Color("30211a"), 500);
        lore.RemoveThemeColorOverride("font_shadow_color"); lore.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        lore.AddThemeConstantOverride("line_spacing", -3);
        lore.CustomMinimumSize = new Vector2(loreArea.Size.X - 4, 0);
        scroll.AddChild(lore);
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
