"""Build mono weapon foley from CC0 recordings. Requires numpy and miniaudio.
Cached originals live in Temp/WeaponAudioSources; see Docs/Audio/Sources.md.
"""
from pathlib import Path
import sys, urllib.request, re, zipfile, wave, hashlib, json
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Temp/WeaponAudioTools'))
import miniaudio

RATE = 44100
CACHE = ROOT / 'Temp/WeaponAudioSources'
OUT = ROOT / 'Assets/Resources/WeaponAudio'
CACHE.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)

def download(name, url):
    p = CACHE / name
    if not p.exists():
        p.write_bytes(urllib.request.urlopen(url, timeout=40).read())
    return p

def freesound(name, url):
    if (CACHE/name).exists():
        return CACHE/name
    page = urllib.request.urlopen(url, timeout=30).read().decode()
    previews = re.findall(r'https://cdn.freesound.org/previews/[^"\s<>]+?-hq\.mp3', page)
    if not previews:
        raise RuntimeError('No public HQ preview: ' + url)
    return download(name, previews[0])

mechanism = freesound('revolver-mechanism-hq.mp3', 'https://freesound.org/people/Dredile/sounds/177863/')
revolver = freesound('revolver-shot-hq.mp3', 'https://freesound.org/people/Bane_Sound/sounds/542549/')
taser = freesound('taser-hq.mp3', 'https://freesound.org/people/The_Chemical_Workshop/sounds/403252/')
breaking = freesound('glass-break-hq.mp3', 'https://freesound.org/people/dasebr/sounds/212698/')
bullet = download('singlebullet.wav', 'https://opengameart.org/sites/default/files/singlebullet1.wav')
clicks = download('clicks.wav', 'https://opengameart.org/sites/default/files/equipmentclicks.wav')
reload_zip = download('reload.zip', 'https://opengameart.org/sites/default/files/shotgunsounds.zip')
shots_zip = download('shots.zip', 'https://opengameart.org/sites/default/files/sounds.zip')
for archive in [reload_zip, shots_zip]:
    with zipfile.ZipFile(archive) as z:
        for name in z.namelist():
            if name.lower().endswith(('.wav', '.mp3')):
                dest = CACHE / archive.stem / Path(name).name
                dest.parent.mkdir(exist_ok=True)
                if not dest.exists():
                    dest.write_bytes(z.read(name))

def read(p):
    d = miniaudio.decode_file(str(p), output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=RATE)
    return np.asarray(d.samples, dtype=np.float64).copy()

M, R, T, B, C = map(read, [mechanism, revolver, taser, bullet, clicks])
G = read(breaking)
S = read(CACHE / 'shots/shotty.wav')
K = read(CACHE / 'reload/Rack.mp3')
L = read(CACHE / 'reload/First Shell.mp3')

def cut(a, start, end):
    return a[int(start*RATE):int(end*RATE)].copy()

def pitch(a, factor):
    return np.interp(np.arange(0, len(a)-1, factor), np.arange(len(a)), a)

def mix(*parts):
    n = max(int(t*RATE)+len(a) for t,a,gain in parts)
    result = np.zeros(n)
    for t,a,gain in parts:
        offset = int(t*RATE)
        result[offset:offset+len(a)] += a*gain
    return result

def finish(a, peak=.66, trim=True):
    a = a - np.mean(a)
    if trim:
        audible = np.flatnonzero(np.abs(a) > max(np.max(np.abs(a))*.004, .00005))
        if len(audible):
            a = a[max(0,audible[0]-int(.004*RATE)):min(len(a),audible[-1]+int(.025*RATE))]
    # Tiny fades remove edit clicks; short room taps preserve the actual transient.
    fade = min(int(.002*RATE),len(a)//4)
    a[:fade] *= np.linspace(0,1,fade)
    end = min(int(.025*RATE),len(a)//3)
    a[-end:] *= np.linspace(1,0,end)
    a *= peak/max(np.max(np.abs(a)), .00001)
    return a

def room(a):
    return mix((0,a,1),(.031,a,.12),(.064,a,.075),(.107,a,.035))

clips = {
    'RevolverPickup': mix((0,cut(C,3.72,4.12),.55),(.10,cut(M,.42,.63),.25)),
    'RevolverHammer': cut(M,2.22,2.42),
    'RevolverUnlock': cut(M,.42,.65),
    'RevolverOpen': pitch(cut(M,2.00,2.19),.90),
    'RevolverEject': cut(M,.72,.99),
    'RevolverRod': pitch(cut(M,1.03,1.20),.88),
    'RevolverLoad': B,
    'RevolverClose': cut(M,1.46,1.72),
    'RevolverAim': pitch(cut(C,3.75,4.09),.93),
    'SawPickup': mix((0,pitch(cut(C,3.72,4.14),.82),.65),(.13,cut(K,.61,.76),.25)),
    'SawOpen': pitch(cut(K,.60,.81),.75),
    'SawEject': mix((0,pitch(cut(M,.73,.98),.78),1),(.08,cut(C,1.04,1.22),.2)),
    'SawLoad': cut(L,.32,.93),
    'SawClose': pitch(cut(K,.82,1.10),.84),
    'SawAim': pitch(cut(C,3.75,4.09),.83),
    'TaserDraw': pitch(cut(C,3.73,4.10),1.06),
    'TaserSafety': cut(C,1.04,1.23),
    'BottleBreak': pitch(cut(G,0,1.02),.88),
    'GlassBreak': pitch(cut(G,0,.75),1.12),
}
for i, factor in enumerate([.97,1.0,1.025]):
    body = pitch(cut(S,0,.065),.83)
    clips['RevolverFire'+str(i)] = room(pitch(mix((0,R,.95),(.004,body,.16)),factor))
    clips['SawFire'+str(i)] = room(pitch(S,factor*.94))
    clips['TaserFire'+str(i)] = mix((0,cut(C,1.04,1.15),.10),(.012,pitch(cut(T,.75,1.10),factor),1),(.17,cut(T,1.66,1.90),.4))
# A restrained, regularly pulsed electrical contact bed, rather than a laser chirp.
arc = finish(cut(T,.80,.84), .35)
loop = np.zeros(int(RATE*.72))
for i in range(13):
    offset=int(i*.055*RATE)
    count=min(len(arc),len(loop)-offset)
    loop[offset:offset+count] += arc[:count]*(.8+.2*np.sin(i*2.4))
clips['TaserContactLoop'] = loop

report = {}
for name,a in clips.items():
    if name.endswith('Break'):
        transient=np.flatnonzero(np.abs(a)>np.max(np.abs(a))*.30)[0]
        a=a[max(0,transient-int(.006*RATE)):]
    a = finish(a, .70 if 'Fire' in name else .54, trim=name!='TaserContactLoop')
    pcm = np.clip(a*32767,-32767,32767).astype('<i2')
    dest = OUT / (name+'.wav')
    with wave.open(str(dest),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    # Stable import settings: short, mono, resident sounds with no runtime decode stall.
    guid = hashlib.md5(('quick-hands-weapon-audio/'+name).encode()).hexdigest()
    meta = dest.with_suffix('.wav.meta')
    if not meta.exists():
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  loadInBackground: 0
  preloadAudioData: 1
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf8')
    report[name] = {'seconds':round(len(a)/RATE,3),'peak':round(float(np.max(np.abs(a))),3),'bytes':dest.stat().st_size}
(CACHE/'built-clips.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(f'Built {len(report)} clips, {sum(r["bytes"] for r in report.values())/1024:.0f} KiB; mono 44.1kHz, no clipped samples.')
