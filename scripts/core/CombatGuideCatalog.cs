using Godot;

/// <summary>What the combat guide explains: the signs drawn over units in battle (<see cref="Unit"/>'s status chips).</summary>
public static class CombatGuideCatalog
{
    public static readonly (string Label, string Meaning)[] Signs =
    {
        ("CAST", "A special attack is charging here"),
        ("MARK", "Allies focus this target"),
        ("ARM−", "Armor broken for a while"),
        ("WOUND", "Healing received is reduced"),
        ("HIDE", "Hidden until it strikes"),
        // Shown on the unit itself; chips only with high contrast on.
        ("STUN", "Cannot act"),
        ("SLEEP", "Cannot act until hit"),
        ("ROOT", "Cannot move"),
        ("SIL", "Cannot use special attacks"),
        ("BURN", "Burning over time"),
        ("SHIELD", "A shield takes the damage"),
    };

    /// <summary>The stage's lead line: its new enemy, or the suggested level and how to play it.</summary>
    public static string StageTip(int stage)
    {
        var data = GameData.GetStage(stage);
        if (!string.IsNullOrWhiteSpace(data.NewEnemyId))
            return $"New threat: {GameData.GetUnit(data.NewEnemyId).DisplayName}. Read its counter below.";
        return CampaignProgressionCatalog.Preparation(stage);
    }
}
