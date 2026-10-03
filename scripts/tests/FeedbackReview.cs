using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class FeedbackReview : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _failures;
    private static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static void Write(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj,args);
    private void Check(bool ok, string description) { GD.Print($"FEEDBACK_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async Task Capture(string id)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false);
        using var pixels = GetViewport().GetTexture().GetImage();
        var folder = ProjectSettings.GlobalizePath("res://artifacts/feedback-review/" + (OS.GetCmdlineUserArgs().Contains("--mobile-preview") ? "phone" : "desktop")); System.IO.Directory.CreateDirectory(folder);
        pixels.SavePng(folder + "/" + id + ".png");
    }
    private static IEnumerable<Node> Walk(Node node)
    { yield return node; foreach (var child in node.GetChildren()) foreach (var descendant in Walk(child)) yield return descendant; }
    private async Task<T> Open<T>(string scene) where T : Node
    { var node = ResourceLoader.Load<PackedScene>("res://scenes/" + scene + ".tscn").Instantiate<T>(); AddChild(node); await Wait(.5); return node; }
    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--save-suffix=feedback-review-"))) throw new Exception("Use an isolated feedback-review save.");
            GetTree().AutoAcceptQuit = false;
            GetWindow().Mode = Window.ModeEnum.Windowed;
            var dimensions = OS.GetCmdlineUserArgs().Contains("--mobile-preview") ? new Vector2I(844,390) : new Vector2I(1280,720);
            GetWindow().Size = dimensions;
            GetWindow().ContentScaleSize = new Vector2I(1280,720);
            GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            await Wait();
            var state = GameState.Instance; state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false);
            Check(state.ActiveDeckUnitIds.SequenceEqual(new[] { GameData.PlayerBrawlerId }) && state.GetOwnedPlayerUnits().Count == 1, "A new game starts with only a swordsman");
            Check(state.GetOwnedPlayerSpells().Count == 0 && state.CanStartBattle(out _), "One unit and no spells is a valid starting squad");
            Check(state.DeckSizeLimit == 6 && state.SpellDeckSizeLimit == 5, "Expanded unit and spell capacities");
            Check(GameData.Stages.All(s => state.GetStageEntryFoodCost(s.StageNumber) == 4 && s.RewardFood == 0), "Bosses and ordinary stages cost the same, and wins do not refill entry costs");
            Check(state.TrySpendStageEntryFood(1, out _) && state.Food == 20, "Battle entry spends food once");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Check(!state.RefreshFoodRecharge(now + 299) && state.Food == 20, "Food does not recharge early");
            Check(state.RefreshFoodRecharge(now + 300) && state.Food == 22, "Two food recharges after five minutes");
            Check(state.RefreshFoodRecharge(now + 600) && state.Food == 24 && !state.RefreshFoodRecharge(now + 900), "Offline recharge stops at its cap");
            state.ResetProgress(); state.SetShowHints(false);
            Check(state.TryBuyFoodRefill(out _) && state.Gold == 20 && state.Food == 34 && !state.TryBuyFoodRefill(out _), "Gold refills credit food and refuse insufficient funds");
            state.ResetProgress(); state.SetShowHints(false);
            var from = AdventureTerrain.Cell(state.GetAdventureHeroPosition("city")); var discovery = AdventureDiscoveryCatalog.ForMap("city")[0]; var to = discovery.Cell;
            var path = AdventureTerrain.Path("city", from, to);
            Check(path.Length > 0 && path.All(c => AdventureTerrain.Walkable("city", c)) && path.Zip(path.Skip(1)).All(p => AdventureTerrain.Neighbors(p.First).Contains(p.Second)), "Travel paths only cross neighboring ground tiles");
            Check(Enumerable.Range(0,AdventureTerrain.CellCount).All(c => AdventureTerrain.Cell(AdventureTerrain.Point(c)) == c), "Drawn isometric tile centers match movement coordinates");
            var cost = state.GetAdventureTravelFoodCost("city", path);
            Check(!state.TryBeginAdventureTravel("city", new[] { from, to }, out _) && state.Food == 24, "Invalid routes cannot move the hero or spend food");
            Check(cost == 0 && state.TryBeginAdventureTravel("city", path, out _) && state.Food == 24, "Exploration is free before walking the route");
            foreach (var cell in path.Skip(1)) { state.TryPayAdventureStep("city",cell,out _); state.CompleteAdventureStep("city",cell); }
            Check(state.Food == 24 + discovery.Amount, "Entering tiles is free and reaching provisions grants their full amount");
            var blocked = Enumerable.Range(0,AdventureTerrain.CellCount).First(c => !AdventureTerrain.Walkable("city",c));
            Check(state.GetAdventureTravelFoodCost("city", path) == 0 && !state.MoveAdventureHero("city", AdventureTerrain.Point(blocked)), "Walked routes are free and obstacles cannot be crossed");
            var save = state.BuildSaveData(); state.ReloadFromDisk();
            Check(state.GetOwnedPlayerSpells().Count == 0, "Reloading does not grant free starter spells");
            Check(state.GetAdventureHeroPosition("city") == new Vector2(save.AdventureHeroPositions["city"][0], save.AdventureHeroPositions["city"][1]) && state.Food == save.Food, "Exploration progress and food balance persist across reload");
            state.ResetProgress(); state.SetShowHints(false);
            var menu = await Open<MainMenu>("MainMenu"); await Capture("01-title");
            AccountDialog.Show(menu); await Wait(.5);
            var account = Walk(menu).OfType<AccountDialog>().Single();
            Check(Walk(account).OfType<ScrollContainer>().Any() && Walk(account).OfType<Button>().Any(b => b.Text == "Continue with Google"), "Account offers email and Google in a scrollable dialog");
            await Capture("01b-account"); account.EmitSignal(AcceptDialog.SignalName.Confirmed); await Wait();
            menu.QueueFree(); await Wait();
            var map = await Open<MapMenu>("MapMenu"); await Capture("02-map");
            Check(!Walk(map).OfType<MapPathCanvas>().Single().IsTravelling, "Map opens with a stable selection");
            map.QueueFree(); await Wait();
            var shop = await Open<ShopMenu>("ShopMenu"); await Capture("03-squad");
            // The armory is a two-column scrolling collection of portrait tiles beside one animated model of the selected unit.
            var squadGrid = Walk(shop).OfType<GridContainer>().FirstOrDefault(g => g.GetParent() is ScrollContainer && g.GetChildCount() == GameData.GetPlayerUnits().Count);
            Check(squadGrid != null && squadGrid.Columns == 2, "Squad browser displays every unit in one scrolling grid");
            Check(squadGrid != null && !Walk(squadGrid).OfType<UnitModelPreview>().Any() && squadGrid.GetChildren().OfType<Button>().All(b => b.GetChildren().OfType<TextureRect>().Any()), "Squad browser uses portraits instead of side-facing models");
            var rites = Walk(shop).OfType<Button>().Single(b => b.Text == "Spells"); rites.ButtonPressed = true; rites.EmitSignal(BaseButton.SignalName.Pressed); await Wait();
            Check(Walk(shop).OfType<GridContainer>().Any(g => g.Columns == 2 && g.GetParent() is ScrollContainer && g.GetChildCount() == GameData.GetPlayerSpells().Count), "Spell browser also displays its full inventory grid");
            await Capture("03b-spells");
            shop.QueueFree(); await Wait();
            var loadout = await Open<LoadoutMenu>("LoadoutMenu"); await Capture("04-loadout");
            Check(!Walk(loadout).OfType<UnitModelPreview>().Any(), "Preparation uses unit icons"); loadout.QueueFree(); await Wait();
            state.SetSelectedStage(1); state.PrepareCampaignBattle();
            var battle = await Open<BattleController>("Battle"); battle.SetPhysicsProcess(false); await Capture("05-battle");
            Check(!Walk(battle).Any(n => n.Name == "CampaignFieldNavigation" || n.Name == "BattlefieldNavigation"), "Battle minimap and capture navigation are removed");
            Check(!Walk(battle).OfType<Button>().Any(b => b.IsVisibleInTree() && b.Text == "1x"), "No battle speed control is shown");
            var units = Read<List<Unit>>(battle, "_units");
            var archer = (Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit("player_shooter")),new Vector2(450,340));
            var enemy = (Unit)Call(battle,"SpawnUnit",Team.Enemy,new UnitStats(GameData.GetUnit("enemy_walker")),new Vector2(490,340));
            var before = archer.Position; Call(battle,"SimulateRangedPositioning",archer,enemy,.5f,true);
            Check(archer.Position == before, "Ranged units hold their firing position at close range");
            archer.FaceCombatTarget(enemy); archer.TryAttack(enemy); archer.TickAttackTimer(archer.AttackContactSeconds);
            Check(archer.Position == before && (Vector2)Call(archer,"ContactDrawOffset") == Vector2.Zero, "Attack animation cannot shift a unit's feet");
            var swordsman = (Unit)Call(battle,"SpawnUnit",Team.Player,new UnitStats(GameData.GetUnit("player_brawler")),new Vector2(500,400));
            swordsman.MoveToward(new Vector2(400,400),.1f,84,2476,108,572);
            Check((float)Call(swordsman,"GetFacing") == -1 && swordsman.Speed == 60, "The slower swordsman turns while walking back");
            var enemyBase = (Vector2)typeof(BattleController).GetProperty("EnemyBaseCorePosition",Hidden).GetValue(battle);
            swordsman.Position = enemyBase;
            Call(battle,"TryAttackBase",swordsman); swordsman.TickAttackTimer(2);
            enemy.Position = enemyBase - new Vector2(100,0);
            Write(battle,"_enemyBaseHealth",1f);
            var basePosition = swordsman.Position;
            Call(battle,"SimulateUnits",.01f); swordsman.TickAttackTimer(swordsman.AttackContactSeconds);
            Check(swordsman.Position == basePosition && Read<float>(battle,"_enemyBaseHealth") <= 0, "A unit finishing a base keeps attacking through its final hit");
            Check(Read<bool>(battle,"_battleEnded") && units.Any(u => u.Team == Team.Enemy && !u.IsDead), "Destroying a base ends battle even with remaining defenders and waves");
            battle.QueueFree(); await Wait();
            var expanded = state.BuildSaveData();
            expanded.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); expanded.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
            expanded.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(6).ToArray(); expanded.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(5).ToArray();
            state.RestoreCloudSave(expanded); state.SetShowHints(false); state.PrepareCampaignBattle();
            battle = await Open<BattleController>("Battle"); battle.SetPhysicsProcess(false);
            Check(Walk(battle).OfType<BattleActionCard>().Count() == 11, "Expanded battle displays all six units and five spells");
            Check(Walk(battle).OfType<BattleActionCard>().All(c => c.GetParent().GetParent().GetParent() is ScrollContainer), "Expanded cards share a horizontal scroller");
            await Capture("06-expanded-battle"); battle.QueueFree(); await Wait();
            await CheckAccountClient();
            Check(ResourceLoader.Load<AudioStreamOggVorbis>("res://assets/music/title.ogg").GetLength() > 40 &&
                ResourceLoader.Load<AudioStreamOggVorbis>("res://assets/music/battle.ogg").GetLength() > 30, "Music loops are real, complete tracks");
            Check(Read<Dictionary<string,AudioStream>>(AudioDirector.Instance,"_authoredOverrides").Count >= 20, "Authored impacts, spells and interface sounds are loaded");
            var music = MusicPlayer.Instance.GetChildren().OfType<AudioStreamPlayer>().ToArray();
            state.SetAudioMuted(true);
            Check(music.All(p => !p.Playing), "Mute stops background music immediately");
            state.SetAudioMuted(false); await Wait(.1);
            Check(music.Any(p => p.Playing && p.Stream is AudioStreamOggVorbis loop && loop.Loop), "Unmute resumes a looping music track");
            state.SetMusicVolumePercent(0); await Wait(.1);
            Check(music.Where(p => p.Playing).All(p => p.VolumeDb <= -80), "Music volume reaches silence");
            state.SetMusicVolumePercent(50);
            Check(!Walk(GetTree().Root).OfType<Control>().Any(c => c.IsVisibleInTree() && c.TooltipText.Length > 0), "Touch screens have no hover tooltips");
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        GD.Print($"FEEDBACK_REVIEW_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
