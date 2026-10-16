using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>Layout, exploration-rule, save-migration and rendered-veil checks for the zone atlases.</summary>
public partial class ExplorationReview : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _failures;
    private bool Phone => OS.GetCmdlineUserArgs().Contains("--mobile-preview");
    private static T Read<T>(object obj,string field) => (T)(obj is MapMenu ? typeof(MapMenu) : obj.GetType()).GetField(field,Hidden).GetValue(obj);
    private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,Hidden).Invoke(obj,args);
    private void Check(bool ok,string text) { GD.Print($"EXPLORATION_CHECK: {(ok ? "PASS" : "FAIL")} {text}"); if (!ok) _failures++; }
    private async Task Wait(double seconds=.1) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Reset() { GameState.Instance.ResetProgress(); GameState.Instance.SetShowHints(false); GameState.Instance.SetAnalyticsConsent(false); GameState.Instance.SetReducedMotion(false); }
    private void Restore(GameSaveData save) => Call(GameState.Instance,"ApplySavedData",save);
    private void Fund(int food) { var save=GameState.Instance.BuildSaveData(); save.Food=food; save.FoodRechargedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Restore(save); }
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
            Reset(); var state=GameState.Instance;
            int Steps(AdventureTile from, AdventureTile to)
            {
                var seen=new System.Collections.Generic.Dictionary<string,int>{[from.Id]=0}; var queue=new System.Collections.Generic.Queue<AdventureTile>(); queue.Enqueue(from);
                while (queue.Count>0) { var tile=queue.Dequeue(); if (tile.Id==to.Id) return seen[tile.Id];
                    foreach (var next in AdventureTileCatalog.Neighbors(tile)) if (seen.TryAdd(next.Id,seen[tile.Id]+1)) queue.Enqueue(next); }
                return int.MaxValue;
            }
            bool BesideStage(AdventureTile tile) => AdventureTileCatalog.Neighbors(tile).Any(t=>t.Site?.Kind==AdventureSiteKind.Leader);
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                var tiles=AdventureTileCatalog.ForMap(zone); var stages=Enumerable.Range(0,10).Select(i=>AdventureTileCatalog.Stage(zone,i)).ToArray();
                var coast=AdventureAtlasLandscape.Coast(zone);
                Check(tiles.Count==AdventureTileCatalog.Columns*AdventureTileCatalog.Rows && tiles.All(t=>Geometry2D.IsPointInPolygon(t.Point,coast) && !AdventureAtlasLandscape.IsWater(zone,t.Point)),
                    zone+$" has {tiles.Count} tiles, every one on dry land inside its coast");
                Check(tiles.All(t=>AdventureTileCatalog.Neighbors(t).Count is >= 2 and <= 9 && AdventureTileCatalog.Neighbors(t).All(n=>AdventureTileCatalog.Neighbors(n).Contains(t)))
                    && tiles.Average(t=>AdventureTileCatalog.Neighbors(t).Count) is > 5 and < 7,zone+" tiles border about six neighbours each, symmetrically");
                Check(tiles.All(t=>Steps(stages[0],t)<int.MaxValue),zone+" can be explored from its first stage to every tile");
                var hops=AdventureTileCatalog.Roads.Select(road=>Steps(stages[road.From],stages[road.To])).ToArray();
                GD.Print($"EXPLORATION_LAYOUT: {zone} stage hops {string.Join(",",hops)}, start to boss {Steps(stages[0],stages[9])}, bridges {AdventureAtlasLandscape.Bridges(zone).Length}, finds {AdventureDiscoveryCatalog.ForMap(zone).Count}, neighbours {tiles.Min(t=>AdventureTileCatalog.Neighbors(t).Count)}-{tiles.Max(t=>AdventureTileCatalog.Neighbors(t).Count)}");
                Check(hops.All(h=>h>=3) && hops.Average()>=4.5 && Steps(stages[0],stages[9])>=18,zone+" spreads its stages several tiles apart along the roads");
                Check(AdventureTileCatalog.Roads.Count(road=>road.From==1)==3 && AdventureTileCatalog.Roads.Count(road=>road.To==8)==3 && AdventureAtlasLandscape.Roads(zone).Length==AdventureTileCatalog.Roads.Length,
                    zone+" splits its road into three lanes that converge before the boss");
                Check(AdventureAtlasLandscape.Bridges(zone).Length>=3,zone+" bridges the river where the lanes cross it");
                var resources=tiles.Where(t=>t.IsResource).ToArray();
                Check(resources.Length==39 && tiles.Count(t=>!t.HasInterest)>=190 && resources.All(r=>AdventureTileCatalog.Neighbors(r).All(n=>!n.IsResource)),
                    zone+" scatters 39 resources over mostly plain ground, never side by side");
                // Food economy: tiles to open every road, then the shortest detours to every food source.
                var opened=new System.Collections.Generic.HashSet<string>(AdventureTileCatalog.Roads.SelectMany(road=>GameState.AdventureTilePath(stages[road.From],stages[road.To])).Select(t=>t.Id));
                // Winning a stage opens every tile around it for free, gathering what lies there.
                var free=stages.SelectMany(AdventureTileCatalog.Neighbors).Select(t=>t.Id).ToHashSet();
                var roadTiles=opened.Count(id=>AdventureTileCatalog.Find(zone,id).Site?.Kind!=AdventureSiteKind.Leader && !free.Contains(id));
                opened.UnionWith(free);
                var foodSources=tiles.Where(t=>t.Discovery?.Kind==AdventureDiscoveryKind.Food || t.Site?.Kind==AdventureSiteKind.Food).ToArray();
                var detour=0;
                foreach (var source in foodSources.OrderBy(t=>opened.Contains(t.Id)?0:1))
                {
                    var best=opened.Select(id=>GameState.AdventureTilePath(AdventureTileCatalog.Find(zone,id),source)).Where(path=>path.Count>0).OrderBy(path=>path.Count).FirstOrDefault();
                    if (best==null) continue;
                    foreach (var step in best.Skip(1).SkipLast(1)) if (opened.Add(step.Id)) detour++;
                    opened.Add(source.Id);
                }
                var foodGained=foodSources.Sum(t=>t.Discovery?.Amount ?? t.Site.FoodReward);
                var entry=stages.Sum(t=>GameState.Instance.GetStageEntryFoodCost(t.Site.Stage));
                GD.Print($"EXPLORATION_ECONOMY: {zone} road tiles {roadTiles} ({roadTiles*GameState.AdventureTileFoodCost} food), food detours {detour} tiles, {foodSources.Length} food sources ({foodSources.Count(BesideStage)} beside a stage, gathered free) pay {foodGained} for {foodSources.Count(t=>!free.Contains(t.Id))*GameState.AdventureTileFoodCost} to open, battles {entry}, first wins pay {stages.Length*GameState.CampaignFirstClearFood}; net {foodGained+stages.Length*GameState.CampaignFirstClearFood-(roadTiles+detour+foodSources.Count(t=>!free.Contains(t.Id)))*GameState.AdventureTileFoodCost-entry}");
                GD.Print($"EXPLORATION_STAGE_RINGS: {zone} resources beside a stage: {string.Join(", ",tiles.Where(t=>t.IsResource && BesideStage(t)).Select(t=>t.Discovery?.Kind.ToString() ?? t.Site.Kind.ToString()))}");
                Check(AdventureTileCatalog.Neighbors(stages[0]).All(t=>!t.HasInterest),zone+" rings its first stage with plain ground");
                var finds=AdventureDiscoveryCatalog.ForMap(zone);
                Check(finds.Count==28 && finds.Select(f=>f.Kind).Distinct().Count()==Enum.GetValues<AdventureDiscoveryKind>().Length
                    && finds[0].Kind==AdventureDiscoveryKind.Food && Steps(stages[0],AdventureTileCatalog.Find(zone,finds[0].Id))<=4,zone+" offers every kind of find, with provisions close to the start");
            }
            var city=AdventureTileCatalog.ForMap("city"); var start=AdventureTileCatalog.Starting("city"); var boss=AdventureTileCatalog.Stage("city",9);
            // The first stage is already discovered, so the plain ground around it starts charted and never costs food.
            var startingGround=AdventureTileCatalog.Neighbors(start).Where(t=>!t.HasInterest).Append(start).ToArray();
            var food=state.Food;
            Check(city.Where(state.IsAdventureTileOpened).Select(t=>t.Id).ToHashSet().SetEquals(startingGround.Select(t=>t.Id))
                && city.Where(state.IsAdventureTileRevealed).Select(t=>t.Id).ToHashSet().SetEquals(startingGround.Concat(startingGround.SelectMany(AdventureTileCatalog.Neighbors)).Select(t=>t.Id))
                && !state.IsAdventureTileRevealed(boss) && !state.TryOpenAdventureTile(startingGround[0],out _) && state.Food==food,
                "A fresh zone has its first stage and the plain ground around it charted, with only the land beyond on the frontier");
            // A plain frontier tile and a hidden tile beyond it that still has hidden land of its own past the frontier tile's reach.
            bool Touches(AdventureTile a,AdventureTile b) => AdventureTileCatalog.Neighbors(a).Any(t=>t.Id==b.Id);
            var (frontier,beyond)=city.Where(t=>state.IsAdventureTileFrontier(t) && !t.HasInterest).OrderBy(t=>t.Point.DistanceTo(start.Point))
                .SelectMany(f=>AdventureTileCatalog.Neighbors(f).Where(b=>!state.IsAdventureTileRevealed(b)).Select(b=>(f,b)))
                .First(pair=>AdventureTileCatalog.Neighbors(pair.b).Any(t=>t.Id!=pair.f.Id && !state.IsAdventureTileRevealed(t) && !Touches(pair.f,t)));
            Check(!state.TryOpenAdventureTile(beyond,out _) && state.Food==food && !state.IsAdventureTileOpened(beyond),"Tiles beyond the frontier cannot be opened and cost nothing");
            Check(state.TryOpenAdventureTile(frontier,out _) && state.Food==food-GameState.AdventureTileFoodCost && state.IsAdventureTileOpened(frontier)
                && AdventureTileCatalog.Neighbors(frontier).All(state.IsAdventureTileRevealed) && !AdventureTileCatalog.Neighbors(beyond).Where(t=>t.Id!=frontier.Id).All(state.IsAdventureTileRevealed),
                "Opening a frontier tile costs 1 food and reveals only the tiles touching it");
            Check(!state.TryOpenAdventureTile(frontier,out _) && state.Food==food-GameState.AdventureTileFoodCost,"Charted ground cannot be bought twice");
            Fund(0);
            Check(!state.TryOpenAdventureTile(beyond,out var cost) && cost.Contains("1 food") && state.Food==0 && !state.IsAdventureTileOpened(beyond),"Without 1 food a frontier tile explains its cost and stays closed");
            Fund(1);
            Check(GameData.Stages.All(stage => state.GetStageEntryFoodCost(stage.StageNumber) == 2)
                && !state.TrySpendStageEntryFood(start.Site.Stage, out _) && state.Food == 1,
                "Stages cost 2 food and cannot start with only 1 food");
            Check(state.TryOpenAdventureTile(beyond, out _) && state.Food == 0,
                "Exactly 1 food opens a frontier tile and is charged once");
            Fund(100);
            var second=AdventureTileCatalog.Stage("city",1);
            foreach (var tile in GameState.AdventureTilePath(start,second).Skip(1).SkipLast(1)) state.TryOpenAdventureTile(tile,out _);
            Check(state.IsAdventureTileFrontier(second) && state.CanTravelToAdventureTile(second,out _) && !state.TryOpenAdventureTile(second,out _),
                "A stage on the frontier is challenged rather than bought");
            var victoryExpected=city.Where(state.IsAdventureTileOpened).Select(t=>t.Id).Append(second.Id).Concat(AdventureTileCatalog.Neighbors(second).Select(t=>t.Id)).ToHashSet();
            state.PrepareCampaignBattle(); state.ApplyVictory(second.Site.Stage,0,0,3);
            Check(state.IsAdventureTileOpened(second) && city.Where(state.IsAdventureTileOpened).Select(t=>t.Id).ToHashSet().SetEquals(victoryExpected)
                && AdventureTileCatalog.Neighbors(second).All(state.IsAdventureTileComplete) && AdventureTileCatalog.Neighbors(second).SelectMany(AdventureTileCatalog.Neighbors).All(state.IsAdventureTileRevealed),
                "Winning a stage opens every tile around it for free and reveals only the land beyond");
            var cache=city.First(t=>t.Id=="supply-1"); var cacheFood=state.Food; var cacheGold=state.Gold;
            foreach (var tile in GameState.AdventureTilePath(start,cache).Skip(1).SkipLast(1)) state.TryOpenAdventureTile(tile,out _);
            cacheFood=state.Food; cacheGold=state.Gold;
            Check(state.TryOpenAdventureTile(cache,out _) && state.HasVisitedAdventureSite(cache.Id) && state.Food==cacheFood-GameState.AdventureTileFoodCost+cache.Site.FoodReward
                && state.Gold==cacheGold+cache.Site.GoldReward && !state.TryOpenAdventureTile(cache,out _),"Opening a cache's tile gathers it once");
            var survey=city.First(t=>t.Discovery?.Kind==AdventureDiscoveryKind.Survey);
            var surveyFixture=state.BuildSaveData(); surveyFixture.AdventureOpenTiles=surveyFixture.AdventureOpenTiles.Concat(AdventureTileCatalog.Neighbors(survey).Take(1).Select(t=>t.Id)).ToArray(); Restore(surveyFixture);
            Check(state.TryOpenAdventureTile(survey,out _) && AdventureTileCatalog.Neighbors(survey).Where(t=>!t.HasInterest).All(state.IsAdventureTileOpened)
                && AdventureTileCatalog.Neighbors(survey).Where(t=>t.HasInterest && t.Id!=AdventureTileCatalog.Neighbors(survey).First().Id).All(t=>!state.IsAdventureTileOpened(t)),
                "A surveyor's chart charts the plain ground around it for free");
            var charted=state.BuildSaveData(); state.ReloadFromDisk();
            Check(city.Where(state.IsAdventureTileOpened).Select(t=>t.Id).ToHashSet().SetEquals(charted.AdventureOpenTiles.Concat(startingGround.Select(t=>t.Id))) && state.HasVisitedAdventureSite(cache.Id),
                "A reload keeps every charted tile and gathered cache");
            // Provisions beside a stage open the frontier around them without food, one ring deep; elsewhere they open like any tile.
            var cityFood=city.Where(t=>t.Discovery?.Kind==AdventureDiscoveryKind.Food || t.Site?.Kind==AdventureSiteKind.Food).ToArray();
            Check(cityFood.Any(BesideStage) && cityFood.Any(t=>!BesideStage(t)),"city has provisions both beside a stage and away from one");
            foreach (var provisions in new[]{cityFood.FirstOrDefault(BesideStage),cityFood.FirstOrDefault(t=>!BesideStage(t))}.Where(t=>t!=null))
            {
                var beside=BesideStage(provisions);
                Reset(); var fixture=state.BuildSaveData(); fixture.Food=10; fixture.FoodRechargedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                fixture.AdventureOpenTiles=new[]{AdventureTileCatalog.Neighbors(provisions).First(t=>!t.HasInterest).Id}; Restore(fixture);
                var ring=AdventureTileCatalog.Neighbors(provisions).Where(t=>!t.HasInterest).Select(t=>t.Id).ToArray();
                var expected=city.Where(state.IsAdventureTileOpened).Select(t=>t.Id).Append(provisions.Id).Concat(beside ? ring : Array.Empty<string>()).ToHashSet();
                Check(ring.Count(id=>!state.IsAdventureTileOpened(AdventureTileCatalog.Find("city",id)))>=2 && state.TryOpenAdventureTile(provisions,out _)
                    && state.Food==10-GameState.AdventureTileFoodCost+(provisions.Discovery?.Amount ?? provisions.Site.FoodReward)
                    && expected.SetEquals(city.Where(state.IsAdventureTileOpened).Select(t=>t.Id)) && AdventureTileCatalog.Neighbors(provisions).All(state.IsAdventureTileRevealed),
                    provisions.Title+(beside ? " beside a stage opens the plain ground around it for free and nothing beyond" : " away from any stage opens only its own tile"));
            }
            // Saves from the smaller atlas: chart the roads between won stages, and all but the resources of a beaten zone.
            Reset(); var former=state.BuildSaveData(); former.Version=46; former.AdventureOpenTiles=new[]{"ground-city-3-4","discovery-city-5-7"};
            former.AdventureCaravanTiles["city"]="ground-city-3-4"; former.StageStars=new int[state.MaxStage];
            foreach (var index in new[]{0,1,3}) former.StageStars[AdventureTileCatalog.Stage("city",index).Site.Stage-1]=3;
            foreach (var stage in GameData.GetStagesForMap("harbor")) former.StageStars[stage.StageNumber-1]=3;
            foreach (var stage in GameData.GetStagesForMap("city").Where(s=>s.StageNumber>AdventureTileCatalog.Stage("city",3).Site.Stage)) former.StageStars[stage.StageNumber-1]=0;
            former.VisitedAdventureSites=new[]{"supply-1"};
            Restore(former);
            Check(GameState.AdventureTilePath(start,AdventureTileCatalog.Stage("city",1)).All(state.IsAdventureTileOpened) && GameState.AdventureTilePath(AdventureTileCatalog.Stage("city",1),AdventureTileCatalog.Stage("city",3)).All(state.IsAdventureTileOpened)
                && !state.IsAdventureTileOpened(AdventureTileCatalog.Stage("city",2)) && state.IsAdventureTileOpened(city.First(t=>t.Id=="supply-1"))
                && state.GetAdventureCaravanTile("city").Id==start.Id,"Older saves chart the roads between the stages they won and their gathered caches");
            var harbor=AdventureTileCatalog.ForMap("harbor");
            Check(harbor.Where(t=>!t.IsResource).All(state.IsAdventureTileOpened) && harbor.Where(t=>t.IsResource).All(t=>!state.IsAdventureTileOpened(t)),"A zone whose boss fell in an older save is charted apart from its resources");
            // The rendered atlas: storm cloud over hidden land, mist over the frontier, a click opens a frontier tile.
            Reset();
            var menu=await Map(); var canvas=Read<MapPathCanvas>(menu,"_mapCanvas");
            var shown=canvas.GetChildren().OfType<AdventureMapToken>().Where(t=>t.Visible).Select(t=>t.Site.Id).ToArray();
            Check(Read<AdventureMapNode>(menu,"_selected").Id==start.Id && shown.Contains(start.Id) && shown.All(id=>id==start.Id || state.IsAdventureTileFrontier(AdventureTileCatalog.Find("city",id))),
                "A fresh map selects the first stage and shows only its marker and the sites on its frontier");
            await Wait(.2);
            var fog=Read<MapFogLayer>(canvas,"_fogLayer");
            Check(fog.Visible && fog.Mask!=null && fog.MaskRect.HasArea(),"The cloud bank covers the hidden land around the first stage");
            await Capture("01-fresh-veil");
            var target=city.Where(t=>state.IsAdventureTileFrontier(t) && !t.HasInterest).OrderBy(t=>t.Point.DistanceTo(start.Point)).First(); food=state.Food;
            canvas.FocusSite(start.Id); await Wait(.1);
            var click=canvas.GlobalPosition+target.Point*canvas.Zoom+canvas.MapOffset;
            menu.GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true,Position=click,GlobalPosition=click});
            menu.GetViewport().PushInput(new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false,Position=click,GlobalPosition=click});
            await Wait(.2);
            Check(state.IsAdventureTileOpened(target) && state.Food==food-GameState.AdventureTileFoodCost,"Clicking a frontier tile on the map opens it for 1 food");
            await Capture("02-first-tile");
            Fund(100);
            foreach (var tile in GameState.AdventureTilePath(start,AdventureTileCatalog.Stage("city",1)).Skip(1).SkipLast(1)) state.TryOpenAdventureTile(tile,out _);
            canvas.RefreshKnowledge(); await Wait(.3); await Capture("03-road-to-second-stage");
            // The painting holds up at the closest zoom.
            canvas.ChangeZoom(10f); canvas.FocusSite(AdventureTileCatalog.Stage("city",1).Id); await Wait(.3); await Capture("04-closest-zoom");
            menu.QueueFree(); await Wait(.2);
            Reset(); menu=await Map("citadel"); await Capture("06-citadel-veil"); menu.QueueFree(); await Wait(.2);
        }
        catch (Exception e) { GD.PrintErr(e); _failures++; }
        GD.Print($"EXPLORATION_RESULT: {_failures} failures"); await LiveUiReview.StopAudio(this); GetTree().Quit(_failures==0 ? 0 : 1);
    }
}
