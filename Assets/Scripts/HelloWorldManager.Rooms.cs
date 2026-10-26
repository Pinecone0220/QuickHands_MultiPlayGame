using System;
namespace HelloWorld
{
    public partial class HelloWorldManager
    {
        public DuelLanRooms Rooms {get;}=new DuelLanRooms();
        public string RoomTitle {get;private set;}="";
        public string RoomCode {get;private set;}="";
        private string roomId="",roomProof="";
        private enum RoomRequest {None,Create,Join}
        private RoomRequest roomRequest;
        private string pendingCode="",pendingTitle="",pendingPassword="";
        private double roomRequestEnds;
        public bool RoomRequestPending=>roomRequest!=RoomRequest.None;
        public void CreateRoom(string title,string password,string code)
        {
            if(Busy||RoomRequestPending||network==null||network.IsListening||network.ShutdownInProgress)return;
            if(!DuelRoomCode.TryNormalize(code,out code)){Status="방 코드는 영문·숫자 4~12자로 입력하세요.";return;}
            if(!ValidPassword(password)){Status="비밀번호는 제어 문자 없이32자 이내로 입력하세요.";return;}
            title=(title??"").Trim();if(title.Length>40)title=title.Substring(0,40);
            foreach(char c in title)if(char.IsControl(c)){Status="방 제목을 확인하세요.";return;}
            StartRoomRequest(RoomRequest.Create,code,title,password);
        }
        private void CreateRoomNow(string title,string password,string code)
        {
            roomId=Guid.NewGuid().ToString("N");roomProof=DuelRoomAdmission.PasswordProof(roomId,password);
            RoomTitle=title.Length==0?"함께 한 판":title;RoomCode=code;
            Begin(DuelMode.PvP,false,"127.0.0.1","7777");
            if(Busy&&!leaving&&network.IsHost&&!Rooms.StartHosting())Fail(Rooms.Error);
        }
        public void BrowseRooms(){if(!Busy&&!RoomRequestPending){Rooms.Search(UnityEngine.Time.realtimeSinceStartupAsDouble);Status="같은 Wi-Fi/LAN의 방을 찾는 중…";}}
        public void JoinRoomCode(string code,string password)
        {
            if(Busy||RoomRequestPending||network==null||network.IsListening||network.ShutdownInProgress)return;
            if(!DuelRoomCode.TryNormalize(code,out code)){Status="방 코드는 영문·숫자 4~12자로 입력하세요.";return;}
            if(!ValidPassword(password)){Status="비밀번호는32자 이내로 입력하세요.";return;}
            StartRoomRequest(RoomRequest.Join,code,"",password);
        }
        private void StartRoomRequest(RoomRequest request,string code,string title,string password)
        {
            double now=UnityEngine.Time.realtimeSinceStartupAsDouble;
            if(!Rooms.Search(now)){Status=Rooms.Error;return;}
            roomRequest=request;pendingCode=code;pendingTitle=title;pendingPassword=password;
            roomRequestEnds=now+2;Status=request==RoomRequest.Create?"방 코드 중복을 확인하는 중…":"방 코드로 찾는 중…";
        }
        private void FinishRoomRequest(double now)
        {
            if(!RoomRequestPending||now<roomRequestEnds)return;
            int matches=0;DuelLanRoom match=null;
            foreach(var room in Rooms.Rooms)if(room.code==pendingCode){matches++;match=room;}
            var request=roomRequest;string code=pendingCode,title=pendingTitle,password=pendingPassword;
            CancelRoomRequest();
            if(request==RoomRequest.Create)
            {
                if(matches>0){Status="이미 사용 중인 방 코드입니다. 다른 코드를 입력하세요.";return;}
                CreateRoomNow(title,password,code);
            }
            else if(matches==0)Status="해당 방 코드를 찾지 못했습니다. 같은 Wi-Fi/LAN인지 확인하세요.";
            else if(matches>1)Status="같은 방 코드가 여러 개입니다. 방 목록에서 선택하세요.";
            else JoinRoom(match,password);
        }
        private void CancelRoomRequest()
        {roomRequest=RoomRequest.None;pendingCode=pendingTitle=pendingPassword="";Rooms.StopBrowsing();}
        public void JoinRoom(DuelLanRoom room,string password)
        {
            if(Busy||RoomRequestPending||room==null)return;
            if(!room.joinable||room.count>=6){Status=room.count>=6?"가득 찬 방입니다.":"이미 게임이 진행 중인 방입니다.";return;}
            if(!ValidPassword(password)){Status="비밀번호는32자 이내로 입력하세요.";return;}
            roomId=room.id;roomProof=DuelRoomAdmission.PasswordProof(roomId,password);RoomTitle=room.title;RoomCode=room.code;
            Begin(DuelMode.PvP,true,room.address,room.port.ToString());
        }
        private static bool ValidPassword(string password)
        {if(password==null)return true;if(password.Length>32)return false;foreach(char c in password)if(char.IsControl(c))return false;return true;}
        private void TickRooms()
        {
            DuelLanRoom room=null;
            if(Busy&&!leaving&&network.IsHost&&director!=null&&ServerGame.Mode==DuelMode.PvP&&roomId.Length>0)
                room=new DuelLanRoom{id=roomId,code=RoomCode,title=RoomTitle,port=SessionPort,count=RouletteRules.Count(ServerGame.ConnectedMask),max=6,protocol=DuelLanRooms.Protocol,
                    locked=roomProof.Length>0,joinable=director.Stage==DuelStage.Idle&&ServerGame.Phase!=DuelPhase.Playing&&OccupiedRoomSlots<RouletteRules.MaxPlayers};
            double now=UnityEngine.Time.realtimeSinceStartupAsDouble;Rooms.Tick(room,now);FinishRoomRequest(now);
        }
    }
}
