"""Write the comparison page (artifacts/remaster/compare/index.html) from the compare/ media."""
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'artifacts/remaster'
# Originals come from git HEAD (assets/ may already hold the remaster in this branch).
OLD = REVIEW / 'old'
OUT = REVIEW / 'compare'
UNITS = json.loads((ROOT / 'data/units.json').read_text())['Units']


def portraits():
    """Small old/new portrait pairs for the detail view."""
    d = OUT / 'portraits'
    d.mkdir(parents=True, exist_ok=True)
    for u in UNITS:
        ident = u['Id']
        new = REVIEW / 'units' / ident / 'portrait.png'
        old = OLD / 'assets/ui/portraits/codex' / f'{ident}.png'
        for tag, src in (('old', old), ('new', new)):
            if src.exists():
                im = Image.open(src).convert('RGBA')
                bg = Image.new('RGBA', im.size, (38, 42, 46, 255))
                bg.alpha_composite(im)
                bg.convert('RGB').resize((320, 320), Image.LANCZOS).save(d / f'{ident}-{tag}.jpg', quality=84)


def build():
    portraits()
    rows = []
    for u in UNITS:
        ident = u['Id']
        if not (OUT / 'units' / f'{ident}.webp').exists():
            continue
        meta = json.loads((REVIEW / 'stage/assets/units' / f'{ident}.json').read_text())
        group = 'boss' if u['VisualClass'] == 'boss' else ('player' if u['Side'] != 'Enemy' else 'enemy')
        rows.append(dict(id=ident, name=u['DisplayName'], group=group, cls=u['VisualClass'],
                         profile=meta['motion']['profile']))
    sections = {
        'ingame': sorted(p.name for p in (OUT / 'ingame').glob('*.jpg')) if (OUT / 'ingame').exists() else [],
        'battle': sorted(p.name for p in (OUT / 'battle').glob('*.jpg')) if (OUT / 'battle').exists() else [],
        'items': sorted(p.name for p in (OUT / 'items').glob('*.jpg')) if (OUT / 'items').exists() else [],
        'structures': sorted(p.name for p in (OUT / 'structures').glob('*.jpg')) if (OUT / 'structures').exists() else [],
        'menus': sorted(p.name for p in (OUT / 'menus').glob('*.jpg')) if (OUT / 'menus').exists() else [],
        'battlefields': sorted(p.name for p in (OUT / 'battlefields').glob('*.jpg')) if (OUT / 'battlefields').exists() else [],
        'maps': sorted(p.name for p in (OUT / 'maps').glob('*.jpg')) if (OUT / 'maps').exists() else [],
        'particles': sorted(p.name for p in (OUT / 'particles').glob('*.jpg')) if (OUT / 'particles').exists() else [],
    }
    data = json.dumps(dict(units=rows, sections=sections))
    html = TEMPLATE.replace('__DATA__', data)
    (OUT / 'index.html').write_text(html)
    files = {}
    for p in OUT.rglob('*'):
        if p.is_file() and p.name != 'index.html' and p.suffix in ('.webp', '.jpg', '.png'):
            if p.parent.name == 'units' and p.suffix == '.jpg':
                continue
            files[str(p.relative_to(OUT))] = str(p.relative_to(ROOT))
    (OUT / 'files.json').write_text(json.dumps(files, indent=1))
    size = sum((ROOT / v).stat().st_size for v in files.values())
    print(json.dumps({'units': len(rows), 'files': len(files), 'mb': round(size / 1e6, 1)}))


TEMPLATE = r'''<title>Crownroad Art Remaster</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Marcellus+SC&family=Alegreya+Sans:wght@400;500;700&family=IBM+Plex+Mono:wght@400;500&display=swap">
<style>
/* Layout: a field-review ledger — banner header, section rail, then dense comparison grids on a slate "stage". */
:root {
  --bg: #eef0ee; --surface: #ffffff; --fg: #1d2523; --muted: #5b6764; --line: #d4dad7;
  --brass: #9a6f22; --teal: #1c6e69; --crimson: #7a1e30; --stage: #262a2e; --stage-fg: #e6dcc4;
  --display: "Marcellus SC", "Trajan Pro", Georgia, serif;
  --body: "Alegreya Sans", "Gill Sans", "Segoe UI", sans-serif;
  --mono: "IBM Plex Mono", ui-monospace, Menlo, monospace;
  color-scheme: light;
}
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
  --bg: #141917; --surface: #1c2220; --fg: #e8e4da; --muted: #9aa49f; --line: #2c3532;
  --brass: #d6a54e; --teal: #4bb3aa; --crimson: #e0687c; --stage: #23272b; --stage-fg: #e6dcc4; color-scheme: dark } }
:root[data-theme="dark"] {
  --bg: #141917; --surface: #1c2220; --fg: #e8e4da; --muted: #9aa49f; --line: #2c3532;
  --brass: #d6a54e; --teal: #4bb3aa; --crimson: #e0687c; --stage: #23272b; --stage-fg: #e6dcc4; color-scheme: dark }
body { background: var(--bg); color: var(--fg); font: 17px/1.5 var(--body); }
.wrap { max-width: 1240px; margin: 0 auto; padding-inline: 20px; padding-block: 28px 64px; }
header { display: grid; gap: 10px; padding-block: 8px 22px; border-bottom: 1px solid var(--line); }
.eyebrow { font: 500 12px/1 var(--mono); letter-spacing: .14em; text-transform: uppercase; color: var(--brass); }
h1 { font: 400 clamp(32px, 5vw, 52px)/1.05 var(--display); margin: 0; text-wrap: balance; letter-spacing: .01em; }
h2 { font: 400 26px/1.15 var(--display); margin: 0; text-wrap: balance; }
.lede { max-width: 68ch; color: var(--muted); margin: 0; }
.counts { display: flex; flex-wrap: wrap; gap: 8px 22px; font: 14px var(--mono); color: var(--muted); font-variant-numeric: tabular-nums; }
.counts b { color: var(--fg); font-weight: 500; }
nav { position: sticky; top: env(safe-area-inset-top, 0px); z-index: 5; background: var(--bg); display: flex; gap: 6px;
  flex-wrap: wrap; padding-block: 12px; border-bottom: 1px solid var(--line); }
nav a { font: 500 14px var(--body); color: var(--fg); text-decoration: none; padding: 6px 12px; border: 1px solid var(--line);
  border-radius: 999px; }
nav a:hover, nav a:focus-visible { border-color: var(--brass); color: var(--brass); outline: none; }
section { display: grid; gap: 16px; padding-block: 34px 10px; scroll-margin-top: 64px; }
.sec-head { display: flex; flex-wrap: wrap; align-items: baseline; justify-content: space-between; gap: 8px 20px; }
.sec-head p { margin: 0; color: var(--muted); max-width: 70ch; }
.legend { display: flex; gap: 14px; font: 13px var(--mono); color: var(--muted); }
.legend span::before { content: ""; display: inline-block; width: 9px; height: 9px; margin-right: 6px; border-radius: 2px;
  vertical-align: middle; background: var(--muted); }
.legend .new::before { background: var(--teal); }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chip { font: 500 14px var(--body); padding: 5px 12px; border-radius: 999px; border: 1px solid var(--line); background: var(--surface);
  color: var(--fg); cursor: pointer; }
.chip[aria-pressed="true"] { background: var(--fg); color: var(--bg); border-color: var(--fg); }
.chip:focus-visible { outline: 2px solid var(--brass); outline-offset: 2px; }
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 280px), 1fr)); gap: 14px; }
.card { display: grid; gap: 0; background: var(--surface); border: 1px solid var(--line); border-radius: 10px; overflow: hidden;
  cursor: pointer; text-align: left; padding: 0; color: inherit; font: inherit; }
.card:hover, .card:focus-visible { border-color: var(--brass); outline: none; }
.stage { background: var(--stage); position: relative; aspect-ratio: 2 / 1; max-width: 100%; }
.stage img { width: 100%; height: 100%; object-fit: contain; display: block; }
.stage .tag { position: absolute; top: 8px; font: 500 11px var(--mono); letter-spacing: .1em; text-transform: uppercase;
  color: var(--stage-fg); opacity: .8; }
.stage .tag.l { left: 10px; } .stage .tag.r { left: calc(50% + 10px); color: #7fd6cd; opacity: 1; }
.meta { display: grid; gap: 2px; padding: 10px 12px 12px; }
.meta strong { font: 500 17px var(--body); }
.meta span { font: 12.5px var(--mono); color: var(--muted); }
.side { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; vertical-align: middle; }
.side.player { background: var(--teal); } .side.enemy { background: var(--crimson); } .side.boss { background: var(--brass); }
.wide { display: grid; gap: 14px; }
figure { margin: 0; display: grid; gap: 6px; }
figure img { width: 100%; border-radius: 8px; display: block; background: var(--stage); }
figcaption { font: 13px var(--mono); color: var(--muted); }
.pairs { display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 520px), 1fr)); gap: 18px; }
dialog { border: 1px solid var(--line); border-radius: 12px; background: var(--surface); color: var(--fg); padding: 0;
  width: min(940px, calc(100vw - 32px)); max-height: calc(100vh - 32px); }
dialog::backdrop { background: rgba(10, 12, 12, .7); }
.dlg { display: grid; gap: 14px; padding: 18px; }
.dlg-head { display: flex; justify-content: space-between; align-items: start; gap: 12px; }
.dlg-head button { font: 500 14px var(--body); background: none; border: 1px solid var(--line); color: var(--fg); border-radius: 999px;
  padding: 4px 12px; cursor: pointer; }
.duo { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.duo figure img { aspect-ratio: 1; object-fit: cover; }
@media (max-width: 520px) { .duo { grid-template-columns: 1fr; } }
@media (prefers-reduced-motion: reduce) { * { scroll-behavior: auto !important; } }
</style>
<div class="wrap">
  <header>
    <div class="eyebrow">Crownroad · Siege of Ash · art review</div>
    <h1>Art remaster, current vs new</h1>
    <p class="lede">Every Blender asset the game ships, rebuilt with sculpted skinned bodies, real armour and weapon models, hand-finished
      materials and new combat animation. Each pair shows the current asset on the left and the remaster on the right, at the same
      in-game scale. Click a unit to see its portrait.</p>
    <div class="counts" id="counts"></div>
  </header>
  <nav aria-label="Sections" id="nav"></nav>
  <section id="units">
    <div class="sec-head"><h2>Units</h2><div class="legend"><span>Current</span><span class="new">Remaster</span></div></div>
    <p class="lede">Animated loop: idle, walk, attack, hit, death and deploy. Enemies are authored facing right and mirrored by the game.</p>
    <div class="chips" role="group" aria-label="Filter units" id="chips"></div>
    <div class="grid" id="unit-grid"></div>
  </section>
  <section id="ingame" hidden>
    <div class="sec-head"><h2>In the game</h2><p>Captured from the running game with the project's own asset smoke test: current art on top, remaster below.</p></div>
    <div class="wide" id="ingame-list"></div>
  </section>
  <section id="battle" hidden>
    <div class="sec-head"><h2>On the battlefield</h2><p>Idle sprites placed on the painted King's Road stages at the exact runtime scale.</p></div>
    <div class="wide" id="battle-list"></div>
  </section>
  <section id="structures" hidden>
    <div class="sec-head"><h2>Caravan, gatehouse and mounts</h2><p>War wagon and its seven skins, the enemy gatehouse, and the five weapon mounts.</p></div>
    <div class="pairs" id="structures-list"></div>
  </section>
  <section id="items" hidden>
    <div class="sec-head"><h2>Icons</h2><p>Spells, relics, rewards and status badges. The small row under each pair shows the icon at 64 px.</p></div>
    <div class="wide" id="items-list"></div>
  </section>
  <section id="menus" hidden>
    <div class="sec-head"><h2>Menu backgrounds</h2><p>The 23 Blender menu scenes behind the game's screens.</p></div>
    <div class="pairs" id="menus-list"></div>
  </section>
  <section id="battlefields" hidden>
    <div class="sec-head"><h2>Battlefield fallbacks</h2><p>Blender battle backgrounds, one per terrain. The game shows these whenever a stage's painted art is missing.</p></div>
    <div class="pairs" id="battlefields-list"></div>
  </section>
  <section id="maps" hidden>
    <div class="sec-head"><h2>District maps</h2><p>Blender campaign map panels for the ten districts.</p></div>
    <div class="pairs" id="maps-list"></div>
  </section>
  <section id="particles" hidden>
    <div class="sec-head"><h2>Particles</h2><p>Tintable battle effect sprites.</p></div>
    <div class="wide" id="particles-list"></div>
  </section>
</div>
<dialog id="dlg" aria-labelledby="dlg-title">
  <div class="dlg">
    <div class="dlg-head"><div><div class="eyebrow" id="dlg-eyebrow"></div><h2 id="dlg-title"></h2></div>
      <button type="button" id="dlg-close">Close</button></div>
    <div class="stage"><img id="dlg-anim" alt=""><span class="tag l">Current</span><span class="tag r">Remaster</span></div>
    <div class="duo">
      <figure><img id="dlg-old" alt=""><figcaption>Current portrait</figcaption></figure>
      <figure><img id="dlg-new" alt=""><figcaption>Remaster portrait</figcaption></figure>
    </div>
  </div>
</dialog>
<script>
const DATA = __DATA__;
const groups = [['all', 'All'], ['player', 'Lantern Caravan'], ['enemy', 'Rotbound Host'], ['boss', 'Bosses']];
const label = { player: 'Lantern Caravan', enemy: 'Rotbound Host', boss: 'Boss' };
const titles = { ingame: 'In the game', battle: 'Battlefield', structures: 'Structures', items: 'Icons', menus: 'Menus', battlefields: 'Battle fallbacks', maps: 'District maps', particles: 'Particles' };
const nice = s => s.replace(/\.jpg$/, '').replace(/[-_]/g, ' ');
const el = (tag, attrs = {}, kids = []) => { const n = document.createElement(tag); Object.entries(attrs).forEach(([k, v]) => {
  if (k === 'text') n.textContent = v; else n.setAttribute(k, v); }); kids.forEach(c => n.append(c)); return n; };

const counts = document.getElementById('counts');
const nItems = { units: DATA.units.length, structures: DATA.sections.structures.length, menus: DATA.sections.menus.length };
counts.innerHTML = `<span><b>${nItems.units}</b> units</span>` + (nItems.structures ? `<span><b>${nItems.structures}</b> structures</span>` : '') +
  (nItems.menus ? `<span><b>${nItems.menus}</b> menu scenes</span>` : '') + (DATA.sections.items.length ? `<span><b>${DATA.sections.items.length}</b> icon sheets</span>` : '');

const nav = document.getElementById('nav');
nav.append(el('a', { href: '#units', text: 'Units' }));
Object.keys(titles).forEach(k => { if (DATA.sections[k] && DATA.sections[k].length) {
  document.getElementById(k).hidden = false; nav.append(el('a', { href: '#' + k, text: titles[k] })); } });

const grid = document.getElementById('unit-grid');
const chips = document.getElementById('chips');
let filter = 'all';
function renderUnits() {
  grid.replaceChildren();
  DATA.units.filter(u => filter === 'all' || u.group === filter).forEach(u => {
    const card = el('button', { class: 'card', type: 'button', 'aria-label': `${u.name}: open portrait comparison` }, [
      el('div', { class: 'stage' }, [el('img', { src: `units/${u.id}.webp`, alt: `${u.name}, current and remaster animation`, loading: 'lazy' }),
        el('span', { class: 'tag l', text: 'Current' }), el('span', { class: 'tag r', text: 'Remaster' })]),
      el('div', { class: 'meta' }, [el('strong', {}, [el('span', { class: 'side ' + u.group }), document.createTextNode(u.name)]),
        el('span', { text: `${u.id} · ${u.profile}` })])]);
    card.addEventListener('click', () => openUnit(u));
    grid.append(card);
  });
}
groups.forEach(([key, text]) => { const b = el('button', { class: 'chip', type: 'button', 'aria-pressed': String(key === filter), text });
  b.addEventListener('click', () => { filter = key; chips.querySelectorAll('.chip').forEach(c => c.setAttribute('aria-pressed', String(c === b))); renderUnits(); });
  chips.append(b); });
renderUnits();

const dlg = document.getElementById('dlg');
function openUnit(u) {
  document.getElementById('dlg-eyebrow').textContent = `${label[u.group]} · ${u.cls} · ${u.profile}`;
  document.getElementById('dlg-title').textContent = u.name;
  document.getElementById('dlg-anim').src = `units/${u.id}.webp`;
  document.getElementById('dlg-old').src = `portraits/${u.id}-old.jpg`;
  document.getElementById('dlg-new').src = `portraits/${u.id}-new.jpg`;
  if (dlg.showModal) dlg.showModal(); else dlg.setAttribute('open', '');
}
document.getElementById('dlg-close').addEventListener('click', () => dlg.close());
dlg.addEventListener('click', e => { if (e.target === dlg) dlg.close(); });

function figures(key, list, cls) {
  const box = document.getElementById(key + '-list');
  list.forEach(f => box.append(el('figure', {}, [el('img', { src: `${key}/${f}`, alt: nice(f), loading: 'lazy' }),
    el('figcaption', { text: key === 'battle' ? nice(f) : key === 'ingame' ? `${nice(f)} · current top, remaster bottom` : `${nice(f)} · current left, remaster right` })])));
}
['ingame', 'battle', 'structures', 'items', 'menus', 'battlefields', 'maps', 'particles'].forEach(k => figures(k, DATA.sections[k] || []));
</script>
'''

if __name__ == '__main__':
    build()
