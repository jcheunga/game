using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            && width > tuning.ViewWidth && width <= tuning.ViewWidth * 1.5f,
            "Soldiers are drawn at the set scale and the field runs a little more than one screen");
        var hud = Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect();
        var bandTop = Screen(new Vector2(0, tuning.BattlefieldTop + tuning.SpawnVerticalPadding - 52));
        var bandBottom = Screen(new Vector2(0, tuning.BattlefieldBottom - tuning.SpawnVerticalPadding + 14));
        Check(bandTop.Y >= hud.End.Y - 1 && bandBottom.Y <= viewport.Y - 160 && Screen(new Vector2(0, center)).Y > viewport.Y * .5f,
            "The band runs low across the screen, clear of the meters and the card tray");
        Check(Mathf.IsEqualApprox(camera.Position.X, half), "Battle opens on the wagon side");
        var deck = Read<BattleDeckState>(battle, "_deck");
        Write(battle, "_courage", 100f);
        Write(battle, "_mana", Read<float>(battle, "_maxMana"));
        var deployments = Read<int>(battle, "_playerDeployments");
        void Wheel(MouseButton direction, float factor = 1) => battle._UnhandledInput(new InputEventMouseButton
            { ButtonIndex = direction, Factor = factor, Pressed = true, Position = new Vector2(600, 330) });
        await CaptureCamera("desktop-wagon");
        Wheel(MouseButton.WheelDown);
        Check(Mathf.IsEqualApprox(camera.Position.X, half + 100 / zoom), "Wheel down pans toward the enemy by one screen step");
        Wheel(MouseButton.WheelLeft);
        Check(Mathf.IsEqualApprox(camera.Position.X, half), "Horizontal wheel clamps at the wagon edge");
        Wheel(MouseButton.WheelRight, 1000);
        Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "Horizontal wheel reaches and clamps at the far edge");
        Check(Read<int>(battle, "_playerDeployments") == deployments, "Scrolling never deploys");
        Check(Read<PanelContainer>(battle, "_topHudPanel").GetGlobalRect() == hud, "HUD stays fixed while the battlefield scrolls");
        // The camera travels only (width - view) across the short field, so these pans stay small.
        Invoke(battle, "SetBattleCameraX", half + 10);
        battle._UnhandledInput(new InputEventPanGesture { Delta = new Vector2(2, .1f) });
        Check(Mathf.IsEqualApprox(camera.Position.X, half + 10 + 96 / zoom), "Trackpad horizontal gestures pan the map");
        var beforeDrag = camera.Position.X;
        battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true, Position = new Vector2(600, 330) });
        battle._Input(new InputEventMouseMotion { Position = new Vector2(500, 330) });
        battle._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false });
        Check(Mathf.IsEqualApprox(camera.Position.X, beforeDrag + 100 / zoom) && !Read<bool>(battle, "_battleCameraDragging"),
            "Middle-drag moves the ground with the pointer and releases cleanly");
        battle._UnhandledInput(new InputEventKey { Keycode = Key.End, Pressed = true });
        Check(Mathf.IsEqualApprox(camera.Position.X, width - half), "End jumps to the enemy base");
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
        battle._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen });
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
        var resting = camera.Position;
        for (var frame = 0; frame < 240; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(camera.Position == resting, "The camera stays where the player put it while the fighting moves on");

        // A flick glides on, slowing, and comes to rest about speed / friction further on.
        var friction = (float)typeof(BattleController).GetField("BattleScrollFriction", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        const float flick = 200f;
        Write(battle, "_battleCameraVelocity", flick); Write(battle, "_battleCameraGliding", true);
        Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        var speed = Read<float>(battle, "_battleCameraVelocity");
        Check(camera.Position.X > half && speed > flick * .9f && speed < flick, "A flick glides on and slows");
        for (var frame = 0; frame < 600 && Read<bool>(battle, "_battleCameraGliding"); frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(!Read<bool>(battle, "_battleCameraGliding") && Mathf.Abs(camera.Position.X - (half + flick / friction)) < 3,
            "The glide comes to rest inside the field");
        // A hard flick at the far end runs past it, stretching the field a little, and springs back exactly to it.
        var far = width - half;
        var stretchLimit = half * 2 * .25f;
        Invoke(battle, "SetBattleCameraX", far - 50);
        Write(battle, "_battleCameraVelocity", 3000f); Write(battle, "_battleCameraGliding", true);
        var furthest = 0f;
        for (var frame = 0; frame < 120 && Read<bool>(battle, "_battleCameraGliding"); frame++)
        {
            Invoke(battle, "UpdateBattleCamera", 1f / 60f);
            furthest = Mathf.Max(furthest, camera.Position.X);
        }
        Check(furthest > far + 10 && furthest <= far + stretchLimit, "A flick into the end overshoots it a little");
        Check(Mathf.IsEqualApprox(camera.Position.X, far) && !Read<bool>(battle, "_battleCameraGliding"), "The field springs back to the end and rests there");
        // Pausing stops a glide where it is.
        Invoke(battle, "SetBattleCameraX", half);
        Write(battle, "_battleCameraVelocity", 900f); Write(battle, "_battleCameraGliding", true);
        Write(battle, "_battlePaused", true);
        Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(camera.Position.X == half && !Read<bool>(battle, "_battleCameraGliding"), "Pause stops a glide");
        Write(battle, "_battlePaused", false);
        await CaptureCamera("desktop-rest");
        battle._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true });
        battle._Notification((int)Node.NotificationApplicationFocusOut);
        Check(!Read<bool>(battle, "_battleCameraDragging"), "Losing focus cancels dragging");
        // An ultrawide viewport must center a smaller world, not invert clamp limits.
        camera.Zoom = Vector2.One * .25f;
        Invoke(battle, "ClampBattleCamera", camera);
        Check(Mathf.IsEqualApprox(camera.Position.X, width * .5f), "World centers when the viewport sees beyond both edges");
        await CloseBattle(battle);

        battle = await OpenBattle(1);
        camera = Read<Camera2D>(battle, "_battleCamera");
        Check(Mathf.IsEqualApprox(camera.Zoom.X, (float)Invoke(battle, "get_BattleFitZoom")) && viewport.X / camera.Zoom.X < width,
            "Phone combat opens at the soldier scale with the rest of the field off screen");
        var point = new Vector2(viewport.X * .5f,
            (Read<float>(battle, "_mobileFieldTop") + Read<float>(battle, "_mobileFieldBottom")) * .5f);
        var count = Read<int>(battle, "_playerDeployments");
        var phoneHalf = viewport.X / camera.Zoom.X * .5f;
        var phoneFar = width - phoneHalf;
        battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
        battle._Input(new InputEventScreenDrag { Index = 0, Position = point - new Vector2(10000, 0) });
        Check(camera.Position.X > phoneFar + phoneHalf * .25f && camera.Position.X < phoneFar + phoneHalf * .5f,
            "Dragged past the far end, the field stretches, but only so far");
        await CaptureCamera("mobile-stretched-gate");
        battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point - new Vector2(10000, 0) });
        for (var frame = 0; frame < 60; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(Mathf.IsEqualApprox(camera.Position.X, phoneFar) && !Read<bool>(battle, "_battleCameraGliding"),
            "Let go, the field springs back to the far end");
        Check(Read<int>(battle, "_playerDeployments") == count, "A long touch drag cannot deploy");
        await CaptureCamera("mobile-gate");
        var panned = camera.Position.X;
        for (var frame = 0; frame < 300; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(camera.Position.X == panned, "A touch pan stays where it was left");
        Invoke(battle, "SetBattleCameraX", phoneHalf);
        battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
        battle._Input(new InputEventScreenDrag { Index = 0, Position = point + new Vector2(10000, 0) });
        Check(camera.Position.X < phoneHalf * .75f && camera.Position.X > 0, "The wagon end stretches the same way");
        await CaptureCamera("mobile-stretched-wagon");
        battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point + new Vector2(10000, 0) });
        for (var frame = 0; frame < 60; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(Mathf.IsEqualApprox(camera.Position.X, phoneHalf), "Let go, the field springs back to the wagon end");
        // A real flick: the finger's speed over its last moments carries the field on after it lifts.
        Invoke(battle, "SetBattleCameraX", phoneHalf);
        battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
        for (var step = 1; step <= 6; step++)
        {
            OS.DelayMsec(16);
            battle._Input(new InputEventScreenDrag { Index = 0, Position = point - new Vector2(30 * step, 0) });
        }
        battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point - new Vector2(180, 0) });
        var lifted = camera.Position.X;
        Check(Mathf.IsEqualApprox(lifted, phoneHalf + 180 / camera.Zoom.X), "The ground moves exactly with the finger");
        Check(Read<bool>(battle, "_battleCameraGliding") && Read<float>(battle, "_battleCameraVelocity") * camera.Zoom.X > 800,
            "A flick keeps the field gliding after the finger lifts");
        Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(camera.Position.X > lifted, "The flicked field moves on by itself");
        // Touching the gliding field catches it, and that touch is not a tap.
        battle._UnhandledInput(new InputEventScreenTouch { Index = 0, Pressed = true, Position = point });
        Check(!Read<bool>(battle, "_battleCameraGliding") && Read<bool>(battle, "_mobileCaughtGlide"), "A touch stops a gliding field");
        var caught = camera.Position.X;
        battle._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = point });
        for (var frame = 0; frame < 30; frame++) Invoke(battle, "UpdateBattleCamera", 1f / 60f);
        Check(camera.Position.X == caught, "A caught field stays where it stopped");
        var oldWindowSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1000, 600);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(Mathf.IsEqualApprox(camera.Zoom.X, (float)Invoke(battle, "get_BattleFitZoom")), "Resizing keeps the soldier-scale framing");
        GetWindow().Size = oldWindowSize;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await CloseBattle(battle);
    }
}
