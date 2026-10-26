using System;
namespace HelloWorld
{
    public partial class HelloWorldManager
    {
        public DuelLanRooms Rooms {get;}=new DuelLanRooms();
        public string RoomTitle {get;private set;}="";
        private string roomId="",roomProof="";
        public void CreateRoom(string title,string password)
        {
            if(Busy)return;
            if(!ValidPassword(password)){Status="비밀번호는 제어 문자 없이32자 이내로 입력하세요.";return;}
            title=(title??"").Trim();if(title.Length>40)title=title.Substring(0,40);
            foreach(char c in title)if(char.IsControl(c)){Status="방 제목을 확인하세요.";return;}
            roomId=Guid.NewGuid().ToString("N");roomProof=DuelRoomAdmission.PasswordProof(roomId,password);
            RoomTitle=title.Length==0?"함께 한 판":title;
            Begin(DuelMode.PvP,false,"127.0.0.1","7777");
            if(Busy&&!leaving&&network.IsHost&&!Rooms.StartHosting())Fail(Rooms.Error);
        }
        public void BrowseRooms(){if(!Busy){Rooms.Search(UnityEngine.Time.realtimeSinceStartupAsDouble);Status="같은 Wi-Fi/LAN의 방을 찾는 중…";}}
        public void JoinRoom(DuelLanRoom room,string password)
        {
            if(Busy||room==null||!room.joinable||room.count>=6)return;
            if(!ValidPassword(password)){Status="비밀번호는32자 이내로 입력하세요.";return;}
            roomId=room.id;roomProof=DuelRoomAdmission.PasswordProof(roomId,password);RoomTitle=room.title;
            Begin(DuelMode.PvP,true,room.address,room.port.ToString());
        }
        private static bool ValidPassword(string password)
        {if(password==null)return true;if(password.Length>32)return false;foreach(char c in password)if(char.IsControl(c))return false;return true;}
        private void TickRooms()
        {
            DuelLanRoom room=null;
            if(Busy&&!leaving&&network.IsHost&&director!=null&&ServerGame.Mode==DuelMode.PvP&&roomId.Length>0)
                room=new DuelLanRoom{id=roomId,title=RoomTitle,port=SessionPort,count=RouletteRules.Count(ServerGame.ConnectedMask),max=6,protocol=DuelLanRooms.Protocol,
                    locked=roomProof.Length>0,joinable=director.Stage==DuelStage.Idle&&ServerGame.Phase!=DuelPhase.Playing&&reservations.Count<6};
            Rooms.Tick(room,UnityEngine.Time.realtimeSinceStartupAsDouble);
        }
    }
}
