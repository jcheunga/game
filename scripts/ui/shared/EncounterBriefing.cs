using System.Linq;
using Godot;

/// <summary>
/// Battle prep's "battle tips": the stage's lead line and a card for each enemy in its waves (figure, stats at this
/// stage and difficulty, and how to counter it). The battle guide's "This battle" page shows the same.
/// </summary>
public static class EncounterBriefing
{
    public static void Show(Node host, StageDefinition stage)
    {
        var page = GuidePage.Open(host, "EncounterBriefing", $"Stage {stage.StageNumber} · Battle tips", "codex", 70);
        Build(page.Body, stage);
        page.Finish();
    }

    public static void Build(VBoxContainer body, StageDefinition stage)
    {
        var newEnemy = stage.NewEnemyId ?? "";
        body.AddChild(GuidePage.Callout(newEnemy.Length > 0 ? "icon-skull-red" : "icon-open-book", CombatGuideCatalog.StageTip(stage.StageNumber)));
        using var rng = new RandomNumberGenerator();
        var director = new BattleSpawnDirector(rng);
        director.Initialize(stage.StageNumber, stage, GameData.Combat, GameData.GetEnemyUnits());
        var difficulty = GameState.Instance.GetDifficulty();
        director.SetEnemyScaleModifiers(difficulty.EnemyHealthScale, difficulty.EnemyDamageScale);
        // The boss and the stage's new enemy lead; the rest keep the order they march in.
        var enemies = stage.Waves.SelectMany(w => w.Entries).Select(e => e.UnitId).Distinct().Select(GameData.GetUnit)
            .OrderByDescending(e => e.IsBoss).ThenByDescending(e => e.Id == newEnemy).ToArray();
        var grid = GuidePage.Grid(2);
        body.AddChild(grid);
        foreach (var enemy in enemies)
            EnemyCard(grid, enemy, director.TryBuildEnemyStats(enemy.Id, out var stats) ? stats : null,
                enemy.IsBoss ? "Boss" : enemy.Id == newEnemy ? "New" : "");
    }

    private static void EnemyCard(Control parent, UnitDefinition enemy, UnitStats stats, string tag)
    {
        var card = GuidePage.Card(parent);
        card.GetParent<Control>().Name = enemy.DisplayName;
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 12); card.AddChild(head);
        head.AddChild(GuidePage.Portrait(null, 76, enemy));
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 2);
        head.AddChild(titles);
        var name = new HBoxContainer(); name.AddThemeConstantOverride("separation", 8); titles.AddChild(name);
        name.AddChild(RealmUi.SectionTitle(enemy.DisplayName));
        if (tag.Length > 0) name.AddChild(GuidePage.Chip(tag, new Color(tag == "Boss" ? "eab18d" : "efd082")));
        titles.AddChild(RealmUi.Label((stats != null ? $"Level {stats.Level} · " : "") + enemy.Description, 18, true));
        // Icons and values only, so two cards sit side by side; each cell names its stat in its hint.
        if (stats != null)
            card.AddChild(new StatSheet(new ArmoryDetailUi.Stat[] {
                new("heart", "Health", $"{stats.MaxHealth:0}"), new("sword", "Damage", $"{stats.AttackDamage:0.#}"),
                new("shield", "Armor", $"{stats.Armor:0.#}"), new("mana", "Magic resist", $"{stats.MagicalResistance:0.#}%") }, 4, names: false));
        var counter = string.IsNullOrWhiteSpace(enemy.Weakness) ? "Hold the melee line and watch for attack warnings." : enemy.Weakness;
        card.AddChild(GuidePage.Explained(GuidePage.Picture(RoyalKit.Texture("icon-crossed-swords"), new Vector2(24, 24)), counter, 24));
    }
}
