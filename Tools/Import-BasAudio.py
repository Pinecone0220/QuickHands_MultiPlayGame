"""Import the requested BAS recordings and the shared Pump Shell Load for sawed-off."""
from pathlib import Path
import runpy
import re
import uuid
import shutil
import wave
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
shared=runpy.run_path(str(ROOT/'Tools/Import-SawedOffAudio.py'))
prepare,RATE=shared['prepare'],shared['RATE']
OUT=ROOT/'Assets/Resources/WeaponAudio'
PACK=ROOT/"Assets/Sounds/Snake's Authentic Gun Sounds"
mapping={
    'BasBolt':PACK/'Reloads, Cycling & More/MP3/Semi 22LR Rack MP3.mp3',
    'BasLoad':PACK/'Reloads, Cycling & More/MP3/Pump Shell Load MP3.mp3',
}
for i in range(3):mapping[f'BasFire{i}']=PACK/'Full Sound/7.62x54R/MP3/762x54r Single MP3.mp3'
template=(OUT/'SawLoad.wav.meta').read_text(encoding='utf-8-sig')
def metadata(path):
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        guid=uuid.uuid5(uuid.NAMESPACE_URL,'roulette/WeaponAudio/'+path.name).hex
        meta.write_text(re.sub(r'guid: [a-f0-9]+','guid: '+guid,template),encoding='utf-8')
preview=[]
for name,source in mapping.items():
    pcm=prepare(source,.70 if 'Fire' in name else .54)
    target=OUT/f'{name}.wav'
    with wave.open(str(target),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())
    metadata(target)
    print(f'{name} <- {source.name} ({len(pcm)/RATE:.3f}s)')
    if name in ('BasBolt','BasLoad','BasFire0'):preview.extend([pcm,np.zeros(round(.35*RATE),dtype='<i2')])
for name,original in [('BasPickup','RevolverPickup'),('BasAim','RevolverAim')]:
    target=OUT/f'{name}.wav';shutil.copyfile(OUT/f'{original}.wav',target);metadata(target)
with wave.open(str(ROOT/'Docs/Previews/BAS-Audio.wav'),'wb') as w:
    w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(np.concatenate(preview).tobytes())
assert (OUT/'BasLoad.wav').read_bytes()==(OUT/'SawLoad.wav').read_bytes()
print('PASS BAS clips imported; sawed-off shares the requested loading sound.')
