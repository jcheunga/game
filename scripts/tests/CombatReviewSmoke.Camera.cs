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
            var viewport = battle.GetViewportRect().Size;
            var half = viewport.X * .5f;
            Check(width == 2560 && tuning.EnemyBaseX == width - tuning.PlayerBaseX &&
                tuning.EnemySpawnX == width - tuning.PlayerSpawnX, "Double-width map preserves base and spawn margins");
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Battle opens on the wagon side at normal zoom");
            var hud = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect();
            var deck = Read<BattleDeckState>(battle, "_deck");
            Invoke(battle, "ArmPlayerUnit", deck.Roster[0]);
            Write(battle, "_courage", 100f);
            var deployments = Read<int>(battle, "_playerDeployments");
            void Wheel(MouseButton direction, float factor = 1) => battle._UnhandledInput(new InputEventMouseButton
                { ButtonIndex = direction, Factor = factor, Pressed = true, Position = new Vector2(600, 330) });
            await CaptureCamera("desktop-wagon");
            Wheel(MouseButton.WheelDown);
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 100), "Wheel down pans toward the enemy");
            Wheel(MouseButton.WheelUp, .5f);
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 50), "Wheel up respects fractional scrolling");
            Wheel(MouseButton.WheelLeft);
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Horizontal wheel clamps at the wagon edge");
            Wheel(MouseButton.WheelRight, 100);
            Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "Horizontal wheel reaches and clamps at the far edge");
            Check(Read<int>(battle, "_playerDeployments") == deployments, "Scrolling with a card armed never deploys");
            Check(Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect() == hud, "HUD stays fixed while the battlefield scrolls");
            GetViewport().PushInput(new InputEventMouseMotion { Position = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect().GetCenter() }, true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Factor = 1, Pressed = true, Position = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect().GetCenter() }, true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = false, Position = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect().GetCenter() }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "Scrolling over the HUD does not pan the battlefield");
            GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(600, 330) }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!battle.FindChildren("*", "Control", true, false).Any(n => n.Name == "BattlefieldNavigation" || n.Name == "CampaignFieldNavigation"),
                "Battle has no navigation strip or minimap");
            Invoke(battle, "SetBattleCameraX", half + 600);
            battle._UnhandledInput(new InputEventPanGesture { Delta = new Vector2(2, .1f) });
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 696), "Trackpad horizontal gestures pan the map");
            await CaptureCamera("desktop-middle");
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true, Position = new Vector2(600, 330) });
            battle._Input(new InputEventMouseMotion { Position = new Vector2(400, 330) });
            battle._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
            Check(Mathf.IsEqualApprox(camera.Position.X, half + 896) && !Read<bool>(battle, "_battleCameraDragging"), "Middle-drag pans without deploying and releases cleanly");
            battle._UnhandledInput(new InputEventKey { Keycode = Key.End, Pressed = true });
            Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "End jumps to the enemy base");

            var target = new Vector2(2100, 340);
            var screen = battle.GetGlobalTransformWithCanvas() * target;
            Check(((Vector2)Invoke(battle, "ScreenToBattle", screen)).DistanceTo(target) < .01f, "Panned input resolves to the correct world point");
            var enemy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker"), healthScale: 20), target);
            var decoy = (Unit)Invoke(battle, "SpawnUnit", Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker"), healthScale: 20), screen);
            var fireball = GameData.GetSpell("spell_fireball");
            Read<BattleSpellState>(battle, "_spellDeck").Initialize(new[] { fireball });
            Invoke(battle, "ArmSpell", fireball);
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen });
            Check(enemy.Health < enemy.MaxHealth && decoy.Health == decoy.MaxHealth, "Spell hits the visible enemy on the far half, not the raw screen coordinate");
            Invoke(battle, "ArmPlayerUnit", deck.Roster[0]);
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen });
            var deployed = Read<List<Unit>>(battle, "_units").Single(u => u.Team == Team.Player);
            Check(Mathf.IsEqualApprox(deployed.Position.X, tuning.PlayerSpawnX), "Deployment while panned still starts at the wagon");
            await CaptureCamera("desktop-gate");

            var oldPosition = camera.Position;
            Write(battle, "_battlePaused", true);
            Wheel(MouseButton.WheelUp);
            Check(camera.Position == oldPosition, "Pause blocks battlefield scrolling");
            Write(battle, "_battlePaused", false);
            battle._UnhandledInput(new InputEventKey { Keycode = Key.Home, Pressed = true });
            Check(Mathf.IsEqualApprox(camera.Position.X, half), "Home returns to the wagon");
            battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
            battle._Notification((int)Node.NotificationApplicationFocusOut);
            Check(!Read<bool>(battle, "_battleCameraDragging"), "Losing focus cancels dragging");
            deck.ReduceCooldowns(100);
            Write(battle, "_courage", 100f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            // An ultrawide viewport must center a smaller world, not invert clamp limits.
            camera.Zoom = Vector2.One * .25f;
            Invoke(battle, "RefreshBattleCamera");
            Check(Mathf.IsEqualApprox(camera.Position.X, width * .5f), "World centers when the viewport sees beyond both edges");
            await CloseBattle(battle);

            MobilePresentation.TestOverride = true;
            battle = await OpenBattle(1);
            camera = Read<Camera2D>(battle, "_mobileCamera");
            var point = new Vector2(viewport.X * .5f,
                (Read<float>(battle, "_mobileFieldTop") + Read<float>(battle, "_mobileFieldBottom")) * .5f);
            var count = Read<int>(battle, "_playerDeployments");
            battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
            battle._Input(new InputEventScreenDrag { Index = 0, Position = point - new Vector2(10000, 0) });
            battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point - new Vector2(10000, 0) });
            Check(camera.Position.X > 2000 && Mathf.IsEqualApprox(camera.Position.X + viewport.X / camera.Zoom.X * .5f, width), "Touch drag reaches the new far end and clamps there");
            Check(Read<int>(battle, "_playerDeployments") == count, "A long touch drag cannot deploy");
            await CaptureCamera("mobile-gate");
            Invoke(battle, "ToggleMobileOverview"); // Resume follow after manual drag.
            Invoke(battle, "ToggleMobileOverview");
            Check(viewport.X / camera.Zoom.X >= width - .01f, "Mobile overview fits both ends of the doubled map");
            var wagonScreen = battle.GetGlobalTransformWithCanvas() * new Vector2(tuning.PlayerBaseX, 340);
            var gateScreen = battle.GetGlobalTransformWithCanvas() * new Vector2(tuning.EnemyBaseX, 340);
            Check(wagonScreen.X >= 0 && gateScreen.X <= viewport.X &&
                gateScreen.Y >= Read<float>(battle, "_mobileFieldTop") && gateScreen.Y <= Read<float>(battle, "_mobileFieldBottom"),
                "Overview keeps both bases inside the unobstructed field");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!battle.FindChildren("*", "Control", true, false).Any(n => n.Name == "CampaignFieldNavigation"),
                "Phone combat has no capture controls or minimap");
            await CaptureCamera("mobile-overview");
            var oldWindowSize = GetWindow().Size;
            GetWindow().Size = new Vector2I(1000, 600);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(battle.GetViewportRect().Size.X / camera.Zoom.X >= width - .01f, "Resizing keeps the full map in mobile overview");
            GetWindow().Size = oldWindowSize;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Invoke(battle, "ToggleMobileOverview");
            Check(Mathf.IsEqualApprox(camera.Zoom.X, MobilePresentation.BattleZoom) && camera.Position.X > 2000,
                "Leaving overview restores the close view on the far half");
            deck = Read<BattleDeckState>(battle, "_deck");
            Invoke(battle, "ArmPlayerUnit", deck.Roster[0]);
            Write(battle, "_courage", 100f);
            await CloseBattle(battle);
        }
        finally { MobilePresentation.TestOverride = null; }
    }
}
