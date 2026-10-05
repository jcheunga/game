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
            Check(start.Site.Stage == GameData.GetStagesForMap(map).Min(stage => stage.StageNumber) && start.Column == 1 && start.Row == 5
                && state.GetAdventureCaravanTile(map).Id == start.Id && tiles.Count(state.IsAdventureTileOpen) == 1, map + " begins on its first stage with only one visible tile");
            var reached = new[] { start.Id }.ToHashSet();
            for (var pass = 0; pass < 20; pass++)
                foreach (var tile in tiles.Where(tile => tile.HasInterest && reached.Contains(tile.Id)).ToArray())
                    foreach (var near in AdventureTileCatalog.Surrounding(tile)) reached.Add(near.Id);
            Check(tiles.Where(tile => tile.HasInterest).All(tile => reached.Contains(tile.Id)), map + " has a connected completion route to every point of interest");
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
        Check(state.IsAdventureTileOpen(leader) && !state.IsAdventureTileOpen(supply) && tilesCity.Count(state.IsAdventureTileOpen) == 1,
            "A fresh map exposes only the first stage, with surrounding supplies concealed");
        Check(!state.IsAdventureTileOpen(tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage))), "Distant stages remain under the tile veil");
        state.ReloadFromDisk();
        Check(tilesCity.Count(state.IsAdventureTileOpen) == 1 && state.GetAdventureCaravanTile("city").Id == leader.Id,
            "Reloading an untouched campaign keeps only its first stage visible");
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
        canvas.TravelToPoint(new Vector2(500, 500)); await Wait(.1);
        Check(!canvas.IsTravelling && state.GetAdventureCaravanTile("city").Id == position, "Empty ground cannot initiate movement");
        canvas.FocusCurrentTile();
        var originalZoom = canvas.Zoom;
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = click, GlobalPosition = click });
        await Wait(.1); Check(canvas.Zoom > originalZoom, "Scrolling zooms the map in");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Position = click, GlobalPosition = click });
        await Wait(.1);
        Check(Math.Abs(canvas.Zoom - originalZoom) < .01f, "Map zoom returns to its original scale");
        canvas.ChangeZoom(1.2f); canvas.FocusSite(leader.Id); await Wait(.1);
        var building = canvas.GlobalPosition + leader.Point * canvas.Zoom + canvas.MapOffset - new Vector2(0, 24) * canvas.Zoom;
        Send(new InputEventMouseMotion { Position = building, GlobalPosition = building });
        await Wait(.1); await Capture("01-stage-hover");
        Send(new InputEventMouseMotion { Position = canvas.GlobalPosition + new Vector2(12, 12), GlobalPosition = canvas.GlobalPosition + new Vector2(12, 12) });
        await Wait(.1); await Capture("01-stage-hover-out");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = building, GlobalPosition = building });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = building, GlobalPosition = building });
        await Wait(.1);
        Check(menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible && Walk(menu).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == leader.Site.Title), "Clicking the painted fort after zooming and panning opens its matching stage");
        await PressHint("Close site details"); canvas.ChangeZoom(originalZoom / canvas.Zoom); canvas.FocusCurrentTile();
        var far = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage));
        Check(!state.TryReachAdventureTile(far, out _) && state.Food == food, "Unopened destinations reject travel without charging");
        var openedBeforeVisit = tilesCity.Count(state.IsAdventureTileOpen);
        var beforeStageTravel = state.Food;
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel, "The caravan starts on the first stage without a travel charge");
        Check(state.TryCollectAdventureTile(leader, out _) && tilesCity.Count(state.IsAdventureTileOpen) == openedBeforeVisit, "Preparing a stage does not open surrounding tiles");
        state.ApplyDefeat(1); state.ApplyRetreat(1);
        Check(tilesCity.Count(state.IsAdventureTileOpen) == openedBeforeVisit, "Defeat and retreat do not open surrounding tiles");
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel, "Returning to a reached stage is free");
        var entryFood = state.Food; var entryCost = state.GetStageEntryFoodCost(1);
        Check(state.TrySpendStageEntryFood(1, out _) && state.Food == entryFood - entryCost, "Stage entry charges its existing food cost");
        state.PrepareCampaignBattle(); state.ApplyVictory(1, 0, 0, 3);
        Check(AdventureTileCatalog.Surrounding(leader).All(state.IsAdventureTileOpen) && tilesCity.Count(state.IsAdventureTileOpen) == 9
            && state.GetStageStars(1) == 3, "The first victory opens only the surrounding eight tiles and retains stars");
        typeof(MapMenu).GetMethod("RefreshUi", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
        canvas.ShowMap("city", leader.Id); canvas.FocusSite(leader.Id); await Wait(.2);
        await Capture("03-victory-map"); AuditText("Tile map / victory");
        var cacheGold = state.Gold; food = state.Food; knowledge = state.AdventureKnowledgeRevision;
        // Native resource selection traverses the real marker and collects without spending food.
        canvas.FocusSite(supply.Id); await Wait(.1);
        var supplyToken = Walk(menu).OfType<AdventureMapToken>().Single(token => token.Site.Id == supply.Id);
        var native = supplyToken.GlobalPosition + supplyToken.Size * supplyToken.Scale * .5f;
        Send(new InputEventMouseMotion { Position = native, GlobalPosition = native });
        await Wait(.1); await Capture("02-resource-hover");
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = native, GlobalPosition = native });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = native, GlobalPosition = native });
        await FinishTravel();
        Check(state.HasVisitedAdventureSite(supply.Id) && state.Gold == cacheGold + supply.Site.GoldReward && state.Food == food, "Native cache tap grants the advertised gold without spending food");
        Check(state.AdventureKnowledgeRevision > knowledge && !supplyToken.Visible && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Gathering opens surrounding tiles and removes the cache marker");
        var gold = state.Gold;
        Check(!state.TryCollectAdventureTile(supply, out _) || state.Gold == gold, "Collected caches cannot pay twice");
        await Capture("02-resource-opens-tiles");
        var discovered = tilesCity.First(tile => tile.Discovery != null && state.IsAdventureTileOpen(tile) && !state.IsAdventureTileComplete(tile));
        var rewardFood = state.Food;
        canvas.TravelToTile(discovered, () => state.TryCollectAdventureTile(discovered, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(discovered.Id) && state.Food == rewardFood + (discovered.Discovery.Kind == AdventureDiscoveryKind.Food ? discovered.Discovery.Amount : 0), "Discovery grants its real reward without a destination charge");
        Check(!state.TryCollectAdventureTile(discovered, out _), "Discovery claims reject duplicates");
        Check(state.TryReachAdventureTile(leader, out _), "The completed starting stage remains reachable after resource collection");
        var saved = state.BuildSaveData();
        state.ReloadFromDisk();
        Check(state.GetAdventureCaravanTile("city").Id == leader.Id && state.IsAdventureTileOpen(leader) && state.HasClaimedAdventureDiscovery(discovered.Id), "A full disk reload retains tile progression and claims");
        Restore(saved);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && AdventureTileCatalog.Surrounding(leader).All(state.IsAdventureTileOpen) && state.GetAdventureCaravanTile("city").Id == leader.Id, "Save reload retains reached tiles, claims, caravan destination and opened neighbors");
        foreach (var retiredId in new[] { "landmark-1", "camp-city", "landmark-2" })
        {
            var replacement = tilesCity.FirstOrDefault(tile => tile.RetiredSiteId == retiredId);
            var retired = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(saved))!;
            retired.VisitedAdventureSites = retired.VisitedAdventureSites.Append(retiredId).ToArray();
            retired.AdventureOpenTiles = retired.AdventureOpenTiles.Where(id => id != replacement?.Id).Append(retiredId).ToArray();
            retired.AdventureReachedTiles = retired.AdventureReachedTiles.Append(retiredId).ToArray();
            retired.AdventureCaravanTiles["city"] = retiredId;
            Restore(retired);
            Check(state.GetAdventureCaravanTile("city").Id == leader.Id && AdventureTileCatalog.Find("city", retiredId) == null
                && !state.CanVisitAdventureSite(retiredId)
                && saved.AdventureOpenTiles.Where(id => id != replacement?.Id).All(id => state.IsAdventureTileOpen(AdventureTileCatalog.Find("city", id)))
                && (replacement == null || state.IsAdventureTileOpen(replacement))
                && state.Gold == saved.Gold && state.Food == saved.Food, $"Retired {retiredId} returns to the first stage without losing exploration or balances");
        }
        var survey = tilesCity.First(tile => tile.Discovery?.Kind == AdventureDiscoveryKind.Survey);
        var surveyFixture = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(initial))!;
        surveyFixture.Food = 100; surveyFixture.AdventureOpenTiles = new[] { survey.Id };
        Restore(surveyFixture);
        var surveyExpected = tilesCity.Where(state.IsAdventureTileOpen).Select(tile => tile.Id)
            .Concat(AdventureTileCatalog.Surrounding(survey).Select(tile => tile.Id)).ToHashSet();
        Check(state.TryReachAdventureTile(survey, out _) && state.TryCollectAdventureTile(survey, out _)
            && surveyExpected.SetEquals(tilesCity.Where(state.IsAdventureTileOpen).Select(tile => tile.Id)), "Survey collection opens exactly one neighboring ring, never a wider area");
        state.ReloadFromDisk();
        Check(surveyExpected.SetEquals(tilesCity.Where(state.IsAdventureTileOpen).Select(tile => tile.Id)), "Reloading a collected chart does not expand its reveal radius");
        Restore(saved);
        var oldShrine = state.BuildSaveData(); oldShrine.Version = 44;
        oldShrine.VisitedAdventureSites = oldShrine.VisitedAdventureSites.Append("landmark-2").ToArray();
        oldShrine.AdventureHeroNodes["city"] = "landmark-2";
        oldShrine.AdventureOpenTiles = null; oldShrine.AdventureReachedTiles = null; oldShrine.AdventureCaravanTiles = null;
        Restore(oldShrine);
        Check(state.GetAdventureCaravanTile("city").Id == leader.Id && AdventureTileCatalog.Find("city", "landmark-2") == null
            && !state.CanVisitAdventureSite("landmark-2") && state.GetAdventureStartingCourageBonus(1) == 0,
            "Legacy shrine saves return to the first stage without restoring a shrine or its courage bonus");
        var oldUntouched = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(initial))!;
        oldUntouched.Version = 44;
        oldUntouched.AdventureOpenTiles = null; oldUntouched.AdventureReachedTiles = null; oldUntouched.AdventureCaravanTiles = null;
        Restore(oldUntouched);
        Check(state.IsAdventureZoneUnlocked("city") && AssetCoverageCatalog.RouteIds.Skip(1).All(map => !state.IsAdventureZoneUnlocked(map)),
            "Legacy starting camps do not unlock unplayed zones after becoming ordinary terrain");
        Restore(saved);
        var noFood = state.BuildSaveData(); noFood.Food = 0; noFood.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Restore(noFood);
        var unvisited = tilesCity.First(tile => tile.Discovery != null && state.IsAdventureTileOpen(tile) && !state.IsAdventureTileComplete(tile));
        Check(state.TryReachAdventureTile(unvisited, out _) && !state.HasClaimedAdventureDiscovery(unvisited.Id) && state.Food == 0
            && state.GetAdventureCaravanTile("city").Id == unvisited.Id, "An open resource tile can be reached with zero food before collection");
        Restore(saved);
        var immediateFood = state.Food + (unvisited.Discovery.Kind == AdventureDiscoveryKind.Food ? unvisited.Discovery.Amount : 0);
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _));
        Check(!canvas.IsTravelling && state.Food == immediateFood && state.HasClaimedAdventureDiscovery(unvisited.Id)
            && state.GetAdventureCaravanTile("city").Id == unvisited.Id, "Tile travel immediately collects without a charge or cart animation");
        await Open("MainMenu");
        Check(state.Food == immediateFood && state.HasClaimedAdventureDiscovery(unvisited.Id)
            && state.GetAdventureCaravanTile("city").Id == unvisited.Id, "Reopening the map retains the immediate destination and collection");
        menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        Restore(saved);
        var reduced = state.BuildSaveData(); reduced.ReducedMotion = true; Restore(reduced);
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(unvisited.Id), "Reduced motion reaches and collects the same tile without animation");
        Restore(saved);
        var legacy = state.BuildSaveData(); legacy.Version = 44; legacy.AdventureOpenTiles = null; legacy.AdventureReachedTiles = null; legacy.AdventureCaravanTiles = null;
        Restore(legacy);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && state.IsAdventureTileOpen(leader) && state.GetStageStars(1) == 3, "Version 44 saves migrate site claims, resource claims and cleared-stage reveals");
        Restore(saved);
        var otherStage = tilesCity.First(tile => tile.Site?.Kind == AdventureSiteKind.Leader && !state.HasReachedAdventureTile(tile.Id));
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
        Walk(menu).OfType<AdventureMapToken>().Single(token => token.Site.Id == leader.Id).EmitSignal(BaseButton.SignalName.Pressed); await Wait(.1);
        var details = menu.GetNode<PanelContainer>("HomeHud/SelectedSite");
        Check(!Walk(details).OfType<Button>().Any(button => button.Text is "Intel" or "Overview"), "Stage details present rewards and costs directly without an Intel tab");
        var resourceIcons = Walk(details).OfType<TextureRect>().Where(icon => icon.IsVisibleInTree()).ToArray();
        Check(resourceIcons.Count(icon => icon.Texture == HomeMapArt.Icon("food")) == (GameData.GetStage(1).RewardFood > 0 ? 2 : 1)
            && resourceIcons.Any(icon => icon.Texture == HomeMapArt.Icon("gold"))
            && Walk(details).OfType<Label>().Any(label => label.Text == $"+{GameData.GetStage(1).RewardGold:N0}"),
            "Stage details display the configured victory reward and a single battle entry ration cost");
        Check(!Walk(details).OfType<Button>().Any(button => button.Text.Contains("directive", StringComparison.OrdinalIgnoreCase)),
            "Cleared stage details offer no heroic directive");
        AuditText("Tile map / stage details"); await Capture("05-stage-costs");
        await Press("Prepare battle"); await Wait(.3);
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
}
