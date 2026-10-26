# Quick Hands 앱 아이콘 생성 기록

- 모드: 내장 image_gen.imagegen
- 생성일: 2026-10-26 (Asia/Seoul)
- 최종 자산: Assets/Art/Icons/QuickHands-AppIcon.png
- 프로젝트의 어두운 바·원형 테이블·리볼버·팔면체 주사위를 로우폴리 스타일로 표현했다. 문자를 넣지 않아 작은 실행 아이콘에 사용한다.

## 실제 생성 프롬프트

```text
Use case: logo-brand. Asset type: square desktop game/application icon, 1:1 composition. Create one polished icon for Quick Hands, a dark but playful low-poly 3D multiplayer party game about quick actions, tense shootouts and chance around a circular bar table. Main subject: a single bold, highly readable low-poly six-shot revolver, dark charcoal steel with a warm brass cylinder and walnut grip, in a strong diagonal three-quarter side view. Behind it, a simplified deep green round felt tabletop with a restrained antique-gold rim; near the grip, one small red faceted eight-sided die hints at party minigames and chance. Keep the revolver clearly dominant and silhouettes chunky and simple, designed to remain legible at 32 pixels. Stylized faceted 3D illustration, clean polygon planes, restrained material detail, dramatic warm amber overhead light, subtle bevel highlights and ambient shadows, deep green/near-black background, warm cream-gold accents, one muted red accent. Tight balanced centered emblem, generous safe margin, square full-bleed background, no surrounding UI or mockup. No text, no letters, no numbers on the die, no watermarks, no extra weapons, no blood, no intricate room scene. Deliver a finished premium indie PC game icon, not a poster, not an icon sheet.
```


## 프로젝트 적용·검증

- 원본:1254×1254 PNG, 불투명 배경. 파일 SHA256 `9b3a6363946fcf11243a09c34043e902debff362e501efff2edd7c458e40ba46`.
- Unity Texture2D/색상 텍스처로 가져오며 밉맵/압축/NPOT 크기 변경을 끈다. 생성 이미지를 임의로 다시 그리거나 수정하지 않았다.
- 기본 아이콘과 데스크톱1024/512/256/128/64/48/32/16 크기에 같은 원본을 연결했다. Unity가 빌드용 크기를 생성한다.
- 격리 Unity 프로젝트의 아이콘 참조 검사와 Windows 빌드 통과(`game-icon-final-build.log`). 실행 파일에서32×32 아이콘을 실제 추출해 확인했다(`Docs/Previews/Game-Icon-Windows.png`).
- 원본 프로젝트에는 아이콘 설정 블록만 반영했다. 작업 도중의 제품명/버전 등 기존 변경은 보존했다.
- 도구 `Tools/UnityQA/ApplyGameIcon.cs`. 최초 가져오기에서 자동 지정된 큐브맵 모양을 명시적인 Texture2D로 바로잡고 최종 검사를 통과했다. 네트워크/게임 규칙은 변경하지 않았다.
