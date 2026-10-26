# 목숨 차감 화면·음향 검증

2026-10-26 / Unity6000.3.18f1 / NGO2.13.3 / 프로토콜52 유지.

소유자의 확정된 DiedAt/LivesRemaining/ReviveAt/Life만 읽는다.0.3초 흐림/18% 암전과 리스너 저역통과850Hz·작은 잔향, 짧은 심장박동을 적용한다. 목숨 × N 텍스트는0.25초 뒤 페이드인하고 숫자만0.65~1.30초에 아래로 스크롤한 뒤1.7~2.2초에 페이드아웃한다.2목숨 차감도 중간 숫자를 거친다. 서버 ReviveAt에 들숨을1회 재생하고0.75초 안에 화면·음향을 복구하며 기존1.3초 일어나기를 유지한다. 영구 탈락/최종 결과는2.35초 내 정상화하고 최종 승리 UI가 목숨 텍스트에 우선한다. 재경기/퇴장/비활성화 때 효과와 오디오 필터를 정리한다.

- Windows 개발 빌드 성공: 격리 프로젝트 `C:/Users/PC/AppData/Local/Temp/RouletteQA-bb1739d9`, `life-feedback-build.log`. Resources VolumeProfile을 포함해 빌드의 URP DOF 셰이더 제거를 방지했다.
- 실제 NGO 호스트17개·원격 클라이언트11개 통과: 서버의 HP0/목숨 차감 전달, 소유자만 먹먹함/화면 효과/심장박동,3→2 및2→0 숫자, 실제 복귀 시작 때1회 숨소리, 복귀 완료/관전/퇴장 정리, 타인 사망이 본인 효과에 영향 없음.
- 그래픽18개 통과: URP 카메라 스택의 Gaussian 모드, 중앙 숫자/페이드, 실제 일어나기와 정상화, 최종 패널 우선/메뉴 복귀.1200×800의 차감/스크롤/복귀 캡처 및 고정 카메라의 흐림 전후를 직접 검사했다.
- 오디오 리스너 출력의4kHz 시험 신호 진폭을 측정했다. 정상0.0041269 → 먹먹함0.00000916으로 감소해 파라미터만 설정된 것이 아닌 실제 출력 필터 적용을 확인했다. 측정용 음원/음소거는 QA에서만 사용한다.
- 자체 합성 WAV: 심장박동2.55초/피크0.68, 들숨1.30초/피크0.72,44.1kHz/16bit 모노·유한 샘플·클리핑 없음. 도구 `Tools/Build-LifeFeedbackAudio.py`, 미리듣기 `Docs/Previews/Life-Recovery-Audio.wav`, 수치 `Docs/Audio/LifeFeedback.json`.

검증 도구: `Tools/UnityQA/LifeFeedbackRuntimeQA.cs`. 로그 `life-feedback-{host,client}.log`, `life-feedback-preview-verified.log`; 마커 `Life-Feedback-Verified`, `Life-Feedback-Final-Verified`. 호스트/원격 실행 게임 예외0개. 에디터의 기존 Unity Search 초기화 예외는 검증 실패와 구분했다. 화면/음향 연출만 바꾸었으며 실제 피해·사망·목숨 차감·복귀 시각·입력·관전 정책과 새 네트워크 필드는 변경하지 않았다.

## 사용자 제공 호흡 녹음 교체
이후 같은 날짜에 LifeGasp을 제공 MP3의0.65~1.90초 한 호흡(1.25초)으로 교체했다. 앞의1.30초 자체 합성 들숨 기록은 이전 버전이다. 기존1회 재생/복귀 시각을 유지하고 WAV GUID 및 심장박동 파일 해시가 보존되는 것을 확인했다. 원본/변환 정보는 Docs/Audio/Sources.md와 LifeFeedback.json을 따른다.
교체 후 Unity 실제 재생의 기존18개 검사를 통과했다. 새 녹음 가져오기/복귀 중1회 숨소리/반복 및 중복 재생 없음/복귀 후 정상화 확인 로그: 격리 프로젝트 life-breath-preview.log. 별도 Windows 재빌드는 수행하지 않았다.

## 사용자 제공 심장박동 녹음 교체
같은 날짜에 LifeHeartbeat도 사용자 제공 shidenbeatsmusic-heartbeat-sound-effect-111218.mp3로 교체했다. 두 타격의 음높이를 유지해 주기0.65초/약92 BPM으로 재배치하고2초/3쌍/피크0.68로 저장했다. 기존 GUID와 한 호흡 LifeGasp의 파일 해시가 유지됐다. Unity 실제 재생의 기존18개 검사를 다시 통과했다(격리 프로젝트 life-heartbeat-preview.log). 별도 Windows 재빌드는 하지 않았다. 최신 수치/원본 해시는 Docs/Audio/LifeFeedback.json을 따른다.

## 목숨0 관전 전환 순서 수정
2026-10-26: 목숨0에서는 과호흡을 재생하지 않고 남은 호흡도 중지한다.1.6~2.35초에 흐림/암전/먹먹함을 정상화한 뒤0.4초 동안3인칭 고정 관전으로 이동한다. AI전과 최종 결과가 동시에 나온 경우도 같은 순서를 사용한다. 남은 목숨의 ReviveAt·일어나기·1회 호흡은 그대로다.

Windows 개발 빌드 통과(`life-elimination-build.log`). 동일 PC의 실제 NGO 호스트19개/원격 클라이언트13개 통과(`life-elimination-{host,client}.log`). 정상 복귀 호흡 유지, 마지막 목숨2→0, 정상화 중 얼굴 시점 유지, 음향 필터 정상화, 숨소리 없이 높이6m 고정 관전 진입과 소유자별 격리를 확인했다. 테스트용 게임 상태/사망 처리는 격리 프로젝트에서만 실행했다. 서버 권한/RPC/프로토콜52는 변경하지 않았다.
AI전/PvP 각각 최종 결과와 영구 탈락이 동시에 발생하는 검사23개도 통과했다(`life-elimination-terminal.log`). 정상화 전 얼굴 시점 유지, 정상화 후 볼륨 가중치0/저역통과22000Hz/잔향-10000, 과호흡0회,3인칭 카메라 고정 확인.

기존 전체 그래픽 프리뷰를 실행 파일과 에디터에서 재실행했으나 전역 VolumeManager 스택의 Gaussian 모드 검사에서 실패했다(`life-elimination-preview.log`, `life-elimination-editor.log`). 이번 관전 순서 변경은 흐림 설정을 바꾸지 않았으며, 이 기록을 그래픽 전체 통과로 계산하지 않는다. 현재 검증한 범위는 실제 소유자별 효과 강도/필터 정상화/호흡/관전 전환이며 Gaussian 실제 렌더 재검증은 미완료다.
