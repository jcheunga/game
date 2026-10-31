using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class ContentRevampSmoke : Node
{
    private int _failures,_checks;
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Read<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    private static void Write(object owner,string name,object value)=>owner.GetType().GetField(name,Hidden).SetValue(owner,value);
    private static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);
    private void Check(bool ok,string label) { _checks++;if(!ok) _failures++;GD.Print($"CONTENT_CHECK: {(ok?"PASS":"FAIL")} {label}"); }
    public override void _Ready()=>Callable.From(Run).CallDeferred();
    private async void Run()
    {
        BattleController battle=null;
        try
        {
            if(!OS.GetCmdlineUserArgs().Any(a=>a.StartsWith("--save-suffix=content-review-")))
                throw new InvalidOperationException("An isolated content-review save is required.");
            var state=GameState.Instance;
            state.ResetProgress();state.SetAnalyticsConsent(false);state.SetShowHints(false);
            Check(state.ActiveDeckUnitIds.SequenceEqual(new[]{GameData.PlayerPeasantId}),"New saves begin with Peasant");
            Check(state.MaxUnitLevel==20,"Player units level to 20");
            var core=GameData.GetPlayerUnits().Where(u=>!u.LegacyRecruit).ToArray();
            Check(core.Length==32,"32 core recruits registered");
            Check(GameData.GetPlayerUnits().Count==37,"Existing legacy ownership IDs retained");
            Check(GameData.GetPlayerSpells().Count==42 && GameData.GetPlayerSpells().Count(s=>s.CardKind=="tactical")==12,"30 spells and 12 tactical cards share the loadout");
            Check(GameData.GetAllEquipment().Count==100,"100 relics registered");
            using(var catalog=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://docs/design/playable-content-catalog.json"))))
            {
                foreach(var entry in catalog.RootElement.GetProperty("units").EnumerateArray())
                {
                    var def=GameData.GetUnit(entry.GetProperty("id").GetString());
                    var matches=entry.GetProperty("levels").EnumerateArray().All(row=>
                    {
                        var level=row.GetProperty("level").GetInt32();
                        var stats=new UnitStats(def,UnitBalance.HealthScale(level),UnitBalance.DamageScale(level),level:level);
                        return row.GetProperty("health").GetSingle()==stats.MaxHealth && Math.Abs(row.GetProperty("attack").GetSingle()-stats.AttackDamage)<.001f;
                    });
                    Check(matches,$"{def.DisplayName}: all 20 documented stat records match combat");
                }
            }
            var sword=GameData.GetUnit(GameData.PlayerBrawlerId);
            var a=state.BuildPlayerUnitStatsAtLevel(sword,1);
            var z=state.BuildPlayerUnitStatsAtLevel(sword,20);
            Check(Math.Abs(z.MaxHealth-82)<.1f && Math.Abs(z.AttackDamage/a.AttackDamage-1.19f)<.001,"Swordsman retains shallow level growth");
            Check(a.Armor==z.Armor && a.CriticalChance==z.CriticalChance && a.CriticalDamageBonus==z.CriticalDamageBonus,"Defense and critical stats stay fixed with level");
            var budget=new UnitStatBonuses {Health=4,Damage=4,Armor=200,MagicalResistance=100,CriticalChance=100,CriticalDamage=100,AttackInterval=.9f};
            budget.ApplyCaps();
            Check(budget.Health==.1f && budget.Damage==.1f && budget.Armor==10 && budget.AttackInterval==.05f,"All permanent source budgets are bounded");
            foreach(var id in new[]{"spell_barrier_ward","spell_war_cry","spell_resurrect","spell_mirror_ward","tactic_supply_crate"})
            {
                var def=GameData.GetSpell(id);
                var first=new ResolvedSpellStats(def,1);var last=new ResolvedSpellStats(def,3);
                Check(first.Power==last.Power && first.SecondaryPower==last.SecondaryPower,$"{def.DisplayName} upgrades preserve bounded ratios and resources");
            }
            var seen=new HashSet<string>();
            for(var stage=1;stage<=100;stage++)
            {
                var data=GameData.GetStage(stage);
                var ids=data.Waves.SelectMany(w=>w.Entries).Select(e=>e.UnitId).Where(id=>!GameData.GetUnit(id).IsBoss).Distinct().ToArray();
                var fresh=ids.Where(id=>!seen.Contains(id)).ToArray();
                Check(stage<=3 || fresh.Length<=1,$"Stage {stage} introduces at most one ordinary threat");
                Check(ids.All(id=>GameData.GetUnit(id).CampaignDebutStage<=stage),$"Stage {stage} uses taught enemies");
                seen.UnionWith(ids);
                Check(data.EnemyLevel is >=1 and <=20 && data.EnemyHealthScale==1 && data.EnemyDamageScale==1,$"Stage {stage} avoids hidden growth multipliers");
            }
            state.UnlockNextStage(99);state.SetSelectedStage(10);state.PrepareCampaignBattle();
            battle=GD.Load<PackedScene>("res://scenes/Battle.tscn").Instantiate<BattleController>();
            AddChild(battle);battle.SetPhysicsProcess(false);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            Unit Spawn(Team team,UnitDefinition def,float x=550)=> (Unit)Call(battle,"SpawnUnit",team,new UnitStats(def),new Vector2(x,340));
            var defender=Spawn(Team.Player,GameData.GetUnit(GameData.PlayerDefenderId));
            var hp=defender.Health;
            var actual=defender.TakeDamage(10,"test",CombatDamageType.Physical);
            Check(actual<10 && Math.Abs(actual-10*(1-defender.PhysicalResistance/100))<.01f,"Armor reduces physical damage");
            var magic=defender.TakeDamage(10,"test",CombatDamageType.Magical);
            Check(Math.Abs(magic-10*(1-defender.MagicalResistance/100))<.01f,"Magical resistance is independent");
            defender.GiveShield(5,3);hp=defender.Health;defender.TakeDamage(3,"test",CombatDamageType.True);
            Check(Math.Abs(hp-defender.Health-3)<.01f,"Scripted true damage bypasses shields and defenses");
            defender.GiveShield(5,3);hp=defender.Health;defender.TakeDamage(3);
            Check(defender.Health==hp,"Temporary shields absorb an ordinary hit");
            Check(defender.ApplyControl(1) && !defender.ApplyControl(1),"Control resistance prevents repeated locks");
            defender.TickContentState(5);Check(defender.ApplyControl(1),"Control becomes available after recovery");
            var corpse=Spawn(Team.Player,GameData.GetUnit("player_gravebound_knight"));
            corpse.TakeDamage(corpse.MaxHealth*2,damageType:CombatDamageType.True);
            Check((bool)Call(battle,"TryContentReturn",corpse) && corpse.HealthRatio<=.251f,"Gravebound returns once at bounded health");
            corpse.TakeDamage(corpse.MaxHealth*2,damageType:CombatDamageType.True);
            Check(!(bool)Call(battle,"TryContentReturn",corpse),"Return allowance cannot repeat");
            var units=Read<List<Unit>>(battle,"_units");
            foreach(var u in units.ToArray()) {u.Despawn();Call(battle,"CleanupDeadUnits");}
            void ClearField()
            {
                foreach(var u in units.ToArray())u.Despawn();
                Call(battle,"CleanupDeadUnits");
                foreach(var field in Read<List<BattleContentField>>(battle,"_contentFields"))field.QueueFree();
                Read<List<BattleContentField>>(battle,"_contentFields").Clear();
            }
            foreach(var def in GameData.GetPlayerUnits().Concat(GameData.EnemyRosterIds.Select(GameData.GetUnit)).Append(GameData.GetUnit("enemy_ash_pilgrim")))
            {
                ClearField();
                var team=def.Side=="Player"?Team.Player:Team.Enemy;
                var actor=Spawn(team,def);
                var patient=Spawn(team,sword,558);patient.TakeDamage(30,damageType:CombatDamageType.True);
                var target=Spawn(team==Team.Player?Team.Enemy:Team.Player,sword,578);
                for(var step=0;step<30 && !actor.IsDead;step++)
                {
                    actor.TickContentState(.1f);
                    Call(battle,"TryContentAction",actor,target,.1f);
                    Call(battle,"TickContentWorld",.1f);
                }
                Check(actor.DefinitionId==def.Id && float.IsFinite(actor.Health),$"{def.DisplayName} initializes and runs its content action");
            }
            ClearField();
            var caster=Spawn(Team.Player,GameData.GetPlayerUnits().Single(u=>u.DisplayName=="Mage"));
            var resolved=false;
            Call(battle,"BeginContentCast",caster,.5f,(Action)(()=>resolved=true),true);
            caster.ApplyControl(.2f);Call(battle,"TickContentWorld",1f);
            Check(!resolved,"Interrupting a telegraphed cast cancels its action");
            ClearField();
            var guaranteed= new UnitDefinition {Id="critical_probe",DisplayName="Critical probe",MaxHealth=100,AttackDamage=10,CriticalChance=100,CriticalDamageBonus=50,DamageTakenScale=1};
            var striker=Spawn(Team.Player,guaranteed);var victim=Spawn(Team.Enemy,GameData.GetUnit("enemy_walker"));
            var criticalRng=Read<RandomNumberGenerator>(battle,"_contentRng");
            ulong criticalSeed=1;
            do {criticalRng.Seed=criticalSeed++;} while(criticalRng.Randf()*100>=striker.CriticalChance);
            criticalRng.Seed=criticalSeed-1;
            var hit=(float)Call(battle,"ContentDamage",striker,victim,10f,false,true,(CombatDamageType?)CombatDamageType.True,true);
            Check(Math.Abs(hit-15)<.001f,"A critical hit applies its authored damage bonus exactly once");
            Check(Math.Abs(Read<Dictionary<string,float>>(battle,"_unitDamageDealt")[striker.UnitName]-hit)<.001f,"Critical hit reporting records damage once");
            ClearField();
            foreach(var id in new[]{"tactic_supply_crate","tactic_healing_shrine"})
            {
                var def=GameData.GetSpell(id);
                var patientDef=new UnitDefinition {Id="patient_probe",DisplayName="Patient probe",MaxHealth=300,AttackDamage=0,DamageTakenScale=1};
                var patient=Spawn(Team.Player,patientDef,600);patient.TakeDamage(200,damageType:CombatDamageType.True);
                var hpBefore=patient.Health;var packs=Read<List<CouragePickup>>(battle,"_couragePickups");var count=packs.Count;
                Write(battle,"_courage",0f);var courage=0f;
                Call(battle,"ApplyExpandedUtility",new ResolvedSpellStats(def,1),new Vector2(600,340));
                var obj=units.Last();
                for(var step=0;step<(int)Math.Round((def.Duration+.1f)*10);step++) {obj.TickContentState(.1f);Call(battle,"TickTacticalObject",obj,null);}
                Check(obj.UtilityCharges==0,$"{def.DisplayName} delivers every finite pulse before expiry");
                if(id=="tactic_supply_crate") Check(packs.Count==count && Math.Abs(Read<float>(battle,"_courage")-courage-3*def.Power)<.01f,"Supply Crate sends three courage pulses straight to the meter, leaving no packs");
                else Check(Math.Abs(patient.Health-hpBefore-70)<.01f,"Healing Shrine delivers five bounded healing pulses");
                ClearField();
            }
            var militia=GameData.GetUnit("player_militia");
            var roster=Read<List<string>>(state,"_activeDeckUnitIds");roster.Clear();roster.Add(militia.Id);
            Check(state.TryPurchaseUnit(militia.Id, out _),"Militia recruits through the normal saved purchase path");
            var deck=Read<BattleDeckState>(battle,"_deck");deck.Initialize(new[]{militia});
            Write(battle,"_courage",40f);
            var before=Read<float>(battle,"_courage");Call(battle,"DeployPlayerUnit",militia);
            Check(units.Count(u=>!u.IsDead && u.DefinitionId==militia.Id)==2 && Math.Abs(before-Read<float>(battle,"_courage")-militia.Cost)<.01f,"Militia deploys two bodies for one payment");
            foreach(var spell in GameData.GetPlayerSpells())
            {
                var foe=Spawn(Team.Enemy,GameData.GetUnit("enemy_walker"),610);
                var ally=Spawn(Team.Player,sword,595);ally.TakeDamage(8);
                if(spell.EffectType=="resurrect") {Write(battle,"_lastDeadPlayerUnitId",militia.Id);Write(battle,"_lastDeadPlayerPosition",new Vector2(600,340));}
                var result=(string)Call(battle,"ApplySpellEffect",new ResolvedSpellStats(spell,1),new Vector2(600,340));
                Check(!result.Contains("No effect") && !result.Contains("full"),$"{spell.DisplayName} resolves a defined action");
                if(spell.EffectType=="resurrect") Check(units.Last().HasReturned && Math.Abs(units.Last().HealthRatio-.5f)<.01f,"Resurrect restores the eligible owned ID with one shared return allowance");
                foreach(var u in units.ToArray())u.Despawn();
                Call(battle,"CleanupDeadUnits");
            }
            ClearField();
            var archer=Spawn(Team.Player,GameData.GetUnit("player_shooter"));
            var firstTarget=Spawn(Team.Enemy,GameData.GetUnit("enemy_walker"),575);
            var focusTarget=Spawn(Team.Enemy,GameData.GetUnit("enemy_walker"),620);
            Call(battle,"ApplySpellEffect",new ResolvedSpellStats(GameData.GetSpell("tactic_war_horn"),1),focusTarget.Position);
            Check((Unit)Call(battle,"FindClosestEnemy",archer)==focusTarget,"War Horn changes eligible target priority without changing allegiance");
            ClearField();
            var summon=Spawn(Team.Enemy,GameData.GetUnit("enemy_walker"));
            summon.IsSummoned=true;summon.CanDropCourage=false;
            var mana=Read<float>(battle,"_mana");summon.Despawn();Call(battle,"CleanupDeadUnits");
            Check(Read<float>(battle,"_mana")==mana,"Expired summons cannot farm mana");
            state.ReloadFromDisk();
            Check(state.IsUnitOwned(militia.Id),"New ownership survives saving and reload");
            var fixture=state.BuildSaveData();
            var legacy=GameData.GetPlayerUnits().Where(u=>u.LegacyRecruit).ToArray();
            fixture.OwnedPlayerUnitIds=fixture.OwnedPlayerUnitIds.Concat(legacy.Select(u=>u.Id)).Distinct().ToArray();
            foreach(var def in legacy)fixture.UnitLevels[def.Id]=5;
            fixture.ActiveDeckUnitIds=legacy.Select(u=>u.Id).Append(militia.Id).ToArray();
            Call(state,"ApplySavedData",fixture);
            foreach(var def in legacy)Check(state.IsUnitOwned(def.Id) && state.GetUnitLevel(def.Id)==5 && state.GetActiveDeckUnits().Any(u=>u.Id==def.Id),$"Legacy {def.DisplayName} keeps ownership, level and deck access");
            CheckRelicEquipmentPreview(state);
            CheckProgressionExpansion(state);
        }
        catch(Exception e) {GD.PrintErr(e);_failures++;}
        if(battle!=null) {battle.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
        try {await CheckBossRushAndFeedback();}catch(Exception e){GD.PrintErr(e);_failures++;}
        await LiveUiReview.StopAudio(this);
        GD.Print($"CONTENT_REVAMP_RESULT: {_checks} checks, {_failures} failures");
        GetTree().Quit(_failures==0?0:1);
    }

    private void CheckRelicEquipmentPreview(GameState state)
    {
        var fixture=state.BuildSaveData();
        fixture.Gold=100000;fixture.Essence=100;
        fixture.OwnedPlayerUnitIds=GameData.PlayerRosterIds;
        fixture.UnitLevels=GameData.PlayerRosterIds.ToDictionary(id=>id,_=>20);
        fixture.ActiveDeckUnitIds=new[]{"player_brawler","player_shooter"};
        fixture.PromotedUnitIds=new[]{"player_brawler"};
        fixture.OwnedEquipmentIds=new[]{"relic_sharpened_edge","relic_guardian_shield","relic_tower_charm"};
        fixture.UnitEquipmentSlots.Clear();fixture.UnitEquipmentSlot2.Clear();
        Call(state,"ApplySavedData",fixture);
        var sword=GameData.GetUnit("player_brawler");var guardian=GameData.GetEquipment("relic_guardian_shield");
        Check(state.TryEquipItem(sword.Id,"relic_sharpened_edge"),"An owned relic equips to the first slot");
        var savedBefore=System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData());
        var preview=state.BuildRelicEquipmentPreview(sword.Id,guardian);
        Check(preview.CanApply && preview.After.Armor>preview.Current.Armor && preview.After.AttackDamage<preview.Current.AttackDamage,"Relic replacement previews gains and the removed item's damage loss");
        Check(savedBefore==System.Text.Json.JsonSerializer.Serialize(state.BuildSaveData()),"Equipment previews do not mutate or save progression");
        Check(state.TryEquipItem(sword.Id,guardian.Id) && Math.Abs(state.BuildPlayerUnitStats(sword).Armor-preview.After.Armor)<.001f && Math.Abs(state.BuildPlayerUnitStats(sword).AttackDamage-preview.After.AttackDamage)<.001f,"Confirmed equipment matches the preview's battle stats");
        var second=state.BuildRelicEquipmentPreview(sword.Id,GameData.GetEquipment("relic_tower_charm"),2);
        var armorLimit=second.Limits.Single(l=>l.Label=="Armor");
        Check(Math.Abs(armorLimit.Supplied-11.6f)<.01f && armorLimit.Applied==10 && Math.Abs(armorLimit.Excess-1.6f)<.01f,"Slot comparison explains the combined relic armor cap");
        Check(state.TryEquipItem2(sword.Id,"relic_tower_charm") && state.BuildPlayerUnitStats(sword).Armor==second.After.Armor,"Second-slot confirmation uses the same shared bonus cap");
        Check(!state.TryEquipItem(sword.Id,"relic_tower_charm") && !state.TryEquipItem("player_shooter","relic_tower_charm"),"A second-slot relic cannot also occupy either ally's first slot");
        Check(!state.TryEquipItem2(sword.Id,guardian.Id),"A first-slot relic cannot also occupy the second slot");
        Check(!state.CanEquipRelic("player_peasant","relic_sharpened_edge",2,out var locked) && locked.Contains("Promote"),"Unpromoted allies receive a clear second-slot lock reason");
        var remove=state.BuildRelicEquipmentPreview(sword.Id,null,2);
        Check(remove.CanApply && remove.After.Armor<remove.Current.Armor,"Removal preview includes loss of second-slot bonuses");
        Check(state.TryUnequipRelic(sword.Id,2,out _) && state.GetUnitEquipment(sword.Id)?.Id==guardian.Id,"Removing the second relic preserves the first slot");
        Check(state.GetOwnedEquipment().Contains("relic_tower_charm"),"Removal preserves collection ownership");
        state.ReloadFromDisk();
        Check(state.GetUnitEquipment(sword.Id)?.Id==guardian.Id && state.GetUnitEquipment2(sword.Id)==null,"Relic slot changes survive saving and reload");
        var unowned=state.BuildRelicEquipmentPreview(sword.Id,GameData.GetEquipment("relic_war_brand"));
        Check(!unowned.CanApply && unowned.Reason.Contains("Acquire"),"Unowned relics can be compared without granting ownership");
    }
}
