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
            // Each zone battles in front of one Blender backdrop rendered for the exact battle world.
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                var backdrop = WorldEnvironmentArt.LoadZoneBackdrop(zone);
                Check(backdrop is { Layers.Count: 3 }, $"{zone} has a layered Blender battle backdrop");
                if (backdrop == null) continue;
                var near = backdrop.Layers[^1];
                Check(backdrop.Layers.All(l => Mathf.IsEqualApprox(l.Texture.GetWidth() / (float)l.Texture.GetHeight(), l.Rect.Size.X / l.Rect.Size.Y, .01f))
                    && near.Texture.GetWidth() >= 4096, $"{zone} layers import at full resolution and the proportions of their world rects");
                Check(near.Parallax == 1 && backdrop.Layers.Select(l => l.Parallax).SequenceEqual(backdrop.Layers.Select(l => l.Parallax).OrderBy(p => p)),
                    $"{zone} layers run far to near and the road layer is locked to the field");
                Check(backdrop.Layers.All(l => l.Rect.Position.X <= 0 && l.Rect.End.X >= world) && near.Rect.Encloses(ground), $"{zone} backdrop covers the whole battle world and band");
                var path = ProjectSettings.GlobalizePath(WorldEnvironmentArt.BackdropDirectory + zone + ".png");
                Check(hashes.Add(Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(path)))), $"{zone} backdrop is a distinct image");
            }
            for (var material = 0; material < 9; material++)
                Check(AdventureAtlasArt.Material(material) != null, $"Map material {material} loads");
            for (var sprite = 0; sprite < 29; sprite++)
                Check(AdventureAtlasArt.Sprite(sprite) != null, $"Map scenery {sprite} loads");
            var plate = WorldEnvironmentArt.CoverRect(new Vector2(1984,800),new Rect2(0,ground.Position.Y-180,combat.BattlefieldLeft+combat.BattlefieldRight,267));
            Check(Mathf.IsEqualApprox(plate.Size.X / 1984, plate.Size.Y / 800), "Battle scenery preserves its authored proportions");
            Check(plate.Position.X <= 0 && plate.End.X >= combat.BattlefieldLeft + combat.BattlefieldRight, "Scene mapping covers the whole one-screen field");
            Check(Enumerable.Range(0,AdventureTerrain.CellCount).All(c => AdventureTerrain.Neighbors(c).All(n => AdventureTerrain.Diamond(c).Intersect(AdventureTerrain.Diamond(n)).Count() == 2)), "Neighboring map tiles share their exact drawn edges");
            foreach (var zone in AssetCoverageCatalog.RouteIds)
            {
                state.ResetProgress(); state.SetAnalyticsConsent(false); state.SetShowHints(false);
                var zoneIndex = Array.IndexOf(AssetCoverageCatalog.RouteIds, zone);
                if (zoneIndex > 0)
                    state.ApplyVictory(GameData.GetStagesForMap(AssetCoverageCatalog.RouteIds[zoneIndex - 1]).Max(s => s.StageNumber), 0, 0, 1);
                var stage = GameData.GetStagesForMap(zone).First().StageNumber; state.SetSelectedStage(stage);
                var map = (MapMenu)await LiveUiReview.Open(this, "MainMenu"); await Wait(.4);
                var canvas = Read<MapPathCanvas>(map,"_mapCanvas");
                Check(AdventureAtlasArt.Material(AdventureAtlasArt.GroundMaterial(zone)) != null && canvas.ActiveMapId == zone, zone + " artwork is connected to its map screen");
                await Capture("zone-" + zone + "-fresh");
                var explored = state.BuildSaveData();
                explored.AdventureOpenTiles = AdventureTileCatalog.ForMap(zone).Select(tile => tile.Id).ToArray();
                state.RestoreCloudSave(explored);
                canvas.RefreshKnowledge(); canvas.ChangeZoom(.01f); canvas.FocusOverview();
                Check(AdventureTileCatalog.ForMap(zone).All(state.IsAdventureTileOpen), zone + " explored capture opens the current tile map");
                await Wait(); await Capture("zone-" + zone + "-explored"); map.QueueFree(); await Wait();
                state.PrepareCampaignBattle();
                var battle = (BattleController)await LiveUiReview.Open(this, "Battle"); battle.SetPhysicsProcess(false); await Wait(.4);
                Check(Read<Texture2D>(battle,"_stageArtwork") is { } drawn && drawn.ResourcePath == WorldEnvironmentArt.BackdropDirectory + zone + ".png",
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
