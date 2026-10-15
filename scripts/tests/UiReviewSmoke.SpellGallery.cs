using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Casts every player spell on a quiet, staged battlefield and saves a strip of timed frames around the cast
/// to artifacts/spell-gallery/&lt;effect&gt;.png, for judging spell effects side by side.
///   --spell-gallery [--only=fireball,meteor]
/// Staged units are inert dummies (no speed, no damage); the waves are held and one far-off dummy keeps the
/// battle from ending. Spells resolve through the battle's own ApplySpellEffect.
/// </summary>
public partial class UiReviewSmoke
{
    // Battle seconds after the cast; the capture runs at quarter speed so each frame lands on its moment.
    private static readonly float[] GalleryTimes = { .04f, .1f, .18f, .3f, .5f, .8f, 1.2f, 1.5f, 2.1f };
    private const float GallerySlowMotion = .25f;
    private static readonly HashSet<string> GalleryAllySpells = new()
    {
        "heal", "barrier_ward", "aegis_bolt", "purifying_light", "gale_step", "verdant_renewal", "rallying_advance", "mirror_ward",
    };

    private async Task ReviewSpellGallery()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/spell-gallery");
        System.IO.Directory.CreateDirectory(_output);
        var only = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--only="))?["--only=".Length..].Split(',');
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        GetWindow().Size = new Vector2I(1280, 720);
        var fixture = GameState.Instance.BuildSaveData();
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
        fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
        GameState.Instance.RestoreCloudSave(fixture);
        GameState.Instance.SetShowHints(false);
        GameState.Instance.PrepareCampaignBattle(); await Open("Battle");
        var battle = (BattleController)GetTree().CurrentScene;
        var type = typeof(BattleController);
        T Read<T>(string name) => (T)type.GetField(name, hidden)!.GetValue(battle)!;
        void Write(string name, object value) => type.GetField(name, hidden)!.SetValue(battle, value);
        T Prop<T>(string name) => (T)type.GetProperty(name, hidden)!.GetValue(battle)!;
        object Call(string name, params object[] args) => type.GetMethod(name, hidden, null, args.Select(a => a.GetType()).ToArray(), null)!.Invoke(battle, args);
        if (Read<bool>("_battlePaused")) Call("TogglePause");

        // Hold the waves: no further scripted wave, no timed wave, nothing pending.
        var director = Read<BattleSpawnDirector>("_spawnDirector");
        typeof(BattleSpawnDirector).GetProperty("NextScriptedWaveIndex")!.SetValue(director, director.TotalScriptedWaves);
        typeof(BattleSpawnDirector).GetField("_enemySpawnTimer", hidden)!.SetValue(director, 1e9f);
        var pending = typeof(BattleSpawnDirector).GetField("_pendingEnemySpawns", hidden)!.GetValue(director)!;
        pending.GetType().GetMethod("Clear")!.Invoke(pending, null);

        var top = Prop<float>("BattlefieldTop"); var bottom = Prop<float>("BattlefieldBottom");
        var right = Prop<float>("BattlefieldRight");
        Unit Dummy(Team team, string id, Vector2 at, float health = 1f)
        {
            var unit = (Unit)Call("SpawnUnit", team, new UnitStats(GameData.GetUnit(id), healthScale: health, damageScale: 0f, speedScale: 0f), at);
            unit.CanDropCourage = false;
            return unit;
        }
        var anchor = Dummy(Team.Enemy, "enemy_walker", new Vector2(right - 20, (top + bottom) / 2), 1000f);
        await Wait(.3);

        var units = Read<List<Unit>>("_units");
        var fields = Read<List<BattleContentField>>("_contentFields");
        var corpses = (IList)type.GetField("_contentCorpses", hidden)!.GetValue(battle)!;
        var deck = Read<BattleDeckState>("_deck");
        var camera = Read<Camera2D>("_battleCamera");
        var mid = (top + bottom) / 2;
        // A window losing focus pauses the battle; keep it running for the captures.
        void Resume() { if (Read<bool>("_battlePaused")) Call("TogglePause"); }

        foreach (var definition in GameData.GetPlayerSpells())
        {
            var spell = GameState.Instance.BuildSpellStats(definition);
            var effect = spell.EffectType;
            if (only != null && !only.Contains(effect) && !only.Contains(definition.Id)) continue;

            // A clean stage: only the anchor stays; the last spell's dummies, fields and corpses go (no bodies left lying).
            foreach (var unit in units.Where(u => u != anchor).ToArray()) { units.Remove(unit); unit.QueueFree(); }
            foreach (var field in fields) field.QueueFree();
            fields.Clear(); corpses.Clear();
            foreach (var leftover in battle.GetChildren().Where(c => c is VfxSprite or VfxBolt or VfxChains or CpuParticles2D)) leftover.QueueFree();
            Resume();
            await Wait(.5);

            var target = new Vector2(Mathf.Clamp(anchor.Position.X - 420, 260, right - 300), mid);
            var focus = effect == "war_cry" ? Prop<Vector2>("PlayerBaseCorePosition") : target;
            type.GetMethod("SetBattleCameraX", hidden)!.Invoke(battle, new object[] { focus.X });
            var allySpell = GalleryAllySpells.Contains(effect) || definition.CardKind == "tactical" && effect is "tactic_healing_shrine" or "tactic_purifying_censer";
            // Tactics stand beside the enemies so they can be seen; traps keep an enemy within reach to spring.
            var enemyAt = allySpell ? target + new Vector2(90, 0)
                : definition.CardKind == "tactical" && effect is not ("tactic_smoke_pot" or "tactic_war_horn")
                    ? target + new Vector2(effect is "tactic_firepot_mine" or "tactic_frost_trap" ? 30 : 84, 0) : target;
            var allyAt = allySpell ? target : target - new Vector2(90, 0);
            if (effect == "war_cry") allyAt = focus + new Vector2(80, 0);
            var foes = new[] { new Vector2(-22, -14), new Vector2(14, 10), new Vector2(34, -8), new Vector2(-4, 18) }
                .Select((o, i) => Dummy(Team.Enemy, i % 2 == 0 ? "enemy_walker" : "enemy_brute", enemyAt + o, 20f)).ToArray();
            var friends = new[] { new Vector2(-16, -10), new Vector2(18, 6), new Vector2(-2, 16) }
                .Select(o => Dummy(Team.Player, deck.Roster[0].Id, allyAt + o, 4f)).ToArray();
            foreach (var friend in friends) friend.TakeDamage(friend.MaxHealth * .5f, "gallery", CombatDamageType.True);
            if (effect == "soul_harvest")
                foreach (var foe in foes.Take(3)) foe.TakeDamage(foe.MaxHealth * 2, "gallery", CombatDamageType.True);
            if (effect == "resurrect")
            {
                Write("_lastDeadPlayerUnitId", deck.Roster[0].Id);
                Write("_lastDeadPlayerPosition", target + new Vector2(0, 4));
            }
            if (effect == "tactic_repair_cache")
            {
                target = focus = Prop<Vector2>("PlayerBaseCorePosition") + new Vector2(30, 0);
                type.GetMethod("SetBattleCameraX", hidden)!.Invoke(battle, new object[] { target.X });
            }
            Write("_mana", Read<float>("_maxMana"));
            await Wait(.35);

            var screen = battle.GetGlobalTransformWithCanvas() * (effect is "war_cry" or "tactic_repair_cache" ? focus : target);
            var crop = new Rect2I((int)Mathf.Clamp(screen.X - 300, 0, 1280 - 600), (int)Mathf.Clamp(screen.Y - 250, 0, 720 - 340), 600, 340);
            using var strip = Image.CreateEmpty(crop.Size.X * GalleryTimes.Length, crop.Size.Y, false, Image.Format.Rgba8);
            Engine.TimeScale = GallerySlowMotion;
            Call("ApplySpellEffect", spell, target);
            var clock = 0f;
            for (var i = 0; i < GalleryTimes.Length; i++)
            {
                // Review timers run on battle time, so the waits shrink with the slow motion.
                await Wait(GalleryTimes[i] - clock);
                clock = GalleryTimes[i];
                Resume();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw();
                using var frame = GetViewport().GetTexture().GetImage();
                frame.Convert(Image.Format.Rgba8);
                strip.BlitRect(frame, crop, new Vector2I(i * crop.Size.X, 0));
            }
            Engine.TimeScale = 1f;
            strip.SavePng($"{_output}/{definition.Id}.png");
            GD.Print($"SPELL_GALLERY {definition.Id} ({effect})");
        }
        GD.Print($"SPELL_GALLERY_RESULT: {_failures} failures");
        QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
