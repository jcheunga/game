using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class UiReviewSmoke
{
    private async Task ReviewBattlePolish()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/battle-background-polish");
        System.IO.Directory.CreateDirectory(_output);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        T Read<T>(BattleController battle, string field) => (T)typeof(BattleController).GetField(field, hidden)!.GetValue(battle)!;
        void Call(BattleController battle, string name) => typeof(BattleController).GetMethod(name, hidden)!.Invoke(battle, null);
        var state = GameState.Instance;
        var fixture = state.BuildSaveData();
        fixture.Food = 24; fixture.Gold = 1240; fixture.ShowFpsCounter = false; fixture.ReducedMotion = true;
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray(); fixture.OwnedPlayerSpellIds = GameData.PlayerSpellIds.ToArray();
        fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray(); fixture.ActiveDeckSpellIds = GameData.PlayerSpellIds.Take(2).ToArray();
        state.RestoreCloudSave(fixture);
        var artworkSize = new Vector2(1983, 793); var ground = new Rect2(84, 96, 2392, 488);
        var scene = WorldEnvironmentArt.BattleSceneRect(artworkSize, ground);
        Check(Mathf.IsEqualApprox(scene.Size.X / artworkSize.X, scene.Size.Y / artworkSize.Y), "Scene art keeps its original proportions");
        Check((scene.Position + artworkSize * WorldEnvironmentArt.BattleFloorSource.Position * (scene.Size.X / artworkSize.X)).DistanceTo(ground.Position) < .01f,
            "Clear authored ground stays aligned with the simulation");
        Check(scene.Encloses(new Rect2(0, 0, 2560, 720)), "Authored scenery covers the full battle world");
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1024, 768), new Vector2I(844, 390) })
        {
            var phone = size.X == 844;
            MobilePresentation.TestOverride = phone;
            GetWindow().Size = size;
            state.SetSelectedStage(1); state.PrepareCampaignBattle(); await Open("Battle");
            var battle = (BattleController)GetTree().CurrentScene; battle.SetPhysicsProcess(false);
            if (Read<bool>(battle, "_battlePaused")) Call(battle, "TogglePause");
            var tag = phone ? "phone" : size.X == 1024 ? "small" : "desktop";
            var health = Read<PanelContainer>(battle, "_topHudPanel");
            var gold = Read<HBoxContainer>(battle, "_goldReadout");
            var menu = Read<Button>(battle, "_hudSettingsButton");
            Check(health.GetGlobalRect().Position.X < 50 && health.GetGlobalRect().Position.Y < 50, tag + ": health and courage stay top left");
            Check(gold.GetGlobalRect().Position.X > battle.GetViewportRect().Size.X * .7f, tag + ": gold stays top right");
            Check(menu.GetGlobalRect().Position.Y > battle.GetViewportRect().Size.Y * .7f, tag + ": settings stays bottom left");
            state.SetShowDevUi(true); Call(battle, "UpdateHud");
            Check(!Walk(battle).OfType<Label>().Any(l => l.IsVisibleInTree() &&
                (l.Text.Contains("Wave") || l.Text.Contains("surge") || l.Text.Contains("Courage:"))),
                tag + ": wave forecasts and countdowns are absent");
            Check(Walk(battle).OfType<BattleTerrainCanvas>().Any() && BattleTerrainCanvas.GroundMaterial("city") != null, tag + ": authored scene and fine terrain both render");
            Call(battle, "ClearArmedSelection");
            foreach (var (unit, index) in GameData.PlayerRosterIds.Take(3).Select((unit, index) => (unit, index)))
                typeof(BattleController).GetMethod("SpawnUnit", hidden)!.Invoke(battle,
                    new object[] { Team.Player, new UnitStats(GameData.GetUnit(unit)), new Vector2(330 + index * 60, 320 + index * 58) });
            typeof(BattleController).GetMethod("SpawnUnit", hidden)!.Invoke(battle,
                new object[] { Team.Enemy, new UnitStats(GameData.GetUnit("enemy_walker")), new Vector2(630, 370) });
            await Wait(.1);
            await Capture(tag + "-battle");
            menu.EmitSignal(BaseButton.SignalName.Pressed); await Wait(.1);
            Check(GetTree().Paused && Read<bool>(battle, "_battlePaused"), tag + ": menu freezes the fight");
            Send(new InputEventKey { Keycode = Key.Tab, Pressed = true });
            Check(GetViewport().GuiGetFocusOwner() == Read<Button>(battle, "_restartButton"), tag + ": keyboard focus stays within the pause menu");
            Check(Read<Button>(battle, "_restartButton").Icon != null && Read<Button>(battle, "_restartButton").AccessibilityName.Contains("rations"), tag + ": restart exposes its ration price");
            await Capture(tag + "-menu");
            Call(battle, "OpenBattleSettings"); await Wait(.15);
            Check(GetTree().Paused && Walk(battle).OfType<SettingsMenu>().Any(), tag + ": settings opens over the paused battle");
            var slider = Walk(battle).OfType<HSlider>().First();
            var volume = state.MusicVolumePercent; slider.Value = volume == 42 ? 43 : 42;
            Check(state.MusicVolumePercent != volume, tag + ": audio controls work while paused");
            await Capture(tag + "-settings");
            if (phone)
            {
                GetWindow().Size = new Vector2I(667, 375); await Wait(.15);
                var frame = Walk(Read<RealmModal>(battle, "_battleSettingsModal")).OfType<PanelContainer>().First();
                Check(new Rect2(Vector2.Zero, battle.GetViewportRect().Size).Encloses(frame.GetGlobalRect()), "Phone settings refits after resizing");
                GetWindow().Size = size; await Wait(.15);
            }
            Send(new InputEventKey { Keycode = Key.Escape, Pressed = true }); await Wait(.1);
            Check(Read<RealmModal>(battle, "_battleSettingsModal") == null && GetTree().Paused && Read<CenterContainer>(battle, "_pauseOverlay").Visible,
                tag + ": Escape returns to pause menu without restarting the fight");
            var empty = state.BuildSaveData(); empty.Food = 0; empty.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); state.RestoreCloudSave(empty);
            Call(battle, "TryRestartBattle");
            Check(GetTree().CurrentScene == battle && GetTree().Paused && state.Food == 0 && Read<Label>(battle, "_restartMessage").Visible,
                tag + ": unaffordable restart preserves the paused battle and explains the shortage");
            var stocked = state.BuildSaveData(); stocked.Food = 24; state.RestoreCloudSave(stocked);
            var before = state.Food; var cost = state.GetStageEntryFoodCost(1);
            Call(battle, "TryRestartBattle"); Call(battle, "TryRestartBattle");
            Check(state.Food == before - cost && !GetTree().Paused, tag + ": restart spends exactly one entry price and releases the tree pause");
            await Wait(.8);
            Check(GetTree().CurrentScene is BattleController replacement && replacement != battle, tag + ": restart opens a fresh fight");
            var fresh = (BattleController)GetTree().CurrentScene; fresh.SetPhysicsProcess(false);
            if (Read<bool>(fresh, "_battlePaused")) Call(fresh, "TogglePause");
            if (tag == "desktop")
            {
                typeof(BattleController).GetMethod("EndBattle", hidden)!.Invoke(fresh, new object[] { false }); await Wait(.1);
                var depleted = state.BuildSaveData(); depleted.Food = 0; depleted.FoodRechargedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); state.RestoreCloudSave(depleted);
                Call(fresh, "HandleEndPanelPrimaryAction");
                Check(GetTree().CurrentScene == fresh && Read<Label>(fresh, "_endRetryMessage").Visible, "Result-screen retry also rejects an unaffordable restart");
                var retrySave = state.BuildSaveData(); retrySave.Food = 24; state.RestoreCloudSave(retrySave);
                var retryFood = state.Food; Call(fresh, "HandleEndPanelPrimaryAction"); Call(fresh, "HandleEndPanelPrimaryAction");
                Check(state.Food == retryFood - cost, "Result-screen retry also charges the ration price exactly once");
                await Wait(.8); fresh = (BattleController)GetTree().CurrentScene; fresh.SetPhysicsProcess(false);
            }
            Call(fresh, "TogglePause"); Call(fresh, "RetreatToMap"); await Wait(.8);
            Check(GetTree().CurrentScene is MapMenu && !GetTree().Paused, tag + ": quit returns to the map without leaving it paused");
        }
        MobilePresentation.TestOverride = false; GetWindow().Size = new Vector2I(1280, 720);
        foreach (var stage in GameData.Stages.GroupBy(stage => stage.MapId).Where(zone => zone.Key != "city").Select(zone => zone.First().StageNumber))
        {
            state.SetSelectedStage(stage); state.PrepareCampaignBattle(); await Open("Battle");
            var battle = (BattleController)GetTree().CurrentScene; battle.SetPhysicsProcess(false);
            if (Read<bool>(battle, "_battlePaused")) Call(battle, "TogglePause");
            Check(BattleTerrainCanvas.GroundMaterial(GameData.GetStage(stage).MapId) != null, $"Stage {stage}: zone-specific detail is available");
            await Capture($"zone-stage-{stage:00}");
        }
        MobilePresentation.TestOverride = null; GetTree().Paused = false;
        await Open("MainMenu");
        await LiveUiReview.StopAudio(this);
        GD.Print($"BATTLE_POLISH_REVIEW_RESULT: {_failures} failures"); QuitAfterAudio(_failures == 0 ? 0 : 1);
    }
}
