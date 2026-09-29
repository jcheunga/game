"""Mechanical before/after contact sheets and finish-publish validation."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT/'artifacts/blender/polish-v2'
parser = argparse.ArgumentParser()
parser.add_argument('--baseline', type=Path, required=True)
parser.add_argument('--verify', action='store_true')
args = parser.parse_args()
baseline = args.baseline.resolve()
if not (baseline/'assets').is_dir(): raise ValueError('Expected a saved assets baseline')


def font(size, bold=False):
    name = 'Arial Bold.ttf' if bold else 'Arial.ttf'
    path = Path('/System/Library/Fonts/Supplemental')/name
    return ImageFont.truetype(str(path), size) if path.exists() else ImageFont.load_default(size=size)


sheet = Image.new('RGB', (1600, 1460), '#132321')
draw = ImageDraw.Draw(sheet)
draw.text((55, 35), 'CROWNROAD  /  MATERIALS & LIGHT', font=font(32, True), fill='#e4ce99')
draw.text((55, 84), 'Same models and cameras. Refined surfaces, highlights, and environmental depth.', font=font(19), fill='#a7b9b1')


def tile(path, bounds):
    with Image.open(path) as source:
        image = source.convert('RGBA')
    x,y,w,h = bounds
    image.thumbnail((w,h), Image.Resampling.LANCZOS)
    sheet.paste(image, (x+(w-image.width)//2,y+(h-image.height)//2), image)


def label(text,x,y): draw.text((x,y),text,font=font(17,True),fill='#d6c393')


for column, (word,path) in enumerate([
        ('BEFORE', baseline/'assets/structures/war_wagon.png'),
        ('REFINED', REVIEW/'caravan/lantern_caravan/render.png')]):
    x = 55 + column*775
    label(word+'  /  LANTERN CARAVAN',x,135)
    tile(path,(x,170,720,430))
draw.line((55,620,1545,620),fill='#394a41')
for index,(ident,title) in enumerate([('player_brawler','ARMOR / PAINT'),('player_shooter','CLOTH / LEATHER'),('enemy_boss','BONE / BRASS')]):
    x = 55 + index*515
    label(title,x,642)
    for j,(word,path) in enumerate([
            ('Before',baseline/'assets/ui/portraits/codex'/f'{ident}.png'),
            ('Refined',REVIEW/'units'/ident/'portrait.png')]):
        tile(path,(x+j*245,685,230,270))
        draw.text((x+j*245+75,960),word,font=font(15),fill='#a7b9b1')
draw.line((55,998,1545,998),fill='#394a41')
for column,(word,path) in enumerate([
        ('BEFORE',baseline/'assets/backgrounds/urban.png'),
        ('REFINED',REVIEW/'battlefields/urban/render.png')]):
    x=55+column*775
    label(word+'  /  BATTLEFIELD',x,1018)
    tile(path,(x,1058,720,365))
sheet.save(REVIEW/'before-after.jpg',quality=95)
print('COMPARISON '+str(REVIEW/'before-after.jpg'))

if args.verify:
    expected = [ROOT/'art/blender/lantern_caravan.blend']
    for category in ('units','caravans','gatehouse','mounts','battlefields','maps','menus','items'):
        expected += sorted((ROOT/'art/blender'/category).glob('*.blend'))
    failures=[]
    counts=Counter()
    changed=[]
    for source in expected:
        category = 'caravan' if source.name == 'lantern_caravan.blend' else source.parent.name
        record_path=REVIEW/category/source.stem/'published.json'
        if not record_path.exists():
            failures.append('Missing finish record: '+str(source.relative_to(ROOT)))
            continue
        record=json.loads(record_path.read_text())
        if record['source_hash'] != hashlib.sha256(source.read_bytes()).hexdigest():
            failures.append('Source differs from published record: '+str(source.relative_to(ROOT)))
        if not record.get('geometry_preserved'): failures.append('No geometry-preservation check: '+source.stem)
        if record['samples'] < 64: failures.append('Insufficient finish samples: '+source.stem)
        if category == 'units' and len(record['outputs']) != 29: failures.append('Incomplete animation render: '+source.stem)
        counts[category]+=1
        for rel in record['outputs']:
            path=ROOT/rel
            if not path.exists():
                failures.append('Missing output: '+rel)
                continue
            # Units are compared through shipped portraits/atlases below.
            old=baseline/rel
            if old.exists():
                with Image.open(old) as old_image, Image.open(path) as new_image:
                    if old_image.size != new_image.size: failures.append('Changed dimensions: '+rel)
                    elif not ImageChops.difference(old_image.convert('RGB'),new_image.convert('RGB')).getbbox():
                        failures.append('Unchanged render: '+rel)
                    else: changed.append(rel)
    unit_metadata=list((baseline/'assets/units').glob('*.json'))
    for old_path in unit_metadata:
        new_path=ROOT/'assets/units'/old_path.name
        before=json.loads(old_path.read_text())
        after=json.loads(new_path.read_text())
        for key in ('animations','anchorY','drawScale','frameWidth','frameHeight'):
            if before.get(key) != after.get(key): failures.append(f'Changed gameplay art contract: {old_path.stem}/{key}')
    report={'finished_sources':sum(counts.values()),'categories':dict(counts),
            'changed_nonunit_renders':len(changed),'unit_contracts_checked':len(unit_metadata),
            'baseline':str(baseline),'failures':failures}
    (REVIEW/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))
    if failures: raise SystemExit(1)
