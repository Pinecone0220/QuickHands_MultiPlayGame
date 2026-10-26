"""Apply the user's local Snake pack to revolver audio only.

Run after Build-WeaponAudio.py if regenerating the original weapon library.
Requires numpy/miniaudio; source recordings and existing Unity GUIDs are preserved.
"""
from pathlib import Path
import hashlib
import sys
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Temp/WeaponAudioTools'))
import miniaudio

PACK=ROOT/"Assets/Sounds/Snake's SECOND Authentic Gun Sounds"
MECHANISM=PACK/'& More/Revolver/32 SW Long/MP3'
OUT=ROOT/'Assets/Resources/WeaponAudio'
RATE=44100
mapping={
    'RevolverHammer':MECHANISM/'32 SW Hammer Cock.mp3',
    'RevolverUnlock':MECHANISM/'32 SW Open Cylinder Part 1.mp3',
    'RevolverOpen':MECHANISM/'32 SW Open Cylinder Part 2.mp3',
    'RevolverRod':MECHANISM/'32 SW Rotate Cylinder.mp3',
    'RevolverEject':MECHANISM/'32 SW Eject One Casing.mp3',
    'RevolverLoad':MECHANISM/'32 SW Single Load.mp3',
    'RevolverClose':MECHANISM/'32 SW Close Cylinder.mp3',
}
for variant in range(3):
    mapping[f'RevolverFire{variant}']=PACK/'Full Sounds/9mm/MP3/9mm Single.mp3'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def prepare(path,peak):
    decoded=miniaudio.decode_file(str(path),output_format=miniaudio.SampleFormat.FLOAT32,nchannels=1,sample_rate=RATE)
    data=np.array(decoded.samples,dtype=np.float64)
    if not len(data) or not np.isfinite(data).all():
        raise ValueError(f'Invalid source: {path}')
    level=float(np.max(np.abs(data)))
    if level<.0001:
        raise ValueError(f'Silent source: {path}')
    audible=np.flatnonzero(np.abs(data)>max(.0005,level*.008))
    data=data[max(0,audible[0]-round(.003*RATE)):min(len(data),audible[-1]+round(.06*RATE)+1)].copy()
    edge=min(round(.001*RATE),len(data)//4)
    data[:edge]*=np.linspace(0,1,edge);data[-edge:]*=np.linspace(1,0,edge)
    data*=peak/max(float(np.max(np.abs(data))),1e-9)
    return np.rint(data*32767).astype('<i2')

# Decode every requested recording before replacing any runtime asset.
prepared={name:prepare(source,.70 if 'Fire' in name else .54) for name,source in mapping.items()}
untouched={p:digest(p) for p in OUT.iterdir() if p.is_file() and (p.suffix=='.meta' or p.stem not in mapping)}
for name,pcm in prepared.items():
    with wave.open(str(OUT/f'{name}.wav'),'wb') as target:
        target.setnchannels(1);target.setsampwidth(2);target.setframerate(RATE);target.writeframes(pcm.tobytes())
    print(f'{name} <- {mapping[name].name} ({len(pcm)/RATE:.3f}s)')
assert all(digest(p)==before for p,before in untouched.items()),'Unrelated sound or import settings changed'
assert len({digest(OUT/f'RevolverFire{i}.wav') for i in range(3)})==1

preview=[]
for name in ['RevolverHammer','RevolverUnlock','RevolverOpen','RevolverRod','RevolverEject','RevolverLoad','RevolverLoad','RevolverClose','RevolverFire0']:
    preview.extend([prepared[name],np.zeros(round(.22*RATE),dtype='<i2')])
with wave.open(str(ROOT/'Docs/Previews/Revolver-Snake-Preview.wav'),'wb') as target:
    target.setnchannels(1);target.setsampwidth(2);target.setframerate(RATE);target.writeframes(np.concatenate(preview).tobytes())
print('PASS: 10 revolver clips updated; other sounds and Unity GUIDs preserved.')
