using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewTileMap(bool includeHome = false)
    {
        _output = ProjectSettings.GlobalizePath(OS.GetCmdlineUserArgs().Contains("--small-window") ? "res://artifacts/tile-map/small" : "res://artifacts/tile-map/desktop");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
        var restore = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void Restore(GameSaveData save) => restore.Invoke(state, new object[] { save });
        var initial = state.BuildSaveData();
        foreach (var map in GameData.Stages.Select(stage => stage.MapId).Distinct())
        {
            var tiles = AdventureTileCatalog.ForMap(map);
            Check(tiles.All(tile => AdventureAtlasLandscape.Outline(tile).Length > 8 && Geometry2D.TriangulatePolygon(AdventureAtlasLandscape.Outline(tile)).Length > 0), map + " has valid curved terrain regions");
            Check(tiles.All(tile => AdventureAtlasLandscape.Contains(tile, tile.Point) && AdventureTileCatalog.At(map, tile.Point)?.Id == tile.Id), map + " terrain hit tests agree with every point of interest");
            Check(tiles.All(tile => !AdventureAtlasLandscape.IsWater(map, tile.Point)), map + " keeps its points of interest on dry land");
            foreach (var bad in tiles.Where(tile => !Geometry2D.IsPointInPolygon(tile.Point, AdventureAtlasLandscape.Coast(map)) || !AdventureAtlasLandscape.Land(tile).Any(polygon => Geometry2D.IsPointInPolygon(tile.Point, polygon))))
                GD.Print($"ATLAS_SITE_DIAGNOSTIC {map}/{bad.Id} coast={Geometry2D.IsPointInPolygon(bad.Point, AdventureAtlasLandscape.Coast(map))} water={AdventureAtlasLandscape.IsWater(map,bad.Point)} outline={AdventureAtlasLandscape.Contains(bad,bad.Point)} land={AdventureAtlasLandscape.Land(bad).Length}");
            Check(tiles.All(tile => Geometry2D.IsPointInPolygon(tile.Point, AdventureAtlasLandscape.Coast(map)) && AdventureAtlasLandscape.Land(tile).Any(polygon => Geometry2D.IsPointInPolygon(tile.Point, polygon))), map + " coastlines and riverbank boundaries retain every playable site");
            Check(tiles.Select(tile => AdventureAtlasLandscape.Outline(tile).Length).Distinct().Count() > 2, map + " varies its terrain shapes instead of drawing a square lattice");
            Check(tiles.Where(tile => tile.HasInterest).Select(tile => (tile.Column, tile.Row)).Distinct().Count() == tiles.Count(tile => tile.HasInterest), map + " has one point of interest per tile");
            Check(tiles.Count(tile => tile.Site?.Kind == AdventureSiteKind.Leader) == GameData.GetStagesForMap(map).Count && tiles.Count(tile => tile.Discovery != null) == AdventureDiscoveryCatalog.ForMap(map).Count, map + " preserves every stage and discovery");
            Check(tiles.All(tile => tile.Site?.Kind != AdventureSiteKind.Watchtower), map + " has no scout tower destinations");
            var start = AdventureTileCatalog.Starting(map);
            // The first stage is already discovered, so the ground around it is charted and shows no food prices.
            var startingGround = AdventureTileCatalog.Neighbors(start).Append(start).ToArray();
            Check(start.Site.Stage == GameData.GetStagesForMap(map).Min(stage => stage.StageNumber)
                && state.GetAdventureCaravanTile(map).Id == start.Id && tiles.Where(state.IsAdventureTileOpened).Select(tile => tile.Id).ToHashSet().SetEquals(startingGround.Select(tile => tile.Id))
                && tiles.Count(state.IsAdventureTileRevealed) == startingGround.SelectMany(AdventureTileCatalog.Neighbors).Concat(startingGround).Select(tile => tile.Id).Distinct().Count(),
                map + " begins on its first stage with the land around it charted, and only the land beyond on the frontier");
            var reached = new[] { start.Id }.ToHashSet(); var queue = new System.Collections.Generic.Queue<AdventureTile>(new[] { start });
            while (queue.Count > 0)
                foreach (var near in AdventureTileCatalog.Neighbors(queue.Dequeue()))
                    if (reached.Add(near.Id)) queue.Enqueue(near);
            Check(tiles.All(tile => reached.Contains(tile.Id)), map + " can be explored tile by tile from its first stage to every tile");
            Check(tiles.Count(tile => tile.Discovery != null) == 28 && tiles.Count(tile => !tile.HasInterest) >= 190
                && tiles.Where(tile => tile.Discovery != null).Select(tile => tile.Discovery.Kind).Distinct().Count() == Enum.GetValues<AdventureDiscoveryKind>().Length,
                map + " spreads twenty-eight finds of every kind among mostly plain ground");
        }
        if (OS.GetCmdlineUserArgs().Contains("--atlas-geometry"))
        {
            GD.Print($"ATLAS_GEOMETRY_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1); return;
        }
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        var canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var tilesCity = AdventureTileCatalog.ForMap("city");
        var leader = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && tile.Site.Stage == 1);
        var supply = tilesCity.Single(tile => tile.Id == "supply-1");
        var startingCount = AdventureTileCatalog.Neighbors(leader).Count + 1;
        Check(state.IsAdventureTileOpened(leader) && !state.IsAdventureTileOpened(supply) && tilesCity.Count(state.IsAdventureTileOpened) == startingCount,
            "A fresh map charts only the first stage and the land around it, with its supplies still unopened");
        Check(!state.IsAdventureTileRevealed(tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage))), "Distant stages remain under the tile veil");
        state.ReloadFromDisk();
        Check(tilesCity.Count(state.IsAdventureTileOpened) == startingCount && state.GetAdventureCaravanTile("city").Id == leader.Id,
            "Reloading an untouched campaign keeps only its first stage and the land around it charted");
        Check(!menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Fresh map keeps details closed");
        Check(!Walk(menu).OfType<Button>().Any(button => button.Text == "Map guide" || button.AccessibilityName == "Map guide"), "The map guide no longer occupies the home map");
        await Capture("01-fresh-map"); AuditText("Tile map / fresh");
        var food = state.Food; var knowledge = state.AdventureKnowledgeRevision; var position = state.GetAdventureCaravanTile("city").Id;
        var click = canvas.GlobalPosition + new Vector2(canvas.Size.X * .8f, canvas.Size.Y * .55f);
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
        Send(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Relative = new Vector2(-200, 55), Position = click + new Vector2(-200,55), GlobalPosition = click + new Vector2(-200,55) });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click + new Vector2(-200,55), GlobalPosition = click + new Vector2(-200,55) });
        await Wait(.1);
        Check(state.Food == food && state.AdventureKnowledgeRevision == knowledge && state.GetAdventureCaravanTile("city").Id == position, "Panning never spends food, moves the caravan or reveals tiles");
        canvas.TravelToTile(tilesCity.First(tile => !tile.HasInterest && !state.IsAdventureTileRevealed(tile)), null); await Wait(.1);
        Check(!canvas.IsTravelling && state.GetAdventureCaravanTile("city").Id == position, "Hidden ground cannot be travelled to");
        canvas.FocusCurrentTile();
        var originalZoom = canvas.Zoom;
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = click, GlobalPosition = click });
        await Wait(.1); Check(canvas.Zoom > originalZoom, "Scrolling zooms the map in");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Position = click, GlobalPosition = click });
        await Wait(.1);
        Check(Math.Abs(canvas.Zoom - originalZoom) < .01f, "Map zoom returns to its original scale");
        await ReviewMapTouch(menu, canvas, leader);
        canvas.ChangeZoom(originalZoom / canvas.Zoom); canvas.FocusCurrentTile();
        canvas.ChangeZoom(1.2f); canvas.FocusSite(leader.Id); await Wait(.1);
        var building = canvas.GlobalPosition + leader.Point * canvas.Zoom + canvas.MapOffset - new Vector2(0, 24) * canvas.Zoom;
        Send(new InputEventMouseMotion { Position = building, GlobalPosition = building });
        await Wait(.1); await Capture("01-stage-hover");
        Send(new InputEventMouseMotion { Position = canvas.GlobalPosition + new Vector2(12, 12), GlobalPosition = canvas.GlobalPosition + new Vector2(12, 12) });
        await Wait(.1); await Capture("01-stage-hover-out");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = building, GlobalPosition = building });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = building, GlobalPosition = building });
        await FinishTravel(); await Wait(.2);
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene && state.SelectedStage == leader.Site.Stage
            && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Clicking the painted fort after zooming and panning opens its matching stage's preparation");
        menu.CloseHomeModal(); await Wait(.1); canvas.ChangeZoom(originalZoom / canvas.Zoom); canvas.FocusCurrentTile();
        var far = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage));
        Check(!state.TryReachAdventureTile(far, out _) && state.Food == food, "Unopened destinations reject travel without charging");
        var openedBeforeVisit = tilesCity.Count(state.IsAdventureTileRevealed);
        var beforeStageTravel = state.Food;
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel, "The caravan starts on the first stage without a travel charge");
        Check(state.TryCollectAdventureTile(leader, out _) && tilesCity.Count(state.IsAdventureTileRevealed) == openedBeforeVisit, "Preparing a stage does not open surrounding tiles");
        state.ApplyDefeat(1); state.ApplyRetreat(1);
        Check(tilesCity.Count(state.IsAdventureTileRevealed) == openedBeforeVisit, "Defeat and retreat do not open surrounding tiles");
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel, "Returning to a reached stage is free");
        var entryFood = state.Food; var entryCost = state.GetStageEntryFoodCost(1);
        Check(state.TrySpendStageEntryFood(1, out _) && state.Food == entryFood - entryCost, "Stage entry charges its existing food cost");
        state.PrepareCampaignBattle(); state.ApplyVictory(1, 0, 0, 3);
        var second = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && tile.Site.Stage == 2);
        Check(AdventureTileCatalog.Neighbors(leader).All(state.IsAdventureTileComplete) && !state.IsAdventureTileRevealed(second)
            && tilesCity.Count(state.IsAdventureTileRevealed) == openedBeforeVisit && state.GetStageStars(1) == 3,
            "The first victory keeps its stars; the land around the stage was already charted and the next stage still lies beyond it");
        // Chart a road out to the stage's cache and the first find, as a player would.
        foreach (var target in new[] { supply, AdventureTileCatalog.Find("city", AdventureDiscoveryCatalog.ForMap("city")[0].Id) })
            foreach (var step in GameState.AdventureTilePath(leader, target).Skip(1).SkipLast(1)) state.TryOpenAdventureTile(step, out _);
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
        canvas.ShowMap("city", leader.Id); canvas.FocusSite(leader.Id); await Wait(.2);
        await Capture("03-victory-map"); AuditText("Tile map / victory");
        var cacheGold = state.Gold; food = state.Food; knowledge = state.AdventureKnowledgeRevision;
        // Native resource selection traverses the real marker and opens the tile for its food cost.
        canvas.FocusSite(supply.Id); await Wait(.1);
        var supplyToken = Walk(menu).OfType<AdventureMapToken>().Single(token => token.Site.Id == supply.Id);
        var native = supplyToken.GlobalPosition + supplyToken.Size * supplyToken.Scale * .5f;
        Send(new InputEventMouseMotion { Position = native, GlobalPosition = native });
        await Wait(.1); await Capture("02-resource-hover");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = native, GlobalPosition = native });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = native, GlobalPosition = native });
        await FinishTravel();
        Check(state.HasVisitedAdventureSite(supply.Id) && state.Gold == cacheGold + supply.Site.GoldReward && state.Food == food - GameState.AdventureTileFoodCost, "Native cache tap spends 1 food and grants the advertised gold");
        Check(state.AdventureKnowledgeRevision > knowledge && !supplyToken.Visible && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Gathering opens surrounding tiles and removes the cache marker");
        var gold = state.Gold;
        Check(!state.TryCollectAdventureTile(supply, out _) || state.Gold == gold, "Collected caches cannot pay twice");
        await Capture("02-resource-opens-tiles");
        var discovered = tilesCity.First(tile => tile.Discovery != null && state.IsAdventureTileRevealed(tile) && !state.IsAdventureTileComplete(tile));
        var rewardFood = state.Food;
        canvas.TravelToTile(discovered, () => state.TryCollectAdventureTile(discovered, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(discovered.Id) && state.Food == rewardFood - GameState.AdventureTileFoodCost + (discovered.Discovery.Kind == AdventureDiscoveryKind.Food ? discovered.Discovery.Amount : 0), "Discovery spends 1 food and grants its real reward");
        Check(!state.TryCollectAdventureTile(discovered, out _), "Discovery claims reject duplicates");
        Check(state.TryReachAdventureTile(leader, out _), "The completed starting stage remains reachable after resource collection");
        var saved = state.BuildSaveData();
        state.ReloadFromDisk();
        Check(state.GetAdventureCaravanTile("city").Id == leader.Id && state.IsAdventureTileRevealed(leader) && state.HasClaimedAdventureDiscovery(discovered.Id), "A full disk reload retains tile progression and claims");
        Restore(saved);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && AdventureTileCatalog.Neighbors(leader).All(state.IsAdventureTileRevealed) && state.GetAdventureCaravanTile("city").Id == leader.Id, "Save reload retains reached tiles, claims, caravan destination and opened neighbors");
        var survey = tilesCity.First(tile => tile.Discovery?.Kind == AdventureDiscoveryKind.Survey);
        var surveyFixture = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(initial))!;
        var surveyApproach = AdventureTileCatalog.Neighbors(survey).First(tile => !tile.HasInterest);
        surveyFixture.Food = 100; surveyFixture.AdventureOpenTiles = new[] { surveyApproach.Id };
        Restore(surveyFixture);
        var surveyExpected = tilesCity.Where(state.IsAdventureTileOpened).Select(tile => tile.Id).Append(survey.Id)
            .Concat(AdventureTileCatalog.Neighbors(survey).Where(tile => !tile.HasInterest).Select(tile => tile.Id)).ToHashSet();
        Check(state.TryReachAdventureTile(survey, out _) && state.TryCollectAdventureTile(survey, out _)
            && surveyExpected.SetEquals(tilesCity.Where(state.IsAdventureTileOpened).Select(tile => tile.Id)), "A survey chart charts only the plain ground touching it");
        state.ReloadFromDisk();
        Check(surveyExpected.SetEquals(tilesCity.Where(state.IsAdventureTileOpened).Select(tile => tile.Id)), "Reloading a collected chart does not chart any further");
        Restore(saved);
        var oldShrine = state.BuildSaveData(); oldShrine.Version = 44;
        oldShrine.VisitedAdventureSites = oldShrine.VisitedAdventureSites.Append("landmark-2").ToArray();
        oldShrine.AdventureOpenTiles = null; oldShrine.AdventureReachedTiles = null; oldShrine.AdventureCaravanTiles = null;
        Restore(oldShrine);
        Check(state.GetAdventureCaravanTile("city").Id == leader.Id && AdventureTileCatalog.Find("city", "landmark-2") == null
            && !state.CanVisitAdventureSite("landmark-2"),
            "Legacy shrine saves return to the first stage without restoring a shrine or its courage bonus");
        var oldUntouched = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(initial))!;
        oldUntouched.Version = 44;
        oldUntouched.AdventureOpenTiles = null; oldUntouched.AdventureReachedTiles = null; oldUntouched.AdventureCaravanTiles = null;
        Restore(oldUntouched);
        Check(state.IsAdventureZoneUnlocked("city") && AssetCoverageCatalog.RouteIds.Skip(1).All(map => !state.IsAdventureZoneUnlocked(map)),
            "Legacy starting camps do not unlock unplayed zones after becoming ordinary terrain");
        Restore(saved);
        var unvisited = tilesCity.First(tile => tile.Discovery != null && !state.IsAdventureTileRevealed(tile));
        var withFind = state.BuildSaveData();
        withFind.AdventureOpenTiles = withFind.AdventureOpenTiles.Append(AdventureTileCatalog.Neighbors(unvisited).First(tile => !tile.HasInterest).Id).ToArray();
        var noFood = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(withFind))!;
        noFood.Food = 0; noFood.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Restore(noFood);
        var caravan = state.GetAdventureCaravanTile("city").Id;
        Check(!state.TryReachAdventureTile(unvisited, out var costMessage) && costMessage.Contains("1 food") && !state.TryCollectAdventureTile(unvisited, out _)
            && !state.HasClaimedAdventureDiscovery(unvisited.Id) && state.Food == 0 && state.GetAdventureCaravanTile("city").Id == caravan,
            "Without 1 food a frontier resource tile explains its cost and stays closed, charging nothing");
        Restore(withFind);
        var immediateFood = state.Food - GameState.AdventureTileFoodCost + (unvisited.Discovery.Kind == AdventureDiscoveryKind.Food ? unvisited.Discovery.Amount : 0);
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _));
        Check(!canvas.IsTravelling && state.Food == immediateFood && state.HasClaimedAdventureDiscovery(unvisited.Id)
            && state.GetAdventureCaravanTile("city").Id == unvisited.Id, "Tile travel immediately opens the tile for 1 food and collects without a cart animation");
        await Open("MainMenu");
        Check(state.Food == immediateFood && state.HasClaimedAdventureDiscovery(unvisited.Id)
            && state.GetAdventureCaravanTile("city").Id == unvisited.Id, "Reopening the map retains the immediate destination and collection");
        menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        Restore(withFind);
        var reduced = state.BuildSaveData(); reduced.ReducedMotion = true; Restore(reduced);
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(unvisited.Id), "Reduced motion reaches and collects the same tile without animation");
        Restore(saved);
        var legacy = state.BuildSaveData(); legacy.Version = 44; legacy.AdventureOpenTiles = null; legacy.AdventureReachedTiles = null; legacy.AdventureCaravanTiles = null;
        Restore(legacy);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && state.IsAdventureTileRevealed(leader) && state.GetStageStars(1) == 3, "Version 44 saves migrate site claims, resource claims and cleared-stage reveals");
        Restore(saved);
        var otherStage = tilesCity.Where(tile => tile.Site?.Kind == AdventureSiteKind.Leader && !state.HasReachedAdventureTile(tile.Id)).OrderBy(tile => tile.Site.Stage).First();
        var tight = state.BuildSaveData(); tight.AdventureOpenTiles = tight.AdventureOpenTiles.Append(otherStage.Id).ToArray();
        tight.Food = state.GetStageEntryFoodCost(otherStage.Site.Stage); Restore(tight);
        Check(state.TryReachAdventureTile(otherStage, out _) && state.Food == tight.Food
            && state.TrySpendStageEntryFood(otherStage.Site.Stage, out _) && state.Food == 0
            && !state.CanStartCampaignBattle(otherStage.Site.Stage, out _), "A new stage needs only its entry rations, spent once when starting the battle");
        Restore(saved);
        var charted = state.BuildSaveData(); charted.Food = 100;
        charted.AdventureOpenTiles = GameData.Stages.Select(stage => stage.MapId).Distinct().SelectMany(AdventureTileCatalog.ForMap).Select(tile => tile.Id).ToArray();
        Restore(charted);
        Check(!state.TryReachAdventureTile(far, out _) && !state.IsAdventureZoneUnlocked("harbor"), "Open tiles do not bypass boss or zone victory gates");
        state.PrepareCampaignBattle();
        foreach (var stage in GameData.GetStagesForMap("city")) state.ApplyVictory(stage.StageNumber, 0, 0, 3);
        Check(state.IsAdventureZoneUnlocked("harbor") && !state.IsAdventureZoneUnlocked("foundry"), "Defeating the boss reveals only the next zone");
        await Open("MainMenu"); menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        canvas.ChangeZoom(.63f / canvas.Zoom); canvas.FocusOverview(); await Wait(.2);
        await Capture("04-charted-kings-road");
        foreach (var map in GameData.Stages.Select(stage => stage.MapId).Distinct())
        {
            var themed = state.BuildSaveData();
            foreach (var prior in GameData.Stages.Where(stage => Array.IndexOf(AssetCoverageCatalog.RouteIds, stage.MapId) < Array.IndexOf(AssetCoverageCatalog.RouteIds, map))) themed.StageStars[prior.StageNumber - 1] = 3;
            themed.SelectedStage = GameData.GetStagesForMap(map).First().StageNumber; Restore(themed);
            await Open("MainMenu"); canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
            canvas.ChangeZoom(.63f / canvas.Zoom); canvas.FocusOverview(); await Wait(.1); await Capture("zone-" + map);
            AuditText("Tile map / " + map);
        }
        Restore(saved); await Open("MainMenu"); menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var beforeModal = canvas.MapOffset; var beforeModalFood = state.Food; var beforeModalKnowledge = state.AdventureKnowledgeRevision;
        await PressHint("Settings"); AuditText("Tile map / settings"); menu.CloseHomeModal(); await Wait(.1);
        await PressHint("Warband"); AuditText("Tile map / warband"); menu.CloseHomeModal(); await Wait(.1);
        Check(canvas.MapOffset == beforeModal && state.Food == beforeModalFood && state.AdventureKnowledgeRevision == beforeModalKnowledge, "Settings and warband overlays preserve the tile map and resources");
        canvas.FocusSite(leader.Id); await Wait(.1);
        Walk(menu).OfType<AdventureMapToken>().Single(token => token.Site.Id == leader.Id).EmitSignal(BaseButton.SignalName.Pressed);
        // A playable stage skips the details panel: the caravan travels there and its preparation opens.
        await FinishTravel(); await Wait(.3);
        Check(!menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "A playable stage opens without a details panel");
        var preparation = PreparationText();
        Check(preparation.Any(text => text.StartsWith($"{GameData.GetStage(1).RewardGold:N0} GOLD"))
            && preparation.Contains($"{state.GetStageEntryFoodCost(1)} FOOD"),
            "Battle preparation displays the configured victory reward and a single battle entry ration cost");
        AuditText("Tile map / stage preparation"); await Capture("05-stage-costs");
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene && state.SelectedStage == 1, "Stage tile launches the matching real battle preparation");
        var deployFood = state.Food;
        await Press("Deploy");
        Check(GetTree().CurrentScene is BattleController && state.Food == deployFood - state.GetStageEntryFoodCost(1), "Real deployment charges stage entry exactly once");
        await PressHint("Battle menu [Escape]"); await Press("Quit battle");
        if (includeHome)
        {
            Restore(saved); await Open("MainMenu"); menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
            await ReviewMapNotices(menu, canvas);
            await ReviewMapRewards(menu, canvas);
            await ReviewDeveloperMode(menu);
            await ReviewStorehouse(menu);
            await ReviewModalActions(menu, canvas);
            await ReviewModalLaunches();
        }
        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"TILE_MAP_REVIEW_RESULT: {_failures} failures");
        if (includeHome) GD.Print($"HOME_MAP_REVIEW_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }

    /// <summary>Touch on the zone map, sent as a phone sends it: each finger's touch events plus the mouse Godot emulates from the first.</summary>
    private async Task ReviewMapTouch(MapMenu menu, MapPathCanvas canvas, AdventureTile leader)
    {
        void Mouse(InputEvent input) { input.Device = (int)InputEvent.DeviceIdEmulation; Send(input); }
        void Down(int finger, Vector2 at)
        {
            if (finger == 0) Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true, Position = at, GlobalPosition = at });
            Send(new InputEventScreenTouch { Index = finger, Pressed = true, Position = at });
        }
        void Move(int finger, Vector2 from, Vector2 to)
        {
            if (finger == 0) Mouse(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Relative = to - from, Position = to, GlobalPosition = to });
            Send(new InputEventScreenDrag { Index = finger, Relative = to - from, Position = to });
        }
        void Up(int finger, Vector2 at)
        {
            if (finger == 0) Mouse(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
            Send(new InputEventScreenTouch { Index = finger, Pressed = false, Position = at });
        }
        var glide = typeof(MapPathCanvas).GetField("_glideVelocity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var origin = canvas.GlobalPosition;
        Check(Math.Abs(canvas.Zoom - .85f) < .01f, "The zone map opens close enough to read the landmarks around the caravan");

        // One finger: past the tap slop, the ground follows it exactly, once.
        canvas.FocusOverview(); await Wait(.1);
        var finger = origin + canvas.Size * .5f; var before = canvas.MapOffset;
        Down(0, finger); Move(0, finger, finger + new Vector2(-20, 0));
        Move(0, finger + new Vector2(-20, 0), finger + new Vector2(-140, -40)); Move(0, finger + new Vector2(-140, -40), finger + new Vector2(-260, -80));
        Up(0, finger + new Vector2(-260, -80));
        Check((canvas.MapOffset - before).IsEqualApprox(new Vector2(-240, -80)), $"A dragged finger moves the map exactly as far as it travels ({canvas.MapOffset - before})");
        await Wait(.2);
        Check((canvas.MapOffset - before).IsEqualApprox(new Vector2(-240, -80)), "A drag that stops before letting go leaves the map where it is");
        Down(0, finger); Move(0, finger, finger + new Vector2(6, 5)); Up(0, finger + new Vector2(6, 5));
        Check((canvas.MapOffset - before).IsEqualApprox(new Vector2(-240, -80)), "A tap's small wobble never nudges the map");

        // Two fingers: the map zooms with their spread about their midpoint, and pans with it.
        canvas.FocusOverview(); await Wait(.1);
        var zoom = canvas.Zoom; var mid = finger - origin; var ground = (mid - canvas.MapOffset) / zoom;
        Down(0, finger - new Vector2(80, 0)); Down(1, finger + new Vector2(80, 0));
        Move(1, finger + new Vector2(80, 0), finger + new Vector2(120, 10)); Move(0, finger - new Vector2(80, 0), finger - new Vector2(120, -10));
        Move(0, finger - new Vector2(120, -10), finger - new Vector2(100, -40)); Move(1, finger + new Vector2(120, 10), finger + new Vector2(140, 40));
        var pinchMid = finger + new Vector2(20, 40) - origin;
        Check(canvas.Zoom > zoom * 1.3f && canvas.Zoom < zoom * 1.6f, $"Spreading two fingers zooms the map in ({zoom:0.00} to {canvas.Zoom:0.00})");
        Check(((pinchMid - canvas.MapOffset) / canvas.Zoom).DistanceTo(ground) < 2, "The ground between the fingers stays between them as they pinch and move");
        Up(1, finger + new Vector2(140, 40)); var afterPinch = canvas.MapOffset;
        Move(0, finger - new Vector2(100, -40), finger - new Vector2(70, -40));
        Check((canvas.MapOffset - afterPinch).IsEqualApprox(new Vector2(30, 0)), "Lifting one finger hands the pan to the other without a jump");
        Up(0, finger - new Vector2(70, -40));
        zoom = canvas.Zoom;
        Down(0, finger - new Vector2(150, 0)); Down(1, finger + new Vector2(150, 0));
        for (var step = 1; step <= 4; step++) { Move(0, finger - new Vector2(150 - (step - 1) * 30, 0), finger - new Vector2(150 - step * 30, 0)); Move(1, finger + new Vector2(150 - (step - 1) * 30, 0), finger + new Vector2(150 - step * 30, 0)); }
        Up(0, finger - new Vector2(30, 0)); Up(1, finger + new Vector2(30, 0));
        Check(canvas.Zoom < zoom * .5f, $"Pinching two fingers together zooms the map out ({zoom:0.00} to {canvas.Zoom:0.00})");
        canvas.ChangeZoom(100); var closest = canvas.Zoom; canvas.ChangeZoom(.001f);
        Check(Math.Abs(closest - 1.4f) < .01f && canvas.Zoom > .25f && canvas.Zoom < .35f, $"Zoom runs from most of the zone ({canvas.Zoom:0.00}) to close on a landmark ({closest:0.00})");

        // A flick glides on and slows to a stop; a touch catches a glide without choosing anything.
        canvas.ChangeZoom(.85f / canvas.Zoom); canvas.FocusOverview(); await Wait(.1);
        before = canvas.MapOffset; var at = finger;
        Down(0, at);
        for (var step = 0; step < 6; step++) { Move(0, at, at + new Vector2(-24, 0)); at += new Vector2(-24, 0); await Wait(.02); }
        Up(0, at); var released = canvas.MapOffset; var speed = ((Vector2)glide.GetValue(canvas)).X;
        Check(speed < -150, $"A flick sets the map gliding on ({speed:0} px/s)");
        await Wait(.15);
        Check(canvas.MapOffset.X < released.X - 5, "The map glides on after the finger lifts");
        Down(0, finger); Up(0, finger); await Wait(.1);
        Check((Vector2)glide.GetValue(canvas) == Vector2.Zero && menu.HomeModalDestination == "" && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible,
            "A touch on a gliding map stops it without choosing anything");

        // A drag that starts on a landmark pans the map and doesn't open it; a tap on it still does.
        canvas.FocusSite(leader.Id); await Wait(.1);
        var fort = origin + leader.Point * canvas.Zoom + canvas.MapOffset - new Vector2(0, 24) * canvas.Zoom; before = canvas.MapOffset;
        Down(0, fort); Move(0, fort, fort - new Vector2(30, 0)); Move(0, fort - new Vector2(30, 0), fort - new Vector2(120, 0)); Up(0, fort - new Vector2(120, 0));
        await Wait(.2);
        Check(canvas.MapOffset != before && menu.HomeModalDestination == "", "Dragging from a landmark pans the map without opening it");
        canvas.FocusSite(leader.Id); await Wait(.1);
        Down(0, fort); Up(0, fort);
        await FinishTravel(); await Wait(.2);
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene, "Tapping a landmark opens it");
        menu.CloseHomeModal(); await Wait(.1);
    }
}
