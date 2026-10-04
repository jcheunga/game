using System.Linq;
using Godot;

public partial class ShopMenu
{
    private GridContainer _modalRoster, _equippedRoster;
    private Label _equippedLabel;
    private Control _modalShowcase;
    private PanelContainer _displayFrame;
    private VBoxContainer _modalActions;
    private void BuildModalUi()
    {
        var root = new VBoxContainer();
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        var resources = new PanelContainer
        {
            Name = "ArmoryResources"
        };
        resources.SetMeta("modal_unframed", true);
        resources.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Inset, 6));
        root.AddChild(resources);
        _resourcesRow = new HFlowContainer
        {
            Name = "ResourceBalances",
            Alignment = FlowContainer.AlignmentMode.Center
        };
        _resourcesRow.AddThemeConstantOverride("h_separation", 32);
        _resourcesRow.AddThemeConstantOverride("v_separation", 6);
        resources.AddChild(_resourcesRow);
        _armoryTabs = RealmUi.Tabs(root, SelectArmoryTab, "Warband", "Spells", "War wagon", "Relics", "Adviser");
        _unitsPanel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _unitsPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        root.AddChild(_unitsPanel);
        var layout = new HBoxContainer();
        layout.AddThemeConstantOverride("separation", 16);
        _unitsPanel.AddChild(layout);
        var equipped = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(180, 0)
        };
        equipped.AddThemeConstantOverride("separation", 8);
        layout.AddChild(equipped);
        _equippedLabel = RealmUi.Label("EQUIPPED", 18, true);
        equipped.AddChild(_equippedLabel);
        _equippedLabel.AddThemeFontSizeOverride("font_size", 18);
        _equippedRoster = new GridContainer
        {
            Columns = 3
        };
        _equippedRoster.AddThemeConstantOverride("h_separation", 6);
        _equippedRoster.AddThemeConstantOverride("v_separation", 6);
        equipped.AddChild(_equippedRoster);
        var collectionLabel = RealmUi.Label("Collection", 18, true);
        collectionLabel.AddThemeFontSizeOverride("font_size", 18);
        equipped.AddChild(collectionLabel);
        var rosterScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        equipped.AddChild(rosterScroll);
        _modalRoster = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _modalRoster.AddThemeConstantOverride("h_separation", 8);
        _modalRoster.AddThemeConstantOverride("v_separation", 8);
        rosterScroll.AddChild(_modalRoster);
        _displayFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MobilePresentation.Enabled ? 210 : 310, 0)
        };
        _displayFrame.AddThemeStyleboxOverride("panel", new ModalSurface(ModalMaterial.Steel, 8));
        layout.AddChild(_displayFrame);
        _modalShowcase = new Control
        {
            ClipContents = true
        };
        _displayFrame.AddChild(_modalShowcase);
        var details = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        details.AddThemeConstantOverride("separation", 10);
        layout.AddChild(details);
        _unitDetail = RealmUi.Scroll(details);
        _unitDetail.AddThemeConstantOverride("separation", 10);
        _modalActions = new VBoxContainer
        {
            Name = "ProfileActions"
        };
        details.AddChild(_modalActions);
        _baseStack = ModalPage(root, out _basePanel);
        _relicsStack = ModalPage(root, out _relicsPanel);
        _recommendationStack = ModalPage(root, out _summaryPanel);
        _statusLabel = RealmUi.Label("", 18, true);
        _statusLabel.CustomMinimumSize = new Vector2(0, 26);
        root.AddChild(_statusLabel);
        RealmModal.Polish(_armoryTabs);
    }

    private static VBoxContainer ModalPage(Control root, out PanelContainer panel)
    {
        panel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Visible = false
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        root.AddChild(panel);
        var stack = RealmUi.Scroll(panel);
        stack.AddThemeConstantOverride("separation", 12);
        return stack;
    }

    private void RefreshModalUi()
    {
        RebuildResourcesRow();
        RebuildModalRoster();
        RebuildBaseUpgradePanels();
        RebuildRelicPanels();
        RebuildAdviser();
        RealmModal.Polish(_baseStack);
        RealmModal.Polish(_relicsStack);
        RealmModal.Polish(_recommendationStack);
    }

    private void RebuildAdviser()
    {
        RealmUi.Clear(_recommendationStack);
        var state = GameState.Instance;
        var report = state.GetCampaignReadinessReport(state.SelectedStage);
        _recommendationStack.AddChild(ModalUi.Banner(4, report == null ? "Readiness" : $"Readiness · {report.Rating} {report.Score}/100",
            report?.Summary ?? "Equip allies and spells to assess your caravan."));
        var facts = new GridContainer { Columns = MobilePresentation.Enabled ? 1 : 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        facts.AddThemeConstantOverride("h_separation", 32); facts.AddThemeConstantOverride("v_separation", 6);
        void Fact(string name, string value) => facts.AddChild(RealmUi.KeyValue(name, value));
        int Level(string id) => state.GetBaseUpgradeLevel(id);
        Fact("Allies owned", $"{state.GetOwnedPlayerUnits().Count}/{GameData.PlayerRosterIds.Length}");
        Fact("Spells owned", $"{state.GetOwnedPlayerSpells().Count}/{GameData.PlayerSpellIds.Length}");
        Fact("Heroic directives", $"{state.ClaimedCampaignDirectiveCount}/{state.MaxStage}");
        Fact("Wagon plating", $"{Level(BaseUpgradeCatalog.HullPlatingId)}/{state.MaxBaseUpgradeLevel}");
        Fact("Stores", $"{Level(BaseUpgradeCatalog.PantryId)}/{state.MaxBaseUpgradeLevel}");
        Fact("March drum", $"{Level(BaseUpgradeCatalog.DispatchConsoleId)}/{state.MaxBaseUpgradeLevel}");
        Fact("Rune beacon", $"{Level(BaseUpgradeCatalog.SignalRelayId)}/{state.MaxBaseUpgradeLevel}");
        _recommendationStack.AddChild(facts);
        if (report != null && report.Gaps.Count > 0)
        {
            _recommendationStack.AddChild(RealmUi.SectionTitle("Priorities"));
            foreach (var gap in report.Gaps.Take(3)) _recommendationStack.AddChild(RealmUi.Label("•  " + gap, 18));
        }
        var advice = RealmUi.Button("book", "Route & squad advice", () => RealmUi.Details(this, "Prepare your warband", BuildRouteIntelText()));
        advice.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _recommendationStack.AddChild(advice);
    }

    private void RebuildModalRoster()
    {
        RealmUi.Clear(_modalRoster);
        RealmUi.Clear(_equippedRoster);
        RealmUi.Clear(_unitDetail);
        RealmUi.Clear(_modalShowcase);
        RealmUi.Clear(_modalActions);
        var entries = (_showSpells ? GameData.GetPlayerSpells().Select(x => (x.Id, x.DisplayName)) : GameData.GetPlayerUnits().Select(x => (x.Id, x.DisplayName))).ToArray();
        if (!entries.Any(entry => entry.Id == _selectedRosterId))
            _selectedRosterId = entries.FirstOrDefault().Id ?? "";
        foreach (var(id, title)in entries)
        {
            bool owned = _showSpells ? GameState.Instance.IsSpellOwned(id) : GameState.Instance.IsUnitOwned(id);
            bool available = _showSpells ? GameState.Instance.IsSpellAvailableForPurchase(id) : GameState.Instance.IsUnitAvailableForPurchase(id);
            bool equipped = _showSpells ? GameState.Instance.IsSpellInActiveDeck(id) : GameState.Instance.IsUnitInActiveDeck(id);
            Texture2D texture = _showSpells ? UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(id)) : UiArtLoader.TryLoadUnitIcon(GameData.GetUnit(id));
            Button Portrait(bool compact)
            {
                var button = new Button
                {
                    CustomMinimumSize = new Vector2(compact ? 50 : 76, compact ? 50 : 80),
                    TooltipText = title + (owned ? equipped ? " · Equipped" : " · Reserve" : " · Not owned"),
                    AccessibilityName = title
                };
                var colour = _showSpells ? new Color("9863d5") : new Color("5f8cbf");
                ModalUi.StyleButton(button, id == _selectedRosterId, colour, id == _selectedRosterId ? ModalMaterial.Gold : ModalMaterial.Portrait);
                var image = new TextureRect
                {
                    Texture = texture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                    Modulate = owned ? Colors.White : new Color(.45f, .5f, .52f)
                };
                button.AddChild(image);
                image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                image.OffsetLeft = image.OffsetTop = 6;
                image.OffsetRight = image.OffsetBottom = -6;
                if (!compact)
                {
                    var badge = new Label
                    {
                        Text = !owned ? available ? "+" : "🔒" : equipped ? "✓" : $"{(_showSpells ? GameState.Instance.GetSpellLevel(id) : GameState.Instance.GetUnitLevel(id))}",
                        MouseFilter = MouseFilterEnum.Ignore,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Position = new Vector2(51, 54),
                        Size = new Vector2(20, 24)
                    };
                    badge.AddThemeFontSizeOverride("font_size", 18);
                    badge.AddThemeColorOverride("font_color", RealmUi.Gold);
                    button.AddChild(badge);
                }

                button.Pressed += () =>
                {
                    _selectedRosterId = id;
                    _profileExpanded = false;
                    RebuildModalRoster();
                };
                return button;
            }

            _modalRoster.AddChild(Portrait(false));
            if (equipped)
                _equippedRoster.AddChild(Portrait(true));
        }

        if (string.IsNullOrEmpty(_selectedRosterId))
            return;
        var backdrop = new ModalShowcaseBackdrop
        {
            Magic = _showSpells
        };
        _modalShowcase.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _displayFrame.AddThemeStyleboxOverride("panel", new ModalSurface(_showSpells ? ModalMaterial.Arcane : ModalMaterial.Steel, 8));
        Control panel = _showSpells ? BuildSpellPanel(GameData.GetSpell(_selectedRosterId)) : BuildUnitPanel(GameData.GetUnit(_selectedRosterId));
        if (_showSpells)
        {
            var image = new TextureRect
            {
                Texture = UiArtLoader.TryLoadSpellIcon(GameData.GetSpell(_selectedRosterId)),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _modalShowcase.AddChild(image);
            image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            image.OffsetLeft = image.OffsetTop = 28;
            image.OffsetRight = image.OffsetBottom = -28;
        }
        else
        {
            var preview = new UnitModelPreview
            {
                InspectRequested = () => ModelShowcase.Show(this, GameData.GetPlayerUnits().ToArray(), _selectedRosterId)
            };
            _modalShowcase.AddChild(preview);
            preview.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            preview.SetUnit(GameData.GetUnit(_selectedRosterId));
        }

        var actions = panel.FindChild("RosterActions", true, false) as HBoxContainer;
        if (actions != null)
        {
            actions.GetParent().RemoveChild(actions);
            _modalActions.AddChild(actions);
            RealmModal.Polish(actions);
        }

        _unitDetail.AddChild(panel);
        RealmModal.Polish(panel);
        _equippedLabel.Text = _showSpells ? $"Squad {GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit}" : $"Squad {GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit}";
        RealmModal.UpdateHeading(this, subtitle: $"{GameState.Instance.ActiveDeckUnitIds.Count}/{GameState.Instance.DeckSizeLimit} allies · {GameState.Instance.ActiveDeckSpellIds.Count}/{GameState.Instance.SpellDeckSizeLimit} spells equipped");
    }
}

public partial class ShopMenu
{
    private Control BuildModalUpgrade(BaseUpgradeDefinition upgrade)
    {
        var state = GameState.Instance;
        var level = state.GetBaseUpgradeLevel(upgrade.Id);
        bool max = level >= upgrade.MaxLevel;
        var cost = state.GetBaseUpgradeCost(upgrade.Id);
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        panel.SetMeta("modal_material", (int)ModalMaterial.Forge);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        panel.AddChild(stack);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 12);
        stack.AddChild(heading);
        heading.AddChild(new TextureRect { Texture = HomeMapArt.Icon("hammer"), CustomMinimumSize = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var title = RealmUi.Heading(upgrade.Title, 20);
        title.VerticalAlignment = VerticalAlignment.Center;
        heading.AddChild(title);
        stack.AddChild(RealmUi.Label($"Level {level}/{upgrade.MaxLevel}", 18, true));
        stack.AddChild(new ProgressBar { Value = level, MaxValue = upgrade.MaxLevel, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) });
        stack.AddChild(RealmUi.Label(level == 0 ? "Not installed" : BuildBaseUpgradeEffectText(upgrade, level), 18, true));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        stack.AddChild(row);
        row.AddChild(RealmUi.Button("book", "Details", () => RealmUi.Details(this, upgrade.Title, upgrade.Summary + "\n\nCurrent: " + BuildBaseUpgradeEffectText(upgrade, level) + (max ? "\n\nFully trained" : "\n\nNext: " + BuildBaseUpgradeEffectText(upgrade, level + 1)))));
        var action = new RealmButton { CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand };
        if (max) action.Text = "Fully trained";
        else ArmoryDetailUi.GoldAction(action, "Upgrade", cost);
        action.Pressed += () =>
        {
            if (state.TryUpgradeBase(upgrade.Id, out var message))
                AudioDirector.Instance?.PlayUpgradeConfirm();
            _statusLabel.Text = message;
            RefreshUi();
        };
        action.Disabled = max || state.Gold < cost;
        row.AddChild(action);
        return panel;
    }
}
