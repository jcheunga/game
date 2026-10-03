"""Mechanical packing, catalog mapping, and coverage checks for Blender renders."""
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import re
import shutil

from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'art/blender'
REVIEW=ROOT/'artifacts/blender'
UNITS=json.loads((ROOT/'data/units.json').read_text())['Units']
SPELLS=json.loads((ROOT/'data/spells.json').read_text())['Spells']
RELICS=json.loads((ROOT/'data/equipment.json').read_text())['Equipment']
STAGES=json.loads((ROOT/'data/stages.json').read_text())['Stages']
CODEX=re.findall(r'new\("([^"]+)", "([^"]+)", "([^"]+)"', (ROOT/'scripts/core/CodexCatalog.cs').read_text())
ALIASES={
    'enemy_heavy':'enemy_brute','enemy_exploder':'enemy_bloater','enemy_ranged':'enemy_spitter',
    'enemy_rusher':'enemy_saboteur','enemy_buffer':'enemy_howler','enemy_hexer':'enemy_jammer','enemy_tank':'enemy_crusher',
    'boss_grave_lord':'enemy_boss','boss_tidecaller':'enemy_boss_docks','boss_iron_warden':'enemy_boss_forge',
    'boss_plague_archon':'enemy_boss_ward','boss_thornwall':'enemy_boss_pass','boss_bone_pontiff':'enemy_boss_basilica',
    'boss_mire_behemoth':'enemy_boss_mire','boss_steppe_warlord':'enemy_boss_steppe',
    'boss_gloamwood_witch':'enemy_boss_verge','boss_dread_sovereign':'enemy_boss_citadel',
    'spell_frost':'spell_frost_burst','spell_lightning':'spell_lightning_strike','spell_barrier':'spell_barrier_ward',
    'spell_barricade':'spell_stone_barricade','spell_warcry':'spell_war_cry',
}


def copy_image(source,target,size=None):
    target.parent.mkdir(parents=True,exist_ok=True)
    if size:
        image=Image.open(source).convert('RGBA')
        image.thumbnail((size,size),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',(size,size))
        canvas.alpha_composite(image,((size-image.width)//2,(size-image.height)//2))
        canvas.save(target)
    else:
        shutil.copy2(source,target)


def pack_units():
    packed=[]
    clipped=[]
    for u in UNITS:
        ident=u['Id']; source=REVIEW/'units'/ident
        if not (source/'complete.json').exists(): continue
        meta=json.loads((source/'metadata.json').read_text())
        source_w,source_h=meta['frameWidth'],meta['frameHeight']
        # Keep full-resolution masters, ship compact atlases for small battle sprites.
        w,h=192,240
        meta['frameWidth'],meta['frameHeight']=w,h
        count=sum(a['count'] for a in meta['animations'].values())
        sheet=Image.new('RGBA',(w*8,h*math.ceil(count/8)))
        for i in range(count):
            image=Image.open(source/f'{i:03}.png').convert('RGBA')
            if image.size!=(source_w,source_h): raise ValueError(f'{ident}: bad frame size')
            bounds=image.getchannel('A').point(lambda x:255 if x>8 else 0).getbbox()
            if not bounds: raise ValueError(f'{ident}: empty frame {i}')
            if i==0:
                meta['healthBarY']=round(meta['anchorY']-bounds[1]/source_h,6)
            if bounds[0]==0 or bounds[1]==0 or bounds[2]==source_w or bounds[3]==source_h:
                clipped.append({'id':ident,'frame':i,'bbox':bounds})
            image=image.resize((w,h),Image.Resampling.LANCZOS)
            sheet.paste(image,((i%8)*w,(i//8)*h))
        target=ROOT/'assets/units'/f'{ident}.png'
        target.parent.mkdir(parents=True,exist_ok=True)
        sheet.save(target,optimize=True)
        target.with_suffix('.json').write_text(json.dumps(meta,indent=2)+'\n')
        portrait=source/'portrait.png'
        copy_image(portrait,ROOT/'assets/ui/icons/units'/f'{ident}.png',256)
        copy_image(portrait,ROOT/'assets/ui/portraits/codex'/f'{ident}.png')
        packed.append(ident)
    # Shared-class assets remain a deliberate fallback for summoned/unknown IDs.
    representatives={}
    for u in UNITS:
        if u['Id'] in packed: representatives.setdefault(u['VisualClass'],u['Id'])
    for cls,ident in representatives.items():
        for suffix in ('.png','.json'):
            shutil.copy2(ROOT/'assets/units'/f'{ident}{suffix}',ROOT/'assets/units'/f'{cls}{suffix}')
    (REVIEW/'unit-framing-report.json').write_text(json.dumps({'packed':packed,'clipped':clipped},indent=2)+'\n')
    print(json.dumps({'unit_sheets':len(packed),'class_fallbacks':len(representatives),'clipped_frames':clipped}))
    return packed


def codex():
    missing=[]
    for ident,kind,title in CODEX:
        resolved=ALIASES.get(ident,ident)
        category='units' if kind in ('unit','enemy','boss') else 'spells' if kind=='spell' else 'relics'
        icon=ROOT/'assets/ui/icons'/category/f'{resolved}.png'
        portrait=ROOT/'assets/ui/portraits/codex'/f'{resolved}.png'
        if icon.exists(): copy_image(icon,ROOT/'assets/ui/icons/codex'/f'{ident}.png')
        else: missing.append(ident)
        if not portrait.exists(): portrait=icon
        output=ROOT/'assets/ui/portraits/codex'/f'{ident}.png'
        if portrait.exists() and portrait!=output: copy_image(portrait,output,512)
    print('CODEX_MISSING '+json.dumps(missing))


def pack_previews():
    """Pack original-size native renders for the model viewer; no upscaling."""
    packed=[]
    for unit in UNITS:
        ident=unit['Id']; source=REVIEW/'units'/ident
        # Targeted rebuilds may have master frames for only a subset of units.
        # Keep the already-shipped previews for untouched characters.
        if not (source/'complete.json').exists(): continue
        meta=json.loads((source/'metadata.json').read_text())
        meta['animations']={key:value for key,value in meta['animations'].items() if key in ('idle','walk','attack')}
        count=max(clip['start']+clip['count'] for clip in meta['animations'].values())
        w,h=meta['frameWidth'],meta['frameHeight']
        sheet=Image.new('RGBA',(w*5,h*math.ceil(count/5)))
        for i in range(count):
            with Image.open(source/f'{i:03}.png') as frame:
                if frame.size!=(w,h): raise ValueError(f'{ident}: source frame size mismatch')
                sheet.paste(frame,((i%5)*w,(i//5)*h))
        target=ROOT/'assets/ui/models'/f'{ident}.png'
        target.parent.mkdir(parents=True,exist_ok=True)
        sheet.save(target,optimize=True)
        target.with_suffix('.json').write_text(json.dumps(meta,indent=2)+'\n')
        packed.append(ident)
    print(json.dumps({'native_model_previews':len(packed),'ids':packed}))


def contact_sheet(category,entries,columns=8):
    cellw,cellh=176,218
    sheet=Image.new('RGB',(columns*cellw,math.ceil(len(entries)/columns)*cellh),(18,29,31))
    draw=ImageDraw.Draw(sheet)
    for i,(title,path) in enumerate(entries):
        x,y=(i%columns)*cellw,(i//columns)*cellh
        draw.rounded_rectangle((x+5,y+5,x+cellw-5,y+cellh-5),radius=6,fill=(28,42,43),outline=(77,83,67))
        if path.exists():
            image=Image.open(path).convert('RGBA')
            image.thumbnail((cellw-18,cellh-43),Image.Resampling.LANCZOS)
            sheet.paste(image,(x+(cellw-image.width)//2,y+9),image)
        draw.text((x+10,y+cellh-30),title[:25],fill=(221,204,162))
    output=REVIEW/f'{category}-contact-sheet.jpg'
    sheet.save(output,quality=93)
    return output


def catalog_array(name):
    text=(ROOT/'scripts/core/AssetCoverageCatalog.cs').read_text()
    block=re.search(rf'{name}\s*=\s*\{{(.*?)\}};',text,re.S)
    return re.findall(r'"([^"]+)"',block.group(1))


def animation_preview():
    ids=['player_brawler','player_shooter','player_raider','enemy_runner','enemy_lich','enemy_plague_engine']
    titles={u['Id']:u['DisplayName'] for u in UNITS}
    clips=json.loads((REVIEW/'units'/ids[0]/'metadata.json').read_text())['animations']
    frames=[];durations=[]
    for state,clip in clips.items():
        for index in range(clip['start'],clip['start']+clip['count']):
            frame=Image.new('RGB',(1200,320),(18,29,31));draw=ImageDraw.Draw(frame)
            draw.text((20,18),'CROWNROAD  /  '+state.upper(),fill=(221,204,162),font_size=20)
            for column,ident in enumerate(ids):
                tile=Image.open(REVIEW/'units'/ident/f'{index:03}.png').convert('RGBA')
                tile.thumbnail((184,230),Image.Resampling.LANCZOS)
                frame.paste(tile,(column*200+(200-tile.width)//2,52),tile)
                draw.text((column*200+15,295),titles[ident],fill=(221,204,162),font_size=13)
            frames.append(frame)
            durations.append(max(70,int(clip['duration']*1000)))
    target=REVIEW/'animation-review.gif'
    frames[0].save(target,save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2)
    return target


def audit():
    groups={
        'unit_sheets':[f'assets/units/{u["Id"]}.png' for u in UNITS],
        'unit_metadata':[f'assets/units/{u["Id"]}.json' for u in UNITS],
        'unit_sources':[f'art/blender/units/{u["Id"]}.blend' for u in UNITS],
        'unit_icons':[f'assets/ui/icons/units/{u["Id"]}.png' for u in UNITS],
        'model_previews':[f'assets/ui/models/{u["Id"]}.png' for u in UNITS],
        'model_preview_metadata':[f'assets/ui/models/{u["Id"]}.json' for u in UNITS],
        'battlefields':[f'assets/backgrounds/{i}.png' for i in sorted({s['TerrainId'] for s in STAGES})],
        'structures':[f'assets/structures/{i}.png' for i in catalog_array('StructureIds')],
        'caravan_skins':['assets/structures/war_wagon.png']+[f'assets/structures/war_wagon_skin_{i}.png' for i in ('iron','royal','bone','flame','shadow','guild','legendary')],
        'weapon_mounts':[f'assets/structures/mount_{i}.png' for i in ('arrows','ballista','firepot','frost','hex')],
        'particles':[f'assets/particles/{i}.png' for i in catalog_array('ParticleTextureIds')],
        'stage_environments':[f'assets/world/battles/stage-{s["StageNumber"]:02}.png' for s in STAGES],
        'map_atlases':[f'assets/world/overworld/polished-v3/{i}.png' for i in ('terrain-materials','medieval-scenery','utility-scenery','resource-scenery')],
        'spell_icons':[f'assets/ui/icons/spells/{s["Id"]}.png' for s in SPELLS],
        'relic_icons':[f'assets/ui/icons/relics/{r["Id"]}.png' for r in RELICS],
        'reward_icons':[f'assets/ui/icons/rewards/{i}.png' for i in catalog_array('RewardIconIds')],
        'meta_icons':[f'assets/ui/icons/meta/{i}.png' for i in catalog_array('MetaIconIds')],
        'codex_icons':[f'assets/ui/icons/codex/{i}.png' for i,_,_ in CODEX],
        'codex_portraits':[f'assets/ui/portraits/codex/{i}.png' for i,_,_ in CODEX],
    }
    report={}
    for group,paths in groups.items():
        missing=[p for p in paths if not (ROOT/p).exists()]
        report[group]={'present':len(paths)-len(missing),'expected':len(paths),'missing':missing}
    report['scope']='Required visual assets. Existing procedural music/SFX retained.'
    (ART/'coverage.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:f'{v["present"]}/{v["expected"]}' for k,v in report.items() if isinstance(v,dict)},indent=2))
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('action',choices=['units','previews','codex','audit','sheets','all'])
    args=parser.parse_args()
    REVIEW.mkdir(parents=True,exist_ok=True)
    # Native finish previews are review artifacts, never runtime resources.
    finish_review=REVIEW/'polish-v2'
    if finish_review.exists(): (finish_review/'.gdignore').touch(exist_ok=True)
    if args.action in ('units','all'): pack_units()
    if args.action in ('units','previews','all'): pack_previews()
    if args.action in ('codex','all'): codex()
    if args.action in ('sheets','all'):
        print(animation_preview())
        for cat,entries in [('units',[(u['DisplayName'],ROOT/'assets/ui/icons/units'/f'{u["Id"]}.png') for u in UNITS]),
                            ('relics',[(r['DisplayName'],ROOT/'assets/ui/icons/relics'/f'{r["Id"]}.png') for r in RELICS]),
                            ('spells',[(s['DisplayName'],ROOT/'assets/ui/icons/spells'/f'{s["Id"]}.png') for s in SPELLS]),
                            ('battlefields',[(i,ROOT/'assets/backgrounds'/f'{i}.png') for i in sorted({s['TerrainId'] for s in STAGES})]),
                            ('map-atlases',[(i,ROOT/'assets/world/overworld/polished-v3'/f'{i}.png') for i in ('terrain-materials','medieval-scenery','utility-scenery','resource-scenery')]),
                            ('caravans',[('standard',ROOT/'assets/structures/war_wagon.png')]+[(i,ROOT/'assets/structures'/f'war_wagon_skin_{i}.png') for i in ('iron','royal','bone','flame','shadow','guild','legendary')]),
                            ('mounts',[(i,ROOT/'assets/structures'/f'mount_{i}.png') for i in ('arrows','ballista','firepot','frost','hex')]),
                            ('particles',[(i.replace('particle_',''),ROOT/'assets/particles'/f'{i}.png') for i in catalog_array('ParticleTextureIds')])]:
            print(contact_sheet(cat,entries))
    audit()
