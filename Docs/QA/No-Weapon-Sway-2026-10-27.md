# 총기 조준 흔들림 제거 검증 — 2026-10-27

Unity6000.3.18f1 / NGO2.13.3 / Transport2.7.3 / 프로토콜56.

- 공통 DuelGunplay.Sway를0으로 고정했다. 리볼버/저격소총/테이저건의 정조준 흔들림을 제거하고 소유자/관전/총기 자세/AI/서버 탄도의 공통 계산을 유지한다.
- 소드오프는 원래 정조준이 없고 Sway도0이었다. 정조준 불가/산탄6도/37펠릿/피해75+25와 기존 입력/재장전은 그대로다.
- 탄퍼짐·줌·피격/감전/사망 연출은 변경하지 않았다. 무기 정보의 흔들림 설명을 수정했다. 구버전과 조준 계산이 달라56으로 버전을 구분한다.

검증:

- Windows Development 빌드 성공(현재 씬 Assets/Scenes/QuickHands.unity).
- 기존 AdsRulesRuntimeQA 모델118개 통과: 모든6좌석/4무기/ADS·지향 흔들림0, 실제 서버 사격 방향/기존 지향 탄퍼짐·산탄 범위/피해/AI 동작.
- 실제 별도 실행 호스트4개/원격8개 통과: 프로토콜56, 소드오프 정조준 거절·기존 FOV·안정된 카메라, 로비 연습/본 경기 탄퍼짐·탄 소모.
- 기존 기본 전투/테이저 만료/1등 전용 주사위 모델55개 통과.

로그: C:/Users/PC/AppData/Local/Temp/RouletteQA-bb1739d9/no-sway-run/
빌드 로그: C:/Users/PC/AppData/Local/Temp/RouletteQA-bb1739d9/no-sway-final-build.log.
