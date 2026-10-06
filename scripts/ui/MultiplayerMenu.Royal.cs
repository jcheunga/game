using System;
using System.Linq;
using Godot;

/// <summary>
/// Multiplayer challenges on the approved concept: the board tabs, the challenge briefing (route,
/// mutator, code and Load), room rows, and Back, Refresh, Match, Host and Start challenge. The live
/// services, selectors and code field from the full screen are kept and placed on the plate.
/// </summary>
public partial class MultiplayerMenu
{
    private static readonly string[] BoardKeys = { "rooms", "daily", "featured", "saved", "squad" };
    private static readonly string[] BoardNames = { "Rooms", "Daily", "Featured", "Saved", "Squad" };
    private Control _royalLayer;
    private RoyalButton _royalStart;
    private RoyalLabel _routeLabel, _routeStage, _mutatorLabel;
    private Control _skulls;
    private TextureRect _briefArt;
    private int _board;
    private string _shownStatus = "";

    private static RoyalSpec MpSpec => RoyalSpec.For("multiplayer");

    public MultiplayerMenu() { SetMeta("royal_screen", true); }

    private void BuildRoyalLayout()
    {
        var spec = MpSpec;
        foreach (var child in GetChildren().OfType<Control>()) child.Hide();
        var veil = new ColorRect { Color = new Color(.02f, .05f, .08f, .62f), MouseFilter = MouseFilterEnum.Stop };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var plate = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = RoyalArt.Plate("multiplayer"), Position = Vector2.Zero, Size = RoyalArt.Canvas, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(plate);
        _royalLayer = new Control { Position = Vector2.Zero, Size = RoyalArt.Canvas, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_royalLayer);
        _royalLayer.AddChild(spec.Label("title", "Multiplayer Challenges", 560));
        _royalLayer.AddChild(RoyalButton.Over(spec.Rect("close"), "Close panel", CloseRoyal, 6));
        BuildBoardTabs(spec);

        // Briefing: the stage and mutator selectors sit invisibly over their painted labels.
        _briefArt = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Position = spec.Rect("brief.art").Position, Size = spec.Rect("brief.art").Size, MouseFilter = MouseFilterEnum.Ignore };
        _royalLayer.AddChild(_briefArt);
        _royalLayer.AddChild(RoyalKit.Image("icon-map-small", spec.Rect("brief.map.icon")));
        _routeLabel = spec.Label("brief.route", "", 170);
        _routeLabel.Position -= new Vector2(0, 7);
        _royalLayer.AddChild(_routeLabel);
        _routeStage = RoyalText.Serif("", 15, new Color("d9cfb8"), 500);
        _routeStage.Position = new Vector2(spec.Number("brief.route", "pen_x", 408), 240); _routeStage.Size = new Vector2(160, 24);
        _royalLayer.AddChild(_routeStage);
        Overlay(_stageSelector, new Rect2(352, 200, 214, 62), "Challenge stage");
        _royalLayer.AddChild(RoyalKit.Image("icon-skull-red", spec.Rect("brief.modifier.icon")));
        _mutatorLabel = spec.Label("brief.modifier", "", 140);
        _royalLayer.AddChild(_mutatorLabel);
        _skulls = new Control { Position = spec.Rect("brief.difficulty").Position, Size = new Vector2(90, 20), MouseFilter = MouseFilterEnum.Ignore };
        _royalLayer.AddChild(_skulls);
        Overlay(_mutatorSelector, new Rect2(598, 200, 206, 62), "Challenge mutator");
        var codeIcon = RoyalButton.Over(spec.Rect("brief.code.icon").Grow(4), "Code options", ShowCodeMenu, 6);
        codeIcon.SetGlyph(RoyalKit.Texture("icon-scroll-gold"), new Rect2(4, 4, spec.Rect("brief.code.icon").Size.X, spec.Rect("brief.code.icon").Size.Y));
        _royalLayer.AddChild(codeIcon);
        var codeBox = spec.Rect("brief.code");
        _codeEdit.GetParent()?.RemoveChild(_codeEdit);
        _royalLayer.AddChild(_codeEdit);
        _codeEdit.Show();
        _codeEdit.Position = new Vector2(spec.Number("brief.code.label", "pen_x", 895) - 4, codeBox.Position.Y + 8);
        _codeEdit.Size = new Vector2(codeBox.End.X - _codeEdit.Position.X - 6, codeBox.Size.Y - 16);
        _codeEdit.CustomMinimumSize = Vector2.Zero;
        foreach (var state in new[] { "normal", "focus", "read_only" }) _codeEdit.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _codeEdit.AddThemeFontOverride("font", RoyalFonts.Body(500));
        _codeEdit.AddThemeFontSizeOverride("font_size", (int)spec.Number("brief.code.label", "size", 20));
        _codeEdit.AddThemeColorOverride("font_color", new Color("efe5d5"));
        _codeEdit.AddThemeColorOverride("font_placeholder_color", new Color(.94f, .9f, .83f, .4f));
        var load = RoyalButton.Over(spec.Rect("button.load"), "Load code", LoadCode, 6);
        var loadLabel = spec.Label("button.load.label", "LOAD", 120);
        loadLabel.Position -= spec.Rect("button.load").Position;
        load.SetCaption(loadLabel, new Rect2(loadLabel.Position, loadLabel.Size));
        _royalLayer.AddChild(load);

        // Board pages fill the rooms area.
        var board = new Rect2(48, 287, 1188, 323);
        foreach (var page in _boardPages)
        {
            var scroll = page.GetParent<ScrollContainer>();
            scroll.GetParent()?.RemoveChild(scroll);
            AddChild(scroll);
            scroll.Position = board.Position; scroll.Size = board.Size; scroll.CustomMinimumSize = Vector2.Zero;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            page.AddThemeConstantOverride("separation", 8);
        }
        SelectBoard(0);

        Footer(spec, "back", "icon-back-chevron", "BACK", CloseRoyal);
        Footer(spec, "refresh", "icon-refresh", "REFRESH", RefreshOnlineData);
        Footer(spec, "match", "icon-match", "MATCH", QuickMatchOnlineRoom);
        Footer(spec, "host", "icon-host", "HOST", HostOnlineRoom);
        _royalStart = Footer(spec, "start", "icon-start", "START CHALLENGE", StartChallenge, true);
        _startButton.SetMeta("realm_primary", true);
    }

    private void CloseRoyal()
    {
        for (var parent = GetParent(); parent != null; parent = parent.GetParent())
            if (parent is RealmModal modal) { modal.Closed?.Invoke(); return; }
        SceneRouter.Instance.GoToMap();
    }

    private void Overlay(OptionButton selector, Rect2 rect, string name)
    {
        selector.GetParent()?.RemoveChild(selector);
        _royalLayer.AddChild(selector);
        selector.Show();
        selector.Position = rect.Position; selector.Size = rect.Size; selector.CustomMinimumSize = Vector2.Zero;
        selector.TooltipText = name; selector.AccessibilityName = name;
        foreach (var state in new[] { "normal", "pressed", "disabled", "focus" }) selector.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        var hover = new StyleBoxFlat { BgColor = new Color(1f, .9f, .66f, .07f), BorderColor = new Color(1f, .86f, .55f, .45f), AntiAliasing = true };
        hover.SetBorderWidthAll(1); hover.SetCornerRadiusAll(6);
        selector.AddThemeStyleboxOverride("hover", hover);
        foreach (var key in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color" })
            selector.AddThemeColorOverride(key, Colors.Transparent);
        selector.AddThemeIconOverride("arrow", new ImageTexture());
    }

    private void BuildBoardTabs(RoyalSpec spec)
    {
        for (var i = 0; i < BoardKeys.Length; i++)
        {
            var index = i;
            var key = "tab." + BoardKeys[i];
            var rect = spec.Rect(key);
            var tab = RoyalButton.Over(rect, BoardNames[i], () => SelectBoard(index), 4);
            tab.Name = "BoardTab" + BoardNames[i];
            var icon = spec.Rect(key + ".icon");
            tab.SetGlyph(RoyalKit.Texture("mptab-" + BoardKeys[i]), new Rect2(icon.Position - rect.Position, icon.Size));
            var label = spec.Label(key + ".label", BoardNames[i], 150);
            label.Position -= rect.Position;
            tab.SetCaption(label, new Rect2(label.Position, label.Size));
            _royalLayer.AddChild(tab);
        }
    }

    private void SelectBoard(int index)
    {
        _board = index;
        for (var i = 0; i < _boardPages.Length; i++) _boardPages[i].GetParent<ScrollContainer>().Visible = i == index;
        var spec = MpSpec;
        for (var i = 0; i < BoardKeys.Length; i++)
            if (_royalLayer.GetNodeOrNull<RoyalButton>("BoardTab" + BoardNames[i]) is { } tab)
            {
                tab.SetStates(i == index ? RoyalKit.Slice("mp-tab-selected", 12) : null, 4);
                tab.MarkTab(i == index);
                if (tab.Caption != null) tab.Caption.Ink = i == index ? new Color("fff8b9") : new Color("dad8d4");
                tab.Caption?.QueueRedraw();
            }
    }

    private RoyalButton Footer(RoyalSpec spec, string key, string icon, string text, Action run, bool primary = false)
    {
        var rect = spec.Rect($"button.{key}");
        var button = RoyalButton.Over(rect, text, run, 6);
        button.SetGlyph(RoyalKit.Texture(icon), new Rect2(spec.Rect($"button.{key}.icon").Position - rect.Position, spec.Rect($"button.{key}.icon").Size));
        var label = spec.Label($"button.{key}.label", text, rect.End.X - spec.Number($"button.{key}.label", "pen_x", rect.Position.X) - 6);
        label.Position -= rect.Position;
        button.SetCaption(label, new Rect2(label.Position, label.Size));
        _royalLayer.AddChild(button);
        return button;
    }

    private void ShowCodeMenu()
    {
        var menu = new PopupMenu();
        menu.AddItem("Roll a new code", 0); menu.AddItem("Copy code", 1); menu.AddItem("Copy share link", 2);
        menu.AddSeparator(); menu.AddItem("Challenge briefing", 3); menu.AddItem("Records & replay", 4);
        menu.AddSeparator(); menu.AddItem("Sync results", 5); menu.AddItem("LAN race", 6);
        menu.SetItemDisabled(menu.GetItemIndex(5), _syncButton.Disabled);
        menu.IdPressed += id =>
        {
            switch (id)
            {
                case 0: GenerateChallengeCode(); break;
                case 1: DisplayServer.ClipboardSet(_codeEdit.Text); SetStatusMessage($"Copied {_codeEdit.Text} to the clipboard."); break;
                case 2:
                    if (string.IsNullOrWhiteSpace(_codeEdit.Text)) { SetStatusMessage("No challenge code to share."); break; }
                    var url = DeepLinkHandler.BuildShareUrl(_codeEdit.Text);
                    DisplayServer.ClipboardSet(url); SetStatusMessage($"Link copied: {url}"); break;
                case 3: RealmUi.Details(this, "Challenge briefing", _summaryLabel.Text + "\n\n" + _rulesLabel.Text); break;
                case 4: RealmUi.Details(this, "Records & replay", _recordLabel.Text + "\n\n" + _historyLabel.Text + "\n\n" + _tapeLabel.Text); break;
                case 5: FlushOutbox(); break;
                case 6: SceneRouter.Instance.GoToLanRace(); break;
            }
            RefreshUi();
        };
        AddChild(menu);
        menu.Theme = MedievalUiTheme();
        menu.Popup(new Rect2I((Vector2I)(GetGlobalTransformWithCanvas() * MpSpec.Rect("brief.code").Position + new Vector2(0, 58)), Vector2I.Zero));
        menu.PopupHide += menu.QueueFree;
    }

    private Theme MedievalUiTheme() => Theme ?? GetTree().Root.Theme;

    /// <summary>Keeps the painted briefing and footer in step with the live selectors and status.</summary>
    private void RefreshRoyal()
    {
        if (_royalLayer == null) return;
        var stage = GameData.GetStage(Mathf.Clamp(_selectedStage, 1, GameState.Instance.MaxStage));
        _routeLabel.Text = stage.MapName.Replace("'", "’");
        _routeStage.Text = $"Stage {stage.StageNumber} · {stage.StageName}";
        var mission = $"res://assets/ui/royal/missions/{RouteCatalog.Normalize(stage.MapId)}.png";
        _briefArt.Texture = ResourceLoader.Exists(mission) ? RoyalArt.Load(mission) : null;
        var mutator = AsyncChallengeCatalog.GetMutator(_selectedMutatorId);
        _mutatorLabel.Text = mutator?.Title ?? "";
        RoyalUiTools.Clear(_skulls);
        var threat = mutator == null ? 1 : Mathf.Clamp(Mathf.RoundToInt((mutator.EnemyHealthScale + mutator.EnemyDamageScale - 2f) * 4f) + 1, 1, 3);
        for (var i = 0; i < 3; i++) _skulls.AddChild(RoyalKit.Image(i < threat ? "icon-skull-lit" : "icon-skull-dim", new Rect2(i * 26, 0, 16, 20)));
        _skulls.TooltipText = $"Threat {threat} of 3";
        if (_royalStart != null)
        {
            _royalStart.Disabled = _startButton.Disabled;
            _royalStart.TooltipText = _startButton.TooltipText.Length > 0 ? _startButton.TooltipText : "Start challenge";
        }
        var status = _lastStatusMessage ?? "";
        if (status.Length > 0 && status != _shownStatus) { _shownStatus = status; RoyalToast.Show(this, status, 606); }
    }

    /// <summary>A room on the board, laid out like the concept's room rows.</summary>
    private Control BuildRoyalRoomRow(OnlineRoomDirectoryEntry room, int index)
    {
        var spec = MpSpec;
        var origin = spec.Rect("room.1");
        Rect2 Part(string part) => new(spec.Rect("room.1" + part).Position - origin.Position, spec.Rect("room.1" + part).Size);
        var hosted = OnlineRoomCreateService.GetHostedRoom()?.RoomId == room.RoomId;
        var highlight = index == 0;
        var row = new Control { CustomMinimumSize = new Vector2(origin.Size.X, origin.Size.Y), MouseFilter = MouseFilterEnum.Pass,
            TooltipText = $"{room.Title} · {room.Status}\n{room.BoardCode} · Host {room.HostCallsign} · {room.Region}\n{room.Summary}" };
        row.AddChild(new Panel { Size = origin.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice(highlight ? "mp-room-selected" : "mp-room", 14))));
        var art = Part(".art");
        row.AddChild(new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ClipContents = true,
            Texture = RoyalKit.Texture($"mp-room-art-{index % 3 + 1}"), Position = art.Position, Size = art.Size, MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(RoyalKit.Image("mp-tab", Part(".tab")));
        var number = spec.Label("room.1.number", (index + 1).ToString(), 40);
        number.Position -= origin.Position;
        row.AddChild(number);
        row.AddChild(RoyalKit.Image("mp-banner", Part(".banner")));
        row.AddChild(RoyalKit.Image("mp-players", Part(".players.icon")));
        var players = spec.Label("room.1.players", $"{room.CurrentPlayers} / {room.MaxPlayers}", 80);
        players.Position -= origin.Position;
        row.AddChild(players);
        var slot = Part(".slot.1");
        var units = room.UsesLockedDeck && room.LockedDeckUnitIds.Length > 0 ? room.LockedDeckUnitIds : new[] { "player_defender", "player_shooter", "player_spear", "player_brawler" };
        for (var i = 0; i < Math.Min(4, Math.Max(1, room.MaxPlayers)); i++)
        {
            var rect = new Rect2(slot.Position + new Vector2(i * 86, 0), new Vector2(78, slot.Size.Y));
            if (rect.End.X > Part(".sidebanner").Position.X - 4) break;
            row.AddChild(new Panel { Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("mp-slot", 8))));
            if (i < room.CurrentPlayers)
            {
                var figure = new UnitFigure { Position = rect.Position + new Vector2(8, 4), Size = rect.Size - new Vector2(16, 8) };
                figure.SetUnit(GameData.TryGetUnit(units[i % units.Length]));
                row.AddChild(figure);
            }
            else row.AddChild(RoyalKit.Image("empty-plus", new Rect2(rect.GetCenter() - new Vector2(11, 11), new Vector2(22, 22)), new Color(1, 1, 1, .6f)));
        }
        row.AddChild(RoyalKit.Image("mp-sidebanner", Part(".sidebanner")));
        var open = room.CurrentPlayers < room.MaxPlayers;
        var ready = Part(".ready");
        row.AddChild(new Panel { Position = ready.Position, Size = ready.Size, MouseFilter = MouseFilterEnum.Ignore }.With(p => p.AddThemeStyleboxOverride("panel", RoyalKit.Slice("mp-ready", 10))));
        row.AddChild(RoyalKit.Image("mp-dot-green", Part(".ready.dot"), open ? Colors.White : new Color(.4f, .4f, .4f)));
        var readyLabel = spec.Label("room.1.ready.label", open ? "OPEN" : "FULL", 80, open ? null : new Color("b7b6b6"));
        readyLabel.Position -= origin.Position;
        row.AddChild(readyLabel);
        var join = RoyalButton.Over(Part(".join"), hosted ? "Hosting" : "Join room", () => RequestOnlineRoomJoin(room), 6);
        join.SetStates(RoyalKit.Slice(highlight ? "mp-join" : "mp-join-idle", 12), 6);
        join.Disabled = hosted || !open;
        var joinLabel = spec.Label("room.1.join.label", hosted ? "HOSTING" : "JOIN", 140, highlight ? null : new Color("f2eedc"));
        joinLabel.Position -= origin.Position + join.Position;
        join.SetCaption(joinLabel, new Rect2(joinLabel.Position, joinLabel.Size));
        row.AddChild(join);
        var preview = RoyalButton.Over(new Rect2(art.Position, new Vector2(art.Size.X, art.Size.Y)), "Preview this room's board", () => LoadOnlineRoomBoard(room), 6);
        preview.MouseFilter = MouseFilterEnum.Pass;
        row.AddChild(preview);
        return row;
    }
}
