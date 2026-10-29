using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class UiReviewSmoke
{
    private async Task ReviewProductionPolishUi()
    {
        foreach (var phone in new[] { false, true })
        {
            GetWindow().Size = phone ? new Vector2I(844, 390) : new Vector2I(1280, 720);
            _output = ProjectSettings.GlobalizePath("res://artifacts/production-polish/" + (phone ? "phone" : "desktop"));
            System.IO.Directory.CreateDirectory(_output);
            var state = GameState.Instance;
            state.ResetProgress(); state.SetShowHints(true); state.SetAnalyticsConsent(false);
            await Open("MainMenu"); var home = (MapMenu)GetTree().CurrentScene;
            SceneRouter.Instance.GoToLoadout(); await LiveUiReview.Settle(this);
            var food = state.Food;
            var picture = Walk(home).OfType<Button>().Single(b => b.AccessibilityName == "Encounter briefing");
            await TapModal(picture);
            // Each enemy's card ends with how to counter it, beside a crossed-swords mark, and heads with its level at this stage.
            var enemies = GameData.GetStage(state.SelectedStage).Waves.SelectMany(w => w.Entries).Select(e => GameData.GetUnit(e.UnitId)).Distinct().ToArray();
            var briefing = Walk(home).FirstOrDefault(n => n.Name == "EncounterBriefing") ?? home;
            var shown = Walk(briefing).OfType<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text).ToHashSet();
            Check(briefing != home && enemies.Length > 0 && enemies.All(e => shown.Contains(string.IsNullOrWhiteSpace(e.Weakness) ? "Hold the melee line and watch for attack warnings." : e.Weakness)),
                "Preparation picture opens readable enemy counters");
            Check(shown.Any(t => t.StartsWith("Level 1 · ")) && Walk(briefing).OfType<StatSheet>().Count(s => s.IsVisibleInTree()) == enemies.Length,
                "Enemy briefing displays stage-resolved stats");
            AuditText("Polish briefing"); await Capture("01-enemy-briefing");
            await TapModal(Walk(home).OfType<Button>().Last(b => b.AccessibilityName == "Close details"));
            Check(home.HasHomeModal && state.Food == food, "Closing enemy briefing preserves preparation and resources");
            home.CloseHomeModal(); SceneRouter.Instance.GoToSettings(); await LiveUiReview.Settle(this);
            var settings = Walk(home).OfType<SettingsMenu>().Single();
            await TapModal(Walk(settings).OfType<Button>().Single(b => b.AccessibilityName == "Gameplay"));
            var textButton = Walk(settings).OfType<Button>().Single(b => b.AccessibilityName == "Text size");
            await TapModal(textButton);
            Check(state.FontSizeOffset == 2 && ReadingText.Size(20) == 22, "Reading-size control changes actual description text");
            typeof(SettingsMenu).GetMethod("RestoreDefaults", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(settings, null);
            Check(state.FontSizeOffset == 0 && !state.ShowFpsCounter, "Restore defaults resets reading size and keeps debug displays hidden");
            await Capture("02-settings"); home.CloseHomeModal();
            await Capture("03-clean-home");
            SceneRouter.Instance.GoToLoadout(); await LiveUiReview.Settle(this);
            var deploy = Walk(home).OfType<Button>().Single(b => b.AccessibilityName == "Deploy");
            food = state.Food; var cost = state.GetStageEntryFoodCost(state.SelectedStage);
            deploy.EmitSignal(BaseButton.SignalName.Pressed); deploy.EmitSignal(BaseButton.SignalName.Pressed);
            await Wait(.25);
            var loading = Walk(SceneRouter.Instance).OfType<LoadingScreen>().Single();
            Check(loading.IsVisibleInTree() && loading.ArtworkPath.Length > 0 && loading.Tip.Length > 0, "Battle loading uses zone artwork and a readable tip");
            var tipLabel = Walk(loading).OfType<Label>().Single(l => l.Text == loading.Tip);
            Check(GetViewport().GetVisibleRect().Encloses(tipLabel.GetGlobalRect()) && tipLabel.GetLineCount() <= 4,
                "Loading tip wraps into readable lines within the screen");
            await Capture("04-illustrated-loading");
            await LiveUiReview.Settle(this);
            Check(state.Food == food - cost && GetTree().CurrentScene is BattleController, "Repeated deploy input pays entry once and opens one battle");
            var battle = (BattleController)GetTree().CurrentScene;
            battle.SetPhysicsProcess(false);
            var hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BattleController).GetMethod("TryCastSpellAt", hidden).Invoke(battle, new object[] { GameData.GetSpell("spell_fireball"), new Vector2(500, 340) });
            await Wait(.15);
            await Capture("04-spell-denial");
            typeof(BattleController).GetMethod("TogglePause", hidden).Invoke(battle, null);
            await Wait(.1);
            await TapModal(Walk(battle).OfType<Button>().Single(b => b.Text == "Game settings"));
            Check(Walk(battle).OfType<SettingsMenu>().Any(), "Paused battle opens settings without advancing combat");
            var music = Walk(battle).OfType<HSlider>().Single(s => s.IsVisibleInTree() && s.AccessibilityName == "Music volume");
            Check(GetViewport().GetVisibleRect().Encloses(music.GetGlobalRect()), "Paused settings exposes the complete volume control");
            await Capture("05-paused-settings"); battle.CloseBattleSettings(); await Wait(.2);
            await TapModal(Walk(battle).OfType<Button>().Single(b => b.Text == "Quit battle"));
            await LiveUiReview.Settle(this);
            Check(!GetTree().Paused && GetTree().CurrentScene is MapMenu, $"Retreat returns to an interactive home map (scene {GetTree().CurrentScene?.GetType().Name}, paused {GetTree().Paused})");
            state.UnlockNextStage(GameData.MaxStage - 1); state.PrepareBossRush(out _);
            await Open("Battle"); battle = (BattleController)GetTree().CurrentScene; battle.SetPhysicsProcess(false);
            typeof(BattleController).GetField("_rushCleared", hidden).SetValue(battle, 2);
            typeof(BattleController).GetField("_elapsed", hidden).SetValue(battle, 80f);
            var gold = state.Gold;
            typeof(BattleController).GetMethod("TogglePause", hidden).Invoke(battle, null);
            await Wait(.1); await TapModal(Walk(battle).OfType<Button>().Single(b => b.Text == "Quit battle"));
            await LiveUiReview.Settle(this);
            Check(state.Gold == gold + BossRushCatalog.Waves.Take(2).Sum(w => w.RewardGold) && state.LastResultMessage.Contains("Boss rush ended"), "Retreating from boss rush preserves earned commander rewards");
            state.ApplyBossRushResult(2, 80, 9999, false, true);
            Check(state.Gold == gold + BossRushCatalog.Waves.Take(2).Sum(w => w.RewardGold), "Boss-rush retreat rewards cannot be collected twice");
            Check(!GetTree().Paused && Walk(GetTree().CurrentScene).OfType<BossRushMenu>().Any(), "Boss-rush retreat returns to its playable menu");
        }
        GD.Print($"PRODUCTION_POLISH_UI_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
