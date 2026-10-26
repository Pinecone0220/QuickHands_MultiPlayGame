"""Retiming the user's heartbeat pulses without raising pitch; two-second one-shot."""
from pathlib import Path
import argparse
import hashlib
import json
import runpy
import sys
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Temp/WeaponAudioTools'))
import miniaudio

parser=argparse.ArgumentParser()
parser.add_argument('source',nargs='?',type=Path,default=Path.home()/'Downloads/shidenbeatsmusic-heartbeat-sound-effect-111218.mp3')
args=parser.parse_args()
RATE=44100
decoded=miniaudio.decode_file(str(args.source),output_format=miniaudio.SampleFormat.FLOAT32,nchannels=1,sample_rate=RATE)
pcm=np.asarray(decoded.samples,dtype=np.float32)
windows=[(.215,.475),(.565,.825)]
pulses=[]
for start,end in windows:
    pulse=pcm[round(start*RATE):round(end*RATE)].copy()
    fade=round(.006*RATE)
    pulse[:fade]*=np.linspace(0,1,fade);pulse[-fade:]*=np.linspace(1,0,fade)
    pulses.append(pulse)
heartbeat=np.zeros(2*RATE,dtype=np.float64)
# The original primary beat interval is about1.125s; reduce rests, keep each thump's timbre.
interval=.65
for beat in range(3):
    for pulse,offset in zip(pulses,[0,.20]):
        at=round((.08+beat*interval+offset)*RATE)
        heartbeat[at:at+len(pulse)]+=pulse
fade=round(.16*RATE);heartbeat[-fade:]*=np.linspace(1,0,fade)
heartbeat*=.68/max(float(np.max(np.abs(heartbeat))),1e-9)
assert len(heartbeat)==2*RATE and np.isfinite(heartbeat).all() and np.max(np.abs(heartbeat))<1
target=ROOT/'Assets/Resources/GameFeedback/LifeHeartbeat.wav'
with wave.open(str(target),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE)
    w.writeframes((heartbeat*32767).astype('<i2').tobytes())
stats_path=ROOT/'Docs/Audio/LifeFeedback.json'
stats=json.loads(stats_path.read_text(encoding='utf-8-sig')) if stats_path.exists() else {}
stats['LifeHeartbeat']={'source':args.source.name,'source_sha256':hashlib.sha256(args.source.read_bytes()).hexdigest(),
    'source_pulse_windows':windows,'beat_interval':interval,'beats_per_minute':round(60/interval,2),
    'beats':3,'pulse_pair_gap':.2,'pitch_preserved':True,'loop':False}
stats_path.write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
runpy.run_path(str(ROOT/'Tools/Build-LifeFeedbackAudio.py'))
