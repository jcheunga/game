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
            var requestedSize = MobilePresentation.Enabled ? new Vector2I(844, 390)
                : OS.GetCmdlineUserArgs().Contains("--small-window") ? new Vector2I(1024, 768) : new Vector2I(1280, 720);
            GetWindow().Mode = Window.ModeEnum.Windowed;
            GetWindow().Size = requestedSize;
            await Wait(0.5);
            Check(GetWindow().Size == requestedSize, $"Review window uses {requestedSize}");
            GetTree().CurrentScene = null; // Keep this test driver alive through real scene transitions.
            _output = ProjectSettings.GlobalizePath("res://artifacts/ui-review");
            System.IO.Directory.CreateDirectory(_output);
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            if (OS.GetCmdlineUserArgs().Contains("--royal-ui")) { await ReviewRoyalUi(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--concepts")) { await ReviewConcepts(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--map-guides"))
            { await MapGuideRenderer.RenderAll(this, ProjectSettings.GlobalizePath("res://art/royal/gen/map-guides")); QuitAfterAudio(0); return; }
            if (OS.GetCmdlineUserArgs().Contains("--live-parity"))
            { await ReviewLiveParity(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--battle-cleanup"))
            { await ReviewBattleCleanup(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--battle-lighting"))
            { await ReviewBattleLighting(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--battle-polish"))
            { await ReviewBattlePolish(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--home-map"))
            { await ReviewTileMap(includeHome: true); return; }
            if (OS.GetCmdlineUserArgs().Contains("--armory-details"))
            { await ReviewArmoryDetails(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--developer"))
            {
                _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/home-map/small" : "res://artifacts/home-map/desktop");
                System.IO.Directory.CreateDirectory(_output);
                GameState.Instance.ResetProgress();
                GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetShowHints(false);
                await Open("MainMenu");
                await ReviewDeveloperMode((MapMenu)GetTree().CurrentScene);
                System.IO.File.WriteAllText(_output + "/developer-text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"DEVELOPER_REVIEW_RESULT: {_failures} failures");
                QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--map-notices"))
            {
                GameState.Instance.ResetProgress();
                GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetShowHints(false);
                await Open("MainMenu");
                var menu = (MapMenu)GetTree().CurrentScene;
                await ReviewMapNotices(menu, Walk(menu).OfType<MapPathCanvas>().Single());
                GD.Print($"MAP_NOTICE_REVIEW_RESULT: {_failures} failures");
                QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--storehouse"))
            {
                _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/home-map/small" : "res://artifacts/home-map/desktop");
                System.IO.Directory.CreateDirectory(_output);
                GameState.Instance.ResetProgress(); await Open("MainMenu");
                await ReviewStorehouse((MapMenu)GetTree().CurrentScene);
                System.IO.File.WriteAllText(_output + "/storehouse-text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"STOREHOUSE_REVIEW_RESULT: {_failures} failures");
                QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--map-rewards"))
            {
                _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/home-map/small" : "res://artifacts/home-map/desktop");
                System.IO.Directory.CreateDirectory(_output);
                GameState.Instance.ResetProgress();
                GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetShowHints(false);
                await Open("MainMenu");
                var menu = (MapMenu)GetTree().CurrentScene;
                await ReviewMapRewards(menu, Walk(menu).OfType<MapPathCanvas>().Single());
                GD.Print($"MAP_REWARD_REVIEW_RESULT: {_failures} failures");
                QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--poi-map") || OS.GetCmdlineUserArgs().Contains("--atlas-geometry"))
            { await ReviewTileMap(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--materials"))
            { await ReviewMaterials(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--deployment-cards"))
            { await ReviewDeploymentCards(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--drag-cards"))
            { await ReviewCardDragging(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--progression"))
            { await ReviewProgression(); return; }
            Check(!LanChallengeService.Instance.HasRoom, "Offline multiplayer peer is not a LAN room");
            if (OS.GetCmdlineUserArgs().Contains("--adventure"))
            { await ReviewTileMap(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--stage-stars"))
            { await ReviewStageStars(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--typography-advanced"))
            {
                _output = ProjectSettings.GlobalizePath("res://artifacts/typography-advanced");
                System.IO.Directory.CreateDirectory(_output);
                await Open("ShopMenu"); await AuditArmoryPages();
                await Press("Spells"); await AuditArmoryPages();
                var fixture = GameState.Instance.BuildSaveData();
                fixture.StageStars = Enumerable.Repeat(1, GameState.Instance.MaxStage).ToArray();
                fixture.HighestUnlockedStage = GameState.Instance.MaxStage;
                fixture.AdventureOpenTiles = GameData.Stages.SelectMany(stage => AdventureTileCatalog.ForMap(stage.MapId))
                    .Where(tile => !tile.IsResource).Select(tile => tile.Id).Distinct().ToArray();
                typeof(GameState).GetMethod("ApplySavedData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(GameState.Instance, new object[] { fixture });
                await Open("MainMenu"); await PressHint("More"); await Press("Caravan"); AuditText("MainMenu / all unlocks"); await Capture("type-main-unlocked");
                foreach (var stage in GameData.Stages)
                {
                    GameState.Instance.SetSelectedStage(stage.StageNumber);
                    foreach (var scene in new[] { "MapMenu", "LoadoutMenu" })
                    {
                        await Open(scene);
                        if (scene == "MapMenu") await ChooseAdventureSite("leader-" + stage.StageNumber);
                        else Check(((StageDefinition)typeof(LoadoutMenu).GetField("_stage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                            .GetValue(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<LoadoutMenu>().Single())).StageNumber == stage.StageNumber,
                            "Preparation displays requested stage " + stage.StageNumber);
                        var before = _failures; AuditText(scene + " / stage " + stage.StageNumber);
                        if (_failures != before || stage.StageNumber % 10 == 0) await Capture($"type-{scene}-stage-{stage.StageNumber}");
                    }
                }
                System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"TYPOGRAPHY_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--typography"))
            {
                _output = ProjectSettings.GlobalizePath(MobilePresentation.Enabled ? "res://artifacts/typography-phone"
                    : OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/typography-small" : "res://artifacts/typography");
                System.IO.Directory.CreateDirectory(_output);
                // --only=ShopMenu,ArenaMenu narrows a review to the screens being polished.
                var only = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--only="))?["--only=".Length..].Split(',');
                foreach (var scene in new[] { SceneRouter.MainMenuScene }.Concat(LiveUiReview.ActivityScenes)
                    .Where(scene => only == null || only.Contains(System.IO.Path.GetFileNameWithoutExtension(scene))))
                {
                    await Open(scene);
                    await Capture("type-" + System.IO.Path.GetFileNameWithoutExtension(scene));
                    AuditText(scene);
                    var tabs = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<HBoxContainer>().Where(x => x.IsVisibleInTree() && x.HasMeta("realm_tabs")).SelectMany(x => x.GetChildren().OfType<Button>()).Select(x => x.Text).ToArray();
                    foreach (var tab in tabs.Skip(1))
                    {
                        await Press(tab);
                        await Capture("type-" + System.IO.Path.GetFileNameWithoutExtension(scene) + "-" + tab.Replace(" ", "-"));
                        AuditText(scene + "/" + tab);
                    }
                }
                GameState.Instance.PrepareCampaignBattle();
                await Open("Battle"); await Capture("type-Battle"); AuditText("Battle");
                System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                GD.Print($"TYPOGRAPHY_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--all-menus"))
            {
                foreach (var scene in LiveUiReview.ActivityScenes)
                {
                    await Open(scene);
                    var activity = LiveUiReview.AssertDestination(GetTree(), scene);
                    var refresh = activity.GetType().GetMethod("RefreshUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    refresh?.Invoke(activity, null);
                    refresh?.Invoke(activity, null);
                    // Rebuilt rows receive the live modal's styles on its next layout pass.
                    await Wait(0.3);
                    AuditText(scene);
                    await Capture("audit-" + System.IO.Path.GetFileNameWithoutExtension(scene));
                }
                GD.Print($"UI_REVIEW_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--playthrough"))
            {
                await CheckModeIsolation();
                GameState.Instance.PrepareCampaignBattle();
                await Open("LoadoutMenu"); await Press("Deploy");
                Engine.TimeScale = 3;
                // Earlier checks may already have rated stage 1, so play until the result board appears.
                RoyalResult Board() => Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<RoyalResult>().FirstOrDefault(x => x.IsVisibleInTree());
                for (var tick = 0; tick < 150 && Board() == null; tick++)
                {
                    var enemy = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Unit>().Where(x => x.Team == Team.Enemy && !x.IsDead).OrderBy(x => x.Position.X).FirstOrDefault();
                    var y = enemy?.Position.Y ?? 380;
                    Send(new InputEventKey { Keycode = tick % 4 == 0 ? Key.Key2 : Key.Key1, Pressed = true });
                    var pos = new Vector2(350, Mathf.Clamp(y, 230, 550));
                    Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = pos, GlobalPosition = pos });
                    Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = pos, GlobalPosition = pos });
                    Send(new InputEventKey { Keycode = Key.C, Pressed = true });
                    if (tick % 12 == 0) Send(new InputEventKey { Keycode = Key.Z, Pressed = true });
                    if (tick == 15) await Capture("20-battle-in-progress");
                    await Wait(1.5);
                }
                Engine.TimeScale = 1;
                await Wait(0.5); // Capture the readable end state after its entrance animation.
                AuditText("Battle victory");
                Check(Board() is { Won: true } && GameState.Instance.GetStageStars(1) > 0, "Real-input campaign battle reaches victory");
                Check(SaveSystem.Instance.TryLoad(out var result) && result.StageStars.Length > 0 && result.StageStars[0] > 0, "Victory stars persist to disk");
                await Capture("21-victory");
                await PressHint("Back to map"); await Wait(.5);
                Check(GetTree().CurrentScene is MapMenu, "Victory returns to the campaign map");
                await Capture("23-map-after-victory");
                GD.Print($"UI_REVIEW_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1); return;
            }
            await Open("MainMenu");
            Check(!Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<ScrollContainer>().Any(x => x.IsVisibleInTree()), "Home has no visible scrolling regions");
            await Capture("01-camp");
            await PressHint("More");
            await Press("Caravan"); await Capture("02-camp-caravan");
            await Press("Community"); await Capture("03-camp-community");
            // Tapping a battle site travels there and opens its preparation as a modal over the map.
            await Open("MapMenu"); await ChooseAdventureSite("leader-1"); await Capture("04-map");
            await FinishTravel();
            Check(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<LoadoutMenu>().Any(x => x.IsVisibleInTree()), "Map opens preparation");
            await Capture("06-loadout");
            var viewUnit = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().First(b => b.IsVisibleInTree() && (b.AccessibilityName ?? "").StartsWith("View "));
            viewUnit.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.6);
            Check(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<ModelShowcase>().Any(), "Unit details open on demand");
            await Capture("06b-unit-inspector");
            await PressHint("Close details");
            await Open("ShopMenu"); await Capture("07-armory");
            var oldGold = GameState.Instance.Gold;
            var oldLevel = GameState.Instance.GetUnitLevel(GameData.PlayerBrawlerId);
            var upgradeCost = GameState.Instance.GetUnitUpgradeCost(GameData.PlayerBrawlerId);
            await Press("Upgrade");
            Check(GameState.Instance.Gold == oldGold - upgradeCost && GameState.Instance.GetUnitLevel(GameData.PlayerBrawlerId) == oldLevel + 1, "Armory upgrade charges once and increases level");
            if (GameState.Instance.ActiveDeckUnitIds.Count == 1)
            {
                await Press("Unequip");
                Check(GameState.Instance.IsUnitInActiveDeck(GameData.PlayerBrawlerId), "The last starter unit remains equipped");
                // The equip round trip needs a second unit now that new games
                // start with only a swordsman. Keep this fixture in the test save.
                var squadFixture = GameState.Instance.BuildSaveData();
                squadFixture.OwnedPlayerUnitIds = squadFixture.OwnedPlayerUnitIds.Append(GameData.PlayerShooterId).Distinct().ToArray();
                squadFixture.ActiveDeckUnitIds = squadFixture.ActiveDeckUnitIds.Append(GameData.PlayerShooterId).Distinct().ToArray();
                typeof(GameState).GetMethod("ApplySavedData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(GameState.Instance, new object[] { squadFixture });
                await Open("ShopMenu");
            }
            await Press("Unequip"); Check(!GameState.Instance.IsUnitInActiveDeck(GameData.PlayerBrawlerId), "Unit can leave squad");
            await Press("Equip"); Check(GameState.Instance.IsUnitInActiveDeck(GameData.PlayerBrawlerId), "Unit can return to squad");
            await Press("Spells"); await Capture("08-rites");
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
            // Battles open at zero courage, so let it build until the first card is affordable.
            var hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var battleScene = (BattleController)GetTree().CurrentScene;
            var firstSlot = ((System.Collections.IList)typeof(BattleController).GetField("_deploySlots", hidden)!.GetValue(battleScene)!)[0]!;
            var firstCard = (UnitDefinition)firstSlot.GetType().GetProperty("Definition")!.GetValue(firstSlot)!;
            var courage = typeof(BattleController).GetField("_courage", hidden)!;
            for (var i = 0; i < 80 && (float)courage.GetValue(battleScene)! < firstCard.Cost; i++) await Wait(0.25);
            Send(new InputEventKey { Keycode = Key.Key1, Pressed = true });
            await Wait(0.1);
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(350, 380), GlobalPosition = new Vector2(350, 380) });
            Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new Vector2(350, 380), GlobalPosition = new Vector2(350, 380) });
            await Wait(2);
            Check(Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Unit>().Any(x => x.Team == Team.Player && !x.IsDead), "Unit card deploys a live ally");
            await Capture("14-deployed");
            await PressHint("Battle menu [Escape]"); await Capture("15-paused");
            await Press("Game settings"); await Capture("16-battle-settings");
            ((BattleController)GetTree().CurrentScene).CloseBattleSettings();
            await Press("Resume");
            await PressHint("Battle menu [Escape]"); await Press("Quit battle"); await Capture("17-retreat");
            Check(SaveSystem.Instance.TryLoad(out _), "Isolated save reloads");
            GD.Print($"UI_REVIEW_RESULT: {_failures} failures");
            QuitAfterAudio(_failures == 0 ? 0 : 1);
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); QuitAfterAudio(1); }
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
            var cards = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Where(x => x.IsVisibleInTree() && names.Contains(x.AccessibilityName)).Select(x => x.AccessibilityName).ToArray();
            foreach (var name in cards)
            {
                var card = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().First(x => x.IsVisibleInTree() && x.AccessibilityName == name);
                card.EmitSignal(BaseButton.SignalName.Pressed); await Wait(0.1);
                AuditText("Armory / " + name);
            }
            await Capture("type-armory-" + cards.FirstOrDefault()?.Replace(" ", "-"));
            var next = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && x.TooltipText == "Next roster page" && !x.Disabled);
            if (next == null) break;
            next.EmitSignal(BaseButton.SignalName.Pressed); await Wait(0.1);
        }
    }

    private readonly List<object> _textAudit = new();
    private void AuditText(string screen)
    {
        if (OS.GetCmdlineUserArgs().Contains("--scroll-spacing")) AuditScrollSpacing(screen);
        AuditModalBounds(screen);
        AuditFrameClearance(screen);
        var viewport = GetViewport().GetVisibleRect();
        var panels = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<PanelContainer>().Where(x => x.IsVisibleInTree() && x.GetParent() == GetTree().CurrentScene).ToArray();
        foreach (var panel in panels)
        {
            if (!viewport.Grow(2).Encloses(panel.GetGlobalRect()))
            { GD.Print($"PANEL_OUTSIDE_VIEWPORT {screen}: {panel.GetPath()} {panel.GetGlobalRect()}"); _failures++; }
        }
        for (var i = 0; i < panels.Length; i++) for (var j = i + 1; j < panels.Length; j++)
        {
            var overlap = panels[i].GetGlobalRect().Intersection(panels[j].GetGlobalRect());
            if (overlap.Size.X > 4 && overlap.Size.Y > 4)
            { GD.Print($"PANEL_OVERLAP {screen}: {panels[i].GetPath()} / {panels[j].GetPath()} {overlap}"); _failures++; }
        }
        foreach (var button in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().Where(x => x.IsVisibleInTree() && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var box = button.GetThemeStylebox("normal");
            var available = button.Size.X - box.GetContentMargin(Side.Left) - box.GetContentMargin(Side.Right);
            if (button.Icon != null && button is not RealmButton { VerticalContent: true }) available -= button.GetThemeConstant("icon_max_width") + button.GetThemeConstant("h_separation");
            var font = button.GetThemeFont("font"); var size = button.GetThemeFontSize("font_size");
            var widest = button.Text.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
            if (button.AutowrapMode == TextServer.AutowrapMode.Off && widest > available + 3)
            { GD.Print($"BUTTON_TEXT_ISSUE {screen}: {button.Text.Replace("\n", " ")} ({widest:0} > {available:0})"); _failures++; }
        }
        foreach (var control in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Control>().Where(x => x.IsVisibleInTree() && !x.HasMeta("realm_tooltip") && (x is Label || x is Button)))
        {
            for (var parent = control.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is not ScrollContainer scroll) continue;
                if (scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled) break; // This content intentionally extends beyond its nearest horizontal scroller.
                var rect = control.GetGlobalRect(); var clip = scroll.GetGlobalRect().Grow(3);
                if (scroll.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled && (rect.Position.X < clip.Position.X || rect.End.X > clip.End.X))
                { GD.Print($"SCROLL_TEXT_CLIP {screen}: {control.GetPath()} {rect} outside {clip}"); _failures++; break; }
            }
        }
        var homeDock = GetTree().CurrentScene.GetNodeOrNull<PanelContainer>("HomeHud/HomeTabs");
        foreach (var label in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Label>().Where(x => x.IsVisibleInTree() && !x.HasMeta("realm_tooltip") && !string.IsNullOrWhiteSpace(x.Text)))
        {
            var font = label.GetThemeFontSize("font_size");
            var issues = new List<string>();
            // Concept screens use the approved concepts' type sizes (captions down to 12 px).
            var minimumFont = InRoyalScreen(label) ? 11 : homeDock != null && homeDock.IsAncestorOf(label) ? 14 : 18;
            for (var parent = label.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is Button) { minimumFont = Math.Min(minimumFont, RealmUi.ButtonFontSize); break; }
            if (font < minimumFont) issues.Add("small text");
            if (label.GetVisibleLineCount() < label.GetLineCount()) issues.Add("hidden lines");
            var hiddenLines = label.GetVisibleLineCount() < label.GetLineCount();
            var limitedWidth = label.ClipText && label.AutowrapMode == TextServer.AutowrapMode.Off
                && label.Text.Split('\n').Any(line => label.GetThemeFont("font").GetStringSize(line, fontSize: font).X > label.Size.X + 3);
            if (limitedWidth || (label.MaxLinesVisible > 0 && hiddenLines)) issues.Add("text limit");
            for (var parent = label.GetParent(); parent != null; parent = parent.GetParent())
            {
                if (parent is Button owner && !owner.GetGlobalRect().Grow(2).Encloses(label.GetGlobalRect()))
                { issues.Add("outside button"); break; }
            }
            if (!Clipped(label) && !viewport.Grow(2).Encloses(label.GetGlobalRect())) issues.Add($"outside viewport {label.GetGlobalRect()}");
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

    private static bool InRoyalScreen(Node node)
    {
        for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent.HasMeta("royal_screen")) return true;
        return false;
    }

    private void AuditModalBounds(string screen)
    {
        if (GetTree().CurrentScene is not MapMenu { HasHomeModal: true } home) return;
        var body = home.GetNode<RealmModal>("HomeModal").Content;
        var bounds = body.GetGlobalRect().Grow(3);
        foreach (var control in Walk(body).OfType<Control>().Where(c => c.IsVisibleInTree() && c is Label or Button or ScrollContainer))
        {
            bool horizontalScroll = false, verticalScroll = false, separateCanvas = false;
            for (var parent = control.GetParent(); parent != null && parent != body; parent = parent.GetParent())
            {
                if (parent is CanvasLayer) { separateCanvas = true; break; }
                if (parent is ScrollContainer scroll)
                {
                    horizontalScroll |= scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled;
                    verticalScroll |= scroll.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled;
                }
            }
            if (separateCanvas) continue;
            var rect = control.GetGlobalRect();
            if ((!horizontalScroll && (rect.Position.X < bounds.Position.X || rect.End.X > bounds.End.X))
                || (!verticalScroll && (rect.Position.Y < bounds.Position.Y || rect.End.Y > bounds.End.Y)))
            {
                GD.Print($"MODAL_CONTENT_CLIP {screen}: {control.GetPath()} {rect} outside {bounds}");
                _failures++;
            }
        }
    }

    private void AuditScrollSpacing(string screen)
    {
        var areas = 0; var rails = 0;
        foreach (var scroll in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<ScrollContainer>().Where(area => area.IsVisibleInTree()))
        {
            areas++;
            var vertical = scroll.GetVScrollBar(); var horizontal = scroll.GetHScrollBar();
            foreach (var content in scroll.GetChildren().OfType<Control>().Where(child => child.IsVisibleInTree() && child is not ScrollBar))
            {
                // The other axis may intentionally extend beyond the viewport in card strips or trees.
                if (vertical.Visible && !horizontal.Visible)
                {
                    rails++;
                    var gap = vertical.GetGlobalRect().Position.X - content.GetGlobalRect().End.X;
                    if (gap < 10)
                    { _failures++; GD.Print($"SCROLL_SPACING_ISSUE {screen}: {scroll.GetPath()} has {gap:0.#}px beside its vertical rail"); }
                }
                if (horizontal.Visible && !vertical.Visible)
                {
                    rails++;
                    var gap = horizontal.GetGlobalRect().Position.Y - content.GetGlobalRect().End.Y;
                    if (gap < 8)
                    { _failures++; GD.Print($"SCROLL_SPACING_ISSUE {screen}: {scroll.GetPath()} has {gap:0.#}px above its horizontal rail"); }
                }
            }
        }
        GD.Print($"SCROLL_SPACING_REVIEW {screen}: {areas} areas, {rails} visible rails");
    }

    private void Send(InputEvent input) => GetViewport().PushInput(input, true);

    private async Task Open(string scene)
    {
        await LiveUiReview.Open(this, scene);
        await Wait(0.4); // Let the modal fade-in finish before captures.
    }
    private async Task Press(string text)
    {
        var button = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && !x.Disabled && x.Text.StartsWith(text, StringComparison.Ordinal));
        if (button == null) throw new InvalidOperationException($"Missing active button: {text}");
        if (button.ToggleMode) button.ButtonPressed = true;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(0.8);
    }
    private async Task PressHint(string hint)
    {
        var button = Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Button>().FirstOrDefault(x => x.IsVisibleInTree() && (x.TooltipText == hint || x.AccessibilityName == hint));
        if (button == null) throw new InvalidOperationException($"Missing icon button: {hint}");
        if (button.ToggleMode) button.ButtonPressed = true;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(0.5);
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (var item in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<CanvasItem>()) item.QueueRedraw();
        RenderingServer.ForceDraw(); // Hidden macOS windows may stop emitting automatic draw signals.
        using var pixels = GetViewport().GetTexture().GetImage();
        if (pixels.SavePng($"{_output}/{name}.png") != Error.Ok)
            throw new System.IO.IOException("Unable to save review capture " + name);
        var viewport = GetViewport().GetVisibleRect();
        foreach (var control in Walk(LiveUiReview.ActiveRoot(GetTree())).OfType<Control>())
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
            if (p is ScrollContainer || p is Control { ClipContents: true }) return true;
        return false;
    }
    private void Check(bool condition, string label)
    { GD.Print($"{(condition ? "PASS" : "FAIL")} {label}"); if (!condition) _failures++; }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    // Quitting mid-track leaves the music's playback referenced and the engine reports it as leaked.
    private async void QuitAfterAudio(int code)
    {
        await LiveUiReview.StopAudio(this);
        GetTree().Quit(code);
    }
    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
