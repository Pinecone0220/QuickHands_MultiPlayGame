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
        public void AddLobbyBot()
        {
            if(!CanAddBot)return;
            for(int seat=1;seat<RouletteRules.MaxPlayers;seat++)
            {
                if(RouletteRules.Has(ServerGame.ConnectedMask,seat))continue;
                if(!director.AddBot(seat,ServerNow))return;
                names[seat]=new FixedString64Bytes("AI"+seat);damageReports[seat]=default;
                Publish();return;
            }
        }
    }
}
