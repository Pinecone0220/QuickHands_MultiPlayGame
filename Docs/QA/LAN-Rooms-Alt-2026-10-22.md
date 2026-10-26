# LAN 방 목록·Alt 홀드 UI·발사 시점 추종 검증

2026-10-22 / Unity6000.3.18f1 / NGO2.13.3 / Transport2.7.3 / 프로토콜48.

Windows 플레이어 빌드 성공. `Tools/UnityQA/LanRoomsRuntimeQA.cs`로 모델13개, 실제 호스트4개/클라이언트4개/관찰자5개, 그래픽22개로 총48개 검사를 통과했다.

- LAN 검색: 자동 주소/포트, 여러 호스트 검색 포트, loopback/네트워크 응답 중복 제거, 인원/진행/정원 갱신, 방 종료 후6초 만료를 확인했다.
- 연결 승인: 올바른 비밀번호/한글 닉네임 참가, 잘못된 비밀번호/방 ID/UTF8/과대 payload 거절, 진행 중 참가 거절, 거절된 클라이언트의 좌석 미점유를 확인했다. 검색에 비밀번호를 노출하지 않는다.
- Alt: 좌/우 Alt 홀드 시 UI 이벤트 허용, 평소 클릭 차단/커서 잠금, 해제 프레임 행동 차단과 다음 프레임 복귀를 확인했다. 실제5초 아이템 단계의 시점 회전, Alt 사용과 회복 소비도 확인했다.
- 총기: 총구 섬광 중 시선 회전에 따른 위치/회전, 현재 ADS 위치 추종과 발사 당시 방향 불변을 확인했다.
- 방 목록/아이템 사용 캡처를 직접 시각 검토했다. 최종 정리 후 편집기 컴파일/그래픽 실행도 성공했다.

검증은 같은 PC의 별도 실제 네트워크 프로세스와 Unity 그래픽 실행에서 수행했다. 서로 다른 물리 PC/공유기/방화벽 환경은 이번에 검증하지 않았다. 인터넷 방 검색은 구현 범위가 아니다.

실행 증거는 `%TEMP%/RouletteQA-bb1739d9` 아래에 있다.

| 증거 | 결과 |
|---|---|
| rooms-build2-2026-10-22.log | PASS chat build |
| LanPeers/model-done | PASS LAN model: 13 checks |
| LanPeers/host-done | PASS LAN host: 4 checks |
| LanPeers/client-done | PASS LAN client: 4 checks |
| LanPeers/observer-done | PASS LAN observer: 5 checks |
| LanVisuals3/preview-done | PASS LAN preview: 22 checks |
| lan-preview3-2026-10-22.log | 최종 그래픽/컴파일 기록 |

미리보기: [방 목록](../Previews/LAN-Room-List.png), [Alt 아이템 사용](../Previews/Alt-Item-Use.png).

추가 정정: 위 Alt 검사는 루트 이벤트 전달/직접 아이템 요청을 확인했으며 실제 Clickable의 Alt 활성화는 누락했다. 이후 발견된 버튼 무반응을 수정하고 PointerDown/PointerUp 기반28개 회귀 검증을 추가했다. [Alt 클릭 수정 기록](Alt-Click-2026-10-22.md) 참조.
