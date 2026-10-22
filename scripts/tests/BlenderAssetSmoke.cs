using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

// Checks the authored art contract in the real engine without touching the player's save.
public partial class BlenderAssetSmoke : Node
{
    private int _failures;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Hidden)!.GetValue(owner)!;
    private void Check(bool condition, string label)
    {
        GD.Print($"BLENDER_CHECK: {(condition ? "PASS" : "FAIL")} {label}");
        if (!condition) _failures++;
    }
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--save-suffix=blender-review-")))
                throw new InvalidOperationException("Requires an isolated blender-review save suffix.");
            GameState.Instance.SetAnalyticsConsent(false);
            GameState.Instance.SetShowHints(false);
            var ids = GameData.PlayerRosterIds.Concat(GameData.EnemyRosterIds).Distinct().ToArray();
            // Every combat unit in data/units.json is checked (the roster grows; the count comes from the data).
            using (var json = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/units.json")))
            {
                var defined = json.RootElement.GetProperty("Units").EnumerateArray().Select(u => u.GetProperty("Id").GetString())
                    .Where(id => !GameData.GetUnit(id).NonCombatant).ToArray();
                Check(defined.Length > 0 && defined.All(ids.Contains), $"All {defined.Length} active unit definitions covered");
            }
            foreach (var id in ids)
            {
                var def = GameData.GetUnit(id);
                var sheet = UnitSpriteLoader.TryLoad(def.VisualClass, id);
                // Remastered units ship frames cropped to their animation envelope at the density set in
                // art/remaster/density.py (small units can be narrower than the legacy 192x240 frame), still
                // within a mobile-safe atlas.
                // Puppet units ship trimmed frames on one or more pages, each within the mobile-safe atlas size.
                Check(sheet != null && sheet.AnchorY > 0.5f && (sheet.FrameWidth == 192 && sheet.FrameHeight == 240
                    || (sheet.Pages ?? new[] { sheet.Texture }).All(t => t != null && t.GetWidth() <= 4096 && t.GetHeight() <= 4096)),
                    id + " has compact atlas metadata and ground anchor");
                if (sheet == null) continue;
                var probe = new Unit();
                probe.Setup(def.IsPlayerSide ? Team.Player : Team.Enemy, new UnitStats(def), Vector2.Zero);
                var density = sheet.FrameWidth / (probe.Radius * 2 * probe.VisualScale * sheet.DrawScale);
                // Walking puppets: the cycle keeps a natural cadence at the unit's own speed (a short cycle at speed
                // is a scuttle), and its frames stay inside the playback clamp so the planted foot never slides.
                if (sheet.Animations.TryGetValue(UnitAnimState.Walk, out var walk) && walk.Gait != "" && def.Speed > 1f)
                {
                    var cycle = walk.Stride * probe.Radius * 2f * probe.VisualScale * sheet.DrawScale / def.Speed;
                    var (lo, hi) = walk.Gait switch
                    {
                        "gallop" or "canter" or "trot" => (.27f, 1.1f),   // beasts and mounts at speed
                        "walk" => (.6f, 2.4f),                              // a beast or a mount at a walk
                        "jog" or "prowl" => (.45f, 1.1f),
                        _ when def.VisualClass == "boss" => (1.2f, 3.9f),   // giants: long, unhurried strides
                        _ => (.6f, 2.4f),
                    };
                    Check(cycle >= lo && cycle <= hi && cycle / walk.FrameCount is >= .025f and <= .16f,
                        $"{id} {walk.Gait} cycle {cycle:0.00}s at speed {def.Speed:0}");
                }
                probe.Free();
                Check(density >= 1.25f, $"{id} keeps {density:0.00} atlas px per battle px, so enlarged units stay sharp");
                var total = sheet.Frames?.Length > 0 ? sheet.Frames.Length
                    : sheet.Texture.GetWidth() / sheet.FrameWidth * (sheet.Texture.GetHeight() / sheet.FrameHeight);
                bool Valid(UnitAnimState state) => sheet.Animations.TryGetValue(state, out var clip)
                    && clip.FrameCount > 0 && clip.FrameDuration > 0 && clip.StartFrame + clip.FrameCount <= total;
                Check(Enum.GetValues<UnitAnimState>().Where(state => state != UnitAnimState.Melee).All(Valid), id + " six valid clips");
                // Ranged units fight hand to hand at point-blank; cleaving bosses reuse their swing.
                if (def.UsesProjectile && sheet.MotionProfile != "royal-cleave")
                    Check(Valid(UnitAnimState.Melee) && sheet.Animations[UnitAnimState.Melee].ContactFrame is var contact
                        && contact > 0 && contact < sheet.Animations[UnitAnimState.Melee].FrameCount,
                        id + " has a close-quarters melee clip");
                if (def.UsesProjectile)
                {
                    var style = ProjectileStyles.ForUnit(id, sheet.MotionProfile);
                    Check(style != ProjectileStyles.Default && (style.Shape != ProjectileShape.Sprite || ProjectileStyles.Texture(style.Sprite) != null),
                        $"{id} fires its own {style.Id} projectile");
                }
            }
            foreach (BaseWeaponKind kind in Enum.GetValues<BaseWeaponKind>())
            {
                var style = ProjectileStyles.ForBaseWeapon(kind);
                Check(style.Shape != ProjectileShape.Sprite || ProjectileStyles.Texture(style.Sprite) != null, $"{kind} base weapon shot has art");
            }
            if (!OS.GetCmdlineUserArgs().Contains("--units-only"))
            {
                // Battles draw the caravan from its projected battle art (the flat war_wagon PNGs are a fallback the builds leave out).
                foreach (var skin in WagonSkinCatalog.GetAll())
                    Check(BattleStructureArt.Load(skin.Id == WagonSkinCatalog.DefaultSkinId ? "war_wagon" : "war_wagon_" + skin.Id) != null, skin.Id + " caravan loads");
                foreach (var kind in Enum.GetValues<BaseWeaponKind>())
                    Check(BattlefieldTextureLoader.TryLoadStructure("mount_" + kind.ToString().ToLowerInvariant()) != null, kind + " mount loads");
                foreach (var folder in new[] { "structures", "units", "particles", "world/royal", "ui/icons", "ui/portraits", "ui/royal" })
                {
                    var count = 0;
                    var directory = ProjectSettings.GlobalizePath("res://assets/" + folder);
                    foreach (var path in Directory.EnumerateFiles(directory, "*.png", SearchOption.AllDirectories))
                    {
                        using var texture = ResourceLoader.Load<Texture2D>(ProjectSettings.LocalizePath(path), "", ResourceLoader.CacheMode.Ignore);
                        if (texture == null || texture.GetWidth() <= 0 || texture.GetHeight() <= 0)
                            Check(false, "Cannot decode imported texture " + path);
                        count++;
                    }
                    Check(count > 0, $"Imported {folder}: {count} textures decoded");
                }
            }
            await CheckAnimations();
            if (OS.GetCmdlineUserArgs().Contains("--screenshots"))
            {
                foreach (var stage in new[] { 1, 54, 100 }) await CaptureBattle(stage);
                await CaptureCodex();
            }
            if (OS.GetCmdlineUserArgs().Contains("--lineups"))
            {
                // Every unit on a real battlefield, a few at a time, at its true in-battle size and shadow.
                var all = GameData.PlayerRosterIds.Concat(GameData.EnemyRosterIds).Distinct().ToArray();
                for (var page = 0; page * LineupSize < all.Length; page++)
                    await CaptureLineup(all.Skip(page * LineupSize).Take(LineupSize).ToArray(), page);
            }
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        GD.Print($"BLENDER_ASSET_RESULT: {_failures} failures");
        await LiveUiReview.StopAudio(this); GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private async Task CheckAnimations()
    {
        var unit = new Unit();
        unit.Setup(Team.Player, new UnitStats(GameData.GetUnit(GameData.PlayerBrawlerId)), new Vector2(640, 360));
        AddChild(unit);
        unit.SetProcess(false);
        unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Deploy, "Deployment animation selected");
        unit._Process(0.5);
        unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Idle, "Deploy returns to idle");
        unit.Position += new Vector2(1, 0);
        unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Walk, "Movement selects walk");
        unit._Process(0.008);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Walk, "Walk persists between physics ticks");
        Check(unit.TryBeginAttackPosition(unit.Position), "Attack begins");
        unit._Process(0.2);
        unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Attack && Read<int>(unit, "_spriteAnimFrame") >= 2,
            "Attack continues beyond the old flash timer");
        unit.TickAttackTimer(30);
        unit.TryBeginAttackPosition(unit.Position);
        unit._Process(0.01);
        Check(Read<int>(unit, "_spriteAnimFrame") == 0, "Repeated attack restarts its clip");
        unit.TakeDamage(1);
        unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Attack, "Ordinary hit preserves the underlying attack pose");
        unit.ReactToContact(1, 15);
        unit._Process(.04);
        Check(unit.HitReactionAmount > 0 && unit.HitReactionAmount < 1, "Ordinary hit adds a restrained pose reaction");
        var visual = unit.SpawnDeathVisual(this);
        Check(visual != null && GetChildren().OfType<UnitDeathVisual>().Single() == visual, "Death visual spawned");
        var impacts = 0;
        var dissolves = 0;
        visual.Impacted = _ => impacts++;
        visual.DissolveStarted = _ => dissolves++;
        UnitPool.Release(unit);
        Check(IsInstanceValid(visual), "Death visual survives unit pooling");
        visual.SetProcess(false);
        var clip = Read<SpriteAnimRange>(visual, "_clip");
        Check(clip.ImpactFrame > 0 && clip.ImpactFrame < clip.FrameCount, "Death clip reports when the body lands");
        visual._Process(clip.ImpactFrame * clip.FrameDuration + .001);
        Check(impacts == 1 && dissolves == 0, "Impact fires on the landing frame, before the dissolve");
        visual._Process(clip.FrameCount * clip.FrameDuration);
        Check(impacts == 1 && IsInstanceValid(visual), "The body rests on the ground after the clip");
        visual._Process(visual.Lifetime);
        Check(dissolves == 1, "The body dissolves after resting");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!IsInstanceValid(visual), "Death visual cleans itself up");
        unit = UnitPool.Acquire();
        unit.Setup(Team.Enemy, new UnitStats(GameData.GetUnit(GameData.EnemyWalkerId)), new Vector2(700, 360));
        AddChild(unit); unit.SetProcess(false); unit._Process(0.01);
        Check(Read<UnitAnimState>(unit, "_spriteAnimState") == UnitAnimState.Deploy, "Pooled unit resets animation");
        UnitPool.Release(unit);
        UnitPool.Clear();
    }

    private async Task CaptureBattle(int stage)
    {
        GameState.Instance.SetSelectedStage(stage);
        GameState.Instance.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        var playerIds = new[] { "player_brawler", "player_shooter", "player_raider", "player_stormcaller", "player_ballista" };
        var enemyIds = new[] { "enemy_walker", "enemy_brute", "enemy_lich", "enemy_siegetower", stage == 100 ? "enemy_boss_ashen_regent" : "enemy_boss" };
        for (var i = 0; i < 5; i++)
        {
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var id = team == Team.Player ? playerIds[i] : enemyIds[i];
                var x = team == Team.Player ? 280 + i * 60 : 1010 - i * 60;
                var unit = new Unit();
                unit.Setup(team, new UnitStats(GameData.GetUnit(id)), new Vector2(x, 350 + i % 3 * 60));
                battle.AddChild(unit); unit.ZIndex = 5;
            }
        }
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw();
        var folder = ProjectSettings.GlobalizePath("res://artifacts/blender");
        Directory.CreateDirectory(folder);
        var result = GetViewport().GetTexture().GetImage().SavePng($"{folder}/assets-in-battle-{stage}.png");
        Check(result == Error.Ok, "Battle screenshot stage " + stage);
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private const int LineupSize = 8;

    private async Task CaptureLineup(string[] ids, int page)
    {
        // A zone that suits the first unit (player pages use stage 1; enemy pages their own zone's first stage).
        var first = GameData.GetUnit(ids[0]);
        GameState.Instance.SetSelectedStage(first.IsPlayerSide ? 1 : Math.Clamp(page * 7 + 1, 1, 100));
        GameState.Instance.PrepareCampaignBattle();
        var battle = GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
        AddChild(battle);
        battle.SetPhysicsProcess(false);
        for (var i = 0; i < ids.Length; i++)
        {
            var def = GameData.GetUnit(ids[i]);
            var unit = new Unit();
            // two staggered rows across the opening view, so big units do not hide small ones
            unit.Setup(def.IsPlayerSide ? Team.Player : Team.Enemy, new UnitStats(def),
                new Vector2(90 + i * 62, i % 2 == 0 ? 318 : 362));
            battle.AddChild(unit); unit.ZIndex = 5 + i % 2;
        }
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw();
        var folder = ProjectSettings.GlobalizePath("res://artifacts/blender/lineups");
        Directory.CreateDirectory(folder);
        var result = GetViewport().GetTexture().GetImage().SavePng($"{folder}/lineup-{page:00}.png");
        Check(result == Error.Ok, $"Lineup {page}: {string.Join(", ", ids)}");
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureCodex()
    {
        foreach (var entry in CodexCatalog.GetAll()) GameState.Instance.DiscoverCodexEntry(entry.Id);
        var menu = await LiveUiReview.Open(this, "CodexMenu");
        // Royal tabs carry their name as accessibility text (the label is painted on the plate).
        var bosses = menu.FindChildren("*", "Button", true, false).OfType<Button>()
            .First(button => button.Text == "Bosses" || button.AccessibilityName == "Bosses");
        bosses.ButtonPressed = true; bosses.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // The first discovered boss on the page (the boss list is paged, so a named boss may sit further on).
        var titles = CodexCatalog.GetAll().Where(e => e.Category == "boss").Select(e => e.Title).ToHashSet();
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var boss = menu.FindChildren("*", "", true, false).OfType<BaseButton>()
            .First(button => titles.Contains(button.AccessibilityName) && !button.Disabled);
        boss.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw();
        var output = ProjectSettings.GlobalizePath("res://artifacts/blender/codex-in-game.png");
        Check(GetViewport().GetTexture().GetImage().SavePng(output) == Error.Ok, "Discovered Codex portrait screenshot");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
