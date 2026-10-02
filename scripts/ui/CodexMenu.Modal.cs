#nullable enable
using System;
using System.Linq;
using Godot;

public partial class CodexMenu
{
    private GridContainer _bookGrid = null!;
    private VBoxContainer _bookDetail = null!;
    private Label _bookCount = null!, _bookPage = null!;
    private Button _bookPrevious = null!, _bookNext = null!;
    private int _bookPageIndex;

    private static string CategoryKey(string category) => category switch { "Enemies" => "enemy", "Bosses" => "boss", "Units" => "unit", "Spells" => "spell", "Relics" => "relic", _ => category };

    private void BuildBookUi()
    {
        var root = new VBoxContainer(); AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddThemeConstantOverride("separation", 12);
        var tabs = RealmUi.Tabs(root, index => { _activeCategory = Categories[index]; _selectedEntryId = null; _bookPageIndex = 0; RefreshBook(); }, Categories); RealmModal.Polish(tabs);
        var book = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; book.AddThemeConstantOverride("separation", 0); root.AddChild(book);
        VBoxContainer Page()
        {
            var page = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            page.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Paper, 18));
            page.AddThemeColorOverride("font_color", new Color("483725")); book.AddChild(page);
            var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 10); page.AddChild(stack); return stack;
        }
        var left = Page();
        _bookCount = Ink("", 20); left.AddChild(_bookCount);
        var list = RealmUi.Scroll(left); _bookGrid = new GridContainer { Columns = 4 }; _bookGrid.AddThemeConstantOverride("h_separation", 10); _bookGrid.AddThemeConstantOverride("v_separation", 10); list.AddChild(_bookGrid);
        var pager = new HBoxContainer(); left.AddChild(pager);
        _bookPrevious = HomeMapUi.IconButton("back", "Previous codex page", () => { _bookPageIndex--; RefreshBook(); }); pager.AddChild(_bookPrevious);
        _bookPage = Ink("", 18); _bookPage.HorizontalAlignment = HorizontalAlignment.Center; _bookPage.VerticalAlignment = VerticalAlignment.Center; pager.AddChild(_bookPage);
        _bookNext = HomeMapUi.IconButton("arrow", "Next codex page", () => { _bookPageIndex++; RefreshBook(); }); pager.AddChild(_bookNext);
        ModalUi.StyleButton(_bookPrevious); ModalUi.StyleButton(_bookNext);
        book.AddChild(new CodexBookSpine());
        var right = Page(); _bookDetail = RealmUi.Scroll(right); _bookDetail.AddThemeConstantOverride("separation", 12);
    }

    private static Label Ink(string text, int size = 20)
    {
        var label = RealmUi.Label(text, size); label.AddThemeColorOverride("font_color", new Color("483725")); label.AddThemeColorOverride("font_shadow_color", Colors.Transparent); label.AddThemeConstantOverride("shadow_offset_x", 0); label.AddThemeConstantOverride("shadow_offset_y", 0); return label;
    }

    private void RefreshBook()
    {
        var state = GameState.Instance;
        var entries = (_activeCategory == "All" ? CodexCatalog.GetAll() : CodexCatalog.GetByCategory(CategoryKey(_activeCategory)))
            .OrderByDescending(entry => state.IsCodexEntryDiscovered(entry.Id)).ThenBy(entry => entry.Title).ToArray();
        int pages = Math.Max(1, (entries.Length + 11) / 12); _bookPageIndex = Math.Clamp(_bookPageIndex, 0, pages - 1);
        var slice = entries.Skip(_bookPageIndex * 12).Take(12).ToArray();
        _selectedEntryId ??= slice.FirstOrDefault(entry => state.IsCodexEntryDiscovered(entry.Id))?.Id;
        _bookCount.Text = $"{_activeCategory.ToUpperInvariant()} · {entries.Count(entry => state.IsCodexEntryDiscovered(entry.Id))}/{entries.Length} discovered";
        _bookPage.Text = $"Page {_bookPageIndex + 1} of {pages}"; _bookPrevious.Disabled = _bookPageIndex == 0; _bookNext.Disabled = _bookPageIndex == pages - 1;
        RealmUi.Clear(_bookGrid);
        foreach (var entry in slice)
        {
            bool known = state.IsCodexEntryDiscovered(entry.Id);
            var button = new Button { CustomMinimumSize = new Vector2(96, 86), AccessibilityName = known ? entry.Title : "Undiscovered codex entry", TooltipText = known ? entry.Title : "Encounter this creature or acquire this item to discover it", Disabled = !known };
            ModalUi.StyleButton(button, entry.Id == _selectedEntryId, new Color("8c6b44"), entry.Id == _selectedEntryId ? ModalMaterial.Gold : ModalMaterial.Portrait);
            var badge = known ? UiBadgeFactory.CreateCodexBadge(entry, new Vector2(80, 72)) : UiBadgeFactory.CreateMysteryBadge(new Vector2(80, 72));
            RealmModal.Polish(badge);
            button.AddChild(badge); badge.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); badge.OffsetLeft = badge.OffsetTop = 6; badge.OffsetRight = badge.OffsetBottom = -6;
            button.Pressed += () => { _selectedEntryId = entry.Id; RefreshBook(); }; _bookGrid.AddChild(button);
        }
        RealmUi.Clear(_bookDetail);
        var selected = entries.FirstOrDefault(entry => entry.Id == _selectedEntryId && state.IsCodexEntryDiscovered(entry.Id));
        if (selected == null) { _bookDetail.AddChild(Ink("A world waiting to be discovered", 26)); _bookDetail.AddChild(Ink("Explore the kingdom, meet enemies and collect allies, spells and relics to fill these pages.")); return; }
        var title = Ink(selected.Title, 28); title.AddThemeFontOverride("font", ModalUi.HeadingFont); title.HorizontalAlignment = HorizontalAlignment.Center; _bookDetail.AddChild(title);
        var art = new CenterContainer(); var portrait = UiBadgeFactory.CreateCodexPortrait(selected, new Vector2(168, 168)); portrait.SetMeta("badge_tint", new Color("947543")); RealmModal.Polish(portrait); art.AddChild(portrait); _bookDetail.AddChild(art);
        _bookDetail.AddChild(Ink(selected.LoreText));
        if (!string.IsNullOrEmpty(selected.StatSummary)) _bookDetail.AddChild(Ink(selected.StatSummary, 18));
        var defeats = state.GetCodexKillCount(selected.Id);
        if (defeats > 0) _bookDetail.AddChild(Ink($"Defeated {defeats:N0} times", 18));
        var firstSeen = state.GetCodexFirstSeenAt(selected.Id);
        if (firstSeen > 0) _bookDetail.AddChild(Ink($"Discovered {DateTimeOffset.FromUnixTimeSeconds(firstSeen):dd MMM yyyy}", 18));
    }
}
