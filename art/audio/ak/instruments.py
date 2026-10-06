"""Instrument registry: every recorded instrument the score and sound effects draw on (all CC0)."""
from .sampler import Instrument

V = "VSCO-2-CE/"
C = "VCSL/"
AERO = C + "Aerophones/Edge-blown Aerophones/"
IDIO = C + "Idiophones/Struck Idiophones/"
MEMB = C + "Membranophones/Struck Membranophones/"

SPEC = {
    # ---- strings (VSCO-2 sections)
    "violins": dict(folders=V + "Strings/Violin Section/susVib", release=0.35),
    "violas": dict(folders=V + "Strings/Viola Section/susvib", release=0.35),
    "cellos": dict(folders=V + "Strings/Cello Section/susvib", release=0.4),
    "basses": dict(folders=V + "Strings/Solo Contrabass/SusNV", release=0.4, gain_db=2),
    "solo_violin": dict(folders=V + "Strings/Solo Violin/Arco Vib", release=0.3),
    "violins_trem": dict(folders=V + "Strings/Violin Section/Trem", release=0.3),
    "violas_trem": dict(folders=V + "Strings/Viola Section/trem", release=0.3),
    "cellos_trem": dict(folders=V + "Strings/Cello Section/trem", release=0.35),
    "violins_spic": dict(folders=V + "Strings/Violin Section/Spic", sustain=False, max_ring=0.35),
    "violas_spic": dict(folders=V + "Strings/Viola Section/spic", sustain=False, max_ring=0.35),
    "cellos_spic": dict(folders=V + "Strings/Cello Section/spic", sustain=False, max_ring=0.4),
    "violins_pizz": dict(folders=V + "Strings/Violin Section/Pizz", sustain=False, max_ring=0.9),
    "violas_pizz": dict(folders=V + "Strings/Viola Section/pizz", sustain=False, max_ring=0.9),
    "cellos_pizz": dict(folders=V + "Strings/Cello Section/pizzT", sustain=False, max_ring=1.2),
    "basses_pizz": dict(folders=V + "Strings/Solo Contrabass/Pizz", sustain=False, max_ring=1.4),
    "harp": dict(folders=V + "Strings/Harp", sustain=False, max_ring=3.5),
    "folk_harp": dict(folders=C + "Chordophones/Composite Chordophones/Folk Harp", sustain=False, max_ring=3.0),
    "strumstick": dict(folders=C + "Chordophones/Composite Chordophones/Strumstick/Finger", sustain=False, max_ring=2.2),
    "psaltery_bowed": dict(folders=C + "Chordophones/Zithers/Psaltery, Bowed and Plucked/LongBow", release=0.5),
    "psaltery_pluck": dict(folders=C + "Chordophones/Zithers/Psaltery, Bowed and Plucked/Pluck", sustain=False, max_ring=2.5),
    "psaltery_spic": dict(folders=C + "Chordophones/Zithers/Psaltery, Bowed and Plucked/Spiccato", sustain=False, max_ring=0.8),
    # ---- winds
    "flute": dict(folders=V + "Woodwinds/Flute/susvib", release=0.25),
    "flute_stac": dict(folders=V + "Woodwinds/Flute/stac", sustain=False, max_ring=0.4),
    "piccolo": dict(folders=V + "Woodwinds/Piccolo/Sus", release=0.2),
    "oboe": dict(folders=V + "Woodwinds/Oboe/Vib", release=0.22),
    "bassoon": dict(folders=V + "Woodwinds/Bassoon/sus", release=0.25),
    "recorder_soprano": dict(folders=AERO + "Baroque Soprano Recorder/Sustain", release=0.15),
    "recorder_alto": dict(folders=AERO + "Baroque Alto Recorder/SusVib", release=0.15),
    "recorder_alto_plain": dict(folders=AERO + "Baroque Alto Recorder/Sustain", release=0.15),
    "recorder_tenor": dict(folders=AERO + "Baroque Tenor Recorder/SusVib", release=0.18),
    "recorder_bass": dict(folders=AERO + "Baroque Bass Recorder/SusVib", release=0.2),
    "recorder_alto_stac": dict(folders=AERO + "Baroque Alto Recorder/Staccato", sustain=False, max_ring=0.3),
    "recorder_soprano_stac": dict(folders=AERO + "Baroque Soprano Recorder/Staccato", sustain=False, max_ring=0.3),
    "ocarina": dict(folders=AERO + "Ocarina, Typical/Sustains/SusVib", release=0.2),
    "organ": dict(folders=AERO + "Renaissance Organ/8'", release=0.35),
    "organ_full": dict(folders=AERO + "Renaissance Organ/Full", release=0.4),
    "organ_4": dict(folders=AERO + "Renaissance Organ/4'", release=0.3),
    # ---- brass
    "horn": dict(folders=V + "Brass/F Horn/sus", release=0.3),
    "horn_stac": dict(folders=V + "Brass/F Horn/stac", sustain=False, max_ring=0.6),
    "trumpet": dict(folders=V + "Brass/Trumpet/sus", release=0.22),
    "trumpet_stac": dict(folders=V + "Brass/Trumpet/stac", sustain=False, max_ring=0.5),
    "trombone": dict(folders=V + "Brass/Tenor Trombone/sus", release=0.3),
    "tuba": dict(folders=V + "Brass/Tuba/sus", release=0.3),
    # ---- tuned percussion
    "tubular_bells": dict(folders=IDIO + "Tubular Bells 1", sustain=False, max_ring=6.0, tune=False, pitch_hint=12),
    "hand_chimes": dict(folders=IDIO + "Hand Chimes", sustain=False, max_ring=4.0),
    "glockenspiel": dict(folders=IDIO + "Glockenspiel", sustain=False, max_ring=2.5),
    "wine_glass": dict(folders=C + "Idiophones/Friction Idiophones/Wine Glasses/Sustains", release=0.8),
    "bell_tree_note": dict(folders=IDIO + "Bell Tree/Individual", sustain=False, max_ring=2.0, tune=False),
    # ---- unpitched one-shots (velocity layers + round robins)
    "timpani": dict(folders=V + "Percussion/Timpani", include=r"Timpani[12]_Hit", unpitched=True, sustain=False),
    "timpani_high": dict(folders=V + "Percussion/Timpani", include=r"Timpani[34]_Hit", unpitched=True, sustain=False),
    # Measured principal-mode pitches of the five VSCO drums, so timpani can play in key.
    "timpani_tuned": dict(folders=V + "Percussion/Timpani", include=r"Timpani\d_Hit", sustain=False, tune=False,
                          pitch_map={r"Timpani1_": 41.46, r"Timpani2_": 46.85, r"Timpani3_": 49.5,
                                     r"Timpani4_": 52.4, r"Timpani5_": 54.47}),
    "timpani_roll": dict(folders=V + "Percussion/Timpani/Rolls", unpitched=True, sustain=False),
    "big_drum": dict(folders=V + "VSCO 1 Percussion/drums/other/ethnic/giant/mallet", unpitched=True, sustain=False),
    "big_drum_sticks": dict(folders=V + "VSCO 1 Percussion/drums/other/ethnic/giant/sticks", unpitched=True, sustain=False),
    "big_drum_hand": dict(folders=V + "VSCO 1 Percussion/drums/other/ethnic/giant/hand", include=r"_hit_", unpitched=True, sustain=False),
    "bass_drum": dict(folders=MEMB + "Bass Drum 2", include=r"bassdrum_hit", unpitched=True, sustain=False),
    "bass_drum_roll": dict(folders=MEMB + "Bass Drum 2", include=r"bassdrum_roll_(ff|mf|f)\.wav", unpitched=True, sustain=False),
    "frame_drum": dict(folders=MEMB + "Frame Drum", include=r"_Hit_", unpitched=True, sustain=False),
    "frame_drum_muted": dict(folders=MEMB + "Frame Drum", include=r"HitMuted", unpitched=True, sustain=False),
    "darbuka": dict(folders=MEMB + "Darbuka", include=r"Darbuka_1", unpitched=True, sustain=False),
    "darbuka_high": dict(folders=MEMB + "Darbuka", include=r"Darbuka_2", unpitched=True, sustain=False),
    "snare_rope": dict(folders=MEMB + "Snare Drum, Rope Tension/Low", include=r"_sn_|_ns_", unpitched=True, sustain=False),
    "tenor_drum": dict(folders=V + "VSCO 1 Percussion/drums/tenor/tenor_lower", unpitched=True, sustain=False),
    "tom": dict(folders=MEMB + "Tom 1/Mallet", unpitched=True, sustain=False),
    "tambourine": dict(folders=IDIO + "Tambourine 1", unpitched=True, sustain=False),
    "sleigh_bells": dict(folders=IDIO + "Sleigh Bells", unpitched=True, sustain=False),
    "shaker": dict(folders=IDIO + "Shaker, Small", unpitched=True, sustain=False),
    "finger_cymbal": dict(folders=IDIO + "Finger Cymbals", unpitched=True, sustain=False),
    "triangle": dict(folders=IDIO + "Triangles", include=r"Triangle1_Hit", unpitched=True, sustain=False),
    "crash": dict(folders=IDIO + "Clash Cymbals 1", unpitched=True, sustain=False),
    "sus_cymbal": dict(folders=IDIO + "Suspended Cymbal 1", unpitched=True, sustain=False),
    "gong": dict(folders=IDIO + "Gong 1", include=r"gong_(f|fff|mf|p|2_f|2_mp)\.wav", unpitched=True, sustain=False),
    "gong_scrape": dict(folders=IDIO + "Gong 1", include=r"scrape", unpitched=True, sustain=False),
    "anvil": dict(folders=IDIO + "Anvil", unpitched=True, sustain=False),
    "woodblock": dict(folders=IDIO + "Woodblock", unpitched=True, sustain=False),
    "claves": dict(folders=IDIO + "Claves", exclude=r"Legacy", unpitched=True, sustain=False),
    "slapstick": dict(folders=IDIO + "Slapstick", unpitched=True, sustain=False),
    "ratchet": dict(folders=IDIO + "Ratchet", unpitched=True, sustain=False),
    "brake_drum": dict(folders=IDIO + "Brake Drum", include=r"Hit", unpitched=True, sustain=False),
    "brake_drum_bowed": dict(folders=IDIO + "Brake Drum", include=r"Bowed", unpitched=True, sustain=False),
    "bell_tree": dict(folders=IDIO + "Bell Tree/Stroke", unpitched=True, sustain=False),
    "mark_tree": dict(folders=IDIO + "Mark Trees", unpitched=True, sustain=False),
    "hand_bell": dict(folders=IDIO + "Hand Bells, Nepalese", unpitched=True, sustain=False),
    "ocean_drum": dict(folders=C + "Membranophones/Other Membranophones/Ocean Drum", unpitched=True, sustain=False),
    "didgeridoo": dict(folders=C + "Aerophones/Lip Aerophones/Didgeridoo", unpitched=True, sustain=False),
    "vsco_perc": dict(folders=V + "Percussion", exclude=r"Timpani", unpitched=True, sustain=False),
}

K = "Kenney/"


def _kenney(pack, include):
    return dict(folders=f"{K}{pack}/Audio", include=include, exclude=r"Preview", unpitched=True, sustain=False)


# Kenney CC0 foley recordings (unpitched one-shots; round robins chosen at random).
KENNEY = {
    "k_step_grass": ("impact-sounds", r"footstep_grass"), "k_step_concrete": ("impact-sounds", r"footstep_concrete"),
    "k_step_wood": ("impact-sounds", r"footstep_wood"), "k_step_snow": ("impact-sounds", r"footstep_snow"),
    "k_step_carpet": ("impact-sounds", r"footstep_carpet"), "k_step_dirt": ("rpg-audio", r"footstep\d"),
    "k_punch": ("impact-sounds", r"impactPunch_medium"), "k_punch_heavy": ("impact-sounds", r"impactPunch_heavy"),
    "k_metal_light": ("impact-sounds", r"impactMetal_light"), "k_metal": ("impact-sounds", r"impactMetal_medium"),
    "k_metal_heavy": ("impact-sounds", r"impactMetal_heavy"), "k_plate_light": ("impact-sounds", r"impactPlate_light"),
    "k_plate": ("impact-sounds", r"impactPlate_medium"), "k_plate_heavy": ("impact-sounds", r"impactPlate_heavy"),
    "k_glass_light": ("impact-sounds", r"impactGlass_light"), "k_glass": ("impact-sounds", r"impactGlass_medium"),
    "k_glass_heavy": ("impact-sounds", r"impactGlass_heavy"), "k_wood_light": ("impact-sounds", r"impactWood_light"),
    "k_wood": ("impact-sounds", r"impactWood_medium"), "k_wood_heavy": ("impact-sounds", r"impactWood_heavy"),
    "k_plank": ("impact-sounds", r"impactPlank"), "k_mining": ("impact-sounds", r"impactMining"),
    "k_soft": ("impact-sounds", r"impactSoft_medium"), "k_soft_heavy": ("impact-sounds", r"impactSoft_heavy"),
    "k_tin": ("impact-sounds", r"impactTin"), "k_bell": ("impact-sounds", r"impactBell"),
    "k_generic": ("impact-sounds", r"impactGeneric"),
    "k_book_open": ("rpg-audio", r"bookOpen"), "k_book_close": ("rpg-audio", r"bookClose"),
    "k_book_flip": ("rpg-audio", r"bookFlip"), "k_book_place": ("rpg-audio", r"bookPlace"),
    "k_cloth": ("rpg-audio", r"cloth\d"), "k_cloth_belt": ("rpg-audio", r"clothBelt"), "k_belt": ("rpg-audio", r"beltHandle"),
    "k_leather": ("rpg-audio", r"(dropLeather|handleSmallLeather)"),
    "k_creak": ("rpg-audio", r"creak"), "k_door_open": ("rpg-audio", r"doorOpen"), "k_door_close": ("rpg-audio", r"doorClose"),
    "k_draw_knife": ("rpg-audio", r"drawKnife"), "k_knife_slice": ("rpg-audio", r"knifeSlice"), "k_chop": ("rpg-audio", r"chop"),
    "k_coins": ("rpg-audio", r"handleCoins"), "k_metal_click": ("rpg-audio", r"metalClick"),
    "k_metal_latch": ("rpg-audio", r"metalLatch"), "k_metal_pot": ("rpg-audio", r"metalPot"),
    "k_chips_collide": ("casino-audio", r"chips-collide"), "k_chips_handle": ("casino-audio", r"chips-handle"),
    "k_chips_stack": ("casino-audio", r"chips-stack"), "k_chip_lay": ("casino-audio", r"chip-lay"),
    "k_card_slide": ("casino-audio", r"card-slide"), "k_card_place": ("casino-audio", r"card-place"),
    "k_card_shove": ("casino-audio", r"card-shove"), "k_card_fan": ("casino-audio", r"card-fan"),
    "k_cards_pack": ("casino-audio", r"cards-pack"), "k_card_shuffle": ("casino-audio", r"card-shuffle"),
    "k_dice_shake": ("casino-audio", r"dice-shake"), "k_dice_throw": ("casino-audio", r"(dice|die)-throw"),
    "k_dice_grab": ("casino-audio", r"dice-grab"),
    "k_ui_click": ("ui-audio", r"click\d"), "k_ui_switch": ("ui-audio", r"switch\d"), "k_ui_rollover": ("ui-audio", r"rollover"),
    "k_if_select": ("interface-sounds", r"select"), "k_if_tick": ("interface-sounds", r"tick"),
    "k_if_toggle": ("interface-sounds", r"toggle"), "k_if_click": ("interface-sounds", r"click"),
    "k_if_scroll": ("interface-sounds", r"scroll"), "k_if_drop": ("interface-sounds", r"drop"),
    "k_if_pluck": ("interface-sounds", r"pluck"), "k_if_scratch": ("interface-sounds", r"scratch"),
}
for _name, (_pack, _inc) in KENNEY.items():
    SPEC[_name] = _kenney(_pack, _inc)

_CACHE = {}


def get(name):
    if name not in _CACHE:
        spec = dict(SPEC[name])
        folders = spec.pop("folders")
        _CACHE[name] = Instrument(name, folders, **spec)
    return _CACHE[name]


def custom(name, folders, **kwargs):
    """Ad-hoc instrument (e.g. a single named file group) cached by name."""
    if name not in _CACHE:
        _CACHE[name] = Instrument(name, folders, **kwargs)
    return _CACHE[name]
