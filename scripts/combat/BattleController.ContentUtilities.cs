using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController
{
    private Unit ContentSpellTarget(Team team,Vector2 point,float reach=80)
        => NearbyContentUnits(team,point,reach).FirstOrDefault(u=>!u.IsTacticalObject && !u.NonCombatant && !u.IsUntargetable);

    private string ApplyExpandedUtility(ResolvedSpellStats spell,Vector2 point)
    {
        var def=spell.Definition;
        if(def.CardKind=="tactical")
        {
            if(def.EffectType=="tactic_smoke_pot")
            { AddContentField("smoke",Team.Player,point,spell.Radius,spell.Duration,0,spell.DisplayName);_targetLocks.Clear();return "Smoke conceals allies until they attack."; }
            if(def.EffectType=="tactic_war_horn")
            { foreach(var enemy in NearbyContentUnits(Team.Enemy,point,spell.Radius).Take(1)) enemy.FocusMarkTime=spell.Duration;_targetLocks.Clear();return "Focus target marked."; }
            if(_units.Count(u=>u.Team==Team.Player && !u.IsDead)>=32) return "The battlefield is full.";
            var objectDef=new UnitDefinition {
                Id=def.Id,DisplayName=def.DisplayName,Side="Player",MaxHealth=Math.Max(1,def.ObjectHealth),
                Armor=def.ObjectArmor,MagicalResistance=def.ObjectMagicalResistance,
                Speed=0,AttackDamage=def.EffectType=="tactic_bolt_turret"?spell.Power:0,
                AttackRange=spell.Radius,AttackCooldown=1,UsesProjectile=def.EffectType=="tactic_bolt_turret",
                VisualClass=def.EffectType=="tactic_bolt_turret"?"ballista":"shield",PlaceholderVisual=true,
                ColorHex=def.ColorHex,DamagesStructures=false,BaseDamage=0,ContentBehavior=def.EffectType,
            };
            point=new Vector2(Mathf.Clamp(point.X,BattlefieldLeft+28,BattlefieldRight-60),Mathf.Clamp(point.Y,BattlefieldTop+SpawnVerticalPadding,BattlefieldBottom-SpawnVerticalPadding));
            var placed=SpawnUnit(Team.Player,new UnitStats(objectDef),point);
            placed.IsTacticalObject=true;placed.IsSummoned=true;placed.LifetimeRemaining=spell.Duration+.15f;
            placed.UtilityCharges=def.Charges;placed.ContentCooldown=def.EffectType=="tactic_supply_crate"?3:2;
            placed.EffectReadyAt["utility_pulse"]=placed.ContentCooldown;
            placed.CanDropCourage=false;
            return $"{def.DisplayName} placed.";
        }
        var target=ContentSpellTarget(Team.Enemy,point);
        var ally=ContentSpellTarget(Team.Player,point);
        switch(spell.EffectType)
        {
            case "aegis_bolt":
                if(ally!=null) ally.GiveShield(spell.Power,spell.Duration);
                break;
            case "radiant_rebuke":
                if(target!=null) { if(_contentCasts.ContainsKey(target)) target.Silence(.4f);target.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                break;
            case "tidal_push":
                foreach(var e in NearbyContentUnits(Team.Enemy,point,spell.Radius))
                { OffsetUnitWithinBattlefield(e,new Vector2(e.Definition.IsBoss?8:spell.SecondaryPower,0));e.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                break;
            case "anchor_hex":
                if(target!=null) { target.Root(spell.Duration);target.ContentState="anchored"; }
                break;
            case "molten_brand":
                if(target!=null) { target.ApplyArmorBreak(spell.SecondaryPower,spell.Duration);target.ApplyBurn(2,spell.Duration,spell.DisplayName);target.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                break;
            case "meteor":
                AddContentField("meteor",Team.Player,point,spell.Radius,4,spell.Power,spell.DisplayName).Pulse=1.2f;
                break;
            case "purifying_light":
                foreach(var a in NearbyContentUnits(Team.Player,point,spell.Radius)) { a.Cleanse();a.Heal(spell.Power); }
                break;
            case "withering_seal":
                foreach(var e in NearbyContentUnits(Team.Enemy,point,spell.Radius)) e.ApplyHealingPenalty(.7f,spell.Duration);
                break;
            case "gale_step":
                if(ally!=null) { ally.Cleanse();OffsetUnitWithinBattlefield(ally,new Vector2(Math.Min(44,spell.SecondaryPower),0)); }
                break;
            case "gravitation_well":
                AddContentField("gravity",Team.Player,point,spell.Radius,spell.Duration,0,spell.DisplayName);
                break;
            case "soul_harvest":
                var corpses=_contentCorpses.Where(c=>c.Team==Team.Enemy && c.Position.DistanceTo(point)<spell.Radius).Take(3).ToArray();
                foreach(var corpse in corpses) _contentCorpses.Remove(corpse);
                GainMana(corpses.Length*spell.Power);
                break;
            case "consecrated_ground":
                AddContentField("holy",Team.Player,point,spell.Radius,spell.Duration,spell.Power,spell.DisplayName);
                break;
            case "verdant_renewal":
                if(ally!=null) { var field=AddContentField("heal",Team.Player,ally.Position,24,spell.Duration,spell.Power,spell.DisplayName);field.Follow=ally;field.FollowLife=ally.CombatLifetime;field.SingleTarget=true; }
                break;
            case "bramble_snare":
                foreach(var e in NearbyContentUnits(Team.Enemy,point,spell.Radius)) { e.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical);e.Root(spell.Duration); }
                break;
            case "chain_lightning":
                var victim=target;var power=spell.Power;var seen=new System.Collections.Generic.HashSet<Unit>();
                for(var i=0;i<3 && victim!=null;i++)
                {
                    seen.Add(victim);victim.TakeDamage(power,spell.DisplayName,CombatDamageType.Magical);
                    var from=victim.Position;power*=.7f;
                    victim=NearbyContentUnits(Team.Enemy,from,spell.Radius).FirstOrDefault(e=>!seen.Contains(e));
                }
                break;
            case "rallying_advance":
                foreach(var a in NearbyContentUnits(Team.Player,point,spell.Radius).Take(4)) a.ApplyTemporaryCombatBuff(1.08f,1.12f,spell.Duration);
                break;
            case "revealing_flare":
                AddContentField("reveal",Team.Player,point,spell.Radius,spell.Duration,0,spell.DisplayName);
                foreach(var e in NearbyContentUnits(Team.Enemy,point,spell.Radius)) { e.Reveal();e.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                break;
            case "mirror_ward":
                foreach(var a in NearbyContentUnits(Team.Player,point,spell.Radius).Take(3)) { a.ProjectileMirrorTime=spell.Duration;a.ProjectileMirrorCharges=1; }
                break;
            case "banish":
                if(target!=null) { target.RemoveShield();if(target.IsSummoned) target.Despawn();else target.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                break;
            case "royal_edict":
                foreach(var e in NearbyContentUnits(Team.Enemy,point,spell.Radius)) e.Silence(spell.Duration);
                break;
            default:return "No effect at this point.";
        }
        SpawnEffect(point,spell.GetTint(),5,Math.Max(16,spell.Radius),.4f,false);
        return $"{spell.DisplayName} resolved.";
    }

    private bool TickTacticalObject(Unit obj,Unit target)
    {
        var spell=GameData.GetSpell(obj.DefinitionId);
        if(spell.EffectType=="tactic_bolt_turret")
        {
            if(obj.UtilityCharges<=0) { obj.Despawn();return true; }
            if(target!=null && obj.CanAttack(target) && obj.TryBeginAttack(target))
            { obj.UtilityCharges--;QueueUnitStrike(obj,target); }
            return true;
        }
        if(spell.EffectType is "tactic_firepot_mine" or "tactic_frost_trap")
        {
            var victim=ContentSpellTarget(Team.Enemy,obj.Position,spell.Radius);
            if(victim!=null)
            {
                if(spell.EffectType=="tactic_frost_trap")
                { victim.ApplyControl(2);victim.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Magical); }
                else
                {
                    foreach(var enemy in NearbyContentUnits(Team.Enemy,obj.Position,spell.Radius)) enemy.TakeDamage(spell.Power,spell.DisplayName,CombatDamageType.Physical);
                    AddContentField("burn",Team.Player,obj.Position,spell.Radius,3,5,spell.DisplayName);
                }
                obj.Despawn();
            }
            return true;
        }
        if(spell.EffectType=="tactic_decoy_lantern")
        {
            foreach(var enemy in NearbyContentUnits(Team.Enemy,obj.Position,spell.Radius).Where(e=>!e.Definition.IsBoss)) _targetLocks[enemy]=obj;
            return true;
        }
        var interval=spell.EffectType=="tactic_supply_crate"?3f:2f;
        var nextPulse=obj.EffectReadyAt.GetValueOrDefault("utility_pulse",interval);
        if(obj.ContentAge+.001f<nextPulse || obj.UtilityCharges<=0) return true;
        // Absolute age prevents frame rounding from delaying the last finite pulse.
        obj.EffectReadyAt["utility_pulse"]=nextPulse+interval;
        switch(spell.EffectType)
        {
            case "tactic_healing_shrine":
                foreach(var ally in NearbyContentUnits(Team.Player,obj.Position,spell.Radius).Where(a=>!a.IsTacticalObject).Take(3)) ally.Heal(spell.Power);
                obj.UtilityCharges--;break;
            case "tactic_supply_crate":
                if(NearbyContentUnits(Team.Player,obj.Position,spell.Radius).Any(a=>!a.IsTacticalObject))
                { SpawnCouragePickup(obj.Position,spell.Power);obj.UtilityCharges--; }
                break;
            case "tactic_purifying_censer":
                foreach(var ally in NearbyContentUnits(Team.Player,obj.Position,spell.Radius)) ally.Cleanse();
                obj.UtilityCharges--;break;
            case "tactic_repair_cache":
                if(obj.Position.DistanceTo(PlayerBaseCorePosition)<60) RepairBusByAmount(spell.Power);
                obj.UtilityCharges--;break;
            case "tactic_grounding_rod":
                var caster=NearbyContentUnits(Team.Enemy,obj.Position,spell.Radius).FirstOrDefault(e=>_contentCasts.ContainsKey(e));
                if(caster!=null) { caster.Silence(.5f);obj.UtilityCharges--; }
                break;
        }
        return true;
    }
}
