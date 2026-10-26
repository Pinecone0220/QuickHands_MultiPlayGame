using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement inkOverlay;
        private Texture2D inkTexture;
        private void BuildInkOverlay()
        {
            inkOverlay=Box(root,"ink-effect");inkOverlay.pickingMode=PickingMode.Ignore;
            const int size=512;inkTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ink pouch splash",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];var random=new System.Random(89);
            var blobs=new Vector3[23];blobs[0]=new Vector3(252,255,82);blobs[1]=new Vector3(192,243,61);blobs[2]=new Vector3(315,260,68);blobs[3]=new Vector3(260,313,57);blobs[4]=new Vector3(229,190,49);
            for(int i=5;i<blobs.Length;i++){float angle=(float)(random.NextDouble()*Mathf.PI*2),r=105+(float)random.NextDouble()*75;blobs[i]=new Vector3(256+Mathf.Cos(angle)*r,256+Mathf.Sin(angle)*r,4+(float)random.NextDouble()*12);}
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float alpha=0;
                foreach(var b in blobs){float d=Vector2.Distance(new Vector2(x,y),new Vector2(b.x,b.y));float edge=b.z*(1+.06f*Mathf.Sin(x*.19f+y*.17f));alpha=Mathf.Max(alpha,Mathf.Clamp01((edge-d)/2));}
                pixels[y*size+x]=new Color(.018f,.009f,.029f,alpha*.98f);
            }
            inkTexture.SetPixels(pixels);inkTexture.Apply(false,true);inkOverlay.style.backgroundImage=new StyleBackground(inkTexture);
            inkOverlay.SendToBack();Show(inkOverlay,false);
        }
        private void UpdateInkOverlay(HelloWorldPlayer player)
        {
            if(inkOverlay==null)return;
            float opacity=0;
            if(player!=null&&player.IsSpawned&&player.Seat.Value>=0&&player.State.Value.Phase==DuelPhase.Playing&&player.State.Value.Alive(player.Seat.Value))
            {
                var ink=player.State.Value.ForSeat(player.Seat.Value).Ink;
                if(ink.InkedAt>=0)opacity=DuelInk.Opacity(player.NetworkManager.ServerTime.Time-ink.InkedAt);
            }
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
