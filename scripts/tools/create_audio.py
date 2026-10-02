from pathlib import Path
import numpy as np
import wave, subprocess, argparse
parser=argparse.ArgumentParser(description='Generate Crownroad music and sound cues.')
parser.add_argument('--ui-only',action='store_true',help='Regenerate only selection and menu-transition sounds.')
args=parser.parse_args()
R=32000
rng=np.random.default_rng(905)
base=Path(__file__).resolve().parents[2]/'assets'
(base/'music').mkdir(exist_ok=True);(base/'sfx').mkdir(exist_ok=True)
def hz(m):return 440*2**((m-69)/12)
def note(m,duration,kind):
 t=np.arange(int(duration*R))/R;f=hz(m)
 if kind=='lute':
  y=sum((.55**(h-1)/h)*np.sin(2*np.pi*f*h*t+rng.uniform(-.2,.2))*np.exp(-t*(1.4+h*.7)) for h in range(1,9))
  env=np.minimum(t/.006,1)
 elif kind=='strings':
  y=sum((1/h**1.7)*np.sin(2*np.pi*f*h*t+.003*h*np.sin(t*2*np.pi*4.7)) for h in range(1,7))
  env=np.minimum(t/.3,1)*np.minimum((duration-t)/.4,1)*.45
 elif kind=='flute':
  y=np.sin(2*np.pi*f*t+.016*np.sin(t*2*np.pi*5))+.11*np.sin(2*np.pi*f*2*t)+.035*np.sin(2*np.pi*f*3*t)
  env=np.minimum(t/.08,1)*np.minimum((duration-t)/.15,1)*.5
 else:
  y=sum(g*np.sin(2*np.pi*f*ratio*t)*np.exp(-t*dec) for ratio,g,dec in [(1,1,2),(2.01,.35,3),(3.98,.16,5),(5.45,.05,7)])
  env=np.minimum(t/.004,1)
 return y*np.clip(env,0,1)
def drum(duration=.4):
 t=np.arange(int(duration*R))/R
 return np.sin(2*np.pi*(65*t+22*(1-np.exp(-t*25))/25))*np.exp(-t*10)+rng.normal(0,.1,len(t))*np.exp(-t*45)
def save(path,y,loop=False):
 if y.ndim==1:y=np.column_stack([y,y])
 y=np.tanh(y*.9)
 y=y/max(1,np.max(np.abs(y))/.88)
 if not loop:y[-min(500,len(y)):]*=np.linspace(1,0,min(500,len(y)))[:,None]
 wav=path.with_suffix('.wav')
 with wave.open(str(wav),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(R);w.writeframes((y*32767).astype('<i2').tobytes())
 subprocess.run(['ffmpeg','-loglevel','error','-y','-i',str(wav),'-c:a','vorbis','-strict','-2','-q:a','5',str(path)],check=True);wav.unlink()
def music(name,bpm,mode):
 beat=60/bpm; bars=16;duration=bars*4*beat;n=int(duration*R);out=np.zeros((n,2))
 progression=[[50,57,62,65],[46,53,58,62],[53,60,65,69],[48,55,60,64]]
 melody=[74,77,76,74,72,69,72,74,77,79,77,76,74,72,69,72]
 def add(sound,start,gain,pan=0):
  idx=(np.arange(len(sound))+int(start*R))%n
  out[idx,0]+=sound*gain*(1-pan*.4);out[idx,1]+=sound*gain*(1+pan*.4)
 for bar in range(bars):
  chord=progression[(bar//2)%4];start=bar*4*beat
  for i,m in enumerate(chord):add(note(m,beat*4,'strings'),start,.085 if mode!='battle' else .13,(i-1.5)/2)
  add(note(chord[0]-12,beat*3.8,'strings'),start,.16,0)
  for step in range(8):
   m=chord[[0,2,1,3,2,1,3,2][step]]+12
   add(note(m,beat*2.5,'lute'),start+step*beat*.5,.14 if mode!='battle' else .095,np.sin(step)*.7)
  if mode!='shop':
   for k in [0,2]:add(note(melody[(bar+k//2)%len(melody)],beat*1.65,'flute'),start+k*beat,.11 if mode!='battle' else .08,-.25)
  if mode=='title' and bar%4==0:add(note(86,beat*5,'bell'),start,.08,.6)
  if mode=='battle':
   for k in [0,1.5,2,3]:add(drum(),start+k*beat,.18)
   for k in range(8):
    t=np.arange(int(.16*R))/R;brush=rng.normal(0,.3,len(t));brush=np.convolve(brush,np.ones(5)/5,'same')*np.exp(-t*24)
    add(brush,start+k*beat*.5,.12,.7)
 # Circular room reflections make a seamless, stereo loop.
 dry=out.copy()
 for delay,g in [(0.071,.13),(.137,.12),(.229,.08),(.389,.05),(.613,.025)]:out+=np.roll(dry,int(delay*R),axis=0)[:,::-1]*g
 save(base/'music'/f'{name}.ogg',out,True)
def ui_tap(navigation=False):
 # Damped, slightly inharmonic wood resonances keep frequent selections warm.
 duration,frequency,gain=(.18,196,.3) if navigation else (.13,240,.28)
 t=np.arange(int(duration*R))/R
 y=sum(weight*np.sin(2*np.pi*frequency*ratio*t)*np.exp(-t*decay)
       for ratio,weight,decay in [(1,.72,32),(1.62,.21,48),(2.55,.07,68)])
 # A quiet, low-pass contact transient adds texture without a sharp click.
 noise=np.random.default_rng(1493).uniform(-1,1,len(t))
 alpha=1-np.exp(-2*np.pi*1000/R);filtered=0
 for i in range(len(noise)):
  filtered+=alpha*(noise[i]-filtered);noise[i]=filtered
 y+=noise*.06*np.exp(-t*90)
 attack=.5-.5*np.cos(np.pi*np.minimum(t/.004,1))
 release=np.clip((duration-1/R-t)/.025,0,1)
 return y*gain*attack*release
if args.ui_only:
 for name in ['ui_confirm','scene_change']:save(base/'sfx'/f'{name}.ogg',ui_tap(name=='scene_change'))
 print('Created warm selection and menu-transition cues.');raise SystemExit
for spec in [('title',84,'title'),('campaign',92,'travel'),('shop',88,'shop'),('battle',116,'battle')]:music(*spec)
# Layered physical impacts, short interface clicks and elemental spell cues.
def metal(heavy=False):
 t=np.arange(int(.45*R))/R
 y=sum(g*np.sin(2*np.pi*f*t)*np.exp(-t*d) for f,g,d in [(310 if heavy else 690,.2,12),(791,.13,18),(1297,.11,20),(2011,.07,28),(3017,.04,30)])
 y+=rng.normal(0,.18,len(t))*np.exp(-t*65);y+=np.sin(2*np.pi*95*t)*np.exp(-t*25)*(.45 if heavy else .12)
 return y*np.minimum(t/.001,1)
def sweep(kind):
 t=np.arange(int(.8*R))/R;noise=rng.normal(0,.3,len(t));noise=np.convolve(noise,np.ones(12)/12,'same')
 if kind=='fireball':return noise*np.sin(np.pi*np.minimum(t/.8,1))+.3*np.sin(2*np.pi*(90*t+30*t*t))*np.exp(-t*6)
 if kind=='lightning_strike':return rng.normal(0,.12,len(t))*np.exp(-t*12)+metal(True) if False else noise*np.exp(-t*5)+.18*np.sin(t*2*np.pi*210)*np.exp(-t*18)
 return sum(.09*note(m,.8,'bell') for m in ([79,86,91] if kind=='heal' else [74,81,86]))
for name,heavy in [('impact_light',False),('impact_heavy',True),('bus_hit',True),('barricade_hit',True)]:save(base/'sfx'/f'{name}.ogg',metal(heavy))
for name in ['deploy','ui_confirm','scene_change','upgrade_confirm','repair','relic_pickup','achievement_unlock']:
 y=note(62 if name=='deploy' else 81,.32,'lute')*.22
 if name in ['upgrade_confirm','repair','achievement_unlock']:y+=note(88,.32,'bell')*.14
 if name in ['ui_confirm','scene_change']:y=ui_tap(name=='scene_change')
 save(base/'sfx'/f'{name}.ogg',y)
for kind in ['fireball','heal','frost_burst','lightning_strike','barrier_ward']:save(base/'sfx'/f'spell_{kind}.ogg',sweep(kind))
for name,notes in [('victory',[62,65,69,74]),('defeat',[62,58,55,50]),('boss_spawn',[38,45,50]),('boss_death',[50,57,62])]:
 y=np.zeros(int(2.4*R))
 for i,m in enumerate(notes):
  sound=note(m,1.4,'strings');start=int(i*.24*R);y[start:start+len(sound)]+=sound*.25
 save(base/'sfx'/f'{name}.ogg',y)
print('Created 4 music loops and 20 sound cues.')
