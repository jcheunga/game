using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class UiReviewSmoke
{
    /// <summary>
    /// --commanders: plays each commander's signature move against a small line and captures its warning and its
    /// landing, then lets a hazard stage run to its first strike. Captures land in artifacts/commanders.
    /// </summary>
    private async Task ReviewCommanders()
    {
        _output = ProjectSettings.GlobalizePath("res://artifacts/commanders");
        System.IO.Directory.CreateDirectory(_output);
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        T Read<T>(BattleController battle, string field) => (T)typeof(BattleController).GetField(field, hidden)!.GetValue(battle)!;
        Unit Spawn(BattleController battle, Team team, string id, Vector2 at)
        {
            typeof(BattleController).GetMethod("SpawnUnit", hidden)!.Invoke(battle, new object[] { team, new UnitStats(GameData.GetUnit(id)), at });
            return Read<List<Unit>>(battle, "_units").Last();
        }
        var state = GameState.Instance;
        var fixture = state.BuildSaveData();
        fixture.Food = 999; fixture.ShowFpsCounter = false; fixture.HighestUnlockedStage = GameData.MaxStage;
        fixture.OwnedPlayerUnitIds = GameData.PlayerRosterIds.ToArray();
        fixture.ActiveDeckUnitIds = GameData.PlayerRosterIds.Take(3).ToArray();
        state.RestoreCloudSave(fixture);
        GetWindow().Size = new Vector2I(1280, 720);

        var commanders = GameData.GetEnemyUnits().Where(u => u.IsBoss && !string.IsNullOrEmpty(u.SignatureAbilityId)).ToArray();
        foreach (var commander in commanders)
        {
            state.SetSelectedStage(1); state.PrepareCampaignBattle(); await Open("Battle");
            var battle = (BattleController)GetTree().CurrentScene;
            if (Read<bool>(battle, "_battlePaused")) typeof(BattleController).GetMethod("TogglePause", hidden)!.Invoke(battle, null);
            var troops = GameData.PlayerRosterIds.Take(4).Select((id, i) => Spawn(battle, Team.Player, id, new Vector2(330 + i * 34, 330 + i % 2 * 40))).ToArray();
            var boss = Spawn(battle, Team.Enemy, commander.Id, new Vector2(520, 350));
            var escort = Spawn(battle, Team.Enemy, "enemy_walker", new Vector2(560, 380));
            await Wait(.4);
            var health = troops.Sum(t => t.Health); var shield = boss.ShieldHealth; var escortShield = escort.ShieldHealth;
            typeof(BattleController).GetMethod("TriggerCommanderSignature", hidden)!.Invoke(battle, new object[] { boss });
            await Wait(.7);
            await Capture("signature-" + commander.SignatureAbilityId + "-warning");
            await Wait(.8);
            await Capture("signature-" + commander.SignatureAbilityId + "-landed");
            var status = Read<Label>(battle, "_statusLabel")?.Text ?? "";
            Check(boss.IsDead || status.Contains(BattleController.SignatureName(commander.SignatureAbilityId)) ||
                troops.Sum(t => t.Health) < health || boss.ShieldHealth > shield || escort.ShieldHealth > escortShield,
                $"{commander.DisplayName}: {BattleController.SignatureName(commander.SignatureAbilityId)} shows on the field");
        }

        var hazardStage = GameData.Stages.First(StageHazards.HasHazards);
        state.SetSelectedStage(hazardStage.StageNumber); state.PrepareCampaignBattle(); await Open("Battle");
        var field = (BattleController)GetTree().CurrentScene;
        if (Read<bool>(field, "_battlePaused")) typeof(BattleController).GetMethod("TogglePause", hidden)!.Invoke(field, null);
        var first = hazardStage.Hazards.OrderBy(h => h.StartTime).First();
        await Wait(first.StartTime + first.WarningDuration * .6f);
        Check(Read<System.Collections.IList>(field, "_groundStrikes").Count > 0, $"Stage {hazardStage.StageNumber}: {first.Label} warns before it strikes");
        await Capture("hazard-warning");
        await Wait(first.WarningDuration * .4f + .15f);
        await Capture("hazard-landed");
    }
}
