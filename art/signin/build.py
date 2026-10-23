"""Puts the companies' own sign-in art into assets/ui/signin (see README.md for sources, licences and rules).

Google's art and the font (needs Pillow and fontTools):

    python3 art/signin/build.py --google-zip signin-assets.zip --play-games-zip games-branding-icons.zip \
        --google-sans "GoogleSans[GRAD,opsz,wght].ttf"

Apple's art, once its licences are accepted (standard library only; reimports the project with Godot):

    python3 art/signin/build.py --apple-logo "<Sign in with Apple logo-and-text file, black>.svg" \
        --game-center "<Game Center icon>.png"

Nothing here redraws a logo. The Google "G" is cut from Google's own 44 pt iOS button at 4x around its 20 pt logo box,
still on the white fill Google requires behind it. The Play Games controller, the Apple logo file and the Game Center
icon are copied unchanged. Google Sans is cut to Latin text at Medium (500), the weight Google's spec names.
"""
import argparse
import io
import re
import shutil
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/ui/signin"
FONTS = ROOT / "assets/fonts"
# Logo-only light button, iOS, 4x: 176 px square with the 20 pt logo box at 12 pt from each edge. The G fills that
# box edge to edge, so the cut keeps 1 pt of the white fill around it (22 pt in all; SignInButton draws it so).
GOOGLE_BUTTON = "iOS/PNG @4x/Light/Theme=Light, Show text=No, Shape=Square, Platform=iOS@4x.png"
GOOGLE_LOGO_BOX = (44, 44, 132, 132)
PLAY_GAMES_CONTROLLER = "games-branding-icons/Individual_Badges_PNG/games_controller.png"
# Basic Latin, Latin-1, Latin Extended-A and general punctuation: room for the European translations.
TEXT_RANGES = [(0x20, 0x7E), (0xA0, 0x17F), (0x2010, 0x2027), (0x2030, 0x203A), (0x20AC, 0x20AC)]
# Apple's logo file is vector art for a 44 pt button; the game draws it 80 px tall, so Godot rasterises it at 4x.
APPLE_IMPORT = '[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\nsvg/scale=4.0\n'


def google_logo(archive: Path):
    from PIL import Image
    with zipfile.ZipFile(archive) as bundle:
        button = Image.open(io.BytesIO(bundle.read(GOOGLE_BUTTON))).convert("RGBA")
    logo = button.crop(GOOGLE_LOGO_BOX)
    # The cut must be the whole logo on white: every edge pixel is the button's white fill.
    edge = [logo.getpixel((x, y)) for x in range(logo.width) for y in (0, logo.height - 1)]
    edge += [logo.getpixel((x, y)) for y in range(logo.height) for x in (0, logo.width - 1)]
    assert all(min(pixel[:3]) >= 250 for pixel in edge), "the logo box cut through the G"
    logo.save(OUT / "google-g.png")


def play_games_controller(archive: Path):
    with zipfile.ZipFile(archive) as bundle:
        (OUT / "play-games-controller.png").write_bytes(bundle.read(PLAY_GAMES_CONTROLLER))


def google_sans(source: Path):
    from fontTools import subset
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer
    font = instancer.instantiateVariableFont(TTFont(source), {"wght": 500, "opsz": 17, "GRAD": 0})
    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=[code for start, end in TEXT_RANGES for code in range(start, end + 1)])
    subsetter.subset(font)
    font.save(FONTS / "GoogleSans-Medium.ttf")
    shutil.copy(source.with_name("OFL.txt"), FONTS / "GoogleSans-OFL.txt")


def apple_logo(source: Path):
    if source.suffix.lower() != ".svg":
        raise SystemExit("Use the SVG logo file: Apple allows its PNGs only in buttons exactly 44 pt tall.")
    # Logo-only art is always square; the button needs the logo-and-text file, which is not.
    box = re.search(r'viewBox="\s*[-\d.]+[\s,]+[-\d.]+[\s,]+([\d.]+)[\s,]+([\d.]+)', source.read_text(encoding="utf-8"))
    if box and abs(float(box[1]) - float(box[2])) < 0.5:
        raise SystemExit("That looks like the logo-only file (square). Use the logo-and-text file for 'Continue with Apple'.")
    shutil.copy(source, OUT / "apple-logo.svg")
    target = OUT / "apple-logo.svg.import"
    if not target.exists():
        target.write_text(APPLE_IMPORT)


def game_center(source: Path):
    if source.suffix.lower() != ".png":
        raise SystemExit("Use the Game Center icon PNG (Godot does not import PDF).")
    shutil.copy(source, OUT / "game-center.png")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--google-zip", type=Path)
    parser.add_argument("--play-games-zip", type=Path)
    parser.add_argument("--google-sans", type=Path, help="the variable TTF, with OFL.txt beside it")
    parser.add_argument("--apple-logo", type=Path, help="Apple's black logo-and-text SVG for a 44 pt button")
    parser.add_argument("--game-center", type=Path, help="Apple's Game Center icon PNG, the largest size")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    if args.google_zip: google_logo(args.google_zip)
    if args.play_games_zip: play_games_controller(args.play_games_zip)
    if args.google_sans: google_sans(args.google_sans)
    if args.apple_logo: apple_logo(args.apple_logo)
    if args.game_center: game_center(args.game_center)
    if args.apple_logo or args.game_center:
        subprocess.run(["godot", "--headless", "--path", str(ROOT), "--import"], check=False, capture_output=True)
        print("Imported. Check it: godot --path . scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-account --account --mobile-preview")


if __name__ == "__main__":
    main()
