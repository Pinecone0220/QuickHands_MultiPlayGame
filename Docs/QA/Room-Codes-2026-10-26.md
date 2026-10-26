# 방 코드 검증 — 2026-10-26

Unity6000.3.18f1 / NGO2.13.3 / 프로토콜55. 격리 복제본 Windows Development 빌드 통과.

- 기존 LAN 모델13개: 실제 UDP 검색, 비밀번호 승인, 메타데이터 변경/종료 방 만료 확인.
- `Tools/UnityQA/RoomCodesRuntimeQA.cs`: 호스트22개·원격23개 통과.
- 코드 정규화/길이/비ASCII·제어문자 거절, 메인 실제 생성 버튼 연결, 중복 확인 중 시작/중복 UI 차단, 세션 GUID 별도 보존, 로비 코드 표시 확인.
- 없는 코드/중복 생성/서로 다른 두 방의 같은 코드/취소/잘못된 비밀번호/대소문자 무관 정상 접속/원격 초기 상태/퇴장 정리를 실제 두 실행 파일로 확인.
- 검색/생성은 같은 LAN의 응답을 기준으로 한다. 동시 생성/분리된 네트워크까지 전역 유일성을 보장하는 중앙 서비스는 없으며 중복 검색 결과에서는 임의 접속하지 않는다.
- [생성 입력 화면](../Previews/Room-Code-Create.png), [로비 코드 표시](../Previews/Room-Code-Lobby.png).

로그: `C:/Users/PC/AppData/Local/Temp/RouletteQA-bb1739d9/room-codes-run/`. 빌드 로그: 같은 복제본의 `room-codes-build.log`.
