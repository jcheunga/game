using System.Linq;
using Godot;

public partial class ShopMenu
{
    private bool _embedded;
    private GridContainer _modalRoster, _equippedRoster;
    private Label _rosterSummary, _equippedLabel;
    private Control _modalShowcase;
    private PanelContainer _displayFrame;

    private void BuildModalUi()
    {
        var root = new VBoxContainer(); AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 12);
        var head = new HBoxContainer(); root.AddChild(head); head.Hide();
        _rosterSummary = RealmUi.Label("", 18, true); head.AddChild(_rosterSummary);
        _resourcesRow = new HBoxContainer(); head.AddChild(_resourcesRow);
        _armoryTabs = RealmUi.Tabs(root, SelectArmoryTab, "Warband", "Battle rites", "War wagon", "Relics", "Adviser");
        _unitsPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _unitsPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); root.AddChild(_unitsPanel);
        var layout = new VBoxContainer(); layout.AddThemeConstantOverride("separation", 12); _unitsPanel.AddChild(layout);
        var rosterRow = new HBoxContainer(); rosterRow.AddThemeConstantOverride("separation", 14); layout.AddChild(rosterRow);
        var equipped = new VBoxContainer { CustomMinimumSize = new Vector2(212, 0) }; rosterRow.AddChild(equipped);
        _equippedLabel = RealmUi.Label("EQUIPPED", 18, true); equipped.AddChild(_equippedLabel);
        _equippedRoster = new GridContainer { Columns = 3 }; equipped.AddChild(_equippedRoster);
        var collection = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; rosterRow.AddChild(collection);
        collection.AddChild(RealmUi.Label("YOUR COLLECTION", 18, true));
        var rosterScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 98), HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        collection.AddChild(rosterScroll);
        _modalRoster = new GridContainer { Columns = 20, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _modalRoster.AddThemeConstantOverride("h_separation", 8); _modalRoster.AddThemeConstantOverride("v_separation", 8); rosterScroll.AddChild(_modalRoster);
        var detailRow = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; detailRow.AddThemeConstantOverride("separation", 20); layout.AddChild(detailRow);
        _displayFrame = new PanelContainer { CustomMinimumSize = new Vector2(382, 0) }; _displayFrame.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Steel, 8)); detailRow.AddChild(_displayFrame);
        _modalShowcase = new Control { ClipContents = true }; _displayFrame.AddChild(_modalShowcase);
        _unitDetail = RealmUi.Scroll(detailRow); _unitDetail.AddThemeConstantOverride("separation", 10);
        _baseStack = ModalPage(root, out _basePanel);
        _relicsStack = ModalPage(root, out _relicsPanel);
        _recommendationStack = ModalPage(root, out _summaryPanel);
        _statusLabel = RealmUi.Label("Select a portrait to inspect, equip or train.", 18, true); root.AddChild(_statusLabel);
        RealmModal.Polish(_armoryTabs);
    }

    private static VBoxContainer ModalPage(Control root, out PanelContainer panel)
    {
        panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); root.AddChild(panel);
        var stack = RealmUi.Scroll(panel); stack.AddThemeConstantOverride("separation", 12); return stack;
    }

    private void RefreshModalUi()
    {
        RebuildResourcesRow();
        _rosterSummary.Text = _showSpells
            ? $"{GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit} spells equipped"
            : $"{GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit} allies equipped";
        RebuildModalRoster(); RebuildBaseUpgradePanels(); RebuildRelicPanels();
        RealmUi.Clear(_recommendationStack);
        _recommendationStack.AddChild(ModalUi.Banner(4, "Caravan readiness", "A well-prepared caravan carries the day."));
        _recommendationStack.AddChild(RealmUi.Label(BuildSummaryText()));
        _recommendationStack.AddChild(RealmUi.Button("book", "Route & squad advice", () => RealmUi.Details(this, "Prepare your warband", BuildRouteIntelText())));
        RealmModal.Polish(_baseStack); RealmModal.Polish(_relicsStack); RealmModal.Polish(_recommendationStack);
    }

    private void RebuildModalRoster()
    {
        RealmUi.Clear(_modalRoster); RealmUi.Clear(_equippedRoster); RealmUi.Clear(_unitDetail); RealmUi.Clear(_modalShowcase);
        var entries = (_showSpells ? GameData.GetPlayerSpells().Select(x => (x.Id, x.DisplayName)) : GameData.GetPlayerUnits().Select(x => (x.Id, x.DisplayName))).ToArray();
        if (!entries.Any(entry => entry.Id == _selectedRosterId)) _selectedRosterId = entries.FirstOrDefault().Id ?? "";
        foreach (var (id, title) in entries)
        {
            bool owned = _showSpells ? GameState.Instance.IsSpellOwned(id) : GameState.Instance.IsUnitOwned(id);
            bool available = _showSpells ? GameState.Instance.IsSpellAvailableForPurchase(id) : GameState.Instance.IsUnitAvailableForPurchase(id);
            bool equipped = _showSpells ? GameState.Instance.IsSpellInActiveDeck(id) : GameState.Instance.IsUnitInActiveDeck(id);
            Texture2D texture = _showSpells ? UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(id)) : UiArtLoader.TryLoadUnitIcon(GameData.GetUnit(id));
            Button Portrait(bool compact)
            {
                var button = new Button { CustomMinimumSize = new Vector2(compact ? 60 : 82, compact ? 60 : 88), TooltipText = title + (owned ? equipped ? " · Equipped" : " · Reserve" : " · Not owned"), AccessibilityName = title };
                var colour = _showSpells ? new Color("9863d5") : new Color("5f8cbf");
                ModalUi.StyleButton(button, id == _selectedRosterId, colour, id == _selectedRosterId ? ModalMaterial.Gold : ModalMaterial.Portrait);
                var image = new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, Modulate = owned ? Colors.White : new Color(.45f,.5f,.52f) };
                button.AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); image.OffsetLeft = image.OffsetTop = 6; image.OffsetRight = image.OffsetBottom = -6;
                if (!compact) {
                    var badge = new Label { Text = !owned ? available ? "+" : "🔒" : equipped ? "✓" : $"{(_showSpells ? GameState.Instance.GetSpellLevel(id) : GameState.Instance.GetUnitLevel(id))}", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector2(60, 64), Size = new Vector2(22, 24) };
                    badge.AddThemeFontSizeOverride("font_size", 18); badge.AddThemeColorOverride("font_color", RealmUi.Gold); button.AddChild(badge);
                }
                button.Pressed += () => { _selectedRosterId = id; RebuildModalRoster(); }; return button;
            }
            _modalRoster.AddChild(Portrait(false)); if (equipped) _equippedRoster.AddChild(Portrait(true));
        }
        if (string.IsNullOrEmpty(_selectedRosterId)) return;
        var backdrop = new ModalShowcaseBackdrop { Magic = _showSpells }; _modalShowcase.AddChild(backdrop); backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _displayFrame.AddThemeStyleboxOverride("panel", new ModalSurface(_showSpells ? ModalMaterial.Arcane : ModalMaterial.Steel, 8));
        Control panel = _showSpells ? BuildSpellPanel(GameData.GetSpell(_selectedRosterId)) : BuildUnitPanel(GameData.GetUnit(_selectedRosterId));
        // The illustration gets its own generous display; the original action
        // builder still owns all progression, promotion and doctrine behavior.
        var leadingRow = panel.GetChild<MarginContainer>(0).GetChild<HBoxContainer>(0);
        var badge = leadingRow.GetChild<Control>(0); leadingRow.RemoveChild(badge); badge.QueueFree();
        if (_showSpells)
        {
            var image = new TextureRect { Texture = UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(_selectedRosterId)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            _modalShowcase.AddChild(image); image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); image.OffsetLeft = image.OffsetTop = 28; image.OffsetRight = image.OffsetBottom = -28;
            leadingRow.GetChild<VBoxContainer>(0).AddChild(RealmUi.Label(GameData.GetSpell(_selectedRosterId).Description, 20, true));
        }
        else
        {
            var preview = new UnitModelPreview(); _modalShowcase.AddChild(preview); preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); preview.SetUnit(GameData.GetUnit(_selectedRosterId));
        }
        var details = leadingRow.GetChild<VBoxContainer>(0);
        // Keep progression actions visible before the optional stat breakdown.
        var actionRow = details.GetChildren().OfType<HBoxContainer>().LastOrDefault(row => row.GetChildren().OfType<Button>().Any());
        if (actionRow != null) foreach (var action in actionRow.GetChildren().OfType<Button>())
            if (action.Text.StartsWith("Upgrade") || action.Text.StartsWith("Buy ") || action.Text.StartsWith("Scribe ")) action.SetMeta("realm_primary", true);
        if (actionRow != null) details.MoveChild(actionRow, _showSpells ? 2 : 3);
        if (!_showSpells) details.GetChild<Control>(1).Hide();
        foreach (var side in new[] { "left", "right", "top", "bottom" }) panel.GetChild<MarginContainer>(0).AddThemeConstantOverride("margin_" + side, 0);
        _unitDetail.AddChild(panel); RealmModal.Polish(panel);
        details.GetChild<Label>(0).AddThemeColorOverride("font_color", new Color("ffd47d"));
        _equippedLabel.Text = _showSpells ? $"EQUIPPED {GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit}" : $"EQUIPPED {GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit}";
        RealmModal.UpdateHeading(this, subtitle: $"{GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit} ALLIES · {GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit} SPELLS EQUIPPED");
        _rosterSummary.Text = _showSpells ? $"{GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit} spells equipped" : $"{GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit} allies equipped";
    }
}

public partial class ShopMenu
{
    private Control BuildModalUpgrade(BaseUpgradeDefinition upgrade)
    {
        var state = GameState.Instance; var level = state.GetBaseUpgradeLevel(upgrade.Id);
        bool max = level >= upgrade.MaxLevel; var cost = state.GetBaseUpgradeCost(upgrade.Id);
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.SetMeta("modal_material", (int)ModalMaterial.Forge);
        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 10); panel.AddChild(stack);
        var heading = new HBoxContainer(); heading.AddThemeConstantOverride("separation", 12); stack.AddChild(heading);
        heading.AddChild(new TextureRect { Texture = HomeMapArt.Icon("hammer"), CustomMinimumSize = new Vector2(44,44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var title = RealmUi.Heading(upgrade.Title, 20); title.VerticalAlignment = VerticalAlignment.Center; heading.AddChild(title);
        stack.AddChild(RealmUi.Label($"LEVEL {level} / {upgrade.MaxLevel}", 18, true));
        stack.AddChild(new ProgressBar { Value = level, MaxValue = upgrade.MaxLevel, ShowPercentage = false, CustomMinimumSize = new Vector2(0,6) });
        stack.AddChild(RealmUi.Label(level == 0 ? "Ready to install" : BuildBaseUpgradeEffectText(upgrade, level), 20, true));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); stack.AddChild(row);
        row.AddChild(RealmUi.Button("book", "Details", () => RealmUi.Details(this, upgrade.Title, upgrade.Summary + "\n\nCurrent: " + BuildBaseUpgradeEffectText(upgrade, level) + (max ? "\n\nFully trained" : "\n\nNext: " + BuildBaseUpgradeEffectText(upgrade, level + 1)))));
        var action = RealmUi.Button("gold", max ? "Fully trained" : $"Upgrade · {cost} gold", () => { if (state.TryUpgradeBase(upgrade.Id, out var message)) AudioDirector.Instance?.PlayUpgradeConfirm(); _statusLabel.Text = message; RefreshUi(); }, true);
        action.Disabled = max || state.Gold < cost; row.AddChild(action); return panel;
    }
}
