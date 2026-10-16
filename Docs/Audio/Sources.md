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
