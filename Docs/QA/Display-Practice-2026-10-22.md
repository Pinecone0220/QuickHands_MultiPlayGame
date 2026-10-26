# 준비 취소 흔들림·전체화면 렌더 해상도 수정

2026-10-22 / Unity6000.3.18f1 / NGO2.13.3 / 프로토콜48 유지.

준비를 취소하면 서버는 연습 상태를 종료하지만 Action=Aim을 유지한다. 기존 로컬 Rotation은 행동만 검사해 무기 흔들림이 계속됐다. 실제 연습 여부 또는 현재 Racing의 QuickHands/Shooting·생존 여부를 먼저 검사하도록 수정했다. 준비 취소 즉시 기본 시선으로 돌아가고, 다시 준비하면 기존 흔들림을 적용한다. 무기별 수치·서버 판정은 바꾸지 않았다.

전체화면은 이전에 창 모드만 바꾸고 렌더 해상도를 지정하지 않았다. 현재 창이 위치한 모니터의 픽셀 크기로 Screen.SetResolution(width,height,FullScreenWindow)을 요청한다. 해제 시 전환 직전 창 크기를 복원하고, 저장된 크기 없이 처음부터 전체화면인 경우 모니터의75%를 사용한다. UI/카메라 비율은 실제 렌더 크기에 따라 갱신되며 강제로 고정하지 않는다. Unity 공식 [SetResolution](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen.SetResolution.html), [mainWindowDisplayInfo](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen-mainWindowDisplayInfo.html)를 참고했다. 해상도 반영은 프레임 종료 이후 검사한다.

`Tools/UnityQA/DisplayPracticeRuntimeQA.cs`로 Windows 플레이어 빌드와 실제 그래픽 실행43개 검사를 통과했다.

- 메인화면 일반 클릭: 960×720 창→1920×1080 전체화면→960×720 창 복원.
- 로비/준비 연습 Alt 클릭: 1000×700 창→1920×1080 전체화면→1000×700 창 복원.
- 각 전환의 Screen 크기/모드, Camera.pixelWidth/Height, 카메라 종횡비, UI 루트 종횡비, 버튼 문구를 확인했다.
- 미준비 무흔들림→준비 연습 흔들림→취소 후 Aim 잔류 상태에서6개 시각 표본 모두 무흔들림→재준비 시 흔들림 복원을 확인했다.
- 전체화면 PNG를 직접 시각 검토했다. [메인화면](../Previews/Fullscreen-Menu.png), [준비 연습](../Previews/Fullscreen-Practice.png).

증거: `%TEMP%/RouletteQA-bb1739d9/display-practice-build.log`의 PASS chat build, `DisplayPractice/done`의 PASS display/practice: 43 checks, `display-practice-runtime.log`. 검증은 실제 Windows 실행 파일/단일1920×1080 모니터에서 수행했다. Editor GameView/별도 테스트 플레이어와 다중 모니터 이동은 이번 검사에 포함하지 않았다. 네트워크 형식/권한/서버 로직은 변경하지 않았다.
