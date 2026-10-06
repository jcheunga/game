"""Shared musical language: the Crownroad theme, the Rotbound motif, and arranging helpers."""
from ak import theory as T

# ---------------------------------------------------------------- themes
# "The Crownroad" - the caravan's theme. D Dorian/minor, rising and noble. 8 bars of 4/4.
CROWN_A = ("D5:1.5 E5:.5 F5:1 G5:1 A5:2 G5:1 F5:.5 E5:.5 F5:1.5 E5:.5 D5:1 C5:1 D5:3 A4:1 "
           "D5:1.5 E5:.5 F5:1 A5:1 C6:2 Bb5:1 A5:.5 G5:.5 A5:1.5 G5:.5 F5:1 E5:1 D5:4")
CROWN_A_CHORDS = [("Dm", 4), ("F", 4), ("Bb", 2), ("C", 2), ("Dm", 4), ("Dm", 4), ("F", 2), ("Gm", 2), ("A", 4), ("Dm", 4)]
# Second phrase, opening into the relative major and turning back through A.
CROWN_B = ("F5:1 G5:1 A5:2 G5:1 F5:.5 G5:.5 A5:1 C6:1 D6:2 C6:1 A5:1 G5:4 "
           "F5:1 G5:1 A5:2 Bb5:1 A5:1 G5:1 F5:1 E5:1.5 F5:.5 G5:1 A5:1 A5:4")
CROWN_B_CHORDS = [("F", 4), ("C", 4), ("Dm", 4), ("C", 4), ("F", 4), ("Bb", 4), ("C", 4), ("A", 4)]
# The head motif, used as a call in battle music (rising D-E-F-G-A).
CROWN_HEAD = "D5:1.5 E5:.5 F5:1 G5:1 A5:4"

# "The Rotbound" - the host's motif: a half-step sigh and a tritone fall.
ROT_MOTIF = "D3:1 Eb3:1 D3:1 Ab2:1"


def transpose_line(text, semitones):
    out = []
    for token in text.split():
        head, *rest = token.split(":")
        if head not in ("r", "-") and not head.startswith("["):
            head = midi_name(T.note(head) + semitones)
        out.append(":".join([head] + rest))
    return " ".join(out)


NAMES = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"]


def midi_name(m):
    return f"{NAMES[m % 12]}{m // 12 - 1}"


def transpose_chords(prog, semitones):
    out = []
    for sym, beats in prog:
        main, _, bass = sym.partition("/")
        root_len = 2 if len(main) > 1 and main[1] in "#b" else 1
        root = (T.pc(main[:root_len]) + semitones) % 12
        new = NAMES[root] + main[root_len:]
        if bass:
            new += "/" + NAMES[(T.pc(bass) + semitones) % 12]
        out.append((new, beats))
    return out


def expand(prog):
    """[(chord, beats)] -> [(start_beat, chord, beats)]."""
    out, beat = [], 0.0
    for sym, beats in prog:
        out.append((beat, sym, beats))
        beat += beats
    return out


def prog_beats(prog):
    return sum(b for _, b in prog)


# ---------------------------------------------------------------- arranging helpers
def pad(song, part, start, prog, low, high, count=3, vel=0.6, legato=1.0, rearticulate=None):
    """Sustained voice-led chords. rearticulate: beats between re-strikes (None = once per chord)."""
    prev = None
    for beat, sym, beats in expand(prog):
        v = T.voice(sym, low, high, count, prev)
        prev = v
        if rearticulate:
            k = 0.0
            while k < beats - 1e-6:
                d = min(rearticulate, beats - k)
                song.n(part, start + beat + k, v, d, vel, legato)
                k += rearticulate
        else:
            song.n(part, start + beat, v, beats, vel, legato)
    return start + prog_beats(prog)


def _plucked(song, part):
    return not song.parts[part].instrument.sustain


def bass(song, part, start, prog, rhythm=((0, 1.0),), low=31, high=45, vel=0.75, fifth_on=(), octave_on=(), legato=0.95,
         damp=0.18):
    """Bass line: rhythm is [(offset_in_chord_beats, length)], repeated across each chord's span.

    fifth_on / octave_on: offsets where the fifth / upper octave replaces the root.
    damp applies to plucked instruments only (sustained ones keep their natural release)."""
    damp = damp if _plucked(song, part) else None
    for beat, sym, beats in expand(prog):
        root = T.bass_note(sym, low, high)
        span = max(o + l for o, l in rhythm)
        reps = max(1, int(round(beats / span)))
        for r in range(reps):
            for off, length in rhythm:
                pos = r * span + off
                if pos >= beats - 1e-6:
                    continue
                m = root
                if off in fifth_on:
                    m = root + 7 if root + 7 <= high + 5 else root - 5
                if off in octave_on:
                    m = root + 12
                song.n(part, start + beat + pos, m, min(length, beats - pos), vel, legato, damp=damp)
    return start + prog_beats(prog)


def arpeggio(song, part, start, prog, order=(0, 1, 2, 3, 2, 1), step=0.5, low=50, high=76, count=4, vel=0.6,
             ring=2.0, accent_first=0.1, damp=0.35):
    """Broken chords cycling through voice-led chord tones. Notes ring up to `ring` beats but are damped
    at the next chord change (like a harpist muting the old harmony)."""
    damp = damp if _plucked(song, part) else None
    prev = None
    for beat, sym, beats in expand(prog):
        tones = T.voice(sym, low, high, count, prev)
        prev = tones
        k = 0
        pos = 0.0
        while pos < beats - 1e-6:
            idx = order[k % len(order)] % len(tones)
            v = vel + (accent_first if k == 0 else 0)
            song.n(part, start + beat + pos, tones[idx], min(ring, beats - pos), min(1, v), damp=damp)
            pos += step
            k += 1
    return start + prog_beats(prog)


def chord_root(sym, low, high, prefer_fifth=False):
    """Root (or fifth) of a chord inside [low, high] - for timpani and drones."""
    root, intervals, _ = T.chord(sym)
    targets = [(root + 7) % 12, root] if prefer_fifth else [root, (root + 7) % 12]
    for pc in targets:
        for m in range(low, high + 1):
            if m % 12 == pc:
                return m
    return low


def chord_at(prog, beat):
    for b, sym, beats in expand(prog):
        if b <= beat < b + beats:
            return sym
    return prog[-1][0]


def ostinato(song, part, start, prog, figure, step=0.5, base_low=38, base_high=50, vel=0.7, accents=(0,),
             length=None, accent=0.15, damp=0.15):
    """Rhythmic figure built from intervals above each chord's root, e.g. [0,0,12,0,7,0,12,7].

    Short (plucked/spiccato) notes are damped so their tails never smear across a chord change."""
    damp = damp if _plucked(song, part) else None
    for beat, sym, beats in expand(prog):
        root = T.bass_note(sym, base_low, base_high)
        intervals = T.chord(sym)[1]
        third = intervals[1] if len(intervals) > 1 and intervals[1] in (3, 4) else 4
        k = 0
        pos = 0.0
        while pos < beats - 1e-6:
            interval = figure[k % len(figure)]
            if interval is not None:
                if interval == "3":
                    interval = third
                elif interval == "10":
                    interval = third + 12 if third == 3 else 16
                v = vel + (accent if (k % len(figure)) in accents else 0)
                song.n(part, start + beat + pos, root + int(interval), length or step * 0.9, min(1, v), damp=damp)
            pos += step
            k += 1
    return start + prog_beats(prog)


def melody(song, part, start, text, vel=0.8, transpose=0, legato=0.98):
    return song.line(part, start, text, vel=vel, transpose=transpose, legato=legato)


def roll(song, part, beat, beats, start_vel=0.3, end_vel=0.9, rate=8, pitch=60):
    """Single-stroke roll/crescendo (pitch for tuned drums such as timpani)."""
    n = int(beats * rate)
    for k in range(n):
        song.hit(part, beat + k / rate, start_vel + (end_vel - start_vel) * k / max(1, n - 1), pitch=pitch)
