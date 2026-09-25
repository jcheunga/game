using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>Real-engine UI checks. Requires an isolated save and writes review screenshots.</summary>
public partial class UiReviewSmoke : Node
{
    private int _failures;
    private string _output;
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--save-suffix=ui-review-")))
                throw new InvalidOperationException("UI smoke requires --save-suffix=ui-review-<unique-id>.");
            var requestedSize = OS.GetCmdlineUserArgs().Contains("--small-window") ? new Vector2I(1024, 768) : new Vector2I(1280, 720);
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = requestedSize;
            await Wait(0.5);
            Check(GetWindow().Size == requestedSize, $"Review window uses {requestedSize}");
            GetTree().CurrentScene = null; // Keep this test driver alive through real scene transitions.
            _output = ProjectSettings.GlobalizePath("res://artifacts/ui-review");
            System.IO.Directory.CreateDirectory(_output);
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            if (OS.GetCmdlineUserArgs().Contains("--progression"))
            { await ReviewProgression(); return; }
            Check(!LanChallengeService.Instance.HasRoom, "Offline multiplayer peer is not a LAN room");
            if (OS.GetCmdlineUserArgs().Contains("--adventure"))
            { await ReviewAdventure(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--typography-advanced"))
            {
                _output = ProjectSettings.GlobalizePath("res://artifacts/typography-advanced");
                System.IO.Directory.CreateDirectory(_output);
                await Open("ShopMenu"); await AuditArmoryPages();
                await Press("Battle rites"); await AuditArmoryPages();
                BattleSummaryData.Current = new BattleSummaryData { Won = true, Stage = 60, StarsEarned = 3, BattleMode = "Campaign", ElapsedSeconds = 123, EnemiesDefeated = 145, UnitsDeployed = 35, UnitsLost = 12, SpellsCast = 7, TotalDamageDealt = 123456, TotalDamageTaken = 12345, GoldEarned = 1250, FoodEarned = 12, SeasonXPEarned = 100, MasteryXPPerUnit = GameData.GetPlayerUnits().ToDictionary(x => x.Id, _ => 250) };
                await Open("BattleSummaryMenu"); AuditText("Populated battle summary"); await Capture("type-populated-summary");
                GameState.Instance.UnlockNextStage(GameState.Instance.MaxStage - 1);
                await Open("MainMenu"); await Press("Caravan"); AuditText("MainMenu / all unlocks"); await Capture("type-main-unlocked");
                foreach (var stage in GameData.Stages)
                {
                    GameState.Instance.SetSelectedStage(stage.StageNumber);
                    foreach (var scene in new[] { "MapMenu", "LoadoutMenu" })
                    {
                        await Open(scene); var before = _failures; AuditText(scene + " / stage " + stage.StageNumber);
                        if (_failures != before || stage.StageNumber % 10 == 0) await Capture($"type-{scene}-stage-{stage.StageNumber}");
                    }
                }
                System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"TYPOGRAPHY_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--typography"))
            {
                _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/typography-small" : "res://artifacts/typography");
                System.IO.Directory.CreateDirectory(_output);
                foreach (var scene in new[] { "MainMenu", "MapMenu", "LoadoutMenu", "ShopMenu", "EndlessMenu", "MultiplayerMenu", "SettingsMenu", "ArenaMenu", "BattleSummaryMenu", "BountyMenu", "CashShopMenu", "CodexMenu", "EventMenu", "ExpeditionMenu", "ForgeMenu", "FriendsMenu", "GuildMenu", "LanRaceMenu", "LeaderboardMenu", "LoginCalendarMenu", "ProfileMenu", "RaidMenu", "SeasonPassMenu", "SkillTreeMenu", "TowerMenu" })
                {
                    await Open(scene);
                    await Capture("type-" + scene);
                    AuditText(scene);
                    var tabs = Walk(GetTree().CurrentScene).OfType<HBoxContainer>().Where(x => x.HasMeta("realm_tabs")).SelectMany(x => x.GetChildren().OfType<Button>()).Select(x => x.Text).ToArray();
                    foreach (var tab in tabs.Skip(1))
                    {
                        await Press(tab);
                        await Capture("type-" + scene + "-" + tab.Replace(" ", "-"));
                        AuditText(scene + "/" + tab);
                    }
                }
                GameState.Instance.PrepareCampaignBattle();
                await Open("Battle"); await Capture("type-Battle"); AuditText("Battle");
                System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"TYPOGRAPHY_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--all-menus"))
            {
                foreach (var scene in new[] { "ArenaMenu", "BattleSummaryMenu", "BountyMenu", "CashShopMenu", "CodexMenu", "EventMenu", "ExpeditionMenu", "ForgeMenu", "FriendsMenu", "GuildMenu", "LanRaceMenu", "LeaderboardMenu", "LoginCalendarMenu", "ProfileMenu", "RaidMenu", "SeasonPassMenu", "SkillTreeMenu", "TowerMenu" })
                {
                    await Open(scene);
                    var refresh = GetTree().CurrentScene.GetType().GetMethod("RefreshUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    refresh?.Invoke(GetTree().CurrentScene, null);
                    refresh?.Invoke(GetTree().CurrentScene, null);
                    await Wait(0.1);
                    await Capture("audit-" + scene);
                }
                GD.Print($"UI_REVIEW_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--playthrough"))
            {
                await CheckModeIsolation();
                GameState.Instance.PrepareCampaignBattle();
                await Open("LoadoutMenu"); await Press("Deploy");
                Engine.TimeScale = 3;
                for (var tick = 0; tick < 150 && GameState.Instance.GetStageStars(1) == 0; tick++)
                {
                    var enemy = Walk(GetTree().CurrentScene).OfType<Unit>().Where(x => x.Team == Team.Enemy && !x.IsDead).OrderBy(x => x.Position.X).FirstOrDefault();
                    var y = enemy?.Position.Y ?? 380;
                    Send(new InputEventKey { Keycode = tick % 4 == 0 ? Key.Key2 : Key.Key1, Pressed = true });
                    var pos = new Vector2(350, Mathf.Clamp(y, 230, 550));
                    Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = pos, GlobalPosition = pos });
                    Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pos, GlobalPosition = pos });
                    Send(new InputEventKey { Keycode = Key.C, Pressed = true });
                    if (tick % 12 == 0) Send(new InputEventKey { Keycode = Key.Z, Pressed = true });
                    if (tick == 15) await Capture("20-battle-in-progress");
                    await Wait(1.5);
                    if (Walk(GetTree().CurrentScene).OfType<Button>().Any(x => x.IsVisibleInTree() && x.Text == "Retry Stage")) break;
                }
                Engine.TimeScale = 1;
                await Wait(0.5); // Capture the readable end state after its entrance animation.
                AuditText("Battle victory");
                Check(GameState.Instance.GetStageStars(1) > 0, "Real-input campaign battle reaches victory");
                Check(SaveSystem.Instance.TryLoad(out var result) && result.StageStars.Length > 0 && result.StageStars[0] > 0, "Victory stars persist to disk");
                await Capture("21-victory");
                await Press("Back To Map");
                Check(GetTree().CurrentScene is MapMenu, "Victory returns to the campaign map");
                await Capture("23-map-after-victory");
                GD.Print($"UI_REVIEW_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1); return;
            }
            await Open("MainMenu");
            Check(!Walk(GetTree().CurrentScene).OfType<ScrollContainer>().Any(), "Home has no scrolling regions");
            await Capture("01-camp");
            await Press("Caravan"); await Capture("02-camp-caravan");
            await Press("Community"); await Capture("03-camp-community");
            await Open("MapMenu"); await Capture("04-map");
            await Press("Intel"); await Capture("05-map-intel");
            await Press("Prepare battle"); Check(GetTree().CurrentScene is LoadoutMenu, "Map opens preparation");
            await Capture("06-loadout");
            await Press("Details");
            var detailDialog = Walk(GetTree().CurrentScene).OfType<AcceptDialog>().FirstOrDefault(x => x.Visible);
            Check(detailDialog != null, "Unit details open on demand");
            detailDialog?.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await Wait(0.1);
            await Open("ShopMenu"); await Capture("07-armory");
            var oldGold = GameState.Instance.Gold;
            var oldLevel = GameState.Instance.GetUnitLevel(GameData.PlayerBrawlerId);
            var upgradeCost = GameState.Instance.GetUnitUpgradeCost(GameData.PlayerBrawlerId);
            await Press("Upgrade");
            Check(GameState.Instance.Gold == oldGold - upgradeCost && GameState.Instance.GetUnitLevel(GameData.PlayerBrawlerId) == oldLevel + 1, "Armory upgrade charges once and increases level");
            await Press("Unequip"); Check(!GameState.Instance.IsUnitInActiveDeck(GameData.PlayerBrawlerId), "Unit can leave squad");
            await Press("Equip"); Check(GameState.Instance.IsUnitInActiveDeck(GameData.PlayerBrawlerId), "Unit can return to squad");
            await Press("Battle rites"); await Capture("08-rites");
            await Press("War wagon"); await Capture("09-wagon");
            await Press("Relics"); await Capture("10-relics");
            await Open("MultiplayerMenu"); await Capture("18-challenges");
            await Press("Daily"); await Capture("19-daily");
            await Open("EndlessMenu"); await Capture("11-endless");
            await Open("SettingsMenu"); await Capture("12-settings");
            await Open("LoadoutMenu");
            var food = GameState.Instance.Food;
            var cost = GameState.Instance.GetStageEntryFoodCost(GameState.Instance.SelectedStage);
            await Press("Deploy");
            Check(GetTree().CurrentScene is BattleController, "Deployment opens battle");
            Check(GameState.Instance.Food == food - cost, "Deployment charges food once");
            await Capture("13-battle");
            Send(new InputEventKey { Keycode = Key.Key1, Pressed = true });
            await Wait(0.1);
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(350, 380), GlobalPosition = new Vector2(350, 380) });
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(350, 380), GlobalPosition = new Vector2(350, 380) });
            await Wait(2);
            Check(Walk(GetTree().CurrentScene).OfType<Unit>().Any(x => x.Team == Team.Player && !x.IsDead), "Unit card deploys a live ally");
            await Capture("14-deployed");
            await PressHint("Pause [Escape]"); await Capture("15-paused");
            await Press("Resume battle");
            await PressHint("Battle intel [Tab]"); await Capture("16-battle-intel");
            await PressHint("Battle intel [Tab]");
            await PressHint("Retreat"); await Capture("17-retreat");
            Check(SaveSystem.Instance.TryLoad(out _), "Isolated save reloads");
            GD.Print($"UI_REVIEW_RESULT: {_failures} failures");
            GetTree().Quit(_failures == 0 ? 0 : 1);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckModeIsolation()
    {
        var campaignStars = GameState.Instance.TotalStarsEarned;
        var stage = GameState.Instance.HighestUnlockedStage;
        GameState.Instance.PrepareTowerBattle(1);
        await Open("Battle");
        // Exercise the real result handler with a deterministic victory, independent of combat balance.
        typeof(BattleController).GetMethod("EndBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(GetTree().CurrentScene, new object[] { true });
        Check(GameState.Instance.GetTowerFloorStars(1) > 0, "Tower result awards tower stars");
        Check(GameState.Instance.TotalStarsEarned == campaignStars && GameState.Instance.HighestUnlockedStage == stage, "Tower result leaves campaign progress intact");
        await Capture("22-tower-result");
    }

    private async Task AuditArmoryPages()
    {
        var names = GameData.GetPlayerUnits().Select(x => x.DisplayName).Concat(GameData.GetPlayerSpells().Select(x => x.DisplayName)).ToHashSet();
        for (var page = 0; page < 20; page++)
        {
            var cards = Walk(GetTree().CurrentScene).OfType<Button>().Where(x => x.IsVisibleInTree() && names.Contains(x.AccessibilityName)).Select(x => x.AccessibilityName).ToArray();
            foreach (var name in cards)
            {
                var card = Walk(GetTree().CurrentScene).OfType<Button>().First(x => x.IsVisibleInTree() && x.AccessibilityName == name);
                card.EmitSignal(BaseButton.SignalName.Pressed); await Wait(0.1);
                AuditText("Armory / " + name);
            }
            await Capture("type-armory-" + cards.FirstOrDefault()?.Replace(" ", "-"));
            var next = Walk(GetTree().CurrentScene).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && x.TooltipText == "Next roster page" && !x.Disabled);
            if (next == null) break;
            next.EmitSignal(BaseButton.SignalName.Pressed); await Wait(0.1);
        }
    }

    private readonly List<object> _textAudit = new();
    private void AuditText(string screen)
    {
        var viewport = new Rect2(0, 0, 1280, 720);
        var panels = Walk(GetTree().CurrentScene).OfType<PanelContainer>().Where(x => x.IsVisibleInTree() && x.GetParent() == GetTree().CurrentScene).ToArray();
        for (var i = 0; i < panels.Length; i++) for (var j = i + 1; j < panels.Length; j++)
        {
            var overlap = panels[i].GetGlobalRect().Intersection(panels[j].GetGlobalRect());
            if (overlap.Size.X > 4 && overlap.Size.Y > 4)
            { GD.Print($"PANEL_OVERLAP {screen}: {panels[i].GetPath()} / {panels[j].GetPath()} {overlap}"); _failures++; }
        }
        foreach (var button in Walk(GetTree().CurrentScene).OfType<Button>().Where(x => x.IsVisibleInTree() && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var box = button.GetThemeStylebox("normal");
            var available = button.Size.X - box.GetContentMargin(Side.Left) - box.GetContentMargin(Side.Right);
            if (button.Icon != null) available -= button.GetThemeConstant("icon_max_width") + button.GetThemeConstant("h_separation");
            var font = button.GetThemeFont("font"); var size = button.GetThemeFontSize("font_size");
            var widest = button.Text.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
            if (button.AutowrapMode == TextServer.AutowrapMode.Off && widest > available + 3)
            { GD.Print($"BUTTON_TEXT_ISSUE {screen}: {button.Text.Replace("\n", " ")} ({widest:0} > {available:0})"); _failures++; }
        }
        foreach (var control in Walk(GetTree().CurrentScene).OfType<Control>().Where(x => x.IsVisibleInTree() && !x.HasMeta("realm_tooltip") && (x is Label || x is Button)))
        {
            for (var parent = control.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is not ScrollContainer scroll) continue;
                var rect = control.GetGlobalRect(); var clip = scroll.GetGlobalRect().Grow(3);
                if (scroll.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled && (rect.Position.X < clip.Position.X || rect.End.X > clip.End.X))
                { GD.Print($"SCROLL_TEXT_CLIP {screen}: {control.GetPath()} {rect} outside {clip}"); _failures++; break; }
            }
        }
        foreach (var label in Walk(GetTree().CurrentScene).OfType<Label>().Where(x => x.IsVisibleInTree() && !x.HasMeta("realm_tooltip") && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var font = label.GetThemeFontSize("font_size");
            var issues = new List<string>();
            if (font < 18) issues.Add("small text");
            if (label.GetVisibleLineCount() < label.GetLineCount()) issues.Add("hidden lines");
            if (label.ClipText || label.MaxLinesVisible > 0) issues.Add("text limit");
            for (var parent = label.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is Button owner && !owner.GetGlobalRect().Grow(2).Encloses(label.GetGlobalRect()))
                { issues.Add("outside button"); break; }
            }
            if (!Clipped(label) && !viewport.Grow(2).Encloses(label.GetGlobalRect())) issues.Add("outside viewport");
            // Check the whole string, including long words and unwrapped status lines.
            var bounds = new Rect2(Vector2.Zero, label.Size).Grow(3);
            for (var i = 0; i < label.Text.Length; i++)
            {
                var glyph = label.GetCharacterBounds(i);
                if (glyph.Size.X > 0 && glyph.Size.Y > 0 && !bounds.Encloses(glyph))
                { issues.Add("glyph overflow"); break; }
            }
            _textAudit.Add(new { screen, text = label.Text, font, width = label.Size.X, height = label.Size.Y, lines = label.GetLineCount(), visibleLines = label.GetVisibleLineCount(), issues });
            if (issues.Count > 0)
            { _failures++; GD.Print($"TEXT_ISSUE {screen}: {string.Join(", ", issues)} | {label.Text.Replace("\n", " ")[..Math.Min(100, label.Text.Length)]}"); }
        }
    }

    private void Send(InputEvent input) => GetViewport().PushInput(input, true);

    private async Task Open(string scene)
    {
        var error = GetTree().ChangeSceneToFile($"res://scenes/{scene}.tscn");
        if (error != Error.Ok) throw new InvalidOperationException($"Cannot open {scene}: {error}");
        await Wait(0.8);
    }
    private async Task Press(string text)
    {
        var button = Walk(GetTree().CurrentScene).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && !x.Disabled && x.Text.StartsWith(text, StringComparison.Ordinal));
        if (button == null) throw new InvalidOperationException($"Missing active button: {text}");
        if (button.ToggleMode) button.ButtonPressed = true;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(0.8);
    }
    private async Task PressHint(string hint)
    {
        var button = Walk(GetTree().CurrentScene).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && x.TooltipText == hint);
        if (button == null) throw new InvalidOperationException($"Missing icon button: {hint}");
        if (button.ToggleMode) button.ButtonPressed = true;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(0.5);
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(); // Hidden macOS windows may stop emitting automatic draw signals.
        GetViewport().GetTexture().GetImage().SavePng($"{_output}/{name}.png");
        var viewport = new Rect2(0, 0, 1280, 720);
        foreach (var control in Walk(GetTree().CurrentScene).OfType<Control>())
        {
            if (!control.IsVisibleInTree() || control.IsQueuedForDeletion() || control is not Button || Clipped(control)) continue;
            var rect = control.GetGlobalRect();
            if (rect.Size.X > 0 && rect.Size.Y > 0 && !viewport.Grow(2).Encloses(rect))
            { GD.Print($"UI_OVERFLOW {name}: {control.GetPath()} {rect} {(control as Button)?.Text}"); _failures++; }
        }
        GD.Print($"UI_CAPTURE {name}");
    }
    private static bool Clipped(Control control)
    {
        for (var p = control.GetParent(); p != null; p = p.GetParent())
            if (p is ScrollContainer) return true;
        return false;
    }
    private void Check(bool condition, string label)
    { GD.Print($"{(condition ? "PASS" : "FAIL")} {label}"); if (!condition) _failures++; }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
