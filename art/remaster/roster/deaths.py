"""How every unit dies. One entry per unit id; see rk/death.py for the motions and solver.

motion/params  choreography from rk.death.MOTIONS
drops          kit that comes loose: name -> release (0-1), toss (m/s), spin (deg/s or [(deg, axis)]).
               Names are registered kit ('weapon', 'offhand', 'shield', 'nocked', 'weapon2') or
               '@Prefix' for named parts (crowns, helmets); each=True gives every part its own body.
plant          registered kit that stays standing where it was when the unit fell
scatter        bones that break away and tumble (rigid skeletons)
hide           object-name prefix -> time (0-1) at which it vanishes (popped pustules, shattered flasks)
dim            emissive materials to put out: None = all (eyes, soul cores, lanterns, embers), [] = none
fx             runtime death-effect family (BattleDeathEffects): dust | bones | ash | gas | embers |
               water | arcane | storm | holy | alchemy | glass | debris
frames/duration  clip length; bosses get a longer, heavier clip
"""
from rk.rig import LAT, YAW, ROLL

BOSS = dict(frames=16, duration=0.08)

DEATHS = {
    # ------------------------------------------------------------ Lantern Caravan
    'player_brawler': dict(  # Swordsman: blade flies from the hand as he goes over backwards
        motion='fall_back', params=dict(side=0.4),
        drops={'weapon': dict(release=0.0, toss=(-0.9, -0.35, 0.3), spin=[(-820, LAT), (120, ROLL)], inherit=0.0)},
        fx='dust'),
    'player_shooter': dict(  # Archer: spun by the hit, bow and arrow fall away
        motion='spin_fall', params=dict(side=-0.6, turn=1.15),
        drops={'offhand': dict(release=0.05, toss=(0.6, -0.3, 0.4), spin=[(500, LAT)]),
               'nocked': dict(release=0.02, toss=(0.9, -0.1, 0.6), spin=[(-900, LAT)])},
        fx='dust'),
    'player_defender': dict(  # Shield Knight: armour carries him to his knees, then onto his face
        motion='fall_forward', params=dict(side=-0.4, step=0.06, clutch=0.5, reach=0.8),
        drops={'weapon': dict(release=0.42, toss=(0.5, -0.4, 0.2), spin=[(-400, LAT)])},
        fx='dust'),
    'player_spear': dict(  # Spearman: lurches forward, the spear clatters down ahead of him
        motion='fall_forward', params=dict(side=0.5, step=0.12, reach=1.15, head_turn=30, clutch=0.2),
        drops={'weapon': dict(release=0.3, toss=(0.8, 0.0, 0.2), spin=[(-260, LAT)], inherit=0.4)},
        fx='dust'),
    'player_ranger': dict(  # Crossbowman: sits down hard, crossbow dropping into the lap, tips over
        motion='sit_slump', params=dict(side=1.0),
        drops={'weapon': dict(release=0.18, toss=(0.35, -0.2, 0.0), spin=[(240, LAT)]),
               'nocked': dict(release=0.16, toss=(0.5, -0.2, 0.3), spin=[(600, LAT)])},
        fx='dust'),
    'player_breacher': dict(  # Halberdier: reels back two heavy steps, halberd toppling behind
        motion='stagger_back', params=dict(side=0.3, steps=0.8),
        drops={'weapon': dict(release=0.4, toss=(-0.4, 0.2, 0.2), spin=[(380, LAT)])},
        fx='dust'),
    'player_marksman': dict(  # Mage: the spell leaves the body; lifted, then a dead drop
        motion='rise_collapse', params=dict(side=1.0, lift=0.1),
        drops={'weapon': dict(release=0.55, inherit=0.15, toss=(0.3, -0.3, 0.3), spin=[(-500, LAT)])},
        fx='arcane'),
    'player_stormcaller': dict(  # Stormcaller: spun up on the last discharge, crown of sparks dies
        motion='rise_collapse', params=dict(side=-1.0, lift=0.16, arms_up=1.2, spin=1.0),
        drops={'weapon': dict(release=0.5, inherit=0.15, toss=(-0.4, 0.3, 0.6), spin=[(700, LAT), (200, YAW)])},
        fx='storm'),
    'player_necromancer': dict(  # Necromancer: kneels too long in thought, then folds aside
        motion='kneel_topple', params=dict(side=-1.0, pause=1.4, bow=1.4),
        drops={'weapon': dict(release=0.36, toss=(0.2, 0.3, 0.1), spin=[(300, LAT)])},
        fx='arcane'),
    'player_coordinator': dict(  # Battle Monk: lays down the staff, prays, and bows to the ground
        motion='pray_slump', params=dict(side=0.3),
        drops={'weapon': dict(release=0.12, toss=(0.3, -0.25, -0.2), spin=[(160, LAT)])},
        fx='holy'),
    'player_grenadier': dict(  # Alchemist: spins, the flask arcs away and shatters
        motion='spin_fall', params=dict(side=1.0, turn=-1.1),
        drops={'weapon': dict(release=0.0, toss=(-0.5, -0.4, 1.2), spin=[(900, LAT), (400, ROLL)])},
        hide={'Flask': 0.62, 'Potion': 0.62, 'Cork': 0.62, 'Liquid': 0.62}, fx='alchemy'),
    'player_mechanic': dict(  # Siege Engineer: tools scatter as he stumbles onto his face
        motion='fall_forward', params=dict(side=-0.6, step=0.08, reach=0.9, head_turn=-30),
        drops={'weapon': dict(release=0.2, toss=(0.7, -0.3, 0.4), spin=[(-600, LAT)]),
               'offhand': dict(release=0.28, toss=(0.3, 0.4, 0.3), spin=[(500, LAT), (300, YAW)])},
        fx='dust'),
    'player_banner': dict(  # Banner Knight: plants the standard as he falls; it stays standing
        motion='kneel_topple', params=dict(side=-1.0, pause=1.4, bow=1.2, arm_raise=40), plant='offhand',
        drops={'weapon': dict(release=0.45, toss=(0.3, -0.3, 0.1), spin=[(260, LAT)])},
        fx='dust'),
    'player_rogue': dict(  # Rogue: dies mid-lunge and skids, daggers skittering on
        motion='dive_skid', params=dict(side=0.5, dist=0.42),
        drops={'weapon': dict(release=0.3, toss=(0.45, -0.2, 0.2), spin=[(-1100, LAT)], inherit=0.3, friction=0.3),
               'offhand': dict(release=0.34, toss=(0.4, 0.25, 0.3), spin=[(900, LAT)], inherit=0.3, friction=0.3)},
        fx='dust'),
    'player_berserker': dict(  # Berserker: a last roar on his knees, then down on his face
        motion='last_roar', params=dict(side=0.3, roar=1.1),
        drops={'weapon': dict(release=0.16, toss=(0.3, -0.4, -0.3), spin=[(-260, LAT)], inherit=0.0)},
        fx='dust'),
    'player_lantern_guard': dict(  # Lantern Guard: falls like a door onto the tower shield; the lantern goes out
        motion='topple_rigid', params=dict(side=0.2, sway=1.0),
        drops={'weapon': dict(release=0.3, toss=(0.5, -0.4, 0.2), spin=[(-400, LAT)]),
               'shield': dict(release=0.56, toss=(0.6, 0.0, -0.4), spin=[(-200, LAT)], inherit=0.8)},
        fx='dust'),

    'player_hound': dict(  # War Hound: yelps, rolls onto its side, one last kick
        motion='hound_fall', params=dict(side=-1.0), fx='dust'),
    'player_raider': dict(  # Cavalry: the horse's forelegs buckle and it slumps onto its side
        motion='horse_collapse', params=dict(side=-1.0), fx='dust'),
    'player_raider_rider': dict(  # ...pitching the rider over its neck; lance and shield fly loose
        motion='thrown_rider', params=dict(side=0.4, dist=0.85), world_lock=True,
        drops={'weapon': dict(release=0.42, toss=(0.3, -0.2, -0.2), spin=[(-160, LAT)], inherit=0.0),
               'shield': dict(release=0.5, toss=(0.3, -0.3, 0.0), spin=[(300, ROLL)], inherit=0.0)}),
    'player_ballista': dict(  # Ballista: the arm snaps up, a wheel breaks away, the frame drops on that corner
        motion='machine_wreck', params=dict(lean=16, pitch=-9, arm=34),
        scatter={'wheel.FR': dict(release=0.4, toss=(0.6, -0.9, 0.5), spin=[(-500, ROLL)], roll=0.8)},
        drops={'nocked': dict(release=0.2, toss=(0.6, 0, 0.6), spin=[(500, LAT)])}, fx='debris'),
    'player_ballista_crew': dict(  # ...and the crewman sits down hard beside it
        motion='sit_slump', params=dict(side=1.0, step=0.6)),

    # ------------------------------------------------------------ Rotbound Host
    'enemy_walker': dict(  # Risen: the strings are cut; it drops in a heap
        motion='crumple', params=dict(side=1.0, forward=1.2),
        drops={'weapon': dict(release=0.2, toss=(0.3, -0.4, 0.2), spin=[(-400, LAT)])},
        fx='ash'),
    'enemy_runner': dict(  # Ghoul: dies mid-pounce and skids
        motion='dive_skid', params=dict(side=-0.6, dist=0.36, twitch=1.0), fx='ash'),
    'enemy_bloater': dict(  # Rot Hulk: swells, bursts, deflates
        motion='burst', params=dict(side=1.0, swell=1.4),
        drops={'@Plague pustule': dict(each=True, release=0.42, toss=(0, 0, 1.6), spread=1.2)},
        fx='gas'),
    'enemy_brute': dict(  # Grave Brute: two staggering steps back, maul dropping, knees, then a slam
        motion='stagger_back', params=dict(side=-0.4, steps=1.1, weight=1.4),
        drops={'weapon': dict(release=0.32, toss=(0.2, -0.4, 0.1), spin=[(-200, LAT)])}, fx='dust'),
    'enemy_crusher': dict(  # Bone Juggernaut: stumbles forward and crashes down
        motion='fall_forward', params=dict(side=0.6, step=0.1, reach=0.7, clutch=0.0),
        drops={'weapon': dict(release=0.2, toss=(0.4, -0.4, 0.2), spin=[(-300, LAT)])}, fx='bones'),
    'enemy_shieldwall': dict(  # Shield Wall: falls rigid onto the shield like a slammed door
        motion='topple_rigid', params=dict(side=-0.3, sway=1.4),
        drops={'weapon': dict(release=0.3, toss=(0.5, -0.3, 0.3), spin=[(-300, LAT)]),
               'shield': dict(release=0.58, toss=(0.5, 0.0, -0.5), spin=[(-220, LAT)], inherit=0.8)},
        fx='ash'),
    'enemy_mirror': dict(  # Mirror Knight: spins away as the mirror-steel shatters
        motion='spin_fall', params=dict(side=1.0, turn=1.0),
        drops={'weapon': dict(release=0.15, toss=(-0.15, -0.2, 0.2), spin=[(800, LAT)]),
               'shield': dict(release=0.3, toss=(0.1, -0.2, 0.1), spin=[(500, ROLL)])},
        fx='glass'),
    'enemy_revenant_captain': dict(  # Revenant Captain: the sword stays standing over him like a grave marker
        motion='crumple', params=dict(side=-1.0, jolt=0.8, forward=0.8), plant='weapon',
        drops={'shield': dict(release=0.4, toss=(-0.3, 0.3, 0.1), spin=[(200, ROLL)]),
               '@Crown': dict(release=0.62, toss=(-0.6, -0.5, 0.9), spin=[(500, LAT)], roll=0.5)},
        fx='ash'),
    'enemy_spitter': dict(  # Blight Caster: retches, folds and rots where it falls
        motion='crumple', params=dict(side=-1.0, jolt=1.2, forward=0.9),
        drops={'weapon': dict(release=0.25, toss=(0.3, -0.3, 0.1), spin=[(-300, LAT)])}, fx='gas'),
    'enemy_jammer': dict(  # Hexer: the hex rebounds, lifting and twisting it before it drops
        motion='rise_collapse', params=dict(side=-1.0, lift=0.08, spin=-0.6),
        drops={'weapon': dict(release=0.5, inherit=0.15, toss=(-0.3, 0.3, 0.4), spin=[(500, LAT)])}, fx='arcane'),
    'enemy_lich': dict(  # Lich: the phylactery fails; it rises, collapses into its robes, skull rolls free
        motion='rise_collapse', params=dict(side=1.0, lift=0.14),
        scatter={'head': dict(release=0.62, toss=(-0.5, -0.5, 1.0), spin=[(700, LAT), (200, YAW)], roll=0.7),
                 'hand.R': dict(release=0.66, toss=(0.3, -0.3, 0.3), spin=[(300, LAT)]),
                 'hand.L': dict(release=0.68, toss=(-0.3, 0.3, 0.3), spin=[(-300, LAT)])},
        drops={'weapon': dict(release=0.4, inherit=0.15, toss=(0.4, -0.2, 0.2), spin=[(-400, LAT)])},
        fx='bones'),
    'enemy_howler': dict(  # Dread Herald: standard topples one way, the skeleton falls apart the other
        motion='crumple', params=dict(side=-1.0, jolt=1.0),
        scatter={'head': dict(release=0.4, toss=(-0.6, 0.3, 1.2), spin=[(-700, LAT)], roll=0.5),
                 'upper_arm.L': dict(release=0.5, toss=(-0.3, 0.4, 0.3), spin=[(400, LAT)]),
                 'forearm.R': dict(release=0.55, toss=(0.3, -0.4, 0.2), spin=[(-500, LAT)])},
        drops={'weapon': dict(release=0.1, toss=(0.6, -0.2, 0.0), spin=[(-160, LAT)], inherit=0.0)},
        fx='bones'),
    'enemy_saboteur': dict(  # Sapper: spins; the lit charge drops and flares
        motion='spin_fall', params=dict(side=-1.0, turn=-1.1),
        drops={'nocked': dict(release=0.05, toss=(0.4, -0.3, 0.8), spin=[(700, LAT)])},
        fx='embers'),
    'enemy_tunneler': dict(  # Tunneler: thrown onto its back, claws curling
        motion='fall_back', params=dict(side=-1.0, stagger=0.5, sprawl=1.3, arms=0.7),
        drops={'weapon': dict(release=0.1, toss=(0.4, -0.3, 0.6), spin=[(600, LAT)])}, fx='dust'),
    'enemy_catacomb_giant': dict(  # Catacomb Giant: reels, kneels, and shatters on impact
        motion='stagger_back', params=dict(side=0.4, steps=0.9, weight=1.6),
        scatter={'head': dict(release=0.84, toss=(0.6, -0.5, 1.2), spin=[(600, LAT)], roll=0.6),
                 'forearm.R': dict(release=0.85, toss=(0.6, -0.5, 0.6), spin=[(-500, LAT)]),
                 'forearm.L': dict(release=0.86, toss=(0.4, 0.5, 0.5), spin=[(500, LAT)])},
        drops={'weapon': dict(release=0.38, toss=(0.2, -0.4, 0.0), spin=[(-160, LAT)])},
        fx='bones'),

    'enemy_boneballista': dict(  # Bone Ballista: the bone arm tears free and two wheels give way
        motion='machine_wreck', params=dict(lean=-14, pitch=-12, arm=-20),
        scatter={'arm': dict(release=0.3, toss=(0.3, -0.4, 1.2), spin=[(-600, LAT)], bounce=0.3),
                 'wheel.FL': dict(release=0.42, toss=(0.5, 0.7, 0.4), spin=[(500, ROLL)], roll=0.6),
                 'wheel.FR': dict(release=0.46, toss=(0.6, -0.8, 0.4), spin=[(-500, ROLL)], roll=0.7)},
        drops={'nocked': dict(release=0.2, toss=(0.5, 0, 0.6), spin=[(500, LAT)])}, fx='bones'),
    'enemy_plague_engine': dict(  # Plague Engine: the boiler bursts, the barrel blows off, it sags on a broken wheel
        motion='machine_wreck', params=dict(lean=18, pitch=-6, jolt=0.08, squash=1.4),
        scatter={'barrel': dict(release=0.2, toss=(-0.4, -0.3, 2.2), spin=[(-700, LAT), (200, YAW)], bounce=0.25),
                 'wheel.FR': dict(release=0.36, toss=(0.6, -0.9, 0.4), spin=[(-500, ROLL)], roll=0.8)},
        fx='gas'),
    'enemy_siegetower': dict(  # Siege Tower: front wheels shear off, it lurches forward and the ramp falls open
        motion='machine_wreck', params=dict(lean=12, pitch=-14, ramp=-95, squash=1.2, jolt=0.03),
        scatter={'wheel.FR': dict(release=0.32, toss=(0.7, -0.9, 0.4), spin=[(-500, ROLL)], roll=0.8),
                 'wheel.FL': dict(release=0.36, toss=(0.7, 0.8, 0.4), spin=[(500, ROLL)], roll=0.6)},
        fx='debris'),
    'enemy_splitter': dict(  # Bone Nest: the heart swells and bursts, the nest sags open
        motion='nest_burst', params=dict(swell=1.8), fx='bones'),

    # ------------------------------------------------------------ bosses
    'enemy_boss': dict(BOSS,  # Grave Lord: kneels, crown rolls from the bowed head, then he topples
        motion='kneel_topple', params=dict(side=-1.0, pause=1.6, bow=1.3),
        drops={'weapon': dict(release=0.3, toss=(0.5, -0.3, 0.2), spin=[(-500, LAT)]),
               '@Crown': dict(release=0.55, toss=(0.7, -0.6, 0.6), spin=[(700, LAT)], roll=0.8)},
        fx='ash'),
    'enemy_boss_docks': dict(BOSS,  # Tidecaller: the sea lets go of her; lifted, then a drop
        motion='rise_collapse', params=dict(side=1.0, lift=0.18, spin=0.5),
        drops={'weapon': dict(release=0.55, inherit=0.15, toss=(0.4, -0.3, 0.4), spin=[(-500, LAT)]),
               '@Crown': dict(release=0.68, toss=(-0.5, -0.5, 0.8), spin=[(600, LAT)], roll=0.6)},
        fx='water'),
    'enemy_boss_forge': dict(BOSS,  # Iron Warden: reels, kneels, slams down; the forge-iron cools
        motion='stagger_back', params=dict(side=0.4, steps=1.2, weight=1.6),
        drops={'weapon': dict(release=0.46, toss=(0.3, -0.3, 0.1), spin=[(-260, LAT)])},
        hide={'Smoke stack': 1.1}, fx='embers'),
    'enemy_boss_ward': dict(BOSS,  # Plague Archon: buckles in a heap as the pustules burst
        motion='crumple', params=dict(side=1.0, jolt=1.3, forward=1.0),
        drops={'weapon': dict(release=0.3, toss=(0.4, -0.4, 0.3), spin=[(-400, LAT)]),
               '@Plague pustule': dict(each=True, release=0.36, toss=(0, 0, 1.2), spread=1.0)},
        fx='gas'),
    'enemy_boss_pass': dict(BOSS,  # Thornwall Chieftain: hurled back, axe wheeling away
        motion='fall_back', params=dict(side=-0.5, stagger=1.5, sprawl=1.3, head_turn=-30),
        drops={'weapon': dict(release=0.05, toss=(-1.0, 0.4, 0.8), spin=[(-900, LAT), (200, YAW)], inherit=0.0)},
        fx='dust'),
    'enemy_boss_basilica': dict(BOSS,  # Bone Pontiff: kneels in prayer; the mitre falls and the skull rolls after it
        motion='pray_slump', params=dict(side=-0.3),
        scatter={'head': dict(release=0.84, toss=(0.6, -0.4, 0.5), spin=[(500, LAT)], roll=0.8),
                 'hand.R': dict(release=0.86, toss=(0.3, -0.3, 0.2), spin=[(300, LAT)])},
        drops={'weapon': dict(release=0.1, toss=(0.3, -0.3, -0.2), spin=[(160, LAT)]),
               '@Mitre': dict(release=0.56, toss=(0.6, -0.3, 0.3), spin=[(400, LAT)])},
        fx='holy'),
    'enemy_boss_mire': dict(BOSS,  # Mire Behemoth: lurches forward and slams into the muck
        motion='fall_forward', params=dict(side=0.5, step=0.16, reach=1.2, clutch=0.0, head_turn=-40),
        drops={'@Plague pustule': dict(each=True, release=0.68, toss=(0, 0, 1.0), spread=0.9)},
        fx='gas'),
    'enemy_boss_verge': dict(BOSS,  # Gloamwood Witch: whirled up by her own curse, then dropped
        motion='rise_collapse', params=dict(side=-1.0, lift=0.22, arms_up=1.3, spin=1.6),
        drops={'weapon': dict(release=0.45, inherit=0.15, toss=(-0.4, 0.4, 0.8), spin=[(800, LAT), (300, YAW)])},
        fx='arcane'),
    'enemy_boss_citadel': dict(BOSS,  # Dread Sovereign: falls like a toppled statue; the crown bounces away
        motion='topple_rigid', params=dict(side=0.3, sway=1.6),
        drops={'weapon': dict(release=0.5, toss=(0.6, -0.3, 0.2), spin=[(-300, LAT)]),
               '@Crown': dict(release=0.7, toss=(0.9, -0.5, 1.0), spin=[(800, LAT)], roll=0.8)},
        fx='ash'),
    'enemy_boss_reliquary': dict(BOSS,  # Reliquary Tyrant: spun round by the blow, relic-light failing
        motion='spin_fall', params=dict(side=1.0, turn=1.2),
        drops={'weapon': dict(release=0.2, toss=(-0.4, -0.4, 0.5), spin=[(600, LAT)]),
               '@Crown': dict(release=0.6, toss=(-0.6, -0.5, 0.9), spin=[(700, LAT)], roll=0.7),
               '@Halo': dict(release=0.3, toss=(0.0, 0.0, 1.2), spin=[(300, YAW)])},
        fx='holy'),
    'enemy_boss_ashen_regent': dict(BOSS,  # Ashen Regent: the embers die and he collapses into ash
        motion='crumple', params=dict(side=-1.0, jolt=0.9, forward=1.1),
        drops={'weapon': dict(release=0.24, toss=(0.4, -0.4, 0.2), spin=[(-400, LAT)]),
               '@Crown': dict(release=0.56, toss=(0.6, -0.5, 1.0), spin=[(600, LAT)], roll=0.7)},
        hide={'Crown flame': 0.5}, fx='ash'),
    'enemy_boss_tidemaster': dict(BOSS,  # Harrow Tidemaster: a last roar before the tide takes him
        motion='last_roar', params=dict(side=-0.5, roar=1.3),
        drops={'weapon': dict(release=0.24, toss=(0.3, -0.4, 0.0), spin=[(-180, LAT)]),
               '@Tricorn': dict(release=0.46, toss=(-0.5, -0.3, 1.2), spin=[(600, LAT)])},
        fx='water'),
    'enemy_boss_steppe': dict(BOSS,  # Steppe Warlord: the bone horse folds and its skull rolls free
        motion='horse_collapse', params=dict(side=-1.0, roll=80),
        scatter={'head': dict(release=0.62, toss=(0.6, -0.6, 0.6), spin=[(500, LAT)], roll=0.7)}, fx='bones'),
    'enemy_boss_steppe_rider': dict(BOSS,  # ...throwing the warlord, lance wheeling away
        motion='thrown_rider', params=dict(side=-0.4, dist=0.8), world_lock=True,
        drops={'weapon': dict(release=0.42, toss=(0.3, 0.2, -0.2), spin=[(-160, LAT)], inherit=0.0)}),
    'enemy_boss_plague_monarch': dict(BOSS,  # Plague Monarch: swells, bursts, crown flung off
        motion='burst', params=dict(side=-1.0, swell=1.5),
        drops={'weapon': dict(release=0.2, toss=(0.4, -0.3, 0.2), spin=[(-300, LAT)]),
               '@Crown': dict(release=0.44, toss=(-0.6, -0.4, 1.4), spin=[(800, LAT)], roll=0.6),
               '@Plague pustule': dict(each=True, release=0.42, toss=(0, 0, 1.8), spread=1.3)},
        fx='gas'),
}
