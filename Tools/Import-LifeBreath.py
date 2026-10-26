"""Use one breath from the user's recording; preserve the runtime clip GUID."""
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
parser.add_argument('source',nargs='?',type=Path,default=Path.home()/'Downloads/freesound_community-heavy-breath-wheezing-19801.mp3')
args=parser.parse_args()
RATE=44100
decoded=miniaudio.decode_file(str(args.source),output_format=miniaudio.SampleFormat.FLOAT32,nchannels=1,sample_rate=RATE)
pcm=np.asarray(decoded.samples,dtype=np.float32)
start,end=.65,1.90
breath=pcm[round(start*RATE):round(end*RATE)].copy()
assert len(breath)==round((end-start)*RATE) and np.isfinite(breath).all()
fade=round(.008*RATE)
breath[:fade]*=np.linspace(0,1,fade);breath[-fade:]*=np.linspace(1,0,fade)
breath*=.72/max(float(np.max(np.abs(breath))),1e-9)
target=ROOT/'Assets/Resources/GameFeedback/LifeGasp.wav'
with wave.open(str(target),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE)
    w.writeframes((breath*32767).astype('<i2').tobytes())
stats_path=ROOT/'Docs/Audio/LifeFeedback.json'
stats=json.loads(stats_path.read_text(encoding='utf-8-sig')) if stats_path.exists() else {}
stats['LifeGasp']={'source':args.source.name,'source_sha256':hashlib.sha256(args.source.read_bytes()).hexdigest(),
                   'source_start':start,'source_end':end,'single_breath':True}
stats_path.write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
runpy.run_path(str(ROOT/'Tools/Build-LifeFeedbackAudio.py'))
