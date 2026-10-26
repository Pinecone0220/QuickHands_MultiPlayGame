"""Apply the user's chosen Snake pack recordings to sawed-off audio only.

Run after Build-WeaponAudio.py when regenerating the original library.
Requires numpy/miniaudio; original recordings and Unity GUIDs are preserved.
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
OUT=ROOT/'Assets/Resources/WeaponAudio'
RATE=44100
mapping={
    'SawLoad':ROOT/"Assets/Sounds/Snake's Authentic Gun Sounds/Reloads, Cycling & More/MP3/Pump Shell Load MP3.mp3",
    'SawOpen':PACK/'& More/Bipod/MP3/Bipod Impact One Leg.mp3',
}
for variant in range(3):
    mapping[f'SawFire{variant}']=PACK/'Full Sounds/20 Gauge/MP3/20 Gauge Single.mp3'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def prepare(path,peak):
    decoded=miniaudio.decode_file(str(path),output_format=miniaudio.SampleFormat.FLOAT32,nchannels=1,sample_rate=RATE)
    data=np.array(decoded.samples,dtype=np.float64)
    level=float(np.max(np.abs(data))) if len(data) else 0
    if not np.isfinite(data).all() or level<.0001:
        raise ValueError(f'Invalid or silent source: {path}')
    audible=np.flatnonzero(np.abs(data)>max(.0005,level*.008))
    data=data[max(0,audible[0]-round(.003*RATE)):min(len(data),audible[-1]+round(.06*RATE)+1)].copy()
    edge=min(round(.001*RATE),len(data)//4)
    data[:edge]*=np.linspace(0,1,edge);data[-edge:]*=np.linspace(1,0,edge)
    data*=peak/max(float(np.max(np.abs(data))),1e-9)
    return np.rint(data*32767).astype('<i2')

prepared={name:prepare(source,.70 if 'Fire' in name else .54) for name,source in mapping.items()}
untouched={p:digest(p) for p in OUT.iterdir() if p.is_file() and (p.suffix=='.meta' or p.stem not in mapping)}
for name,pcm in prepared.items():
    with wave.open(str(OUT/f'{name}.wav'),'wb') as target:
        target.setnchannels(1);target.setsampwidth(2);target.setframerate(RATE);target.writeframes(pcm.tobytes())
    print(f'{name} <- {mapping[name].name} ({len(pcm)/RATE:.3f}s)')
assert all(digest(p)==before for p,before in untouched.items()),'Unrelated sound or import settings changed'
assert len({digest(OUT/f'SawFire{i}.wav') for i in range(3)})==1

preview=[]
for name in ['SawOpen','SawLoad','SawFire0']:
    preview.extend([prepared[name],np.zeros(round(.35*RATE),dtype='<i2')])
with wave.open(str(ROOT/'Docs/Previews/SawedOff-Snake-Preview.wav'),'wb') as target:
    target.setnchannels(1);target.setsampwidth(2);target.setframerate(RATE);target.writeframes(np.concatenate(preview).tobytes())
print('PASS: 5 sawed-off clips updated; other sounds and Unity GUIDs preserved.')
