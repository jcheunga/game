public static class CombatGuideCatalog
{
    public const string StatusLegend="CAST: a special action is winding up; amber ground marks show where to avoid it. STUN/SLEEP: cannot act. ROOT: cannot move. SIL: special actions are silenced. ARM−: armor is temporarily reduced. WOUND: healing is reduced. BURN: damage over time. SHIELD: temporary protection. MARK: allies prioritize this target. HIDE: concealed until revealed or attacking. CRIT: a critical hit dealt bonus damage.";
    public static string StageTip(int stage)
    {
        var data=GameData.GetStage(stage);
        if(!string.IsNullOrWhiteSpace(data.NewEnemyId))
        {
            var enemy=GameData.GetUnit(data.NewEnemyId);
            return $"New threat: {enemy.DisplayName}. {enemy.Weakness}";
        }
        if(stage==80)return "Steppe Warlord: keep a melee line, protect your Medic, and interrupt support casters. Spread bodies against charge pressure.";
        return CampaignProgressionCatalog.Preparation(stage);
    }
}
