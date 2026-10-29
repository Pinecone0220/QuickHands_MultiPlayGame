using System;
using Unity.Collections;
namespace HelloWorld
{
    public partial class HelloWorldManager
    {
        // Connected seats include bots. Approval reservations include only human connections.
        private int OccupiedRoomSlots=>ServerGame==null?0:RouletteRules.Count(ServerGame.ConnectedMask)+Math.Max(0,reservations.Count-players.Count);
        public bool CanAddBot=>network!=null&&network.IsHost&&!leaving&&director!=null&&
            reservations.Count==players.Count&&director.Stage==DuelStage.Idle&&ServerGame.Phase!=DuelPhase.Playing&&OccupiedRoomSlots<RouletteRules.MaxPlayers;
        public AiDifficulty LobbyBotDifficulty { get; set; }=AiDifficulty.Normal;
        private readonly System.Collections.Generic.List<int> lobbyBotOrder=new System.Collections.Generic.List<int>();
        public bool CanRemoveBot=>network!=null&&network.IsHost&&!leaving&&director!=null&&
            reservations.Count==players.Count&&director.Stage==DuelStage.Idle&&ServerGame.Phase!=DuelPhase.Playing&&lobbyBotOrder.Count>0;
        public void AddLobbyBot()
        {
            if(!CanAddBot)return;
            for(int seat=1;seat<RouletteRules.MaxPlayers;seat++)
            {
                if(RouletteRules.Has(ServerGame.ConnectedMask,seat))continue;
                if(!director.AddBot(seat,ServerNow,LobbyBotDifficulty))return;
                lobbyBotOrder.Add(seat);
                string level=LobbyBotDifficulty==AiDifficulty.Easy?"쉬움":LobbyBotDifficulty==AiDifficulty.Hard?"어려움":"보통";
                names[seat]=new FixedString64Bytes("AI"+seat+" ("+level+")");damageReports[seat]=default;
                Publish();return;
            }
        }
        public void RemoveLobbyBot()
        {
            if(!CanRemoveBot)return;
            int index=lobbyBotOrder.Count-1,seat=lobbyBotOrder[index];
            if(!director.IsBot(seat)||!director.Disconnect(seat,ServerNow))return;
            lobbyBotOrder.RemoveAt(index);names[seat]=default;damageReports[seat]=default;
            Publish();
        }
    }
}
