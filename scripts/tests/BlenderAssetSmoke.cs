using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            Check(ids.Length == 52, "All 52 active unit definitions covered");
            foreach (var id in ids)
            {
                var def = GameData.GetUnit(id);
                var sheet = UnitSpriteLoader.TryLoad(def.VisualClass, id);
                // Units drawn large in battle ship larger frames cropped to their animation
                // envelope (art/remaster/density.py), still within a mobile-safe atlas.
                Check(sheet != null && sheet.AnchorY > 0.5f && (sheet.FrameWidth == 192 && sheet.FrameHeight == 240
                    || sheet.FrameWidth > 192 && sheet.Texture.GetWidth() <= 4096 && sheet.Texture.GetHeight() <= 4096),
                    id + " has compact atlas metadata and ground anchor");
                if (sheet == null) continue;
                var probe = new Unit();
                probe.Setup(def.IsPlayerSide ? Team.Player : Team.Enemy, new UnitStats(def), Vector2.Zero);
                var density = sheet.FrameWidth / (probe.Radius * 2 * probe.VisualScale * sheet.DrawScale);
                probe.Free();
                Check(density >= 1.25f, $"{id} keeps {density:0.00} atlas px per battle px, so enlarged units stay sharp");
                var total = sheet.Texture.GetWidth() / sheet.FrameWidth * (sheet.Texture.GetHeight() / sheet.FrameHeight);
                Check(Enum.GetValues<UnitAnimState>().All(state => sheet.Animations.TryGetValue(state, out var clip)
                    && clip.FrameCount > 0 && clip.FrameDuration > 0 && clip.StartFrame + clip.FrameCount <= total), id + " six valid clips");
            }
            if (!OS.GetCmdlineUserArgs().Contains("--units-only"))
            {
                var coverage = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://art/blender/coverage.json")));
                foreach (var group in coverage.RootElement.EnumerateObject())
                {
                    if (group.Value.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                    Check(group.Value.GetProperty("present").GetInt32() == group.Value.GetProperty("expected").GetInt32(), group.Name + " complete");
                }
                foreach (var terrain in GameData.Stages.Select(x => x.TerrainId).Distinct())
                    Check(BattlefieldTextureLoader.TryLoadBackground(terrain) != null, terrain + " background loads");
                foreach (var skin in WagonSkinCatalog.GetAll())
                    Check(BattlefieldTextureLoader.TryLoadStructure(skin.Id == WagonSkinCatalog.DefaultSkinId ? "war_wagon" : "war_wagon_" + skin.Id) != null, skin.Id + " caravan loads");
                foreach (var kind in Enum.GetValues<BaseWeaponKind>())
                    Check(BattlefieldTextureLoader.TryLoadStructure("mount_" + kind.ToString().ToLowerInvariant()) != null, kind + " mount loads");
                foreach (var folder in new[] { "backgrounds", "structures", "units", "particles", "map/adventure", "world/battles", "world/overworld", "ui/icons", "ui/portraits" })
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
                foreach (var stage in new[] { 1, 24, 60 }) await CaptureBattle(stage);
                await CaptureCodex();
            }
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        GD.Print($"BLENDER_ASSET_RESULT: {_failures} failures");
        GetTree().Quit(_failures == 0 ? 0 : 1);
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
        var enemyIds = new[] { "enemy_walker", "enemy_brute", "enemy_lich", "enemy_siegetower", stage == 60 ? "enemy_boss_citadel" : "enemy_boss" };
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
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://artifacts/blender");
        Directory.CreateDirectory(folder);
        var result = GetViewport().GetTexture().GetImage().SavePng($"{folder}/assets-in-battle-{stage}.png");
        Check(result == Error.Ok, "Battle screenshot stage " + stage);
        battle.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CaptureCodex()
    {
        foreach (var entry in CodexCatalog.GetAll()) GameState.Instance.DiscoverCodexEntry(entry.Id);
        var menu = GD.Load<PackedScene>("res://scenes/CodexMenu.tscn").Instantiate();
        AddChild(menu);
        menu.GetType().GetField("_activeCategory", Hidden)!.SetValue(menu, "Bosses");
        menu.GetType().GetField("_selectedEntryId", Hidden)!.SetValue(menu, "boss_dread_sovereign");
        menu.GetType().GetMethod("RefreshBook", Hidden)!.Invoke(menu, null);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var output = ProjectSettings.GlobalizePath("res://artifacts/blender/codex-in-game.png");
        Check(GetViewport().GetTexture().GetImage().SavePng(output) == Error.Ok, "Discovered Codex portrait screenshot");
        menu.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
