"""Build the playable content from the reviewed roster and balance references."""
import copy
import json
import struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
def read(path): return json.loads((ROOT/path).read_text())
def write(path,key,entries):
    (ROOT/path).write_text(json.dumps({key:entries},indent=2,ensure_ascii=False)+'\n')

roster=read('docs/design/melee-roster-revision.json')
policy=read('docs/design/progression-balance-policy.json')
concepts=read('docs/design/content-expansion-roster.json')
player_copy=read('docs/design/player-facing-content.json')
old_units={u['Id']:u for u in read('data/units.json')['Units']}
old_stages=read('data/stages.json')['Stages']
old_spells={s['Id']:s for s in read('data/spells.json')['Spells']}
core_behaviors={
 'Peasant':'levy','Militia':'paired_militia','Spearman':'piercing_thrust','Archer':'volley',
 'Swordsman':'cleave','Field Medic':'field_heal','Axeman':'shield_break','Maceman':'armor_break',
 'Caravan Engineer':'repair_construct','Billman':'hook','Man-at-Arms':'guard',
 'Crossbowman':'precision_shot','Shield Knight':'interception','Battle Monk':'blessing',
 'Paladin':'radiant_guard','Pikeman':'brace','Halberdier':'sweep','Cavalry Rider':'charge',
 'Gravebound Knight':'return','Greatswordsman':'great_cut','Quartermaster':'supply',
 'Berserker':'frenzy','Sapper Crew':'demolition','Outrider':'intercept','Longbowman':'charged_shot',
 'Warhammer Knight':'stagger','Rogue':'vanish','Duelist':'parry','Mage':'beam',
 'Poleaxe Knight':'armor_pierce','Banner Knight':'rally','Ballista Crew':'siege_shot',
}
visuals={
 'Levy':('fighter','club'),'Spear':('fighter','spear'),'Sword':('fighter','sword'),
 'Axe':('fighter','axe'),'Mace':('fighter','mace'),'Hooked polearm':('fighter','bill'),
 'Professional infantry':('shield','sword'),'Armored infantry':('shield','hammer'),
 'Long pike':('fighter','pike'),'Versatile polearm':('fighter','halberd'),
 'Two-handed sword':('fighter','greatsword'),'Sword and buckler':('fighter','sword'),
 'Cavalry':('skirmisher','spear'),'Fantasy melee':('shield','sword'),
 'Command melee':('banner','sword'),'Aggressive melee':('berserker','axe'),
 'Mobile melee':('rogue','dagger'),'Ranged support':('gunner','bow'),
 'Recovery':('support','staff'),'Caravan support':('support','hammer'),
 'Magical support':('sniper','staff'),'Demolition':('support','hammer'),
 'Artillery':('ballista','ballista'),
}
units=[]
for p in roster['core_units']:
    b=p['base']
    unit=copy.deepcopy(old_units.get(p['id'],{}))
    visual,weapon=visuals.get(p['family'],('fighter','sword'))
    unit.update({
      'Id':p['id'],'DisplayName':p['name'],'Side':'Player','SquadTag':p['role'],
      'UnlockStage':p['discovery'],'GoldCost':0 if p['name']=='Peasant' else min(420,max(50,round(p['recruit_gold']*.3))),
      'Cost':int(p['courage']),'MaxHealth':b['hp'],'AttackDamage':b['damage'],
      'Armor':b['armor'],'MagicalResistance':b['magic'],'CriticalChance':b['crit'],
      'CriticalDamageBonus':b['crit_damage'],'DamageType':'magical' if b['damage_type']=='M' else 'physical',
      'Speed':b['speed'],'AttackRange':b['range'],'AttackCooldown':b['interval'],
      'AttackSplashRadius':b['splash'],'BaseDamage':int(b['structure']),
      'DamagesStructures':b['structure']>0,'UsesProjectile':b['projectile'],
      'DeployCooldown':p['deploy_interval'],'BodiesPerDeployment':p['bodies'],
      'DamageTakenScale':1,'DamageReflectScale':0,
      'VisualClass':unit.get('VisualClass',visual),'VisualScale':unit.get('VisualScale',1),
      'PlaceholderVisual':p['source']=='N','PlaceholderWeapon':weapon,
      'ColorHex':{'frontline':'b8a079','recon':'7caa92','support':'bca76b','breach':'aa927f'}.get(p['role'],'b8a079'),
      'Description':p['identity'],'Weakness':p['weakness'],'ContentBehavior':core_behaviors[p['name']],
      'SpecialCooldown':12,'SpecialAbilityId':'','SpecialSpawnCount':0,'SpecialSpawnUnitId':'',
      'HomeZone':policy['enemy_zones'][(p['discovery']-1)//10]['zone'],
      'AuraRadius':0,'AuraAttackDamageScale':1,'AuraSpeedScale':1,'BusRepairAmount':0,
      'DeathBurstDamage':0,'SpawnOnDeathCount':0,'LegacyRecruit':False,
      'AggroRangeX':max(160,b['range']+35),'AggroRangeY':28,
    })
    if p['name']=='Caravan Engineer':unit['BusRepairAmount']=2
    if p['name']=='Banner Knight':unit.update(AuraRadius=55,AuraAttackDamageScale=1.05)
    if p['name']=='Battle Monk':unit.update(AuraRadius=50,AuraAttackDamageScale=1.03)
    units.append(unit)

# Existing ownership IDs stay usable, but retired concepts are not offered to new saves.
legacy_budgets={
 'player_hound':(28,5,0,0,12,.9,75,'hound_howl'),
 'player_grenadier':(54,10,6,8,26,1.6,29,'volatile_flask'),
 'player_necromancer':(48,9,4,12,30,1.5,29,'corpse_raise_ally'),
 'player_lantern_guard':(80,11,22,5,28,1.2,30,'sweep'),
 'player_stormcaller':(44,10,2,18,28,1.5,30,'chain_attack'),
}
core_ids={u['Id'] for u in units}
for uid,(hp,dmg,armor,magic,cost,interval,speed,behavior) in legacy_budgets.items():
    if uid in core_ids:continue
    u=copy.deepcopy(old_units[uid])
    u.update(MaxHealth=hp,AttackDamage=dmg,Armor=armor,MagicalResistance=magic,
             Cost=cost,AttackCooldown=interval,Speed=speed,DamageTakenScale=1,
             CriticalChance=6,CriticalDamageBonus=50,LegacyRecruit=True,UnlockStage=101,
             ContentBehavior=behavior,SpecialAbilityId='',SpecialSpawnCount=0,
             AttackSplashRadius=24 if uid=='player_grenadier' else 0)
    units.append(u)

new_enemies={e['id']:e for e in concepts['new_enemies']}
existing_enemies={e['id']:e for key in ['existing_enemies','existing_bosses'] for e in concepts[key]}
opening={'enemy_walker':1,'enemy_runner':2,'enemy_brute':3,'enemy_spitter':3}
enemy_visual={
 'swarm':'hound','armor_facing':'shield','channel_summon':'necromancer',
 'ammo_movement':'gunner','pull':'spitter','phase':'walker','breakable_armor':'shield',
 'target_priority':'rogue','charge':'skirmisher','burn_fields':'spitter',
 'vulnerability_cycle':'crusher','hazard_object':'spitter','attachment':'walker',
 'blight_fields':'spitter','healing_channel':'support','shield_strip':'sniper',
 'leap':'rogue','conditional_reflect':'brute','root':'spitter','marked_splash':'brute',
 'tether':'brute','pierce':'gunner','corpse_channel':'necromancer','ambush':'brute',
 'friendly_fire_fields':'bloater','terrain_regen':'crusher','projectile_dodge':'runner',
 'corpse_buff':'howler','attack_recovery':'howler','stealth':'rogue','decoy':'walker',
 'armor_burn':'crusher','sleep':'spitter','structure_splash':'crusher',
 'wound':'fighter','null_field':'jammer','revival_link':'banner','optional_encounter':'walker',
}
for p in policy['enemy_profiles']:
    b=p['base'];u=copy.deepcopy(old_units.get(p['id'],{}));e=new_enemies.get(p['id'])
    is_boss=p['side']=='boss'
    behavior=e['requires'] if e else ''
    debut=e.get('proposed_debut_stage') if e else opening.get(p['id'])
    unit={
      **u,'Id':p['id'],'DisplayName':p['name'],'Side':'Enemy','IsBoss':is_boss,
      'NonCombatant':p['side']=='optional','UnlockStage':debut or 101,
      'CampaignDebutStage':debut or (1 if is_boss else 101),
      'HomeZone':p.get('zone') or (existing_enemies.get(p['id'],{}).get('zone') or 'city'),
      'Cost':0,'MaxHealth':round(b['hp']*(.7 if is_boss else 1 if p['side']=='optional' else .6)),
      'AttackDamage':round(b['damage']*(.65 if is_boss else .55),2),'Armor':round(b['armor']*.7,2),
      'MagicalResistance':round(b['magic']*.7,2),'CriticalChance':b['crit'],'CriticalDamageBonus':b['crit_damage'],
      'DamageType':'magical' if b['damage_type']=='M' else 'physical',
      'Speed':b['speed'],'AttackRange':b['range'],'AttackCooldown':b['interval'] or 999,
      'AttackSplashRadius':min(30,b['splash']),'BaseDamage':max(1,round(b['structure']*.55)) if b['structure'] else 0,
      'DamagesStructures':b['structure']>0,'UsesProjectile':b['projectile'],
      'DamageTakenScale':1,'DamageReflectScale':min(.08,u.get('DamageReflectScale',0)),
      'ContentBehavior':behavior,'Description':p['identity'],'Weakness':e.get('counter','') if e else '',
      'VisualClass':u.get('VisualClass',enemy_visual.get(behavior,'walker')),
      'VisualScale':u.get('VisualScale',1.45 if b['hp']>=150 else 1.0),
      'PlaceholderVisual':p['source']=='N','PlaceholderWeapon':'staff' if b['projectile'] else 'axe',
      'ColorHex':u.get('ColorHex',{'city':'907860','harbor':'629da0','foundry':'c6794c','quarantine':'91a467','thornwall':'8e9470','basilica':'b8ae9a','mire':'829671','steppe':'bda476','gloamwood':'8d80a6','citadel':'ad786f'}.get(p.get('zone'),'9c9686')),
      'AggroRangeX':max(175,b['range']+50),'AggroRangeY':28,
    }
    if e:unit.update(SpecialAbilityId='',SpecialCooldown=9,SpecialSpawnUnitId='',SpecialSpawnCount=0)
    if is_boss:
        unit.update(SpecialSpawnUnitId='enemy_walker',SpecialSpawnCount=min(2,u.get('SpecialSpawnCount',1)),
                    SpecialBuffAttackDamageScale=min(1.1,u.get('SpecialBuffAttackDamageScale',1)))
    units.append(unit)
for unit in units:
    if unit['Id'] in player_copy:
        unit['Description']=player_copy[unit['Id']]['description']
        unit['Weakness']=player_copy[unit['Id']]['weakness']

write('data/units.json','Units',units)

bonus_labels={'hp_pct':'health','damage_pct':'attack','speed_pct':'speed','range_pct':'reach',
 'structure_damage_pct':'structure damage','attack_interval_reduction_pct':'attack recovery',
 'armor':'armor','physical_pp':'physical resistance','magic_pp':'magic resistance',
 'crit_pp':'critical chance','crit_damage_pp':'critical damage'}
def bonus_summary(b):
    parts=[]
    for key,value in b.items():
        if value and key in bonus_labels:
            suffix='%' if key.endswith('_pct') else ' pp' if key.endswith('_pp') else ''
            parts.append(f'+{value:g}{suffix} {bonus_labels[key]}')
    return ', '.join(parts) or 'No permanent stat bonus'

equipment=[]
for e in policy['relics']:
    b=e['bonuses']
    item={
      'Id':e['id'],'DisplayName':e['name'],'Description':bonus_summary(b)+'.' if e['special_rule'].startswith('Keep the current') else e['special_rule'],
      'Rarity':e['rarity'],'Family':e['family'],'EffectId':e['id'],
      'UnlockStage':{'common':1,'rare':12,'epic':32,'hardened':72}[e['rarity']],
      'HealthScale':1+b.get('hp_pct',0)/100,'DamageScale':1+b.get('damage_pct',0)/100,
      'SpeedScale':1+b.get('speed_pct',0)/100,'RangeScale':1+b.get('range_pct',0)/100,
      'StructureDamageScale':1+b.get('structure_damage_pct',0)/100,
      'AttackIntervalReductionPct':b.get('attack_interval_reduction_pct',0),
      'ArmorBonus':b.get('armor',0),'PhysicalResistanceBonus':b.get('physical_pp',0),
      'MagicalResistanceBonus':b.get('magic_pp',0),'CriticalChanceBonus':b.get('crit_pp',0),
      'CriticalDamageBonus':b.get('crit_damage_pp',0),
    }
    equipment.append(item)
write('data/equipment.json','Equipment',equipment)

spells=[]
for spell in concepts['existing_spells']:
    s={k:v for k,v in spell.items() if k and k[0].isupper()}
    s['CardKind']='spell'
    s['GoldCost']=max(0,min(350,s.get('GoldCost',0)))
    if s['EffectType']=='polymorph':
        s['Description']='Briefly disables one enemy. Ordinary targets resist repeated control; bosses recover sooner.'
    spells.append(s)
spell_power={
 'Aegis Bolt':(14,0,24,5),'Radiant Rebuke':(10,0,24,.3),'Tidal Push':(8,36,65,.5),
 'Anchor Hex':(0,0,26,2),'Molten Brand':(12,12,26,3),'Meteor':(30,2,48,3),
 'Purifying Light':(8,0,52,0),'Withering Seal':(0,.7,36,4),'Gale Step':(0,44,36,1.5),
 'Gravitation Well':(0,12,50,3),'Soul Harvest':(4,3,65,0),'Consecrated Ground':(3,0,52,4),
 'Verdant Renewal':(4,0,24,4),'Bramble Snare':(3,0,54,1.5),'Chain Lightning':(15,.7,86,0),
 'Rallying Advance':(1.08,1.12,60,4),'Revealing Flare':(5,0,72,5),
 'Mirror Ward':(.5,1,48,5),'Banish':(8,0,40,0),'Royal Edict':(0,0,60,2),
}
for e in concepts['new_spells']:
    power,secondary,radius,duration=spell_power[e['name']]
    spells.append({
      'Id':e['id'],'DisplayName':e['name'],'UnlockStage':e['discovery_stage'],
      'GoldCost':60+e['discovery_stage']*2,'ManaCost':e['prototype_mana'],
      'Cooldown':e['prototype_cooldown'],'EffectType':e['id'].removeprefix('spell_'),
      'Power':power,'SecondaryPower':secondary,'Radius':radius,'Duration':duration,
      'Description':e['effect']+' '+e['tradeoff'],'ColorHex':'b8a7d3',
      'CardKind':'spell','Family':e['family'],
    })
old_tactics={e['name']:e for e in concepts['new_tactical_items']}
for i,e in enumerate(read('docs/design/balance-stat-catalog.json')['tactical_items']):
    v=e['values'];concept=old_tactics[e['name']]
    power=v['damage'] or v['heal'] or v['courage']
    spells.append({
      'Id':e['id'],'DisplayName':e['name'],'UnlockStage':[6,25,8,28,18,44,84,16,36,21,55,48][i],
      'GoldCost':70+i*12,'ManaCost':int(v['mana']),'Cooldown':v['cooldown'],
      'EffectType':e['id'],'Power':power,'Radius':v['radius'],'Duration':v['lifetime'],
      'SecondaryPower':v['control'],'ObjectHealth':v['hp'],'ObjectArmor':v['armor'],
      'ObjectMagicalResistance':v['magic'],'Charges':int(v['charges']),
      'Description':concept['signature']+' '+concept['tradeoff'],'ColorHex':'b3a17a','CardKind':'tactical',
      'Family':'Tactical Gear',
    })
write('data/spells.json','Spells',spells)

# Teach four opening types, then exactly one regional regular debut per zone.
debut_ids=[next(e['id'] for e in concepts['new_enemies'] if e.get('proposed_debut_stage')==z*10+4) for z in range(10)]
bosses={10:'enemy_boss',15:'enemy_boss_docks',20:'enemy_boss_tidemaster',30:'enemy_boss_forge',
        35:'enemy_boss_ward',40:'enemy_boss_plague_monarch',50:'enemy_boss_pass',
        55:'enemy_boss_basilica',60:'enemy_boss_reliquary',70:'enemy_boss_mire',
        80:'enemy_boss_steppe',90:'enemy_boss_verge',95:'enemy_boss_citadel',100:'enemy_boss_ashen_regent'}
names={u['Id']:u['DisplayName'] for u in units}
stages=[]
for source in old_stages:
    s=copy.deepcopy(source);n=s['StageNumber'];zone=(n-1)//10;local=(n-1)%10+1
    level=zone*2+(1 if local<=5 else 2)
    familiar=['enemy_walker']+(['enemy_runner'] if n>=2 else [])+(['enemy_brute','enemy_spitter'] if n>=3 else [])
    familiar+=debut_ids[:zone]
    current=debut_ids[zone] if local>=4 else None
    count=3 if n<=3 else 4 if n<=10 else min(6,3+zone//2)
    waves=[]
    for wi in range(count):
        entries=[]
        ordinary=1 if n<=2 else 2+int(zone>=3)
        ids=[familiar[(n+wi+j)%len(familiar)] for j in range(ordinary)]
        if wi==0:ids[0]='enemy_walker'
        if n<=3:ids=['enemy_walker']+(['enemy_runner'] if n>=2 and wi==1 else [])+(['enemy_brute','enemy_spitter'] if n==3 and wi==2 else [])
        if current and wi in ([1] if local==4 else [1,count-1]):
            ids.append(current)
        for uid in dict.fromkeys(ids):entries.append({'UnitId':uid,'Count':ids.count(uid)})
        if n in bosses and wi==count-1:entries.append({'UnitId':bosses[n],'Count':1})
        waves.append({
            'Label':('First sighting: '+names[current]) if current and local==4 and wi==1 else ['Road patrol','Broken ranks','Crossroads pressure','Gate defenders','Last reserve','Commander escort'][wi],
            'TriggerTime':3+wi*(14 if n<11 else 12),'SpawnInterval':.85,
            'Area':['Approach','Crossroads','Gate'][min(2,wi)],'SpawnXRatio':1,
            'AdvanceTriggerXRatio':-1 if wi==0 else min(.82,.2+wi*.16),'Entries':entries,
        })
    s.update(EnemyLevel=level,EnemyHealthScale=1,EnemyDamageScale=1,
        PlayerBaseHealth=300+zone*12,EnemyBaseHealth=120+zone*14+local*3,
        EnemySpawnMin=4,EnemySpawnMax=6,BonusWaveChance=0,
        WalkerWeight=1,RunnerWeight=0,BruteWeight=0,SpitterWeight=0,CrusherWeight=0,BossWeight=0,
        BossSpawnStartTime=0,Waves=waves,NewEnemyId=current if local==4 else '',
        RewardGold=55+zone*9+local*2+(35 if n in bosses else 0),
        TwoStarBusHullRatio=.5,ThreeStarTimeLimitSeconds=150+zone*6,
        EntryFoodCost=2,ExploreFoodCost=1,Modifiers=[],Hazards=[],
        Objectives=[{'Type':'clear_route','Label':'Break the stronghold while protecting the caravan.'}],
        Description=('Begin with the Peasant. Recruit Militia next, and keep courage for another body.' if n==1 else
            ('Learn '+names[current]+': '+next(u['Description'] for u in units if u['Id']==current) if current and local==4 else
             'Recombine familiar threats. Protect your melee line, use a counter when needed, and break the stronghold.')))
    if n in bosses:s['EnemyBaseHealth']+=50
    if local==4:
        # Teach one regional rule against familiar bodies, without stacking healing,
        # corpse summoning and ambush mechanics into the first-sighting encounter.
        veteran='enemy_rustguard' if zone>=2 else 'enemy_brute'
        packs=[['enemy_walker','enemy_runner'],
          ['enemy_walker',current],
          ['enemy_walker','enemy_spitter'],
          [veteran,current],
          ['enemy_walker','enemy_runner'],
          ['enemy_walker',current]]
        for wave,pack in zip(s['Waves'],packs):
            wave['Entries']=[{'UnitId':uid,'Count':pack.count(uid)} for uid in dict.fromkeys(pack)]
    if n==80:
        # The commander encounter should not layer multiple healers and corpse summoners.
        # Keep learned threats, but give early melee a recoverable line before the boss.
        packs=[['enemy_walker','enemy_walker','enemy_rustguard'],
          ['enemy_runner','enemy_drowned_harpooner','enemy_cinder_spitter'],
          ['enemy_brute','enemy_cinder_spitter','enemy_bone_lancer'],
          ['enemy_walker','enemy_cliff_stalker','enemy_runner'],
          ['enemy_rustguard','enemy_bone_lancer','enemy_walker'],
          ['enemy_walker','enemy_bone_lancer',bosses[n]]]
        for wave,pack in zip(s['Waves'],packs):
            wave['Entries']=[{'UnitId':uid,'Count':pack.count(uid)} for uid in dict.fromkeys(pack)]
    if n==95:
        packs=[['enemy_walker','enemy_runner','enemy_shade'],
          ['enemy_brute','enemy_rustguard','enemy_null_magister'],
          ['enemy_cinder_spitter','enemy_bone_lancer','enemy_walker'],
          ['enemy_drowned_harpooner','enemy_runner','enemy_walker'],
          ['enemy_rustguard','enemy_shade','enemy_brute'],
          ['enemy_walker','enemy_bone_lancer',bosses[n]]]
        for wave,pack in zip(s['Waves'],packs):
            wave['Entries']=[{'UnitId':uid,'Count':pack.count(uid)} for uid in dict.fromkeys(pack)]
    # Keep authored mission sites and geometry; their guards are already taught types.
    for event in s.get('MissionEvents',[]):event['StartTime']=max(12,event.get('StartTime',12))
    stages.append(s)
write('data/stages.json','Stages',stages)
assert len(stages)==100 and len({u['Id'] for u in units})==len(units)
assert len(equipment)==100 and len(spells)==42

# Keep the server-compatible Codex and review documents reproducible from playable data.
codex_path=ROOT/'scripts/core/CodexCatalog.cs'
codex=codex_path.read_text()
prefix=codex.split('\tprivate static readonly CodexEntry[] Entries =')[0]
suffix=codex[codex.index('\tprivate static readonly Dictionary<string, CodexEntry> ById;'):]
entries=[]
for u in units:
    category='unit' if u['Side']=='Player' else 'boss' if u.get('IsBoss') else 'enemy'
    stats=f"HP {u['MaxHealth']:g} · Attack {u['AttackDamage']:g} · Armor {u.get('Armor',0):g} · Magic resistance {u.get('MagicalResistance',0):g}% · Critical chance {u.get('CriticalChance',0):g}% · Critical damage +{u.get('CriticalDamageBonus',50):g}%"
    entries.append((u['Id'],category,u['DisplayName'],(u.get('Description','')+' '+u.get('Weakness','')).strip(),stats))
for s in spells:
    entries.append((s['Id'],'spell',s['DisplayName'],s['Description'],f"{s['ManaCost']} mana · {s['Cooldown']:g}s cooldown · {s.get('Duration',0):g}s duration"))
for e in equipment:
    original=next(p for p in policy['relics'] if p['id']==e['Id'])
    entries.append((e['Id'],'relic',e['DisplayName'],e['Description'],bonus_summary(original['bonuses'])))
codex_path.write_text(prefix+'\tprivate static readonly CodexEntry[] Entries =\n\t{\n'+
    ''.join('\t\tnew('+', '.join(json.dumps(v,ensure_ascii=False) for v in entry)+'),\n' for entry in entries)+
    '\t};\n\n'+suffix)

def f32(v): return struct.unpack('f',struct.pack('f',v))[0]
def resolved(u,level):
    return {'level':level,'health':round(f32(u['MaxHealth']*f32(1+f32((level-1)*f32(.015))))),
      'attack':round(u['AttackDamage']*(1+(level-1)*.01),3),
      'structure_damage':round(u.get('BaseDamage',0)*(1+(level-1)*.01),3),
      'armor':u.get('Armor',0),'physical_resistance':round(min(75,100*u.get('Armor',0)/(100+u.get('Armor',0))+u.get('PhysicalResistanceBonus',0)),3),
      'magic_resistance':u.get('MagicalResistance',0),'critical_chance':u.get('CriticalChance',0),
      'critical_damage_bonus':u.get('CriticalDamageBonus',50),'damage_type':u.get('DamageType','physical'),
      'speed':u['Speed'],'range':u['AttackRange'],'attack_interval':u['AttackCooldown']}
catalog={'status':'implemented prototype; generated from playable definitions','player_level_cap':20,
 'units':[{'id':u['Id'],'name':u['DisplayName'],'side':u['Side'],'boss':u.get('IsBoss',False),
    'optional':u.get('NonCombatant',False),'legacy':u.get('LegacyRecruit',False),
    'discovery':u.get('UnlockStage',1),'zone':u.get('HomeZone',''),'description':u.get('Description',''),
    'courage':u['Cost'],'bodies':u.get('BodiesPerDeployment',1),'deploy_interval':u.get('DeployCooldown',0),
    'levels':[resolved(u,l) for l in range(1,21)]} for u in units],
 'equipment':equipment,'spells_and_tactical_items':spells,
 'campaign':[{'stage':s['StageNumber'],'enemy_level':s['EnemyLevel'],'new_enemy':s['NewEnemyId']} for s in stages]}
(ROOT/'docs/design/playable-content-catalog.json').write_text(json.dumps(catalog,indent=2,ensure_ascii=False)+'\n')
lines=['# Crownroad playable content and stats','',
 'Implemented prototype · Generated from the playable data. Rebuild with `python3 scripts/tools/revamp_content.py`.','',
 '32 core recruits plus 5 retained legacy ownership IDs; 59 ordinary hostile types, 14 bosses and the optional Ash Pilgrim; 100 equipment relics; 30 spells and 12 tactical cards. New art uses existing placeholder models and pictures.','',
 'Stats below are bare values before equipment and temporary effects. HP and attack show level 1 → level 20. HP gains 1.5% of base per level, attack and structure damage gain 1%; other stats stay fixed. Crit damage is bonus damage: +50% means a critical hit deals 150%. Militia and Sapper stats are per body.','',
 'Every level record is available in [the playable catalog](../design/playable-content-catalog.json). Main campaign enemies progress from level 1 to 20; ten regional debuts preserve familiar troops. Other ordinary types enter the regional endless pools.','']
for title,group in [('Core recruits',[u for u in units if u['Side']=='Player' and not u.get('LegacyRecruit')]),
 ('Retained legacy recruits',[u for u in units if u['Side']=='Player' and u.get('LegacyRecruit')]),
 ('Ordinary enemies',[u for u in units if u['Side']=='Enemy' and not u.get('IsBoss') and not u.get('NonCombatant')]),
 ('Bosses',[u for u in units if u.get('IsBoss')]),('Optional encounter',[u for u in units if u.get('NonCombatant')])]:
    lines+=['## '+title,'','| Name | HP 1 → 20 | Attack 1 → 20 | Armor | Phys % | Magic % | Crit % / bonus % | Speed / reach / interval | Deployment cost / bodies |','| --- | --- | --- | --- | --- | --- | --- | --- | --- |']
    for u in group:
        a,z=resolved(u,1),resolved(u,20)
        lines.append(f"| {u['DisplayName']} | {a['health']} → {z['health']} | {a['attack']:g} → {z['attack']:g} | {a['armor']:g} | {a['physical_resistance']:g} | {a['magic_resistance']:g} | {a['critical_chance']:g} / +{a['critical_damage_bonus']:g} | {a['speed']:g} / {a['range']:g} / {a['attack_interval']:g}s | {u['Cost']} / {u.get('BodiesPerDeployment',1)} |")
    lines+=['']
lines+=['## Equipment relics','','All permanent sources share the same total bonus caps: health/damage/speed/reach/structure +10%, armor +10, physical resistance +0.5 pp, magic resistance +8 pp, critical chance +5 pp, critical bonus +10 pp, attack interval reduction 5%.','',
 '| Relic | Rarity | Stat bonuses | Effect |','| --- | --- | --- | --- |']
for e in equipment:
    b=next(p['bonuses'] for p in policy['relics'] if p['id']==e['Id'])
    lines.append(f"| {e['DisplayName']} | {e['Rarity']} | {bonus_summary(b)} | {e['Description'].replace('|','/')} |")
lines+=['','## Spells and tactical gear','','These share five loadout slots. Levels 1–3 give flat damage, healing and durability +5% per upgrade; ratios, control durations, movement distance and resource income stay fixed.','',
 '| Card | Kind | Stage | Mana | Cooldown | Duration | Power | Effect |','| --- | --- | --- | --- | --- | --- | --- | --- |']
for s in spells:
    lines.append(f"| {s['DisplayName']} | {s['CardKind']} | {s['UnlockStage']} | {s['ManaCost']} | {s['Cooldown']:g}s | {s.get('Duration',0):g}s | {s['Power']:g} | {s['Description'].replace('|','/')} |")
(ROOT/'docs/catalogs/CURRENT_CONTENT_STATS.md').write_text('\n'.join(lines)+'\n')
print(f'Playable data: {len(units)} units including retained ownership IDs; 100 relics; 30 spells + 12 tactical cards; 100 rebuilt stages.')
