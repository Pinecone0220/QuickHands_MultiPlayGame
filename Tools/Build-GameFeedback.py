"""Synthesized UI/party cues plus community-distributed Overwatch hit audio.

Requires numpy and miniaudio. Third-party hit source: Docs/Audio/Sources.md.
"""
from pathlib import Path
import hashlib
import urllib.request
import wave
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/GameFeedback'
RATE=44100
rng=np.random.default_rng(20261020)

def sequence(length,notes):
    data=np.zeros(round(length*RATE))
    for at,freq,gain,decay in notes:
        start=round(at*RATE);t=np.arange(len(data)-start)/RATE
        data[start:]+=(np.sin(2*np.pi*freq*t)+.18*np.sin(2*np.pi*freq*2.76*t))*np.exp(-t/decay)*(1-np.exp(-t*1100))*gain
    return data

def save(name,data,peak=.55,fade=.005):
    n=min(round(fade*RATE),len(data)//4)
    data[:n]*=np.linspace(0,1,n);data[-n:]*=np.linspace(1,0,n)
    data*=peak/max(np.max(np.abs(data)),1e-9)
    assert np.isfinite(data).all()
    with wave.open(str(OUT/(name+'.wav')),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE)
        f.writeframes((np.clip(data,-1,1)*32767).astype('<i2').tobytes())

OUT.mkdir(parents=True,exist_ok=True)
save('UiClick',sequence(.065,[(.002,900,1,.009),(.004,1850,.2,.005)]),.4)
save('KeyTap',sequence(.07,[(.002,640,1,.015),(.003,1280,.16,.008)]),.42)
save('KeyError',sequence(.19,[(.002,240,1,.04),(.075,180,.75,.035)]),.45)
save('CountdownTick',sequence(.10,[(.002,660,.8,.022),(.002,1320,.12,.012)]),.42)
save('CountdownStart',sequence(.24,[(.002,990,.7,.055),(.035,1320,.32,.05)]),.52)
save('ClockLoop',sequence(.5,[(.008,1350,1,.009),(.258,1010,.75,.011)]),.45)
save('ClockConfirm',sequence(.23,[(.002,710,.7,.04),(.065,1065,.55,.055)]),.5)
save('SignalWait',sequence(.19,[(.002,330,1,.035)]),.43)
save('SignalFake',sequence(.24,[(.002,660,1,.028),(.1,587,.65,.025)]),.45)
save('SignalGo',sequence(.26,[(.002,880,.8,.055),(.085,1320,.7,.055)]),.55)
save('GreedLoop',sequence(.4,[(.015,1480,.6,.016),(.235,1760,.35,.012)]),.35)
save('GreedConfirm',sequence(.38,[(.002,660,.7,.07),(.085,825,.65,.08),(.17,990,.6,.075)]),.55)
t=np.arange(round(.68*RATE))/RATE
noise=np.convolve(rng.normal(0,1,len(t)),np.ones(7)/7,mode='same')*np.exp(-t*19)
burst=(np.sin(2*np.pi*(130*t-58*t*t))*np.exp(-t*11)*.7+noise*.3)*(1-np.exp(-t*700))
burst+=sequence(.68,[(.025,890,.18,.06),(.075,570,.15,.06),(.14,330,.13,.07)])
save('GreedBurst',burst,.6)

def overwatch_hit():
    import miniaudio
    source=ROOT/'Temp/HitFeedbackSources/OverwatchHit.ogg'
    url=('https://raw.githubusercontent.com/DrAbcOfficial/HitSound/'
         'dd1a312fdd2de6597782cf793795eb3d9dd81f22/'
         'Contents/mods/HitSound/42/media/sound/HitSound/overwatch/hit.ogg')
    if not source.exists():
        source.parent.mkdir(parents=True,exist_ok=True)
        with urllib.request.urlopen(url,timeout=30) as response:
            data=response.read()
        if hashlib.sha256(data).hexdigest()!='e328fa1b16a9dad4caab785691172b1a34b21624b77df2b3e0affd715b45bf6c':
            raise ValueError('Unexpected Overwatch hit source checksum')
        source.write_bytes(data)
    if hashlib.sha256(source.read_bytes()).hexdigest()!='e328fa1b16a9dad4caab785691172b1a34b21624b77df2b3e0affd715b45bf6c':
        raise ValueError('Cached Overwatch hit source checksum mismatch')
    decoded=miniaudio.decode_file(str(source),output_format=miniaudio.SampleFormat.FLOAT32,nchannels=1,sample_rate=RATE)
    return np.array(decoded.samples,dtype=np.float64)

hit=overwatch_hit()
head=np.interp(np.arange(0,len(hit)-1,1.35),np.arange(len(hit)),hit)
save('HitConfirm',hit.copy(),.60,fade=.0005)
save('HitHead',head,.64,fade=.0005)
print('Rendered 13 synthesized cues and 2 Overwatch-based hit cues.')
