# 총기 효과음 출처

2026-10-15. 아래 원음은 모두 게시 페이지에 CC0로 표시된 공개 녹음이다. 게임에 맞춰 구간 분리, 모노 변환, 레벨 정리, 피치 조정, 레이어/짧은 실내 잔향을 편집했다. 실제 게임 총기의 원본 녹음을 재현한 시뮬레이터라는 뜻은 아니다.

| 원음 / 제작자 | 사용 |
| --- | --- |
| [Clean Revolver Reload / Dredile (Avery Fisk)](https://freesound.org/people/Dredile/sounds/177863/) | 리볼버 격철·게이트·이젝터·탄피 및 소드오프 탄피 레이어. 소품 리볼버와 탄피 녹음 |
| [Gunshot Revolver 22.wav / Bane_Sound](https://freesound.org/people/Bane_Sound/sounds/542549/) | 리볼버 발사음3종의 기본 레이어 |
| [Gun Reload Sound Effects / BMacZero, Brian MacIntosh](https://opengameart.org/content/gun-reload-sound-effects) | `singlebullet1.wav`: 리볼버 한 발 삽입 |
| [Shotgun Reload Sound effects / zer0_sol](https://opengameart.org/content/shotgun-reload-sound-effects) | `Rack`, `First Shell`: 배럴 개폐의 금속 부분과 탄 삽입을 분리. 펌프 장전 전체 소리를 그대로 사용하지 않음 |
| [Gunshot Sounds / Tabasco](https://opengameart.org/content/gunshot-sounds) | `shotty.wav`: 소드오프 발사음3종, 리볼버 저역 레이어 |
| [Equipment Clicks II / LFA](https://opengameart.org/content/equipment-clicks-ii) | 총 집기·조준 자세 전환·테이저 안전 스위치의 짧은 조작 레이어 |
| [Taser/High Voltage discharge / The_Chemical_Workshop](https://freesound.org/people/The_Chemical_Workshop/sounds/403252/) | 실제 고전압 스파크 녹음. 테이저 방전3종 및 작은 감전 접촉 루프 |
| [Breaking a glass bottle / dasebr](https://freesound.org/people/dasebr/sounds/212698/) | 병·잔 파손음. 병은 낮고 무겁게, 잔은 짧고 가볍게 편집 |

라이선스: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). 제작자에게 연락하거나 메시지를 발송하지 않았다. Freesound는 로그인 없는 공개 HQ 미리듣기 파일을 편집 원본으로 사용했다.

`Assets/Resources/WeaponAudio`의29개 짧은 파일은 모노44.1kHz/16bit WAV이며 전체 약1.1MiB다. `Tools/Build-WeaponAudio.py`로 재생성한다. Python의 numpy/miniaudio가 필요하며 원본은 `Temp/WeaponAudioSources`에 캐시된다. 실행 파일에는 편집된 WAV만 포함하고 원본 압축 묶음/개발 도구는 넣지 않는다.

미리듣기 순서: 리볼버 조작/사격→소드오프 조작/사격→테이저 방전/감전→잔 파손. [13초 미리듣기](Weapon-Sound-Preview.wav). 게임의 실제 위치 감쇠/동시 혼합은 이 파일과 별도다. 병·잔 파손음은 강한 파열 직전6ms부터 편집해 명중 순간에 바로 들리도록 했다.

## 로비 입장·퇴장 알림 (2026-10-16)
사용자 제공 파일을 그대로 사용한다. 기존 총기 CC0 출처 목록과 별개의 첨부 음원이다.
- 입장: dragon-studio-opening-door-450444.mp3 → Resources/UIAudio/LobbyJoin.mp3
- 퇴장: dragon-studio-close-door-382723.mp3 → Resources/UIAudio/LobbyLeave.mp3
이전에 제공한 ringtone 파일은 입장음에서 교체했다.

## 2026-10-16 FreeWeaponSFX 동작음 교체
사용자가 제공한 Assets/Sounds/FreeWeaponSFX_v1.1의 원본을 변경하지 않고 Resources/WeaponAudio에 아래 이름으로 복사해 빌드에 포함한다. 이 묶음은 위 CC0 녹음 목록과 별개다. 테이저/유리 파손음은 기존 음원을 사용한다.

| 런타임 이름 | FreeWeaponSFX 원본 |
|---|---|
| RevolverPickup | Handgun/Foley/sfx_wpn_hgn_foley_equip_01.wav |
| RevolverHammer | Handgun/Reload/sfx_wpn_hgn_reload_05_charge.wav |
| RevolverUnlock | Handgun/Reload/sfx_wpn_hgn_reload_01_slideLock.wav |
| RevolverOpen | Handgun/Reload/sfx_wpn_hgn_reload_02_magOut.wav |
| RevolverRod | Shotgun/Reload/sfx_wpn_sgn_reload_03_shellDraw.wav |
| RevolverEject | GrenadeLauncher/sfx_wpn_gl_reload_02_casingExtract.wav |
| RevolverLoad | Shotgun/Reload/sfx_wpn_sgn_reload_04_chambering.wav |
| RevolverClose | Handgun/Reload/sfx_wpn_hgn_reload_04_magIn.wav |
| RevolverAim | Handgun/Foley/sfx_wpn_hgn_foley_aim_01.wav |
| SawPickup | Shotgun/Foley/sfx_wpn_sgn_foley_equip_01.wav |
| SawOpen | GrenadeLauncher/sfx_wpn_gl_reload_01_breechOpen.wav |
| SawEject | GrenadeLauncher/sfx_wpn_gl_reload_02_casingExtract.wav |
| SawLoad | Shotgun/Reload/sfx_wpn_sgn_reload_04_chambering.wav |
| SawClose | GrenadeLauncher/sfx_wpn_gl_reload_04_breechClose.wav |
| SawAim | Shotgun/Foley/sfx_wpn_sgn_foley_aim_01.wav |
| RevolverFire0 | Handgun/Gunshots/sfx_wpn_hgn_gunshot_01.wav |
| SawFire0 | Shotgun/Gunshots/sfx_wpn_sgn_gunshot_01.wav |
| RevolverFire1 | Handgun/Gunshots/sfx_wpn_hgn_gunshot_02.wav |
| SawFire1 | Shotgun/Gunshots/sfx_wpn_sgn_gunshot_02.wav |
| RevolverFire2 | Handgun/Gunshots/sfx_wpn_hgn_gunshot_03.wav |
| SawFire2 | Shotgun/Gunshots/sfx_wpn_sgn_gunshot_03.wav |

## 2026-10-21 리볼버 Snake 팩 교체

사용자 제공 `Assets/Sounds/Snake's SECOND Authentic Gun Sounds`의 MP3를 사용했다. 이 묶음은 앞의 CC0 녹음 및 FreeWeaponSFX와 별개다. `Tools/Import-RevolverAudio.py`가 아래10개 런타임 WAV를 생성한다. 원본의 선행/후행 무음을 정리하고44.1kHz/16bit 모노 변환, 조작 피크0.54/발사0.70과 양끝1ms 페이드를 적용한다. 기존 Unity GUID/재생 이벤트를 보존한다. 이전 `Build-WeaponAudio.py`를 실행했다면 이 가져오기 스크립트를 마지막에 실행한다(numpy/miniaudio 필요).

조작 원본 폴더: `& More/Revolver/32 SW Long/MP3`.

| 런타임 이름 | 원본 파일 | 동작 |
|---|---|---|
| RevolverHammer | 32 SW Hammer Cock.mp3 | 격철 당기기 완료 |
| RevolverUnlock | 32 SW Open Cylinder Part 1.mp3 | 게이트 잠금 해제 완료 |
| RevolverOpen | 32 SW Open Cylinder Part 2.mp3 | 게이트 열기 완료 |
| RevolverRod | 32 SW Rotate Cylinder.mp3 | 탄피 제거 단계의 약실 회전 |
| RevolverEject | 32 SW Eject One Casing.mp3 | 사용한 탄피마다 배출 |
| RevolverLoad | 32 SW Single Load.mp3 | 한 발씩 장전 완료 |
| RevolverClose | 32 SW Close Cylinder.mp3 | 조준으로 전환할 때 게이트 닫기 |
| RevolverFire0/1/2 | Full Sounds/9mm/MP3/9mm Single.mp3 | 세 슬롯 모두 지정한 같은 단발 발사음 |

Open Cylinder Full은 Part1/2와 중복되므로 사용하지 않고, Hammer Decock은 대응 동작이 없어 사용하지 않는다. 집기/조준 자세에는 이 폴더에 대응 음원이 없어 기존 폴리를 유지한다. 스크립트는 나머지 소드오프/테이저/유리 음원과 모든 .meta의 해시가 유지되는지 검사한다. 조작/탄약/피해/프로토콜44는 그대로다. 조작→두 발 삽입→닫기→발사 미리듣기: `Docs/Previews/Revolver-Snake-Preview.wav`.

## 2026-10-22 소드오프 지정 음원 교체

같은 사용자 제공 Snake 팩의 지정 파일3개를 `Tools/Import-SawedOffAudio.py`로 런타임 WAV5개에 적용한다. 변환은 리볼버와 동일한44.1kHz/16bit 모노, 조작 피크0.54/격발0.70, 무음 정리/양끝1ms 페이드다. 기존 GUID와 동작 완료/발사 재생 이벤트를 재사용한다.

| 런타임 이름 | Snake 팩 내 원본 |
|---|---|
| SawLoad | & More/Mag Pack/MP3/Single Shotgun Load.mp3 |
| SawOpen | & More/Bipod/MP3/Bipod Impact One Leg.mp3 |
| SawFire0/1/2 | Full Sounds/20 Gauge/MP3/20 Gauge Single.mp3 |

격발 세 슬롯은 같은 지정 단발음이다. 나머지 리볼버/테이저/소드오프 조작 음원과 .meta는 해시로 보존을 확인한다. 원본 라이브러리를 재생성할 때는 `Build-WeaponAudio.py` 뒤에 각 무기별 Import 스크립트를 실행한다. 미리듣기는 배럴 열기→탄 삽입→격발 순서의 `Docs/Previews/SawedOff-Snake-Preview.wav`다.

## 배경음악 (2026-10-20)
Last Call / 마지막 잔: Tools/Build-BarMusic.py에서 작곡한 코드 진행·선율과 절차적 악기 합성으로 생성. 외부 음악 샘플 없음. 상세는 Docs/Audio/LastCall.md 참조.

## UI/미니게임 효과음 (2026-10-20)
Tools/Build-GameFeedback.py에서 외부 샘플 없이 11종을 합성했고, 2026-10-21에 CountdownTick(0.10초)/CountdownStart(0.24초) 자체 합성2종을 추가해13종이다. 대응 이벤트와 재생 정책은 Docs/Audio/GameFeedback.md 참조.

## 명중 확인음 (2026-10-21)
사용자가 오버워치와 같은 소리를 요청해 기존 합성음을 외부 음원으로 교체했다. 출처: [DrAbcOfficial/HitSound의 Overwatch hit.ogg](https://github.com/DrAbcOfficial/HitSound/blob/dd1a312fdd2de6597782cf793795eb3d9dd81f22/Contents/mods/HitSound/42/media/sound/HitSound/overwatch/hit.ogg). 커뮤니티 배포본이므로 블리자드 원본과 바이트 동일 여부는 별도 검증하지 않았다. 이 음원은 위 CC0 녹음 및 자체 합성11종과 별개인 제3자 게임 음원이며 해당 저장소에서 재배포 라이선스를 확인하지 못했다.

고정 커밋: `dd1a312fdd2de6597782cf793795eb3d9dd81f22`. 원본 SHA256: `e328fa1b16a9dad4caab785691172b1a34b21624b77df2b3e0affd715b45bf6c`. `Temp/HitFeedbackSources/OverwatchHit.ogg`에 캐시하며 Build-GameFeedback.py가 검증 후 변환한다(numpy/miniaudio 필요). 일반음은 피크0.60/0.0377초, 머리는 같은 음원을1.35배 높인 피크0.64/0.0279초 파생음이며 실제 오버워치 헤드샷 추출음은 아니다. 출력은44.1kHz/16bit 모노 PCM, 양끝0.5ms 클릭 방지 페이드다. `Resources/GameFeedback`에 포함하며 미리듣기는 `Docs/Previews/HitFeedback-Preview.wav`다.

## BAS 및 소드오프 장전음 (2026-10-22 최신)
`Tools/Import-BasAudio.py`가 사용자 소유 `Snake's Authentic Gun Sounds` 팩에서 다음 녹음을 변환한다. BAS의 조준/집기 음은 기존 리볼버 녹음을 재사용한다. 기존 총기 WAV/GUID를 유지하고 소드오프의 `SawLoad`만 지정한 새 장전 녹음으로 교체한다.

| 런타임 | 원본 (Assets/Sounds/Snake's Authentic Gun Sounds 아래) |
|---|---|
| BasFire0/1/2 | Full Sound/7.62x54R/MP3/762x54r Single MP3.mp3 |
| BasBolt (열기/닫기 완료) | Reloads, Cycling & More/MP3/Semi 22LR Rack MP3.mp3 |
| BasLoad / SawLoad | Reloads, Cycling & More/MP3/Pump Shell Load MP3.mp3 |

44.1kHz/16bit 모노 PCM, 무음 여백만 트림/1ms 페이드/발사 피크0.70·조작0.54. 원본 파일은 수정하지 않는다. 위의 과거 `Single Shotgun Load` 지정은 이 항목으로 대체한다. BAS 미리듣기: Docs/Previews/BAS-Audio.wav. 기존 팩의 라이선스 기록을 따른다.
