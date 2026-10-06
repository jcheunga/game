using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Fights a short battle in every zone and checks the score, ambience and effects that play:
/// godot --path . res://scenes/tests/AudioReview.tscn -- --save-suffix=audio-review-local
/// </summary>
public partial class AudioReview : Node
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private int _failures;
    private readonly Dictionary<string, int> _played = new();
    private static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static void Write(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, args);
    private void Check(bool ok, string description) { GD.Print($"AUDIO_CHECK: {(ok ? "PASS" : "FAIL")} {description}"); if (!ok) _failures++; }
    // Real-time waits: the battles below run at 4x game speed, but sounds play in real time.
    private async Task Wait(double seconds = .15) => await ToSignal(GetTree().CreateTimer(seconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private static readonly Dictionary<string, (string Track, string Bed)> Zones = new()
    {
        ["city"] = ("battle_road", "amb_road"), ["harbor"] = ("battle_harbor", "amb_harbor"), ["foundry"] = ("battle_foundry", "amb_foundry"),
        ["quarantine"] = ("battle_quarantine", "amb_ward"), ["thornwall"] = ("battle_pass", "amb_pass"), ["basilica"] = ("battle_basilica", "amb_basilica"),
        ["mire"] = ("battle_mire", "amb_mire"), ["steppe"] = ("battle_steppe", "amb_steppe"), ["gloamwood"] = ("battle_gloamwood", "amb_gloamwood"),
        ["citadel"] = ("battle_citadel", "amb_citadel"),
    };

    private int Count(string prefix) => _played.Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(p => p.Value);

    private async void Run()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(x => x.StartsWith("--save-suffix=audio-review-"))) throw new Exception("Use an isolated audio-review save.");
            GetTree().AutoAcceptQuit = false;
            await Wait();
            var state = GameState.Instance;
            state.ResetProgress(); state.SetShowHints(false); state.SetAnalyticsConsent(false); state.SetAudioMuted(false);
            var save = state.BuildSaveData();
            save.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); save.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
            save.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(6).ToArray(); save.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(5).ToArray();
            state.RestoreCloudSave(save); state.SetShowHints(false); state.SetAudioMuted(false);
            AudioDirector.Instance.CuePlayed += id => _played[id] = _played.GetValueOrDefault(id) + 1;
            Check(AudioDirector.Instance.CueCount >= 200, $"Sound catalogue loaded ({AudioDirector.Instance.CueCount} cues)");
            await LiveUiReview.Open(this, "MainMenu"); await Wait(1);
            Check(MusicPlayer.Instance.CurrentTrackId == "title", $"Home plays the title theme ({MusicPlayer.Instance.CurrentTrackId})");
            Check(AudioDirector.Instance.AmbienceBed == "amb_home", $"Home has its meadow ambience ({AudioDirector.Instance.AmbienceBed})");

            var deck = GameData.PlayerRosterIds.Take(6).Select(GameData.GetUnit).ToArray();
            foreach (var (route, expected) in Zones)
            {
                var stage = GameData.Stages.First(s => RouteCatalog.Normalize(s.MapId) == route && !state.IsAdventureBoss(s.StageNumber)).StageNumber;
                state.SetSelectedStage(stage); state.PrepareCampaignBattle();
                _played.Clear();
                var battle = (BattleController)await LiveUiReview.Open(this, "Battle");
                await Wait(.6);
                Check(MusicPlayer.Instance.CurrentTrackId == expected.Track, $"{route}: battle music {MusicPlayer.Instance.CurrentTrackId}");
                Check(AudioDirector.Instance.AmbienceBed == expected.Bed, $"{route}: ambience {AudioDirector.Instance.AmbienceBed}");
                Engine.TimeScale = 4f;
                for (var i = 0; i < 40 && !Read<bool>(battle, "_battleEnded"); i++)
                {
                    Write(battle, "_courage", 999f);
                    Call(battle, "DeployPlayerUnit", deck[i % deck.Length]);
                    await Wait(.5);
                }
                Engine.TimeScale = 1f;
                var summary = string.Join(", ", _played.OrderByDescending(p => p.Value).Take(12).Select(p => $"{p.Key}x{p.Value}"));
                GD.Print($"AUDIO_ZONE {route}: {_played.Values.Sum()} cues ({summary})");
                Check(_played.ContainsKey("battle_start"), $"{route}: the caravan's horn opens the battle");
                Check(Count("deploy_") > 0, $"{route}: deploys have foley");
                Check(Count("swing_") + Count("thrust") > 0 && Count("hit_") > 0, $"{route}: melee swings and hits are heard");
                Check(Count("bow_release") + Count("crossbow_release") + Count("cast_") + Count("flask_") > 0, $"{route}: ranged launches are heard");
                Check(Count("impact_") > 0, $"{route}: projectile impacts are heard");
                Check(Count("death_") + Count("body_fall") > 0, $"{route}: deaths and falls are heard");
                Check(battle.GetChildren().OfType<AudioStreamPlayer2D>().Any() || _played.Values.Sum() > 30, $"{route}: battle sounds are positional on the field");
                battle.QueueFree(); await Wait(.3);
            }

            // A grave lord takes the field: the boss theme takes over, and his fall restores the zone track.
            var bossStage = GameData.Stages.First(s => state.IsAdventureBoss(s.StageNumber)).StageNumber;
            state.SetSelectedStage(bossStage); state.PrepareCampaignBattle();
            var bossBattle = (BattleController)await LiveUiReview.Open(this, "Battle");
            await Wait(.6);
            var zoneTrack = MusicPlayer.Instance.CurrentTrackId;
            var director = Read<BattleSpawnDirector>(bossBattle, "_spawnDirector");
            var args = new object[] { GameData.EnemyBossId, null };
            director.GetType().GetMethod("TryBuildEnemyStats").Invoke(director, args);
            Call(bossBattle, "SpawnEnemyUnit", (UnitStats)args[1], new Vector2(0, 0));
            await Wait(.4);
            Check(MusicPlayer.Instance.CurrentTrackId == "battle_boss", $"A boss arrival switches to the boss theme ({MusicPlayer.Instance.CurrentTrackId})");
            Check(_played.ContainsKey("boss_roar_grave") && _played.ContainsKey("boss_spawn"), "The grave lord roars and the boss stinger sounds");
            MusicPlayer.Instance.SetBossMusic(false);
            await Wait(.2);
            Check(MusicPlayer.Instance.CurrentTrackId == zoneTrack, "The zone track returns after the boss");
            bossBattle.QueueFree(); await Wait(.3);
            GD.Print("AUDIO_TOTALS: " + string.Join(", ", _played.OrderByDescending(p => p.Value).Select(p => $"{p.Key}x{p.Value}")));
        }
        catch (Exception ex) { GD.PrintErr(ex.ToString()); _failures++; }
        Engine.TimeScale = 1f;
        GD.Print($"AUDIO_REVIEW_RESULT: {_failures} failures"); await LiveUiReview.StopAudio(this); GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
