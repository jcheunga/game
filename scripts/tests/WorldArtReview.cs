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
    private static T Read<T>(object obj, string field) => (T)(obj is MapMenu ? typeof(MapMenu) : obj.GetType()).GetField(field,Hidden).GetValue(obj);
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
            var combat = GameData.Combat;
            var ground = new Rect2(combat.BattlefieldLeft,combat.BattlefieldTop,combat.BattlefieldRight-combat.BattlefieldLeft,combat.BattlefieldBottom-combat.BattlefieldTop);
            var world = combat.BattlefieldLeft + combat.BattlefieldRight;
            // Each zone battles in front of painted parallax layers: far, mid, ground and near behind the troops, front before them.
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                var backdrop = WorldEnvironmentArt.LoadZoneBackdrop(zone);
                Check(backdrop is { Layers.Count: 5 }, $"{zone} has a layered painted battle backdrop");
                if (backdrop == null) continue;
                var behind = backdrop.Layers.Where(l => !l.Front).ToArray();
                Check(backdrop.Layers.All(l => Mathf.IsEqualApprox(l.Texture.GetWidth() / (float)l.Texture.GetHeight(), l.Rect.Size.X / l.Rect.Size.Y, .01f)),
                    $"{zone} layers keep the proportions of their world rects");
                Check(behind[0].Parallax < behind[1].Parallax && behind[^1].Parallax == 1 && backdrop.Layers.Single(l => l.Front).Parallax > 1,
                    $"{zone} layers run far to near, the road is locked to the field and the foreground moves fastest");
                var road = behind.Single(l => l.Parallax == 1 && l.Tile && l.Rect.Size.Y > 120);
                Check(road.Rect.Position.Y <= ground.Position.Y && road.Rect.End.Y >= ground.End.Y, $"{zone} road layer spans the whole band");
                Check(backdrop.Layers.All(l => !l.Tile ? l.Rect.Position.X <= 280 && l.Rect.End.X >= 920 : true), $"{zone} untiled layers cover the camera's sweep");
                var path = ProjectSettings.GlobalizePath(WorldEnvironmentArt.RoyalBackdropDirectory + zone + "_far.png");
                Check(hashes.Add(Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path)))), $"{zone} backdrop is a distinct painting");
            }
            foreach (var zone in AssetCoverageCatalog.RouteIds)
                Check(ResourceLoader.Exists($"res://assets/world/royal/maps/{zone}.png") && Godot.FileAccess.FileExists($"res://assets/world/royal/maps/{zone}.json"),
                    $"{zone} has a painted campaign map");
            Check(Enumerable.Range(0,AdventureTerrain.CellCount).All(c => AdventureTerrain.Neighbors(c).All(n => AdventureTerrain.Diamond(c).Intersect(AdventureTerrain.Diamond(n)).Count() == 2)), "Neighboring map tiles share their exact drawn edges");
            // --zones=city,harbor limits the map and battle captures to those zones (the art checks above still cover all).
            var only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--zones="))?["--zones=".Length..].Split(',');
            foreach (var zone in AssetCoverageCatalog.RouteIds.Where(z => only == null || only.Contains(z)))
            {
                state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
                var zoneIndex = Array.IndexOf(AssetCoverageCatalog.RouteIds, zone);
                if (zoneIndex > 0)
                    state.ApplyVictory(GameData.GetStagesForMap(AssetCoverageCatalog.RouteIds[zoneIndex - 1]).Max(s => s.StageNumber), 0, 0, 1);
                var stage = GameData.GetStagesForMap(zone).First().StageNumber; state.SetSelectedStage(stage);
                var map = (MapMenu)await LiveUiReview.Open(this, "MainMenu"); await Wait(.4);
                var canvas = Read<MapPathCanvas>(map,"_mapCanvas");
                Check(Read<Texture2D>(canvas, "_painted") is { } painting && painting.ResourcePath.EndsWith($"/maps/{zone}.png") && canvas.ActiveMapId == zone,
                    zone + " painting is connected to its map screen");
                await Capture("zone-" + zone + "-fresh");
                var explored = state.BuildSaveData();
                explored.AdventureOpenTiles = AdventureTileCatalog.ForMap(zone).Select(tile => tile.Id).ToArray();
                state.RestoreCloudSave(explored);
                canvas.RefreshKnowledge(); canvas.ChangeZoom(.01f); canvas.FocusOverview();
                Check(AdventureTileCatalog.ForMap(zone).All(state.IsAdventureTileOpen), zone + " explored capture opens the current tile map");
                await Wait(); await Capture("zone-" + zone + "-explored"); map.QueueFree(); await Wait();
                state.PrepareCampaignBattle();
                var battle = (BattleController)await LiveUiReview.Open(this, "Battle"); battle.SetPhysicsProcess(false); await Wait(.4);
                Check(Read<Texture2D>(battle,"_stageArtwork") is { } drawn && drawn.ResourcePath.StartsWith(WorldEnvironmentArt.RoyalBackdropDirectory + zone + "_"),
                    $"Stage {stage:00} battles in front of the {zone} backdrop");
                await Capture($"battle-{stage:00}-normal");
                var camera = Read<Camera2D>(battle,Phone ? "_mobileCamera" : "_battleCamera");
                camera.Zoom = Vector2.One * .45f; camera.Position = new Vector2(GameData.Combat.BattlefieldLeft + GameData.Combat.BattlefieldRight,
                    GameData.Combat.BattlefieldTop + GameData.Combat.BattlefieldBottom) * .5f; camera.ForceUpdateScroll();
                await Wait(); await Capture($"battle-{stage:00}-overview"); battle.QueueFree(); await Wait();
            }
            if (!Phone && OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                var gallery = new WorldArtGallery { Size = new Vector2(1280,720) }; AddChild(gallery);
                foreach (var zone in AssetCoverageCatalog.RouteIds)
                    if (WorldEnvironmentArt.LoadZoneBackdrop(zone)?.Layers[^1] is { } art)
                        gallery.Entries.Add((GameData.GetStagesForMap(zone).First().StageNumber, art.Texture, RouteCatalog.Get(zone).Title,
                            new Rect2((ground.Position - art.Rect.Position) / art.Rect.Size, ground.Size / art.Rect.Size)));
                gallery.QueueRedraw(); await Wait(); await Capture("backdrop-gallery"); gallery.QueueFree(); await Wait();
            }
        }
        catch (Exception e) { GD.PrintErr(e); _failures++; }
        GD.Print($"WORLD_ART_RESULT: {_failures} failures"); await LiveUiReview.StopAudio(this); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
