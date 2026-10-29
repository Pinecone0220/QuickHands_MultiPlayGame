using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView : MonoBehaviour
    {
        private HelloWorldManager manager;
        private DuelGameAudio feedback;
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
        private Label clockStop;
        private VisualElement partySelection, partyReel;
        private VisualElement quickStartLayer;
        private Label quickStartNumber,quickStartTitle;
        private Label partySelectionTitle,miniGameName;
        private readonly Label[] partyReelLabels=new Label[5];
        private readonly VisualElement[] partyReelStrikes=new VisualElement[5];
        private DuelDeathCause renderedDeathCause;
        private int announcedTimeoutRound=-1;
        private int deathBannerLife=-1;
        private int clockRound=-1, clockIteration=-1;
        private bool clockStopped,clockAwaiting;
        private int clockRecord;
        private MiniFailure clockFailure;
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
        private int resultCueMask;
        private const double VictoryHold = 0.8, VictoryRise = 0.6;
        private const double HighlightStart = VictoryHold + VictoryRise, HighlightHold = 0.8, HighlightRise = 0.6;
        private const double ResultsStart = HighlightStart + HighlightHold + HighlightRise;
        private const double ResultFooterDelay=.65,ResultFooterDuration=.55;
        private VisualElement resultsLayer, resultsRows, resultsFooter;
        private Label resultsSummary, resultsReady;
        private bool resultLayout;
        private DuelRoundResults renderedResults;
        private readonly Label[] digitLabels = new Label[DuelActionState.InputLength];
        private readonly Label[] nameplates = new Label[RouletteRules.MaxPlayers];
        private readonly Label[] seatStatuses = new Label[RouletteRules.MaxPlayers];
        private readonly Label[] lifeIcons = new Label[RouletteRules.MaxPlayers];
        private readonly Label[] seatHealth = new Label[RouletteRules.MaxPlayers];
        public const float NameplateHealthHeight=24;
        private VisualElement localPlayerInfo;
        private Image localWeaponIcon;
        private Button addBot,removeBot;
        private VisualElement botControls;
        private readonly Button[] botLevels=new Button[3];
        private Label localHealth;
        private VisualElement nameplateLayer;
        private VisualElement aimWarning, scopeShade, deathDetails, deathRows;
        private Label deathSummary;
        private DuelDamageReport renderedDeathReport;
        private Texture2D aimVignette, scopeVignette,hitVignette;
        private readonly Texture2D[] weaponIcons = new Texture2D[WeaponRules.Count];
        private VisualElement weaponChoice;
        private readonly Button[] weaponButtons = new Button[WeaponRules.Selectable.Length];
        private readonly System.Collections.Generic.Queue<KillEntry> kills = new System.Collections.Generic.Queue<KillEntry>();
        private int killRound = -1;
        private int lastKillShot;
        private int lastKillVictims;
        private sealed class KillEntry { public VisualElement Row; public double ExpiresAt; }
        private Button rematch, leave, fullscreen,practiceCancel,hostPractice,hostPracticeStart;
        private VisualElement practiceControls;
        private VisualElement roomSettings;
        private VisualElement developerGameSettings,developerGoldSettings;
        private IntegerField developerGoldAmount;
        private Label developerGoldBalance;
        private bool menuDeveloperMode;
        private Toggle unlimitedShot;
        private Toggle manualMiniGame,unlimitedItems,noItemCooldown,autoSelectItems;
        private Label loadoutTitle;
        private VisualElement loadoutChoice;
        private readonly Button[] loadoutButtons=new Button[DuelLoadout.ItemCount];
        private VisualElement manualSelection,itemRow,healOverlay,electricOverlay;
        private readonly Button[] itemButtons=new Button[DuelLoadout.ItemCount];
        private readonly Label[] itemStatuses=new Label[DuelLoadout.ItemCount];
        private Label recoveryUsedMark;
        private int manualRound=-1,manualIteration=-1;
        private readonly Texture2D[] itemIcons=new Texture2D[DuelLoadout.ItemCount];
        private readonly Texture2D[] itemInfoIcons=new Texture2D[DuelLoadout.ItemCount];
        private Texture2D healTexture,electricTexture;
        private readonly VisualElement[] weaponInfoCards=new VisualElement[WeaponRules.Selectable.Length],itemInfoCards=new VisualElement[DuelLoadout.ItemCount];
        private Label infoTitle,infoIntro;
        private IntegerField shotSeconds;
        private Label shotTimer, roomShotSummary;
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
        private VisualElement hitMarker,crosshair;
        private int hitRound=-1,hitLife=-1,lastHitShot;
        private double hitExpiresAt;
        private HelloWorldPlayer practiceHitOwner;
        private int practiceHitRound=-1,practiceHitSession=-1,lastPracticeHit;
        private bool hitIsPractice;
        public void Initialize(HelloWorldManager owner)
        {
            manager = owner; spectator=GetComponent<DuelSpectator>(); feedback=GetComponent<DuelGameAudio>(); root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear(); root.AddToClassList("duel-root");
            // Enter belongs to chat, never to the previously focused UI control.
            root.RegisterCallback<NavigationSubmitEvent>(e=>e.StopImmediatePropagation(),TrickleDown.TrickleDown);
            var style = Resources.Load<StyleSheet>("Roulette"); if (style != null) root.styleSheets.Add(style);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            foreach(var weapon in WeaponRules.Selectable)weaponIcons[(int)weapon]=MakeWeaponIcon(weapon);
            headshotIcon=DuelHitPartArt.Icon(DuelHitPart.Head);legshotIcon=DuelHitPartArt.Icon(DuelHitPart.Legs);
            for(int i=0;i<DuelLoadout.ItemCount;i++){itemIcons[i]=DuelItemArt.Icon((DuelItem)i);itemInfoIcons[i]=DuelItemArt.Icon((DuelItem)i);}weaponIcons[2]=DuelItemArt.Icon(DuelItem.Taser);
            RefreshSkinIcons();
            root.style.unityFont = font; root.pickingMode = PickingMode.Ignore;
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.contentViewport.style.overflow=Overflow.Hidden; scroll.AddToClassList("menu-scroll"); root.Add(scroll); menu = scroll;
            var panel = Box(scroll, "menu-panel");
            Text(panel, "Quick Hands", "wordmark");
            Text(panel, "퀵핸즈", "title");
            Text(panel,"파티 모드","eyebrow");
            Button(panel,"패치노트",()=>SetPatchNotes(true),"secondary");
            Button(panel,"미니게임 정보",()=>{SetWeaponInfo(true);SetMiniGameInfo();},"secondary");
            weaponInfoButton=Button(panel,"무기 정보",()=>{SetWeaponInfo(true);SetInfoMode(false);},"secondary");
            Button(panel,"아이템 정보",()=>{SetWeaponInfo(true);SetInfoMode(true);},"secondary");
            var nickname = new TextField("닉네임") { value = manager.Nickname, maxLength = 64 };
            nickname.RegisterValueChangedCallback(e => manager.Nickname = e.newValue);
            panel.Add(nickname);
            Text(panel, "최대 16자 · 비워 두면 입장 순서대로 player1, player2…", "fineprint");
            Text(panel, "01  /  AI DUEL", "eyebrow");
            Button(panel, "싱글플레이 로비", () => manager.Begin(DuelMode.AI, false, "127.0.0.1", "7777"), "primary");
            Text(panel, "02  /  PVP DUEL", "eyebrow");
            BuildRoomMenu(panel);
            var musicCredit=Text(panel,"BGM: Local Forecast - Elevator\nKevin MacLeod (incompetech.com) · CC BY 3.0","fineprint");
            musicCredit.tooltip="음악 라이선스 보기 · https://creativecommons.org/licenses/by/3.0/";
            musicCredit.RegisterCallback<ClickEvent>(_=>Application.OpenURL("https://creativecommons.org/licenses/by/3.0/"));
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
            clockStop=Text(clockPanel,"정지 · Space","clock-stop-hint");clockStop.pickingMode=PickingMode.Ignore;
            partySelection=Box(root,"party-selection-layer");partySelection.pickingMode=PickingMode.Ignore;
            miniGameName=Text(root,"","mini-game-name");miniGameName.pickingMode=PickingMode.Ignore;Show(miniGameName,false);
            var selector=Box(partySelection,"party-selector");selector.pickingMode=PickingMode.Ignore;
            Text(selector,"파티 모드","party-wordmark").pickingMode=PickingMode.Ignore;
            partySelectionTitle=Text(selector,"미니게임 선택 중","party-selection-title");
            var reelWindow=Box(selector,"party-reel-window");reelWindow.pickingMode=PickingMode.Ignore;
            partyReel=Box(reelWindow,"party-reel");partyReel.pickingMode=PickingMode.Ignore;
            for(int i=0;i<partyReelLabels.Length;i++){var row=Box(partyReel,"party-reel-row");row.pickingMode=PickingMode.Ignore;partyReelLabels[i]=Text(row,"","party-reel-label");partyReelLabels[i].pickingMode=PickingMode.Ignore;partyReelStrikes[i]=Box(row,"party-reel-strike");partyReelStrikes[i].pickingMode=PickingMode.Ignore;}
            Box(reelWindow,"party-reel-focus").pickingMode=PickingMode.Ignore;
            quickStartLayer=Box(root,"quick-start-layer");quickStartLayer.pickingMode=PickingMode.Ignore;
            quickStartTitle=Text(quickStartLayer,"대난투","quick-start-title");quickStartTitle.pickingMode=PickingMode.Ignore;
            quickStartNumber=Text(quickStartLayer,"","quick-start-number");quickStartNumber.pickingMode=PickingMode.Ignore;
            digits = Box(bottom, "qte-digits");
            for (int i = 0; i < digitLabels.Length; i++) digitLabels[i] = Text(digits, "", "qte-digit");
            result = Text(bottom, "", "result");
            weaponChoice = Box(bottom, "weapon-choice");
            Text(weaponChoice, "대난투 무기 선택", "weapon-choice-title");
            var choices = Box(weaponChoice, "button-row");
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                var weapon = WeaponRules.Selectable[i];
                var button = Button(choices, "", () => HelloWorldPlayer.Local?.SelectWeapon(weapon), "weapon-choice-button");
                button.AddToClassList("secondary"); weaponButtons[i] = button;
                BindHoverTip(button,()=>WeaponHoverInfo(weapon));
                var image = new Image { image = weaponIcons[(int)weapon], pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("weapon-choice-icon"); button.Add(image);
                Text(button, WeaponRules.Name(weapon), "weapon-choice-name").pickingMode = PickingMode.Ignore;
            }
            loadoutChoice=Box(weaponChoice,"loadout-choice");
            loadoutTitle=Text(loadoutChoice,"시작 아이템 선택 · 0/2","weapon-choice-title");
            var loadoutRow=Box(loadoutChoice,"button-row");
            for(int i=0;i<loadoutButtons.Length;i++)
            {
                var item=(DuelItem)i;
                var button=Button(loadoutRow,"",()=>HelloWorldPlayer.Local?.SelectLoadoutItem(item),"loadout-button");
                button.AddToClassList("secondary");loadoutButtons[i]=button;
                BindHoverTip(button,()=>ItemHoverInfo(item));
                var icon=new Image{image=itemIcons[i],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};
                icon.AddToClassList("loadout-icon");button.Add(icon);
                Text(button,DuelItemArt.Name(item),"item-name").pickingMode=PickingMode.Ignore;
            }
            rematch = Button(bottom, "다시 대결", () => HelloWorldPlayer.Local?.Rematch(), "primary");
            BuildRelayCodeButton(root);
            roomShotSummary=Text(bottom,"","room-shot-summary");
            roomSettings=Box(root,"room-settings");Show(roomSettings,false);
            Text(roomSettings,"개발자 모드 · F11","room-settings-title");
            developerGameSettings=Box(roomSettings,"developer-game-settings");
            unlimitedShot=new Toggle("사격시간 무제한");developerGameSettings.Add(unlimitedShot);
            shotSeconds=new IntegerField("제한시간 (초)"){value=10,isDelayed=true};developerGameSettings.Add(shotSeconds);
            unlimitedItems=new Toggle("아이템 무한 사용");developerGameSettings.Add(unlimitedItems);unlimitedItems.RegisterValueChangedCallback(e=>manager.SetRoomUnlimitedItems(e.newValue));
            noItemCooldown=new Toggle("아이템 쿨타임 제거");developerGameSettings.Add(noItemCooldown);noItemCooldown.RegisterValueChangedCallback(e=>manager.SetRoomNoItemCooldown(e.newValue));
            autoSelectItems=new Toggle("아이템 모두 선택");developerGameSettings.Add(autoSelectItems);autoSelectItems.RegisterValueChangedCallback(e=>manager.SetRoomAutoSelectItems(e.newValue));
            manualMiniGame=new Toggle("미니게임 직접 선택");developerGameSettings.Add(manualMiniGame);manualMiniGame.RegisterValueChangedCallback(e=>manager.SetRoomManualMiniGame(e.newValue));
            foreach(var toggle in new[]{unlimitedShot,unlimitedItems,noItemCooldown,autoSelectItems,manualMiniGame})DuelPointer.EnableAltClick(toggle);
            Text(developerGameSettings,"장전 완료 후 적용 · 1~60초","room-settings-note");
            developerGoldSettings=Box(roomSettings,"developer-gold-settings");
            developerGoldBalance=Text(developerGoldSettings,"","room-settings-note");
            developerGoldAmount=new IntegerField("골드 추가"){value=100,isDelayed=true};developerGoldSettings.Add(developerGoldAmount);
            Button(developerGoldSettings,"골드 추가",()=>{if(!manager.Busy){AddGachaGold(developerGoldAmount.value);UpdateGachaGold();}},"secondary");
            unlimitedShot.RegisterValueChangedCallback(e=>manager.SetRoomShotTime(e.newValue?0:Mathf.Clamp(shotSeconds.value,1,60)));
            shotSeconds.RegisterValueChangedCallback(e=>{if(!unlimitedShot.value)manager.SetRoomShotTime(Mathf.Clamp(e.newValue,1,60));});
            shotTimer=Text(table,"","shot-time-remaining");shotTimer.pickingMode=PickingMode.Ignore;
            status = Text(root, "", "status");
            leave = Button(root, "자리에서 일어나기", () => manager.Leave(), "exit");
            fullscreen = Button(root, "", ToggleFullscreen, "fullscreen");
            fullscreen.AddToClassList("secondary");
            UpdateFullscreenLabel();
            practiceControls=Box(root,"practice-controls");
            practiceCancel=Button(practiceControls,"준비 취소",()=>{var player=HelloWorldPlayer.Local;if(player==null)return;if(player.Seat.Value==0&&player.NetworkManager.IsHost)player.ToggleHostPractice();else player.Rematch();},"practice-cancel");
            practiceCancel.AddToClassList("secondary");Show(practiceCancel,false);
            hostPractice=Button(bottom,"연습 준비",()=>HelloWorldPlayer.Local?.ToggleHostPractice(),"host-practice");hostPractice.AddToClassList("secondary");bottom.Insert(bottom.IndexOf(rematch),hostPractice);Show(hostPractice,false);
            hostPracticeStart=Button(practiceControls,"게임 시작",()=>HelloWorldPlayer.Local?.Rematch(),"host-practice-start");hostPracticeStart.AddToClassList("primary");Show(hostPracticeStart,false);Show(practiceControls,false);
            localPlayerInfo=Box(root,"local-player-info");localPlayerInfo.pickingMode=PickingMode.Ignore;
            var localWeaponPanel=Button(localPlayerInfo,"",ToggleQuickInventory,"local-weapon-panel");
            BindHoverTip(localWeaponPanel,LocalEquipmentHover);
            localWeaponIcon=new Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};
            localWeaponIcon.AddToClassList("local-weapon-icon");localWeaponPanel.Add(localWeaponIcon);
            var localHealthPanel=Box(localPlayerInfo,"local-health-panel");localHealthPanel.pickingMode=PickingMode.Ignore;
            Text(localHealthPanel,"체력","local-health-title").pickingMode=PickingMode.Ignore;
            localHealth=Text(localHealthPanel,"","local-health");localHealth.pickingMode=PickingMode.Ignore;
            Show(localPlayerInfo,false);BuildSpectatorNotice();
            botControls=Box(root,"bot-controls");
            Text(botControls,"봇 난이도","bot-controls-title");
            var levels=Box(botControls,"bot-controls-row");string[] levelNames={"쉬움","보통","어려움"};
            for(int i=0;i<botLevels.Length;i++){int level=i;botLevels[i]=Button(levels,levelNames[i],()=>manager.LobbyBotDifficulty=(AiDifficulty)level,"bot-level");}
            var botActions=Box(botControls,"bot-controls-row");
            addBot=Button(botActions,"봇 추가",manager.AddLobbyBot,"bot-action");
            removeBot=Button(botActions,"봇 제거",manager.RemoveLobbyBot,"bot-action");
            Show(botControls,false);
            damageOverlay = Box(root, "damage-overlay"); damageOverlay.pickingMode = PickingMode.Ignore;
            hitVignette=MakeAimVignette(false,true);damageOverlay.style.backgroundImage=new StyleBackground(hitVignette);
            reelSound=gameObject.AddComponent<AudioSource>();reelSound.playOnAwake=false;reelSound.volume=.22f;
            reelTickSound=MakeReelSound(false);reelStopSound=MakeReelSound(true);
            crosshair=Box(root,"crosshair");crosshair.pickingMode=PickingMode.Ignore;
            for(int i=0;i<4;i++)
            {
                var line=Box(crosshair,"crosshair-line");line.pickingMode=PickingMode.Ignore;
                bool horizontal=i<2;
                line.style.width=horizontal?4:1;line.style.height=horizontal?1:4;
                line.style.left=horizontal?(i==0?0:11):7;line.style.top=horizontal?7:(i==2?0:11);
            }
            Show(crosshair,false);
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
                seatHealth[i]=Text(nameplateLayer,"","nameplate");
                seatHealth[i].AddToClassList("seat-health");seatHealth[i].pickingMode=PickingMode.Ignore;
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
            BuildBasScope();
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
            for(int i=0;i<DuelLoadout.ItemCount;i++)
            {
                var item=(DuelItem)i;var b=Button(itemRow,"",()=>HelloWorldPlayer.Local?.UseItem(item),"item-choice");
                var icon=new Image{image=itemIcons[i],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};icon.AddToClassList("item-icon");b.Add(icon);
                Text(b,DuelItemArt.Name(item),"item-name").pickingMode=PickingMode.Ignore;
                itemStatuses[i]=Text(b,"","item-status");itemStatuses[i].pickingMode=PickingMode.Ignore;Show(itemStatuses[i],false);
                if(item==DuelItem.Recovery){recoveryUsedMark=Text(b,"X","item-used-mark");recoveryUsedMark.pickingMode=PickingMode.Ignore;Show(recoveryUsedMark,false);}
                itemButtons[i]=b;
            }
            healOverlay=Box(root,"item-effect");electricOverlay=Box(root,"item-effect");healOverlay.pickingMode=electricOverlay.pickingMode=PickingMode.Ignore;
            healTexture=DuelItemArt.Effect(false);electricTexture=DuelItemArt.Effect(true);healOverlay.style.backgroundImage=new StyleBackground(healTexture);electricOverlay.style.backgroundImage=new StyleBackground(electricTexture);
            Show(itemRow,false);Show(healOverlay,false);Show(electricOverlay,false);
            BuildDiceUI();BuildWeaponInfo();
            BuildHoverTip();
            BuildInkOverlay();
            BuildQuitConfirmation();
            BuildPatchNotes();
            BuildGacha();
            BuildSettings();
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
            infoIntro=Text(scroll,"무기는 로비에서 선택합니다. 대난투에서는 숫자 입력으로 준비·장전하고 직접 사격합니다. 다른 미니게임은 1등에게 한 발의 사격 기회를 줍니다. 화면 중앙으로 겨냥하고 좌클릭으로 발사합니다. 윈체스터는 우클릭을 누르는 동안 가늠자로 정조준할 수 있습니다.","weapon-info-intro");
            for(int i=0;i<WeaponRules.Selectable.Length;i++)
            {
                var weapon=WeaponRules.Selectable[i];var card=Box(scroll,"weapon-info-card");weaponInfoCards[i]=card;var header=Box(card,"weapon-info-card-heading");
                var icon=new Image{image=weaponIcons[(int)weapon],scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};
                icon.AddToClassList("weapon-info-icon");header.Add(icon);Text(header,WeaponRules.Name(weapon),"weapon-info-name");
                if(weapon==DuelWeapon.BAS)
                {
                    Text(card,"대난투 · 장전과 사격","weapon-info-section");
                    Text(card,"집기 → 레버 열기 → 한 발 장전 → 레버 닫기. 각 행동마다 숫자 4개를 입력합니다.\n발사하면 정조준이 풀립니다. 레버를 여는 행동으로 탄피를 배출하고, 새 탄을 넣고 레버를 닫아야 다시 조준·사격할 수 있습니다.","weapon-info-text");
                    Text(card,"피해","weapon-info-section");Text(card,"헤드샷은 체력 100 피해와 함께 목숨 2개를 잃게 합니다. 몸통과 다리는 피해가 없습니다.","weapon-info-text");
                    Text(card,"사격 성능","weapon-info-section");Text(card,"우클릭으로 정조준하면 조준한 곳에 정확하게 사격합니다. 지향사격은 탄이 퍼져 명중률이 낮습니다.","weapon-info-text");
                    continue;
                }
                Text(card,"대난투 · 장전과 사격","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    $"처음 준비는 5개 행동마다 숫자 6개를 입력합니다. 이후 한 발마다 숫자 3개로 장전하며 최대 {WeaponRules.Capacity(weapon)}발까지 넣을 수 있습니다.\n한 발만 장전해도 좌클릭으로 사격할 수 있습니다. 남은 탄은 추가 입력 없이 연속 사격합니다.\n재장전은 탄피 빼기(숫자 6개)부터 시작하고 빈 약실마다 숫자 3개로 채웁니다. 남은 실탄은 유지됩니다.":
                    "집기 → 배럴 열기 → 장전 → 배럴 닫기 순서로, 각 행동마다 숫자 4개를 입력합니다.\n화면 중앙으로 겨냥하고 좌클릭으로 사격합니다. 이후에는 배럴 열기부터 반복하며 탄피가 자동으로 빠진 뒤 새 탄을 넣고 닫습니다.","weapon-info-text");
                Text(card,"피해","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    $"머리 {WeaponRules.Damage(weapon,DuelHitPart.Head)} / 몸통 {WeaponRules.Damage(weapon,DuelHitPart.Body)} / 다리 {WeaponRules.Damage(weapon,DuelHitPart.Legs)}":
                    $"부위에 관계없이 직격 {WeaponRules.Damage(weapon,DuelHitPart.Body)} · 맞은 상대의 좌우 좌석에 각각 25 피해\n사격자와 빈자리·사망자는 주변 피해에서 제외됩니다. 1번과 6번 좌석은 이어지지 않습니다.","weapon-info-text");
                Text(card,"사격 성능","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    "화면 중앙으로 정확하게 사격하며, 맞힌 부위에 따라 피해가 달라집니다.":
                    "화면 중앙을 향해 산탄이 퍼지며 최대 20m까지 날아갑니다. 실제 맞은 상대 중 중앙에 가까운 한 명과 그 좌우에 피해를 줍니다. 머리 추가 피해는 없습니다.","weapon-info-text");
                Text(card,"대난투 · 특징","weapon-info-section");
                Text(card,weapon==DuelWeapon.Revolver?
                    "입력할 숫자가 한 자리씩 공개됩니다. 오입력하면 현재 행동의 숫자가 모두 바뀌고 처음부터 다시 입력하지만, 이미 장전한 탄은 유지됩니다.\n적게 장전해 먼저 쏘거나, 여러 발을 준비해 연속 사격할 수 있습니다.":
                    "리볼버보다 준비 행동과 입력 수가 적어 빠르게 사격을 준비할 수 있습니다. 한 발 쏠 때마다 배럴 열기 → 장전 → 배럴 닫기의 3개 행동을 다시 입력해야 합니다.","weapon-info-text");
            }
            for(int i=0;i<DuelLoadout.ItemCount;i++)
            {var card=Box(scroll,"item-info-card");card.AddToClassList("weapon-info-card");itemInfoCards[i]=card;var h=Box(card,"weapon-info-card-heading");var icon=new Image{image=itemInfoIcons[i],scaleMode=ScaleMode.ScaleToFit};icon.AddToClassList("weapon-info-icon");h.Add(icon);Text(h,DuelItemArt.Name((DuelItem)i),"weapon-info-name");
                if(i==(int)DuelItem.NoruFace){Text(card,"테이저 면역 · 2게임 쿨타임","weapon-info-section");Text(card,"아이템 사용 시간에 착용하는 두꺼운 패딩입니다. 사격자를 포함해 누구나 사용할 수 있고, 이번 사격 턴 동안 테이저건의 피해와 감전을 모두 막습니다. 테이저에 맞지 않아도 사격 턴이 끝나면 벗습니다.","weapon-info-text");continue;}
                if(i==(int)DuelItem.Bullet){Text(card,"발사 1회 추가 · 2개 지급 · 소모성","weapon-info-section");Text(card,"사격자는 아이템 사용 시간에 선택할 수 있습니다. 시간이 끝나면 아이템을 사용해 이번 사격 턴에 한 발을 더 쏠 수 있습니다. 선택한 아이템을 다시 누르면 취소됩니다. 발사하지 않아도 사용 시 1개를 소모합니다.","weapon-info-text");continue;}
                if(i==(int)DuelItem.Ink){Text(card,"투척 · 시야 방해 · 소모성","weapon-info-section");Text(card,"선택한 뒤 좌클릭으로 던집니다. 포물선으로 날아가 맞은 상대의 화면 대부분을 최대 5초 가립니다.","weapon-info-text");continue;}
                Text(card,i==0?"체력 50 회복 · 최대 체력 100 · 소모성":"피해 5 · 양옆 자리까지 · 1.3초 감전 · 2게임 쿨타임","weapon-info-section");
                Text(card,i==0?"아이템 사용 시간에 클릭하면 초록색 회복 효과와 함께 즉시 회복하며 한 번 사용하면 소비됩니다. 체력이 가득 차면 사용할 수 없습니다.":"좌클릭으로 화면 중앙을 향해 쏩니다. 맞은 상대는 최대 1.3초 동안 시점 조작이 막힙니다. 사격이 끝나면 감전도 해제됩니다.","weapon-info-text");}
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
        {infoTitle.text=items?"아이템 정보":"무기 정보";infoIntro.text=items?"로비에서 서로 다른 아이템 2개를 골라 파티에 가져갑니다. 대난투에서는 시작 전 하나를 선택하고, 마우스 휠을 돌리거나 Alt를 누른 채 오른쪽 아래 무기 이미지를 클릭해 무장을 전환합니다. 선택한 장비 아이템은 사용하지 않아도 미니게임 종료 시 소모·쿨타임이 적용됩니다.":"무기는 로비에서 선택합니다. 대난투에서는 숫자 입력으로 준비·장전하고 직접 사격합니다. 다른 미니게임은 1등에게 한 발의 사격 기회를 줍니다. 화면 중앙으로 겨냥하고 좌클릭으로 발사합니다. 윈체스터는 우클릭을 누르는 동안 가늠자로 정조준할 수 있습니다.";foreach(var card in weaponInfoCards)Show(card,!items);foreach(var card in itemInfoCards)Show(card,items);foreach(var card in miniGameInfoCards)Show(card,false);infoScroll.scrollOffset=Vector2.zero;}
        private void SetMiniGameInfo()
        {
            infoTitle.text="미니게임 정보";
            infoIntro.text="체력 100·목숨 3개로 시작하며, 목숨이 남은 마지막 한 명이 최종 승리합니다. 한 사이클에서 진행한 미니게임을 제외하고 추첨하며, 모두 진행하면 다시 추첨합니다. 대난투는 모두가 싸우고, 나머지는 1등이 아이템 사용 시간 후 한 발을 사격합니다. 1등을 가릴 수 없는 무승부는 주사위 겨루기로 결정합니다.";
            foreach(var card in weaponInfoCards)Show(card,false);foreach(var card in itemInfoCards)Show(card,false);
            foreach(var card in miniGameInfoCards)Show(card,true);
            infoScroll.scrollOffset=Vector2.zero;
        }
        private static string MiniGameInfoText(PartyMiniGame game)
        {
            switch(game)
            {
                case PartyMiniGame.Countdown:return "제시된 7~15초에 가장 가깝게 스페이스로 멈추세요. 기록은 소수점 두 자리로 비교합니다.\n타이머는 3초가 되기 전에 가려집니다. 목표 시간 +2초까지 멈추지 못하면 목숨을 하나 잃습니다. 전원이 시간 초과면 사격 없이 다음 미니게임으로 넘어갑니다.";
                case PartyMiniGame.QuickHands:return "아이템 사용 5초와 시작 카운트다운 3초 후 모두 동시에 싸웁니다. 숫자 1~5로 선택한 총을 준비·장전하고 마우스로 직접 겨냥해 좌클릭으로 사격하세요. 준비 방식은 총기마다 다릅니다.\n현재 생존자가 한 명 남으면 종료됩니다. 사망한 참가자는 목숨을 잃으며, 목숨이 남아 있다면 체력을 회복하고 다음 미니게임에 참가합니다.";
                case PartyMiniGame.Signal:return "가짜 신호에 속지 말고 진짜 초록 신호에서 스페이스로 반응하세요. 가장 빠른 사람이 1등입니다.\n성급하게 입력하거나 초록 신호 후 2초 안에 반응하지 못하면 실패합니다. 실패만으로 목숨을 잃지는 않습니다.";
                case PartyMiniGame.Greed:return "점점 빠르게 오르는 점수를 스페이스로 확정하세요. 터지기 전에 확보한 점수가 가장 높은 사람이 1등입니다.\n최대 30초 안에 폭발하며, 숨은 기준값에 닿기 전에도 터질 수 있습니다. 폭발할 때까지 확정하지 못하면 0점이지만 목숨을 잃지는 않습니다.\n최고 기록이 무승부이거나 전원이 폭발하면 주사위 겨루기로 사격자 한 명을 정합니다.";
                case PartyMiniGame.Memory:return "3초 동안 방향 순서를 기억한 뒤, 10초 안에 방향키로 입력하세요. 처음에는 3개이며 라운드마다 하나씩 늘어납니다. 개수만큼 입력하면 자동 확정됩니다.\n모두 맞히면 다음 라운드로 진행합니다. 서로 다른 기록이면 가장 많이 틀린 사람들은 이번 미니게임에서 탈락하며 목숨은 잃지 않습니다. 2명만 남아도 라운드와 입력 개수를 그대로 이어갑니다.\n남아 있는 전원이 같은 개수로 틀렸다면 주사위로 1등만 정합니다. 높은 눈이 한 명이면 즉시 결정하고, 가장 높은 눈이 같으면 그 사람들만 다시 굴립니다. 하위 순위는 따로 가리지 않습니다.";
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
        private Button Button(VisualElement parent, string text, System.Action action, string css)
        { var b = new Button(()=>{feedback?.UiClick();action();}) { text = text }; DuelPointer.EnableAltClick(b); b.AddToClassList(css); parent.Add(b); return b; }
        private Vector2Int windowedSize;
        private void ToggleFullscreen()
        {
            var display=Screen.mainWindowDisplayInfo;
            if(Screen.fullScreen)
            {
                var size=windowedSize.x>0&&windowedSize.y>0?windowedSize:
                    new Vector2Int(Mathf.Max(1,Mathf.RoundToInt(display.width*.75f)),Mathf.Max(1,Mathf.RoundToInt(display.height*.75f)));
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
            }
            else
            {
                windowedSize=new Vector2Int(Screen.width,Screen.height);
                // Changing only the window mode retains the old render resolution/aspect.
                Screen.SetResolution(display.width,display.height,FullScreenMode.FullScreenWindow);
            }
        }
        private void UpdateFullscreenLabel()
        {
            string text = Screen.fullScreen ? "전체화면 끄기" : "전체화면 켜기";
            if (fullscreen.text != text) fullscreen.text = text;
        }
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
            if (enabled) { resultsFooter.Add(weaponChoice); resultsFooter.Add(hostPractice); resultsFooter.Add(rematch); }
            else { inputPanel.Add(weaponChoice); inputPanel.Add(hostPractice); inputPanel.Add(rematch); }
        }
        private bool CanAct()
        {
            if(DuelChat.BlocksGameplay)return false;
            var p = HelloWorldPlayer.Local;
            if(p==null||!p.IsSpawned||p.Seat.Value<0)return false;
            var s=p.State.Value;int seat=p.Seat.Value;
            bool lever=s.IsPracticing(seat)&&s.ForSeat(seat).Weapon==DuelWeapon.BAS&&s.ForSeat(seat).Action==DuelAction.OpenBarrel;
            return !s.ForSeat(seat).QuickEquipped&&!DuelInk.Busy(s.ForSeat(seat).Ink,p.NetworkManager.ServerTime.Time) &&
                (lever||s.Stage==DuelStage.Racing&&s.Clock.Phase==ClockPhase.QuickHands&&s.Phase==DuelPhase.Playing&&s.Alive(seat)) &&
                !(GetComponent<DuelFirstPerson>().Ads&&predicted.Weapon==DuelWeapon.Revolver) &&
                !waitingForAction && predicted.Progress < predicted.Length &&
                p.NetworkManager.ServerTime.Time >= predicted.BlockedUntil && predicted.Action != DuelAction.Aim && predicted.Action != DuelAction.Fire;
        }
        private void Press(int digit)
        {
            if (!CanAct()) return;
            var p = HelloWorldPlayer.Local; int version = predicted.Version;
            // Predict only the visible prefix. The server alone advances actions and awards damage.
            predicted.Version++;
            bool mistake = digit != predicted.Digit(predicted.Progress);
            feedback?.KeyInput(mistake);
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
        private static string WeaponActionName(DuelWeapon weapon,DuelAction action)=>weapon==DuelWeapon.BAS?
            (action==DuelAction.OpenBarrel?"레버 조작 · 탄피 배출":action==DuelAction.Load?"한 발 장전":action==DuelAction.CloseBarrel?"레버 닫기":ActionName(action)):weapon!=DuelWeapon.Revolver?ActionName(action):
            action==DuelAction.UnlockBarrel?"장전 게이트 잠금 해제":action==DuelAction.OpenBarrel?"장전 게이트 열기":
            action==DuelAction.CloseBarrel?"탄피 빼기":ActionName(action);
        private void Update()
        {
            if (manager == null || root == null) return;
            UpdateSettings();
            UpdateGacha();
            Refresh();
            UpdateSettingsLayer();
            UpdateAttendance();
            if(DuelChat.BlocksGameplay||DuelPointer.BlocksActions)return;
            if(Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame){if(StopOwnDice())return;StopClock();}
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
            UpdateFullscreenLabel();
            UpdateMenuQuit();
            UpdatePatchNotes();
            var p = HelloWorldPlayer.Local; bool ready = manager.Busy && p != null && p.IsSpawned && p.Seat.Value >= 0;
            bool practice=ready&&p.State.Value.IsPracticing(p.Seat.Value);Show(practiceControls,practice);Show(practiceCancel,practice);
            bool botLobby=ready&&p.NetworkManager.IsHost&&p.State.Value.Stage==DuelStage.Idle&&p.State.Value.Phase!=DuelPhase.Playing;
            bool hostLobby=botLobby&&p.Seat.Value==0;
            Show(hostPractice,hostLobby&&!practice);hostPractice.SetEnabled(hostLobby&&!practice&&DuelLoadout.Complete(p.State.Value.ForSeat(0).SelectedItems));
            hostPractice.text=hostLobby&&!DuelLoadout.Complete(p.State.Value.ForSeat(0).SelectedItems)?"아이템 2개 선택":"연습 준비";
            Show(hostPracticeStart,hostLobby&&practice);hostPracticeStart.SetEnabled(hostLobby&&practice&&manager.CanHostStart);
            Show(botControls,botLobby);addBot.SetEnabled(botLobby&&manager.CanAddBot);removeBot.SetEnabled(botLobby&&manager.CanRemoveBot);
            for(int i=0;i<botLevels.Length;i++){botLevels[i].SetEnabled(botLobby&&manager.CanAddBot);botLevels[i].EnableInClassList("selected",(int)manager.LobbyBotDifficulty==i);}
            addBot.text=ready&&RouletteRules.Count(p.State.Value.ConnectedMask)>=RouletteRules.MaxPlayers?"정원 6명":"봇 추가";
            Show(menu, !manager.Busy); Show(table, ready); Show(leave, manager.Busy); Show(status, !ready); status.text = manager.Status;
            if(manager.Busy&&weaponInfoOpen)SetWeaponInfo(false);
            RefreshConnectionMenu();
            Show(nameplateLayer, ready);
            if(!DuelChat.BlocksGameplay&&Keyboard.current!=null&&Keyboard.current.f11Key.wasPressedThisFrame)
            {if(ready&&p.NetworkManager.IsHost)manager.ToggleDeveloperMode();else if(!manager.Busy)menuDeveloperMode=!menuDeveloperMode;}
            if(manager.Busy)menuDeveloperMode=false;
            bool settingsVisible=ready&&p.NetworkManager.IsHost&&manager.DeveloperMode||!manager.Busy&&menuDeveloperMode;
            Show(roomSettings,settingsVisible);Show(shotTimer,false);Show(roomShotSummary,false);
            developerGameSettings.SetEnabled(ready&&p.NetworkManager.IsHost&&p.State.Value.Stage==DuelStage.Idle&&p.State.Value.Phase!=DuelPhase.Playing);
            developerGoldSettings.SetEnabled(!manager.Busy);
            if(settingsVisible){roomSettings.BringToFront();developerGoldBalance.text="보유 골드: "+GachaGold.ToString("N0")+"원";}
            if(settingsVisible&&ready)
            {
                roomSettings.BringToFront();
                float limit=p.State.Value.ShotTimeLimit;unlimitedShot.SetValueWithoutNotify(limit==0);
                manualMiniGame.SetValueWithoutNotify(p.State.Value.ManualMiniGame);
                unlimitedItems.SetValueWithoutNotify(p.State.Value.UnlimitedItems);
                noItemCooldown.SetValueWithoutNotify(p.State.Value.NoItemCooldown);
                autoSelectItems.SetValueWithoutNotify(p.State.Value.AutoSelectItems);
                if(limit>0)shotSeconds.SetValueWithoutNotify(Mathf.RoundToInt(limit));shotSeconds.SetEnabled(limit>0);
            }
            UpdateClock(ready?p:null);
            UpdateInkOverlay(ready?p:null);
            UpdateHitMarker(ready?p:null);
            UpdateSpectatorNotice(ready?p:null);
            var own=ready?p.State.Value.ForSeat(p.Seat.Value):default;
            int effectSeat=ready&&spectator!=null&&spectator.Watching?spectator.TargetSeat:ready?p.Seat.Value:-1;
            var displayed=ready?p.State.Value.ForSeat(effectSeat):default;double effectNow=ready?p.NetworkManager.ServerTime.Time:0;
            float hitAge=ready&&displayed.HitAt>=0?(float)(effectNow-displayed.HitAt):1;
            bool hitVisible=ready&&hitAge>=0&&hitAge<.7f;
            Show(damageOverlay,hitVisible);
            damageOverlay.style.opacity=hitVisible?Mathf.SmoothStep(0,1,Mathf.Clamp01(hitAge/.04f))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.7f,hitAge)))*.82f:0;
            float hitScale=Mathf.Lerp(1.16f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(hitAge/.35f)));
            damageOverlay.style.scale=new Scale(new Vector3(hitScale,hitScale,1));
            Show(localPlayerInfo,ready);
            if(ready)
            {
                localHealth.text=displayed.Health.ToString();
                bool preview=p.State.Value.CanPreviewEquipment(effectSeat);
                localWeaponIcon.image=displayed.Ink.Armed||preview&&displayed.PreviewItem==(int)DuelWheelSlot.Ink?itemIcons[2]:displayed.QuickEquipped&&displayed.QuickItem==2||displayed.TaserArmed&&displayed.Weapon==DuelWeapon.Taser||preview&&displayed.PreviewItem==(int)DuelWheelSlot.Taser?itemIcons[1]:weaponIcons[(int)displayed.SelectedWeapon];
            }
            bool healed=ready&&displayed.HealAt>=0&&effectNow-displayed.HealAt<1.1&&(p.State.Value.IsPracticing(effectSeat)||p.State.Value.Clock.Phase==ClockPhase.ItemPreparation||p.State.Value.Clock.Phase==ClockPhase.Shooting||p.State.Value.Clock.Phase==ClockPhase.QuickCountdown);
            if(healed)healOverlay.style.backgroundImage=new StyleBackground(ItemEffectTexture(false,DuelSkinEffects.Skin(effectSeat,DuelItem.Recovery)));
            Show(healOverlay,healed);healOverlay.style.opacity=healed?Mathf.Clamp01((float)(1.1-(effectNow-displayed.HealAt)))*(.8f+.2f*Mathf.Sin(Time.unscaledTime*9)):0;
            if(ready&&displayed.IsStunned(effectNow))electricOverlay.style.backgroundImage=new StyleBackground(ItemEffectTexture(true,DuelSkinEffects.SourceSkin(p.State.Value,effectSeat,DuelItem.Taser)));
            Show(electricOverlay,ready&&displayed.IsStunned(effectNow)&&(p.State.Value.IsPracticing(effectSeat)||p.State.Value.Phase==DuelPhase.Playing&&DuelInk.Shooting(p.State.Value.Clock.Phase)));electricOverlay.style.opacity=.7f+.3f*Mathf.Sin(Time.unscaledTime*35);
            Show(aimWarning, ready && IsUnderAim(p.State.Value, effectSeat));
            var fps = GetComponent<DuelFirstPerson>();
            bool ads = ready && (spectator!=null&&spectator.Watching?displayed.Aiming:fps!=null&&fps.Active&&fps.Ads) && displayed.Weapon!=DuelWeapon.Taser;
            bool firstPersonView=ready&&(practice||p.State.Value.Phase==DuelPhase.Waiting||
                p.State.Value.Phase==DuelPhase.Playing&&p.State.Value.Alive(p.Seat.Value)&&
                (p.State.Value.Stage==DuelStage.Countdown||p.State.Value.Stage==DuelStage.Racing));
            bool combatShooting=ready&&p.State.Value.Phase==DuelPhase.Playing&&DuelInk.Shooting(p.State.Value.Clock.Phase);
            bool showCrosshair=firstPersonView&&own.DiedAt<0&&own.LivesRemaining>0&&!combatShooting&&!(ads&&own.Weapon==DuelWeapon.BAS);
            Show(crosshair,showCrosshair);
            if(showCrosshair)crosshair.BringToFront();
            aimWarning.style.opacity = ads ? .05f : 1;
            Show(scopeShade, ads&&displayed.Weapon!=DuelWeapon.BAS);
            UpdateBasScope(false);
            Show(killFeed, ready);
            UpdateDeathDetails(ready ? p : null);
            if (!ready) { announcedTimeoutRound=-1; hadPlayer = false; predictionRound = -1; previousShots = 0; killRound = -1; lastKillShot = 0; ClearKills(); victoryRound = announcedVictoryRound = -1; victoryExpiresAt = 0; Show(victoryLayer, false); Show(highlightLayer, false); Show(resultsLayer, false); SetResultLayout(false); renderedResults = default; UpdateInputShake(false); return; }
            var s = p.State.Value; int seat = p.Seat.Value; var mine = s.ForSeat(seat); double now = p.NetworkManager.ServerTime.Time;
            UpdateVictory(s);
            // Keep the world labels through minigames, item turns, results and temporary deaths.
            nameplateLayer.SendToBack();
            if (!hadPlayer || predictionRound != s.Round || !practice&&s.Stage != DuelStage.Racing || mine.Version >= predicted.Version ||
                Time.realtimeSinceStartupAsDouble - sentAt > 1.5)
            { predicted = mine; waitingForAction = false; predictionRound = s.Round; }
            if (killRound != s.Round) { ClearKills(); killRound = s.Round; lastKillShot = 0; }
            int fired=0;for(int i=0;i<RouletteRules.MaxPlayers;i++)fired+=s.ForSeat(i).ShotsFired;
            UpdateKills();
            previousShots = fired; hadPlayer = true;
            bool equipmentBusy=mine.QuickEquipped||DuelInk.Busy(mine.Ink,now);
            bool practiceLever=practice&&!equipmentBusy&&mine.Weapon==DuelWeapon.BAS&&mine.Action==DuelAction.OpenBarrel;
            bool actionVisible = !equipmentBusy&&(practiceLever || s.Clock.Phase==ClockPhase.QuickHands && s.Phase == DuelPhase.Playing && s.Alive(seat) && s.Stage == DuelStage.Racing &&
                predicted.Action != DuelAction.Aim && predicted.Action != DuelAction.Fire && !(ads&&mine.Weapon==DuelWeapon.Revolver));
            bool lobby = s.Stage == DuelStage.Idle && s.Phase != DuelPhase.Playing;
            Show(roomShotSummary,false);
            float personalShotLimit=s.ShotLimitFor(seat);
            bool shotCountdown=s.Clock.Phase==ClockPhase.QuickHands&&s.Alive(seat)&&mine.ShootingStartedAt>=0&&personalShotLimit>0;
            Show(shotTimer,shotCountdown);
            if(shotCountdown)shotTimer.text=$"사격 · {System.Math.Max(0,personalShotLimit-(now-mine.ShootingStartedAt)):F1}초";
            bool hasResults = !practice&&s.Results.IsValid && s.Results.Round == s.Round;
            Show(weaponChoice, !practice&&(lobby || hasResults) && s.Connected(seat));
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                weaponButtons[i].EnableInClassList("selected", mine.SelectedWeapon == WeaponRules.Selectable[i]);
                weaponButtons[i].SetEnabled(lobby && !RouletteRules.Has(s.ReadyMask, seat));
            }
            Show(loadoutChoice,lobby&&!mine.LoadoutLocked);
            loadoutTitle.text=s.AutoSelectFor(seat)?$"시작 아이템 · 전체 {DuelLoadout.Count(mine.SelectedItems)}종 선택":$"시작 아이템 선택 · {DuelLoadout.Count(mine.SelectedItems)}/{DuelLoadout.Required}";
            for(int i=0;i<loadoutButtons.Length;i++)
            {
                loadoutButtons[i].EnableInClassList("selected",DuelLoadout.Has(mine.SelectedItems,(DuelItem)i));
                loadoutButtons[i].SetEnabled(lobby&&!s.AutoSelectFor(seat)&&!mine.LoadoutLocked&&!RouletteRules.Has(s.ReadyMask,seat));
            }
            Show(inputPanel, practiceLever||!practice&&!hasResults && (actionVisible || lobby || s.Stage == DuelStage.Countdown));
            UpdateInputShake(actionVisible);
            Show(turn, practiceLever||s.Phase != DuelPhase.Finished);
            bool highlightShooter=s.Phase==DuelPhase.Playing&&s.Stage==DuelStage.Racing&&s.Clock.MiniGame!=PartyMiniGame.QuickHands&&
                (s.Clock.Phase==ClockPhase.Ranking||s.Clock.Phase==ClockPhase.ItemPreparation||s.Clock.Phase==ClockPhase.Shooting);
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                nameplates[i].EnableInClassList("nameplate-shooter",highlightShooter&&s.Clock.First==i&&s.Alive(i));
                string detail = !s.Connected(i) ? "빈 자리" : lobby ? (i==0?(RouletteRules.Has(s.ReadyMask,0)?"방장 · 연습 중":"방장"):RouletteRules.Has(s.ReadyMask, i) ? "준비 완료" : "준비 대기") : s.Alive(i) ? "생존" : s.InParty(i)?"미니게임 탈락":"탈락";
                Vector3 anchor;
                bool visibleName = s.Connected(i) ? manager.Table.TryGetNameViewport(i, out anchor) :
                    manager.Table.TryGetChairViewport(i, out anchor);
                UpdateSeatDice(s,i,visibleName,anchor,now,seat);
                visibleName &= !s.Connected(i)||s.ForSeat(i).LivesRemaining>0;
                Show(nameplates[i], visibleName);
                Show(seatStatuses[i], visibleName && s.Connected(i));
                lifeIcons[i].text=new string('♥',System.Math.Clamp(s.ForSeat(i).LivesRemaining,0,RouletteRules.StartingLives));
                Show(lifeIcons[i],visibleName&&s.Connected(i)&&s.ForSeat(i).LivesRemaining>0);
                Show(seatHealth[i],visibleName&&s.Connected(i));
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
                    seatHealth[i].text=s.ForSeat(i).Health.ToString();
                    seatHealth[i].style.left=anchor.x*root.resolvedStyle.width;
                    seatHealth[i].style.top=(1-anchor.y)*root.resolvedStyle.height-67-NameplateHealthHeight;
                }
            }
            Show(digits, actionVisible);
            Show(rematch, !practice&&(hasResults || lobby) && s.Connected(seat));
            bool host=seat==0&&p.NetworkManager.IsHost;
            int others=s.ConnectedMask&~1;
            bool othersReady=RouletteRules.Count(s.ConnectedMask)>=2&&(s.ReadyMask&others)==others;
            rematch.SetEnabled(lobby && (host?othersReady&&manager.CanHostStart&&DuelLoadout.Complete(mine.SelectedItems):RouletteRules.Has(s.ReadyMask,seat)||DuelLoadout.Complete(mine.SelectedItems)));
            rematch.text=host?(DuelLoadout.Complete(mine.SelectedItems)?"게임 시작":"아이템 2개 선택"):
                RouletteRules.Has(s.ReadyMask, seat) ? "준비 취소" : DuelLoadout.Complete(mine.SelectedItems)?"준비 완료":"아이템 2개 선택";
            Show(result, !actionVisible);
            if (s.Stage == DuelStage.Countdown)
            { turn.text = $"시작까지  {Mathf.Max(1, Mathf.CeilToInt((float)(DuelDirector.CountdownDuration - now + s.StageStartedAt)))}"; result.text = "게임 시작"; }
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
                turn.text = "파티 모드 · " + (participants >= 2 ? "참가자 준비 후 방장이 시작" : "참가자를 기다리는 중");
                result.text = $"준비 {RouletteRules.Count(s.ReadyMask&others)} / {RouletteRules.Count(others)} · " +
                    (participants >= 2 ? "참가자 전원 준비 시 시작 가능" : "2명 이상부터 시작 가능");
                if(RouletteRules.Has(s.ReadyMask,seat)&&!DuelLoadout.Complete(mine.SelectedItems))result.text="준비를 취소하고 아이템 2개를 선택해 주세요.";
                else if(host&&othersReady&&!manager.CanHostStart)result.text=DuelLoadout.Complete(mine.SelectedItems)?"참가자의 아이템 선택 또는 입장을 확인해 주세요.":"아이템 2개를 선택해 주세요.";
            }
            else { turn.text = ""; result.text = ""; }
        }
        private static string MemoryArrow(int direction)=>direction==0?"↑":direction==1?"↓":direction==2?"←":direction==3?"→":"•";
        private bool IsMemoryTurn()
        {
            if(DuelChat.BlocksGameplay)return false;
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
            feedback?.KeyInput();
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
                clockTitle.text=finalist?$"{PartyGames.Name(PartyMiniGame.Memory)} · 최종 결과":$"{PartyGames.Name(PartyMiniGame.Memory)} · {memory.Round}라운드 결과";
                int eliminated=-1;for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(memory.EliminatedMask,i)){eliminated=i;break;}
                int eliminatedCount=RouletteRules.Count(memory.EliminatedMask);
                clockTimer.text=finalist?(memory.Winner>=0?$"{s.NameForSeat(memory.Winner)} 1등":"승자 없음 · 다음 게임으로"):eliminatedCount>1?$"{eliminatedCount}명 탈락":eliminated>=0?$"{s.NameForSeat(eliminated)} 탈락":"전원 정답";
                miniInstruction.text=finalist?(memory.Winner>=0?"1등에게 사격 기회가 주어집니다.":"다음 미니게임으로 진행합니다."):eliminated>=0?"남은 참가자는 다음 라운드로 진행합니다.":"다음 라운드는 방향이 하나 늘어납니다.";
                var order=new System.Collections.Generic.List<int>();
                int mask=memory.RoundMask;
                for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(mask,i))order.Add(i);
                order.Sort((a,b)=>finalist?
                    (memory.ForSeat(a).Rank==0?int.MaxValue:memory.ForSeat(a).Rank).CompareTo(memory.ForSeat(b).Rank==0?int.MaxValue:memory.ForSeat(b).Rank):
                    memory.ForSeat(a).Errors.CompareTo(memory.ForSeat(b).Errors));
                for(int row=0;row<clockRankingEntries.Length;row++)
                {
                    var label=clockRankingEntries[row];Show(label,row<order.Count);if(row>=order.Count)continue;
                    int seat=order[row];var entry=memory.ForSeat(seat);bool own=seat==p.Seat.Value;
                    label.EnableInClassList("clock-ranking-own",own);
                    label.text=(finalist?(seat==memory.Winner?"1등  ":"탈락  "):"")+(own?"나 · ":"")+s.NameForSeat(seat)+$"  오입력 {entry.Errors}개"+
                        (entry.Rolls>0?$" · 주사위 {entry.Dice}"+(entry.Rolls>1?$" ({entry.Rolls}회)":""):"")+
                        (!s.Connected(seat)?" · 연결 종료":!finalist?(RouletteRules.Has(memory.EliminatedMask,seat)?" · 탈락":" · 통과"):"");
                }
            }
        }
        public void ReportClockStop(int round,int iteration,bool accepted,int record=0,MiniFailure failure=MiniFailure.None)
        {if(round==clockRound&&iteration==clockIteration){clockStopped=accepted;clockAwaiting=false;clockRecord=record;clockFailure=failure;
            var p=HelloWorldPlayer.Local;if(p!=null&&p.IsSpawned)feedback?.ReportStop(round,iteration,accepted,p.State.Value.Clock.MiniGame,failure);}}
        private void StopClock()
        {
            if(DuelChat.BlocksGameplay)return;
            var p=HelloWorldPlayer.Local;
            if(p==null || !p.IsSpawned || p.Seat.Value<0)return;
            var s=p.State.Value;double now=p.NetworkManager.ServerTime.Time;
            if(s.Phase!=DuelPhase.Playing || !s.Alive(p.Seat.Value) || s.Clock.MiniGame==PartyMiniGame.QuickHands || s.Clock.MiniGame==PartyMiniGame.Memory || s.Clock.Phase!=ClockPhase.Counting || now<s.Clock.StartedAt || clockStopped)return;
            if(s.Clock.MiniGame==PartyMiniGame.Countdown&&now>s.Clock.StartedAt+s.Clock.Target/100.0+2 || s.Clock.Signal==MiniSignal.Burst || s.Clock.MiniGame==PartyMiniGame.Signal&&s.Clock.Signal==MiniSignal.Go&&now>s.Clock.SignalAt+2)return;
            clockStopped=clockAwaiting=true;feedback?.StopPending();p.StopClock(now);
        }
        private void UpdateMiniGameName(HelloWorldPlayer p)
        {
            bool visible=false;
            if(p!=null&&p.IsSpawned)
            {
                var s=p.State.Value;var c=s.Clock;
                visible=s.Phase==DuelPhase.Playing&&s.Stage==DuelStage.Racing&&
                    (c.Phase==ClockPhase.Counting||c.Phase==ClockPhase.Memory||c.Phase==ClockPhase.QuickHands||c.Phase==ClockPhase.QuickCountdown||
                    c.Phase==ClockPhase.ItemPreparation&&c.MiniGame==PartyMiniGame.QuickHands||
                    c.Phase==ClockPhase.Selecting&&!s.ManualMiniGame&&p.NetworkManager.ServerTime.Time-c.PhaseAt>=PartyGames.SpinDuration);
                if(visible)miniGameName.text=PartyGames.Name(c.MiniGame);
            }
            Show(miniGameName,visible);
        }
        private void UpdateClock(HelloWorldPlayer p)
        {
            UpdateMiniGameName(p);
            UpdateShooterAnnouncement(p);
            if(p==null)feedback?.Reset();else feedback?.Observe(p.State.Value,p.Seat.Value,p.NetworkManager.ServerTime.Time);
            if(UpdateDice(p))
            {Show(clockPanel,false);Show(quickStartLayer,false);Show(manualSelection,false);Show(itemRow,false);Show(partySelection,false);Show(shootingNotice,false);return;}
            if(p!=null&&p.State.Value.IsPracticing(p.Seat.Value))
            {
                Show(clockPanel,false);Show(quickStartLayer,false);Show(manualSelection,false);Show(partySelection,false);Show(shootingNotice,false);
                UpdatePracticeItems(p);return;
            }
            itemRow.EnableInClassList("practice-items",false);
            Show(memoryScroll,false);clockPanel.EnableInClassList("memory-panel",false);
            bool visible=p!=null && p.State.Value.Phase==DuelPhase.Playing && p.State.Value.Stage==DuelStage.Racing && p.State.Value.Clock.Phase!=ClockPhase.QuickHands;
            bool manual=visible&&p.State.Value.ManualMiniGame&&p.State.Value.Clock.Phase==ClockPhase.Selecting;
            Show(manualSelection,manual&&p.NetworkManager.IsHost);
            if(manual&&p.NetworkManager.IsHost&&(manualRound!=p.State.Value.Round||manualIteration!=p.State.Value.Clock.Iteration))
            {manualRound=p.State.Value.Round;manualIteration=p.State.Value.Clock.Iteration;BuildManualChoices();}
            bool itemTime=visible&&p.State.Value.Clock.Phase==ClockPhase.ItemPreparation&&p.State.Value.Alive(p.Seat.Value);
            bool liveInk=LiveInkTime(p);
            bool liveBullet=liveInk&&p.State.Value.Clock.First==p.Seat.Value&&p.State.Value.Clock.MiniGame!=PartyMiniGame.QuickHands;
            var inventory=visible||liveInk?p.State.Value.ForSeat(p.Seat.Value):default;
            bool recoverySpent=inventory.LoadoutLocked&&DuelLoadout.Has(inventory.SelectedItems,DuelItem.Recovery)&&!inventory.HasRecovery;
            bool recoveryUsed=recoverySpent||itemTime&&inventory.HealAt>=0&&inventory.HealAt>=p.State.Value.Clock.PhaseAt;
            Show(itemRow,itemTime&&(inventory.HasRecovery||inventory.HasTaser||recoverySpent||inventory.Ink.Owned||inventory.BulletItems>0||inventory.HasNoruFace)||liveInk&&inventory.Ink.Owned||liveBullet&&(inventory.BulletItems>0||inventory.BulletUsed));
            if(visible||liveInk)
            {
                var a=inventory;var c0=p.State.Value.Clock;
                Show(itemButtons[0],itemTime&&(a.HasRecovery||recoverySpent));Show(itemButtons[1],itemTime&&a.HasTaser);
                Show(itemButtons[3],(itemTime||liveBullet)&&(a.BulletItems>0||a.BulletUsed));
                itemButtons[3].SetEnabled((itemTime||liveBullet&&a.FiredAt<c0.PhaseAt)&&c0.MiniGame!=PartyMiniGame.QuickHands&&c0.First==p.Seat.Value&&a.BulletItems>0&&!a.BulletUsed&&!a.Stunned);
                itemButtons[3].EnableInClassList("item-used",a.BulletUsed);
                itemStatuses[3].text=$"{a.BulletItems}개";Show(itemStatuses[3],(itemTime||liveBullet)&&(a.BulletItems>0||a.BulletUsed));
                UpdateInkItem(p,itemTime,liveInk);
                bool quickPrep=c0.MiniGame==PartyMiniGame.QuickHands;
                Show(itemButtons[4],itemTime&&a.HasNoruFace);
                int noruCooldown=p.State.Value.NoCooldownFor(p.Seat.Value)?0:System.Math.Max(0,a.NoruFaceReadyIteration-c0.Iteration-(a.NoruFaceActive?1:0));
                itemButtons[4].SetEnabled(itemTime&&a.HasNoruFace&&!a.NoruFaceActive&&!a.Stunned&&noruCooldown==0&&(!quickPrep||a.QuickItem==0&&!a.QuickSpent));
                itemButtons[4].EnableInClassList("item-used",a.NoruFaceActive);
                itemButtons[4].EnableInClassList("item-cooldown",!a.NoruFaceActive&&noruCooldown>0);
                itemStatuses[4].text=!a.NoruFaceActive&&noruCooldown>0?$"{noruCooldown}게임 뒤 사용가능":"";
                Show(itemStatuses[4],itemTime&&!a.NoruFaceActive&&noruCooldown>0);
                itemButtons[0].SetEnabled(a.HasRecovery&&a.Health<100&&(!quickPrep||a.QuickItem==0));bool eligible=quickPrep||c0.First>=0&&c0.First!=p.Seat.Value;
                itemButtons[1].SetEnabled(a.HasTaser&&!a.TaserArmed&&(p.State.Value.NoCooldownFor(p.Seat.Value)||c0.Iteration>=a.TaserReadyIteration)&&eligible&&(!quickPrep||!a.QuickSpent));
                itemButtons[1].EnableInClassList("item-used",a.TaserArmed||quickPrep&&a.QuickItem==2);
                itemButtons[0].EnableInClassList("item-spent",recoveryUsed);Show(recoveryUsedMark,recoveryUsed);
                itemStatuses[0].text=recoveryUsed?"사용완료":"";Show(itemStatuses[0],recoveryUsed);
                int cooldown=p.State.Value.NoCooldownFor(p.Seat.Value)?0:System.Math.Max(0,a.TaserReadyIteration-c0.Iteration-(a.TaserArmed?1:0));
                itemButtons[1].EnableInClassList("item-cooldown",!a.TaserArmed&&cooldown>0);
                itemStatuses[1].text=!a.TaserArmed&&cooldown>0?$"{cooldown}게임 뒤 사용가능":"";
                Show(itemStatuses[1],!a.TaserArmed&&cooldown>0);
                if(itemTime)
                    for(int i=0;i<itemButtons.Length;i++)
                    {
                        bool chosen=a.PreparedItem==i+1;
                        if(a.PreparedItem!=0)itemButtons[i].SetEnabled(chosen);
                        itemButtons[i].EnableInClassList("item-used",chosen);
                    }
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
            if(clockRound!=s.Round || clockIteration!=c.Iteration){clockRound=s.Round;clockIteration=c.Iteration;clockStopped=clockAwaiting=false;clockRecord=0;clockFailure=MiniFailure.None;}
            if(manual){clockTitle.text="미니게임 직접 선택";clockTimer.text="방장이 미니게임을 선택하고 있습니다.";Show(clockStop,false);Show(clockRankingRows,false);Show(clockRanking,false);Show(shootingNotice,false);Show(miniInstruction,false);return;}
            if(c.Phase==ClockPhase.Selecting)
            {
                double age=now-c.PhaseAt;float t=Mathf.Clamp01((float)(age/PartyGames.SpinDuration));
                float travel=(PartyGames.Count*12+PartyGames.IndexOf(c.MiniGame)-2)*(1-Mathf.Pow(1-t,3));
                int index=Mathf.FloorToInt(travel);
                if(reelRound!=s.Round || reelIteration!=c.Iteration)
                {reelRound=s.Round;reelIteration=c.Iteration;reelIndex=index;reelStopped=t>=1;}
                if(t<1 && index!=reelIndex)
                {reelSound.pitch=Mathf.Lerp(1.15f,.85f,t);reelSound.PlayOneShot(reelTickSound,.7f*DuelSettings.Effects);}
                if(t>=1 && !reelStopped)
                {reelStopped=true;reelSound.pitch=1;reelSound.PlayOneShot(reelStopSound,DuelSettings.Effects);}
                reelIndex=index;
                partyReel.style.translate=new Translate(0,-60-(travel-index)*60,0);
                for(int i=0;i<partyReelLabels.Length;i++)
                {var game=PartyGames.At(index+i);float opacity=Mathf.Lerp(.25f,1,1-Mathf.Clamp01(Mathf.Abs(i-2-(travel-index))));partyReelLabels[i].text=PartyGames.Name(game);partyReelLabels[i].style.opacity=opacity;partyReelStrikes[i].style.opacity=opacity;Show(partyReelStrikes[i],(c.PlayedMask&PartyGames.Bit(game))!=0);}
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
                {clockTimer.text=clockStopped?"정지 완료 · 결과 대기":expired?"정지 마감":elapsed<0?"준비":elapsed<c.HideAfter?$"{elapsed:F2}":"••.••";clockStop.text="정지 · Space";}
                else if(c.MiniGame==PartyMiniGame.Signal)
                {
                    miniInstruction.text="초록색 ‘지금!’ 신호에서만 입력하세요.";
                    clockTimer.text=clockAwaiting?"입력 확인 중":clockStopped?clockFailure==MiniFailure.None?$"반응 {clockRecord/100.0:F2}초":"너무 빨랐습니다!":expired?"입력 마감":visibleSignal==MiniSignal.Go?"지금!":visibleSignal==MiniSignal.Fake?"지금?":"기다리세요";
                    clockStop.text="반응 · Space";
                }
                else
                {
                    miniInstruction.text="멈춰 점수를 확정하세요. 터지면 0점!";
                    clockTimer.text=clockAwaiting?"확정 중":clockStopped?clockFailure==MiniFailure.None?$"{clockRecord:N0}점 확정":"터졌습니다! 0점":c.Signal==MiniSignal.Burst?"터졌습니다! 0점":elapsed<0?"준비":$"{DuelClockContest.GreedScoreAt(c.StartedAt,now):N0}점";
                    clockStop.text="점수 확정 · Space";
                }
            }
            else if(c.Phase==ClockPhase.QuickResults)
            {
                clockTitle.text=c.First>=0&&s.Connected(c.First)?$"{s.NameForSeat(c.First)}가 대난투에서 살아남았습니다":"대난투 종료";
                clockTimer.text="";
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
            {float limit=s.ShotLimitFor(c.First);clockTitle.text="사격 시간";clockTimer.text=limit==0?"무제한":$"{System.Math.Max(0,limit-System.Math.Max(0,now-DuelClockContest.ShootingStartsAt(c))):F1}초";}
            else {clockTitle.text="다음 미니게임 준비";clockTimer.text=$"선택까지 {System.Math.Max(0,DuelClockContest.ReturnDuration-(now-c.PhaseAt)):F1}초";}
        }
        private static string ResultRecord(PartyMiniGame game,ClockEntry e)
        {
            if(e.Failure==MiniFailure.EarlyInput)return "성급한 입력 · 실패";
            if(e.Failure==MiniFailure.MissedSignal)return "미입력 · 실패";
            if(e.Failure==MiniFailure.Burst)return "폭발 · 0점";
            if(game==PartyMiniGame.Greed)return $"{e.Points:N0}점";
            if(game==PartyMiniGame.Signal)return $"반응 {e.Centiseconds/100.0:F2}초";
            return e.Centiseconds<0?"시간 초과 · 목숨 차감":$"{e.Centiseconds/100.0:F2}초 · 오차 {e.Error/100.0:F2}초";
        }
        public void ReportHit(int round,int life,int shot,bool headshot=false)
        {
            var p=HelloWorldPlayer.Local;
            if(p==null || !p.IsSpawned || p.Seat.Value<0 || p.State.Value.Round!=round ||
                p.State.Value.ForSeat(p.Seat.Value).Life!=life)return;
            if(hitRound!=round || hitLife!=life){hitRound=round;hitLife=life;lastHitShot=0;}
            if(shot<=lastHitShot)return;
            lastHitShot=shot;hitExpiresAt=Time.realtimeSinceStartupAsDouble+.24;
            hitIsPractice=false;
            feedback?.ConfirmHit(headshot);
        }
        public void ReportPracticeHit(int round,int session,int shot,bool headshot)
        {
            var p=HelloWorldPlayer.Local;
            if(p==null||!p.IsSpawned||p.State.Value.Round!=round||!p.State.Value.IsPracticing(p.Seat.Value)||p.State.Value.ForSeat(p.Seat.Value).PracticeSession!=session)return;
            if(practiceHitOwner!=p||practiceHitRound!=round||practiceHitSession!=session)
            {practiceHitOwner=p;practiceHitRound=round;practiceHitSession=session;lastPracticeHit=0;}
            if(shot<=lastPracticeHit)return;
            lastPracticeHit=shot;hitRound=round;hitLife=p.State.Value.ForSeat(p.Seat.Value).Life;
            hitIsPractice=true;hitExpiresAt=Time.realtimeSinceStartupAsDouble+.24;feedback?.ConfirmHit(headshot);
        }
        private void UpdateHitMarker(HelloWorldPlayer p)
        {
            bool valid=p!=null && p.State.Value.Round==hitRound && p.State.Value.ForSeat(p.Seat.Value).Life==hitLife &&
                (hitIsPractice?p.State.Value.IsPracticing(p.Seat.Value)&&p.State.Value.ForSeat(p.Seat.Value).PracticeSession==practiceHitSession:p.State.Value.Alive(p.Seat.Value));
            double remaining=hitExpiresAt-Time.realtimeSinceStartupAsDouble;
            bool visible=valid&&remaining>0;Show(hitMarker,visible);
            if(visible){hitMarker.style.opacity=Mathf.Clamp01((float)(remaining/.12));hitMarker.BringToFront();}
            if(p==null){hitRound=hitLife=-1;lastHitShot=0;hitExpiresAt=0;practiceHitOwner=null;lastPracticeHit=0;hitIsPractice=false;}
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
            var local=HelloWorldPlayer.Local;
            if(local!=null&&state.IsPracticing(local.Seat.Value))
            {Show(victoryLayer,false);Show(highlightLayer,false);Show(resultsLayer,false);SetResultLayout(false);return;}
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
                    resultCueMask=hadPlayer?0:7;
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
                if(elapsed>=0&&(resultCueMask&1)==0){resultCueMask|=1;feedback?.ResultCue(0);}
                if(elapsed>=HighlightStart&&(resultCueMask&2)==0){resultCueMask|=2;feedback?.ResultCue(1);}
                if(elapsed>=ResultsStart&&(resultCueMask&4)==0){resultCueMask|=4;feedback?.ResultCue(2);}
                Show(highlightLayer, elapsed >= HighlightStart);
                float factRise = Mathf.Clamp01((float)((elapsed - HighlightStart - HighlightHold) / HighlightRise));
                factRise = factRise * factRise * (3 - 2 * factRise);
                highlightText.style.translate = new Translate(0, (152 - height * .5f) * factRise, 0);
                Show(resultsLayer, elapsed >= ResultsStart);
                float footer=Mathf.Clamp01((float)((elapsed-ResultsStart-ResultFooterDelay)/ResultFooterDuration));
                footer=footer*footer*(3-2*footer);
                resultsFooter.style.maxHeight=450*footer;resultsFooter.style.opacity=footer;
                resultsFooter.style.translate=new Translate(0,-18*(1-footer),0);
                resultsFooter.SetEnabled(footer>=.999f);
                UpdateResults(state);
                return;
            }
            var owner=HelloWorldPlayer.Local;
            if(owner!=null && state.ForSeat(owner.Seat.Value).Life!=deathBannerLife && victoryText.ClassListContains("death-banner"))
            {victoryExpiresAt=0;victoryText.RemoveFromClassList("death-banner");}
            if(owner!=null && owner.IsSpawned && state.ForSeat(owner.Seat.Value).DeathCause==DuelDeathCause.ClockTimeout && (announcedTimeoutRound!=state.Round||deathBannerLife!=state.ForSeat(owner.Seat.Value).Life))
            {announcedTimeoutRound=state.Round;deathBannerLife=state.ForSeat(owner.Seat.Value).Life;victoryText.text=DeathMessage("초읽기 시간 초과",state.ForSeat(owner.Seat.Value).LivesRemaining);victoryText.AddToClassList("death-banner");victoryExpiresAt=now+4;}
            Show(resultsLayer, false);
            resultsFooter.style.maxHeight=StyleKeyword.None;resultsFooter.style.opacity=1;resultsFooter.style.translate=new Translate(0,0,0);resultsFooter.SetEnabled(true);
            Show(highlightLayer, false);
            victoryText.style.translate = new Translate(0, 0, 0);
            float remaining = (float)(victoryExpiresAt - Time.realtimeSinceStartupAsDouble);
            var lifeFeedback=GetComponent<DuelLifeFeedback>();
            if(lifeFeedback!=null&&lifeFeedback.SuppressDeathBanner&&victoryText.ClassListContains("death-banner"))remaining=0;
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
                $"참가자 준비 {RouletteRules.Count(state.ReadyMask&state.ConnectedMask&~1)} / {participants-1}";
            var local=HelloWorldPlayer.Local;
            if(local!=null&&local.IsSpawned&&RouletteRules.Has(state.ReadyMask,local.Seat.Value)&&!DuelLoadout.Complete(state.ForSeat(local.Seat.Value).SelectedItems))
                resultsReady.text="준비를 취소하고 아이템 2개를 선택해 주세요.";
            else if(local!=null&&local.IsSpawned&&local.Seat.Value==0&&state.Stage==DuelStage.Idle&&participants>=2&&
                (state.ReadyMask&state.ConnectedMask&~1)==(state.ConnectedMask&~1)&&!manager.CanHostStart)
                resultsReady.text=DuelLoadout.Complete(state.ForSeat(0).SelectedItems)?"참가자의 아이템 선택 또는 입장을 확인해 주세요.":"아이템 2개를 선택해 주세요.";
        }
        private static string DeathMessage(string cause,int remainingLives)=>
            (remainingLives>0?"목숨을 잃었습니다!":"사망했습니다!")+"\n사인 ("+cause+")\n남은 목숨: "+remainingLives;
        public void ReportKill(int round, int shot, int victimSeat, DuelWeapon weapon, string attacker, string victim,int victimLife=-1,int remainingLives=-1,DuelHitPart part=DuelHitPart.None,bool fatal=true)
        {
            if (root == null || !manager.Busy || round < killRound || victimSeat < 0 || victimSeat >= RouletteRules.MaxPlayers || !WeaponRules.IsValid(weapon)) return;
            if (round != killRound) { ClearKills(); killRound = round; lastKillShot = 0; }
            if (shot < lastKillShot || shot == lastKillShot && RouletteRules.Has(lastKillVictims, victimSeat)) return;
            if (shot > lastKillShot) lastKillVictims = 0;
            lastKillShot = shot; lastKillVictims |= 1 << victimSeat; AddKill(attacker, victim, weapon,fatal?part:DuelHitPart.None,!fatal);
            var local = HelloWorldPlayer.Local;
            if (fatal && local != null && local.IsSpawned && local.Seat.Value == victimSeat && announcedVictoryRound != round && (victimLife<0||local.State.Value.ForSeat(victimSeat).Life==victimLife))
            {
                victoryRound = round;
                deathBannerLife=local.State.Value.ForSeat(victimSeat).Life;
                victoryText.text = DeathMessage(attacker,remainingLives<0?local.State.Value.ForSeat(victimSeat).LivesRemaining:remainingLives);
                victoryText.AddToClassList("death-banner");
                victoryExpiresAt = Time.realtimeSinceStartupAsDouble + 4;
                Show(victoryLayer, true); victoryLayer.style.opacity = 1;
            }
        }
        private void AddKill(string attacker, string victim, DuelWeapon weapon,DuelHitPart part,bool stunned=false)
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
            if(stunned)Text(row,"감전","kill-stun").pickingMode=PickingMode.Ignore;
            kills.Enqueue(new KillEntry { Row = row, ExpiresAt = Time.realtimeSinceStartupAsDouble + 6 });
        }
        private void UpdateKills()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            while (kills.Count > 0 && kills.Peek().ExpiresAt <= now) kills.Dequeue().Row.RemoveFromHierarchy();
            foreach (var entry in kills) entry.Row.style.opacity = Mathf.Clamp01((float)(entry.ExpiresAt - now));
        }
        private static Texture2D MakeWeaponIcon(DuelWeapon weapon,bool readable=false)
        {
            const int width = 96, height = 48;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = WeaponRules.Name(weapon) + " kill icon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                bool saw = weapon == DuelWeapon.SawedOff;
                if(weapon==DuelWeapon.BAS)
                {
                    bool rifle=(x>=33&&x<=67&&y>=23&&y<=29)||(x>=61&&x<=93&&y>=27&&y<=30);
                    bool stock=x>=3&&x<=34&&y>=13+(x-3)*.18f&&y<=24+(x-3)*.16f;
                    bool optic=(x>=35&&x<=38||x>=86&&x<=88)&&y>=30&&y<=33;
                    float ring=Mathf.Pow((x-34)/12f,2)+Mathf.Pow((y-18)/6f,2);
                    if(rifle||stock||optic||ring<=1&&ring>=.65f)pixels[y*width+x]=new Color(.9f,.86f,.7f,1);
                    continue;
                }
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
            texture.SetPixels(pixels); texture.Apply(false, !readable); return texture;
        }
        private static bool IsUnderAim(DuelSnapshot state, int seat)
        {
            bool practice=state.IsPracticing(seat);
            if (seat < 0 || seat >= RouletteRules.MaxPlayers || !state.Connected(seat) ||
                !practice&&(!state.Alive(seat)||state.Phase != DuelPhase.Playing || state.Stage != DuelStage.Racing)) return false;
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                var action = state.ForSeat(i);
                if (i != seat && state.Connected(i) && (practice?state.IsPracticing(i):state.Alive(i)) && action.Target == seat &&
                    action.Weapon!=DuelWeapon.Taser && action.Ammo>0 && (practice||state.Clock.Phase==ClockPhase.QuickHands||state.Clock.Phase==ClockPhase.Shooting)) return true;
            }
            return false;
        }
        private static Texture2D MakeAimVignette(bool black = false,bool hit=false)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Aim warning vignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float inner=hit?.42f:.74f;
                float horizontal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, 1, Mathf.Abs(x / (size - 1f) * 2 - 1)));
                float vertical = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, 1, Mathf.Abs(y / (size - 1f) * 2 - 1)));
                var color=black?Color.black:hit?new Color(.95f,.24f,.025f):new Color(.80f,.32f,.025f);
                color.a=(1-(1-horizontal)*(1-vertical))*(hit?.68f:.42f);pixels[y*size+x]=color;
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
        private void OnDestroy() { DuelSettings.WindowOpen=false; DestroyRareItemTextures(); DestroyGacha(); if(attendanceOpenSound!=null)Destroy(attendanceOpenSound);if(attendanceRewardSound!=null)Destroy(attendanceRewardSound);if(goldCoinIcon!=null)Destroy(goldCoinIcon);if(inkTexture!=null)Destroy(inkTexture); if(basScopeTexture!=null)Destroy(basScopeTexture); if(headshotIcon!=null)Destroy(headshotIcon);if(legshotIcon!=null)Destroy(legshotIcon);if (font != null) Destroy(font);if(reelTickSound!=null)Destroy(reelTickSound);if(reelStopSound!=null)Destroy(reelStopSound); if (aimVignette != null) Destroy(aimVignette); if(scopeVignette!=null)Destroy(scopeVignette);if(hitVignette!=null)Destroy(hitVignette); foreach (var icon in weaponIcons) if (icon != null) Destroy(icon);foreach(var icon in itemIcons)if(icon!=null)Destroy(icon);foreach(var icon in itemInfoIcons)if(icon!=null)Destroy(icon);if(healTexture!=null)Destroy(healTexture);if(electricTexture!=null)Destroy(electricTexture); }
    }
}



