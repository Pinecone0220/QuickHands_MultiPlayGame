# Local Forecast - Elevator

2026-10-26: 사용자 지정 YouTube 링크 https://youtu.be/xy_NKN75Jhw 의 배경음악을 적용했다. 영상에서 재인코딩하지 않고 동일 곡의 작곡가 제공 원본 MP3를 받았다.

- 곡: Local Forecast - Elevator / Kevin MacLeod (incompetech.com), ISRC USUAN1300012.
- 원본: https://incompetech.com/music/royalty-free/mp3-royaltyfree/Local%20Forecast%20-%20Elevator.mp3
- 공식 곡 정보: https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1300012
- 이용 조건: CC BY 3.0, https://creativecommons.org/licenses/by/3.0/ . 공식 무료 라이선스 안내: https://incompetech.com/music/royalty-free/licenses/ . 해당 녹음의 기존 CC BY 3.0 표기: https://commons.wikimedia.org/wiki/File:Local_Forecast_-_Elevator_(ISRC_USUAN1300012).mp3
- 파일: `Assets/Resources/Music/LocalForecastElevator.mp3`, 7,570,404 bytes.
- SHA256: `8c46b44e65324bc339fbbc3e770611d671f6c74a40f3ec8dd50332c082b25cc9`.

원본 파일을 편집하지 않았다. Unity에서 스테레오 Vorbis 스트리밍으로 가져온다. DuelBackgroundMusic은 기존 LastCall 대신 이 파일을 사용하며 메인/로비0.28, 경기0.16, 부드러운 볼륨 전환과2D 반복을 유지한다. 네트워크 변화는 없다. 이전 자체 제작 LastCall 파일/생성 도구는 보관한다.

메인 화면에 제목·작곡가·라이선스를 표시하며 클릭하면 라이선스 페이지를 연다. 빌드에도 `StreamingAssets/ThirdPartyNotices.txt`를 포함한다.

## 검증
Unity 에디터 실제 재생에서 BarMusicRuntimeQA 9개 검사 통과: 지정 MP3/스테레오/길이, 스트리밍, 2D 반복/우선순위, 메뉴 페이드, AI 경기 진입 중 재생 연속성/볼륨, 실제 디코더 반복 경계, 퇴장 후 메뉴 볼륨 복귀. 로그: 격리 프로젝트 RouletteQA-bb1739d9/elevator-music-preview.log. 별도 Windows 재빌드는 수행하지 않았다. 기존 에디터 Search 초기화 예외는 게임 재생 검사와 구분했다.
