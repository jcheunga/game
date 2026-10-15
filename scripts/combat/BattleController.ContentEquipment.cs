using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController
{
    private IEnumerable<EquipmentDefinition> ContentEquipment(Unit unit)
        => IsInstanceValid(unit) && unit.Team==Team.Player && !unit.IsSummoned && !unit.IsTacticalObject
            ? GameState.Instance.GetBattleEquipment(unit.DefinitionId) : Array.Empty<EquipmentDefinition>();
    private IEnumerable<EnchantmentDefinition> ContentEnchantments(Unit unit)
        => IsInstanceValid(unit) && unit.Team==Team.Player && !unit.IsSummoned && !unit.IsTacticalObject
            ? GameState.Instance.GetBattleEnchantments(unit.DefinitionId) : Array.Empty<EnchantmentDefinition>();
    private readonly HashSet<string> _contentObjectiveRewards = new();
    private float _gateExposedUntil;

    private void ApplyContentSpellEquipment()
    {
        foreach(var unit in _units.Where(u=>!u.IsDead && u.Team==Team.Player))
            foreach(var item in ContentEquipment(unit))
            {
                if(item.Id=="relic_sages_ring" && unit.TryEffect(item.Id,10)) unit.ContentCooldown=Math.Max(0,unit.ContentCooldown-.75f);
                if(item.Id=="relic_runic_capacitor" && unit.TryEffect("charge_gain",4))
                    unit.EffectReadyAt["runic_charge"]=Math.Min(2,unit.EffectReadyAt.GetValueOrDefault("runic_charge")+1);
            }
    }

    private void ApplyContentInterruptEquipment(Unit unit)
    {
        foreach(var item in ContentEquipment(unit))
        {
            if(item.Id=="relic_sentinel_buckle" && unit.TryEffect(item.Id,10)) unit.GiveShield(3,3);
            if(item.Id=="relic_hardened_fang" && unit.TryEffect(item.Id,10)) unit.ApplyAttackRecovery(.95f,2);
        }
    }

    private void ApplyContentObjectiveEquipment(string key,Vector2 position)
    {
        if(!_contentObjectiveRewards.Add(key)) return;
        var friends=NearbyContentUnits(Team.Player,position,100).ToArray();
        if(friends.Any(u=>ContentEquipment(u).Any(i=>i.Id=="relic_caravan_token"))) SpawnCouragePickup(position,2);
        if(friends.Any(u=>ContentEquipment(u).Any(i=>i.Id=="relic_crownroad_charter"))) GainMana(7);
        foreach(var friend in friends)
            if(ContentEquipment(friend).Any(i=>i.Id=="relic_tower_ascendant")) friend.ApplyTemporaryCombatBuff(1.04f,1,2);
    }

    private void ApplyContentDeployEquipment(Unit unit)
    {
        foreach(var item in ContentEquipment(unit))
        {
            switch(item.EffectId)
            {
                case "relic_guardian_shield": case "relic_rampart_seal":
                    unit.GiveShield(5,4);break;
                case "relic_nightveil_brooch":unit.Conceal(1.5f);break;
                case "relic_hearthstone": case "relic_springwater_charm":
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,45).Take(3)) ally.Cleanse();
                    break;
                case "relic_marshal_s_signet":
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,50).Take(3)) ally.ApplyTemporaryCombatBuff(1.05f,1.04f,2);
                    break;
                case "relic_raid_plague_censer": case "relic_tower_sentinel":
                    unit.GiveShield(4,3);break;
                case "relic_windrunner_cloak":unit.SlowDurationScale=.85f;break;
                case "relic_fireward_clasp":unit.BurnDurationScale=.8f;unit.BurnDamageScale=.9f;break;
                case "relic_plagueglass_mask":unit.HealingPenaltyFloor=.8f;break;
                case "relic_immortal_wreath":unit.LowHealthHealingBonus=.06f;break;
                case "relic_windstring":unit.FirstWindupScale=.9f;break;
                case "relic_chain_hoist_charm":unit.ApplyTemporarySpeedModifier(1.05f,30);break;
            }
        }
    }

    private float ModifyContentEquipmentDamage(Unit attacker,Unit target,float damage,bool ranged)
    {
        if(!IsInstanceValid(attacker)) return damage;
        var bonus=0f;
        foreach(var item in ContentEquipment(attacker))
        {
            switch(item.EffectId)
            {
                case "relic_farwatch_lens":bonus+=Mathf.Clamp(attacker.Position.DistanceTo(target.Position)/100,.02f,.06f);break;
                case "relic_huntsman_s_knot":
                    if(NearbyContentUnits(target.Team,target.Position,45).Count()==1) bonus+=.06f;
                    break;
                case "relic_ashen_rosary":if(target.IsBurning) bonus+=.06f;break;
                case "relic_winter_ribbon":if(target.MovementControlled) bonus+=.05f;break;
                case "relic_assassin_s_token":
                    if(attacker.TryEffect(item.Id+target.CombatLifetime,10)) bonus+=.08f;
                    break;
                case "relic_siegebreaker_s_crest":if(target.Armor>=25) bonus+=.06f;break;
                case "relic_tower_apex":if(target.Definition.IsBoss) bonus+=.06f;break;
                case "relic_wolftooth_charm":
                    if(NearbyContentUnits(attacker.Team,attacker.Position,50).Any(a=>a!=attacker && a.VisualClass=="hound")) bonus+=.05f;
                    break;
                case "relic_king_s_commission":
                    if(NearbyContentUnits(attacker.Team,attacker.Position,50).Any(a=>a!=attacker && a.Definition.SquadTag!=attacker.Definition.SquadTag)) bonus+=.05f;
                    break;
                case "relic_royal_execution_writ":
                    if(target.HealthRatio<.15f && !target.Definition.IsBoss && attacker.TryEffect(item.Id,12)) bonus+=.15f;
                    break;
                case "relic_blade_of_ruin":
                    if(attacker.CommittedAttacks%5==0 && attacker.Health>2 && attacker.TryEffect(item.Id,8))
                    { attacker.TakeDamage(1,attacker.UnitName,CombatDamageType.True);bonus+=.08f; }
                    break;
            }
        }
        foreach(var item in ContentEquipment(target))
        {
            if(item.Id=="relic_hardened_bulwark" && damage>12 && target.TryEffect(item.Id,999)) damage*=.85f;
            if(item.Id=="relic_gateward_medallion" && ranged && (attacker.Definition.VisualClass is "ballista" or "siegetower" || attacker.Definition.ContentBehavior=="siege_shot")) damage*=.95f;
            if(item.Id=="relic_phantom_mantle" && ranged && target.TryEffect("mantle_dodge",14)) damage=0;
        }
        return damage*(1+Math.Min(.12f,bonus));
    }

    private void ApplyContentHitEquipment(Unit source,Unit target,float actual,bool ranged)
    {
        foreach(var enchant in ContentEnchantments(source))
        {
            if(enchant.LifestealRatio>0) source.Heal(actual*enchant.LifestealRatio);
            if(enchant.Id=="ench_flame_touch" && source.TryEffect(enchant.Id,6)) target.ApplyBurn(1.5f,2,source.UnitName);
            if(enchant.Id=="ench_poison" && source.TryEffect(enchant.Id,8)) target.ApplyHealingPenalty(.85f,2);
        }
        foreach(var item in ContentEquipment(source))
        {
            switch(item.EffectId)
            {
                case "relic_cinder_ring":case "relic_coalglass_vial":
                    if(source.CommittedAttacks%4==0 && source.TryEffect(item.Id,5)) target.ApplyBurn(2,2,source.UnitName);
                    break;
                case "relic_rime_needle":
                    if(source.CommittedAttacks%4==0 && source.TryEffect(item.Id,6)) target.ApplyTemporarySpeedModifier(.8f,1.5f);
                    break;
                case "relic_falcon_feather":
                    if(source.TryEffect("first_mark",999)) {target.FocusMarkTime=3;_targetLocks.Clear();}
                    break;
                case "relic_stormcaller_sigil":
                    if(source.Definition.ContentBehavior=="chain_attack" && source.TryEffect(item.Id,8)) target.TakeDamage(1,source.UnitName,CombatDamageType.Magical);
                    break;
                case "relic_iron_prayer":if(source.CommittedAttacks%4==0) source.Heal(2);break;
                case "relic_bloodroot_pendant":source.Heal(actual*.025f);break;
                case "relic_twinbolt_clasp":case "relic_dragonbone_quiver":case "relic_stormglass_prism":
                    if(ranged && source.CommittedAttacks%5==0 && source.TryEffect(item.Id,5))
                    {
                        var next=NearbyContentUnits(target.Team,target.Position,45).FirstOrDefault(u=>u!=target);
                        if(next!=null) ContentDamage(source,next,actual*.25f,true,false,source.AttackDamageType,false);
                    }
                    break;
                case "relic_war_brand":
                    if(target.ArmorBreak>0 && source.TryEffect(item.Id,8)) source.ApplyTemporaryCombatBuff(1.06f,1,2);
                    break;
                case "relic_emberforge_anvil":if(source.TryEffect(item.Id,6)) source.GuardPhysical(.06f,1);break;
                case "relic_raid_iron_heart":
                    if(source.MovedRecently==false && source.TryEffect(item.Id,10)) source.GiveShield(3,3);
                    break;
                case "relic_moonstep_anklet":
                    if(source.CommittedAttacks%5==0 && source.TryEffect(item.Id,8)) source.ProjectileDodgeCharges=1;
                    break;
                case "relic_runic_capacitor":case "relic_hardened_sigil":
                    if(item.Id=="relic_runic_capacitor" && source.EffectReadyAt.TryGetValue("runic_charge",out var charges) && charges>0)
                    { source.EffectReadyAt["runic_charge"]=charges-1;target.TakeDamage(2,source.UnitName,CombatDamageType.Magical); }
                    else if(item.Id=="relic_hardened_sigil" && source.CommittedAttacks%5==0 && source.TryEffect(item.Id,8)) target.TakeDamage(2,source.UnitName,CombatDamageType.Magical);
                    break;
                case "relic_counterweight":
                    if(ranged && source.AttackCooldown>1.5f && source.CommittedAttacks%3==0 && source.TryEffect(item.Id,6))
                    {
                        var secondary=NearbyContentUnits(target.Team,target.Position,35).FirstOrDefault(t=>t!=target);
                        if(secondary!=null) secondary.TakeDamage(2,source.UnitName,source.AttackDamageType);
                        source.ApplyAttackRecovery(1.05f,2);
                    }
                    break;
                case "relic_hollow_chalice":
                    var corpse=_contentCorpses.FirstOrDefault(c=>c.Team==Team.Enemy && c.Position.DistanceTo(source.Position)<50);
                    if(corpse!=null && source.TryEffect(item.Id,10)) { _contentCorpses.Remove(corpse);source.Heal(4); }
                    break;
            }
        }
        if(target.IsTacticalObject && target.IsDead && ContentEquipment(source).Any(i=>i.Id=="relic_demolition_rune") && source.TryEffect("demolition",8))
            AddContentField("burn",source.Team,target.Position,22,1.5f,1,source.UnitName);
        foreach(var enchant in ContentEnchantments(target))
            if(!ranged && enchant.ThornsDamageRatio>0) source.TakeDamage(Math.Min(2,actual*enchant.ThornsDamageRatio),target.UnitName,CombatDamageType.Physical);
        foreach(var item in ContentEquipment(target))
            if(item.Id=="relic_mirror_shard" && !ranged) source.TakeDamage(Math.Min(2,actual*.04f),target.UnitName,CombatDamageType.Physical);
    }

    private void ApplyContentHealEquipment(Unit healer,Unit patient)
    {
        foreach(var item in ContentEquipment(healer))
        {
            switch(item.Id)
            {
                case "relic_mercy_bell":if(patient.HealthRatio<.4f) patient.Heal(1);break;
                case "relic_field_surgeon_s_badge":healer.GiveShield(2,2);break;
                case "relic_glacial_band":healer.ContentState="chill_next";break;
                case "relic_mender_s_thread":if(patient.HealthRatio>.95f) patient.GiveShield(2,2);break;
            }
        }
        foreach(var item in ContentEquipment(patient))
        {
            if(item.Id=="relic_phantom_bell") patient.GuardPhysical(.03f,2);
            if(item.Id=="relic_oathbound_chain" && patient.TryEffect(item.Id,8))
                NearbyContentUnits(patient.Team,patient.Position,40).FirstOrDefault(a=>a!=patient)?.Heal(1);
        }
    }

    private void ApplyContentAbilityEquipment(Unit unit)
    {
        foreach(var item in ContentEquipment(unit))
        {
            if(!unit.TryEffect("ability_"+item.Id,10)) continue;
            switch(item.Id)
            {
                case "relic_frostbound_crown":case "relic_icebound_hourglass":case "relic_glacial_band":
                    foreach(var enemy in NearbyContentUnits(Team.Enemy,unit.Position,45).Take(2)) enemy.ApplyTemporarySpeedModifier(.85f,1.5f);
                    break;
                case "relic_captain_s_whistle":
                    NearbyContentUnits(Team.Enemy,unit.Position,45).FirstOrDefault(e=>_contentCasts.ContainsKey(e))?.Silence(.3f);
                    break;
                case "relic_raid_plague_censer":
                    NearbyContentUnits(Team.Player,unit.Position,40).FirstOrDefault(a=>a!=unit)?.Cleanse();
                    break;
                case "relic_pinnacle_crown":case "relic_tower_pinnacle":
                    if(unit.TryEffect("pinnacle_once",999)) NearbyContentUnits(Team.Enemy,unit.Position,70).FirstOrDefault()?.TakeDamage(unit.AttackDamage*.25f,unit.UnitName,unit.AttackDamageType);
                    break;
                case "relic_hardened_crown":case "relic_hardened_fang":case "relic_hardened_soul":
                    unit.ApplyTemporaryCombatBuff(1.04f,1f,2);
                    break;
            }
        }
        foreach(var enchant in ContentEnchantments(unit))
            if(enchant.Id=="ench_arcane_echo" && unit.TryEffect(enchant.Id,14))
                NearbyContentUnits(Team.Enemy,unit.Position,60).FirstOrDefault()?.TakeDamage(2,unit.UnitName,CombatDamageType.Magical);
    }

    private bool TryContentEquipmentReturn(Unit dead)
    {
        if(dead.HasReturned) return false;
        var guardian=NearbyContentUnits(dead.Team,dead.Position,45).FirstOrDefault(a=>a!=dead &&
            ContentEquipment(a).Any(i=>i.Id=="relic_lantern_of_dawn") && a.TryEffect("dawn_save",999));
        if(guardian!=null && dead.TryReturn(.01f)) return true;
        foreach(var item in ContentEquipment(dead))
        {
            if(item.Id is "relic_phoenix_ember" or "relic_basilica_hourglass")
                if(dead.TryReturn(.2f)) return true;
            if(item.Id=="relic_second_breath_seal" && dead.TryReturn(.01f)) return true;
            if(item.Id=="relic_lantern_of_dawn" && dead.TryEffect("lethal_save",999))
            {
                var ally=NearbyContentUnits(dead.Team,dead.Position,45).FirstOrDefault(a=>a!=dead && ContentEquipment(a).Any(i=>i.Id=="relic_lantern_of_dawn"));
                if(ally!=null && ally.TryEffect("dawn_save",999) && dead.TryReturn(.01f)) return true;
            }
        }
        return false;
    }

    private void ApplyContentDeathEquipment(Unit dead)
    {
        if(dead.Team==Team.Player)
        {
            foreach(var item in ContentEquipment(dead))
            {
                if(item.Id=="relic_supply_ledger" && !dead.HasReturned) _courage=Math.Min(_maxCourage,_courage+dead.Definition.Cost*.1f);
                if(item.Id=="relic_saint_s_reliquary" && !dead.HasReturned)
                    foreach(var ally in NearbyContentUnits(Team.Player,dead.Position,45).Take(3)) ally.Heal(3);
                if(item.Id=="relic_sapper_s_fuse" && !dead.HasReturned)
                    AddContentField("burn",Team.Player,dead.Position,25,2,2,dead.UnitName);
            }
            return;
        }
        foreach(var ally in NearbyContentUnits(Team.Player,dead.Position,70))
        {
            foreach(var item in ContentEquipment(ally))
            {
                switch(item.Id)
                {
                    case "relic_trailfinder_boots":
                        if(ally.TryEffect(item.Id,8)) ally.ApplyTemporarySpeedModifier(1.08f,2);
                        break;
                    case "relic_tombstone_shard":if(ally.TryEffect(item.Id,8)) ally.Heal(2);break;
                    case "relic_moonfire_talisman":
                        if(ally.TryEffect(item.Id,10)) ally.ApplyTemporaryCombatBuff(1.06f,1,2);
                        break;
                    case "relic_grave_silk":case "relic_gravewarden_s_key":
                        if(ally.TryEffect(item.Id,10)) _contentCorpses.RemoveAll(c=>c.Position.DistanceTo(dead.Position)<4);
                        break;
                    case "relic_umbral_coin":
                        if(dead.Definition.Armor>=25 && ally.GeneratedCourage+2<=ally.Definition.Cost*.2f && ally.TryEffect(item.Id,15))
                        { ally.GeneratedCourage+=2;SpawnCouragePickup(dead.Position,2); }
                        break;
                    case "relic_hardened_soul":case "relic_tower_ascendant":
                        if(ally.TryEffect(item.Id,12)) ally.ApplyTemporaryCombatBuff(1.04f,1,2);
                        break;
                    case "relic_sunfall_spur":
                        if(ally.ContentState=="charging" && ally.TryEffect(item.Id,10)) ally.ContentCooldown=Math.Max(0,ally.ContentCooldown-.5f);
                        break;
                    case "relic_crown_of_valor":case "relic_raid_sovereign_mantle":
                        if(ally.TryEffect(item.Id,12)) foreach(var friend in NearbyContentUnits(Team.Player,ally.Position,45).Take(3)) friend.ApplyTemporaryCombatBuff(1.04f,1,2);
                        break;
                }
            }
        }
    }

    private void TickContentEquipment(Unit unit)
    {
        foreach(var item in ContentEquipment(unit))
        {
            switch(item.Id)
            {
                case "relic_tower_charm":if(!unit.MovedRecently) unit.GuardPhysical(.04f,.15f);break;
                case "relic_last_wall_token":if(unit.HealthRatio<.3f && unit.TryEffect(item.Id,14)) unit.GiveShield(3,3);break;
                case "relic_dragon_heart":
                    if(unit.HealthRatio<.3f && unit.TryEffect(item.Id,999))
                        foreach(var enemy in NearbyContentUnits(Team.Enemy,unit.Position,30).Take(3)) enemy.ApplyBurn(1,2,unit.UnitName);
                    break;
                case "relic_hoarfrost_medal":if(unit.MovementControlled && unit.TryEffect(item.Id,12)) unit.GiveShield(2,2);break;
                case "relic_thawstone":if(unit.TryEffect(item.Id,12)) unit.Cleanse();break;
                case "relic_wandering_shrine":
                    if(!unit.MovedRecently && unit.TryEffect(item.Id,12)) foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,40).Take(2)) ally.Heal(2);
                    break;
                case "relic_spectral_lantern":
                    if(unit.TryEffect(item.Id,8)) foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,45)) ally.ShortenBlight();
                    break;
                case "relic_gloam_lantern":
                    foreach(var enemy in NearbyContentUnits(Team.Enemy,unit.Position,50)) enemy.Reveal();
                    break;
                case "relic_raid_iron_heart":
                    if(unit.ContentAge-unit.LastAttackAge>2 && unit.TryEffect(item.Id,12)) unit.GiveShield(3,3);
                    break;
                case "relic_raid_sovereign_mantle":
                    if(unit.TryEffect(item.Id,14)) foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,45).Take(3)) ally.ApplyTemporaryCombatBuff(1.04f,1,2);
                    break;
                case "relic_grave_lord_s_crown":case "relic_raid_grave_crown":
                    var corpse=_contentCorpses.FirstOrDefault(c=>c.Team==Team.Enemy && c.Position.DistanceTo(unit.Position)<50);
                    if(corpse!=null && unit.TryEffect(item.Id,15) && !_contentSummons.Any(p=>p.Value.Owner==unit && p.Value.Life==unit.CombatLifetime && !p.Key.IsDead))
                    { _contentCorpses.Remove(corpse);SpawnContentMinion(unit,"raised",20,3,8); }
                    break;
            }
        }
    }
}
