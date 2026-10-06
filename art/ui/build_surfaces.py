"""Editable SVG material library for Crownroad's shared, nine-sliced UI.

No bitmap synthesis, external artwork, shaders, or SVG filters. Grain uses
explicit seamless vector paths; Godot tiles the center at a fixed pixel scale.
"""
from pathlib import Path
import random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/ui/frames"


def svg(w, h, body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">\n{body}\n</svg>\n'


def grain(w, h, clip=None, margin=0):
    # Godot's SVG importer ignores <pattern> fills. Expand a seamless 32 px
    # repeat into ordinary paths, which are supported by the native importer.
    rng = random.Random(124)
    marks = [[], []]
    samples = []
    for i in range(95):
        x, y = rng.randrange(1, 31), rng.randrange(1, 31)
        samples.append((x,y,rng.choice([.45,.7,1.2,2]),0 if i%3 else 1))
    for by in range(0,h,32):
        for bx in range(0,w,32):
            for x,y,length,kind in samples:
                px,py=bx+x,by+y
                if margin<=px and px+length<w-margin and margin<=py<h-margin:
                    marks[kind].append(f'M{px} {py}h{length}')
    paths=''.join(f'<path d="{" ".join(points)}" stroke="{color}" stroke-opacity="{opacity}" stroke-width=".7"/>'
                  for points,color,opacity in zip(marks,['#d8d3bc','#030b0f'],['.045','.14']))
    return f'<g clip-path="url(#{clip})">{paths}</g>' if clip else paths


def bevel(w, h, inset=1, cut=8):
    a, b = inset, inset + cut
    return f'M{b} {a}H{w-b}L{w-a} {b}V{h-b}L{w-b} {h-a}H{b}L{a} {h-b}V{b}Z'


def panel():
    # The center is a uniform material. Lighting lives entirely in the fixed
    # edge slices, so tiling a very large panel never repeats a broad gradient.
    w = h = 256
    shape = bevel(w,h,2,10)
    defs = f'''<defs>
      <clipPath id="clip"><path d="{shape}"/></clipPath>
      <linearGradient id="top" x2="0" y2="1"><stop stop-color="#a4ac8c" stop-opacity=".13"/><stop offset="1" stop-color="#a4ac8c" stop-opacity="0"/></linearGradient>
      <linearGradient id="bottom" x2="0" y2="1"><stop stop-color="#02090c" stop-opacity="0"/><stop offset="1" stop-color="#02090c" stop-opacity=".6"/></linearGradient>
      <linearGradient id="brass" x2="0" y2="1"><stop stop-color="#dac596"/><stop offset=".25" stop-color="#927c55"/><stop offset=".72" stop-color="#595945"/><stop offset="1" stop-color="#b19663"/></linearGradient>
    </defs>'''
    body = f'''{defs}
      <path d="{shape}" fill="#17282c" stroke="#050c10" stroke-width="3"/>
      {grain(w,h,'clip')}
      <g clip-path="url(#clip)">
        <path d="M0 0H256V28H0Z" fill="url(#top)"/>
        <path d="M0 228H256V256H0Z" fill="url(#bottom)"/>
      </g>
      <path d="{bevel(w,h,4,9)}" fill="none" stroke="url(#brass)" stroke-width="1.5"/>
      <path d="{bevel(w,h,8,7)}" fill="none" stroke="#060f13" stroke-width="2"/>
      <path d="M17 10H239M10 17V239" fill="none" stroke="#c7bd8d" stroke-opacity=".14"/>
      <path d="M17 246H239L246 239" fill="none" stroke="#060d11" stroke-width="2"/>'''
    for angle in (0,90,180,270):
        body += f'''<g transform="rotate({angle} 128 128)">
          <path d="M10 29V17L17 10H29" fill="none" stroke="#c6ad76" stroke-width="1.2"/>
          <path d="M14 27V19L19 14H27" fill="none" stroke="#657568" stroke-width=".8"/>
          <circle cx="20" cy="20" r="2.8" fill="#060e11"/>
          <circle cx="20" cy="19.4" r="1.7" fill="#a58f62"/>
          <path d="M19 19h2" stroke="#eed4a1" stroke-width=".6"/>
        </g>'''
    return svg(w,h,body)


def button(top, mid, bottom, edge, pressed=False, disabled=False):
    w,h = 192,64
    shape = bevel(w,h,2,7)
    body = f'''<defs>
      <linearGradient id="face" x2="0" y2="1"><stop stop-color="{top}"/><stop offset=".36" stop-color="{mid}"/><stop offset="1" stop-color="{bottom}"/></linearGradient>
      <linearGradient id="rim" x2="0" y2="1"><stop stop-color="{edge}"/><stop offset=".52" stop-color="#52645d"/><stop offset="1" stop-color="{edge}"/></linearGradient>
      <clipPath id="clip"><path d="{bevel(w,h,5,5)}"/></clipPath>
    </defs>
    <path d="{bevel(w,h,1,8)}" fill="#070e12" stroke="#050a0d"/>
    <path d="{shape}" fill="url(#face)" stroke="url(#rim)" stroke-width="1.5"/>
    {grain(w,h,'clip')}
    <path d="{bevel(w,h,5,5)}" fill="none" stroke="#070f12" stroke-width="1"/>
    <path d="M11 6H181M6 11V50" fill="none" stroke="{'#040b0e' if pressed else edge}" stroke-opacity=".42"/>
    <path d="M11 58H181L186 53" fill="none" stroke="{edge if pressed else '#050c10'}" stroke-opacity=".6" stroke-width="1.5"/>
    <path d="M8 29v6M184 29v6" stroke="{edge}" stroke-opacity=".55" stroke-width="1"/>'''
    if pressed:
        body += f'<path d="M16 55H176" stroke="{edge}" stroke-width="2"/>'
    if not disabled:
        body += '<path d="M19 4h9m9 0h3m113 0h11m-108 56h7m44 0h14" stroke="#e0ce9c" stroke-opacity=".17" stroke-width=".6"/>'
    return svg(w,h,body)


def inset(fill='#101d22', edge='#52635e', grey=False, rim_only=False):
    shape = bevel(96,96,1,6)
    # Tinted body and tinted border can be rendered independently for badges.
    body = f'<defs><clipPath id="clip"><path d="{bevel(96,96,3,5)}"/></clipPath></defs>'
    if not rim_only:
        body += f'<path d="{shape}" fill="{fill}"/>{grain(96,96,"clip")}'
    body += f'<path d="{shape}" fill="none" stroke="{edge}" stroke-width="1.2"/>'
    body += f'<path d="M9 3H87M3 9V87" stroke="{"#fff" if grey else "#030b0f"}" stroke-opacity=".38" fill="none"/>'
    body += f'<path d="M9 93H87L93 87" stroke="{"#fff" if grey else "#adc2aa"}" stroke-opacity=".12" fill="none"/>'
    return svg(96,96,body)


def focus():
    return svg(192,64, f'''<path d="{bevel(192,64,1,8)}" fill="none" stroke="#080d10" stroke-width="4"/>
      <path d="{bevel(192,64,2,7)}" fill="none" stroke="#ffe1a4" stroke-width="2"/>
      <path d="M11 6h8m154 0h8M6 11v8m180 0v-8M11 58h8m154 0h8" stroke="#fff4d8" fill="none"/>''')


def meter(fill=False):
    if fill:
        return svg(128,24, f'''<defs><linearGradient id="g" x2="0" y2="1">
        <stop stop-color="#f5ebce"/><stop offset=".18" stop-color="#d8d4ba"/>
        <stop offset=".5" stop-color="#a8b3a5"/><stop offset="1" stop-color="#667b71"/></linearGradient></defs>
        <rect width="128" height="24" fill="url(#g)"/>{grain(128,24)}
        <path d="M0 1H128" stroke="#fff1cd" stroke-opacity=".45"/>
        <path d="M0 23H128" stroke="#04151c" stroke-opacity=".55"/>''')
    return svg(128,24, f'''<path d="{bevel(128,24,1,3)}" fill="#0a141a" stroke="#7e7759"/>
      {grain(128,24,margin=4)}
      <path d="M5 3H123" stroke="#02080b" stroke-width="2"/>
      <path d="M5 21H123" stroke="#d5be88" stroke-opacity=".24"/>''')


def thumb(hover=False, horizontal=False):
    if horizontal:
        # A horizontal rail needs vertical lighting. Repeating the vertical
        # thumb sideways produces a distracting striped band on phone cards.
        return svg(76,24, f'''<defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="#4c564c"/>
          <stop offset=".45" stop-color="{'#b7a06c' if hover else '#847957'}"/><stop offset="1" stop-color="#474e43"/></linearGradient></defs>
          <rect x="1" y="2" width="74" height="20" rx="3" fill="url(#g)" stroke="{'#ead29b' if hover else '#a89c73'}"/>
          {grain(76,24,margin=4)}''')
    return svg(24,64, f'''<defs><linearGradient id="g"><stop stop-color="#4c564c"/>
      <stop offset=".45" stop-color="{'#b7a06c' if hover else '#847957'}"/><stop offset="1" stop-color="#474e43"/></linearGradient></defs>
      <rect x="2" y="1" width="20" height="62" rx="3" fill="url(#g)" stroke="{'#ead29b' if hover else '#a89c73'}"/>
      {grain(24,64,margin=4)}
      <path d="M7 28h10m-10 4h10m-10 4h10" stroke="#1b292b" stroke-opacity=".75"/>
      <path d="M7 29h10m-10 4h10m-10 4h10" stroke="#dfc895" stroke-opacity=".32"/>''')


def checkbox(checked=False, radio=False, disabled=False):
    edge = '#63716b' if disabled else '#bdab7f'
    body = f'<rect x="1" y="1" width="22" height="22" rx="{11 if radio else 4}" fill="#0e1c21" stroke="{edge}"/>'
    body += '<path d="M5 4h14" stroke="#000" stroke-opacity=".45"/>'
    if checked:
        body += f'<circle cx="12" cy="12" r="5" fill="{edge}"/>' if radio else f'<path d="M6 12l4 4 8-9" fill="none" stroke="{edge}" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>'
    return svg(24,24,body)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    assets = {
        'engraved_panel': panel(),
        'button': button('#3a4a49','#263c3e','#1a2b30','#a79570'),
        'button_hover': button('#47615c','#304b4b','#213a3c','#e2c896'),
        'button_pressed': button('#14282e','#21383b','#2a4140','#d0b477',pressed=True),
        'button_hover_pressed': button('#20363a','#2b4545','#334e49','#efcf91',pressed=True),
        'button_disabled': button('#293333','#222d30','#1c272b','#62716a',disabled=True),
        'button_primary': button('#8a6b3c','#655033','#3e392a','#e0c08a'),
        'button_primary_hover': button('#a0804a','#80623b','#51462f','#ffe0a1'),
        'button_primary_pressed': button('#3b3024','#57422c','#6a5030','#e0c08a',pressed=True),
        'focus': focus(),
        'inset': inset(),
        'input_focus': inset('#14272b','#e0c08a'),
        'input_disabled': inset('#131c20','#46534e'),
        'surface_body': inset('#ffffff','#ffffff',grey=True),
        'surface_rim': inset(edge='#ffffff',grey=True,rim_only=True),
        'meter_track': meter(),
        'meter_fill': meter(True),
        'scroll_thumb': thumb(),
        'scroll_thumb_hover': thumb(True),
        'scroll_thumb_horizontal': thumb(horizontal=True),
        'scroll_thumb_horizontal_hover': thumb(True,horizontal=True),
        'separator': svg(128,4,'<path d="M0 1.5H128" stroke="#907e58" stroke-opacity=".5"/><path d="M0 2.5H128" stroke="#050f14" stroke-opacity=".85"/>'),
        'dropdown': svg(18,18,'<path d="M4 7l5 5 5-5" fill="none" stroke="#d9bd82" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>'),
    }
    for radio in (False,True):
        for checked in (False,True):
            for disabled in (False,True):
                key=('radio_' if radio else '')+('checked' if checked else 'unchecked')+('_disabled' if disabled else '')
                assets[key]=checkbox(checked,radio,disabled)
    for name, content in assets.items():
        (OUT/f'{name}.svg').write_text(content)
    print(f'Published {len(assets)} native SVG UI surfaces to {OUT}')


if __name__ == '__main__':
    main()
