using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed class RouletteView : MonoBehaviour
    {
        private HelloWorldManager manager;
        private VisualElement root, menu, table, damageOverlay, digits, killFeed, inputPanel, victoryLayer;
        private VisualElement weaponInfoLayer;
        private Button weaponInfoButton,weaponInfoClose;
        private bool weaponInfoOpen;
        private Texture2D headshotIcon,legshotIcon;
        private VisualElement infoReturnFocus;
        private ScrollView infoScroll;
        private readonly VisualElement[] miniGameInfoCards=new VisualElement[PartyGames.Count];
        private Label status, turn, result;
        private VisualElement clockPanel;
        private Label clockTitle, clockTimer, clockRanking;
        private Label shootingNotice;
        private VisualElement clockRankingRows;
        private readonly Label[] clockRankingEntries=new Label[RouletteRules.MaxPlayers];
        private Button clockStop;
        private VisualElement partySelection, partyReel;
        private VisualElement quickStartLayer;
        private Label quickStartNumber,quickStartTitle;
        private Label partySelectionTitle;
        private readonly Label[] partyReelLabels=new Label[5];
        private DuelDeathCause renderedDeathCause;
        private int announcedTimeoutRound=-1;
        private int deathBannerLife=-1;
        private int clockRound=-1, clockIteration=-1;
        private bool clockStopped,clockAwaiting;
        private int clockRecord;
        private MiniFailure clockFailure;
        private MiniSignal lastMiniSignal;
        private Label miniInstruction;
        private ScrollView memoryScroll;
        private VisualElement memoryArrows;
        private readonly System.Collections.Generic.List<Label> memoryArrowLabels=new System.Collections.Generic.List<Label>();
        private int memoryPartyRound=-1,memoryIteration=-1,memoryRound=-1,memoryCount,memoryConfirmed;
        private const float MemoryCellWidth=56;
        private Label victoryText;
        private VisualElement highlightLayer;
        private Label highlightText;
        private int victoryRound = -1, announcedVictoryRound = -1;
        private double victoryExpiresAt;
        private double victoryShownAt;
        private const double VictoryHold = 0.8, VictoryRise = 0.6;
        private const double HighlightStart = VictoryHold + VictoryRise, HighlightHold = 0.8, HighlightRise = 0.6;
        private const double ResultsStart = HighlightStart + HighlightHold + HighlightRise;
        private VisualElement resultsLayer, resultsRows, resultsFooter;
        private Label resultsSummary, resultsReady;
        private bool resultLayout;
        private DuelRoundResults renderedResults;
        private readonly Label[] digitLabels = new Label[DuelActionState.InputLength];
        private readonly Label[] nameplates = new Label[RouletteRules.MaxPlayers];
        private readonly Label[] seatStatuses = new Label[RouletteRules.MaxPlayers];
        private readonly Label[] lifeIcons = new Label[RouletteRules.MaxPlayers];
        private VisualElement nameplateLayer;
        private VisualElement aimWarning, scopeShade, deathDetails, deathRows;
        private Label deathSummary;
        private DuelDamageReport renderedDeathReport;
        private Texture2D aimVignette, scopeVignette;
        private readonly Texture2D[] weaponIcons = new Texture2D[3];
        private VisualElement weaponChoice;
        private readonly Button[] weaponButtons = new Button[2];
        private readonly System.Collections.Generic.Queue<KillEntry> kills = new System.Collections.Generic.Queue<KillEntry>();
        private int killRound = -1;
        private int lastKillShot;
        private int lastKillVictims;
        private sealed class KillEntry { public VisualElement Row; public double ExpiresAt; }
        private Button rematch, leave;
        private VisualElement roomSettings;
        private Toggle unlimitedShot;
        private Toggle startWithItems,manualMiniGame,unlimitedItems;
        private VisualElement manualSelection,itemRow,healOverlay,electricOverlay;
        private readonly Button[] itemButtons=new Button[2];
        private int manualRound=-1,manualIteration=-1;
        private readonly Texture2D[] itemIcons=new Texture2D[2];
        private Texture2D healTexture,electricTexture;
        private readonly VisualElement[] weaponInfoCards=new VisualElement[2],itemInfoCards=new VisualElement[2];
        private Label infoTitle,infoIntro;
        private IntegerField shotSeconds;
        private Label shotTimer, roomShotSummary;
        private TextField address, port;
        private DuelActionState predicted;
        private int previousShots, predictionRound = -1;
        private bool hadPlayer, waitingForAction;
        private double sentAt;
        private double inputShakeStartedAt = -1;
        private const float InputShakeDuration = 0.32f, InputShakeDistance = 7f;
        private Font font;
        private AudioSource reelSound;
        private AudioClip reelTickSound,reelStopSound;
        private int reelRound=-1,reelIteration=-1,reelIndex;
        private bool reelStopped;
        private VisualElement hitMarker;
        private int hitRound=-1,hitLife=-1,lastHitShot;
        private double hitExpiresAt;
        public void Initialize(HelloWorldManager owner)
        {
            manager = owner; root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.AddToClassList("duel-root");
            var style = Resources.Load<StyleSheet>("Roulette"); if (style != null) root.styleSheets.Add(style);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            for (int i = 0; i < 2; i++) weaponIcons[i] = MakeWeaponIcon((DuelWeapon)i);
            headshotIcon=DuelHitPartArt.Icon(DuelHitPart.Head);legshotIcon=DuelHitPartArt.Icon(DuelHitPart.Legs);
            for(int i=0;i<2;i++)itemIcons[i]=DuelItemArt.Icon((DuelItem)i);weaponIcons[2]=DuelItemArt.Icon(DuelItem.Taser);
            root.style.unityFont = font; root.pickingMode = PickingMode.Ignore;
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.contentViewport.style.overflow=Overflow.Hidden; scroll.AddToClassList("menu-scroll"); root.Add(scroll); menu = scroll;
            var panel = Box(scroll, "menu-panel");
            Text(panel, "PARTY MODE", "wordmark");
            Text(panel, "빠른 손.\n단 한 발.", "title");
            Text(panel,"파티 모드","eyebrow");
            Button(panel,"미니게임 정보",()=>{SetWeaponInfo(true);SetMiniGameInfo();},"secondary");
            weaponInfoButton=Button(panel,"무기 정보",()=>{SetWeaponInfo(true);SetInfoMode(false);},"secondary");
            Button(panel,"아이템 정보",()=>{SetWeaponInfo(true);SetInfoMode(true);},"secondary");
            var nickname = new TextField("닉네임") { value = manager.Nickname, maxLength = 64 };
            nickname.RegisterValueChangedCallback(e => manager.Nickname = e.newValue);
            panel.Add(nickname);
            Text(panel, "최대 16자 · 비워 두면 입장 순서대로 player1, player2…", "fineprint");
            Text(panel, "01  /  AI DUEL", "eyebrow");
            var difficulty = new DropdownField("AI 난이도", new System.Collections.Generic.List<string> { "쉬움", "보통", "어려움" }, 1);
            difficulty.RegisterValueChangedCallback(e => manager.Difficulty = (AiDifficulty)difficulty.index); panel.Add(difficulty);
            var aiCount = new DropdownField("상대 AI 수", new System.Collections.Generic.List<string> { "1명 (총 2명)", "2명 (총 3명)", "3명 (총 4명)", "4명 (총 5명)", "5명 (총 6명)" }, 0);
            aiCount.RegisterValueChangedCallback(e => manager.AiCount = aiCount.index + 1); panel.Add(aiCount);
            Text(panel, "쉬움: 느림 · 실수 많음 / 보통: 중간 / 어려움: 빠름 · 정확", "fineprint");
            Button(panel, "AI 대결 시작", () => manager.Begin(DuelMode.AI, false, "127.0.0.1", "7777"), "primary");
            Text(panel, "02  /  PVP DUEL", "eyebrow");
            address = new TextField("호스트 IP") { value = "127.0.0.1" }; port = new TextField("포트") { value = "7777" };
            panel.Add(address); panel.Add(port); var connections = Box(panel, "button-row");
            Button(connections, "방 만들기", () => manager.Begin(DuelMode.PvP, false, address.value, port.value), "secondary");
            Button(connections, "참가하기", () => manager.Begin(DuelMode.PvP, true, address.value, port.value), "secondary");
            Text(panel, "PvP: 같은 PC는 127.0.0.1, 같은 LAN은 호스트 IP.\n인터넷 중계 서비스는 아직 연결되지 않았습니다.", "fineprint");
            table = Box(root, "match"); table.pickingMode = PickingMode.Ignore;
            Box(table, "scene-space").pickingMode = PickingMode.Ignore;
            var bottom = Box(table, "hud-bottom"); turn = Text(bottom, "", "turn");
            inputPanel = bottom;
            clockPanel=Box(table,"clock-panel");
            clockTitle=Text(clockPanel,"","clock-title");
            shootingNotice=Text(clockPanel,"","shooting-notice");shootingNotice.pickingMode=PickingMode.Ignore;shootingNotice.enableRichText=false;
            miniInstruction=Text(clockPanel,"","mini-instruction");
            clockTimer=Text(clockPanel,"","clock-timer");
            memoryScroll=new ScrollView(ScrollViewMode.Horizontal){horizontalScrollerVisibility=ScrollerVisibility.Hidden,verticalScrollerVisibility=ScrollerVisibility.Hidden};memoryScroll.AddToClassList("memory-scroll");clockPanel.Add(memoryScroll);
            memoryScroll.contentViewport.style.overflow=Overflow.Hidden;
            memoryArrows=Box(memoryScroll,"memory-arrows");memoryArrows.pickingMode=PickingMode.Ignore;
            Show(memoryScroll,false);
            clockRanking=Text(clockPanel,"","clock-ranking");
            clockRankingRows=Box(clockPanel,"clock-ranking-rows");clockRankingRows.pickingMode=PickingMode.Ignore;
            for(int i=0;i<clockRankingEntries.Length;i++)
            {clockRankingEntries[i]=Text(clockRankingRows,"","clock-ranking-row");clockRankingEntries[i].enableRichText=false;clockRankingEntries[i].pickingMode=PickingMode.Ignore;}
            clockStop=Button(clockPanel,"정지",StopClock,"primary");
            partySelection=Box(root,"party-selection-layer");partySelection.pickingMode=PickingMode.Ignore;
            var selector=Box(partySelection,"party-selector");selector.pickingMode=PickingMode.Ignore;
            Text(selector,"파티 모드","party-wordmark").pickingMode=PickingMode.Ignore;
            partySelectionTitle=Text(selector,"미니게임 선택 중","party-selection-title");
            var reelWindow=Box(selector,"party-reel-window");reelWindow.pickingMode=PickingMode.Ignore;
            partyReel=Box(reelWindow,"party-reel");partyReel.pickingMode=PickingMode.Ignore;
            for(int i=0;i<partyReelLabels.Length;i++){partyReelLabels[i]=Text(partyReel,"","party-reel-label");partyReelLabels[i].pickingMode=PickingMode.Ignore;}
            Box(reelWindow,"party-reel-focus").pickingMode=PickingMode.Ignore;
            quickStartLayer=Box(root,"quick-start-layer");quickStartLayer.pickingMode=PickingMode.Ignore;
            quickStartTitle=Text(quickStartLayer,"퀵핸즈","quick-start-title");quickStartTitle.pickingMode=PickingMode.Ignore;
            quickStartNumber=Text(quickStartLayer,"","quick-start-number");quickStartNumber.pickingMode=PickingMode.Ignore;
            digits = Box(bottom, "qte-digits");
            for (int i = 0; i < digitLabels.Length; i++) digitLabels[i] = Text(digits, "", "qte-digit");
            result = Text(bottom, "", "result");
            weaponChoice = Box(bottom, "weapon-choice");
            Text(weaponChoice, "퀵핸즈 무기 선택", "weapon-choice-title");
            var choices = Box(weaponChoice, "button-row");
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                var weapon = (DuelWeapon)i;
                var button = Button(choices, "", () => HelloWorldPlayer.Local?.SelectWeapon(weapon), "weapon-choice-button");
                button.AddToClassList("secondary"); weaponButtons[i] = button;
                var image = new Image { image = weaponIcons[i], pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("weapon-choice-icon"); button.Add(image);
                Text(button, WeaponRules.Name(weapon), "weapon-choice-name").pickingMode = PickingMode.Ignore;
            }
            rematch = Button(bottom, "다시 대결", () => HelloWorldPlayer.Local?.Rematch(), "primary");
            roomShotSummary=Text(bottom,"","room-shot-summary");
            roomSettings=Box(root,"room-settings");Show(roomSettings,false);
            Text(roomSettings,"개발자 모드 · F11","room-settings-title");
            unlimitedShot=new Toggle("사격시간 무제한");roomSettings.Add(unlimitedShot);
            shotSeconds=new IntegerField("제한시간 (초)"){value=10,isDelayed=true};roomSettings.Add(shotSeconds);
            startWithItems=new Toggle("아이템을 갖고 시작");roomSettings.Add(startWithItems);
            startWithItems.RegisterValueChangedCallback(e=>manager.SetRoomStartWithItems(e.newValue));
            unlimitedItems=new Toggle("아이템 무한 사용");roomSettings.Add(unlimitedItems);unlimitedItems.RegisterValueChangedCallback(e=>manager.SetRoomUnlimitedItems(e.newValue));
            manualMiniGame=new Toggle("미니게임 직접 선택");roomSettings.Add(manualMiniGame);manualMiniGame.RegisterValueChangedCallback(e=>manager.SetRoomManualMiniGame(e.newValue));
            Text(roomSettings,"장전 완료 후 적용 · 1~60초","room-settings-note");
            unlimitedShot.RegisterValueChangedCallback(e=>manager.SetRoomShotTime(e.newValue?0:Mathf.Clamp(shotSeconds.value,1,60)));
            shotSeconds.RegisterValueChangedCallback(e=>{if(!unlimitedShot.value)manager.SetRoomShotTime(Mathf.Clamp(e.newValue,1,60));});
            shotTimer=Text(table,"","shot-time-remaining");shotTimer.pickingMode=PickingMode.Ignore;
            status = Text(root, "", "status");
            leave = Button(root, "자리에서 일어나기", () => manager.Leave(), "exit");
            damageOverlay = Box(root, "damage-overlay"); damageOverlay.pickingMode = PickingMode.Ignore;
            reelSound=gameObject.AddComponent<AudioSource>();reelSound.playOnAwake=false;reelSound.volume=.22f;
            reelTickSound=MakeReelSound(false);reelStopSound=MakeReelSound(true);
            hitMarker=Box(root,"hit-marker");hitMarker.pickingMode=PickingMode.Ignore;
            for(int i=0;i<4;i++)
            {var line=Box(hitMarker,"hit-marker-line");line.pickingMode=PickingMode.Ignore;line.style.left=i%2==0?4:20;line.style.top=i<2?8:24;line.style.rotate=new Rotate(Angle.Degrees(i==0||i==3?45:-45));}
            Show(hitMarker,false);
            nameplateLayer = Box(root, "nameplate-layer"); nameplateLayer.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                nameplates[i] = Text(nameplateLayer, "", "nameplate");
                nameplates[i].pickingMode = PickingMode.Ignore;
                seatStatuses[i] = Text(nameplateLayer, "", "nameplate");
                seatStatuses[i].AddToClassList("seat-status");
                seatStatuses[i].pickingMode = PickingMode.Ignore;
                lifeIcons[i]=Text(nameplateLayer,"","nameplate");
                lifeIcons[i].AddToClassList("seat-lives");lifeIcons[i].pickingMode=PickingMode.Ignore;
            }
            aimWarning = new VisualElement { pickingMode = PickingMode.Ignore };
            aimWarning.AddToClassList("aim-warning");
            aimVignette = MakeAimVignette();
            aimWarning.style.backgroundImage = new StyleBackground(aimVignette);
            root.Insert(0, aimWarning);
            scopeShade = new VisualElement { pickingMode = PickingMode.Ignore };
            scopeShade.AddToClassList("aim-warning");
            scopeVignette = MakeAimVignette(true);
            scopeShade.style.backgroundImage = new StyleBackground(scopeVignette);
            root.Insert(0, scopeShade);
            killFeed = Box(root, "kill-feed"); killFeed.pickingMode = PickingMode.Ignore;
            deathDetails = Box(root, "death-details"); deathDetails.pickingMode = PickingMode.Ignore;
            Text(deathDetails, "사망 원인", "death-details-title").pickingMode = PickingMode.Ignore;
            deathSummary = Text(deathDetails, "", "death-details-summary"); deathSummary.pickingMode = PickingMode.Ignore;
            var damageScroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            damageScroll.AddToClassList("death-details-scroll"); deathDetails.Add(damageScroll);
            deathRows = Box(damageScroll, "death-details-rows");
            Show(deathDetails, false);
            resultsLayer = Box(root, "round-results-layer"); resultsLayer.pickingMode = PickingMode.Ignore;
            var report = Box(resultsLayer, "round-results");
            Text(report, "경기 결과", "results-title");
            resultsSummary = Text(report, "", "results-summary");
            var header = Box(report, "results-row"); header.AddToClassList("results-header");
            ResultCell(header, "플레이어", true); ResultCell(header, "오입력"); ResultCell(header, "킬");
            ResultCell(header, "입력 정확도");ResultCell(header,"초읽기 1등");ResultCell(header,"평균 오차");
            var scrollResults = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scrollResults.AddToClassList("results-scroll"); report.Add(scrollResults);
            resultsRows = Box(scrollResults, "results-rows");
            resultsFooter = Box(report, "results-footer");
            resultsReady = Text(resultsFooter, "", "results-ready");
            Show(resultsLayer, false);
            victoryLayer = Box(root, "victory-layer"); victoryLayer.pickingMode = PickingMode.Ignore;
            victoryText = Text(victoryLayer, "", "victory-banner"); victoryText.pickingMode = PickingMode.Ignore;
            highlightLayer = Box(root, "highlight-layer"); highlightLayer.pickingMode = PickingMode.Ignore;
            highlightText = Text(highlightLayer, "", "highlight-banner"); highlightText.pickingMode = PickingMode.Ignore;
            manualSelection=Box(root,"manual-selection");Text(manualSelection,"미니게임 직접 선택","room-settings-title");
            for(int i=0;i<PartyGames.Count;i++){var game=PartyGames.At(i);Button(manualSelection,PartyGames.Name(game),()=>manager.ChooseMiniGame(game),"secondary");}
            Show(manualSelection,false);
            itemRow=Box(clockPanel,"item-row");
            for(int i=0;i<2;i++){var item=(DuelItem)i;var b=Button(itemRow,"",()=>HelloWorldPlayer.Local?.UseItem(item),"item-choice");var icon=new Image{image=itemIcons[i],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};icon.AddToClassList("item-icon");b.Add(icon);Text(b,DuelItemArt.Name(item),"item-name").pickingMode=PickingMode.Ignore;itemButtons[i]=b;}
            healOverlay=Box(root,"item-effect");electricOverlay=Box(root,"item-effect");healOverlay.pickingMode=electricOverlay.pickingMode=PickingMode.Ignore;
            healTexture=DuelItemArt.Effect(false);electricTexture=DuelItemArt.Effect(true);healOverlay.style.backgroundImage=new StyleBackground(healTexture);electricOverlay.style.backgroundImage=new StyleBackground(electricTexture);
            Show(itemRow,false);Show(healOverlay,false);Show(electricOverlay,false);
            BuildWeaponInfo();
            Refresh();
        }
        private void BuildWeaponInfo()
        {
            weaponInfoLayer=Box(root,"weapon-info-layer");var panel=Box(weaponInfoLayer,"weapon-info-panel");
            var heading=Box(panel,"weapon-info-heading");infoTitle=Text(heading,"무기 정보","weapon-info-title");
            weaponInfoClose=Button(heading,"닫기",()=>SetWeaponInfo(false),"secondary");
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            infoScroll=scroll;
            scroll.contentViewport.style.overflow=Overflow.Hidden;
            scroll.AddToClassList("weapon-info-scroll");panel.Add(scroll);
            infoIntro=Text(scroll,"무기는 방에 입장한 뒤 선택합니다. 숫자로 총을 준비하고 재장전하는 행동 입력은 퀵핸즈에만 적용됩니다. 우클릭으로 조준하고 좌클릭으로 사격합니다.","weapon-info-intro");
            for(int i=0;i<2;i++)
            {
                var weapon=(DuelWeapon)i;var card=Box(scroll,"weapon-info-card");weaponInfoCards[i]=card;var header=Box(card,"weapon-info-card-heading");
                var icon=new Image{image=weaponIcons[i],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};
                icon.AddToClassList("weapon-info-icon");header.Add(icon);Text(header,WeaponRules.Name(weapon),"weapon-info-name");
                Text(card,"퀵핸즈 · 장전과 사격","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    $"처음 준비는 5개 행동마다 숫자 6개를 입력합니다. 이후 한 발마다 숫자 3개로 장전하며 최대 {WeaponRules.Capacity(weapon)}발까지 넣을 수 있습니다.\n한 발만 장전해도 우클릭으로 조준하고 좌클릭으로 사격할 수 있습니다. 남은 탄은 추가 입력 없이 연속 사격합니다.\n재장전은 탄피 빼기(숫자 6개)부터 시작하고 빈 약실마다 숫자 3개로 채웁니다. 남은 실탄은 유지됩니다.":
                    "처음에는 집기 → 배럴 열기 → 장전 → 배럴 닫기 순서로, 각 행동마다 숫자 4개를 입력합니다.\n우클릭으로 조준하고 좌클릭으로 사격합니다. 이후에는 배럴 열기부터 반복하며 탄피가 자동으로 빠진 뒤 새 탄을 넣고 닫습니다.","weapon-info-text");
                Text(card,"피해","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    $"머리 {WeaponRules.Damage(weapon,DuelHitPart.Head)} / 몸통 {WeaponRules.Damage(weapon,DuelHitPart.Body)} / 다리 {WeaponRules.Damage(weapon,DuelHitPart.Legs)}":
                    $"부위에 관계없이 직격 {WeaponRules.Damage(weapon,DuelHitPart.Body)} · 맞은 상대의 좌우 좌석에 각각 25 피해\n사격자와 빈자리·사망자는 주변 피해에서 제외됩니다. 1번과 6번 좌석은 이어지지 않습니다.","weapon-info-text");
                Text(card,"사격 성능","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    "한 발의 탄환으로 정확히 맞혀야 하며 조준 흔들림이 큽니다. 맞힌 부위에 따라 피해가 달라집니다.":
                    "산탄이 퍼져 나가 맞히기 비교적 쉽지만 빗나갈 수 있습니다. 머리를 맞혀도 추가 피해는 없으며 주변 상대에게도 피해를 줄 수 있습니다.","weapon-info-text");
                Text(card,"퀵핸즈 · 특징","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    "입력할 숫자가 한 자리씩 공개됩니다. 오입력하면 현재 행동의 숫자가 모두 바뀌고 처음부터 다시 입력하지만, 이미 장전한 탄은 유지됩니다.\n적게 장전해 먼저 쏘거나, 여러 발을 준비해 연속 사격할 수 있습니다.":
                    "리볼버보다 준비 행동과 입력 수가 적어 빠르게 사격을 준비할 수 있습니다. 한 발 쏠 때마다 배럴 열기 → 장전 → 배럴 닫기의 3개 행동을 다시 입력해야 합니다.","weapon-info-text");
            }
            for(int i=0;i<2;i++)
            {var card=Box(scroll,"item-info-card");card.AddToClassList("weapon-info-card");itemInfoCards[i]=card;var h=Box(card,"weapon-info-card-heading");var icon=new Image{image=itemIcons[i],scaleMode=ScaleMode.ScaleToFit};icon.AddToClassList("weapon-info-icon");h.Add(icon);Text(h,DuelItemArt.Name((DuelItem)i),"weapon-info-name");
                Text(card,i==0?"체력 50 회복 · 최대 체력 100":"피해 5 · 감전","weapon-info-section");
                Text(card,i==0?"아이템 사용 시간에 클릭하면 초록색 회복 효과와 함께 즉시 회복하며 한 번 사용하면 소비됩니다. 체력이 가득 차면 사용할 수 없습니다.":"맞은 상대는 사격 시간이 끝날 때까지 감전되어 조준할 수 없고 무작위로 격발됩니다. 퀵핸즈에서는 사용할 수 없습니다.","weapon-info-text");}
            for(int i=0;i<PartyGames.Count;i++)
            {
                var game=PartyGames.At(i);var card=Box(scroll,"weapon-info-card");miniGameInfoCards[i]=card;
                Text(card,PartyGames.Name(game),"weapon-info-name");
                Text(card,MiniGameInfoText(game),"weapon-info-text");
            }
            SetInfoMode(false);
            weaponInfoLayer.RegisterCallback<ClickEvent>(e=>{if(e.target==weaponInfoLayer)SetWeaponInfo(false);});
            root.RegisterCallback<KeyDownEvent>(e=>{if(weaponInfoOpen&&e.keyCode==KeyCode.Escape){SetWeaponInfo(false);e.StopPropagation();}});
            root.RegisterCallback<KeyDownEvent>(e=>
            {
                if(IsMemoryTurn()&&(e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow||e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow))
                {e.StopPropagation();e.PreventDefault();}
            },TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(e=>{if(IsMemoryTurn()){e.StopPropagation();e.PreventDefault();}},TrickleDown.TrickleDown);
            Show(weaponInfoLayer,false);
        }
        private void BuildManualChoices()
        {
            // Fresh controls per turn discard any pointer capture/focus left by the previous choice.
            if(root.panel.GetCapturingElement(PointerId.mousePointerId) is VisualElement captured)captured.ReleasePointer(PointerId.mousePointerId);
            manualSelection.Clear();Text(manualSelection,"미니게임 직접 선택","room-settings-title");
            for(int i=0;i<PartyGames.Count;i++){var game=PartyGames.At(i);Button(manualSelection,PartyGames.Name(game),()=>manager.ChooseMiniGame(game),"secondary");}
            manualSelection.BringToFront();
        }
        private void SetInfoMode(bool items)
        {infoTitle.text=items?"아이템 정보":"무기 정보";infoIntro.text=items?"모든 아이템은 아이템 사용 시간에 선택합니다. 이번 사격 시간이 끝나면 사용 효과는 사라집니다.":"무기는 방에 입장한 뒤 선택합니다. 숫자로 총을 준비하고 재장전하는 행동 입력은 퀵핸즈에만 적용됩니다. 우클릭으로 조준하고 좌클릭으로 사격합니다.";for(int i=0;i<2;i++){Show(weaponInfoCards[i],!items);Show(itemInfoCards[i],items);}foreach(var card in miniGameInfoCards)Show(card,false);infoScroll.scrollOffset=Vector2.zero;}
        private void SetMiniGameInfo()
        {
            infoTitle.text="미니게임 정보";
            infoIntro.text="목숨은 3개, 마지막까지 남은 한 명이 최종 승리합니다. 퀵핸즈를 제외한 미니게임에서는 1등에게 한 발의 사격 기회가 주어집니다.";
            for(int i=0;i<2;i++){Show(weaponInfoCards[i],false);Show(itemInfoCards[i],false);}
            foreach(var card in miniGameInfoCards)Show(card,true);
            infoScroll.scrollOffset=Vector2.zero;
        }
        private static string MiniGameInfoText(PartyMiniGame game)
        {
            switch(game)
            {
                case PartyMiniGame.Countdown:return "제시된 7~15초에 가장 가깝게 멈추세요. 스페이스 또는 왼쪽 클릭으로 정지합니다.\n타이머는 3초가 되기 전에 가려집니다. 목표 시간 +2초까지 멈추지 못하면 목숨을 하나 잃습니다.";
                case PartyMiniGame.QuickHands:return "숫자를 입력해 총을 준비하고 장전한 뒤, 우클릭으로 조준하고 좌클릭으로 사격하세요.\n마지막 한 명이 남을 때까지 진행합니다. 사망하면 목숨을 하나 잃고, 목숨이 남아 있다면 다음 미니게임에 복귀합니다.";
                case PartyMiniGame.Signal:return "가짜 신호에 속지 말고 진짜 초록 신호에서 스페이스 또는 왼쪽 클릭으로 반응하세요. 가장 빠른 사람이 1등입니다.\n성급하게 입력하거나 초록 신호 후 2초 안에 반응하지 못하면 실패합니다. 실패만으로 목숨을 잃지는 않습니다.";
                case PartyMiniGame.Greed:return "점수가 계속 올라갑니다. 스페이스 또는 왼쪽 클릭으로 멈춰 점수를 확정하세요.\n늦게 멈출수록 높은 점수를 얻지만, 터질 때까지 멈추지 못하면 0점입니다. 확정 점수가 가장 높은 사람이 1등입니다.";
                case PartyMiniGame.Memory:return "3초 동안 방향 순서를 기억한 뒤, 10초 안에 방향키로 입력하세요. 처음에는 3개이며 라운드마다 하나씩 늘어납니다. 개수만큼 입력하면 자동 확정됩니다.\n모두 맞히면 계속 진행하고, 오입력이 있다면 가장 많이 틀린 한 명만 이번 미니게임에서 탈락합니다. 목숨은 잃지 않습니다.\n오입력 동점이 생기면 그 라운드에서 순위를 확정합니다. 적게 틀린 순서가 우선이며, 같은 개수끼리는 주사위가 높은 사람이 앞섭니다.";
                default:return "";
            }
        }
        private void SetWeaponInfo(bool visible)
        {
            visible &= !manager.Busy;
            bool changed=weaponInfoOpen!=visible;weaponInfoOpen=visible;Show(weaponInfoLayer,visible);
            if(visible){if(changed)infoReturnFocus=root.panel?.focusController.focusedElement as VisualElement;weaponInfoLayer.BringToFront();weaponInfoClose.Focus();}
            else if(changed&&!manager.Busy)(infoReturnFocus??weaponInfoButton).Focus();
        }
        private static VisualElement Box(VisualElement parent, string css)
        { var item = new VisualElement(); item.AddToClassList(css); parent.Add(item); return item; }
        private static Label Text(VisualElement parent, string text, string css)
        { var label = new Label(text) { enableRichText = false }; label.AddToClassList(css); parent.Add(label); return label; }
        private static Button Button(VisualElement parent, string text, System.Action action, string css)
        { var b = new Button(action) { text = text }; b.AddToClassList(css); parent.Add(b); return b; }
        private static void Show(VisualElement item, bool show) => item.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        private static Label ResultCell(VisualElement row, string value, bool player = false)
        {
            var label = Text(row, value, "results-cell");
            if (player) label.AddToClassList("results-player");
            return label;
        }
        private void SetResultLayout(bool enabled)
        {
            if (resultLayout == enabled) return;
            resultLayout = enabled;
            if (enabled) { resultsFooter.Add(weaponChoice); resultsFooter.Add(rematch); }
            else { inputPanel.Add(weaponChoice); inputPanel.Add(rematch); }
        }
        private bool CanAct()
        {
            var p = HelloWorldPlayer.Local;
            return p != null && p.IsSpawned && p.Seat.Value >= 0 && p.State.Value.Stage == DuelStage.Racing &&
                !(GetComponent<DuelFirstPerson>().Ads&&predicted.Weapon==DuelWeapon.Revolver) &&
                p.State.Value.Clock.Phase==ClockPhase.QuickHands && p.State.Value.Phase == DuelPhase.Playing && !waitingForAction && predicted.Progress < predicted.Length && p.State.Value.Alive(p.Seat.Value) &&
                p.NetworkManager.ServerTime.Time >= predicted.BlockedUntil && predicted.Action != DuelAction.Aim && predicted.Action != DuelAction.Fire;
        }
        private void Press(int digit)
        {
            if (!CanAct()) return;
            var p = HelloWorldPlayer.Local; int version = predicted.Version;
            // Predict only the visible prefix. The server alone advances actions and awards damage.
            predicted.Version++;
            bool mistake = digit != predicted.Digit(predicted.Progress);
            if (!mistake) predicted.Progress++;
            else
            {
                predicted.Progress = 0; predicted.Mistakes++; predicted.BlockedUntil = p.NetworkManager.ServerTime.Time + DuelDirector.MistakePenalty;
                inputShakeStartedAt = Time.realtimeSinceStartupAsDouble;
            }
            // A new Revolver code must come from the server before input can resume.
            waitingForAction = predicted.Progress == predicted.Length || (mistake && predicted.Weapon == DuelWeapon.Revolver);
            sentAt = Time.realtimeSinceStartupAsDouble;
            p.InputDigit(digit, version);
        }
        private static string WeaponActionName(DuelWeapon weapon,DuelAction action)=>weapon!=DuelWeapon.Revolver?ActionName(action):
            action==DuelAction.UnlockBarrel?"장전 게이트 잠금 해제":action==DuelAction.OpenBarrel?"장전 게이트 열기":
            action==DuelAction.CloseBarrel?"탄피 빼기":ActionName(action);
        private void Update()
        {
            if (manager == null || root == null) return;
            Refresh();
            if(UnityEngine.Cursor.lockState==CursorLockMode.Locked && ((Keyboard.current!=null && Keyboard.current.spaceKey.wasPressedThisFrame) || (Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame))) StopClock();
            var k = Keyboard.current;if(k==null)return;
            if(IsMemoryTurn())
            {
                // Independent of number commands and stop buttons; a held arrow never repeats.
                if(k.upArrowKey.wasPressedThisFrame)PressMemory(0);
                else if(k.downArrowKey.wasPressedThisFrame)PressMemory(1);
                else if(k.leftArrowKey.wasPressedThisFrame)PressMemory(2);
                else if(k.rightArrowKey.wasPressedThisFrame)PressMemory(3);
                return;
            }
            if(!CanAct())return;
            // One rising edge per frame; holding a key never repeats inputs.
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Press(1);
            else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Press(2);
            else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Press(3);
            else if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) Press(4);
            else if (k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame) Press(5);
        }
        private void UpdateInputShake(bool actionVisible)
        {
            float offset = 0;
            double age = Time.realtimeSinceStartupAsDouble - inputShakeStartedAt;
            if (!actionVisible || inputShakeStartedAt < 0 || age >= InputShakeDuration) inputShakeStartedAt = -1;
            else
            {
                float progress = Mathf.Clamp01((float)age / InputShakeDuration);
                offset = Mathf.Sin(progress * Mathf.PI * 6) * InputShakeDistance * (1 - progress) * (1 - progress);
            }
            inputPanel.style.translate = new Translate(offset, 0, 0);
        }
        public static string ActionName(DuelAction action) => action == DuelAction.Pickup ? "총 집기" : action == DuelAction.Load ? "한 발 장전" :
            action == DuelAction.UnlockBarrel ? "배럴 잠금 해제" : action == DuelAction.CockHammer ? "격철 젖히기" :
            action == DuelAction.OpenBarrel ? "배럴 열기" : action == DuelAction.CloseBarrel ? "배럴 닫기" : action == DuelAction.Aim ? "조준" : "사격";
        private void Refresh()
        {
            damageOverlay.style.opacity = manager.Table.HitPulse * 0.6f;
            var p = HelloWorldPlayer.Local; bool ready = manager.Busy && p != null && p.IsSpawned && p.Seat.Value >= 0;
            Show(menu, !manager.Busy); Show(table, ready); Show(leave, manager.Busy); Show(status, !ready); status.text = manager.Status;
            if(manager.Busy&&weaponInfoOpen)SetWeaponInfo(false);
            if (manager.Busy && manager.SessionPort > 0) port.SetValueWithoutNotify(manager.SessionPort.ToString());
            Show(nameplateLayer, ready);
            if(ready&&p.NetworkManager.IsHost&&Keyboard.current!=null&&Keyboard.current.f11Key.wasPressedThisFrame)manager.ToggleDeveloperMode();
            bool settingsVisible=ready&&p.NetworkManager.IsHost&&manager.DeveloperMode;
            Show(roomSettings,settingsVisible);Show(shotTimer,false);Show(roomShotSummary,false);
            roomSettings.SetEnabled(settingsVisible&&p.State.Value.Stage==DuelStage.Idle&&p.State.Value.Phase!=DuelPhase.Playing);
            if(settingsVisible)
            {
                roomSettings.BringToFront();
                float limit=p.State.Value.ShotTimeLimit;unlimitedShot.SetValueWithoutNotify(limit==0);
                startWithItems.SetValueWithoutNotify(p.State.Value.StartWithItems);manualMiniGame.SetValueWithoutNotify(p.State.Value.ManualMiniGame);
                unlimitedItems.SetValueWithoutNotify(p.State.Value.UnlimitedItems);
                if(limit>0)shotSeconds.SetValueWithoutNotify(Mathf.RoundToInt(limit));shotSeconds.SetEnabled(limit>0);
            }
            UpdateClock(ready?p:null);
            UpdateHitMarker(ready?p:null);
            var own=ready?p.State.Value.ForSeat(p.Seat.Value):default;double effectNow=ready?p.NetworkManager.ServerTime.Time:0;
            bool healed=ready&&own.HealAt>=0&&effectNow-own.HealAt<1.1&&p.State.Value.Clock.Phase==ClockPhase.ItemPreparation;
            Show(healOverlay,healed);healOverlay.style.opacity=healed?Mathf.Clamp01((float)(1.1-(effectNow-own.HealAt)))*(.8f+.2f*Mathf.Sin(Time.unscaledTime*9)):0;
            Show(electricOverlay,ready&&own.Stunned);electricOverlay.style.opacity=.7f+.3f*Mathf.Sin(Time.unscaledTime*35);
            Show(aimWarning, ready && IsUnderAim(p.State.Value, p.Seat.Value));
            var fps = GetComponent<DuelFirstPerson>();
            bool ads = ready && fps != null && fps.Active && fps.Ads && own.Weapon!=DuelWeapon.Taser;
            aimWarning.style.opacity = ads ? .05f : 1;
            Show(scopeShade, ads);
            Show(killFeed, ready);
            UpdateDeathDetails(ready ? p : null);
            if (!ready) { announcedTimeoutRound=-1; hadPlayer = false; predictionRound = -1; previousShots = 0; killRound = -1; lastKillShot = 0; ClearKills(); victoryRound = announcedVictoryRound = -1; victoryExpiresAt = 0; Show(victoryLayer, false); Show(highlightLayer, false); Show(resultsLayer, false); SetResultLayout(false); renderedResults = default; UpdateInputShake(false); return; }
            var s = p.State.Value; int seat = p.Seat.Value; var mine = s.ForSeat(seat); double now = p.NetworkManager.ServerTime.Time;
            UpdateVictory(s);
            bool greedCountdown=s.Clock.Phase==ClockPhase.Counting&&s.Clock.MiniGame==PartyMiniGame.Greed&&now<s.Clock.StartedAt;
            Show(nameplateLayer, resultsLayer.style.display.value != DisplayStyle.Flex && s.Clock.Phase!=ClockPhase.Selecting && s.Clock.Phase!=ClockPhase.QuickCountdown && s.Clock.Phase!=ClockPhase.Ranking && s.Clock.Phase!=ClockPhase.Memory && s.Clock.Phase!=ClockPhase.ItemPreparation && !greedCountdown);
            if (!hadPlayer || predictionRound != s.Round || s.Stage != DuelStage.Racing || mine.Version >= predicted.Version ||
                Time.realtimeSinceStartupAsDouble - sentAt > 1.5)
            { predicted = mine; waitingForAction = false; predictionRound = s.Round; }
            if (killRound != s.Round) { ClearKills(); killRound = s.Round; lastKillShot = 0; }
            int fired=0;for(int i=0;i<RouletteRules.MaxPlayers;i++)fired+=s.ForSeat(i).ShotsFired;
            UpdateKills();
            previousShots = fired; hadPlayer = true;
            bool actionVisible = s.Clock.Phase==ClockPhase.QuickHands && s.Phase == DuelPhase.Playing && s.Alive(seat) && s.Stage == DuelStage.Racing &&
                predicted.Action != DuelAction.Aim && predicted.Action != DuelAction.Fire && !(ads&&mine.Weapon==DuelWeapon.Revolver);
            bool lobby = s.Stage == DuelStage.Idle && s.Phase != DuelPhase.Playing;
            Show(roomShotSummary,false);
            bool shotCountdown=s.Clock.Phase==ClockPhase.QuickHands&&s.Alive(seat)&&mine.ShootingStartedAt>=0&&s.ShotTimeLimit>0;
            Show(shotTimer,shotCountdown);
            if(shotCountdown)shotTimer.text=$"사격 · {System.Math.Max(0,s.ShotTimeLimit-(now-mine.ShootingStartedAt)):F1}초";
            bool hasResults = s.Results.IsValid && s.Results.Round == s.Round;
            Show(weaponChoice, (lobby || hasResults) && s.Connected(seat));
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                weaponButtons[i].EnableInClassList("selected", mine.SelectedWeapon == (DuelWeapon)i);
                weaponButtons[i].SetEnabled(lobby && !RouletteRules.Has(s.ReadyMask, seat));
            }
            Show(inputPanel, !hasResults && (actionVisible || lobby || s.Stage == DuelStage.Countdown));
            UpdateInputShake(actionVisible);
            Show(turn, s.Phase != DuelPhase.Finished);
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                string detail = !s.Connected(i) ? "빈 자리" : lobby ? (RouletteRules.Has(s.ReadyMask, i) ? "준비 완료" : "준비 대기") : s.Alive(i) ? "생존" : s.InParty(i)?"미니게임 탈락":"탈락";
                Vector3 anchor;
                bool visibleName = s.Connected(i) ? manager.Table.TryGetNameViewport(i, out anchor) :
                    manager.Table.TryGetChairViewport(i, out anchor);
                Show(nameplates[i], visibleName);
                Show(seatStatuses[i], visibleName && s.Connected(i));
                lifeIcons[i].text=new string('♥',System.Math.Clamp(s.ForSeat(i).LivesRemaining,0,RouletteRules.StartingLives));
                Show(lifeIcons[i],visibleName&&s.Connected(i)&&s.ForSeat(i).LivesRemaining>0);
                if (visibleName)
                {
                    nameplates[i].text = s.Connected(i) ? s.NameForSeat(i) : "빈 자리";
                    nameplates[i].style.left = anchor.x * root.resolvedStyle.width;
                    nameplates[i].style.top = (1 - anchor.y) * root.resolvedStyle.height - 22;
                    seatStatuses[i].text = detail;
                    seatStatuses[i].style.left = anchor.x * root.resolvedStyle.width;
                    seatStatuses[i].style.top = (1 - anchor.y) * root.resolvedStyle.height - 45;
                    lifeIcons[i].style.left=anchor.x*root.resolvedStyle.width;
                    lifeIcons[i].style.top=(1-anchor.y)*root.resolvedStyle.height-67;
                }
            }
            Show(digits, actionVisible);
            Show(rematch, (hasResults || lobby) && s.Connected(seat));
            rematch.SetEnabled(lobby && !RouletteRules.Has(s.ReadyMask, seat));
            rematch.text = RouletteRules.Has(s.ReadyMask, seat) ? "준비 완료 · 다른 참가자 대기" : "준비 완료";
            Show(result, !actionVisible);
            if (s.Stage == DuelStage.Countdown)
            { turn.text = $"시작까지  {Mathf.Max(1, Mathf.CeilToInt((float)(DuelDirector.CountdownDuration - now + s.StageStartedAt)))}"; result.text = "전원 준비 완료"; }
            else if (actionVisible)
            {
                turn.text = WeaponActionName(predicted.Weapon,predicted.Action);
                result.text = "";
                for (int i = 0; i < digitLabels.Length; i++)
                {
                    Show(digitLabels[i], i < predicted.Length);
                    digitLabels[i].text = predicted.Weapon == DuelWeapon.Revolver &&
                        (i > predicted.Progress || predicted.Mistakes > mine.Mistakes)
                        ? "" : predicted.Digit(i).ToString();
                    digitLabels[i].EnableInClassList("done", i < predicted.Progress);
                    digitLabels[i].EnableInClassList("current", i == predicted.Progress);
                    digitLabels[i].EnableInClassList("mistake", now < predicted.BlockedUntil);
                }
            }
            else if (lobby)
            {
                int participants = RouletteRules.Count(s.ConnectedMask);
                turn.text = "파티 모드 · " + (participants >= 2 ? "모두 준비하면 시작합니다" : "참가자를 기다리는 중");
                result.text = $"준비 {RouletteRules.Count(s.ReadyMask)} / {participants} · " +
                    (participants >= 2 ? $"현재 {participants}명 전원 준비 시 시작" : "2명 이상부터 시작 가능");
                if (s.Mode == DuelMode.PvP && manager.SessionPort > 0) result.text += $" · 접속 포트 {manager.SessionPort}";
            }
            else { turn.text = ""; result.text = ""; }
        }
        private static string MemoryArrow(int direction)=>direction==0?"↑":direction==1?"↓":direction==2?"←":direction==3?"→":"•";
        private bool IsMemoryTurn()
        {
            var p=HelloWorldPlayer.Local;
            return p!=null&&p.IsSpawned&&p.Seat.Value>=0&&p.State.Value.Phase==DuelPhase.Playing&&
                p.State.Value.Stage==DuelStage.Racing&&p.State.Value.Clock.Phase==ClockPhase.Memory;
        }
        private void SyncMemoryContext(HelloWorldPlayer p)
        {
            var s=p.State.Value;var c=s.Clock;var m=c.Memory;
            if(memoryPartyRound==s.Round&&memoryIteration==c.Iteration&&memoryRound==m.Round)return;
            memoryPartyRound=s.Round;memoryIteration=c.Iteration;memoryRound=m.Round;
            memoryCount=memoryConfirmed=0;
            memoryScroll.scrollOffset=Vector2.zero;
        }
        private void PressMemory(int direction)
        {
            if(!IsMemoryTurn())return;
            var p=HelloWorldPlayer.Local;var s=p.State.Value;var m=s.Clock.Memory;
            double now=p.NetworkManager.ServerTime.Time;
            if(m.Phase!=ArrowMemoryPhase.Input||!RouletteRules.Has(m.ActiveMask,p.Seat.Value)||!s.Alive(p.Seat.Value)||now<m.PhaseAt||now>=m.InputEndsAt)return;
            SyncMemoryContext(p);if(memoryCount>=m.Length)return;
            int index=memoryCount++;
            // Predict progress only; never display or retain the entered directions.
            p.InputMemory(direction,m.Round,index);
        }
        public void ReceiveMemoryInput(int round,int iteration,int roundIndex,bool accepted,int count)
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||p.Seat.Value<0)return;
            var s=p.State.Value;if(s.Round!=round||s.Clock.Iteration!=iteration||s.Clock.Memory.Round!=roundIndex)return;
            SyncMemoryContext(p);count=Mathf.Clamp(count,0,s.Clock.Memory.Length);
            if(accepted){memoryConfirmed=System.Math.Max(memoryConfirmed,count);memoryCount=System.Math.Max(memoryCount,count);}
            else
            {
                memoryConfirmed=memoryCount=count;
            }
        }
        private void RenderMemoryArrows(ArrowMemoryState memory,bool showing,bool input,double now)
        {
            int count=System.Math.Max(0,memory.Length);
            float viewport=memoryScroll.contentViewport.resolvedStyle.width;
            if(float.IsNaN(viewport)||viewport<MemoryCellWidth)viewport=Mathf.Max(MemoryCellWidth,clockPanel.resolvedStyle.width-32);
            if(float.IsNaN(viewport))viewport=Mathf.Max(MemoryCellWidth,Screen.width*.56f-32);
            float span=count*MemoryCellWidth,width=Mathf.Max(viewport,span),overflow=Mathf.Max(0,span-viewport);
            memoryArrows.style.width=width;memoryScroll.contentContainer.style.width=width;
            float offset=0;
            if(showing)
            {
                // Pause briefly at each end, traversing the entire row during the reveal.
                float travel=Mathf.InverseLerp(.35f,(float)DuelArrowMemory.ShowDuration-.35f,(float)(now-memory.PhaseAt));
                offset=overflow*travel;
            }
            else if(input&&count>0)
            {
                int current=System.Math.Min(memoryCount,count-1);
                offset=Mathf.Clamp((current+1)*MemoryCellWidth-viewport,0,overflow);
            }
            memoryScroll.scrollOffset=new Vector2(offset,0);
            // Keep only a viewport's worth of cells, even after many perfect rounds.
            int first=Mathf.Max(0,Mathf.FloorToInt(memoryScroll.scrollOffset.x/MemoryCellWidth)-1);
            int visible=System.Math.Min(count-first,Mathf.CeilToInt(viewport/MemoryCellWidth)+3);
            while(memoryArrowLabels.Count<visible)
            {
                var label=Text(memoryArrows,"","memory-arrow");label.pickingMode=PickingMode.Ignore;memoryArrowLabels.Add(label);
            }
            for(int i=0;i<memoryArrowLabels.Count;i++)
            {
                var label=memoryArrowLabels[i];Show(label,i<visible);if(i>=visible){label.text="";continue;}
                int index=first+i;label.userData=index;
                label.style.left=(index*MemoryCellWidth)+Mathf.Max(0,(viewport-span)*.5f);
                bool entered=input&&index<memoryCount;
                label.text=showing?MemoryArrow(DuelArrowMemory.DirectionAt(memory.AnswerSeed,index)):entered?"●":"•";
                label.EnableInClassList("memory-arrow-entered",entered);
                label.EnableInClassList("memory-arrow-next",input&&index==memoryCount);
                label.EnableInClassList("memory-arrow-hidden",!showing&&!entered);
            }
        }
        private void UpdateMemory(HelloWorldPlayer p,DuelSnapshot s,ClockState clock,double now)
        {
            var memory=clock.Memory;SyncMemoryContext(p);
            bool showing=clock.Phase==ClockPhase.Memory&&memory.Phase==ArrowMemoryPhase.Showing;
            bool input=clock.Phase==ClockPhase.Memory&&memory.Phase==ArrowMemoryPhase.Input;
            bool results=clock.Phase==ClockPhase.Memory&&memory.Phase==ArrowMemoryPhase.Results;
            bool finalist=clock.Phase==ClockPhase.Ranking;
            bool active=RouletteRules.Has(memory.ActiveMask,p.Seat.Value);
            Show(clockStop,false);Show(clockRanking,false);Show(clockRankingRows,results||finalist);
            Show(memoryScroll,showing||input&&active);Show(miniInstruction,true);
            clockTitle.text=$"{PartyGames.Name(PartyMiniGame.Memory)} · {memory.Round}라운드 · {memory.Length}개";
            clockTimer.EnableInClassList("signal-wait",false);clockTimer.EnableInClassList("signal-fake",false);
            clockTimer.EnableInClassList("signal-go",false);clockTimer.EnableInClassList("signal-failure",false);
            if(showing)
            {
                bool reveal=now<memory.PhaseAt+DuelArrowMemory.ShowDuration;
                Show(memoryScroll,reveal);
                clockTimer.text=reveal?$"{System.Math.Min(DuelArrowMemory.ShowDuration,System.Math.Max(0,DuelArrowMemory.ShowDuration-(now-memory.PhaseAt))):F1}초":"입력 준비 중";
                miniInstruction.text=active?"순서를 기억하세요.":"이번 미니게임 탈락 · 관전 중";
                if(reveal)RenderMemoryArrows(memory,true,false,now);
            }
            else if(input)
            {
                bool locked=memoryCount>=memory.Length;
                clockTimer.text=active?$"{System.Math.Min(DuelArrowMemory.InputDuration,System.Math.Max(0,memory.InputEndsAt-now)):F1}초 · {memoryCount}/{memory.Length}":"관전 중";
                miniInstruction.text=!active?"이번 미니게임에서 탈락했습니다.":locked?memoryConfirmed<memoryCount?"입력 확인 중":"입력 확정 · 결과 대기":"방향키로 순서대로 입력하세요.";
                if(active)RenderMemoryArrows(memory,false,true,now);
            }
            else if(results||finalist)
            {
                clockTitle.text=finalist?$"{PartyGames.Name(PartyMiniGame.Memory)} · 최종 순위":$"{PartyGames.Name(PartyMiniGame.Memory)} · {memory.Round}라운드 결과";
                int eliminated=-1;for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(memory.EliminatedMask,i)){eliminated=i;break;}
                clockTimer.text=finalist?(memory.Winner>=0?$"{s.NameForSeat(memory.Winner)} 1등":"승자 없음 · 다음 게임으로"):eliminated>=0?$"{s.NameForSeat(eliminated)} 탈락":"전원 정답";
                miniInstruction.text=finalist?(memory.Winner>=0?"1등에게 사격 기회가 주어집니다.":"다음 미니게임으로 진행합니다."):eliminated>=0?"남은 참가자는 다음 라운드로 진행합니다.":"다음 라운드는 방향이 하나 늘어납니다.";
                var order=new System.Collections.Generic.List<int>();
                int mask=finalist?clock.ParticipantsMask:memory.RoundMask;
                for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(mask,i))order.Add(i);
                order.Sort((a,b)=>finalist?
                    (memory.ForSeat(a).Rank==0?int.MaxValue:memory.ForSeat(a).Rank).CompareTo(memory.ForSeat(b).Rank==0?int.MaxValue:memory.ForSeat(b).Rank):
                    memory.ForSeat(a).Errors.CompareTo(memory.ForSeat(b).Errors));
                for(int row=0;row<clockRankingEntries.Length;row++)
                {
                    var label=clockRankingEntries[row];Show(label,row<order.Count);if(row>=order.Count)continue;
                    int seat=order[row];var entry=memory.ForSeat(seat);bool own=seat==p.Seat.Value;
                    label.EnableInClassList("clock-ranking-own",own);
                    label.text=(finalist&&entry.Rank>0?$"{entry.Rank}위  ":"")+(own?"나 · ":"")+s.NameForSeat(seat)+$"  오입력 {entry.Errors}개"+
                        (entry.Rolls>0?$" · 주사위 {entry.Dice}"+(entry.Rolls>1?$" ({entry.Rolls}회)":""):"")+
                        (!s.Connected(seat)?" · 연결 종료":!finalist?(RouletteRules.Has(memory.EliminatedMask,seat)?" · 탈락":" · 통과"):"");
                }
            }
        }
        public void ReportClockStop(int round,int iteration,bool accepted,int record=0,MiniFailure failure=MiniFailure.None)
        {if(round==clockRound&&iteration==clockIteration){clockStopped=accepted;clockAwaiting=false;clockRecord=record;clockFailure=failure;}}
        private void StopClock()
        {
            var p=HelloWorldPlayer.Local;
            if(p==null || !p.IsSpawned || p.Seat.Value<0)return;
            var s=p.State.Value;double now=p.NetworkManager.ServerTime.Time;
            if(s.Phase!=DuelPhase.Playing || !s.Alive(p.Seat.Value) || s.Clock.MiniGame==PartyMiniGame.QuickHands || s.Clock.MiniGame==PartyMiniGame.Memory || s.Clock.Phase!=ClockPhase.Counting || now<s.Clock.StartedAt || clockStopped)return;
            if(s.Clock.MiniGame==PartyMiniGame.Countdown&&now>s.Clock.StartedAt+s.Clock.Target/100.0+2 || s.Clock.Signal==MiniSignal.Burst || s.Clock.MiniGame==PartyMiniGame.Signal&&s.Clock.Signal==MiniSignal.Go&&now>s.Clock.SignalAt+2)return;
            clockStopped=clockAwaiting=true;p.StopClock(now);
        }
        private void UpdateClock(HelloWorldPlayer p)
        {
            Show(memoryScroll,false);clockPanel.EnableInClassList("memory-panel",false);
            bool visible=p!=null && p.State.Value.Phase==DuelPhase.Playing && p.State.Value.Stage==DuelStage.Racing && p.State.Value.Clock.Phase!=ClockPhase.QuickHands;
            bool manual=visible&&p.State.Value.ManualMiniGame&&p.State.Value.Clock.Phase==ClockPhase.Selecting;
            Show(manualSelection,manual&&p.NetworkManager.IsHost);
            if(manual&&p.NetworkManager.IsHost&&(manualRound!=p.State.Value.Round||manualIteration!=p.State.Value.Clock.Iteration))
            {manualRound=p.State.Value.Round;manualIteration=p.State.Value.Clock.Iteration;BuildManualChoices();}
            bool itemTime=visible&&p.State.Value.Clock.Phase==ClockPhase.ItemPreparation&&p.State.Value.Alive(p.Seat.Value);
            var inventory=visible?p.State.Value.ForSeat(p.Seat.Value):default;
            Show(itemRow,itemTime&&(inventory.HasRecovery||inventory.HasTaser));
            if(visible)
            {
                var a=inventory;var c0=p.State.Value.Clock;
                Show(itemButtons[0],a.HasRecovery);Show(itemButtons[1],a.HasTaser);
                itemButtons[0].SetEnabled(a.HasRecovery&&a.Health<100);bool eligible=c0.MiniGame!=PartyMiniGame.QuickHands&&c0.First>=0&&c0.First!=p.Seat.Value;
                itemButtons[1].SetEnabled(a.HasTaser&&!a.TaserArmed&&(p.State.Value.UnlimitedItems||c0.Iteration>=a.TaserReadyIteration)&&eligible);
                itemButtons[1].EnableInClassList("item-used",a.TaserArmed);
            }
            bool starting=visible && (p.State.Value.Clock.Phase==ClockPhase.QuickCountdown ||
                p.State.Value.Clock.Phase==ClockPhase.Counting&&p.State.Value.Clock.MiniGame==PartyMiniGame.Greed&&p.NetworkManager.ServerTime.Time<p.State.Value.Clock.StartedAt);
            Show(quickStartLayer,starting);
            Show(clockPanel,visible && (p.State.Value.Clock.Phase!=ClockPhase.Selecting||manual&&!p.NetworkManager.IsHost) && !starting);
            Show(partySelection,visible && p.State.Value.Clock.Phase==ClockPhase.Selecting&&!manual);
            if(!visible){clockRound=clockIteration=manualRound=manualIteration=-1;memoryPartyRound=memoryIteration=memoryRound=-1;memoryCount=memoryConfirmed=0;clockStopped=false;reelRound=reelIteration=-1;reelSound.Stop();return;}
            var s=p.State.Value;var c=s.Clock;double now=p.NetworkManager.ServerTime.Time;
            if(starting)
            {quickStartTitle.text=PartyGames.Name(c.MiniGame);quickStartNumber.text=now<c.StartedAt?System.Math.Ceiling(c.StartedAt-now).ToString("0"):"시작!";return;}
            float viewport=root.resolvedStyle.width>0?root.resolvedStyle.width:Screen.width;
            float gutter=Mathf.Max(viewport*.22f,s.Alive(p.Seat.Value)?0:Mathf.Min(280,viewport*.25f)+32);
            clockPanel.style.left=gutter;clockPanel.style.right=gutter;
            if(clockRound!=s.Round || clockIteration!=c.Iteration){clockRound=s.Round;clockIteration=c.Iteration;clockStopped=clockAwaiting=false;clockRecord=0;clockFailure=MiniFailure.None;lastMiniSignal=MiniSignal.Wait;}
            if(manual){clockTitle.text="미니게임 직접 선택";clockTimer.text="방장이 미니게임을 선택하고 있습니다.";Show(clockStop,false);Show(clockRankingRows,false);Show(clockRanking,false);Show(shootingNotice,false);Show(miniInstruction,false);return;}
            if(c.Phase==ClockPhase.Selecting)
            {
                double age=now-c.PhaseAt;float t=Mathf.Clamp01((float)(age/PartyGames.SpinDuration));
                float travel=(PartyGames.Count*12+PartyGames.IndexOf(c.MiniGame)-2)*(1-Mathf.Pow(1-t,3));
                int index=Mathf.FloorToInt(travel);
                if(reelRound!=s.Round || reelIteration!=c.Iteration)
                {reelRound=s.Round;reelIteration=c.Iteration;reelIndex=index;reelStopped=t>=1;}
                if(t<1 && index!=reelIndex)
                {reelSound.pitch=Mathf.Lerp(1.15f,.85f,t);reelSound.PlayOneShot(reelTickSound,.7f);}
                if(t>=1 && !reelStopped)
                {reelStopped=true;reelSound.pitch=1;reelSound.PlayOneShot(reelStopSound);}
                reelIndex=index;
                partyReel.style.translate=new Translate(0,-60-(travel-index)*60,0);
                for(int i=0;i<partyReelLabels.Length;i++)
                {partyReelLabels[i].text=PartyGames.Name(PartyGames.At(index+i));partyReelLabels[i].style.opacity=Mathf.Lerp(.25f,1,1-Mathf.Clamp01(Mathf.Abs(i-2-(travel-index))));}
                partySelectionTitle.text=t<1?"미니게임 선택 중":"선택된 미니게임";
                partySelection.EnableInClassList("selected",t>=1);return;
            }
            Show(clockStop,c.Phase==ClockPhase.Counting && s.Alive(p.Seat.Value));
            Show(clockRanking,c.Phase==ClockPhase.QuickResults);
            Show(clockRankingRows,c.Phase==ClockPhase.Ranking);
            bool shotStage=c.MiniGame!=PartyMiniGame.QuickHands&&c.First>=0&&s.Alive(c.First)&&RouletteRules.Count(s.AliveMask)>=2&&
                (c.Phase==ClockPhase.Ranking||c.Phase==ClockPhase.ItemPreparation||c.Phase==ClockPhase.Shooting);
            bool shooter=p.Seat.Value==c.First || c.Phase==ClockPhase.Shooting&&s.ForSeat(p.Seat.Value).TaserArmed;
            Show(shootingNotice,shotStage);
            shootingNotice.EnableInClassList("shooting-notice-own",shooter);
            if(shotStage)shootingNotice.text=c.Phase==ClockPhase.Shooting?
                (shooter?"아무나 쏘세요.":"행운을 비십시오."):
                (shooter?"당신은 사격자입니다. 사격을 준비하세요.":$"{s.NameForSeat(c.First)} 사격자가 준비 중입니다.");
            if(c.MiniGame==PartyMiniGame.Memory&&(c.Phase==ClockPhase.Memory||c.Phase==ClockPhase.Ranking))
            {clockPanel.EnableInClassList("memory-panel",true);UpdateMemory(p,s,c,now);return;}
            bool signalGame=c.Phase==ClockPhase.Counting&&c.MiniGame==PartyMiniGame.Signal;
            // Incoming state can arrive before the client's buffered ServerTime reaches its cue timestamp.
            var visibleSignal=c.Signal==MiniSignal.Go&&now<c.SignalAt?MiniSignal.Wait:c.Signal;
            clockTimer.EnableInClassList("signal-wait",signalGame&&visibleSignal==MiniSignal.Wait);
            clockTimer.EnableInClassList("signal-fake",signalGame&&visibleSignal==MiniSignal.Fake);
            clockTimer.EnableInClassList("signal-go",signalGame&&visibleSignal==MiniSignal.Go);
            clockTimer.EnableInClassList("signal-failure",c.Phase==ClockPhase.Counting&&(clockFailure!=MiniFailure.None||c.Signal==MiniSignal.Burst&&!clockStopped));
            Show(miniInstruction,c.Phase==ClockPhase.Counting&&c.MiniGame!=PartyMiniGame.Countdown);
            if(c.Phase==ClockPhase.Counting)
            {
                double elapsed=now-c.StartedAt;
                bool expired=c.MiniGame==PartyMiniGame.Countdown?elapsed>c.Target/100.0+2:c.MiniGame==PartyMiniGame.Greed?c.Signal==MiniSignal.Burst:c.Signal==MiniSignal.Go&&now>c.SignalAt+2;
                clockStop.SetEnabled(!clockStopped&&elapsed>=0&&!expired);
                clockTitle.text=c.MiniGame==PartyMiniGame.Countdown?$"목표 {c.Target/100.0:F2}초":PartyGames.Name(c.MiniGame);
                if(c.MiniGame==PartyMiniGame.Countdown)
                {clockTimer.text=clockStopped?"정지 완료 · 결과 대기":expired?"정지 마감":elapsed<0?"준비":elapsed<c.HideAfter?$"{elapsed:F2}":"••.••";clockStop.text="정지 · Space / 왼쪽 클릭";}
                else if(c.MiniGame==PartyMiniGame.Signal)
                {
                    miniInstruction.text="초록색 ‘지금!’ 신호에서만 입력하세요.";
                    clockTimer.text=clockAwaiting?"입력 확인 중":clockStopped?clockFailure==MiniFailure.None?$"반응 {clockRecord/100.0:F2}초":"너무 빨랐습니다!":expired?"입력 마감":visibleSignal==MiniSignal.Go?"지금!":visibleSignal==MiniSignal.Fake?"지금?":"기다리세요";
                    clockStop.text="반응 · Space / 왼쪽 클릭";
                    if(lastMiniSignal!=visibleSignal){if(visibleSignal==MiniSignal.Go)reelSound.PlayOneShot(reelStopSound,.55f);else if(visibleSignal==MiniSignal.Fake)reelSound.PlayOneShot(reelTickSound,.35f);lastMiniSignal=visibleSignal;}
                }
                else
                {
                    miniInstruction.text="멈춰 점수를 확정하세요. 터지면 0점!";
                    string risk=elapsed<3?"낮음":elapsed<6?"상승 중":"높음";
                    clockTimer.text=clockAwaiting?"확정 중":clockStopped?clockFailure==MiniFailure.None?$"{clockRecord}점 확정":"터졌습니다! 0점":c.Signal==MiniSignal.Burst?"터졌습니다! 0점":elapsed<0?"준비":$"{DuelClockContest.GreedScoreAt(c.StartedAt,now)}점\n위험도 · {risk}";
                    clockStop.text="점수 확정 · Space / 왼쪽 클릭";
                }
            }
            else if(c.Phase==ClockPhase.QuickResults)
            {
                clockTitle.text=c.First>=0&&s.Connected(c.First)?$"{s.NameForSeat(c.First)} · 퀵핸즈 승리":"퀵핸즈 종료";
                clockTimer.text="사망한 참가자는 목숨을 하나 잃었습니다.";
                clockRanking.text=$"목숨이 남은 참가자는 회복하고 다음 미니게임에 참가합니다.\n선택까지 {System.Math.Max(0,DuelClockContest.QuickResultsDuration-(now-c.PhaseAt)):F1}초";
            }
            else if(c.Phase==ClockPhase.Ranking)
            {
                clockTitle.text=c.MiniGame==PartyMiniGame.Countdown?$"목표 {c.Target/100.0:F2}초 · 순위":PartyGames.Name(c.MiniGame)+" · 순위";clockTimer.text=c.First<0?"전원 실패 · 다음 게임으로": "";
                var order=new System.Collections.Generic.List<int>();
                for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(c.ParticipantsMask,i))order.Add(i);
                order.Sort((a,b)=>(c.ForSeat(a).Rank==0?int.MaxValue:c.ForSeat(a).Rank).CompareTo(c.ForSeat(b).Rank==0?int.MaxValue:c.ForSeat(b).Rank));
                for(int row=0;row<clockRankingEntries.Length;row++)
                {
                    var label=clockRankingEntries[row];Show(label,row<order.Count);
                    if(row>=order.Count)continue;
                    int i=order[row];var e=c.ForSeat(i);bool own=i==p.Seat.Value;
                    label.EnableInClassList("clock-ranking-own",own);
                    label.text=(e.Rank==0?"":$"{e.Rank}위  ")+(own?"나 · ":"")+s.NameForSeat(i)+"  "+
                        ResultRecord(c.MiniGame,e)+
                        (e.Rolls>0?$" · 주사위 {e.Dice}"+(e.Rolls>1?$" ({e.Rolls}회)":""):"");
                }
            }
            else if(c.Phase==ClockPhase.ItemPreparation)
            {clockTitle.text="아이템 사용";clockTimer.text=$"{System.Math.Max(0,c.StartedAt-now):F1}초";}
            else if(c.Phase==ClockPhase.Shooting)
            {clockTitle.text="사격 시간";clockTimer.text=s.ShotTimeLimit==0?"무제한":$"{System.Math.Max(0,s.ShotTimeLimit-(now-c.PhaseAt)):F1}초";}
            else {clockTitle.text="다음 미니게임 준비";clockTimer.text=$"선택까지 {System.Math.Max(0,DuelClockContest.ReturnDuration-(now-c.PhaseAt)):F1}초";}
        }
        private static string ResultRecord(PartyMiniGame game,ClockEntry e)
        {
            if(e.Failure==MiniFailure.EarlyInput)return "성급한 입력 · 실패";
            if(e.Failure==MiniFailure.MissedSignal)return "미입력 · 실패";
            if(e.Failure==MiniFailure.Burst)return "폭발 · 0점";
            if(game==PartyMiniGame.Greed)return $"{e.Points}점";
            if(game==PartyMiniGame.Signal)return $"반응 {e.Centiseconds/100.0:F2}초";
            return e.Centiseconds<0?"시간 초과 · 목숨 차감":$"{e.Centiseconds/100.0:F2}초 · 오차 {e.Error/100.0:F2}초";
        }
        public void ReportHit(int round,int life,int shot)
        {
            var p=HelloWorldPlayer.Local;
            if(p==null || !p.IsSpawned || p.Seat.Value<0 || p.State.Value.Round!=round ||
                p.State.Value.ForSeat(p.Seat.Value).Life!=life)return;
            if(hitRound!=round || hitLife!=life){hitRound=round;hitLife=life;lastHitShot=0;}
            if(shot<=lastHitShot)return;
            lastHitShot=shot;hitExpiresAt=Time.realtimeSinceStartupAsDouble+.24;
        }
        private void UpdateHitMarker(HelloWorldPlayer p)
        {
            bool valid=p!=null && p.State.Value.Round==hitRound && p.State.Value.ForSeat(p.Seat.Value).Life==hitLife && p.State.Value.Alive(p.Seat.Value);
            double remaining=hitExpiresAt-Time.realtimeSinceStartupAsDouble;
            bool visible=valid&&remaining>0;Show(hitMarker,visible);
            if(visible){hitMarker.style.opacity=Mathf.Clamp01((float)(remaining/.12));hitMarker.BringToFront();}
            if(p==null){hitRound=hitLife=-1;lastHitShot=0;hitExpiresAt=0;}
        }
        private void UpdateDeathDetails(HelloWorldPlayer player)
        {
            bool timeout=player!=null && player.State.Value.ForSeat(player.Seat.Value).DeathCause==DuelDeathCause.ClockTimeout;
            bool visible = player != null && player.State.Value.ForSeat(player.Seat.Value).DiedAt >= 0 &&
                (timeout || (player.DeathReport.Value.Round == player.State.Value.Round && player.DeathReport.Value.Life==player.State.Value.ForSeat(player.Seat.Value).Life && player.DeathReport.Value.Count > 0));
            Show(deathDetails, visible);
            // Equal gutters keep the report aligned with the victory banner's screen center.
            float viewportWidth = root.resolvedStyle.width;
            if (!(viewportWidth > 0)) viewportWidth = Screen.width;
            float width = Mathf.Min(280, viewportWidth * .25f);
            if (player != null && player.State.Value.Results.IsValid)
                width = Mathf.Min(width, Mathf.Max(180, (viewportWidth - 780) * .5f - 32));
            deathDetails.style.width = width;
            float gutter = visible ? width + 32 : viewportWidth * .03f;
            resultsLayer.style.left = gutter;
            resultsLayer.style.right = gutter;
            if (!visible) { renderedDeathReport = default;renderedDeathCause=DuelDeathCause.None; return; }
            var report = player.DeathReport.Value;
            var cause=player.State.Value.ForSeat(player.Seat.Value).DeathCause;
            if (renderedDeathReport.Equals(report) && renderedDeathCause==cause) return;
            renderedDeathCause=cause;
            renderedDeathReport = report; deathRows.Clear();
            if(timeout)
            {var row=Box(deathRows,"death-hit");row.AddToClassList("death-hit-fatal");Text(row,"목숨 차감 · 초읽기 시간 초과","death-hit-attacker");Text(row,"목표 시간 +2초까지\n정지하지 않았습니다.","death-hit-info");}
            int total = 0;
            for (int i = 0; i < report.Count; i++)
            {
                var entry = report.ForHit(i); total += entry.Damage;
                var row = Box(deathRows, "death-hit");
                row.pickingMode = PickingMode.Ignore;
                row.EnableInClassList("death-hit-fatal", entry.Fatal);
                Text(row, (entry.Fatal ? "최종 타격 · " : "공격자 · ") + entry.Attacker.ToString(), "death-hit-attacker").pickingMode = PickingMode.Ignore;
                string part = entry.Splash ? "주변 피해 (스플래시)" : entry.Weapon==DuelWeapon.Taser?(entry.Part==DuelHitPart.Head?"머리":entry.Part==DuelHitPart.Body?"몸통":"다리")+" · 감전 (고정 피해)" : entry.Weapon==DuelWeapon.SawedOff ? (entry.Part==DuelHitPart.Head?"머리":entry.Part==DuelHitPart.Body?"몸통":"다리")+" · 방사 직격 (부위 동일)" : entry.Part == DuelHitPart.Head ? "머리 · 직격" :
                    entry.Part == DuelHitPart.Body ? "몸통 · 직격" : "다리 · 직격";
                Text(row, WeaponRules.Name(entry.Weapon) + "\n" + part + "\n피해 " + entry.Damage, "death-hit-info").pickingMode = PickingMode.Ignore;
            }
            deathSummary.text = timeout && report.Count==0?"사인: 초읽기 시간 초과":$"총 받은 피해 {total}\n{report.Count}회 피격";
        }
        private void ClearKills() { kills.Clear(); killFeed.Clear(); lastKillVictims = 0; }
        private void UpdateVictory(DuelSnapshot state)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (victoryRound != state.Round) { victoryRound = state.Round; victoryExpiresAt = 0; }
            bool finished = state.Results.IsValid && state.Results.Round == state.Round;
            SetResultLayout(finished);
            if (finished)
            {
                if (announcedVictoryRound != state.Round)
                {
                    announcedVictoryRound = state.Round;
                    victoryShownAt = hadPlayer ? now : now - ResultsStart - 0.1;
                    victoryText.text = state.Results.Winner<0?"전원 탈락":state.Results.ForSeat(state.Results.Winner).Name.ToString() + " 최종 승리";
                    victoryText.RemoveFromClassList("death-banner");
                    victoryExpiresAt = 0;
                    highlightText.text = state.Results.Highlight.ToString();
                }
                float rise = Mathf.Clamp01((float)((now - victoryShownAt - VictoryHold) / VictoryRise));
                rise = rise * rise * (3 - 2 * rise);
                float height = float.IsNaN(root.resolvedStyle.height) ? Screen.height : root.resolvedStyle.height;
                victoryText.style.translate = new Translate(0, (64 - height * .5f) * rise, 0);
                Show(victoryLayer, true); victoryLayer.style.opacity = 1;
                double elapsed = now - victoryShownAt;
                Show(highlightLayer, elapsed >= HighlightStart);
                float factRise = Mathf.Clamp01((float)((elapsed - HighlightStart - HighlightHold) / HighlightRise));
                factRise = factRise * factRise * (3 - 2 * factRise);
                highlightText.style.translate = new Translate(0, (152 - height * .5f) * factRise, 0);
                Show(resultsLayer, elapsed >= ResultsStart);
                UpdateResults(state);
                return;
            }
            var owner=HelloWorldPlayer.Local;
            if(owner!=null && state.ForSeat(owner.Seat.Value).Life!=deathBannerLife && victoryText.ClassListContains("death-banner"))
            {victoryExpiresAt=0;victoryText.RemoveFromClassList("death-banner");}
            if(owner!=null && owner.IsSpawned && state.ForSeat(owner.Seat.Value).DeathCause==DuelDeathCause.ClockTimeout && (announcedTimeoutRound!=state.Round||deathBannerLife!=state.ForSeat(owner.Seat.Value).Life))
            {announcedTimeoutRound=state.Round;deathBannerLife=state.ForSeat(owner.Seat.Value).Life;victoryText.text=DeathMessage("초읽기 시간 초과",state.ForSeat(owner.Seat.Value).LivesRemaining);victoryText.AddToClassList("death-banner");victoryExpiresAt=now+4;}
            Show(resultsLayer, false);
            Show(highlightLayer, false);
            victoryText.style.translate = new Translate(0, 0, 0);
            float remaining = (float)(victoryExpiresAt - Time.realtimeSinceStartupAsDouble);
            Show(victoryLayer, remaining > 0);
            victoryLayer.style.opacity = Mathf.Clamp01(remaining);
        }
        private void UpdateResults(DuelSnapshot state)
        {
            var data = state.Results;
            if (!renderedResults.Equals(data))
            {
                renderedResults = data; resultsRows.Clear();
                resultsSummary.text = $"{data.Round}라운드 · 경기 시간 {data.Duration:F1}초";
                var seats = new System.Collections.Generic.List<int>();
                for (int seat = 0; seat < RouletteRules.MaxPlayers; seat++)
                    if (RouletteRules.Has(data.ParticipantsMask, seat)) seats.Add(seat);
                seats.Sort((a, b) => a == b ? 0 : a == data.Winner ? -1 : b == data.Winner ? 1 :
                    data.ForSeat(b).Kills != data.ForSeat(a).Kills ? data.ForSeat(b).Kills.CompareTo(data.ForSeat(a).Kills) : a.CompareTo(b));
                foreach (int seat in seats)
                {
                    var entry = data.ForSeat(seat); var row = Box(resultsRows, "results-row");
                    if (seat == data.Winner) row.AddToClassList("results-winner");
                    ResultCell(row, entry.Name.ToString(), true); ResultCell(row, entry.Mistakes.ToString()); ResultCell(row, entry.Kills.ToString());
                    int attempts = entry.CorrectInputs + entry.Mistakes;
                    ResultCell(row, attempts == 0 ? "—" : $"{100f * entry.CorrectInputs / attempts:F1}%");
                    ResultCell(row,entry.ClockWins.ToString());ResultCell(row,entry.ClockStops==0?"—":$"{entry.ClockErrorTotal/(entry.ClockStops*100.0):F2}초");
                }
            }
            int participants = RouletteRules.Count(state.ConnectedMask);
            resultsReady.text = state.Stage != DuelStage.Idle ? "경기 종료 중…" : participants < 2 ? "참가자를 기다리는 중" :
                $"준비 {RouletteRules.Count(state.ReadyMask)} / {participants}";
        }
        private static string DeathMessage(string cause,int remainingLives)=>
            (remainingLives>0?"목숨을 잃었습니다!":"사망했습니다!")+"\n사인 ("+cause+")\n남은 목숨: "+remainingLives;
        public void ReportKill(int round, int shot, int victimSeat, DuelWeapon weapon, string attacker, string victim,int victimLife=-1,int remainingLives=-1,DuelHitPart part=DuelHitPart.None)
        {
            if (root == null || !manager.Busy || round < killRound || victimSeat < 0 || victimSeat >= RouletteRules.MaxPlayers || !WeaponRules.IsValid(weapon)) return;
            if (round != killRound) { ClearKills(); killRound = round; lastKillShot = 0; }
            if (shot < lastKillShot || shot == lastKillShot && RouletteRules.Has(lastKillVictims, victimSeat)) return;
            if (shot > lastKillShot) lastKillVictims = 0;
            lastKillShot = shot; lastKillVictims |= 1 << victimSeat; AddKill(attacker, victim, weapon,part);
            var local = HelloWorldPlayer.Local;
            if (local != null && local.IsSpawned && local.Seat.Value == victimSeat && announcedVictoryRound != round && (victimLife<0||local.State.Value.ForSeat(victimSeat).Life==victimLife))
            {
                victoryRound = round;
                deathBannerLife=local.State.Value.ForSeat(victimSeat).Life;
                victoryText.text = DeathMessage(attacker,remainingLives<0?local.State.Value.ForSeat(victimSeat).LivesRemaining:remainingLives);
                victoryText.AddToClassList("death-banner");
                victoryExpiresAt = Time.realtimeSinceStartupAsDouble + 4;
                Show(victoryLayer, true); victoryLayer.style.opacity = 1;
            }
        }
        private void AddKill(string attacker, string victim, DuelWeapon weapon,DuelHitPart part)
        {
            while (kills.Count >= 3) kills.Dequeue().Row.RemoveFromHierarchy();
            var row = Box(killFeed, "kill-row"); row.pickingMode = PickingMode.Ignore;
            Text(row, attacker, "kill-name").pickingMode = PickingMode.Ignore;
            var icon = new Image { image = weaponIcons[(int)weapon], pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("kill-weapon"); row.Add(icon);
            if(part==DuelHitPart.Head||part==DuelHitPart.Legs)
            {
                var partIcon=new Image{image=part==DuelHitPart.Head?headshotIcon:legshotIcon,pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};
                partIcon.AddToClassList("kill-part-icon");row.Add(partIcon);
            }
            Text(row, victim, "kill-name").pickingMode = PickingMode.Ignore;
            kills.Enqueue(new KillEntry { Row = row, ExpiresAt = Time.realtimeSinceStartupAsDouble + 6 });
        }
        private void UpdateKills()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            while (kills.Count > 0 && kills.Peek().ExpiresAt <= now) kills.Dequeue().Row.RemoveFromHierarchy();
            foreach (var entry in kills) entry.Row.style.opacity = Mathf.Clamp01((float)(entry.ExpiresAt - now));
        }
        private static Texture2D MakeWeaponIcon(DuelWeapon weapon)
        {
            const int width = 96, height = 48;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = WeaponRules.Name(weapon) + " kill icon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                bool saw = weapon == DuelWeapon.SawedOff;
                float guard = Mathf.Pow((x - 41) / 9f, 2) + Mathf.Pow((y - 20) / 7f, 2);
                bool body = x >= (saw ? 28 : 32) && x <= 52 && y >= 25 && y <= 34;
                bool barrel = x >= 48 && x <= 91 && y >= 30 && y <= 36;
                bool cylinder=!saw&&x>=37&&x<=55&&y>=25&&y<=38;
                bool grip = saw ? y >= 12 && y <= 27 && x >= 7 + (y - 12) && x <= 24 + (y - 12) :
                    y >= 6 && y <= 26 && x >= 22 + (y - 6) * .4f && x <= 35 + (y - 6) * .4f;
                bool forearm = saw && x >= 55 && x <= 76 && y >= 25 && y <= 30;
                bool detail = (x >= 85 && x <= 88 && y >= 36 && y <= 39) ||
                    (x >= 30 && x <= 35 && y >= 33 && y <= 40) || (guard <= 1 && guard >= .57f) ||
                    (x >= 39 && x <= 41 && y >= 18 && y <= 25);
                if (body || barrel || grip || forearm || detail || cylinder)
                {
                    bool flute=cylinder&&y>=29&&y<=34&&(x==40||x==46||x==52);
                    if(!flute)pixels[y * width + x] = new Color(.9f, .86f, .7f, 1);
                }
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }
        private static bool IsUnderAim(DuelSnapshot state, int seat)
        {
            if (seat < 0 || seat >= RouletteRules.MaxPlayers || !state.Connected(seat) || !state.Alive(seat) ||
                state.Phase != DuelPhase.Playing || state.Stage != DuelStage.Racing) return false;
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                var action = state.ForSeat(i);
                if (i != seat && state.Connected(i) && state.Alive(i) && action.Target == seat &&
                    action.Weapon!=DuelWeapon.Taser && action.Ammo>0 && action.Aiming) return true;
            }
            return false;
        }
        private static Texture2D MakeAimVignette(bool black = false)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Aim warning vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float horizontal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.74f, 1, Mathf.Abs(x / (size - 1f) * 2 - 1)));
                float vertical = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.74f, 1, Mathf.Abs(y / (size - 1f) * 2 - 1)));
                pixels[y * size + x] = new Color(black?0:0.55f, black?0:0.025f, black?0:0.015f, (1 - (1 - horizontal) * (1 - vertical)) * 0.42f);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }
        private static AudioClip MakeReelSound(bool stop)
        {
            const int rate=22050;var data=new float[(int)(rate*(stop?.24:.045))];var random=new System.Random(73);
            for(int i=0;i<data.Length;i++)
            {
                double t=i/(double)rate;
                data[i]=(float)(stop?(System.Math.Sin(t*System.Math.PI*2*660)+.35*System.Math.Sin(t*System.Math.PI*2*990))*System.Math.Exp(-t*16)*.45:
                    (System.Math.Sin(t*System.Math.PI*2*1850)*.5+(random.NextDouble()*2-1)*.5)*System.Math.Exp(-t*110)*.65);
            }
            var clip=AudioClip.Create(stop?"Mini-game selected":"Mini-game reel tick",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void OnDestroy() { if(headshotIcon!=null)Destroy(headshotIcon);if(legshotIcon!=null)Destroy(legshotIcon);if (font != null) Destroy(font);if(reelTickSound!=null)Destroy(reelTickSound);if(reelStopSound!=null)Destroy(reelStopSound); if (aimVignette != null) Destroy(aimVignette); if(scopeVignette!=null)Destroy(scopeVignette); foreach (var icon in weaponIcons) if (icon != null) Destroy(icon);foreach(var icon in itemIcons)if(icon!=null)Destroy(icon);if(healTexture!=null)Destroy(healTexture);if(electricTexture!=null)Destroy(electricTexture); }
    }
}



