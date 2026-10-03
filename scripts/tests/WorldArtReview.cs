using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

public partial class WorldArtReview : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _failures;
    private bool Phone => OS.GetCmdlineUserArgs().Contains("--mobile-preview");
    private bool Incomplete => OS.GetCmdlineUserArgs().Contains("--allow-incomplete");
    private static T Read<T>(object obj, string field) => (T)obj.GetType().GetField(field,Hidden).GetValue(obj);
    private void Check(bool ok, string message) { GD.Print($"WORLD_ART_CHECK: {(ok ? "PASS" : "FAIL")} {message}"); if (!ok) _failures++; }
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(false);
        using var pixels = GetViewport().GetTexture().GetImage();
        var directory = ProjectSettings.GlobalizePath("res://artifacts/world-art-review/" + (Phone ? "phone" : "desktop"));
        Directory.CreateDirectory(directory); Check(pixels.SavePng(directory + "/" + name + ".png") == Error.Ok, "Capture " + name);
    }
    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(s => s.StartsWith("--save-suffix=world-art-review-"))) throw new Exception("Use an isolated world-art-review save.");
            GetTree().AutoAcceptQuit = false;
            GetWindow().Mode = Window.ModeEnum.Windowed; GetWindow().Size = Phone ? new Vector2I(844,390) : new Vector2I(1280,720);
            GetWindow().ContentScaleSize = new Vector2I(1280,720); GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            var state = GameState.Instance; state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
            var hashes = new HashSet<string>();
            foreach (var stage in GameData.Stages)
            {
                var path = WorldEnvironmentArt.BattlePath(stage.StageNumber);
                if (Incomplete && !ResourceLoader.Exists(path)) continue;
                Check(ResourceLoader.Exists(path), $"Stage {stage.StageNumber:00} has its own environment");
                if (!ResourceLoader.Exists(path)) continue;
                using var texture = ResourceLoader.Load<Texture2D>(path,"",ResourceLoader.CacheMode.Ignore);
                Check(texture != null && texture.GetWidth() >= 1280 && texture.GetHeight() >= 600, $"Stage {stage.StageNumber:00} imports at usable resolution");
                Check(hashes.Add(Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(path))))), $"Stage {stage.StageNumber:00} is a distinct image");
            }
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                var path = WorldEnvironmentArt.ZonePath(zone);
                if (Incomplete && !ResourceLoader.Exists(path)) continue;
                Check(ResourceLoader.Exists(path), zone + " has its own overworld environment");
                if (!ResourceLoader.Exists(path)) continue;
                using var texture = ResourceLoader.Load<Texture2D>(path,"",ResourceLoader.CacheMode.Ignore);
                Check(texture != null && texture.GetWidth() >= 1280 && texture.GetHeight() >= 900, zone + " map imports at usable resolution");
                Check(hashes.Add(Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath(path))))), zone + " is a distinct image");
            }
            var ground = new Rect2(84,96,2392,488); var world = new Vector2(2560,720);
            var sourceSize = new Vector2(1984,800); var scene = WorldEnvironmentArt.BattleSceneRect(sourceSize,ground);
            Check(Mathf.IsEqualApprox(scene.Size.X / sourceSize.X, scene.Size.Y / sourceSize.Y), "Battle scenery preserves its authored proportions");
            Check(scene.Encloses(new Rect2(Vector2.Zero,world)), "Scene mapping covers the complete battle world without repeated panels");
            Check(Enumerable.Range(0,AdventureTerrain.CellCount).All(c => AdventureTerrain.Neighbors(c).All(n => AdventureTerrain.Diamond(c).Intersect(AdventureTerrain.Diamond(n)).Count() == 2)), "Neighboring map tiles share their exact drawn edges");
            Check(Enumerable.Range(0,AdventureTerrain.CellCount).SelectMany(AdventureTerrain.Diamond).Select(WorldEnvironmentArt.ZoneGroundUv).All(uv => uv.X >= 0 && uv.X <= 1 && uv.Y >= 0 && uv.Y <= 1), "Zone ground UVs remain inside the authored texture");
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                if (Incomplete && !ResourceLoader.Exists(WorldEnvironmentArt.ZonePath(zone))) continue;
                state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
                var zoneIndex = Array.IndexOf(AssetCoverageCatalog.RouteIds, zone);
                if (zoneIndex > 0)
                    state.ApplyVictory(GameData.GetStagesForMap(AssetCoverageCatalog.RouteIds[zoneIndex - 1]).Max(s => s.StageNumber), 0, 0, 1);
                var stage = GameData.GetStagesForMap(zone).First().StageNumber; state.SetSelectedStage(stage);
                var map = GD.Load<PackedScene>("res://scenes/MapMenu.tscn").Instantiate<MapMenu>(); AddChild(map); await Wait(.4);
                var canvas = Read<MapPathCanvas>(map,"_mapCanvas");
                Check(Read<Texture2D>(canvas,"_zoneArtwork") != null && canvas.ActiveMapId == zone, zone + " artwork is connected to its map screen");
                await Capture("zone-" + zone + "-fresh");
                for (var cell = 0; cell < AdventureTerrain.CellCount; cell++) if (AdventureTerrain.Walkable(zone,cell)) state.MoveAdventureHero(zone,AdventureTerrain.Point(cell),false);
                state.MoveAdventureHero(zone,AdventureMapCatalog.ForMap(zone).First().Point,false);
                canvas.RefreshKnowledge(); canvas.ChangeZoom(.01f); canvas.FocusPoint(AdventureMapCatalog.WorldSize * .5f);
                await Wait(); await Capture("zone-" + zone + "-explored"); map.QueueFree(); await Wait();
                if (Incomplete && !ResourceLoader.Exists(WorldEnvironmentArt.BattlePath(stage))) continue;
                state.PrepareCampaignBattle();
                var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>(); AddChild(battle); battle.SetPhysicsProcess(false); await Wait(.4);
                Check(Read<Texture2D>(battle,"_stageArtwork") != null, $"Stage {stage:00} screen loads the individual scene");
                await Capture($"battle-{stage:00}-normal");
                var camera = Read<Camera2D>(battle,Phone ? "_mobileCamera" : "_battleCamera");
                camera.Zoom = Vector2.One * .45f; camera.Position = world*.5f; camera.ForceUpdateScroll();
                await Wait(); await Capture($"battle-{stage:00}-overview"); battle.QueueFree(); await Wait();
            }
            if (!Phone && OS.GetCmdlineUserArgs().Contains("--capture"))
                for (var page = 0; page < 5; page++)
                {
                    var gallery = new WorldArtGallery { Size = new Vector2(1280,720) }; AddChild(gallery);
                    foreach (var stage in GameData.Stages.OrderBy(s => s.StageNumber).Skip(page*12).Take(12))
                    {
                        var texture = WorldEnvironmentArt.LoadBattle(stage.StageNumber);
                        if (texture != null) gallery.Entries.Add((stage.StageNumber,texture,stage.StageName));
                    }
                    gallery.QueueRedraw(); await Wait(); await Capture($"stage-gallery-{page+1}"); gallery.QueueFree(); await Wait(); GC.Collect();
                }
        }
        catch (Exception e) { GD.PrintErr(e); _failures++; }
        GD.Print($"WORLD_ART_RESULT: {_failures} failures"); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
