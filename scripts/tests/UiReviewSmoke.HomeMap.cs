using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewHomeMap()
    {
        _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/home-map/small" : "res://artifacts/home-map/desktop");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress();
        state.SetAnalyticsConsent(false);
        state.SetShowHints(false);
        var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
        Check(maps.Count(state.IsAdventureZoneUnlocked) == 1 && state.IsAdventureZoneUnlocked("city"), "A fresh campaign exposes only the first zone");
        Check(!state.IsAdventureZoneUnlocked("invalid"), "Unknown zones cannot be opened");
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        var canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        Check(canvas.GetGlobalRect() == menu.GetGlobalRect(), "Home map fills the viewport");
        Check(AdventureTerrain.WorldSize.X * canvas.Zoom > canvas.Size.X * 2.5f && AdventureTerrain.WorldSize.Y * canvas.Zoom > canvas.Size.Y * 2.5f,
            "A zone spans several screens instead of opening as a full-map overview");
        Check(!menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Site details stay closed until selected");
        Check(menu.GetNode<PanelContainer>("HomeHud/HomeTabs").GetChildren().Single().GetChildCount() == 6, "Home has six bottom navigation tabs");
        var next = Walk(menu).OfType<Button>().Single(button => button.AccessibilityName == "Next zone");
        Check(next.Disabled && next.Icon == RealmUi.Icon("lock"), "Next zone stays locked behind the current boss");
        typeof(MapMenu).GetMethod("SwitchRegion", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, new object[] { "harbor" });
        Check(canvas.ActiveMapId == "city", "Locked zone switching is rejected");
        AuditText("Home / fresh");
        await Capture("01-home");

        var offsetBeforePan = canvas.MapOffset;
        var foodBeforePan = state.Food;
        var knowledgeBeforePan = state.AdventureKnowledgeRevision;
        var dragStart = canvas.GlobalPosition + new Vector2(canvas.Size.X * .65f, canvas.Size.Y * .46f);
        var dragOffset = new Vector2(-260, 80);
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = dragStart, GlobalPosition = dragStart });
        Send(new InputEventMouseMotion { Position = dragStart + dragOffset, GlobalPosition = dragStart + dragOffset, Relative = dragOffset, ButtonMask = MouseButtonMask.Left });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = dragStart + dragOffset, GlobalPosition = dragStart + dragOffset });
        await Wait(.1);
        Check(canvas.MapOffset.DistanceTo(offsetBeforePan) > 200, "Dragging pans across the larger zone");
        Check(state.Food == foodBeforePan && state.AdventureKnowledgeRevision == knowledgeBeforePan, "Panning does not travel, spend food or reveal fog");
        await Capture("01b-panned-fog");
        await PressHint("Find my caravan");
        Check(canvas.MapOffset.DistanceTo(offsetBeforePan) < 1, "Find caravan returns to the explored foothold");

        var token = Walk(menu).OfType<AdventureMapToken>().Single(button => button.Visible);
        token.EmitSignal(BaseButton.SignalName.Pressed);
        await Wait(.2);
        Check(menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "A landmark opens its floating details");
        AuditText("Home / camp details");
        await Capture("02-landmark");
        await Press("Intel");
        AuditText("Home / camp intel");
        await PressHint("Close site details");
        Check(!menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Details close to restore the clear map");
        var originalZoom = canvas.Zoom;
        await PressHint("Zoom in");
        Check(canvas.Zoom > originalZoom, "Floating map controls zoom in");
        await PressHint("Zoom out");
        Check(Math.Abs(canvas.Zoom - originalZoom) < .01f, "Floating map controls restore zoom");
        canvas.ChangeZoom(.01f);
        Check(AdventureTerrain.WorldSize.X * canvas.Zoom > canvas.Size.X * 1.5f, "Zooming out still requires panning through the zone");
        canvas.ChangeZoom(originalZoom / canvas.Zoom); canvas.FocusCaravan();
        canvas.ChangeZoom(3f); canvas.FocusCaravan();
        var neighbor = AdventureTerrain.Neighbors(AdventureTerrain.Cell(state.GetAdventureHeroPosition("city")))
            .First(cell => AdventureTerrain.Walkable("city", cell));
        var point = AdventureTerrain.Point(neighbor);
        var click = canvas.GlobalPosition + point * canvas.Zoom + canvas.MapOffset;
        var foodBefore = state.Food;
        var chartedBefore = Enumerable.Range(0, AdventureTerrain.CellCount).Count(cell => state.IsAdventureCellRevealed("city", cell));
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        await FinishTravel();
        Check(state.GetAdventureHeroPosition("city").DistanceTo(point) < 1 && state.Food == foodBefore - 1, "The floating HUD allows real map clicks and a single travel charge");
        Check(Enumerable.Range(0, AdventureTerrain.CellCount).Count(cell => state.IsAdventureCellRevealed("city", cell)) > chartedBefore, "Travel uncovers more terrain in the fog knowledge mask");
        canvas.ChangeZoom(originalZoom / canvas.Zoom); canvas.FocusCaravan();
        await Capture("02b-first-travel");
        var firstDiscovery = AdventureDiscoveryCatalog.ForMap("city").First();
        canvas.TravelToPoint(firstDiscovery.Point);
        await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(firstDiscovery.Id), "Exploration reaches and claims the first discovery");
        canvas.FocusCaravan();
        await Capture("02c-first-discovery");

        await PressHint("More");
        AuditText("Home / more adventure");
        await Capture("03-more");
        await Press("Caravan");
        AuditText("Home / more caravan");
        await Press("Community");
        AuditText("Home / more community");
        await PressHint("Close panel");
        var mapBeforeModal = canvas.MapOffset;
        await PressHint("Settings");
        Check(GetTree().CurrentScene == menu && menu.HomeModalDestination == SceneRouter.SettingsScene, "Settings opens over the same map");
        AuditText("Home / settings sound"); await Capture("07-settings");
        var slider = Walk(menu).OfType<HSlider>().Single(control => control.AccessibilityName == "Music volume"); slider.Value = 37;
        Check(state.MusicVolumePercent == 37, "The music slider saves the chosen volume");
        foreach (var tab in new[] { "Gameplay", "Online", "Account" }) { await Press(tab); AuditText("Home / settings " + tab); }
        SceneRouter.Instance.ReturnFromSettings(); await Wait(.2);
        Check(GetTree().CurrentScene == menu && !menu.HasHomeModal && canvas.MapOffset == mapBeforeModal, "Closing settings preserves the map camera");
        foreach (var entry in new[] { ("Warband", "Warband"), ("Spells", "Battle rites"), ("Upgrades", "War wagon") })
        {
            await PressHint(entry.Item1);
            Check(GetTree().CurrentScene == menu && menu.HomeModalDestination == SceneRouter.ShopScene && Walk(menu).OfType<Button>().Any(button => button.Text == entry.Item2 && button.ButtonPressed), entry.Item1 + " opens its matching overlay page");
            AuditText("Home / " + entry.Item1); await Capture("08-" + entry.Item1.ToLowerInvariant());
            menu.CloseHomeModal(); await Wait(.2);
        }
        await PressHint("Achievements"); AuditText("Home / achievements"); await Capture("09-achievements");
        Check(menu.HomeModalDestination == "achievements", "Achievements opens a dedicated reward board");
        await PressHint("Next achievement page"); AuditText("Home / achievement page 2");
        menu.CloseHomeModal(); await Wait(.2);
        await PressHint("Codex");
        Check(GetTree().CurrentScene == menu && menu.HomeModalDestination == SceneRouter.CodexScene, "Codex opens as a book over the map");
        AuditText("Home / codex"); await Capture("10-codex");
        menu.CloseHomeModal(); await PressHint("More"); await PressHint("Endless");
        Check(menu.HomeModalDestination == SceneRouter.EndlessScene, "Game modes open over the map");
        AuditText("Home / endless"); await Capture("11-endless");
        await PressHint("Back to previous panel");
        Check(menu.HomeModalDestination == "more", "A mode returns to its activity hub");
        await PressHint("Tower"); AuditText("Home / tower"); await Capture("12-tower");
        menu.CloseHomeModal(); await Wait(.2);
        Check(GetTree().CurrentScene == menu && canvas.MapOffset == mapBeforeModal, "Browsing menus preserves the map and camera");

        await ReviewModalActions(menu, canvas);

        // District stage numbers are interleaved: beating a late numbered rival
        // must not reveal every other zone through HighestUnlockedStage.
        state.PrepareCampaignBattle();
        state.ApplyVictory(GameData.GetStagesForMap("city").OrderBy(stage => stage.StageNumber).ToArray()[^2].StageNumber, 0, 0, 1);
        Check(maps.Count(state.IsAdventureZoneUnlocked) == 1, "A regular rival victory does not reveal later zones");
        foreach (var stage in GameData.GetStagesForMap("city")) state.ApplyVictory(stage.StageNumber, 0, 0, 1);
        Check(state.IsAdventureZoneUnlocked("harbor") && !state.IsAdventureZoneUnlocked("foundry"), "Defeating the zone boss reveals just the next zone");
        await Open("MainMenu");
        await PressHint("Next zone");
        canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        Check(canvas.ActiveMapId == "harbor" && Walk(GetTree().CurrentScene).OfType<AdventureMapToken>().All(button => button.Site.MapId == "harbor"), "The next zone replaces the map instead of adding a second zone");
        Check(GameData.GetStage(state.SelectedStage).MapId == "harbor", "Zone selection persists with the campaign stage");
        AuditText("Home / next zone");
        await Capture("04-next-zone");
        await Open("MainMenu");
        Check(Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single().ActiveMapId == "harbor", "Returning home restores the chosen zone");
        await PressHint("Previous zone");
        Check(Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single().ActiveMapId == "city", "Previously unlocked zones remain reachable");
        await PressHint("Next zone");
        await PressHint("Next zone"); // Disabled buttons can emit a test signal; the action must also guard.
        Check(Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single().ActiveMapId == "harbor", "The next locked zone also rejects a direct action");

        await PressHint("Previous zone");
        menu = (MapMenu)GetTree().CurrentScene;
        canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        // A charted fixture lets the review inspect the whole zone and every
        // leader panel without modifying the user's campaign or awarding rewards.
        foreach (var cell in Enumerable.Range(0, AdventureTerrain.CellCount).Where(cell => AdventureTerrain.Walkable("city", cell)))
            state.MoveAdventureHero("city", AdventureTerrain.Point(cell), false);
        state.MoveAdventureHero("city", AdventureMapCatalog.ForMap("city").First().Point, false);
        canvas.ShowMap("city", "camp-city");
        canvas.ChangeZoom(.01f);
        canvas.FocusPoint(AdventureTerrain.WorldSize * .5f);
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
        await Wait(.2);
        AuditText("Home / explored zone");
        await Capture("05-explored-zone");
        foreach (var site in AdventureMapCatalog.ForMap("city"))
        {
            typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, new object[] { site });
            await Wait(.05);
            AuditText("Home / site " + site.Id);
        }
        var zoneBoss = AdventureMapCatalog.ForMap("city").Last(site => site.Kind == AdventureSiteKind.Leader);
        typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, new object[] { zoneBoss });
        await Wait(.1);
        await Capture("06-leader-details");

        SceneRouter.Instance.GoToShop(); await Wait(.3);
        AuditText("Home / charted map modal"); await Capture("17-warband-over-map"); menu.CloseHomeModal();

        var saved = state.BuildSaveData();
        saved.StageStars = new int[state.MaxStage];
        saved.StageStars[GameData.GetStagesForMap("mire").First().StageNumber - 1] = 1;
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { saved });
        Check(state.IsAdventureZoneUnlocked("mire") && state.IsAdventureZoneUnlocked("basilica") && !state.IsAdventureZoneUnlocked("steppe"), "Existing campaigns retain played zones and the route back to them");
        saved.StageStars = new int[state.MaxStage];
        saved.VisitedAdventureSites = Array.Empty<string>();
        saved.AdventureTravelledCells.Clear();
        var harborCamp = AdventureTerrain.Cell(AdventureMapCatalog.ForMap("harbor").First().Point);
        saved.AdventureTravelledCells["harbor"] = new[] { AdventureTerrain.Neighbors(harborCamp).First(cell => AdventureTerrain.Walkable("harbor", cell)) };
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { saved });
        Check(state.IsAdventureZoneUnlocked("harbor") && !state.IsAdventureZoneUnlocked("foundry"), "A single paid exploration step preserves an existing zone");
        saved.AdventureTravelledCells["harbor"] = new[] { harborCamp };
        typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(state, new object[] { saved });
        Check(!state.IsAdventureZoneUnlocked("harbor"), "An initialized camp alone does not reveal a later zone");

        await ReviewModalLaunches();

        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"HOME_MAP_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
