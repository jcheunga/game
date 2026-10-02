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
            Check(tiles.Count(tile => tile.Site?.Kind == AdventureSiteKind.Leader) == 6 && tiles.Count(tile => tile.Discovery != null) == AdventureDiscoveryCatalog.ForMap(map).Count, map + " preserves every stage and discovery");
            var reached = AdventureTileCatalog.Surrounding(AdventureTileCatalog.Find(map, "camp-" + map)).Select(tile => tile.Id).ToHashSet();
            for (var pass = 0; pass < 20; pass++)
                foreach (var tile in tiles.Where(tile => tile.HasInterest && reached.Contains(tile.Id)).ToArray())
                    foreach (var near in AdventureTileCatalog.Surrounding(tile)) reached.Add(near.Id);
            Check(tiles.Where(tile => tile.HasInterest).All(tile => reached.Contains(tile.Id)), map + " has a connected completion route to every point of interest");
        }
        if (OS.GetCmdlineUserArgs().Contains("--atlas-geometry"))
        {
            GD.Print($"ATLAS_GEOMETRY_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1); return;
        }
        await Open("MainMenu");
        var menu = (MapMenu)GetTree().CurrentScene;
        var canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var tilesCity = AdventureTileCatalog.ForMap("city");
        var leader = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && tile.Site.Stage == 1);
        var supply = tilesCity.Single(tile => tile.Id == "supply-1");
        Check(state.IsAdventureTileOpen(leader) && state.IsAdventureTileOpen(supply), "A fresh map exposes a starter stage and supplies");
        Check(!state.IsAdventureTileOpen(tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage))), "Distant stages remain under the tile veil");
        Check(!menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Fresh map keeps details closed");
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
        canvas.FocusCaravan();
        var originalZoom = canvas.Zoom; await PressHint("Zoom in"); Check(canvas.Zoom > originalZoom, "Map zoom controls work"); await PressHint("Zoom out");
        Check(Math.Abs(canvas.Zoom - originalZoom) < .01f, "Map zoom returns to its original scale");
        var far = tilesCity.Single(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureBoss(tile.Site.Stage));
        Check(!state.TryReachAdventureTile(far, out _) && state.Food == food, "Unopened destinations reject travel without charging");
        // Native resource selection traverses the real marker and commits its first-travel charge once.
        canvas.FocusSite(supply.Id); await Wait(.1);
        var supplyToken = Walk(menu).OfType<AdventureMapToken>().Single(token => token.Site.Id == supply.Id);
        var native = supplyToken.GlobalPosition + supplyToken.Size * supplyToken.Scale * .5f;
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = native, GlobalPosition = native });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = native, GlobalPosition = native });
        await FinishTravel();
        Check(state.HasVisitedAdventureSite(supply.Id) && state.Gold == initial.Gold + supply.Site.GoldReward && state.Food == food - 1, "Native cache tap grants the advertised gold and charges one food");
        Check(state.AdventureKnowledgeRevision > knowledge && !supplyToken.Visible && !menu.GetNode<PanelContainer>("HomeHud/SelectedSite").Visible, "Gathering opens surrounding tiles and removes the cache marker");
        var gold = state.Gold;
        Check(!state.TryCollectAdventureTile(supply, out _) || state.Gold == gold, "Collected caches cannot pay twice");
        await Capture("02-resource-opens-tiles");
        var discovered = tilesCity.First(tile => tile.Discovery != null && state.IsAdventureTileOpen(tile) && !state.IsAdventureTileComplete(tile));
        var rewardFood = state.Food;
        canvas.TravelToTile(discovered, () => state.TryCollectAdventureTile(discovered, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(discovered.Id) && state.Food == rewardFood - 1 + (discovered.Discovery.Kind == AdventureDiscoveryKind.Food ? discovered.Discovery.Amount : 0), "Discovery grants its real reward after the destination charge");
        Check(!state.TryCollectAdventureTile(discovered, out _), "Discovery claims reject duplicates");
        var openedBeforeVisit = tilesCity.Count(state.IsAdventureTileOpen);
        var beforeStageTravel = state.Food;
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel - 1, "Reaching a new stage costs one food");
        Check(state.TryCollectAdventureTile(leader, out _) && tilesCity.Count(state.IsAdventureTileOpen) == openedBeforeVisit, "Preparing a stage does not open surrounding tiles");
        state.ApplyDefeat(1); state.ApplyRetreat(1);
        Check(tilesCity.Count(state.IsAdventureTileOpen) == openedBeforeVisit, "Defeat and retreat do not open surrounding tiles");
        Check(state.TryReachAdventureTile(leader, out _) && state.Food == beforeStageTravel - 1, "Returning to a reached stage is free");
        var entryFood = state.Food; var entryCost = state.GetStageEntryFoodCost(1);
        Check(state.TrySpendStageEntryFood(1, out _) && state.Food == entryFood - entryCost, "Stage entry charges its separate existing food cost");
        state.PrepareCampaignBattle(); state.ApplyVictory(1, 0, 0, 3);
        Check(AdventureTileCatalog.Surrounding(leader).All(state.IsAdventureTileOpen) && state.GetStageStars(1) == 3, "Campaign victory opens the surrounding eight tiles and retains stars");
        canvas.ShowMap("city", leader.Id); canvas.FocusSite(leader.Id); await Wait(.2);
        await Capture("03-victory-map"); AuditText("Tile map / victory");
        var saved = state.BuildSaveData();
        state.ReloadFromDisk();
        Check(state.GetAdventureCaravanTile("city").Id == leader.Id && state.IsAdventureTileOpen(leader) && state.HasClaimedAdventureDiscovery(discovered.Id), "A full disk reload retains tile progression and claims");
        Restore(saved);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && AdventureTileCatalog.Surrounding(leader).All(state.IsAdventureTileOpen) && state.GetAdventureCaravanTile("city").Id == leader.Id, "Save reload retains reached tiles, claims, caravan destination and opened neighbors");
        var tower = tilesCity.Single(tile => tile.Id == "landmark-1");
        var shrine = tilesCity.First(tile => tile.Site?.Kind == AdventureSiteKind.Shrine);
        var landmarkFixture = state.BuildSaveData(); landmarkFixture.Food = 100;
        landmarkFixture.AdventureOpenTiles = landmarkFixture.AdventureOpenTiles.Append(tower.Id).Append(shrine.Id).ToArray();
        Restore(landmarkFixture);
        Check(state.TryReachAdventureTile(tower, out _) && state.TryCollectAdventureTile(tower, out _)
            && AdventureTileCatalog.Surrounding(tower, 2).All(state.IsAdventureTileOpen), "A watchtower opens two full rings of tiles");
        Check(state.TryReachAdventureTile(shrine, out _) && state.TryCollectAdventureTile(shrine, out _)
            && state.GetAdventureStartingCourageBonus(1) == 3 && state.GetAdventureStartingCourageBonus(7) == 0, "Shrine collection grants its district blessing and opens neighbors");
        var shrineFood = state.Food;
        state.TryReachAdventureTile(shrine, out _); state.TryCollectAdventureTile(shrine, out _);
        Check(state.Food == shrineFood && state.GetAdventureStartingCourageBonus(1) == 3, "Returning to a shrine is free and cannot stack its blessing");
        var oldTower = state.BuildSaveData(); oldTower.Version = 44; oldTower.AdventureOpenTiles = null; oldTower.AdventureReachedTiles = null; oldTower.AdventureCaravanTiles = null;
        Restore(oldTower);
        Check(AdventureTileCatalog.Surrounding(tower, 2).All(state.IsAdventureTileOpen) && state.GetAdventureStartingCourageBonus(1) == 3,
            "Legacy landmark migration retains the wide tower reveal and shrine blessing");
        Restore(saved);
        var noFood = state.BuildSaveData(); noFood.Food = 0; noFood.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Restore(noFood);
        var unvisited = tilesCity.First(tile => tile.Discovery != null && state.IsAdventureTileOpen(tile) && !state.IsAdventureTileComplete(tile));
        Check(!state.TryReachAdventureTile(unvisited, out _) && !state.HasClaimedAdventureDiscovery(unvisited.Id) && state.Food == 0, "Insufficient food leaves rewards and destination untouched");
        Restore(saved);
        var cancelledFood = state.Food; var cancelledDestination = state.GetAdventureCaravanTile("city").Id;
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _));
        await Wait(.05); await Open("MainMenu");
        Check(state.Food == cancelledFood && !state.HasClaimedAdventureDiscovery(unvisited.Id) && state.GetAdventureCaravanTile("city").Id == cancelledDestination, "Leaving during the travel animation cancels without spending food or collecting rewards");
        menu = (MapMenu)GetTree().CurrentScene; canvas = Walk(menu).OfType<MapPathCanvas>().Single();
        var reduced = state.BuildSaveData(); reduced.ReducedMotion = true; Restore(reduced);
        canvas.TravelToTile(unvisited, () => state.TryCollectAdventureTile(unvisited, out _)); await FinishTravel();
        Check(state.HasClaimedAdventureDiscovery(unvisited.Id), "Reduced motion reaches and collects the same tile without animation");
        Restore(saved);
        var legacy = state.BuildSaveData(); legacy.Version = 44; legacy.AdventureOpenTiles = null; legacy.AdventureReachedTiles = null; legacy.AdventureCaravanTiles = null;
        Restore(legacy);
        Check(state.HasVisitedAdventureSite(supply.Id) && state.HasClaimedAdventureDiscovery(discovered.Id) && state.IsAdventureTileOpen(leader) && state.GetStageStars(1) == 3, "Version 44 saves migrate site claims, resource claims and cleared-stage reveals");
        Restore(saved);
        var otherStage = tilesCity.First(tile => tile.Site?.Kind == AdventureSiteKind.Leader && state.IsAdventureTileOpen(tile) && !state.HasReachedAdventureTile(tile.Id));
        var tight = state.BuildSaveData(); tight.Food = state.GetStageEntryFoodCost(otherStage.Site.Stage); Restore(tight);
        Check(!state.TryReachAdventureTile(otherStage, out _) && state.Food == tight.Food, "Stage travel requires enough food for both travel and eventual entry");
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
        AuditText("Tile map / stage details"); await Capture("05-stage-costs");
        await Press("Prepare battle"); await Wait(.3);
        Check(menu.HomeModalDestination == SceneRouter.LoadoutScene && state.SelectedStage == 1, "Stage tile launches the matching real battle preparation");
        var deployFood = state.Food;
        await Press("Deploy");
        Check(GetTree().CurrentScene is BattleController && state.Food == deployFood - state.GetStageEntryFoodCost(1), "Real deployment charges stage entry exactly once");
        await PressHint("Retreat");
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
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
