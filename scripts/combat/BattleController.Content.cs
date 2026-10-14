using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class BattleController
{
    private readonly RandomNumberGenerator _contentRng = new();
    private sealed class ContentCast
    {
        public float Remaining;
        public int Generation;
        public long Lifetime;
        public Vector2 Origin;
        public Action Resolve;
        public bool Stationary;
        public BattleContentWarning Warning;
        public float Total;
    }
    private sealed record CorpseToken(Team Team, Vector2 Position, float Expires);
    private readonly Dictionary<Unit,ContentCast> _contentCasts = new();
    private readonly Dictionary<Unit,(Unit Owner,long Life)> _contentSummons = new();
    private readonly List<CorpseToken> _contentCorpses = new();
    private readonly List<BattleContentField> _contentFields = new();
    private int _crewSequence;
    private bool _pilgrimSpawned;
    private float _contentOutpostProgress,_contentSupplyProgress;

    private void InitializeContent(ulong seed) => _contentRng.Seed = seed ^ 0x4D454C4545434F52UL;

    private void InitializeContentUnit(Unit unit)
    {
        _pendingBossPhases.Remove(unit);
        _campaignBossPhaseTriggeredUnits.Remove(unit);
        ClearContentCast(unit);
        _contentSummons.Remove(unit);
        var behavior = unit.Definition?.ContentBehavior ?? "";
        if (behavior is "stealth" or "vanish") unit.Conceal(2f);
        if (behavior == "ambush") unit.Conceal(12f);
        if(unit.NonCombatant)GameState.Instance.DiscoverCodexEntry(unit.DefinitionId);
        if (unit.Team == Team.Player && !unit.IsTacticalObject) ApplyContentDeployEquipment(unit);
    }

    private void TickContentWorld(float delta)
    {
        TickEnemyLessons(delta);
        if(HasCampaignField)
        {
            var outpost=FieldPoint(_stageData.Battlefield.OutpostXRatio,_stageData.Battlefield.OutpostYRatio);
            var supply=FieldPoint(_stageData.Battlefield.SupplyXRatio,_stageData.Battlefield.SupplyYRatio);
            if(HasUnitNear(Team.Player,outpost,45) && !HasUnitNear(Team.Enemy,outpost,55))
            {
                _contentOutpostProgress+=delta;
                if(_contentOutpostProgress>3 && _contentObjectiveRewards.Add("outpost_base"))
                { SpawnCouragePickup(outpost,4);ApplyContentObjectiveEquipment("outpost",outpost); }
            }
            if(HasUnitNear(Team.Player,supply,45) && !HasUnitNear(Team.Enemy,supply,55))
            {
                _contentSupplyProgress+=delta;
                if(_contentSupplyProgress>3 && _contentObjectiveRewards.Add("supply_base"))
                { SpawnCouragePickup(supply,6);ApplyContentObjectiveEquipment("supply",supply); }
            }
        }
        _contentCorpses.RemoveAll(c => c.Expires < _elapsed);
        foreach (var pair in _contentCasts.ToArray())
        {
            var unit=pair.Key;var cast=pair.Value;
            if (!IsInstanceValid(unit) || unit.IsDead || unit.CombatLifetime!=cast.Lifetime ||
                unit.CastGeneration!=cast.Generation || unit.IsSilenced ||
                (cast.Stationary && unit.Position.DistanceTo(cast.Origin)>12f))
            { ClearContentCast(unit); continue; }
            cast.Remaining-=delta;
            cast.Warning.Progress=1-Mathf.Clamp(cast.Remaining/cast.Total,0,1);cast.Warning.QueueRedraw();
            if (cast.Remaining>0f) continue;
            ClearContentCast(unit);
            unit.MarkedTime=0f;
            cast.Resolve();
            ApplyContentAbilityEquipment(unit);
        }
        foreach (var field in _contentFields.ToArray())
        {
            if(field.Follow!=null)
            {
                if(!IsInstanceValid(field.Follow) || field.Follow.IsDead || field.Follow.CombatLifetime!=field.FollowLife)
                { _contentFields.Remove(field);field.QueueFree();continue; }
                field.Position=field.Follow.Position;
            }
            field.Remaining-=delta;field.Pulse-=delta;
            if (field.Remaining<=0f) { _contentFields.Remove(field);field.QueueFree();continue; }
            if (field.Pulse>0f) continue;
            field.Pulse=1f;
            ApplyContentField(field);
        }
        if (!_pilgrimSpawned && !IsEndlessMode && _stage is 17 or 67 && _elapsed>18f)
        {
            _pilgrimSpawned=true;
            var pilgrim=SpawnUnit(Team.Player,new UnitStats(GameData.GetUnit("enemy_ash_pilgrim")),CaravanDeployPosition+new Vector2(18,0));
            pilgrim.CanDropCourage=false;
        }
    }

    private void BeginContentCast(Unit unit,float delay,Action resolve,bool stationary=true)
    {
        if (_contentCasts.ContainsKey(unit)) return;
        if (unit.Team == Team.Player && !unit.IsSummoned && !unit.IsTacticalObject) _activeAbilitiesTriggered++;
        unit.MarkedTime=delay;
        var warning=new BattleContentWarning {Position=_targetLocks.TryGetValue(unit,out var victim)?victim.Position:unit.Position,
            Label=unit.Definition.ContentBehavior switch {"charge"=>"CHARGE","root" or "tether"=>"ROOT","sleep"=>"SLEEP","field_heal" or "healing_channel"=>"HEAL", "channel_summon" or "corpse_channel"=>"SUMMON", "hazard_object" or "marked_splash" or "blight_fields"=>"BLAST",_=>"CAST"},
            Tint=new Color(unit.Team==Team.Enemy?"e9ac78":"91c7cf"),ZIndex=3};AddChild(warning);
        _contentCasts[unit]=new ContentCast {
            Remaining=delay,Generation=unit.CastGeneration,Lifetime=unit.CombatLifetime,
            Origin=unit.Position,Resolve=resolve,Stationary=stationary,Warning=warning,Total=Mathf.Max(.01f,delay)
        };
        SpawnEffect(unit.Position,unit.Team==Team.Enemy?new Color("edac78"):unit.Tint,4,18,delay,false);
    }

    private bool TryContentAction(Unit unit,Unit target,float delta)
    {
        if (unit.IsControlled || _contentCasts.ContainsKey(unit)) return true;
        if (unit.IsTacticalObject) return TickTacticalObject(unit,target);
        var behavior=unit.Definition?.ContentBehavior ?? "";
        if (unit.NonCombatant)
        {
            if (unit.Position.X>Mathf.Lerp(PlayerSpawnX,EnemySpawnX,.78f))
            { SpawnCouragePickup(unit.Position,8);unit.Despawn(); }
            else unit.Advance(delta,BattlefieldLeft,BattlefieldRight,BattlefieldTop+SpawnVerticalPadding,BattlefieldBottom-SpawnVerticalPadding);
            return true;
        }
        if (unit.IsConcealed && target!=null && unit.CanAttack(target)) unit.Reveal();
        if (unit.IsSilenced) return false;
        if (behavior=="swarm" && target!=null && target.Position.DistanceTo(unit.Position)<45 &&
            NearbyContentUnits(target.Team,target.Position,50).Count()==1 && unit.TryEffect("slip",10))
            OffsetUnitWithinBattlefield(unit,new Vector2(unit.Team==Team.Enemy?-36:36,0));
        if (behavior=="guard" && unit.CommittedAttacks>0 && unit.CommittedAttacks%2==0 && unit.TryEffect("guard",2f))
            unit.GuardPhysical(.18f,1f);
        if (behavior=="vulnerability_cycle")
        {
            if (unit.ContentAge%5f>3f) unit.ApplyArmorBreak(15f,1f);
            else unit.GuardPhysical(.12f,.2f);
        }
        if (behavior=="terrain_regen" && unit.TryEffect("regenerate",2f)) unit.Heal(3f);
        if (behavior=="brace" && !unit.MovedRecently && unit.TryEffect("brace_scan",.25f))
        {
            var charger=_units.FirstOrDefault(e=>!e.IsDead && e.Team!=unit.Team && e.ContentState=="charging" && e.Position.DistanceTo(unit.Position)<82f);
            if (charger!=null && unit.TryEffect("brace_stop",8f))
            { if(charger.ApplyControl(.65f)) ApplyContentInterruptEquipment(unit);charger.ContentState="";ContentDamage(unit,charger,unit.AttackDamage*.8f,false,false); }
        }
        if (behavior is "field_heal" or "healing_channel" or "attachment")
        {
            var patient=_units.Where(a=>a!=unit && !a.IsDead && a.Team==unit.Team && !a.IsTacticalObject && a.HealthRatio<.9f)
                .OrderBy(a=>a.HealthRatio).FirstOrDefault();
            if (patient!=null)
            {
                if (patient.Position.DistanceTo(unit.Position)>54f)
                    unit.MoveToward(patient.Position,delta,BattlefieldLeft,BattlefieldRight,BattlefieldTop+SpawnVerticalPadding,BattlefieldBottom-SpawnVerticalPadding);
                else if (unit.ContentCooldown<=0)
                {
                    unit.ContentCooldown=2f;
                    var life=patient.CombatLifetime;
                    BeginContentCast(unit,.45f,()=>{
                        if (IsInstanceValid(patient) && patient.CombatLifetime==life && !patient.IsDead)
                        { patient.Heal(5f*UnitBalance.DamageScale(unit.Level));ApplyContentHealEquipment(unit,patient); }
                    });
                }
                return true;
            }
        }
        if (unit.ContentCooldown>0f) return false;
        if (unit.UsesProjectile && target != null && unit.InMeleeReach(target) &&
            behavior is "volley" or "precision_shot" or "charged_shot") return false;
        switch (behavior)
        {
            case "repair_construct":
                if (!_contentSummons.Any(p=>p.Value.Owner==unit && p.Value.Life==unit.CombatLifetime && !p.Key.IsDead))
                {
                    unit.ContentCooldown=20f;
                    BeginContentCast(unit,.7f,()=>SpawnContentMinion(unit,"emplacement",24,4,10));
                }
                break;
            case "blessing":
                if (_units.Any(a=>a.Team==unit.Team && !a.IsDead && a.HealthRatio<.85f && a.Position.DistanceTo(unit.Position)<50))
                {
                    unit.ContentCooldown=14;
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,50).Take(3)) { ally.Heal(7*UnitBalance.DamageScale(unit.Level));ApplyContentHealEquipment(unit,ally); }
                }
                break;
            case "radiant_guard":
                var injured=NearbyContentUnits(unit.Team,unit.Position,50).Where(a=>a!=unit && a.HealthRatio<.65f).FirstOrDefault();
                if(injured!=null)
                {
                    unit.ContentCooldown=12;injured.GiveShield(8,4);
                    if(target!=null && unit.CanAttack(target)) ContentDamage(unit,target,unit.AttackDamage*.65f,false,false,CombatDamageType.Magical);
                }
                break;
            case "vanish":
                if(target!=null && target.Position.DistanceTo(unit.Position)<100)
                { unit.ContentCooldown=12;unit.Conceal(1.5f);unit.ContentState="ambush_strike"; }
                break;
            case "frenzy":
                if(unit.HealthRatio<.5f) { unit.ContentCooldown=12;unit.ApplyAttackRecovery(.9f,3f); }
                break;
            case "rally":
                unit.ContentCooldown=14;
                foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,55).Take(3)) ally.ApplyTemporaryCombatBuff(1.08f,1f,3f);
                break;
            case "charged_shot":
                if(target!=null && unit.CanAttack(target) && !unit.IsAttackCommitted)
                {
                    unit.ContentCooldown=unit.AttackCooldown;
                    var life=target.CombatLifetime;
                    BeginContentCast(unit,.7f,()=> { if(IsInstanceValid(target) && !target.IsDead && target.CombatLifetime==life && unit.TryBeginAttack(target)) QueueUnitStrike(unit,target); });
                    return true;
                }
                break;
            case "volley": case "precision_shot": case "beam": case "chain_attack": case "volatile_flask":
                if(target!=null && unit.CanAttack(target))
                {
                    unit.ContentCooldown=behavior=="beam"?14:12;
                    var point=target.Position;var life=target.CombatLifetime;
                    BeginContentCast(unit,.35f,()=>{
                        if(behavior=="beam")
                        {
                            foreach(var e in _units.Where(e=>!e.IsDead && e.Team!=unit.Team && Math.Abs(e.Position.Y-unit.Position.Y)<22 && e.Position.DistanceTo(unit.Position)<150).Take(3).ToArray())
                                ContentDamage(unit,e,unit.AttackDamage*.8f,true,false,CombatDamageType.Magical);
                        }
                        else if(behavior is "chain_attack" or "volley")
                        {
                            foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,point,65).Take(behavior=="volley"?2:3))
                                ContentDamage(unit,e,unit.AttackDamage*.65f,true,false);
                        }
                        else if(IsInstanceValid(target) && !target.IsDead && target.CombatLifetime==life)
                            ContentDamage(unit,target,unit.AttackDamage*1.25f,true,false);
                    });
                    return true;
                }
                break;
            case "intercept":
                target=_units.Where(e=>!e.IsDead && e.Team!=unit.Team && e.Position.DistanceTo(PlayerBaseCorePosition)<175).OrderBy(e=>e.Position.X).FirstOrDefault();
                if(target!=null && unit.ContentState!="intercept_used")
                {
                    unit.ContentState="intercept_used";unit.ContentCooldown=999;
                    OffsetUnitWithinBattlefield(unit,new Vector2(Mathf.Clamp(target.Position.X-unit.Position.X-24,0,70),0));
                    ContentDamage(unit,target,unit.AttackDamage*.8f,false,false);
                }
                break;
            case "charge":
                if(target!=null && target.Position.DistanceTo(unit.Position)<120)
                {
                    unit.ContentCooldown=11;unit.ContentState="charging";var point=target.Position;
                    BeginContentCast(unit,.8f,()=> {
                        if(unit.IsControlled) return;
                        OffsetUnitWithinBattlefield(unit,new Vector2(Mathf.Clamp(point.X-unit.Position.X, -65,65),0));
                        foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,unit.Position,42).Take(3))
                            ContentDamage(unit,e,unit.AttackDamage*.8f,false,false);
                        unit.ContentState="";
                    });
                    return true;
                }
                break;
            case "pull": case "leap": case "root": case "tether": case "sleep": case "shield_strip": case "wound":
                if(target!=null && target.Position.DistanceTo(unit.Position)<145)
                {
                    unit.ContentCooldown=9;var victim=target;var life=target.CombatLifetime;
                    BeginContentCast(unit,.8f,()=>{
                        if(!IsInstanceValid(victim) || victim.IsDead || victim.CombatLifetime!=life) return;
                        if(behavior=="pull") OffsetUnitWithinBattlefield(victim,(unit.Position-victim.Position).Normalized()*30);
                        if(behavior=="leap") { OffsetUnitWithinBattlefield(unit,(victim.Position-unit.Position).LimitLength(75)+new Vector2(12,0));ContentDamage(unit,victim,unit.AttackDamage*.7f,false,false); }
                        if(behavior is "root" or "tether") victim.Root(1.4f);
                        if(behavior=="sleep") victim.Sleep(1.5f);
                        if(behavior=="shield_strip") { victim.RemoveShield();ContentDamage(unit,victim,6,true,false,CombatDamageType.Magical); }
                        if(behavior=="wound") { victim.ApplyHealingPenalty(.7f,3);ContentDamage(unit,victim,unit.AttackDamage*.6f,false,false); }
                    });
                    return true;
                }
                break;
            case "blight_fields": case "null_field": case "hazard_object": case "friendly_fire_fields": case "marked_splash":
                if(target!=null)
                {
                    unit.ContentCooldown=11;var point=target.Position;
                    BeginContentCast(unit,1f,()=>{
                        var kind=behavior=="blight_fields"?"miasma":behavior=="null_field"?"null":behavior=="friendly_fire_fields"?"rot":"burn";
                        if(behavior is "hazard_object" or "marked_splash")
                            foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,point,36).Take(4)) ContentDamage(unit,e,unit.AttackDamage*.75f,true,false);
                        AddContentField(kind,unit.Team,point,behavior=="null_field"?60:36,4,2,unit.UnitName);
                    });
                    return true;
                }
                break;
            case "corpse_channel": case "corpse_raise_ally": case "channel_summon": case "corpse_buff":
                if(!_contentSummons.Any(p=>p.Value.Owner==unit && p.Value.Life==unit.CombatLifetime && !p.Key.IsDead))
                {
                    var token=_contentCorpses.FirstOrDefault(c=>c.Team==(behavior=="corpse_raise_ally"?Team.Enemy:unit.Team) && c.Position.DistanceTo(unit.Position)<90);
                    if(token!=null || behavior=="channel_summon")
                    {
                        unit.ContentCooldown=12;BeginContentCast(unit,1.2f,()=>{
                            if(token!=null && !_contentCorpses.Remove(token)) return;
                            if(behavior=="corpse_buff") foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,65).Take(3)) ally.ApplyTemporaryCombatBuff(1.08f,1f,3);
                            else SpawnContentMinion(unit,"raised",unit.Team==Team.Player?28:64,unit.Team==Team.Player?4:9,12);
                        });
                        return true;
                    }
                }
                break;
            case "attack_recovery":
                unit.ContentCooldown=12;BeginContentCast(unit,.8f,()=>{
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,60).Take(3)) ally.ApplyAttackRecovery(.9f,3);
                });return true;
            case "ambush":
                if(target!=null && target.Position.DistanceTo(unit.Position)<70)
                {
                    unit.ContentCooldown=9;var victim=target;
                    BeginContentCast(unit,.8f,()=>{unit.Reveal();if(IsInstanceValid(victim) && !victim.IsDead) ContentDamage(unit,victim,unit.AttackDamage*.8f,false,false);});
                    return true;
                }
                return true;
            case "phase":
                if(target!=null && target.Position.DistanceTo(unit.Position)<60)
                { unit.ContentCooldown=10;unit.Conceal(.6f);OffsetUnitWithinBattlefield(unit,new Vector2(unit.Team==Team.Enemy?-45:45,0)); }
                break;
            case "decoy":
                unit.ContentCooldown=14;SpawnContentMinion(unit,"decoy",1,0,5);break;
            case "revival_link":
                if(!unit.TryEffect("soul_link_once",999)) break;
                unit.ContentCooldown=15;
                foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,70).Where(a=>a!=unit && !a.IsSummoned && a.Definition.IsBoss==false).Take(1))
                    ally.ContentState="soul_link";
                break;
            case "ammo_movement":
                if(unit.CommittedAttacks>=2 && unit.ContentState!="retreated")
                { unit.ContentState="retreated";unit.ContentCooldown=12;OffsetUnitWithinBattlefield(unit,new Vector2(unit.Team==Team.Enemy?25:-25,0));unit.BecomeMelee(); }
                break;
        }
        return _contentCasts.ContainsKey(unit);
    }

    private IEnumerable<Unit> NearbyContentUnits(Team team,Vector2 point,float radius)
        => _units.Where(u=>!u.IsDead && !u.NonCombatant && u.Team==team && u.Position.DistanceTo(point)<=radius).OrderBy(u=>u.Position.DistanceSquaredTo(point)).ToArray();

    private Unit SpawnContentMinion(Unit owner,string kind,float hp,float damage,float lifetime)
    {
        if(_units.Count(u=>!u.IsDead && u.Team==owner.Team)>=32) return null;
        var def=new UnitDefinition {
            Id=kind=="raised"?GameData.EnemyWalkerId:"content_"+kind,
            DisplayName=kind=="raised"?"Raised Risen":kind=="decoy"?"Illusion":"Bolt Emplacement",
            Side=owner.Team==Team.Player?"Player":"Enemy",
            MaxHealth=hp,AttackDamage=damage,Speed=kind=="emplacement"?0:30,AttackRange=kind=="emplacement"?92:30,
            AttackCooldown=kind=="emplacement"?1.4f:1.2f,UsesProjectile=kind=="emplacement",DamagesStructures=false,
            VisualClass=kind=="raised"?"walker":kind=="emplacement"?"ballista":"fighter",
            ColorHex=owner.Tint.ToHtml(false),PlaceholderVisual=true,Armor=4,BaseDamage=0
        };
        var minion=SpawnUnit(owner.Team,new UnitStats(def,UnitBalance.HealthScale(owner.Level),UnitBalance.DamageScale(owner.Level),level:owner.Level),owner.Position+new Vector2(owner.Team==Team.Player?18:-18,0));
        minion.IsSummoned=true;minion.CanDropCourage=false;minion.LifetimeRemaining=lifetime;
        _contentSummons[minion]=(owner,owner.CombatLifetime);
        return minion;
    }

    private float ContentDamage(Unit source,Unit target,float damage,bool ranged,bool critical=true,CombatDamageType? type=null,bool triggerEffects=true)
    {
        if(!IsInstanceValid(target) || target.IsDead || damage<=0) return 0;
        var validSource=IsInstanceValid(source) && !source.IsDead;
        if(ranged && target.ProjectileDodgeCharges>0) { target.ProjectileDodgeCharges--;return 0; }
        if(ranged && target.Definition.ContentBehavior=="projectile_dodge" && target.TryEffect("dodge",12))
        { OffsetUnitWithinBattlefield(target,new Vector2(-8,4));return 0; }
        var dtype=type ?? (validSource?source.AttackDamageType:CombatDamageType.Physical);
        var criticalHit=validSource && critical && _contentRng.Randf()*100<source.CriticalChance;
        if(criticalHit)damage*=1+source.CriticalDamageBonus/100f;
        var penetration=0f;
        if(validSource && dtype==CombatDamageType.Physical)
        {
            var behavior=source.Definition.ContentBehavior;
            if(behavior=="armor_pierce" && source.CommittedAttacks%2==0) penetration=.5f;
            if(behavior=="piercing_thrust" && source.CommittedAttacks%3==0) penetration=.5f;
            if(behavior=="siege_shot" && source.CommittedAttacks%4==0) penetration=.4f;
            if(target.Definition.ContentBehavior=="armor_facing" && (target.Team==Team.Enemy?source.Position.X>target.Position.X+2:source.Position.X<target.Position.X-2)) penetration=1;
            if(target.Definition.ContentBehavior=="armor_burn" && target.IsBurning) penetration=Math.Max(penetration,.4f);
        }
        if(target.Definition.ContentBehavior=="breakable_armor" && target.DirectHitsTaken<3) damage*=.6f;
        if(dtype==CombatDamageType.Physical && !ranged && target.Definition.ContentBehavior=="parry" && target.TryEffect("parry",8))
        {
            damage*=.45f;
            if(validSource) source.TakeDamage(target.AttackDamage*.35f,target.UnitName,CombatDamageType.Physical);
        }
        if(validSource && source.Definition.ContentBehavior=="shield_break" && (target.ShieldHealth>0 || target.IsTacticalObject)) damage*=1.3f;
        if(validSource && source.ContentState=="ambush_strike") { damage*=1.15f;source.ContentState=""; }
        damage=ModifyContentEquipmentDamage(source,target,damage,ranged);
        if(ranged && dtype==CombatDamageType.Physical && target.ProjectileMirrorTime>0 && target.ProjectileMirrorCharges>0)
        {
            target.ProjectileMirrorCharges--;
            if(validSource) source.TakeDamage(damage*.5f,target.UnitName,dtype);
            return 0;
        }
        target.DirectHitsTaken++;
        var actual=target.TakeDamage(damage,validSource?source.UnitName:null,dtype,penetration);
        if(validSource)
        {
            source.Reveal();
            TrackDamageDealt(source,actual);
            if(triggerEffects) AfterContentHit(source,target,actual,ranged);
        }
        SpawnDamageFeedback(target.Position,actual,validSource?source.Tint:Colors.White);
        if(criticalHit && actual>0)SpawnCombatNumber(target.Position+new Vector2(0,-28),"CRIT",new Color("f6d47d"),.6f);
        return actual;
    }

    private void AfterContentHit(Unit source,Unit target,float damage,bool ranged)
    {
        if(damage<=0) return;
        var behavior=source.Definition.ContentBehavior;
        if(behavior=="armor_break" && source.CommittedAttacks%3==0) target.ApplyArmorBreak(12,2);
        if(behavior=="stagger" && source.CommittedAttacks%4==0 && target.ApplyControl(.45f)) ApplyContentInterruptEquipment(source);
        if(behavior=="hook" && source.CommittedAttacks%3==0 && !target.Definition.IsBoss && target.Definition.Speed>0)
        { OffsetUnitWithinBattlefield(target,(source.Position-target.Position).Normalized()*22);target.ApplyControl(.18f); }
        if(behavior=="burn_fields") AddContentField("burn",source.Team,target.Position,26,3,2,source.UnitName);
        if(behavior=="wound") target.ApplyHealingPenalty(.7f,3);
        if(source.ContentState=="chill_next") { source.ContentState="";target.ApplyTemporarySpeedModifier(.85f,1.5f); }
        if(behavior is "cleave" or "sweep" or "great_cut" or "pierce")
        {
            if(behavior=="great_cut" || behavior=="pierce" || source.CommittedAttacks%3==0)
            {
                var dir=source.Team==Team.Player?1:-1;
                foreach(var other in NearbyContentUnits(target.Team,target.Position,behavior=="sweep"?42:32)
                    .Where(e=>e!=target && (e.Position.X-source.Position.X)*dir>=-3).Take(2))
                    ContentDamage(source,other,damage*.35f,ranged,false,source.AttackDamageType,false);
            }
        }
        if(target.Definition.ContentBehavior=="conditional_reflect" && !ranged && target.ContentAge%5<2)
            source.TakeDamage(Math.Min(3,damage*.15f),target.UnitName,CombatDamageType.Physical);
        ApplyContentHitEquipment(source,target,damage,ranged);
    }

    private bool TryContentReturn(Unit dead)
    {
        if(dead.IsTacticalObject || dead.NonCombatant || dead.IsSummoned) return false;
        if(dead.Definition.ContentBehavior=="return" || (dead.ContentState=="soul_link" &&
            _units.Any(u=>!u.IsDead && u.Team==dead.Team && u.Definition.ContentBehavior=="revival_link" && u.Position.DistanceTo(dead.Position)<90)))
        {
            dead.ContentState="";
            if(dead.TryReturn()) { SpawnEffect(dead.Position,dead.Tint,5,25,.4f,false);return true; }
        }
        return TryContentEquipmentReturn(dead);
    }

    private void RegisterContentDeath(Unit dead)
    {
        ClearContentCast(dead);
        if(dead.Definition.ContentBehavior=="friendly_fire_fields")
            AddContentField("rot",dead.Team,dead.Position,34,3,2,dead.UnitName);
        if(!dead.IsSummoned && !dead.IsTacticalObject && !dead.NonCombatant)
        {
            _contentCorpses.Add(new CorpseToken(dead.Team,dead.Position,_elapsed+10));
            if(dead.Team==Team.Enemy)
                foreach(var quarter in NearbyContentUnits(Team.Player,dead.Position,70).Where(a=>a.Definition.ContentBehavior=="supply"))
                {
                    var amount=Math.Min(2,quarter.Definition.Cost*.2f-quarter.GeneratedCourage);
                    if(amount>0 && quarter.TryEffect("supply_pack",10))
                    { quarter.GeneratedCourage+=amount;SpawnCouragePickup(dead.Position,amount); }
                }
            ApplyContentDeathEquipment(dead);
        }
    }

    private BattleContentField AddContentField(string kind,Team owner,Vector2 point,float radius,float duration,float power,string label)
    {
        var existing=_contentFields.FirstOrDefault(f=>f.Kind==kind && f.TeamOwner==owner && f.Position.DistanceTo(point)<radius*.4f);
        if(existing!=null) { existing.Remaining=Math.Max(existing.Remaining,duration);existing.Power=Math.Max(existing.Power,power);return existing; }
        if(_contentFields.Count>=12) { var first=_contentFields[0];_contentFields.RemoveAt(0);first.QueueFree(); }
        var field=new BattleContentField { Kind=kind,TeamOwner=owner,Position=point,Radius=radius,Remaining=duration,Power=power,Label=label,ZIndex=2 };
        AddChild(field);_contentFields.Add(field);return field;
    }

    private void ApplyContentField(BattleContentField field)
    {
        if(field.Kind=="meteor")
        {
            foreach(var enemy in NearbyContentUnits(Team.Enemy,field.Position,field.Radius)) enemy.TakeDamage(field.Power,field.Label,CombatDamageType.Magical);
            field.Kind="burn";field.Power=2;field.Remaining=3;field.QueueRedraw();
        }
        if(field.Kind=="holy") _contentCorpses.RemoveAll(c=>c.Position.DistanceTo(field.Position)<field.Radius);
        foreach(var unit in _units.Where(u=>!u.IsDead && u.Position.DistanceTo(field.Position)<field.Radius).ToArray())
        {
            var friendly=unit.Team==field.TeamOwner;
            if(field.Kind=="heal" && friendly && (!field.SingleTarget || unit==field.Follow)) unit.Heal(field.Power);
            if(field.Kind=="smoke" && friendly) unit.Conceal(1.2f);
            if(field.Kind=="reveal" && !friendly) unit.Reveal();
            if(field.Kind=="null" && !friendly) { unit.RemoveShield();unit.Silence(1.2f); }
            if(field.Kind=="gravity" && !friendly)
            { OffsetUnitWithinBattlefield(unit,(field.Position-unit.Position).LimitLength(10));unit.ApplyTemporarySpeedModifier(.8f,1.1f); }
            if(field.Kind=="miasma" && !friendly)
            { unit.ApplyHealingPenalty(.7f,1.4f);unit.ApplyBurn(field.Power,1.4f,field.Label); }
            if(field.Kind is "burn" or "holy" or "rot" && (!friendly || field.Kind=="rot") && unit.TryEffect("field_"+field.Kind,1f))
                unit.TakeDamage(field.Power,field.Label,CombatDamageType.Magical);
        }
    }
}

public partial class BattleContentField : Node2D
{
    public string Kind="",Label="";
    public Team TeamOwner;
    public float Radius=30,Remaining=3,Power=2,Pulse;
    public Unit Follow;
    public long FollowLife;
    public bool SingleTarget;
    public override void _Draw()
    {
        var tint=Kind switch { "burn"=>new Color("e09a50"),"miasma" or "rot"=>new Color("94a45c"),"null"=>new Color("826694"),"heal" or "holy"=>new Color("bbd79c"),"smoke"=>new Color("99928c"),_=>new Color("a9b7cf") };
        DrawCircle(Vector2.Zero,Radius,new Color(tint,.14f));
        DrawLine(new Vector2(-Radius,1),new Vector2(Radius,1),new Color(tint,.65f),2,true);
    }
}
