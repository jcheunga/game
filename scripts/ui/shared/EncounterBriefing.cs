using System.Linq;
using Godot;

public static class EncounterBriefing
{
    public static void Show(Node host, StageDefinition stage)
    {
        var layer = new CanvasLayer { Layer = 70, Name = "EncounterBriefing" };
        host.AddChild(layer);
        var modal = RealmModal.OpenInspector(layer, $"Stage {stage.StageNumber} · Battle tips", "codex", 860, 560);
        var body = RealmUi.Scroll(modal.Content);
        ((ScrollContainer)body.GetParent()).SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        body.AddChild(RealmUi.Label(CombatGuideCatalog.StageTip(stage.StageNumber), 22));
        body.AddChild(RealmUi.Label("Keep affordable melee troops in front, protect your support units, and collect courage packs before they fade. You can revisit this guide from the battle menu.", 20, true));
        using var rng = new RandomNumberGenerator();
        var director = new BattleSpawnDirector(rng);
        director.Initialize(stage.StageNumber, stage, GameData.Combat, GameData.GetEnemyUnits());
        var difficulty = GameState.Instance.GetDifficulty();
        director.SetEnemyScaleModifiers(difficulty.EnemyHealthScale, difficulty.EnemyDamageScale);
        foreach (var id in stage.Waves.SelectMany(w => w.Entries).Select(e => e.UnitId).Distinct())
        {
            var enemy = GameData.GetUnit(id);
            body.AddChild(RealmUi.SectionTitle(enemy.DisplayName));
            body.AddChild(RealmUi.Label(enemy.Description, 20));
            if (director.TryBuildEnemyStats(id, out var stats))
                body.AddChild(RealmUi.Label($"Level {stats.Level} · Health {stats.MaxHealth:0} · Damage {stats.AttackDamage:0.#} · Armor {stats.Armor:0.#} · Magic resistance {stats.MagicalResistance:0.#}%", 20, true));
            body.AddChild(RealmUi.Label("Counter: " + (string.IsNullOrWhiteSpace(enemy.Weakness) ? "Hold the melee line and watch for attack warnings." : enemy.Weakness), 20));
        }
        body.AddChild(RealmUi.SectionTitle("Equipment and growth"));
        body.AddChild(RealmUi.Label("Units level to 20 with modest stat gains. Equipment improves a role within shared limits; compare applied bonuses before equipping. Early troops remain useful as affordable frontline choices.", 20));
        RealmModal.Polish(body);
    }
}
