"""Rebuild metadata/preview from the imported heartbeat and single breath; keep both clips."""
from pathlib import Path
import json
import re
import uuid
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/GameFeedback'
RATE=44100

def save(path,data,peak):
    data=data.copy()
    n=round(.008*RATE)
    data[:n]*=np.linspace(0,1,n);data[-n:]*=np.linspace(1,0,n)
    data*=peak/max(np.max(np.abs(data)),1e-9)
    assert np.isfinite(data).all() and np.max(np.abs(data))<1
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes((data*32767).astype('<i2').tobytes())
    return data,{'duration':round(len(data)/RATE,4),'peak':round(float(np.max(np.abs(data))),4),
                 'rms':round(float(np.sqrt(np.mean(data*data))),4)}

stats_path=ROOT/'Docs/Audio/LifeFeedback.json'
stats=json.loads(stats_path.read_text(encoding='utf-8-sig')) if stats_path.exists() else {}
clips={}
for name in ['LifeHeartbeat','LifeGasp']:
    with wave.open(str(OUT/(name+'.wav')),'rb') as w:
        assert w.getnchannels()==1 and w.getsampwidth()==2 and w.getframerate()==RATE
        data=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(np.float64)/32767
    clips[name]=data
    stats.setdefault(name,{}).update(duration=round(len(data)/RATE,4),peak=round(float(np.max(np.abs(data))),4),rms=round(float(np.sqrt(np.mean(data*data))),4))
template=(OUT/'CountdownStart.wav.meta').read_text(encoding='utf-8-sig')
for name in stats:
    meta=OUT/(name+'.wav.meta')
    if not meta.exists():
        meta.write_text(re.sub(r'guid: [a-f0-9]+','guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'roulette/life/'+name).hex,template),encoding='utf-8')
preview=np.concatenate([clips['LifeHeartbeat'],np.zeros(round(.35*RATE)),clips['LifeGasp']])
save(ROOT/'Docs/Previews/Life-Recovery-Audio.wav',preview,.72)
(ROOT/'Docs/Audio/LifeFeedback.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
print(json.dumps(stats))
