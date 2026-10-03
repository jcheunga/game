"""Decode and validate the current visual catalog and its editable sources."""
import json
from pathlib import Path
import sys
from PIL import Image
from pack_assets import ROOT,ART,REVIEW,UNITS,SPELLS,RELICS,STAGES,catalog_array,audit

failures=[]
checked=[]

def check(path,size=None,transparent=False,opaque=False,margin=False):
    if not path.exists():
        failures.append(f'Missing: {path.relative_to(ROOT)}');return
    try:
        with Image.open(path) as source:
            source.load()
            image=source.convert('RGBA')
        if size and image.size!=size:failures.append(f'Wrong size: {path.relative_to(ROOT)} {image.size} != {size}')
        alpha=image.getchannel('A')
        bounds=alpha.point(lambda x:255 if x>8 else 0).getbbox()
        if not bounds:failures.append(f'Empty: {path.relative_to(ROOT)}')
        if opaque and alpha.getextrema()!=(255,255):failures.append(f'Background has transparency: {path.relative_to(ROOT)}')
        if transparent and alpha.getextrema()[0]!=0:failures.append(f'Missing transparent background: {path.relative_to(ROOT)}')
        if margin and bounds and (bounds[0]==0 or bounds[1]==0 or bounds[2]==image.width or bounds[3]==image.height):
            failures.append(f'Clipped silhouette: {path.relative_to(ROOT)}')
        checked.append(str(path.relative_to(ROOT)))
    except Exception as exc:failures.append(f'Invalid image: {path.relative_to(ROOT)}: {exc}')

report=audit()
for group,data in report.items():
    if isinstance(data,dict):failures.extend('Missing: '+p for p in data['missing'])

for unit in UNITS:
    ident=unit['Id'];path=ROOT/'assets/units'/f'{ident}.png'
    meta=json.loads(path.with_suffix('.json').read_text())
    frame_count=sum(v['count'] for v in meta['animations'].values())
    check(path,(1536,240*((frame_count+7)//8)),transparent=True)
    for frame in range(frame_count):check(REVIEW/'units'/ident/f'{frame:03}.png',(256,320),True,margin=True)
    check(ROOT/'assets/ui/icons/units'/f'{ident}.png',(256,256),True)
    check(ROOT/'assets/ui/portraits/codex'/f'{ident}.png',(512,512),True)
    if path.with_suffix('.json').exists():
        meta=json.loads(path.with_suffix('.json').read_text())
        if frame_count not in (28,32):failures.append('Wrong clip count: '+ident)
        if 'motion' in meta and meta['animations']['attack'].get('contactFrame') != 4:failures.append('Wrong contact frame: '+ident)
        if not .5<meta['anchorY']<1:failures.append('Invalid ground anchor: '+ident)
        if not .1<meta['healthBarY']<1:failures.append('Invalid health bar height: '+ident)

for key in ('structures','caravan_skins','weapon_mounts','particles','spell_icons','relic_icons','reward_icons','meta_icons'):
    # Gather catalog paths explicitly, avoiding unrelated pre-existing assets.
    paths={
        'structures':[ROOT/'assets/structures'/f'{x}.png' for x in catalog_array('StructureIds')],
        'caravan_skins':list((ROOT/'assets/structures').glob('war_wagon_skin_*.png')),
        'weapon_mounts':[ROOT/'assets/structures'/f'mount_{x}.png' for x in ('arrows','ballista','firepot','frost','hex')],
        'particles':[ROOT/'assets/particles'/f'{x}.png' for x in catalog_array('ParticleTextureIds')],
        'spell_icons':[ROOT/'assets/ui/icons/spells'/f'{x["Id"]}.png' for x in SPELLS],
        'relic_icons':[ROOT/'assets/ui/icons/relics'/f'{x["Id"]}.png' for x in RELICS],
        'reward_icons':[ROOT/'assets/ui/icons/rewards'/f'{x}.png' for x in catalog_array('RewardIconIds')],
        'meta_icons':[ROOT/'assets/ui/icons/meta'/f'{x}.png' for x in catalog_array('MetaIconIds')],
    }[key]
    for path in paths:check(path,transparent=True,margin=True)

source_paths=[ART/'lantern_caravan.blend',ART/'gatehouse/gatehouse.blend']
source_paths += [ART/'units'/f'{u["Id"]}.blend' for u in UNITS]
source_paths += [ART/'caravans'/f'war_wagon_skin_{i}.blend' for i in ('iron','royal','bone','flame','shadow','guild','legendary')]
source_paths += [ART/'mounts'/f'mount_{i}.blend' for i in ('arrows','ballista','firepot','frost','hex')]
source_paths += [ART/'items'/f'{x["Id"]}.blend' for x in SPELLS+RELICS]
source_paths += [ART/'items'/f'{i}.blend' for i in catalog_array('RewardIconIds')+catalog_array('MetaIconIds')]
source_paths += [ART/'particles'/f'{i}.blend' for i in catalog_array('ParticleTextureIds')]
for category,folder,ids,size in (
    ('battlefields','backgrounds',sorted({s['TerrainId'] for s in STAGES}),(1280,720)),
):
    for ident in ids:
        check(ROOT/'assets'/folder/f'{ident}.png',size,opaque=True)
        source_paths.append(ART/category/f'{ident}.blend')
for stage in STAGES:
    check(ROOT/'assets/world/battles'/f'stage-{stage["StageNumber"]:02}.png',opaque=True)
for atlas in ('terrain-materials','medieval-scenery','utility-scenery','resource-scenery'):
    check(ROOT/'assets/world/overworld/polished-v3'/f'{atlas}.png')
for path in source_paths:
    if not path.exists() or path.stat().st_size<1000:failures.append(f'Missing/empty source: {path.relative_to(ROOT)}')

output={'images_checked':len(set(checked)),'native_sources_checked':len(set(source_paths)),'failures':failures}
(REVIEW/'image-validation.json').write_text(json.dumps(output,indent=2)+'\n')
print(json.dumps(output,indent=2))
sys.exit(1 if failures else 0)
