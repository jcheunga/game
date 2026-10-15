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
        var locked=_targetLocks.TryGetValue(unit,out var victim) && IsInstanceValid(victim);
        var warning=new BattleContentWarning {Position=locked?victim.Position:unit.Position,Follow=locked?victim:unit,
            Label=unit.Definition.ContentBehavior switch {"charge"=>"Charge","root" or "tether"=>"Root","sleep"=>"Sleep","field_heal" or "healing_channel"=>"Heal", "channel_summon" or "corpse_channel"=>"Summon", "hazard_object" or "marked_splash" or "blight_fields"=>"Blast",_=>"Cast"},
            Tint=CastTint(unit),Radius=Mathf.Clamp((locked?victim:unit).Radius*1.4f,16f,40f),School=CastSchool(unit)};AddChild(warning);
        unit.BeginCast(delay+.15f);
        BattleCastGlow.Spawn(this,unit,CastTint(unit),delay);
        _contentCasts[unit]=new ContentCast {
            Remaining=delay,Generation=unit.CastGeneration,Lifetime=unit.CombatLifetime,
            Origin=unit.Position,Resolve=resolve,Stationary=stationary,Warning=warning,Total=Mathf.Max(.01f,delay)
        };
    }

    internal static string CastSchool(Unit unit) => unit.Definition?.ContentBehavior switch
    {
        "field_heal" or "healing_channel" or "blessing" => "holy",
        "channel_summon" or "corpse_channel" or "corpse_raise_ally" or "revival_link" or "corpse_buff" or "wound" => "shadow",
        "sleep" or "null_field" or "shield_strip" => "arcane",
        "root" or "tether" => "nature",
        "hazard_object" or "marked_splash" or "friendly_fire_fields" => "fire",
        "blight_fields" => "nature",
        "beam" or "chain_attack" => "storm",
        _ => "war"
    };

    /// <summary>The colour of a unit's magic: heals warm gold, summons and the dead sickly green, hexes violet,
    /// blasts and charges ember; otherwise its side's colour.</summary>
    internal static Color CastTint(Unit unit) => unit.Definition?.ContentBehavior switch
    {
        "field_heal" or "healing_channel" or "blessing" => new Color("ffd77a"),
        "channel_summon" or "corpse_channel" or "corpse_raise_ally" or "revival_link" or "corpse_buff" => new Color("8cf08a"),
        "sleep" or "null_field" or "root" or "tether" or "shield_strip" => new Color("b58cff"),
        "hazard_object" or "marked_splash" or "blight_fields" or "friendly_fire_fields" => new Color("ff9a52"),
        "charge" or "leap" or "intercept" => new Color("ffc46b"),
        "beam" or "chain_attack" or "precision_shot" => new Color("8fd8ff"),
        _ => unit.Team == Team.Enemy ? new Color("e9ac78") : new Color("91c7cf")
    };

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
        {
            SpellVfx.Burst(this,unit.Position,"p_dust",5,.5f,SpellVfx.Fade(new Color(.6f,.54f,.44f,.6f)),4f,8f,10f,30f,70f,Vector2.Up,additive:false,layer:SpellVfx.TroopLayer);
            OffsetUnitWithinBattlefield(unit,new Vector2(unit.Team==Team.Enemy?-36:36,0));
        }
        if (behavior=="guard" && unit.CommittedAttacks>0 && unit.CommittedAttacks%2==0 && unit.TryEffect("guard",2f))
        { unit.GuardPhysical(.18f,1f);SpellVfx.Flash(this,unit.WeaponContactPosition,new Color("cfe0f0"),12f,.25f); }
        if (behavior=="vulnerability_cycle")
        {
            if (unit.ContentAge%5f>3f)
            {
                unit.ApplyArmorBreak(15f,1f);
                // the furnace vents (or the eye opens): the moment to strike shows as a burst of heat and light
                if (unit.TryEffect("vent",4.5f))
                {
                    var hot=unit.DefinitionId=="enemy_reliquary_eye"?new Color("ffe08a"):new Color("ff8a3a");
                    SpellVfx.Flash(this,unit.BodyContactPosition,hot,unit.Radius*1.2f,.45f);
                    SpellVfx.Burst(this,unit.BodyContactPosition,"p_ember",14,.9f,SpellVfx.Fade(hot.Lightened(.3f),hot),2.5f,5f,30f,90f,70f,Vector2.Up,gravity:-40f);
                }
            }
            else unit.GuardPhysical(.12f,.2f);
        }
        if (behavior=="terrain_regen" && unit.TryEffect("regenerate",2f) && unit.Heal(3f)>.05f)
            SpellVfx.Burst(this,unit.Position,"p_droplet",6,.8f,SpellVfx.Fade(new Color(.6f,.9f,.85f,.8f)),2f,4f,10f,26f,40f,Vector2.Up,
                gravity:-30f,groundRadius:unit.Radius*.8f);
        if (behavior=="brace" && !unit.MovedRecently && unit.TryEffect("brace_scan",.25f))
        {
            var charger=_units.FirstOrDefault(e=>!e.IsDead && e.Team!=unit.Team && e.ContentState=="charging" && e.Position.DistanceTo(unit.Position)<82f);
            if (charger!=null && unit.TryEffect("brace_stop",8f))
            {
                if(charger.ApplyControl(.65f)) ApplyContentInterruptEquipment(unit);
                charger.ContentState="";ContentDamage(unit,charger,unit.AttackDamage*.8f,false,false);
                // the charge breaks on the set pikes
                var at=charger.BodyContactPosition;
                SpellVfx.Flash(this,at,Colors.White,18f,.22f);
                ContentSparks(at,new Color("ffd890"),12,new Vector2(Mathf.Sign(charger.Position.X-unit.Position.X),-.5f),180f);
                SpawnCombatNumber(unit.Position+new Vector2(0,-26),"BRACED",new Color("f3e2b0"),.55f);
                AudioDirector.Instance?.PlayShieldBlock(at);
                charger.HoldPose(.1f);unit.HoldPose(.1f);
            }
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
                        { var healed=patient.Heal(5f*UnitBalance.DamageScale(unit.Level));ContentHealFx(unit,patient,healed);ApplyContentHealEquipment(unit,patient); }
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
                    unit.BeginCast(.6f);SpellVfx.Sigil(this,unit.Position,"holy",new Color("ffd77a"),40f,1f,.4f);
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,50).Take(3)) { var healed=ally.Heal(7*UnitBalance.DamageScale(unit.Level));ContentHealFx(unit,ally,healed);ApplyContentHealEquipment(unit,ally); }
                }
                break;
            case "radiant_guard":
                var injured=NearbyContentUnits(unit.Team,unit.Position,50).Where(a=>a!=unit && a.HealthRatio<.65f).FirstOrDefault();
                if(injured!=null)
                {
                    unit.ContentCooldown=12;injured.GiveShield(8,4);
                    SpellVfx.Flash(this,injured.BodyContactPosition,new Color("ffe39a"),20f,.45f);SpellVfx.Sigil(this,injured.Position,"holy",new Color("ffe39a"),22f,.8f,.5f);
                    if(target!=null && unit.CanAttack(target)) ContentDamage(unit,target,unit.AttackDamage*.65f,false,false,CombatDamageType.Magical);
                }
                break;
            case "vanish":
                if(target!=null && target.Position.DistanceTo(unit.Position)<100)
                { unit.ContentCooldown=12;unit.Conceal(1.5f);unit.ContentState="ambush_strike";
                  SpellVfx.Burst(this,unit.Position,"p_smoke",8,.7f,SpellVfx.Fade(new Color(.3f,.3f,.34f,.6f)),6f,12f,8f,26f,additive:false,layer:SpellVfx.TroopLayer); }
                break;
            case "frenzy":
                if(unit.HealthRatio<.5f) { unit.ContentCooldown=12;unit.ApplyAttackRecovery(.9f,3f);unit.BeginCast(.4f);
                    SpellVfx.Flash(this,unit.BodyContactPosition,new Color("ff6a4a"),20f,.4f);SpellVfx.Sigil(this,unit.Position,"war",new Color("ff6a4a"),26f,.8f,.8f); }
                break;
            case "rally":
                unit.ContentCooldown=14;unit.BeginCast(.6f);
                SpellVfx.Sigil(this,unit.Position,"war",new Color("ffd27a"),55f,1f,.5f);SpellVfx.Shockwave(this,unit.Position,new Color("ffd27a"),55f);
                foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,55).Take(3)) { ally.ApplyTemporaryCombatBuff(1.08f,1f,3f);SpellVfx.Flash(this,ally.BodyContactPosition,new Color("ffd27a"),12f,.35f); }
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
                            // a beam of light along the line: it strikes each foe it passes through
                            var hits=_units.Where(e=>!e.IsDead && e.Team!=unit.Team && Math.Abs(e.Position.Y-unit.Position.Y)<22 && e.Position.DistanceTo(unit.Position)<150).Take(3).ToArray();
                            var reach=hits.Length>0?hits.Max(e=>Math.Abs(e.Position.X-unit.Position.X))+10:120;
                            var dir=unit.Team==Team.Player?1:-1;
                            ContentBolt(unit,unit.WeaponContactPosition+new Vector2(dir*reach,0),new Color("8fd8ff"),5f);
                            foreach(var e in hits)
                                ContentDamage(unit,e,unit.AttackDamage*.8f,true,false,CombatDamageType.Magical);
                        }
                        else if(behavior=="chain_attack")
                        {
                            // lightning that jumps from foe to foe
                            var from=unit.WeaponContactPosition;
                            foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,point,65).Take(3))
                            {
                                SpellVfx.Bolt(this,from,e.BodyContactPosition,new Color("9fd8ff"),.3f,2.6f);
                                SpellVfx.Flash(this,e.BodyContactPosition,new Color("9fd8ff"),14f,.25f);
                                from=e.BodyContactPosition;
                                ContentDamage(unit,e,unit.AttackDamage*.65f,true,false);
                            }
                        }
                        else if(behavior=="volley")
                        {
                            // two shots loosed in quick succession, each landing on its own foe
                            var k=0;
                            foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,point,65).Take(2).ToArray())
                            {
                                var victim=e;
                                FireContentShot(unit,victim,()=>ContentDamage(unit,victim,unit.AttackDamage*.65f,true,false),delay:.09f*k++);
                            }
                        }
                        else if(IsInstanceValid(target) && !target.IsDead && target.CombatLifetime==life)
                        {
                            var victim=target;
                            if(behavior=="volatile_flask")
                                FireContentShot(unit,victim,()=>ContentDamage(unit,victim,unit.AttackDamage*1.25f,true,false),.9f,ProjectileStyles.Flask);
                            else
                                FireContentShot(unit,victim,()=>ContentDamage(unit,victim,unit.AttackDamage*1.25f,true,false),1.6f);
                        }
                    });
                    return true;
                }
                break;
            case "intercept":
                target=_units.Where(e=>!e.IsDead && e.Team!=unit.Team && e.Position.DistanceTo(PlayerBaseCorePosition)<175).OrderBy(e=>e.Position.X).FirstOrDefault();
                if(target!=null && unit.ContentState!="intercept_used")
                {
                    unit.ContentState="intercept_used";unit.ContentCooldown=999;
                    var victimI=target;
                    ContentChargeRun(unit,Mathf.Clamp(target.Position.X-unit.Position.X-24,0,70),()=>{ if(IsInstanceValid(victimI) && !victimI.IsDead) ContentDamage(unit,victimI,unit.AttackDamage*.8f,false,false); });
                }
                break;
            case "charge":
                if(target!=null && target.Position.DistanceTo(unit.Position)<120)
                {
                    unit.ContentCooldown=11;unit.ContentState="charging";var point=target.Position;
                    BeginContentCast(unit,.8f,()=> {
                        if(unit.IsControlled) return;
                        ContentChargeRun(unit,Mathf.Clamp(point.X-unit.Position.X, -65,65),()=>{
                            SpellVfx.Shockwave(this,unit.Position,new Color("ffc46b"),34f,.35f);
                            foreach(var e in NearbyContentUnits(unit.Team==Team.Player?Team.Enemy:Team.Player,unit.Position,42).Take(3))
                                ContentDamage(unit,e,unit.AttackDamage*.8f,false,false);
                            unit.ContentState="";
                        });
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
                        var tint=CastTint(unit);
                        if(behavior=="pull") { SpellVfx.Chains(this,victim.Position,unit.WeaponContactPosition,new Color("cfc6b4"),1,2f,.45f);OffsetUnitWithinBattlefield(victim,(unit.Position-victim.Position).Normalized()*30); }
                        if(behavior=="leap")
                        {
                            var to=(victim.Position-unit.Position).LimitLength(75)+new Vector2(12,0);
                            ContentChargeRun(unit,to.X,()=>{ if(IsInstanceValid(victim) && !victim.IsDead) ContentDamage(unit,victim,unit.AttackDamage*.7f,false,false); });
                        }
                        if(behavior is "root" or "tether") { victim.Root(1.4f);SpellVfx.Chains(this,victim.Position,victim.BodyContactPosition,behavior=="root"?new Color("7fb86a"):new Color("cfc6b4"),3,10f,1.4f);ContentMarkFx(victim,tint,behavior=="root"?"nature":"shadow"); }
                        if(behavior=="sleep") { victim.Sleep(1.5f);ContentMarkFx(victim,tint,"arcane");SpellVfx.Burst(this,victim.BodyContactPosition,"p_swirl",6,1.2f,SpellVfx.Fade(tint),3f,6f,6f,14f,40f,Vector2.Up,gravity:-12f); }
                        if(behavior=="shield_strip") { ContentBolt(unit,victim.BodyContactPosition,tint);victim.RemoveShield();ContentDamage(unit,victim,6,true,false,CombatDamageType.Magical); }
                        if(behavior=="wound") { ContentMarkFx(victim,new Color("c0392b"),"shadow");victim.ApplyHealingPenalty(.7f,3);ContentDamage(unit,victim,unit.AttackDamage*.6f,false,false); }
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
                            if(behavior=="corpse_buff")
                            {
                                SpellVfx.Sigil(this,unit.Position,"shadow",new Color("8cf08a"),65f,1f,.5f);
                                foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,65).Take(3)) { ally.ApplyTemporaryCombatBuff(1.08f,1f,3);SpellVfx.Flash(this,ally.BodyContactPosition,new Color("8cf08a"),12f,.35f); }
                            }
                            else
                            {
                                var at=token?.Position;
                                var minion=SpawnContentMinion(unit,"raised",unit.Team==Team.Player?28:64,unit.Team==Team.Player?4:9,12);
                                if(minion!=null)
                                {
                                    // the raised come up where the body lay (or beside the caster)
                                    if(at.HasValue) OffsetUnitWithinBattlefield(minion,at.Value-minion.Position);
                                    minion.MarkRaised();
                                    var wisp=new RaiseWisp{Start=unit.WeaponContactPosition,Target=minion.Position+new Vector2(0,-10)};AddChild(wisp);
                                    BattleParticles.SpawnResurrectRise(this,minion.Position,!at.HasValue);
                                }
                            }
                        });
                        return true;
                    }
                }
                break;
            case "attack_recovery":
                unit.ContentCooldown=12;BeginContentCast(unit,.8f,()=>{
                    SpellVfx.Shockwave(this,unit.Position,new Color("ffb05a"),60f,.5f);SpellVfx.Shockwave(this,unit.Position,new Color("ffb05a"),60f,.5f,.18f);
                    foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,60).Take(3)) { ally.ApplyAttackRecovery(.9f,3);SpellVfx.Flash(this,ally.BodyContactPosition,new Color("ffb05a"),12f,.3f); }
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
                { unit.ContentCooldown=10;unit.Conceal(.6f);
                  SpellVfx.Burst(this,unit.BodyContactPosition,"p_wisp",8,.6f,SpellVfx.Fade(new Color("a8f0ff")),3f,6f,10f,30f);
                  OffsetUnitWithinBattlefield(unit,new Vector2(unit.Team==Team.Enemy?-45:45,0));
                  SpellVfx.Flash(this,unit.BodyContactPosition,new Color("a8f0ff"),16f,.35f); }
                break;
            case "decoy":
                unit.ContentCooldown=14;SpawnContentMinion(unit,"decoy",1,0,5);break;
            case "revival_link":
                if(!unit.TryEffect("soul_link_once",999)) break;
                unit.ContentCooldown=15;
                foreach(var ally in NearbyContentUnits(unit.Team,unit.Position,70).Where(a=>a!=unit && !a.IsSummoned && a.Definition.IsBoss==false).Take(1))
                    { ally.ContentState="soul_link";SpellVfx.Bolt(this,unit.WeaponContactPosition,ally.BodyContactPosition,new Color("8cf08a"),.4f,2f);ContentMarkFx(ally,new Color("8cf08a")); }
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
        if(ranged && target.ProjectileDodgeCharges>0)
        { target.ProjectileDodgeCharges--;SpawnCombatNumber(target.Position+new Vector2(0,-24),"DODGE",new Color("cfe3f0"),.5f);return 0; }
        if(ranged && target.Definition.ContentBehavior=="projectile_dodge" && target.TryEffect("dodge",12))
        {
            // the eel ducks under the water: a splash where it was, and the shot passes over
            SpellVfx.Burst(this,target.Position,"p_splash",9,.5f,SpellVfx.Fade(new Color(.75f,.9f,.95f,.8f),new Color(.4f,.6f,.7f,.4f)),4f,8f,40f,100f,
                55f,Vector2.Up,gravity:300f,additive:false,layer:SpellVfx.TroopLayer+1);
            OffsetUnitWithinBattlefield(target,new Vector2(-8,4));
            SpawnCombatNumber(target.Position+new Vector2(0,-24),"DODGE",new Color("cfe3f0"),.5f);
            return 0;
        }
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
        if(target.Definition.ContentBehavior=="breakable_armor" && target.DirectHitsTaken<3)
        {
            damage*=.6f;
            // barnacle crust breaking away, a chunk a blow until it is bare
            if(validSource) ContentShardsFx(source,target,new Color(.62f,.66f,.58f),5);
        }
        if(validSource && dtype==CombatDamageType.Physical && penetration<=0f && target.Definition.ContentBehavior=="armor_facing" && target.TryEffect("facing_block",.6f))
            ContentSparks(target.WeaponContactPosition,new Color("ffd890"),6,new Vector2(Mathf.Sign(source.Position.X-target.Position.X),-.4f));
        if(dtype==CombatDamageType.Physical && !ranged && target.Definition.ContentBehavior=="parry" && target.TryEffect("parry",8))
        {
            damage*=.45f;
            if(validSource) { source.TakeDamage(target.AttackDamage*.35f,target.UnitName,CombatDamageType.Physical);ContentParryFx(target,source); }
        }
        if(validSource && source.Definition.ContentBehavior=="shield_break" && (target.ShieldHealth>0 || target.IsTacticalObject))
        {
            damage*=1.3f;
            if(target.TryEffect("shield_shatter",.8f)) ContentShardsFx(source,target,target.ShieldHealth>0?new Color("8bc7e2"):new Color(.6f,.5f,.38f),7);
        }
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
        if(validSource && penetration>0f && actual>0f) ContentPierceFx(source,target);
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
        if(behavior=="armor_break" && source.CommittedAttacks%3==0)
        { target.ApplyArmorBreak(12,2);ContentShardsFx(source,target,new Color(.7f,.72f,.76f)); }
        if(behavior=="stagger" && source.CommittedAttacks%4==0 && target.ApplyControl(.45f))
        {
            ApplyContentInterruptEquipment(source);
            SpellVfx.Shockwave(this,target.Position,new Color(.85f,.78f,.62f,.7f),Mathf.Max(22f,target.Radius*1.6f),.35f);
            SpellVfx.Burst(this,target.Position,"p_rubble",6,.5f,SpellVfx.Fade(new Color(.55f,.48f,.38f)),3f,6f,40f,90f,60f,Vector2.Up,
                gravity:380f,additive:false,layer:SpellVfx.TroopLayer+1);
        }
        if(behavior=="hook" && source.CommittedAttacks%3==0 && !target.Definition.IsBoss && target.Definition.Speed>0)
        { ContentHookChain(source,target);OffsetUnitWithinBattlefield(target,(source.Position-target.Position).Normalized()*22);target.ApplyControl(.18f); }
        if(behavior=="burn_fields") AddContentField("burn",source.Team,target.Position,26,3,2,source.UnitName);
        if(behavior=="wound") target.ApplyHealingPenalty(.7f,3);
        if(source.ContentState=="chill_next") { source.ContentState="";target.ApplyTemporarySpeedModifier(.85f,1.5f); }
        if(behavior is "cleave" or "sweep" or "great_cut" or "pierce")
        {
            if(behavior=="great_cut" || behavior=="pierce" || source.CommittedAttacks%3==0)
            {
                var dir=source.Team==Team.Player?1:-1;
                var reach=source.Position.DistanceTo(target.Position);
                var carried=false;
                foreach(var other in NearbyContentUnits(target.Team,target.Position,behavior=="sweep"?42:32)
                    .Where(e=>e!=target && (e.Position.X-source.Position.X)*dir>=-3).Take(2))
                {
                    ContentDamage(source,other,damage*.35f,ranged,false,source.AttackDamageType,false);
                    reach=Mathf.Max(reach,source.Position.DistanceTo(other.Position));
                    carried=true;
                    if(behavior=="pierce") ContentPierceFx(source,other);
                }
                if(carried && !ranged) ContentSlashArc(source,reach+6f,behavior=="great_cut"?new Color("ffd59a"):new Color("e8eef5"));
            }
        }
        if(target.Definition.ContentBehavior=="conditional_reflect" && !ranged && target.ContentAge%5<2)
        {
            source.TakeDamage(Math.Min(3,damage*.15f),target.UnitName,CombatDamageType.Physical);
            // thorns: splinters of bramble driven back into the striker
            if(target.TryEffect("thorns_fx",.5f))
                SpellVfx.Burst(this,target.BodyContactPosition,"p_splinter",6,.35f,SpellVfx.Fade(new Color(.5f,.56f,.3f),new Color(.32f,.26f,.16f)),3f,6f,
                    110f,190f,25f,(source.BodyContactPosition-target.BodyContactPosition).Normalized(),additive:false,layer:SpellVfx.TroopLayer+1);
        }
        ApplyContentHitEquipment(source,target,damage,ranged);
    }

    private bool TryContentReturn(Unit dead)
    {
        if(dead.IsTacticalObject || dead.NonCombatant || dead.IsSummoned) return false;
        if(dead.Definition.ContentBehavior=="return" || (dead.ContentState=="soul_link" &&
            _units.Any(u=>!u.IsDead && u.Team==dead.Team && u.Definition.ContentBehavior=="revival_link" && u.Position.DistanceTo(dead.Position)<90)))
        {
            dead.ContentState="";
            if(dead.TryReturn())
            {
                // back from the edge: grave-light (or the soul link's green) wells up and the soldier stands again
                SpellVfx.Pillar(this,dead.Position,new Color("bff0a8"),26f,70f,.7f);
                SpellVfx.Sigil(this,dead.Position,"holy",new Color("bff0a8"),24f,.9f,.6f);
                BattleParticles.SpawnResurrectRise(this,dead.Position,false);
                return true;
            }
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
        var field=new BattleContentField { Kind=kind,TeamOwner=owner,Position=point,Radius=radius,Remaining=duration,Power=power,Label=label,ZIndex=SpellVfx.GroundLayer };
        AddChild(field);_contentFields.Add(field);return field;
    }

    private void ApplyContentField(BattleContentField field)
    {
        if(field.Kind=="meteor")
        {
            MeteorImpactVfx(field.Position,field.Radius);
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
            {
                OffsetUnitWithinBattlefield(unit,(field.Position-unit.Position).LimitLength(10));unit.ApplyTemporarySpeedModifier(.8f,1.1f);
                // dragged: their feet scrape and grit trails off towards the eye
                SpellVfx.Burst(this,unit.Position,"p_dust",4,.6f,DustRamp,14,24,20,50,spread:40,direction:(field.Position-unit.Position).Normalized(),additive:false);
            }
            if(field.Kind=="miasma" && !friendly)
            { unit.ApplyHealingPenalty(.7f,1.4f);unit.ApplyBurn(field.Power,1.4f,field.Label); }
            if(field.Kind is "burn" or "holy" or "rot" && (!friendly || field.Kind=="rot") && unit.TryEffect("field_"+field.Kind,1f))
                unit.TakeDamage(field.Power,field.Label,CombatDamageType.Magical);
        }
    }
}

/// <summary>
/// A lasting area on the road (spell fields and enemy hazards). It lies on the ground plane as a turning rune
/// circle and a soft glow, or as a stain for smoke and rot, with its own drifting particles; a meteor's field
/// shows the warning circle filling until the strike. Rules live in BattleController.ApplyContentField.
/// </summary>
public partial class BattleContentField : Node2D
{
    public string Kind="",Label="";
    public Team TeamOwner;
    public float Radius=30,Remaining=3,Power=2,Pulse;
    public Unit Follow;
    public long FollowLife;
    public bool SingleTarget;
    private const float MeteorDelay = 1.2f;
    private float _age;
    private string _built;
    private readonly System.Collections.Generic.List<CpuParticles2D> _motes = new();

    private (string Sigil, Color Tint, bool Glow, float Spin) Look => Kind switch
    {
        "burn" => ("fire", new Color(1f, .5f, .16f), true, .3f),
        "meteor" => ("fire", new Color(1f, .28f, .2f), true, 1.2f),
        "holy" => ("holy", new Color(1f, .84f, .44f), true, .25f),
        "heal" => ("nature", new Color(.5f, .92f, .5f), true, .5f),
        "gravity" => ("shadow", new Color(.7f, .45f, 1f), true, -2.2f),
        "reveal" => ("holy", new Color(1f, .97f, .84f), true, .4f),
        "null" => ("arcane", new Color(.62f, .42f, .88f), true, -.6f),
        "smoke" => ("", new Color(.5f, .49f, .47f), false, 0f),
        "miasma" or "rot" => ("", new Color(.42f, .5f, .22f), false, 0f),
        _ => ("arcane", new Color(.66f, .72f, .82f), true, .3f),
    };

    public override void _Ready() => ProcessMode = ProcessModeEnum.Pausable;

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_built != Kind) Rebuild();
        QueueRedraw();
    }

    private void Rebuild()
    {
        _built = Kind;
        foreach (var mote in _motes) mote.QueueFree();
        _motes.Clear();
        Material = Look.Glow ? SpellVfx.Additive : null;
        var smoke = new[] { new Color(.55f, .54f, .52f, 0), new Color(.6f, .59f, .57f, .55f), new Color(.66f, .65f, .63f, 0) };
        var rot = new[] { new Color(.45f, .52f, .25f, 0), new Color(.5f, .58f, .28f, .5f), new Color(.55f, .6f, .35f, 0) };
        switch (Kind)
        {
            case "burn":
                Motes("p_flame", 10, .8f, SpellVfx.Fade(new Color(1f, .9f, .6f), new Color(.9f, .3f, .05f)), 10, 20, 15, 35, -40, true);
                Motes("p_ember", 8, 1.2f, SpellVfx.Fade(new Color(1f, .85f, .5f), new Color(1f, .5f, .15f)), 3, 6, 20, 60, -30, true);
                break;
            case "holy": Motes("p_orb", 8, 1.6f, SpellVfx.Fade(new Color(1f, .97f, .84f), new Color(1f, .83f, .44f)), 4, 8, 10, 30, -30, true); break;
            case "heal":
                Motes("p_orb", 5, 1.3f, SpellVfx.Fade(new Color(.9f, 1f, .78f), new Color(.5f, .92f, .5f)), 4, 7, 10, 25, -40, true);
                Motes("p_leaf", 2, 1.6f, SpellVfx.Fade(Colors.White), 6, 9, 10, 25, -20, false, 200);
                break;
            case "gravity": Motes("p_pebble", 10, .9f, SpellVfx.Fade(Colors.White), 5, 9, 0, 6, 0, false, 200, Radius * 3.2f); break;
            case "reveal": Motes("p_star", 6, 1.4f, SpellVfx.Fade(new Color(1f, .97f, .84f)), 5, 9, 4, 12, 20, true); break;
            case "null": Motes("p_rune", 5, 1.4f, SpellVfx.Fade(new Color(.86f, .72f, 1f), new Color(.62f, .42f, .88f)), 7, 11, 8, 20, -25, true); break;
            case "smoke": Motes("p_smoke", 12, 2.4f, smoke, 40, 70, 6, 18, -6, false, 25); break;
            case "miasma" or "rot": Motes("p_smoke", 7, 1.8f, rot, 26, 44, 6, 16, -10, false, 25); break;
        }
    }

    private void Motes(string texture, int amount, float life, Color[] ramp, float sizeMin, float sizeMax, float speedMin, float speedMax,
        float gravity, bool additive, float spin = 0f, float pull = 0f)
    {
        var tex = SpellVfx.Tex(texture);
        var scale = 1f / Mathf.Max(1, tex.GetWidth());
        var points = new Vector2[20];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = GD.Randf() * Mathf.Tau;
            var distance = pull > 0 ? Radius * (float)GD.RandRange(.75, 1) : Mathf.Sqrt(GD.Randf()) * Radius * .85f;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * BattleGroundPlane.DepthScale) * distance;
        }
        var gradient = new Gradient();
        gradient.SetColor(0, ramp[0]); gradient.SetColor(1, ramp[^1]);
        for (var i = 1; i < ramp.Length - 1; i++) gradient.AddPoint(i / (float)(ramp.Length - 1), ramp[i]);
        var motes = new CpuParticles2D
        {
            Amount = SpellVfx.Reduced ? Mathf.Max(1, amount / 3) : amount, Lifetime = life, Texture = tex, LocalCoords = false,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Points, EmissionPoints = points,
            Direction = Vector2.Up, Spread = 30, InitialVelocityMin = speedMin, InitialVelocityMax = speedMax, Gravity = new Vector2(0, gravity),
            ScaleAmountMin = sizeMin * scale, ScaleAmountMax = sizeMax * scale, AngleMin = 0, AngleMax = 360,
            AngularVelocityMin = -spin, AngularVelocityMax = spin, ColorRamp = gradient, RadialAccelMin = -pull, RadialAccelMax = -pull,
            // Above the troops: smoke hides them, flames lick up around them.
            ZIndex = SpellVfx.AirLayer - SpellVfx.GroundLayer, ProcessMode = ProcessModeEnum.Pausable,
        };
        if (additive) motes.Material = SpellVfx.Additive;
        AddChild(motes);
        _motes.Add(motes);
    }

    public override void _Draw()
    {
        var look = Look;
        var fade = Mathf.Clamp(_age / .35f, 0, 1) * Mathf.Clamp(Remaining / .6f, 0, 1);
        var plane = Transform2D.Identity.Scaled(new Vector2(1, BattleGroundPlane.DepthScale));
        DrawSetTransformMatrix(plane);
        if (!look.Glow)
        {
            DrawTextureRect(SpellVfx.Glow, new Rect2(-Vector2.One * Radius * 1.3f, Vector2.One * Radius * 2.6f), false, new Color(look.Tint, .45f * fade));
            DrawSetTransform(Vector2.Zero);
            return;
        }
        var breathe = .85f + .15f * Mathf.Sin(_age * 3.1f);
        DrawTextureRect(SpellVfx.Glow, new Rect2(-Vector2.One * Radius * 1.3f, Vector2.One * Radius * 2.6f), false, new Color(look.Tint, .3f * fade * breathe));
        if (Kind == "meteor")
        {
            // The strike lands when the fill reaches the rim; the rim beats faster as it closes.
            var progress = Mathf.Clamp(1 - Pulse / MeteorDelay, 0, 1);
            DrawTextureRect(SpellVfx.Glow, new Rect2(-Vector2.One * Radius * 1.2f * progress, Vector2.One * Radius * 2.4f * progress), false,
                new Color(look.Tint, .8f * fade));
            var beat = .6f + .4f * Mathf.Abs(Mathf.Sin(_age * (6f + progress * 14f)));
            for (var rim = 0; rim < 2; rim++)
                DrawTextureRect(SpellVfx.Ring, new Rect2(-Vector2.One * Radius * 1.18f, Vector2.One * Radius * 2.36f), false, new Color(look.Tint, fade * beat));
        }
        var spin = new Transform2D(_age * look.Spin, Vector2.Zero);
        DrawSetTransformMatrix(plane * spin);
        DrawTextureRect(SpellVfx.Tex("sigil_" + look.Sigil), new Rect2(-Vector2.One * Radius * 1.05f, Vector2.One * Radius * 2.1f), false,
            new Color(look.Tint, (Kind == "meteor" ? 1f : .62f) * fade * breathe));
        DrawSetTransform(Vector2.Zero);
    }
}
