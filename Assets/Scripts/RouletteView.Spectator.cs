using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private DuelSpectator spectator;
        private Label spectatorNotice;
        private void BuildSpectatorNotice()
        {
            spectatorNotice=new Label{enableRichText=false,pickingMode=PickingMode.Ignore};
            spectatorNotice.style.position=Position.Absolute;spectatorNotice.style.bottom=18;
            spectatorNotice.style.left=Length.Percent(15);spectatorNotice.style.right=Length.Percent(15);
            spectatorNotice.style.unityTextAlign=TextAnchor.MiddleCenter;spectatorNotice.style.fontSize=16;
            spectatorNotice.style.color=new Color(.92f,.85f,.65f);
            root.Add(spectatorNotice);Show(spectatorNotice,false);
        }
        private void UpdateSpectatorNotice(HelloWorldPlayer player)
        {
            bool visible=player!=null&&spectator!=null&&spectator.Active;
            Show(spectatorNotice,visible);if(!visible)return;
            string view=spectator.Watching?player.State.Value.NameForSeat(spectator.TargetSeat)+" · 1인칭 관전":"기본 관전 시점";
            spectatorNotice.text="Q 이전   ·   "+view+"   ·   다음 E";
        }
    }
}
