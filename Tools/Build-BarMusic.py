"""Original procedural lounge score, 'Last Call'. No third-party music/samples.
Run with Python + numpy. Render circular note tails and room reflections for looping.
"""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Music/LastCall.wav'
RATE, BPM, BARS = 44100, 68, 32
BEAT = 60 / BPM
N = round(BARS * 4 * BEAT * RATE)
rng = np.random.default_rng(20261020)
mix = np.zeros((N, 2), dtype=np.float32)
events = []


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def add(signal, beat, gain, pan=0):
    # Fold releases across the end, instead of fading out at every repeat.
    start = round(beat * BEAT * RATE) % N
    signal = signal.astype(np.float32) * gain
    stereo = signal[:, None] * np.array([np.sqrt((1-pan)/2), np.sqrt((1+pan)/2)], dtype=np.float32)
    length = min(len(signal), N)
    first = min(length, N-start)
    mix[start:start+first] += stereo[:first]
    if first < length:
        mix[:length-first] += stereo[first:length]


def keys(note, hold, velocity):
    t = np.arange(round((hold+2.7)*RATE)) / RATE
    f = hz(note)
    release = np.exp(-np.maximum(0, t-hold)*3.3)
    attack = 1-np.exp(-t*115)
    # Soft tine attack, then a warm, slightly detuned fundamental.
    tine = np.sin(2*np.pi*f*t + .8*velocity*np.exp(-t*4)*np.sin(2*np.pi*2*f*t))
    body = .66*tine*np.exp(-t/2.7)
    body += .18*np.sin(2*np.pi*f*1.0012*t)*np.exp(-t/2.1)
    for partial, amp, decay in [(2,.10,.8),(3,.055,.45),(4,.018,.23)]:
        body += amp*np.sin(2*np.pi*f*partial*t)*np.exp(-t/decay)
    return body*attack*release


def bass(note, hold):
    t = np.arange(round((hold+.35)*RATE)) / RATE
    phase = 2*np.pi*hz(note)*t
    signal = np.sin(phase)*np.exp(-t/1.1)
    signal += .23*np.sin(phase*2+.1)*np.exp(-t/.55)
    signal += .07*np.sin(phase*3)*np.exp(-t/.24)
    return signal*(1-np.exp(-t*100))*np.exp(-np.maximum(0,t-hold)*15)


def brush(duration, bright=False):
    n = round(duration*RATE)
    noise = rng.normal(0,1,n)
    spectrum = np.fft.rfft(noise)
    frequencies = np.fft.rfftfreq(n,1/RATE)
    center = 4900 if bright else 2100
    spectrum *= np.exp(-.5*(np.log(np.maximum(frequencies,1)/center)/.62)**2)
    spectrum *= 1/(1+(frequencies/8500)**4)
    signal = np.fft.irfft(spectrum,n)
    signal /= max(np.max(np.abs(signal)),1e-8)
    t = np.arange(n)/RATE
    return signal*(1-np.exp(-t*130))*np.exp(-t/(duration*.27))


# Root and close, gently moving seventh/ninth voicings: ii-V-I and a minor turnaround.
changes = [
    ('Dm9',38,[53,57,60,64]), ('G13',43,[53,57,59,64]),
    ('Cmaj9',36,[52,55,59,62]), ('Fmaj9',41,[52,55,57,60]),
    ('Bm7b5',35,[53,57,59,62]), ('E7b9',40,[53,56,59,62]),
    ('Am9',33,[55,59,60,64]), ('A7b9',33,[55,58,61,64]),
]
# Sparse original phrases (offset in beats, MIDI note, duration in beats).
phrases = [
    [(2.6,69,.65),(3.4,72,.8),(6.5,71,1),(10.2,67,1.7),(13.2,64,1.2),(14.6,67,.8),(18.4,65,1.2),(21.4,64,.7),(22.5,68,.8),(26.4,71,.7),(27.4,69,1.2),(30.6,67,.8)],
    [(1.4,65,.8),(2.6,69,1.2),(6.3,64,1.4),(9.5,62,.65),(10.6,64,1.7),(14.3,60,1.5),(18.4,62,1.1),(22.5,59,1.1),(25.5,64,.8),(26.7,67,1.5),(30.5,61,.8)],
    [(2.5,72,.8),(3.5,69,1.2),(6.4,67,.8),(9.2,71,1.4),(11.2,67,.7),(14.4,69,1.3),(18.3,65,1.5),(22.3,68,1.4),(26.5,64,1.2),(30.4,67,1)],
    [(2.2,64,1.6),(6.4,65,.7),(7.4,64,.7),(10.3,62,1.3),(14.2,60,1.5),(18.4,62,1.4),(21.4,65,.7),(22.5,64,1.2),(26.4,60,1.6),(30.4,61,.9)],
]

for bar in range(BARS):
    chord, root, voicing = changes[bar % 8]
    base = bar*4
    # Loose timing and soft syncopation avoid a mechanical metronome feel.
    for offset, gain, hold in [(0.07,.105,1.55),(2.56,.065,1.05)]:
        if bar % 8 == 7 and offset > 2:
            continue
        for i,note in enumerate(voicing):
            when = base+offset+i*.012+rng.uniform(-.015,.015)
            add(keys(note,hold*BEAT,.65),when,gain*rng.uniform(.88,1.04),-.25+i*.13)
    add(bass(root,1.25*BEAT),base+.035,.115,-.12)
    fifth = root+7 if bar%8 != 4 else root+6
    add(bass(fifth,.95*BEAT),base+2.08,.085,-.12)
    if bar%4==3:
        next_root = changes[(bar+1)%8][1]
        add(bass(next_root-1,.35*BEAT),base+3.58,.043,-.12)
    for beat in range(4):
        add(brush(.21,True),base+beat+.07+rng.uniform(-.025,.025),.012 if beat%2==0 else .016,.32)
    for beat in [1.05,3.08]:
        add(brush(.43),base+beat+rng.uniform(-.025,.025),.026,-.3)
    events.append({'bar':bar+1,'chord':chord})

for chorus,phrase in enumerate(phrases):
    for offset,note,length in phrase:
        add(keys(note,length*BEAT,.45),chorus*32+offset,.085+rng.uniform(-.009,.009),.1)

# A small, dark room. Circular reflections retain the same ambience at the seam.
dry=mix.copy()
for delay,level in [(.073,.08),(.137,.065),(.233,.05),(.337,.038),(.487,.025),(.719,.015)]:
    reflected=np.roll(dry,round(delay*RATE),axis=0)
    mix += reflected[:,::-1]*level

# Gentle saturation and ample headroom; musical dynamics are retained.
mix=np.tanh(mix*1.15)
mix*=.43/max(float(np.max(np.abs(mix))),1e-8)
assert np.all(np.isfinite(mix))
pcm=(np.clip(mix,-1,1)*32767).astype('<i2')
OUT.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(OUT),'wb') as output:
    output.setnchannels(2);output.setsampwidth(2);output.setframerate(RATE);output.writeframes(pcm.tobytes())
report={'title':'Last Call / 마지막 잔','bpm':BPM,'bars':BARS,'seconds':N/RATE,
        'peak_dbfs':float(20*np.log10(np.max(np.abs(mix)))),
        'rms_dbfs':float(20*np.log10(np.sqrt(np.mean(mix**2)))),
        'loop_seam_delta':float(np.max(np.abs(mix[0]-mix[-1]))),
        'max_adjacent_delta':float(np.max(np.abs(np.diff(mix,axis=0)))),
        'score':events,'source':'Original generated score and synthesis; no external samples.'}
report_path=ROOT/'Docs/Audio/LastCall.json'
report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='score'},ensure_ascii=False))
