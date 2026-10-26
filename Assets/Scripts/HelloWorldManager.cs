using System;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace HelloWorld
{
    public partial class HelloWorldManager : MonoBehaviour
    {
        public static HelloWorldManager Instance { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "모드를 선택하세요.";
        public RouletteRules ServerGame => director?.Game;
        private DuelDirector director;
        private double ServerNow => network.ServerTime.Time;
        public RouletteTable Table { get; private set; }
        private NetworkManager network;
        private readonly Dictionary<ulong, HelloWorldPlayer> players = new Dictionary<ulong, HelloWorldPlayer>();
        private readonly DuelChatLog chatLog=new DuelChatLog();
        private readonly HashSet<ulong> reservations = new HashSet<ulong>();
        private readonly Dictionary<ulong, string> requestedNames = new Dictionary<ulong, string>();
        private readonly FixedString64Bytes[] names = new FixedString64Bytes[RouletteRules.MaxPlayers];
        private int joinOrder;
        private int publishedRound, publishedShots;
        private DuelRoundResults roundResults;
        private readonly DuelDamageReport[] damageReports = new DuelDamageReport[RouletteRules.MaxPlayers];
        private readonly int[] publishedLives=new int[RouletteRules.MaxPlayers];
        public string Nickname { get; set; } = "";
        public ushort SessionPort { get; private set; }
        public bool DeveloperMode { get; private set; }
        private double connectDeadline;
        private bool leaving, everConnected;
        public AiDifficulty Difficulty { get; set; } = AiDifficulty.Normal;
        public int AiCount { get; set; } = 1;
        private int sessionAiCount;

        private void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
        }

        private void Start()
        {
            network = NetworkManager.Singleton;
            gameObject.AddComponent<LobbyJoinAudio>();
            gameObject.AddComponent<DuelBackgroundMusic>();
            gameObject.AddComponent<DuelGameAudio>();
            gameObject.AddComponent<DuelFirstPerson>();
            Table = gameObject.AddComponent<RouletteTable>();
            gameObject.AddComponent<RouletteView>().Initialize(this);
            gameObject.AddComponent<DuelChat>();
            gameObject.AddComponent<DuelPointer>();
            if (network == null) { Status = "씬에 NetworkManager가 없습니다."; return; }
            network.NetworkConfig.ConnectionApproval = true;
            network.NetworkConfig.ProtocolVersion = DuelLanRooms.Protocol;
            network.ConnectionApprovalCallback = Approve;
            network.OnClientDisconnectCallback += Disconnected;
            network.OnClientConnectedCallback += Connected;
            network.OnTransportFailure += TransportFailed;
        }

        public void Begin(DuelMode mode, bool join, string address, string portText)
        {
            if (network == null || Busy || network.IsListening || network.ShutdownInProgress) return;
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            { Status = "포트는 1~65535 사이의 숫자여야 합니다."; return; }
            if (join && !IPAddress.TryParse(address.Trim(), out _))
            { Status = "접속할 호스트의 IP 주소를 입력하세요."; return; }
            var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) { Status = "Unity Transport가 필요합니다."; return; }
            ushort selectedPort = port;
            try
            {
                if (!join && mode == DuelMode.PvP) selectedPort = SelectHostPort(port);
            }
            catch (SocketException exception)
            {
                Debug.LogWarning("호스트 포트 확인 실패: " + exception.SocketErrorCode);
                Status = "사용 가능한 UDP 포트를 찾지 못했습니다. 네트워크 설정을 확인하세요.";
                return;
            }
            DeveloperMode=false;
            SessionPort = mode == DuelMode.AI && !join ? (ushort)0 : selectedPort;
            players.Clear(); reservations.Clear();chatLog.Clear();GetComponent<DuelChat>()?.ClearRoom();
            requestedNames.Clear(); Array.Clear(names, 0, names.Length); joinOrder = 0;
            publishedRound = publishedShots = 0;
            Array.Clear(damageReports, 0, damageReports.Length);
            Array.Clear(publishedLives,0,publishedLives.Length);
            roundResults = default;
            if(mode==DuelMode.AI){roomId=roomProof=RoomTitle="";}
            Rooms.StopBrowsing();
            network.NetworkConfig.ConnectionData = roomId.Length>0?DuelRoomAdmission.Encode(roomId,Nickname,roomProof):Encoding.UTF8.GetBytes(NicknamePolicy.Normalize(Nickname));
            leaving = everConnected = false;
            director = join ? null : new DuelDirector(mode, difficulty: Difficulty);
            if (director != null)
            {
                director.ShotConfirmed += Publish;
                director.PracticeHitConfirmed += (actor,session,shot,headshot)=>
                {
                    Publish();
                    foreach(var player in players.Values)
                        if(player!=null&&player.IsSpawned&&player.Seat.Value==actor)
                            player.ReportPracticeHitRpc(director.Game.Round,session,shot,headshot);
                };
            }
            sessionAiCount = Mathf.Clamp(AiCount, 1, RouletteRules.MaxPlayers - 1);
            Busy = true;
            connectDeadline = Time.realtimeSinceStartupAsDouble + 15;
            Status = join ? "상대의 방에 연결 중…" : mode == DuelMode.AI ? "딜러가 자리에 앉습니다…" : "상대를 기다리는 중…";
            if (!join && mode == DuelMode.PvP && selectedPort != port)
                Status = $"{port} 포트가 사용 중이어서 {selectedPort} 포트로 방을 만듭니다.";
            // Solo uses loopback and a free ephemeral port; approval rejects remote guests.
            transport.SetConnectionData(join ? address.Trim() : "127.0.0.1",
                SessionPort,
                mode == DuelMode.AI ? "127.0.0.1" : "0.0.0.0");
            try
            {
                if (!(join ? network.StartClient() : network.StartHost()))
                    Fail(join ? "접속을 시작하지 못했습니다. 호스트 IP와 포트를 확인하세요." :
                        $"UDP {SessionPort} 포트로 방을 열지 못했습니다. 포트가 새로 점유되었거나 네트워크 오류가 발생했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Fail("연결을 시작하지 못했습니다. 네트워크 설정을 확인하세요.");
            }
        }

        private static ushort SelectHostPort(ushort requested)
        {
            for (int candidate = requested; candidate <= ushort.MaxValue && candidate < requested + 32; candidate++)
                if (TryHostPort((ushort)candidate, out ushort available)) return available;
            // Let the OS pick a free port if the nearby ports are all occupied.
            if (TryHostPort(0, out ushort ephemeral)) return ephemeral;
            throw new SocketException((int)SocketError.AddressAlreadyInUse);
        }
        private static bool TryHostPort(ushort candidate, out ushort available)
        {
            available = 0;
            try
            {
                using (var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    probe.ExclusiveAddressUse = true;
                    probe.Bind(new IPEndPoint(IPAddress.Any, candidate));
                    available = (ushort)((IPEndPoint)probe.LocalEndPoint).Port;
                    return true;
                }
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                exception.SocketErrorCode == SocketError.AccessDenied) { return false; }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool host = request.ClientNetworkId == Unity.Netcode.NetworkManager.ServerClientId;
            bool allowed = ServerGame != null && (host ||
                (ServerGame.Mode == DuelMode.PvP && director.Stage == DuelStage.Idle && ServerGame.Phase != DuelPhase.Playing && reservations.Count < RouletteRules.MaxPlayers));
            string requestedName = "";
            string admissionError="";
            try
            {
                byte[] payload = request.Payload ?? Array.Empty<byte>();
                if(roomId.Length>0)
                {if(!DuelRoomAdmission.TryRead(payload,roomId,roomProof,out requestedName,out admissionError))allowed=false;}
                else if (payload.Length > NicknamePolicy.MaxBytes) allowed = false;
                else requestedName = NicknamePolicy.Normalize(new UTF8Encoding(false, true).GetString(payload));
            }
            catch (DecoderFallbackException) { allowed = false; }
            response.Approved = allowed;
            response.CreatePlayerObject = allowed;
            response.Pending = false;
            response.Reason = allowed ? string.Empty : admissionError.Length>0?admissionError:"참가할 수 없습니다. 진행 중인 경기 또는 가득 찬 6인 방입니다.";
            if (allowed) { reservations.Add(request.ClientNetworkId); requestedNames[request.ClientNetworkId] = requestedName; }
        }

        public void RegisterPlayer(HelloWorldPlayer player)
        {
            if (ServerGame == null || !network.IsServer) return;
            int seat = -1;
            for (int i = 0; i < RouletteRules.MaxPlayers; i++) if (!RouletteRules.Has(ServerGame.ConnectedMask, i)) { seat = i; break; }
            if (seat < 0 || !director.Join(seat, ServerNow)) { network.DisconnectClient(player.OwnerClientId); return; }
            player.Seat.Value = seat; players[player.OwnerClientId] = player;
            damageReports[seat] = default;
            requestedNames.TryGetValue(player.OwnerClientId, out string requested);
            names[seat] = new FixedString64Bytes(NicknamePolicy.Resolve(requested, ++joinOrder));
            requestedNames.Remove(player.OwnerClientId);
            if (ServerGame.Mode == DuelMode.AI)
                for (int bot = 1; bot <= sessionAiCount; bot++)
                {
                    director.Join(bot, ServerNow);
                    names[bot] = new FixedString64Bytes(sessionAiCount == 1 ? "AI" : "AI" + bot);
                }
            Publish();
            StartCoroutine(ReplayChat(player));
        }
        private IEnumerator ReplayChat(HelloWorldPlayer player)
        {
            // Wait until the player's spawn has been sent before owner-targeted history RPCs.
            yield return null;
            if(network==null||!network.IsServer||player==null||!player.IsSpawned)yield break;
            foreach(var line in chatLog.History)
                player.ReceiveChatRpc(line.Sequence,line.Sender,line.Name,line.Message);
        }

        private void Publish()
        {
            var state = DuelSnapshot.From(director);
            state.Name0 = names[0]; state.Name1 = names[1]; state.Name2 = names[2]; state.Name3 = names[3]; state.Name4 = names[4]; state.Name5 = names[5];
            if (roundResults.Round != state.Round)
            {
                roundResults = default;
                if (state.Phase == DuelPhase.Finished)
                    roundResults = DuelRoundResults.Capture(director, names, ServerNow);
            }
            state.Results = roundResults;
            foreach (var player in players.Values)
                if (player != null && player.IsSpawned) player.State.Value = state;
            if (publishedRound != state.Round)
            {
                publishedRound = state.Round; publishedShots = 0;
                Array.Clear(damageReports, 0, damageReports.Length);
                Array.Clear(publishedLives,0,publishedLives.Length);
                foreach (var player in players.Values)
                    if (player != null && player.IsSpawned) player.DeathReport.Value = default;
            }
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                if(publishedLives[seat]!=state.ForSeat(seat).Life)
                {publishedLives[seat]=state.ForSeat(seat).Life;damageReports[seat]=default;foreach(var player in players.Values)if(player!=null&&player.IsSpawned&&player.Seat.Value==seat)player.DeathReport.Value=default;}
            if (state.Shots > publishedShots)
            {
                publishedShots = state.Shots;
                if(ServerGame.LastDamage>0)
                    foreach(var player in players.Values)
                        if(player!=null && player.IsSpawned && player.Seat.Value==state.LastActor)
                            player.ReportHitRpc(state.Round,state.ForSeat(state.LastActor).Life,state.Shots,
                                WeaponRules.IsPrecision(ServerGame.LastWeapon)&&state.ForSeat(state.LastActor).LastHitPart==DuelHitPart.Head);
                for (int victim = 0; victim < RouletteRules.MaxPlayers; victim++)
                {
                    int damage = ServerGame.LastDamageForSeat(victim);
                    if (damage <= 0) continue;
                    var report = damageReports[victim]; report.Round = state.Round;report.Life=state.ForSeat(victim).Life;
                    bool splash = victim != state.LastTarget;
                    report.Add(new DuelDamageEntry { Attacker = names[state.LastActor], Weapon = ServerGame.LastWeapon,
                        Part = splash ? DuelHitPart.None : state.ForSeat(state.LastActor).LastHitPart,
                        Damage = damage, Shot = state.Shots, Splash = splash, Fatal = RouletteRules.Has(ServerGame.LastKilledMask, victim) });
                    damageReports[victim] = report;
                    if (!RouletteRules.Has(ServerGame.LastKilledMask, victim)) continue;
                    foreach (var player in players.Values)
                        if (player != null && player.IsSpawned && player.Seat.Value == victim) player.DeathReport.Value = report;
                }
                for (int victim = 0; victim < RouletteRules.MaxPlayers; victim++)
                    if (RouletteRules.Has(ServerGame.LastKilledMask, victim)||ServerGame.LastWeapon==DuelWeapon.Taser&&RouletteRules.Has(ServerGame.LastHitMask,victim))
                        foreach (var player in players.Values)
                            if (player != null && player.IsSpawned)
                                player.ReportKillRpc(state.Round, state.Shots, victim, ServerGame.LastWeapon, names[state.LastActor], names[victim],state.ForSeat(victim).Life,state.ForSeat(victim).LivesRemaining,victim==state.LastTarget?state.ForSeat(state.LastActor).LastHitPart:DuelHitPart.None,RouletteRules.Has(ServerGame.LastKilledMask,victim));
            }
            foreach(var player in players.Values)
                if(player!=null && player.IsSpawned && state.ForSeat(player.Seat.Value).DeathCause==DuelDeathCause.ClockTimeout)
                {var report=damageReports[player.Seat.Value];report.Round=state.Round;report.Life=state.ForSeat(player.Seat.Value).Life;player.DeathReport.Value=report;}
        }

        public bool RequestChat(ulong sender,FixedString512Bytes message)
        {
            if(network==null||!network.IsServer||leaving||ServerGame==null||
                !players.TryGetValue(sender,out var player)||player==null||!player.IsSpawned||
                player.OwnerClientId!=sender||player.Seat.Value<0||!RouletteRules.Has(ServerGame.ConnectedMask,player.Seat.Value))return false;
            if(!chatLog.TryAppend(sender,names[player.Seat.Value],message,ServerNow,out var line))
            {player.RejectChatRpc();return false;}
            foreach(var recipient in players.Values)
                if(recipient!=null&&recipient.IsSpawned)
                    recipient.ReceiveChatRpc(line.Sequence,line.Sender,line.Name,line.Message);
            return true;
        }
        public void RequestInput(ulong sender, int digit, int version)
        {
            if (!network.IsServer || director == null || !players.TryGetValue(sender, out var player) ||
                player == null || !player.IsSpawned) return;
            if (director.Input(player.Seat.Value, digit, version, ServerNow)) Publish();
        }
        public void SetRoomShotTime(float seconds)
        {
            if(!DeveloperMode||network==null || !network.IsHost || director==null || director.Stage!=DuelStage.Idle)return;
            if(director.Game.SetShotTime(seconds))Publish();
        }
        public void RequestLoadoutItem(ulong sender,DuelItem item,int round)
        {
            if(network==null||!network.IsServer||director==null||round!=ServerGame.Round||
                !players.TryGetValue(sender,out var player)||player==null||!player.IsSpawned||player.OwnerClientId!=sender)return;
            if(director.SelectItem(player.Seat.Value,item))Publish();
        }

        public void SetRoomUnlimitedItems(bool enabled)
        {if(!DeveloperMode||network==null||!network.IsHost||director==null||director.Stage!=DuelStage.Idle)return;if(director.Game.SetUnlimitedItems(enabled))Publish();}
        public void SetRoomAutoSelectItems(bool enabled)
        {if(!DeveloperMode||network==null||!network.IsHost||director==null||director.Stage!=DuelStage.Idle)return;if(director.SetAutoSelectItems(enabled))Publish();}
        public void SetRoomManualMiniGame(bool enabled)
        {if(!DeveloperMode||network==null||!network.IsHost||director==null||director.Stage!=DuelStage.Idle)return;if(director.Game.SetManualMiniGame(enabled))Publish();}
        public void ToggleDeveloperMode()
        {
            if(network==null||!network.IsHost||!network.IsListening||director==null)return;
            DeveloperMode=!DeveloperMode;
            if(!DeveloperMode)director.ResetDeveloperSettings(ServerNow);
            Publish();
        }
        public void ChooseMiniGame(PartyMiniGame game)
        {if(network==null||!network.IsHost||director==null)return;if(director.ChooseMiniGame(game,director.Clock.Iteration,ServerNow))Publish();}
        public void RequestItem(ulong sender,DuelItem item,int round,int iteration)
        {if(network==null||!network.IsServer||director==null||round!=ServerGame.Round||!players.TryGetValue(sender,out var p)||p==null||!p.IsSpawned)return;if(director.UseItem(p.Seat.Value,item,iteration,ServerNow))Publish();}
        public void RequestRematch(ulong sender, int round)
        {
            if (network==null||!network.IsServer||director==null||!players.TryGetValue(sender,out var player)||player==null||!player.IsSpawned||player.OwnerClientId!=sender)return;
            if (round == ServerGame.Round && reservations.Count == players.Count && director.Ready(player.Seat.Value, ServerNow)) Publish();
        }
        public void RequestLook(ulong sender,float yaw,float pitch,bool ads,int round)
        {
            if (!network.IsServer || director==null || round!=ServerGame.Round || !players.TryGetValue(sender,out var p) || p==null || !p.IsSpawned) return;
            if (director.Look(p.Seat.Value,yaw,pitch,ads,ServerNow)) Publish();
        }
        public void RequestStopDice(ulong sender,int round,int iteration,int diceRound)
        {
            if(network==null||!network.IsServer||director==null||round!=ServerGame.Round||
                !players.TryGetValue(sender,out var player)||player==null||!player.IsSpawned||player.OwnerClientId!=sender)return;
            if(director.StopDice(player.Seat.Value,iteration,diceRound,ServerNow))Publish();
        }
        public void RequestStopClock(ulong sender,int round,int iteration,double sample)
        {
            if (!network.IsServer || director==null || !players.TryGetValue(sender,out var p) || p==null || !p.IsSpawned) return;
            bool accepted=round==ServerGame.Round && director.StopClock(p.Seat.Value,iteration,sample,ServerNow);
            if(accepted && director.Clock.Phase==ClockPhase.Ranking) Publish();
            var entry=director.SubmittedEntry(p.Seat.Value);
            p.ReportClockStopRpc(round,iteration,accepted,director.Clock.MiniGame==PartyMiniGame.Greed?entry.Points:entry.Centiseconds,entry.Failure);
        }
        public void RequestMemoryInput(ulong sender,int round,int iteration,int memoryRound,int index,int direction)
        {
            if(!network.IsServer||director==null||!players.TryGetValue(sender,out var p)||p==null||!p.IsSpawned)return;
            var before=director.Clock.Memory;
            bool accepted=round==ServerGame.Round&&director.InputMemory(p.Seat.Value,iteration,memoryRound,index,direction,ServerNow);
            var after=director.Clock.Memory;
            if(accepted&&(before.Phase!=after.Phase||before.Round!=after.Round||before.ActiveMask!=after.ActiveMask||before.EliminatedMask!=after.EliminatedMask))Publish();
            p.ReportMemoryInputRpc(round,iteration,memoryRound,accepted,director.MemoryProgress(p.Seat.Value));
        }
        public void RequestFire(ulong sender,float yaw,float pitch,bool ads,int round,int version,double shotTime)
        {
            if (!network.IsServer || director==null || round!=ServerGame.Round || !players.TryGetValue(sender,out var p) || p==null || !p.IsSpawned) return;
            // Hit seat, hit part, origin, spread and damage are always calculated by the server.
            if (director.Fire(p.Seat.Value,yaw,pitch,ads,version,shotTime,ServerNow)) Publish();
        }
        public void RequestInk(ulong sender,float yaw,float pitch,double sample,int round,int iteration,int life,int version)
        {
            if(network==null||!network.IsServer||director==null||round!=ServerGame.Round||!players.TryGetValue(sender,out var p)||p==null||!p.IsSpawned||p.OwnerClientId!=sender)return;
            if(director.ThrowInk(p.Seat.Value,yaw,pitch,iteration,life,version,sample,ServerNow))Publish();
        }
        public void RequestQuickItem(ulong sender,bool equip,int round,int iteration,int life,int version)
        {
            if(network==null||!network.IsServer||director==null||round!=ServerGame.Round||!players.TryGetValue(sender,out var p)||p==null||!p.IsSpawned||p.OwnerClientId!=sender)return;
            if(director.SwitchQuickItem(p.Seat.Value,equip,iteration,life,version,ServerNow))Publish();
        }
        public void RequestWeapon(ulong sender, DuelWeapon weapon, int round)
        {
            if (!network.IsServer || director == null || !players.TryGetValue(sender, out var player) ||
                player == null || !player.IsSpawned || round != ServerGame.Round) return;
            if (director.SelectWeapon(player.Seat.Value, weapon, ServerNow)) Publish();
        }
        private void Update()
        {
            if (network == null) return;
            TickRooms();
            if (leaving && !network.IsListening && !network.ShutdownInProgress)
            {
                Busy = leaving = everConnected = false;
                director = null;
                players.Clear(); reservations.Clear();
            }
            if (Busy && !leaving && !everConnected && Time.realtimeSinceStartupAsDouble > connectDeadline)
                Fail("연결 시간이 초과되었습니다. 방 목록을 새로고침해 다시 참가하세요.");
            if (!leaving && network.IsServer && director != null)
            {
                if (director.Tick(ServerNow)) Publish();

            }
        }
        private void Connected(ulong id)
        {
            if (id == network.LocalClientId) everConnected = true;
        }

        private void Disconnected(ulong id)
        {
            chatLog.Disconnect(id);
            reservations.Remove(id);
            requestedNames.Remove(id);
            if (leaving) return;
            if (network.IsServer && id != Unity.Netcode.NetworkManager.ServerClientId)
            {
                if (players.TryGetValue(id, out var departing))
                {
                    int seat = departing.Seat.Value;
                    players.Remove(id);
                    director?.Disconnect(seat, ServerNow);
                    if (ServerGame != null) Publish();
                }
            }
            else if (id == network.LocalClientId)
            {
                string reason = network.DisconnectReason;
                Fail(string.IsNullOrWhiteSpace(reason) ? "호스트와 연결이 끊어졌습니다." : reason);
            }
        }

        private void TransportFailed() => Fail("네트워크 연결 오류가 발생했습니다. 방 목록을 확인하세요.");
        private void Fail(string message)
        {
            Status = message;
            Rooms.StopHosting();roomId=roomProof=RoomTitle="";
            leaving = true;
            if (network != null && !network.ShutdownInProgress) network.Shutdown();
        }
        public void Leave()
        {
            if (Busy) Fail("테이블을 떠났습니다. 새 모드를 선택하세요.");
        }
        private void OnDestroy()
        {
            Rooms.Dispose();
            if (network != null)
            {
                network.OnClientDisconnectCallback -= Disconnected;
                network.OnClientConnectedCallback -= Connected;
                network.OnTransportFailure -= TransportFailed;
                network.ConnectionApprovalCallback = null;
                if (Busy && !network.ShutdownInProgress) network.Shutdown();
            }
            if (Instance == this) Instance = null;
        }
    }
}




