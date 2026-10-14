using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class CombatReviewSmoke
{
    private async Task ReviewBossRushGameplay()
    {
        var state=GameState.Instance;state.ResetProgress();state.SetShowHints(false);state.SetAnalyticsConsent(false);
        var save=state.BuildSaveData();save.HighestUnlockedStage=100;save.Gold=100000;
        save.OwnedPlayerUnitIds=GameData.PlayerRosterIds;save.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
        save.ActiveDeckUnitIds=new[]{"player_brawler","player_spear","player_defender","player_shooter","player_field_medic","player_coordinator"};
        save.OwnedPlayerSpellIds=new[]{"spell_fireball","spell_heal","spell_frost_burst"};save.ActiveDeckSpellIds=save.OwnedPlayerSpellIds;
        save.OwnedEquipmentIds=new[]{"relic_sharpened_edge","relic_war_brand","relic_guardian_shield","relic_crown_of_valor","relic_iron_pendant","relic_battle_drum"};
        save.UnitEquipmentSlots=save.ActiveDeckUnitIds.Zip(save.OwnedEquipmentIds).ToDictionary(pair=>pair.First,pair=>pair.Second);
        save.BaseUpgradeLevels=new[]{BaseUpgradeCatalog.HullPlatingId,BaseUpgradeCatalog.PantryId,BaseUpgradeCatalog.DispatchConsoleId,BaseUpgradeCatalog.SignalRelayId,BaseUpgradeCatalog.ProjectileWardId}.ToDictionary(id=>id,_=>3);
        Invoke(state,"ApplySavedData",save);state.PrepareBossRush(out _);
        var battle=GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();AddChild(battle);battle.SetPhysicsProcess(false);
        Invoke(battle,"InitializeContent",4100UL);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        var startArg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--rush-start="));
        var startRound=startArg==null?0:Math.Clamp(int.Parse(startArg.Split('=')[1]),0,13);
        if(startRound>0)Write(battle,"_rushCleared",startRound);
        var units=Read<System.Collections.Generic.List<Unit>>(battle,"_units");var deck=Read<BattleDeckState>(battle,"_deck");var tick=0;
        while(!Read<bool>(battle,"_battleEnded") && Read<float>(battle,"_elapsed")<2400)
        {
            if(tick++%30==0)
            {
                var courage=Read<float>(battle,"_courage");var friends=units.Where(u=>u.Team==Team.Player && !u.IsDead).ToArray();
                var front=friends.Count(u=>!u.UsesProjectile && u.Definition.SquadTag!=SquadSynergyCatalog.SupportTag);
                var preferred=front<2?deck.Roster.Where(u=>!u.UsesProjectile && u.SquadTag!=SquadSynergyCatalog.SupportTag).OrderBy(u=>friends.Count(f=>f.DefinitionId==u.Id)).FirstOrDefault():
                    !friends.Any(u=>u.DefinitionId=="player_field_medic")?GameData.GetUnit("player_field_medic"):
                    !friends.Any(u=>u.DefinitionId=="player_coordinator")?GameData.GetUnit("player_coordinator"):
                    deck.Roster.Where(u=>u.UsesProjectile).OrderBy(u=>friends.Count(f=>f.DefinitionId==u.Id)).FirstOrDefault();
                var card=deck.Roster.OrderBy(u=>u==preferred?0:1).FirstOrDefault(u=>deck.CanDeploy(u,courage,false,out _));if(card!=null)Invoke(battle,"DeployPlayerUnit",card);
                var hurt=friends.OrderBy(u=>u.HealthRatio).FirstOrDefault(u=>u.HealthRatio<.6f);
                var foe=units.FirstOrDefault(u=>u.Team==Team.Enemy && !u.IsDead);
                var spells=Read<BattleSpellState>(battle,"_spellDeck");var mana=Read<float>(battle,"_mana");
                var spell=hurt!=null?GameData.GetSpell("spell_heal"):foe!=null?GameData.GetSpell("spell_fireball"):null;
                if(spell!=null && mana>=spell.ManaCost && spells.GetCooldownRemaining(spell.Id)<=0)Invoke(battle,"TryCastSpellAt",spell,hurt?.Position??foe.Position);
            }
            battle._PhysicsProcess(1.0/60);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        GD.Print($"BOSS_RUSH_SAMPLE: rounds={Read<int>(battle,"_rushCleared")},seconds={Read<float>(battle,"_elapsed"):0.0},hull={Read<float>(battle,"_playerBaseHealth"):0.0},ended={Read<bool>(battle,"_battleEnded")}");
        Check(Read<bool>(battle,"_battleEnded") && Read<int>(battle,"_rushCleared")==14,
            startRound==0?"An endgame melee squad can complete a real boss-rush run":"An endgame melee squad can clear the selected late boss-rush encounter");
        await CloseBattle(battle);
    }
}
