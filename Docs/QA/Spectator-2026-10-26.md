# Q/E 관전 검증 — 2026-10-26

- Unity6000.3.18f1 / NGO2.13.3 / 프로토콜54. 격리 복제본 Windows Development 빌드.
- `Tools/UnityQA/SpectatorRuntimeQA.cs`: 실제 호스트63개·원격 클라이언트63개 통과.
- Q/E 순환 순서와 빈자리 건너뛰기, 임시 사망/목숨0/사망 연출 대기, 기본3인칭 유지, 생존자 좌석 눈 위치·시선·몸 표시/머리 숨김, 저격소총 ADS/FOV/조준경, 체력 변경 동기화 확인.
- 채팅·Alt 중 전환 방지, 사망자 조작 비활성, 크로스헤어 숨김, 대상 사망 시 기본 시점 복귀, 최종 결과 고정, 재경기 본인1인칭 복귀 확인.
- 새 RPC나 관전 대상 조작 요청을 만들지 않았다. 개인 미니게임 입력 UI는 관전 대상에게서 전송하지 않는다.
- 로그: `C:/Users/PC/AppData/Local/Temp/RouletteQA-bb1739d9/spectator-render/`.

렌더 확인: [생존자1인칭](../Previews/Spectator-First-Person.png), [조준경](../Previews/Spectator-Scope.png).
