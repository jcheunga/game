using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task CaptureCamera(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--screenshots") || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://artifacts/battle-camera");
        System.IO.Directory.CreateDirectory(folder);
        GetViewport().GetTexture().GetImage().SavePng($"{folder}/{name}.png");
    }

    private async Task CheckBattleCamera()
    {
        try
        {
            MobilePresentation.TestOverride = false;
            var battle = await OpenBattle(1);
            var camera = Read<Camera2D>(battle, "_battleCamera");
            var tuning = GameData.Combat;
            var width = tuning.BattlefieldRight + tuning.BattlefieldLeft;
            var center = (tuning.BattlefieldTop + tuning.BattlefieldBottom) * .5f;
            var viewport = battle.GetViewportRect().Size;
            var zoom = camera.Zoom.X;
            var half = viewport.X / zoom * .5f;
            camera.ForceUpdateScroll();
            Vector2 Screen(Vector2 world) => battle.GetGlobalTransformWithCanvas() * world;
            Check(tuning.EnemyBaseX == width - tuning.PlayerBaseX && tuning.EnemySpawnX == width - tuning.PlayerSpawnX,
                "The field keeps matching margins at both bases");
            Check(Mathf.IsEqualApprox(zoom, viewport.X / tuning.ViewWidth) && Mathf.IsEqualApprox(zoom, (float)Invoke(battle, "get_BattleFitZoom"))
                && width >= tuning.ViewWidth * 2 - 1, "Soldiers are drawn at the set scale and the field runs at least two screens");
            var hud = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect();
            var bandTop = Screen(new Vector2(0, tuning.BattlefieldTop + tuning.SpawnVerticalPadding - 52));
            var bandBottom = Screen(new Vector2(0, tuning.BattlefieldBottom - tuning.SpawnVerticalPadding + 14));
            Check(bandTop.Y >= hud.End.Y - 1 && bandBottom.Y <= viewport.Y - 160 && Screen(new Vector2(0, center)).Y > viewport.Y * .5f,
                "The band runs low across the screen, clear of the meters and the card tray");
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Battle opens on the wagon side");
            var follow = Read<Button>(battle, "_battleFollowButton");
            Check(Read<bool>(battle, "_battleCameraFollow") && !follow.Visible, "The camera follows the fighting by default");
            var deck = Read<BattleDeckState>(battle, "_deck");
            Write(battle, "_courage", 100f);
            Write(battle, "_mana", Read<float>(battle, "_maxMana"));
            var deployments = Read<int>(battle, "_playerDeployments");
            void Wheel(MouseButton direction, float factor = 1) => battle._UnhandledInput(new InputEventMouseButton
                { ButtonIndex = direction, Factor = factor, Pressed = true, Position = new Vector2(600, 330) });
            await CaptureCamera("desktop-wagon");
            Wheel(MouseButton.WheelDown);
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 100 / zoom), "Wheel down pans toward the enemy by one screen step");
            Check(!Read<bool>(battle, "_battleCameraFollow") && follow.Visible, "Panning by hand pauses following and offers the follow button");
            Wheel(MouseButton.WheelLeft);
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Horizontal wheel clamps at the wagon edge");
            Wheel(MouseButton.WheelRight, 1000);
            Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "Horizontal wheel reaches and clamps at the far edge");
            Check(Read<int>(battle, "_playerDeployments") == deployments, "Scrolling never deploys");
            Check(Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect() == hud, "HUD stays fixed while the battlefield scrolls");
            Invoke(battle, "SetBattleCameraX", half + 100);
            battle._UnhandledInput(new InputEventPanGesture { Delta = new Vector2(2, .1f) });
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 100 + 96 / zoom), "Trackpad horizontal gestures pan the map");
            var beforeDrag = camera.Position.X;
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true, Position = new Vector2(600, 330) });
            battle._Input(new InputEventMouseMotion { Position = new Vector2(400, 330) });
            battle._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
            Check(Mathf.IsEqualApprox(camera.Position.X, beforeDrag + 200 / zoom) && !Read<bool>(battle, "_battleCameraDragging"),
                "Middle-drag moves the ground with the pointer and releases cleanly");
            battle._UnhandledInput(new InputEventKey { Keycode = Key.End, Pressed = true });
            Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "End jumps to the enemy base");
            Check(!battle.FindChildren("*", "Control", true, false).Any(n => n.Name == "BattlefieldNavigation" || n.Name == "CampaignFieldNavigation"),
                "Battle has no navigation strip or minimap");
            camera.ForceUpdateScroll();
            await CaptureCamera("desktop-gate");

            var target = new Vector2(tuning.EnemySpawnX - 60, center);
            var screen = Screen(target);
            Check(((Vector2)Invoke(battle, "ScreenToBattle", screen)).DistanceTo(target) < .01f, "Panned input resolves to the correct world point");
            var enemy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker"), healthScale: 20), target);
            var decoy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker"), healthScale: 20), new Vector2(tuning.PlayerSpawnX, center));
            var fireball = GameData.GetSpell("spell_fireball");
            Read<BattleSpellState>(battle, "_spellDeck").Initialize(new[] { fireball });
            Invoke(battle, "ArmSpell", fireball);
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen });
            Check(enemy.Health < enemy.MaxHealth && decoy.Health == decoy.MaxHealth, "A spell lands on the enemy under the pointer, not the raw screen point");
            Invoke(battle, "DeployPlayerUnit", deck.Roster[0]);
            var deployed = Read<List<Unit>>(battle, "_units").Single(u => u.Team == Team.Player);
            Check(deployed.Position == (Vector2)Invoke(battle, "get_WagonDoorExit"), "Deployment while panned still starts at the wagon's door");

            var oldPosition = camera.Position;
            Write(battle, "_battlePaused", true);
            Wheel(MouseButton.WheelUp);
            Check(camera.Position == oldPosition, "Pause blocks battlefield scrolling");
            Write(battle, "_battlePaused", false);
            battle._UnhandledInput(new InputEventKey { Keycode = Key.Home, Pressed = true });
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Home returns to the wagon");
            deployed.Position = new Vector2(width * .5f, center);
            enemy.TakeDamage(enemy.MaxHealth * 1000); decoy.TakeDamage(decoy.MaxHealth * 1000);
            Invoke(battle, "CleanupDeadUnits");
            battle._UnhandledInput(new InputEventKey { Keycode = Key.F, Pressed = true });
            Check(Read<bool>(battle, "_battleCameraFollow") && !follow.Visible, "F resumes following");
            for (var frame = 0; frame < 240; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
            camera.ForceUpdateScroll();
            var shown = Screen(deployed.Position);
            Check(shown.X > 0 && shown.X < viewport.X * .5f, "Following keeps the leading soldier in shot with room ahead");
            var straggler = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), new Vector2(tuning.PlayerSpawnX + 40, center));
            for (var frame = 0; frame < 240; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
            camera.ForceUpdateScroll();
            shown = Screen(straggler.Position);
            Check(shown.X > 0 && shown.X < viewport.X, "An enemy that slips past the front pulls the camera back to it");
            straggler.TakeDamage(straggler.MaxHealth * 1000);
            await CaptureCamera("desktop-follow");
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
            battle._Notification((int)Node.NotificationApplicationFocusOut);
            Check(!Read<bool>(battle, "_battleCameraDragging"), "Losing focus cancels dragging");
            // An ultrawide viewport must center a smaller world, not invert clamp limits.
            camera.Zoom = Vector2.One * .25f;
            Invoke(battle, "ClampBattleCamera", camera);
            Check(Mathf.IsEqualApprox(camera.Position.X, width * .5f), "World centers when the viewport sees beyond both edges");
            await CloseBattle(battle);

            MobilePresentation.TestOverride = true;
            battle = await OpenBattle(1);
            camera = Read<Camera2D>(battle, "_mobileCamera");
            Check(Mathf.IsEqualApprox(camera.Zoom.X, (float)Invoke(battle, "get_BattleFitZoom")) && viewport.X / camera.Zoom.X < width,
                "Phone combat opens at the soldier scale with the rest of the field off screen");
            var point = new Vector2(viewport.X * .5f,
                (Read<float>(battle, "_mobileFieldTop") + Read<float>(battle, "_mobileFieldBottom")) * .5f);
            var count = Read<int>(battle, "_playerDeployments");
            battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
            battle._Input(new InputEventScreenDrag { Index = 0, Position = point - new Vector2(10000, 0) });
            battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point - new Vector2(10000, 0) });
            Check(Mathf.IsEqualApprox(camera.Position.X + viewport.X / camera.Zoom.X * .5f, width), "Touch drag pans to the far end and clamps there");
            Check(Read<int>(battle, "_playerDeployments") == count, "A long touch drag cannot deploy");
            await CaptureCamera("mobile-gate");
            Invoke(battle, "ToggleMobileOverview"); // Resume follow after manual drag.
            Invoke(battle, "ToggleMobileOverview");
            Check(viewport.X / camera.Zoom.X >= width - .5f, "Mobile overview fits both ends of the field");
            var wagonScreen = battle.GetGlobalTransformWithCanvas() * new Vector2(tuning.PlayerBaseX, center);
            var gateScreen = battle.GetGlobalTransformWithCanvas() * new Vector2(tuning.EnemyBaseX, center);
            Check(wagonScreen.X >= 0 && gateScreen.X <= viewport.X &&
                gateScreen.Y >= Read<float>(battle, "_mobileFieldTop") && gateScreen.Y <= Read<float>(battle, "_mobileFieldBottom"),
                "Overview keeps both bases inside the unobstructed field");
            Check(!battle.FindChildren("*", "Control", true, false).Any(n => n.Name == "CampaignFieldNavigation"),
                "Phone combat has no capture controls or minimap");
            await CaptureCamera("mobile-overview");
            var oldWindowSize = GetWindow().Size;
            GetWindow().Size = new Vector2I(1000, 600);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(battle.GetViewportRect().Size.X / camera.Zoom.X >= width - .5f, "Resizing keeps the full map in mobile overview");
            GetWindow().Size = oldWindowSize;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Invoke(battle, "ToggleMobileOverview");
            Check(Mathf.IsEqualApprox(camera.Zoom.X, (float)Invoke(battle, "get_MobileCombatZoom")),
                "Leaving overview restores the chosen close view");
            await CloseBattle(battle);
        }
        finally { MobilePresentation.TestOverride = null; }
    }
}
