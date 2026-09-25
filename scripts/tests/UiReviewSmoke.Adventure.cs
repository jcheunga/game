using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewAdventure()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/adventure-map");
        System.IO.Directory.CreateDirectory(_output);
        var state = GameState.Instance;
        var initialGold = state.Gold; var initialFood = state.Food;
        Check(!state.TryVisitAdventureSite("leader-60", out _) && !state.TryVisitAdventureSite("hidden-city", out _) && !state.TryVisitAdventureSite("invalid", out _), "Locked bosses, hidden treasures and unknown sites reject visits");
        Check(state.Gold == initialGold && state.Food == initialFood, "Rejected sites do not change resources");
        await Open("MapMenu"); await Capture("01-first-expedition"); AuditText("Adventure / fresh map");
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        var panStart = canvas.MapOffset;
        var dragFrom = canvas.GlobalPosition + new Vector2(160, 30);
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = dragFrom, GlobalPosition = dragFrom });
        Send(new InputEventMouseMotion { ButtonMask = MouseButtonMask.Left, Relative = new Vector2(-65, 0), Position = dragFrom + new Vector2(-65,0), GlobalPosition = dragFrom + new Vector2(-65,0) });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = dragFrom + new Vector2(-65,0), GlobalPosition = dragFrom + new Vector2(-65,0) });
        await Wait(.1); Check(canvas.MapOffset != panStart, "Dragging the terrain pans the map");
        var originalZoom = canvas.Zoom;
        await PressHint("Zoom in"); Check(canvas.Zoom > originalZoom, "Map zooms in");
        await PressHint("Zoom out"); Check(Math.Abs(canvas.Zoom - originalZoom) < .01, "Map zooms back out");
        await ChooseAdventureSite("supply-1"); await Press("Travel & gather"); await FinishTravel();
        Check(state.Gold == initialGold + 38 && state.Food == initialFood, "Treasury credits the advertised reward once");
        await Capture("02-treasury-collected"); AuditText("Adventure / treasury");
        await Press("Travel here"); await FinishTravel();
        Check(state.Gold == initialGold + 38, "Revisiting does not duplicate a cache reward");
        var oldReveal = state.GetAdventureRevealAreas("city").Max(x => x.Z);
        await ChooseAdventureSite("landmark-1"); await Press("Scout from tower"); await FinishTravel();
        Check(state.GetAdventureRevealAreas("city").Max(x => x.Z) > oldReveal, "Watchtower widens the revealed terrain");
        await Capture("03-watchtower-reveal"); AuditText("Adventure / watchtower");
        Check(state.CanVisitAdventureSite("hidden-city"), "Watchtower discovers a previously hidden treasury");
        await ChooseAdventureSite("hidden-city"); await Press("Travel & gather"); await FinishTravel();
        Check(state.Gold == initialGold + 76, "Discovered treasure grants its real resource reward");
        Check(GameData.Stages.Where(x => !state.IsAdventureBoss(x.StageNumber)).All(x => state.IsCampaignStageUnlocked(x.StageNumber)), "Every regular leader is available independently across all districts");
        Check(GameData.Stages.Where(x => state.IsAdventureBoss(x.StageNumber)).All(x => !state.IsCampaignStageUnlocked(x.StageNumber)), "Every final boss starts behind its district victory gate");
        var revealBeforeWalk = state.GetAdventureRevealAreas("city").Count;
        var destination = new Vector2(650, 600);
        canvas.FocusPoint(destination); await Wait(.1);
        var click = canvas.GlobalPosition + destination * canvas.Zoom + canvas.MapOffset;
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        await FinishTravel();
        Check(state.GetAdventureHeroPosition("city").DistanceTo(destination) < 2, "Clicking empty terrain moves the caravan off the roads");
        Check(state.GetAdventureRevealAreas("city").Count > revealBeforeWalk, "Free movement reveals fog along the travelled path");
        Check(state.Gold == initialGold + 76 && state.Food == initialFood && state.TotalStarsEarned == 0, "Free movement charges nothing and cannot clear encounters or collect passing caches");
        var positionBeforeExplore = state.GetAdventureHeroPosition("city");
        await Press("Explore"); await FinishTravel();
        Check(state.GetAdventureHeroPosition("city") != positionBeforeExplore && state.Food == initialFood, "Explore discovers another location for free without a victory");
        await ChooseAdventureSite("landmark-2"); await Press("Kindle shrine"); await FinishTravel();
        Check(state.GetAdventureStartingCourageBonus(2) == 3 && state.GetAdventureStartingCourageBonus(7) == 0, "Shrine bonus belongs only to its district");
        await Press("Travel here"); await FinishTravel();
        Check(state.GetAdventureStartingCourageBonus(2) == 3, "Revisiting a shrine cannot stack its bonus");
        Check(SaveSystem.Instance.TryLoad(out var checkpoint) && checkpoint.VisitedAdventureSites.Contains("landmark-2") && checkpoint.AdventureHeroNodes["city"] == "landmark-2", "Exploration and caravan position persist to disk");
        var restore = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        restore.Invoke(state, new object[] { checkpoint });
        Check(state.HasVisitedAdventureSite("supply-1") && state.GetAdventureStartingCourageBonus(2) == 3 && state.GetAdventureHeroNode("city").Id == "landmark-2", "Reload restores claims, bonuses and caravan position");
        await Open("MapMenu"); await Capture("04-restored-expedition"); AuditText("Adventure / restored");
        await ChooseAdventureSite("leader-2"); await Press("Prepare battle"); await Wait(1.5);
        Check(GetTree().CurrentScene is LoadoutMenu && state.SelectedStage == 2, "Rival leader opens the matching battle preparation");
        await Capture("05-leader-challenge"); AuditText("Adventure / leader preparation");
        await Press("Deploy");
        Check(GetTree().CurrentScene is BattleController, "Leader challenge enters a real battle");
        Check(Walk(GetTree().CurrentScene).OfType<Label>().Any(x => x.Text.Contains("Shrine blessing: +3")), "Battle receives the district shrine blessing");
        var courage = (float)typeof(BattleController).GetField("_courage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(GetTree().CurrentScene)!;
        Check(courage >= GameData.Combat.CourageStart + state.GetCampaignScoutStartingCourageBonus(2) + 3, "Shrine adds real starting courage to combat");
        await Capture("06-blessed-battle");
        await PressHint("Retreat");
        restore.Invoke(state, new object[] { checkpoint });
        var legacy = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(checkpoint))!;
        legacy.Version = 39; legacy.HighestUnlockedStage = 8; legacy.SelectedStage = 8; legacy.VisitedAdventureSites = null; legacy.AdventureHeroNodes = null;
        restore.Invoke(state, new object[] { legacy });
        Check(state.HighestUnlockedStage == 8 && state.SelectedStage == 8 && state.GetAdventureRevealAreas("harbor").Count > 0, "Older saves preserve progress and derive explored terrain");
        Check(state.GetAdventureStartingCourageBonus(8) == 0, "Older saves do not invent collected shrine bonuses");
        restore.Invoke(state, new object[] { checkpoint });
        await ReviewFreeRoamProgression(checkpoint);
        restore.Invoke(state, new object[] { checkpoint });
        state.UnlockNextStage(state.MaxStage - 1);
        Check(!state.CanPrestige && !state.IsHardModeUnlocked, "Exploration alone cannot unlock post-campaign rewards before the final boss victory");
        state.TryVisitAdventureSite("camp-city", out _); state.SetSelectedStage(1);
        await Open("MapMenu");
        canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        var lateSite = AdventureMapCatalog.ForMap("city").Last(x => x.Kind == AdventureSiteKind.Shrine);
        canvas.TravelTo(lateSite, () => state.TryVisitAdventureSite(lateSite.Id, out _));
        await FinishTravel();
        Check(state.GetAdventureHeroNode("city").Id == lateSite.Id, "Travel handles nonconsecutive stages within a district");
        var pendingCache = AdventureMapCatalog.Find("supply-3");
        var goldBeforeCancel = state.Gold;
        canvas.TravelTo(pendingCache, () => state.TryVisitAdventureSite(pendingCache.Id, out _));
        await Wait(.1); await Open("MainMenu");
        var stoppedPosition = state.GetAdventureHeroPosition("city");
        state.ReloadFromDisk();
        Check(!state.HasVisitedAdventureSite(pendingCache.Id) && state.Gold == goldBeforeCancel, "Leaving during travel does not collect a cache before arrival");
        Check(stoppedPosition.DistanceTo(pendingCache.Point) > 1 && state.GetAdventureHeroPosition("city").DistanceTo(stoppedPosition) < 1, "Interrupted travel saves the actual position without teleporting to its destination");
        foreach (var map in GameData.Stages.Select(x => x.MapId).Distinct())
        {
            state.SetSelectedStage(GameData.GetStagesForMap(map).Last().StageNumber);
            await Open("MapMenu");
            var menu = GetTree().CurrentScene;
            var select = typeof(MapMenu).GetMethod("SelectSite", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var node in AdventureMapCatalog.ForMap(map))
            {
                state.MoveAdventureHero(map, node.Point);
                select.Invoke(menu, new object[] { node }); await Wait(.08);
                AuditText($"Adventure / {node.Id}");
            }
            await Capture("district-" + map);
        }
        System.IO.File.WriteAllText(_output + "/text-audit.json", System.Text.Json.JsonSerializer.Serialize(_textAudit, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"ADVENTURE_REVIEW_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
    private async Task ReviewFreeRoamProgression(GameSaveData checkpoint)
    {
        var state = GameState.Instance;
        var restore = typeof(GameState).GetMethod("ApplySavedData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var funded = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(checkpoint))!;
        funded.Food = 100; // Isolate ordering and boss rules from late-stage entry costs.
        restore.Invoke(state, new object[] { funded });
        var leaders = GameData.GetStagesForMap("city").OrderBy(x => x.StageNumber).ToArray();
        var boss = leaders.Last().StageNumber;
        var late = leaders[^2].StageNumber;
        Check(late > state.HighestUnlockedStage && state.CanStartCampaignBattle(late, out _), "An uncleared later encounter can be challenged before early encounters");
        state.SetSelectedStage(late);
        state.MoveAdventureHero("city", AdventureMapCatalog.Leader(late).Point);
        var freePoint = new Vector2(1111, 666);
        state.MoveAdventureHero("city", freePoint);
        state.ReloadFromDisk();
        Check(state.SelectedStage == late && state.GetAdventureHeroPosition("city").DistanceTo(freePoint) < 1, "Full save reload preserves an out-of-order selection and off-road position");
        Check(state.IsAdventureSiteDiscovered($"leader-{late}"), "Exploration knowledge survives a full save reload");
        state.ApplyDefeat(late);
        Check(state.CanStartCampaignBattle(2, out _) && state.GetStageStars(late) == 0, "Losing a later encounter leaves other leaders available");
        state.ApplyRetreat(2);
        Check(state.CanStartCampaignBattle(3, out _), "Retreating leaves another encounter available");
        Check(!state.TrySpendStageEntryFood(boss, out _), "The battle entry guard rejects the final boss before its gate opens");
        var food = state.Food;
        Check(!state.TryVisitAdventureSite($"leader-{boss}", out _) && state.Food == food, "Map travel cannot bypass the boss gate or charge a rejected battle");
        foreach (var leader in leaders.Take(leaders.Length - 1).Reverse())
        {
            state.TryVisitAdventureSite($"leader-{leader.StageNumber}", out _);
        }
        Check(!state.IsCampaignStageUnlocked(boss), "Visiting every regular leader does not unlock the boss");
        foreach (var leader in leaders.Skip(1).Take(leaders.Length - 2).Reverse())
        {
            state.ApplyVictory(leader.StageNumber, 0, 0, 1);
            Check(!state.IsCampaignStageUnlocked(boss), "Out-of-order victories keep the boss sealed while one leader remains");
        }
        state.ApplyVictory(late, 0, 0, 3);
        Check(state.GetAdventureBossRemainingLeaders(boss) == 1, "Replaying one leader cannot replace a missing victory");
        state.ReloadFromDisk();
        Check(!state.IsCampaignStageUnlocked(boss), "Partial boss requirements survive reload");
        await Open("MapMenu");
        await ChooseAdventureSite($"leader-{boss}");
        Check(Walk(GetTree().CurrentScene).OfType<Button>().Any(x => x.Text == "Boss gate sealed" && x.Disabled), "Discovered boss has a disabled gate with remaining-leader progress");
        await Capture("07-boss-gate-sealed"); AuditText("Adventure / boss locked");
        state.ApplyVictory(leaders[0].StageNumber, 0, 0, 1);
        Check(state.IsCampaignStageUnlocked(boss) && state.CanStartCampaignBattle(boss, out _), "Defeating the final remaining regular leader opens the boss challenge");
        Check(!state.IsCampaignStageUnlocked(GameData.GetStagesForMap("harbor").Max(x => x.StageNumber)), "Victories in one district do not unlock another district's boss");
        state.ReloadFromDisk();
        Check(state.IsCampaignStageUnlocked(boss), "An opened boss gate survives full save reload");
        await Open("MapMenu"); await ChooseAdventureSite($"leader-{boss}");
        await Press("Prepare battle"); await Wait(.5);
        Check(GetTree().CurrentScene is LoadoutMenu && state.SelectedStage == boss, "Unlocked boss opens the matching loadout");
        await Press("Deploy");
        Check(GetTree().CurrentScene is BattleController, "Unlocked boss enters a real battle");
        await PressHint("Retreat");
        restore.Invoke(state, new object[] { checkpoint });
        var legacy = System.Text.Json.JsonSerializer.Deserialize<GameSaveData>(System.Text.Json.JsonSerializer.Serialize(checkpoint))!;
        legacy.Version = 40;
        legacy.StageStars = new int[state.MaxStage]; legacy.StageStars[boss - 1] = 1;
        restore.Invoke(state, new object[] { legacy });
        Check(state.IsCampaignStageUnlocked(boss), "A boss defeated in an older save remains replayable");
        restore.Invoke(state, new object[] { checkpoint });
    }
    private async Task ChooseAdventureSite(string id)
    {
        var canvas = Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Single();
        if (!GameState.Instance.IsAdventureSiteDiscovered(id))
        {
            canvas.TravelToPoint(AdventureMapCatalog.Find(id).Point); await FinishTravel();
        }
        canvas.FocusPoint(AdventureMapCatalog.Find(id).Point); await Wait(.1);
        var token = Walk(GetTree().CurrentScene).OfType<AdventureMapToken>().Single(x => x.Site.Id == id && x.IsVisibleInTree());
        token.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.2);
    }
    private async Task FinishTravel()
    {
        for (var i = 0; i < 60 && Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Any(x => x.IsTravelling); i++) await Wait(.1);
        Check(!Walk(GetTree().CurrentScene).OfType<MapPathCanvas>().Any(x => x.IsTravelling), "Caravan reaches the chosen site");
        await Wait(.1);
    }
}
