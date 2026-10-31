using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Every vertical scroll area must reveal its last row. Scrolled to its end (with any outer scroll
/// following), the bottom-most content must sit inside what the screen shows, and a wheel turn or a
/// finger drag over the area must move it.
///   --scroll-reach [--only=ProfileMenu,levels] [--late-game] [--mobile-preview]
/// </summary>
public partial class UiReviewSmoke
{
    private static readonly string[] ScrollReachDestinations = { "levels", "achievements", "more" };

    private async Task ReviewScrollReach()
    {
        var lateGame = OS.GetCmdlineUserArgs().Contains("--late-game");
        _output = ProjectSettings.GlobalizePath(lateGame ? "res://artifacts/scroll-reach/late-game" : "res://artifacts/scroll-reach/fresh");
        System.IO.Directory.CreateDirectory(_output);
        if (lateGame) SeedLateGameSave();
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--only="))?["--only=".Length..].Split(',');
        bool Want(string name) => only == null || only.Contains(name);
        foreach (var scene in new[] { SceneRouter.MainMenuScene }.Concat(LiveUiReview.ActivityScenes))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(scene);
            if (!Want(name)) continue;
            await Open(scene);
            await AuditScrollReach(name);
            foreach (var tab in VisibleTabs())
            {
                await Open(scene);
                await PressTab(tab);
                await AuditScrollReach(name + "-" + tab.Replace(" ", "-"));
            }
        }
        foreach (var destination in ScrollReachDestinations.Where(Want))
        {
            await Open("MainMenu");
            ((MapMenu)GetTree().CurrentScene).OpenHomeDestination(destination); await Wait(.5);
            await AuditScrollReach(destination);
            foreach (var tab in VisibleTabs())
            {
                await Open("MainMenu");
                ((MapMenu)GetTree().CurrentScene).OpenHomeDestination(destination); await Wait(.5);
                await PressTab(tab);
                await AuditScrollReach(destination + "-" + tab.Replace(" ", "-"));
            }
        }
        if (Want("extras")) await ReviewScrollReachExtras();
        if (Want("ProfileMenu")) { await Open(SceneRouter.ProfileScene); await ProbeTapInScroll(); }
        GD.Print($"SCROLL_REACH_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }

    /// <summary>Scroll areas outside the home pages: briefings, relic equipping, the forge, the account dialog and the battle menus.</summary>
    private async Task ReviewScrollReachExtras()
    {
        var state = GameState.Instance;
        state.PrepareCampaignBattle();
        await Open(SceneRouter.LoadoutScene);
        var loadout = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<LoadoutMenu>().First();
        EncounterBriefing.Show(loadout, GameData.GetStage(state.SelectedStage));
        var briefing = loadout.GetNode<CanvasLayer>("EncounterBriefing");
        await AuditScrollReach("encounter-briefing", briefing); briefing.QueueFree();
        await Open("MainMenu");
        var home = (MapMenu)GetTree().CurrentScene;
        // Relics are equipped on the relics page: its ally list and before/after comparison.
        SceneRouter.Instance.GoToShop(3); await Wait(.3);
        var armory = Walk(home).OfType<ShopMenu>().Single();
        armory.BeginRelicEquip(GameData.GetAllEquipment().First().Id, state.GetOwnedPlayerUnits().First().Id); await Wait(.2);
        await AuditScrollReach("relic-equip", armory);
        home.CloseHomeModal(); await Wait(.2);
        ForgeEnchantmentInspector.Show(home, _ => { }, GameData.GetAllEquipment().First().Id); await Wait(.2);
        var forge = Walk(home).OfType<ForgeEnchantmentInspector>().Last();
        await AuditScrollReach("enchantments", forge); forge.QueueFree();
        // Players open the account dialog from Settings: the Account page's Manage row.
        await Open("SettingsMenu"); await PressTab("Account");
        Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<RoyalButton>()
            .Single(b => b.IsVisibleInTree() && b.AccessibilityName == "Account" && !b.HasMeta("royal_tab"))
            .EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.3);
        var account = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<AccountDialog>().Single();
        await AuditScrollReach("account", account); account.Close();
        state.PrepareCampaignBattle();
        await Open("Battle");
        var battle = GetTree().CurrentScene;
        await PressHint("Battle menu [Escape]");
        await AuditScrollReach("battle-menu", battle);
        await Press("Battle guide");
        var guide = battle.GetNode<CanvasLayer>("CombatGuide");
        await AuditScrollReach("battle-guide", guide); guide.QueueFree(); await Wait(.1);
        await Press("Game settings");
        await AuditScrollReach("battle-settings", battle.GetNode("BattleSettingsLayer"));
        ((BattleController)battle).CloseBattleSettings(); await Wait(.2);
        await Press("Resume");
        typeof(BattleController).GetMethod("EndBattle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, new object[] { true });
        await Wait(1.5);
        await AuditScrollReach("battle-victory", battle);
    }

    /// <summary>The pages a screen opens on besides its current one.</summary>
    private string[] VisibleTabs() => LiveUiReview.VisibleTabs(LiveUiReview.ActiveRoot(GetTree()))
        .Where(x => !x.ButtonPressed).Select(x => x.Text).ToArray();

    /// <summary>A long-played save so every list is as long as a player will see it.</summary>
    private void SeedLateGameSave()
    {
        var state = GameState.Instance; state.ResetProgress();
        var fixture = state.BuildSaveData();
        fixture.Gold = 98_450; fixture.Food = 240; fixture.RelicShards = 860; fixture.Sigils = 312; fixture.Essence = 1_420;
        fixture.HighestUnlockedStage = state.MaxStage;
        fixture.StageStars = Enumerable.Range(1, state.MaxStage).Select(stage => stage % 4 == 0 ? 2 : 3).ToArray();
        fixture.OwnedPlayerUnitIds = GameData.GetPlayerUnits().Select(u => u.Id).ToArray();
        fixture.OwnedPlayerSpellIds = GameData.GetPlayerSpells().Select(s => s.Id).ToArray();
        fixture.OwnedEquipmentIds = GameData.GetAllEquipment().Select(e => e.Id).ToArray();
        fixture.DiscoveredCodexIds = CodexCatalog.GetAll().Select(e => e.Id).ToArray();
        fixture.UnlockedAchievementIds = AchievementCatalog.GetAll().Select(a => a.Id).ToArray();
        fixture.PlayerXP = PlayerLevelCatalog.TotalXPForLevel(46);
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(state, new object[] { fixture });
        state.SetShowHints(false); state.SetAnalyticsConsent(false);
    }

    // The audited surface: the active home page by default, or an inspector layer or dialog window.
    private Node _scrollRoot;
    private Viewport _scrollViewport;

    private async Task AuditScrollReach(string screen, Node surface = null, Viewport viewportOverride = null)
    {
        // Let entrance fades finish so rows are not skipped as transparent.
        await Wait(0.5);
        var root = _scrollRoot = surface ?? LiveUiReview.ActiveRoot(GetTree());
        _scrollViewport = viewportOverride ?? GetViewport();
        var scrolls = Walk(root).OfType<ScrollContainer>()
            .Where(s => s.IsVisibleInTree() && s.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled).ToArray();
        var viewport = _scrollViewport.GetVisibleRect();
        var index = 0;
        foreach (var scroll in scrolls)
        {
            if (!GodotObject.IsInstanceValid(scroll) || !scroll.IsVisibleInTree()) continue;
            index++;
            var label = $"{screen}: {DescribeScrollPart(scroll)}";
            await ScrollToEnd(scroll);
            var area = VisibleArea(scroll, viewport);
            var rows = ScrollRows(scroll, scroll).ToArray();
            if (area.Size.Y < 1)
            {
                _failures++; GD.Print($"SCROLL_HIDDEN {label} shows nothing even when scrolled into view ({scroll.GetGlobalRect()})");
                continue;
            }
            if (rows.Length == 0) continue;
            var last = rows.OrderByDescending(r => r.Rect.End.Y).First();
            var bar = scroll.GetVScrollBar();
            var travel = bar.MaxValue - bar.Page;
            GD.Print($"SCROLL_AREA {label} view {area.Size.Y:0} of {scroll.Size.Y * scroll.GetGlobalTransform().Scale.Y:0}, travel {Math.Max(0, travel):0}, last row ends {last.Rect.End.Y - area.End.Y:+0;-0;0} past view");
            // A strip a row or two tall technically scrolls, but reads as cut off (boss rush once showed one commander at a time).
            if (travel > 4 && area.Size.Y < 96)
            {
                _failures++;
                GD.Print($"SCROLL_SQUASHED {label} shows {area.Size.Y:0}px of {area.Size.Y + travel * scroll.GetGlobalTransform().Scale.Y:0}px");
                await Capture($"squashed-{screen}-{index}");
            }
            if (last.Rect.End.Y > area.End.Y + 3)
            {
                _failures++;
                GD.Print($"SCROLL_CUT_OFF {label} last row {DescribeScrollPart(last.Control)} ends at {last.Rect.End.Y:0}, {last.Rect.End.Y - area.End.Y:0}px below the visible end {area.End.Y:0}");
                await Capture($"cut-{screen}-{index}");
            }
            if (travel > 4) await ProbeScrollInput(scroll, label, viewport);
        }
        await Capture("reach-" + screen);
    }

    /// <summary>Scrolls an area to its end, then each outer scroll until that end is in view.</summary>
    private async Task ScrollToEnd(ScrollContainer scroll)
    {
        scroll.ScrollVertical = int.MaxValue / 4;
        await Frames(2);
        for (var parent = scroll.GetParent(); parent != null && parent is not CanvasLayer; parent = parent.GetParent())
        {
            if (parent is not ScrollContainer outer || outer.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled) continue;
            var scale = outer.GetGlobalTransform().Scale.Y;
            var overshoot = scroll.GetGlobalRect().End.Y - outer.GetGlobalRect().End.Y;
            outer.ScrollVertical += Mathf.CeilToInt(overshoot / scale);
            await Frames(2);
        }
    }

    /// <summary>
    /// A wheel turn and a finger drag at points across the area must move it or a scroll around it.
    /// Scrolls nested inside start at their end, as after a player has read them through.
    /// </summary>
    private async Task ProbeScrollInput(ScrollContainer scroll, string label, Rect2 viewport)
    {
        var root = _scrollRoot;
        var scene = GetTree().CurrentScene; var destination = (scene as MapMenu)?.HomeModalDestination;
        var saved = ScrollPositions();
        var nested = Walk(scroll).OfType<ScrollContainer>().Where(s => s != scroll && s.IsVisibleInTree()).ToArray();
        var middle = Mathf.Max(0, (int)(scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page) / 2);
        async Task Reset()
        {
            scroll.ScrollVertical = middle;
            foreach (var inner in nested) inner.ScrollVertical = int.MaxValue / 4;
            await Frames(2);
        }
        var wheelBlocked = new List<string>(); var dragBlocked = new List<string>();
        await Reset();
        var area = VisibleArea(scroll, viewport);
        foreach (var fy in new[] { .25f, .5f, .75f })
        foreach (var fx in new[] { .2f, .5f, .8f })
        {
            var point = area.Position + area.Size * new Vector2(fx, fy);
            await Reset();
            // A button only takes a press while hovered, and hover follows the real pointer.
            _scrollViewport.WarpMouse(point); await Frames(2);
            var target = TopControlAt(root, point, viewport);
            var where = $"{DescribeScrollPart(target)} at {point.X:0},{point.Y:0}";
            var before = ScrollPositions();
            SendTo(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 1, Position = point, GlobalPosition = point });
            SendTo(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = false, Factor = 1, Position = point, GlobalPosition = point });
            await Frames(2);
            if (!Moved(before)) wheelBlocked.Add(where);
            // Dragging a slider or into a text field would change it rather than scroll.
            if (target is LineEdit or TextEdit or Godot.Range) continue;
            await Reset();
            before = ScrollPositions();
            await Drag(point, target);
            if (!GodotObject.IsInstanceValid(scroll) || !scroll.IsInsideTree() || GetTree().CurrentScene != scene || (scene as MapMenu)?.HomeModalDestination != destination)
            { _failures++; GD.Print($"SCROLL_DRAG_ACTED {label}: a drag over {where} changed the screen"); return; }
            if (!Moved(before)) dragBlocked.Add(where);
        }
        foreach (var other in Walk(root).OfType<ScrollContainer>())
            if (saved.TryGetValue(other.GetInstanceId(), out var value)) other.ScrollVertical = value;
        await Frames(2);
        if (wheelBlocked.Count > 0) { _failures++; GD.Print($"SCROLL_WHEEL_BLOCKED {label} over {string.Join("; ", wheelBlocked)}"); }
        if (dragBlocked.Count > 0) { _failures++; GD.Print($"SCROLL_DRAG_BLOCKED {label} over {string.Join("; ", dragBlocked.Distinct())}"); }
    }

    /// <summary>
    /// Letting drags through to the scroll must not cost taps: a tap with a fingertip's wobble on a button in a
    /// list still presses it, and a drag that starts on it scrolls the list without pressing it.
    /// </summary>
    private async Task ProbeTapInScroll()
    {
        _scrollRoot = LiveUiReview.ActiveRoot(GetTree()); _scrollViewport = GetViewport();
        var scroll = Walk(_scrollRoot).OfType<ScrollContainer>()
            .First(s => s.IsVisibleInTree() && s.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled);
        var stack = scroll.GetChildren().OfType<Control>().First(c => c is not ScrollBar);
        var button = new RealmButton { Text = "Tap probe", CustomMinimumSize = new Vector2(220, 56) };
        var presses = 0; button.Pressed += () => presses++;
        stack.AddChild(button); stack.MoveChild(button, 0);
        scroll.ScrollVertical = 0;
        await Frames(3);
        Check(button.MouseFilter == Control.MouseFilterEnum.Pass, "A button in a scroll area passes drags to it");
        Check(scroll.ScrollDeadzone >= 8, $"Scroll areas ignore a tap's wobble (deadzone {scroll.ScrollDeadzone})");
        var centre = button.GetGlobalRect().GetCenter();
        var emulate = Input.EmulateTouchFromMouse;
        Input.EmulateTouchFromMouse = true;
        try
        {
            // Hover follows the real pointer, and a button only takes a press while hovered.
            GetViewport().WarpMouse(centre); await Frames(2);
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = centre, GlobalPosition = centre });
            await Frames(1);
            var wobble = centre + new Vector2(2, -3);
            Send(new InputEventMouseMotion { Position = wobble, GlobalPosition = wobble, Relative = new Vector2(2, -3), ButtonMask = MouseButtonMask.Left });
            await Frames(1);
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = wobble, GlobalPosition = wobble });
            await Frames(2);
            Check(presses == 1 && scroll.ScrollVertical == 0, $"A wobbly tap presses a button in a list without scrolling it ({presses} presses, scrolled {scroll.ScrollVertical})");
            await Drag(centre, button);
            Check(presses == 1 && scroll.ScrollVertical > 0, $"A drag from a button scrolls its list without pressing it ({presses} presses, scrolled {scroll.ScrollVertical})");
        }
        finally { Input.EmulateTouchFromMouse = emulate; button.QueueFree(); }
    }

    /// <summary>A finger drag upward, as phones deliver it (emulated mouse events with touch scrolling on).</summary>
    private async Task Drag(Vector2 point, Control target = null)
    {
        var emulate = Input.EmulateTouchFromMouse;
        Input.EmulateTouchFromMouse = true; // ScrollContainer only drag-scrolls when a touchscreen is available.
        try
        {
            if (!DisplayServer.IsTouchscreenAvailable()) throw new InvalidOperationException("Touch scrolling cannot be exercised on this display server.");
            SendTo(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left, Position = point, GlobalPosition = point });
            await Frames(1);
            var position = point;
            for (var step = 0; step < 6; step++)
            {
                position += new Vector2(0, -12);
                SendTo(new InputEventMouseMotion { Position = position, GlobalPosition = position, Relative = new Vector2(0, -12), ButtonMask = MouseButtonMask.Left });
                await Frames(1);
            }
            // Once the list moves, the button under the finger lets go of its press.
            if (target is BaseButton { ToggleMode: false } held && held.GetDrawMode() is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed)
            { _failures++; GD.Print($"SCROLL_DRAG_HOLDS_PRESS {DescribeScrollPart(held)} still looks pressed while its list scrolls"); }
            SendTo(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position });
            await Frames(2);
        }
        finally { Input.EmulateTouchFromMouse = emulate; }
    }

    private void SendTo(InputEvent input) => _scrollViewport.PushInput(input, true);

    private Dictionary<ulong, int> ScrollPositions() => Walk(_scrollRoot).OfType<ScrollContainer>()
        .ToDictionary(s => s.GetInstanceId(), s => s.ScrollVertical);

    private bool Moved(Dictionary<ulong, int> before) => Walk(_scrollRoot).OfType<ScrollContainer>()
        .Any(s => before.TryGetValue(s.GetInstanceId(), out var value) && s.ScrollVertical > value);

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>The part of a control the screen can show: its rect cut by every clipping ancestor.</summary>
    private static Rect2 VisibleArea(Control control, Rect2 viewport)
    {
        var area = control.GetGlobalRect().Intersection(viewport);
        for (var parent = control.GetParent(); parent != null && parent is not CanvasLayer; parent = parent.GetParent())
            if (parent is Control { ClipContents: true } clip) area = area.Intersection(clip.GetGlobalRect());
        return area;
    }

    /// <summary>Visible content rows of a scroll, cut by clips inside it; nested scrolls count as one row.</summary>
    private static IEnumerable<(Control Control, Rect2 Rect)> ScrollRows(ScrollContainer scroll, Control node)
    {
        foreach (var child in node.GetChildren().OfType<Control>())
        {
            if (!child.Visible || child is ScrollBar || child.Size.X < 1 || child.Size.Y < 1 || child.Modulate.A < .05f || child.SelfModulate.A < .05f && child.GetChildCount() == 0) continue;
            var rect = child.GetGlobalRect();
            for (var parent = child.GetParent(); parent != scroll && parent is Control clip; parent = parent.GetParent())
                if (clip.ClipContents) rect = rect.Intersection(clip.GetGlobalRect());
            if (rect.Size.Y < 1) continue;
            yield return (child, rect);
            if (child is ScrollContainer) continue;
            foreach (var row in ScrollRows(scroll, child)) yield return row;
        }
    }

    /// <summary>Approximates the GUI pick: the last visible, mouse-catching control drawn under the point.</summary>
    private static Control TopControlAt(Node root, Vector2 point, Rect2 viewport)
    {
        Control top = null;
        foreach (var control in Walk(root).OfType<Control>())
            if (control.IsVisibleInTree() && control.MouseFilter != Control.MouseFilterEnum.Ignore && VisibleArea(control, viewport).HasPoint(point))
                top = control;
        return top;
    }

    private static string DescribeScrollPart(Control control)
    {
        if (control == null) return "nothing";
        var text = control switch { Label l => l.Text, Button b => b.Text, _ => "" };
        text = string.IsNullOrWhiteSpace(text) ? "" : $" \"{text.Replace("\n", " ")[..Math.Min(32, text.Length)]}\"";
        var path = control.GetPath().ToString();
        var tail = string.Join("/", path.Split('/').TakeLast(3));
        return $"{control.GetType().Name}{text} …/{tail}";
    }
}
