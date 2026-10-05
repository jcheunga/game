using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class UiReviewSmoke
{
    private async Task ReviewBattleLighting()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/battle-lighting");
        System.IO.Directory.CreateDirectory(_output);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        object Call(BattleController battle, string method, params object[] args) =>
            typeof(BattleController).GetMethod(method, hidden)!.Invoke(battle, args);
        var state = GameState.Instance;
        var fixture = state.BuildSaveData();
        fixture.Food = 24; fixture.ReducedMotion = true; fixture.ShowFpsCounter = false;
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
        fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
        state.RestoreCloudSave(fixture);
        MobilePresentation.TestOverride = false;
        var world = GameData.Combat;
        foreach (var stage in GameData.Stages.GroupBy(stage => stage.MapId).Select(zone => zone.First()))
        {
            state.SetSelectedStage(stage.StageNumber); state.PrepareCampaignBattle(); await Open("Battle");
            var battle = (BattleController)GetTree().CurrentScene; battle.SetPhysicsProcess(false);
            if ((bool)typeof(BattleController).GetField("_battlePaused", hidden)!.GetValue(battle)!) Call(battle, "TogglePause");
            Call(battle, "ClearArmedSelection");
            var ground = new Vector2(world.PlayerBaseX, (world.BattlefieldTop + world.BattlefieldBottom) * .5f);
            var bases = Walk(battle).OfType<BattleBaseCanvas>().ToArray();
            Check(battle.YSortEnabled && bases.Length == 7
                && bases.Single(b => b.Name == "Caravan").Position == (Vector2)Call(battle, "get_WagonSortPosition")
                && bases.Single(b => b.Name == "Castle").Position == (Vector2)Call(battle, "get_CastleGround"),
                stage.MapId + ": bases, outworks and units sort from their ground positions");
            var unit = (Unit)Call(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), new Vector2(330, 340));
            Check(unit.GroundShadowsManaged && unit.EnvironmentTint == BattleLighting.ForZone(stage.MapId).Tint,
                stage.MapId + ": unit inherits shared environmental light and ground shadow pass");
            Call(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[1])), new Vector2(415, 410));
            Call(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), new Vector2(525, 365));
            // Overlap the wagon at two depths to inspect natural occlusion.
            Call(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), ground + new Vector2(36, -8));
            Call(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), ground + new Vector2(-22, 18));
            await Wait(.15);
            if (stage.MapId == "city")
            {
                foreach (var actor in Walk(battle).OfType<Unit>()) actor.SetProcess(false);
                var shadows = Walk(battle).OfType<BattleShadowCanvas>().Single();
                shadows.Visible = false; await Wait(.1); RenderingServer.ForceDraw();
                using var unshadowed = GetViewport().GetTexture().GetImage();
                shadows.Visible = true; await Wait(.1); RenderingServer.ForceDraw();
                using var shadowed = GetViewport().GetTexture().GetImage();
                int Darkened(Rect2I region)
                {
                    var count = 0;
                    for (var y = region.Position.Y; y < region.End.Y; y++)
                        for (var x = region.Position.X; x < region.End.X; x++)
                            if (unshadowed.GetPixel(x, y).R - shadowed.GetPixel(x, y).R > .008f) count++;
                    return count;
                }
                Check(Darkened(new Rect2I(335, 342, 35, 17)) > 30, "Animated silhouette casts a visible shadow beyond unit feet");
                Check(Darkened(new Rect2I(180, 335, 95, 50)) > 150, "Caravan shadow lands visibly on top of the terrain");
            }
            await Capture(stage.MapId + "-caravan");
            Call(battle, "SetBattleCameraX", world.BattlefieldRight);
            var enemyGround = new Vector2(world.EnemyBaseX, ground.Y);
            Call(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), enemyGround + new Vector2(-45, -8));
            Call(battle, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), enemyGround + new Vector2(-40, 25));
            await Wait(.1); await Capture(stage.MapId + "-castle");
        }
        MobilePresentation.TestOverride = true; GetWindow().Size = new Vector2I(844, 390);
        state.SetSelectedStage(1); state.PrepareCampaignBattle(); await Open("Battle");
        var phone = (BattleController)GetTree().CurrentScene; phone.SetPhysicsProcess(false);
        if ((bool)typeof(BattleController).GetField("_battlePaused", hidden)!.GetValue(phone)!) Call(phone, "TogglePause");
        Call(phone, "ClearArmedSelection");
        Call(phone, "SpawnUnit", Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), new Vector2(280, 340));
        Call(phone, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), new Vector2(420, 380));
        await Wait(.15); await Capture("phone-caravan");
        Call(phone, "ToggleMobileOverview"); await Wait(.15); await Capture("phone-overview");
        var canvases = Walk(phone).OfType<BattleBaseCanvas>().ToArray();
        Check(Walk(phone).OfType<BattleShadowCanvas>().Single().Visible && canvases.Count(b => b.Name == "Caravan" || b.Name == "Castle") == 2
            && canvases.Count(b => b.Name.ToString().StartsWith("Outwork")) == 5,
            "Phone view and overview retain the same grounded bases, outworks and shadow pass");
        foreach (var id in new[] { "war_wagon", "gatehouse" }.Concat(WagonSkinCatalog.GetAll().Where(skin => skin.Id != WagonSkinCatalog.DefaultSkinId).Select(skin => "war_wagon_" + skin.Id)))
        {
            var art = BattleStructureArt.Load(id);
            Check(art != null && art.Texture.GetSize() == new Vector2(1024, 1024), id + ": new native render and projected metadata load");
            if (art == null) continue;
            var ground = new Vector2(300, 340);
            Check(art.Point(ground, art.Anchor).DistanceTo(ground) < .001f &&
                art.Mounts.All(socket => art.Point(ground, socket).Y < ground.Y - 65 * GameData.Combat.StructureScale), id + ": ground anchor and roof sockets align");
            if (id.StartsWith("war_wagon"))
                Check(art.Door is { Frames: >= 2 } door && door.Strip != null && door.Foot.Y > door.Exit.Y
                    && new Rect2(Vector2.Zero, Vector2.One).Encloses(door.Rect), id + ": troop door frames and exit points load");
        }
        var reused = new Unit();
        reused.Setup(Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerRosterIds[0])), Vector2.Zero);
        reused.EnvironmentTint = BattleLighting.ForZone("gloamwood").Tint; reused.GroundShadowsManaged = true;
        reused.LocalLightTint = new Color(1.15f, 1.06f, .975f);
        reused.ResetForPool();
        Check(reused.EnvironmentTint == Colors.White && reused.LocalLightTint == Colors.White && !reused.GroundShadowsManaged,
            "Pooled troops release previous zone and lamp lighting"); reused.Free();
        MobilePresentation.TestOverride = null;
        await Open("MainMenu");
        GD.Print($"BATTLE_LIGHTING_REVIEW_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
