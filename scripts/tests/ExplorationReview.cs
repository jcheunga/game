using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>Resource, save-migration, route and rendered-veil checks for the expanded atlas.</summary>
public partial class ExplorationReview : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _failures;
    private bool Phone => OS.GetCmdlineUserArgs().Contains("--mobile-preview");
    private static T Read<T>(object obj,string field) => (T)(obj is MapMenu ? typeof(MapMenu) : obj.GetType()).GetField(field,Hidden).GetValue(obj);
    private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,Hidden).Invoke(obj,args);
    private void Check(bool ok,string text) { GD.Print($"EXPLORATION_CHECK: {(ok ? "PASS" : "FAIL")} {text}"); if (!ok) _failures++; }
    private async Task Wait(double seconds=.1) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static int Charted(string map) => Enumerable.Range(0,AdventureTerrain.CellCount).Count(c => GameState.Instance.IsAdventureCellRevealed(map,c));
    private static int Cell(string map) => AdventureTerrain.Cell(GameState.Instance.GetAdventureHeroPosition(map));
    private void Reset() { GameState.Instance.ResetProgress(); GameState.Instance.SetShowHints(false); GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetReducedMotion(false); }
    private void Restore(GameSaveData save) => Call(GameState.Instance,"ApplySavedData",save);
    private void Fund(int food) { var save=GameState.Instance.BuildSaveData(); save.Food=food; save.FoodRechargedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Restore(save); }
    private static int[] RouteTo(int cell) => AdventureTerrain.Path("city",Cell("city"),cell);
    private bool Walk(int[] path)
    {
        var state=GameState.Instance;
        if (!state.TryBeginAdventureTravel("city",path,out _)) return false;
        foreach (var cell in path.Skip(1)) if (!state.TryPayAdventureStep("city",cell,out _) || !state.CompleteAdventureStep("city",cell)) return false;
        return true;
    }
    private async Task<MapMenu> Map(string zone="city")
    {
        if (!GameState.Instance.IsAdventureZoneUnlocked(zone))
        {
            var maps = GameData.Stages.Select(stage => stage.MapId).Distinct().ToArray();
            var previous = maps[Array.IndexOf(maps, zone) - 1];
            GameState.Instance.ApplyVictory(GameData.GetStagesForMap(previous).Max(stage => stage.StageNumber), 0, 0, 1);
        }
        GameState.Instance.SetSelectedStage(GameData.GetStagesForMap(zone).First().StageNumber);
        var menu=(MapMenu)await LiveUiReview.Open(this, "MainMenu"); await Wait(.2); return menu;
    }
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); RenderingServer.ForceDraw(false);
        using var image=GetViewport().GetTexture().GetImage();
        var folder=ProjectSettings.GlobalizePath("res://artifacts/exploration-review/"+(Phone ? "phone" : "desktop")); Directory.CreateDirectory(folder);
        Check(image.SavePng(folder+"/"+name+".png")==Error.Ok,"Capture "+name);
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(s=>s.StartsWith("--save-suffix=exploration-review-"))) throw new Exception("Use an isolated exploration-review save.");
            GetTree().AutoAcceptQuit=false; GetWindow().Mode=Window.ModeEnum.Windowed;
            GetWindow().Size=Phone ? new Vector2I(844,390) : new Vector2I(1280,720);
            GetWindow().ContentScaleSize=new Vector2I(1280,720); GetWindow().ContentScaleMode=Window.ContentScaleModeEnum.CanvasItems;
            Reset(); var state=GameState.Instance; var cityStages=GameData.GetStagesForMap("city").OrderBy(s=>s.StageNumber).ToArray(); var cityBoss=cityStages[^1].StageNumber;
            Check(AdventureTerrain.CellCount==768 && Enumerable.Range(0,AdventureTerrain.CellCount).All(c=>AdventureTerrain.Cell(AdventureTerrain.Point(c))==c),"All 768 isometric tiles match their movement coordinates");
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                var sites=AdventureMapCatalog.ForMap(zone); var camp=AdventureTerrain.Cell(sites[0].Point); var boss=sites.Last(n=>n.Kind==AdventureSiteKind.Leader);
                var discoveries=AdventureDiscoveryCatalog.ForMap(zone); var walkable=Enumerable.Range(0,AdventureTerrain.CellCount).Count(c=>AdventureTerrain.Walkable(zone,c));
                var spacing=sites.SelectMany((a,i)=>sites.Skip(i+1).Select(b=>AdventureTerrain.Distance(AdventureTerrain.Cell(a.Point),AdventureTerrain.Cell(b.Point)))).Min();
                GD.Print($"EXPLORATION_LAYOUT: {zone} {walkable} ground, {discoveries.Count} discoveries, landmark spacing {spacing}");
                Check(walkable>550 && sites.All(n=>AdventureTerrain.Path(zone,camp,AdventureTerrain.Cell(n.Point)).Length>0),zone+" has a large connected landscape with reachable landmarks");
                Check(spacing>=4 && AdventureTerrain.Distance(camp,AdventureTerrain.Cell(boss.Point))>=35 && sites.Where(n=>n!=boss).All(n=>AdventureTerrain.Distance(AdventureTerrain.Cell(n.Point),AdventureTerrain.Cell(boss.Point))>=5),zone+" separates points of interest and gives the boss a distant approach");
                Check(discoveries.Count>=32 && discoveries.Count<=40 && discoveries.Select(d=>d.Kind).Distinct().Count()==5 && discoveries.All(d=>AdventureTerrain.Path(zone,camp,d.Cell).Length>0),zone+" has sparse reachable discoveries with all five reward types");
                Check(discoveries.SelectMany((a,i)=>discoveries.Skip(i+1).Select(b=>AdventureTerrain.Distance(a.Cell,b.Cell))).All(d=>d>=4) && discoveries.All(d=>sites.All(n=>AdventureTerrain.Distance(d.Cell,AdventureTerrain.Cell(n.Point))>=3)),zone+" spaces discoveries away from one another and from landmarks");
                Check(discoveries[0].Kind==AdventureDiscoveryKind.Food && AdventureTerrain.Path(zone,camp,discoveries[0].Cell).Length<=7,zone+" offers early provisions within a short expedition");
                Check(!state.IsAdventureSiteDiscovered(boss.Id) && Charted(zone)<=13 && !state.BuildSaveData().ClaimedAdventureDiscoveries.Any(),zone+" starts with nearby camp ground visible and its distant contents hidden");
            }
            var early=AdventureDiscoveryCatalog.ForMap("city")[0]; var path=RouteTo(early.Cell); var campCell=path[0];
            Check(!state.TryBeginAdventureTravel("city",new[]{campCell,early.Cell},out _) && state.Food==24,"Invalid travel cannot spend food");
            Check(state.TryBeginAdventureTravel("city",path,out _) && state.Food==24,"Starting travel reserves no food for unseen future steps");
            var chartedBefore=Charted("city"); var first=path[1];
            Check(state.IsAdventureCellRevealed("city",first) && state.TryPayAdventureStep("city",first,out _) && state.Food==24 && state.IsAdventureCellTravelled("city",first),"Charted ground is free on its first entry and is recorded as travelled");
            Check(state.CompleteAdventureStep("city",first) && Charted("city")>chartedBefore && !state.HasClaimedAdventureDiscovery(early.Id),"Arrival expands sight without granting an unreached discovery");
            Check(Walk(RouteTo(early.Cell)) && state.Food==24+early.Amount && state.HasClaimedAdventureDiscovery(early.Id),"Arriving at hidden provisions pays the advertised food bonus once");
            var food=state.Food; var reverse=path.Reverse().ToArray();
            Check(state.GetAdventureTravelFoodCost("city",reverse)==0 && Walk(reverse) && Walk(path) && state.Food==food,"Backtracking is free and provisions cannot be claimed twice");
            var save=state.BuildSaveData(); state.ReloadFromDisk();
            Check(state.HasClaimedAdventureDiscovery(early.Id) && state.GetAdventureTravelFoodCost("city",reverse)==0 && state.Food==save.Food && Cell("city")==early.Cell,"Reload preserves food, paid routes, discovery claims and caravan position");
            Check(!state.HasClaimedAdventureDiscovery(AdventureDiscoveryCatalog.ForMap("harbor")[0].Id),"A discovery claim belongs only to its own zone");
            foreach (var kind in new[]{AdventureDiscoveryKind.Gold,AdventureDiscoveryKind.Tomes,AdventureDiscoveryKind.Essence,AdventureDiscoveryKind.Survey})
            {
                Reset(); Fund(100);
                var reward=AdventureDiscoveryCatalog.ForMap("city").First(d=>d.Kind==kind); var near=AdventureTerrain.Neighbors(reward.Cell).First(c=>AdventureTerrain.Walkable("city",c));
                state.MoveAdventureHero("city",AdventureTerrain.Point(near));
                var beforeGold=state.Gold; var beforeTomes=state.Tomes; var beforeEssence=state.Essence; var beforeCharted=Charted("city");
                Check(!state.HasClaimedAdventureDiscovery(reward.Id) && state.IsAdventureCellRevealed("city",reward.Cell),kind+" stays unclaimed while merely visible");
                state.TryPayAdventureStep("city",reward.Cell,out _); state.CompleteAdventureStep("city",reward.Cell);
                var correct=kind switch { AdventureDiscoveryKind.Gold=>state.Gold==beforeGold+reward.Amount,AdventureDiscoveryKind.Tomes=>state.Tomes==beforeTomes+reward.Amount,AdventureDiscoveryKind.Essence=>state.Essence==beforeEssence+reward.Amount,_=>Charted("city")>beforeCharted+5 };
                var claimed=state.BuildSaveData(); state.CompleteAdventureStep("city",reward.Cell);
                Check(correct && state.HasClaimedAdventureDiscovery(reward.Id) && state.Gold==claimed.Gold && state.Tomes==claimed.Tomes && state.Essence==claimed.Essence,kind+" grants its real bonus once on arrival");
            }
            Reset(); var tower=AdventureMapCatalog.Find("landmark-1"); state.MoveAdventureHero("city",tower.Point); var beforeTower=Charted("city"); var beforeTowerFood=state.Food;
            Check(!state.TryVisitAdventureSite(tower.Id,out _) && AdventureTileCatalog.Find("city",tower.Id)==null && !state.HasVisitedAdventureSite(tower.Id) && Charted("city")==beforeTower && state.Food==beforeTowerFood && state.BuildSaveData().ClaimedAdventureDiscoveries.Length==0,"Retired watchtowers cannot be visited and grant no reveal, food or tile rewards");
            Reset(); var legacy=state.BuildSaveData(); var shrine=AdventureMapCatalog.Find("landmark-2"); legacy.Version=43;
            legacy.VisitedAdventureSites=new[]{"supply-1",shrine.Id}; legacy.AdventureHeroNodes["city"]=shrine.Id;
            legacy.AdventureHeroPositions["city"]=new[]{640f,600f}; legacy.AdventureExploredCells["city"]=new[]{73,38};
            legacy.StageStars=new int[state.MaxStage]; legacy.StageStars[cityStages[^2].StageNumber-1]=3; legacy.Gold=147; legacy.Food=17;
            Restore(legacy);
            Check(state.GetAdventureCaravanTile("city").Id==AdventureTileCatalog.Starting("city").Id && state.HasVisitedAdventureSite("supply-1")
                && state.GetAdventureStartingCourageBonus(2)==0 && state.Gold==147 && state.Food==17,"Old saves return the caravan to the first stage, keep collected landmarks' ground and resources, and drop shrine courage");
            Check(state.IsAdventureSiteDiscovered("leader-2") && state.IsAdventureSiteDiscovered($"leader-{cityStages[^2].StageNumber}") && !state.CanVisitAdventureSite($"leader-{cityBoss}") && !state.IsCampaignStageUnlocked(cityBoss) && state.BuildSaveData().ClaimedAdventureDiscoveries.Length==0,"Migration preserves known and defeated rivals, keeps the boss gate sealed and grants no new discoveries");
            var invalid=state.BuildSaveData(); invalid.AdventureHeroPositions["city"]=new[]{float.NaN,0f}; invalid.AdventureExploredCells["city"]=new[]{-1,8000}; invalid.AdventureTravelledCells["city"]=new[]{-1,8000}; invalid.ClaimedAdventureDiscoveries=new[]{"invented"}; Restore(invalid);
            Check(state.GetAdventureHeroPosition("city")==shrine.Point && !state.HasClaimedAdventureDiscovery("invented") && state.BuildSaveData().AdventureTravelledCells["city"].Length==0,"Invalid saved positions, cells and discovery IDs are discarded");
            Reset(); Check(state.BuildSaveData().ClaimedAdventureDiscoveries.Length==0 && Charted("city")==13 && !state.IsCampaignStageUnlocked(cityBoss),"Reset clears discoveries and keeps the boss victory gate");
            // Exercise the actual tile canvas: veil, immediate free travel, collection and gating.
            var menu=await Map(); var canvas=Read<MapPathCanvas>(menu,"_mapCanvas");
            var cityTiles=AdventureTileCatalog.ForMap("city"); var start=AdventureTileCatalog.Starting("city");
            var bossTile=cityTiles.Single(t=>t.Site?.Kind==AdventureSiteKind.Leader && state.IsAdventureBoss(t.Site.Stage));
            Check(Read<AdventureMapNode>(menu,"_selected").Id==start.Id && canvas.GetChildren().OfType<AdventureMapToken>().Count(t=>t.Visible)==1,"A fresh map selects the first stage and hides distant landmark markers");
            await Wait(.2);
            Check(Read<NoiseTexture2D>(canvas,"_atlasMist")!=null && Read<Vector2[][]>(canvas,"_landscapeFrontier").Length>0,"Rendered veil uses textured mist with a frontier around known tiles");
            Check(cityTiles.Count(state.IsAdventureTileOpen)==1 && !state.IsAdventureTileOpen(bossTile),"The veil conceals everything except the first stage, including the distant boss");
            await Capture("01-fresh-veil");
            var opened=state.BuildSaveData(); opened.Food=0; opened.FoodRechargedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            opened.AdventureOpenTiles=AdventureTileCatalog.Surrounding(start).Select(t=>t.Id).ToArray(); Restore(opened); canvas.ShowMap("city",start.Id);
            var provisions=AdventureTileCatalog.Find("city",early.Id); var arrived=false;
            canvas.TravelToTile(provisions,()=>arrived=state.TryCollectAdventureTile(provisions,out _));
            Check(!canvas.IsTravelling && arrived && state.Food==early.Amount && state.GetAdventureCaravanTile("city").Id==provisions.Id,"With no food, open provisions are reached immediately and pay their bonus");
            Check(AdventureTileCatalog.Surrounding(provisions).All(state.IsAdventureTileOpen) && !state.TryCollectAdventureTile(provisions,out _) && state.Food==early.Amount,"Collected provisions open their ring and cannot be claimed twice");
            canvas.TravelToTile(start,null); Check(state.GetAdventureCaravanTile("city").Id==start.Id && state.Food==early.Amount,"Returning to the first stage is free");
            await Capture("03-first-provisions");
            var veiled=cityTiles.First(t=>t.Discovery?.Kind==AdventureDiscoveryKind.Gold && !state.IsAdventureTileOpen(t)); var beforeVeiled=state.Gold; var claims=state.BuildSaveData().ClaimedAdventureDiscoveries.Length;
            canvas.TravelToTile(veiled,()=>state.TryCollectAdventureTile(veiled,out _)); menu.QueueFree(); await Wait(.2); state.ReloadFromDisk();
            Check(state.Gold==beforeVeiled && state.BuildSaveData().ClaimedAdventureDiscoveries.Length==claims && state.GetAdventureCaravanTile("city").Id==start.Id,"Veiled tiles reject travel and a reload grants no distant rewards");
            Reset(); menu=await Map("citadel"); await Capture("06-citadel-veil"); menu.QueueFree(); await Wait(.2);
        }
        catch (Exception e) { GD.PrintErr(e); _failures++; }
        GD.Print($"EXPLORATION_RESULT: {_failures} failures"); await LiveUiReview.StopAudio(this); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
