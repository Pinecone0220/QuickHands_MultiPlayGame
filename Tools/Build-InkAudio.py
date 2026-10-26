"""Original short cloth/air throw and wet ink-pouch contact; numpy only."""
from pathlib import Path
import json
import re
import uuid
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/GameFeedback'
RATE=44100
rng=np.random.default_rng(2026102602)

def air(length,center,width):
    n=round(length*RATE);f=np.fft.rfftfreq(n,1/RATE)
    shape=np.exp(-((f-center)/width)**2)*(1-np.exp(-(f/170)**2))
    noise=np.fft.irfft(np.fft.rfft(rng.normal(0,1,n))*shape,n=n)
    return noise/max(np.std(noise),1e-9)

def save(path,data,peak):
    data=data.copy();n=round(.006*RATE)
    data[:n]*=np.linspace(0,1,n);data[-n:]*=np.linspace(1,0,n)
    data*=peak/max(np.max(np.abs(data)),1e-9)
    assert np.isfinite(data).all() and np.max(np.abs(data))<1
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes((data*32767).astype('<i2').tobytes())
    return {'duration':round(len(data)/RATE,4),'peak':peak,'rms':round(float(np.sqrt(np.mean(data*data))),4)}

t=np.arange(round(.24*RATE))/RATE
wind=air(.24,1450,1500)*np.exp(-((t-.095)/.055)**2)
wind+=np.sin(2*np.pi*(180*t+90*t*t))*np.exp(-((t-.105)/.04)**2)*.10
t=np.arange(round(.36*RATE))/RATE
splash=air(.36,1700,2200)*(1-np.exp(-t*1100))*np.exp(-t*42)*.45
for at,frequency,gain in [(0,180,.65),(.018,290,.42),(.047,420,.30),(.083,240,.22),(.14,530,.10)]:
    tau=np.maximum(t-at,0);envelope=(t>=at)*(1-np.exp(-tau*1500))*np.exp(-tau*55)
    phase=2*np.pi*(frequency*tau+190/42*(1-np.exp(-tau*42)))
    splash+=np.sin(phase)*envelope*gain
stats={'InkThrow':save(OUT/'InkThrow.wav',wind,.40),'InkImpact':save(OUT/'InkImpact.wav',splash,.62)}
template=(OUT/'UiClick.wav.meta').read_text(encoding='utf-8-sig')
for name in stats:
    meta=OUT/(name+'.wav.meta')
    if not meta.exists():meta.write_text(re.sub(r'guid: [a-f0-9]+','guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'roulette/ink-audio/'+name).hex,template),encoding='utf-8')
save(ROOT/'Docs/Previews/Ink-Audio.wav',np.concatenate([wind,np.zeros(round(.22*RATE)),splash]),.62)
(ROOT/'Docs/Audio/InkAudio.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
print(json.dumps(stats))
