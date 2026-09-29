"""Contact sheets/GIF assembly of native renders, not generated artwork."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/blender/combat-motion'
ids=['player_brawler','player_spear','player_shooter','player_berserker','player_hound','player_ballista']
sheet=Image.new('RGB',(1000,180*len(ids)),(22,32,37))
d=ImageDraw.Draw(sheet)
for y,ident in enumerate(ids):
    d.text((14,y*180+8),ident.replace('player_','').upper(),fill=(230,210,166))
    for x,i in enumerate((10,12,14,15,18)):
        im=Image.open(OUT/ident/f'{i:03}.png').convert('RGBA');im.thumbnail((160,150))
        sheet.paste(im,(x*200+20,y*180+24),im)
        d.text((x*200+14,y*180+164),('Ready','Wind-up','Contact / release','Follow-through','Recover')[x],fill=(157,181,181))
sheet.save(OUT/'attack-poses.jpg',quality=94)
frames=[]
for i in range(10,20):
    canvas=Image.new('RGB',(900,600),(22,32,37));draw=ImageDraw.Draw(canvas)
    for n,ident in enumerate(ids):
        im=Image.open(OUT/ident/f'{i:03}.png').convert('RGBA');im.thumbnail((230,260))
        x,y=(n%3)*300,(n//3)*300
        canvas.paste(im,(x+35,y+25),im)
        draw.text((x+25,y+15),ident.replace('player_','').upper(),fill=(230,210,166))
    frames.append(canvas)
frames[0].save(OUT/'attack-studies.gif',save_all=True,append_images=frames[1:],duration=[160,85,85,110,85,85,85,85,85,300],loop=0)

captures=sorted((OUT/'game-frames').glob('*.png'))
if captures:
    game_frames=[]
    for path in captures:
        source=Image.open(path).convert('RGB')
        # Enlarge a close crop of real-engine footage; no motion or hit effects are added.
        canvas=Image.new('RGB',(1080,550),(22,32,37)); draw=ImageDraw.Draw(canvas)
        for n,(box,title) in enumerate([
            ((270,210,420,330),'SWORD / COUNTER'), ((590,210,740,330),'HEAVY CLEAVE'), ((885,230,1035,350),'HOUND POUNCE'),
            ((260,365,410,490),'SPEAR THRUST'), ((535,375,760,490),'BOW RELEASE'), ((835,375,1135,490),'BALLISTA RECOIL')]):
            scaled_box=tuple(round(v*(source.width/1280 if k%2==0 else source.height/720)) for k,v in enumerate(box))
            crop=source.crop(scaled_box)
            scale=min(340/crop.width,250/crop.height)
            crop=crop.resize((round(crop.width*scale),round(crop.height*scale)),Image.Resampling.LANCZOS)
            x=(n%3)*360; y=(n//3)*275
            canvas.paste(crop,(x+(360-crop.width)//2,y+25+(250-crop.height)//2)); draw.text((x+16,y+9),title,fill=(236,218,181))
        game_frames.append(canvas)
    game_frames[0].save(OUT/'combat-in-game.gif',save_all=True,append_images=game_frames[1:],duration=33,loop=0)
    game_frames[8].save(OUT/'combat-in-game.jpg',quality=95)
