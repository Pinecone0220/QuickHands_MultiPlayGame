using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement inkOverlay;
        private Texture2D inkTexture;
        private DuelInkFade inkFade;
        private int inkViewSeat=-1;
        private void UpdateInkLayering()
        {
            if(inkOverlay==null||root==null)return;
            bool covering=inkOverlay.style.display.value!=DisplayStyle.None&&inkOverlay.style.opacity.value>0;
            var player=HelloWorldPlayer.Local;
            bool result=covering&&player!=null&&player.IsSpawned&&player.State.Value.Phase==DuelPhase.Playing&&
                (player.State.Value.Clock.Phase==ClockPhase.Ranking||player.State.Value.Clock.Phase==ClockPhase.QuickResults||player.State.Value.Clock.Phase==ClockPhase.Returning);
            // The clock panel normally belongs to the gameplay HUD; lift only its new result view.
            var parent=result?root:table;
            if(clockPanel.parent!=parent){clockPanel.RemoveFromHierarchy();parent.Add(clockPanel);}
            if(!covering)return;
            // Chat also raises itself in LateUpdate; its earlier execution order keeps this mask last.
            inkOverlay.BringToFront();
            if(result)clockPanel.BringToFront();
            diceLayer?.BringToFront();resultsLayer.BringToFront();victoryLayer.BringToFront();highlightLayer.BringToFront();
        }
        private void BuildInkOverlay()
        {
            inkOverlay=Box(root,"ink-effect");inkOverlay.pickingMode=PickingMode.Ignore;
            const int size=512;inkTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ink pouch splash",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2-1,ny=(y+.5f)/size*2-1;
                float radius=Mathf.Pow(Mathf.Pow(Mathf.Abs(nx)/1.11f,4)+Mathf.Pow(Mathf.Abs(ny)/1.11f,4),.25f);
                float edge=1+.035f*Mathf.Sin(x*.09f+y*.06f)+.025f*Mathf.Cos(y*.10f-x*.02f);
                float alpha=Mathf.Clamp01((edge-radius)*40);
                pixels[y*size+x]=new Color(.018f,.009f,.029f,alpha*.995f);
            }
            inkTexture.SetPixels(pixels);inkTexture.Apply(false,true);inkOverlay.style.backgroundImage=new StyleBackground(inkTexture);
            inkOverlay.SendToBack();Show(inkOverlay,false);
        }
        private void UpdateInkOverlay(HelloWorldPlayer player)
        {
            if(inkOverlay==null)return;
            float opacity=0;
            int seat=player!=null&&player.IsSpawned?(spectator!=null&&spectator.Watching?spectator.TargetSeat:player.Seat.Value):-1;
            if(inkViewSeat!=seat){inkViewSeat=seat;inkFade=default;}
            if(player!=null&&player.IsSpawned&&seat>=0&&player.State.Value.Alive(seat))
            {
                var s=player.State.Value;var ink=s.ForSeat(seat).Ink;
                opacity=inkFade.Observe(ink,player.NetworkManager.ServerTime.Time,s.Phase,s.Clock.Phase,s.Clock.PhaseAt,s.StageStartedAt);
            }
            else inkFade=default;
            inkOverlay.style.opacity=opacity;Show(inkOverlay,opacity>0);
        }
        private bool LiveInkTime(HelloWorldPlayer p)=>p!=null&&p.State.Value.Phase==DuelPhase.Playing&&p.State.Value.Alive(p.Seat.Value)&&p.State.Value.Clock.Phase==ClockPhase.Shooting;
        private void UpdateInkItem(HelloWorldPlayer p,bool itemTime,bool live)
        {
            bool floating=live&&!itemTime;
            var parent=floating?root:clockPanel;
            if(itemRow.parent!=parent){itemRow.RemoveFromHierarchy();parent.Add(itemRow);}
            itemRow.EnableInClassList("ink-live-items",floating);
            var ink=p.State.Value.ForSeat(p.Seat.Value).Ink;double now=p.NetworkManager.ServerTime.Time;
            bool resolved=ink.ThrownAt<0||ink.ImpactAt>=ink.ThrownAt||now-ink.ThrownAt>=DuelInk.ReleaseDelay+DuelInk.Lifetime;
            Show(itemButtons[2],ink.Owned&&(itemTime||live));
            itemButtons[2].SetEnabled(ink.Owned&&!p.State.Value.ForSeat(p.Seat.Value).Stunned&&(ink.Armed||resolved&&!DuelInk.Busy(ink,now)));
            itemButtons[2].EnableInClassList("item-used",ink.Armed);
            var a=p.State.Value.ForSeat(p.Seat.Value);
            if(itemTime&&p.State.Value.Clock.MiniGame==PartyMiniGame.QuickHands)
            {itemButtons[2].SetEnabled(ink.Owned&&!a.QuickSpent);itemButtons[2].EnableInClassList("item-used",a.QuickItem==3);}
        }
    }
}
