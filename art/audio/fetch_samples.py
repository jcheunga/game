"""Fetch the public-domain (CC0) instrument recordings the audio pipeline renders with.

Only the folders listed below are sparse-checked out, so the download stays near
1.5 GB instead of the full 6.3 GB of both libraries. Samples land in
art/audio/samples/ (gitignored, and art/ is never imported by Godot).

  VSCO-2 Community Edition  https://github.com/sgossner/VSCO-2-CE  (CC0-1.0)
  Versilian Community Sample Library  https://github.com/sgossner/VCSL  (CC0-1.0)
  Kenney audio packs (RPG, Impact, Interface, Casino, UI)  https://kenney.nl  (CC0-1.0)
"""
import io
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent / "samples"

LIBRARIES = {
    "VSCO-2-CE": [
        "Strings/Cello Section/susvib", "Strings/Cello Section/pizzT", "Strings/Cello Section/spic",
        "Strings/Cello Section/trem",
        "Strings/Viola Section/susvib", "Strings/Viola Section/pizz", "Strings/Viola Section/spic",
        "Strings/Viola Section/trem",
        "Strings/Violin Section/susVib", "Strings/Violin Section/Pizz", "Strings/Violin Section/Spic",
        "Strings/Violin Section/Trem",
        "Strings/Solo Violin/Arco Vib", "Strings/Solo Contrabass/SusNV", "Strings/Solo Contrabass/Pizz",
        "Strings/Harp",
        "Brass/F Horn/sus", "Brass/F Horn/stac", "Brass/Tenor Trombone/sus", "Brass/Trumpet/sus",
        "Brass/Trumpet/stac", "Brass/Tuba/sus",
        "Woodwinds/Flute/susvib", "Woodwinds/Flute/stac", "Woodwinds/Oboe/Vib", "Woodwinds/Bassoon/sus",
        "Woodwinds/Piccolo/Sus",
        "Percussion/Timpani",
        "VSCO 1 Percussion/drums/bass", "VSCO 1 Percussion/drums/tenor", "VSCO 1 Percussion/drums/snare",
        "VSCO 1 Percussion/drums/other",
    ],
    "VCSL": [
        "Aerophones/Edge-blown Aerophones/Baroque Alto Recorder",
        "Aerophones/Edge-blown Aerophones/Baroque Tenor Recorder",
        "Aerophones/Edge-blown Aerophones/Baroque Soprano Recorder",
        "Aerophones/Edge-blown Aerophones/Baroque Bass Recorder",
        "Aerophones/Edge-blown Aerophones/Renaissance Organ",
        "Aerophones/Edge-blown Aerophones/Ocarina, Typical",
        "Aerophones/Lip Aerophones/Didgeridoo",
        "Chordophones/Composite Chordophones/Folk Harp",
        "Chordophones/Composite Chordophones/Strumstick",
        "Chordophones/Zithers/Psaltery, Bowed and Plucked",
        "Idiophones/Friction Idiophones/Wine Glasses",
        "Idiophones/Struck Idiophones/Anvil", "Idiophones/Struck Idiophones/Hand Bells, Nepalese",
        "Idiophones/Struck Idiophones/Tubular Bells 1", "Idiophones/Struck Idiophones/Hand Chimes",
        "Idiophones/Struck Idiophones/Glockenspiel", "Idiophones/Struck Idiophones/Finger Cymbals",
        "Idiophones/Struck Idiophones/Sleigh Bells", "Idiophones/Struck Idiophones/Tambourine 1",
        "Idiophones/Struck Idiophones/Woodblock", "Idiophones/Struck Idiophones/Ratchet",
        "Idiophones/Struck Idiophones/Gong 1", "Idiophones/Struck Idiophones/Clash Cymbals 1",
        "Idiophones/Struck Idiophones/Suspended Cymbal 1", "Idiophones/Struck Idiophones/Mark Trees",
        "Idiophones/Struck Idiophones/Bell Tree", "Idiophones/Struck Idiophones/Claves",
        "Idiophones/Struck Idiophones/Shaker, Small", "Idiophones/Struck Idiophones/Slapstick",
        "Idiophones/Struck Idiophones/Brake Drum", "Idiophones/Struck Idiophones/Triangles",
        "Membranophones/Struck Membranophones/Frame Drum", "Membranophones/Struck Membranophones/Darbuka",
        "Membranophones/Struck Membranophones/Bass Drum 2",
        "Membranophones/Struck Membranophones/Snare Drum, Rope Tension",
        "Membranophones/Struck Membranophones/Timpani 1", "Membranophones/Struck Membranophones/Tom 1",
        "Membranophones/Other Membranophones/Ocean Drum",
    ],
}
# VSCO keeps its loose percussion hits (anvil, gong, tam-tam, crashes) at the folder root.
ROOT_FILES = {"VSCO-2-CE": ["/Percussion/*.wav"]}


# Kenney's CC0 foley packs (about 3.9 MB): footsteps, coins, cloth, pages, creaks, impacts, UI clicks, cards.
KENNEY = {
    "rpg-audio": "https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip",
    "impact-sounds": "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
    "interface-sounds": "https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip",
    "casino-audio": "https://kenney.nl/media/pages/assets/casino-audio/2472606a04-1721639069/kenney_casino-audio.zip",
    "ui-audio": "https://kenney.nl/media/pages/assets/ui-audio/490d233f68-1677590494/kenney_ui-audio.zip",
}


def fetch_kenney():
    for name, url in KENNEY.items():
        target = ROOT / "Kenney" / name
        if target.exists():
            continue
        with urllib.request.urlopen(url) as response:
            data = response.read()
        zipfile.ZipFile(io.BytesIO(data)).extractall(target)


def run(*args, cwd=None):
    subprocess.run(args, cwd=cwd, check=True)


def fetch(name, folders):
    target = ROOT / name
    if not (target / ".git").exists():
        ROOT.mkdir(parents=True, exist_ok=True)
        run("git", "clone", "--depth", "1", "--filter=blob:none", "--no-checkout",
            f"https://github.com/sgossner/{name}.git", str(target))
    patterns = [f"/{folder}/" for folder in folders] + ROOT_FILES.get(name, []) + ["/LICENSE*", "/README*"]
    run("git", "sparse-checkout", "init", "--no-cone", cwd=target)
    run("git", "sparse-checkout", "set", "--no-cone", *patterns, cwd=target)
    run("git", "checkout", cwd=target)


if __name__ == "__main__":
    for library, folders in LIBRARIES.items():
        if len(sys.argv) > 1 and library not in sys.argv[1:]:
            continue
        fetch(library, folders)
    if len(sys.argv) == 1 or "Kenney" in sys.argv[1:]:
        fetch_kenney()
    print("Samples ready in", ROOT)
