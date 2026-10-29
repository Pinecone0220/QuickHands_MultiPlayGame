using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement inkOverlay;
        private Texture2D inkTexture;
        private DuelInkFade inkFade;
        private int inkViewSeat=-1,inkEffectSkin; private double inkEffectStamp=-1;
        private readonly Texture2D[] rareInkEffects=new Texture2D[DuelSkins.Colors+1];
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
            inkTexture=MakeInkEffectTexture(0);inkOverlay.style.backgroundImage=new StyleBackground(inkTexture);
            inkOverlay.SendToBack();Show(inkOverlay,false);
        }
        private static Texture2D MakeInkEffectTexture(int skin)
        {
            const int size=512;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ink pouch splash",wrapMode=TextureWrapMode.Clamp};var pixels=new Color[size*size];
            Color color=skin>0?DuelSkins.Tint(skin)*.45f:new Color(.018f,.009f,.029f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2-1,ny=(y+.5f)/size*2-1;
                float radius=Mathf.Pow(Mathf.Pow(Mathf.Abs(nx)/1.11f,4)+Mathf.Pow(Mathf.Abs(ny)/1.11f,4),.25f);
                float edge=1+.035f*Mathf.Sin(x*.09f+y*.06f)+.025f*Mathf.Cos(y*.10f-x*.02f);
                pixels[y*size+x]=new Color(color.r,color.g,color.b,Mathf.Clamp01((edge-radius)*40)*.995f);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private Texture2D InkEffectTexture(int skin)
        {
            if(skin==0)return inkTexture;
            if(rareInkEffects[skin]==null)rareInkEffects[skin]=MakeInkEffectTexture(skin);return rareInkEffects[skin];
        }
        private void DestroyRareInkTextures(){foreach(var texture in rareInkEffects)if(texture!=null)Destroy(texture);}
        private void UpdateInkOverlay(HelloWorldPlayer player)
        {
            if(inkOverlay==null)return;
            float opacity=0;
            int seat=player!=null&&player.IsSpawned?(spectator!=null&&spectator.Watching?spectator.TargetSeat:player.Seat.Value):-1;
            if(inkViewSeat!=seat){inkViewSeat=seat;inkFade=default;inkEffectStamp=-1;inkEffectSkin=0;}
            if(player!=null&&player.IsSpawned&&seat>=0&&(player.State.Value.IsPracticing(seat)||player.State.Value.Alive(seat)))
            {
                var s=player.State.Value;var ink=s.ForSeat(seat).Ink;
                inkEffectStamp=ink.InkedAt;inkEffectSkin=DuelSkinEffects.SourceSkin(s,seat,DuelItem.Ink);
                inkOverlay.style.backgroundImage=new StyleBackground(InkEffectTexture(inkEffectSkin));
                bool practice=s.IsPracticing(seat);
                opacity=inkFade.Observe(ink,player.NetworkManager.ServerTime.Time,practice?DuelPhase.Playing:s.Phase,practice?ClockPhase.Shooting:s.Clock.Phase,s.Clock.PhaseAt,s.StageStartedAt);
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
            itemButtons[2].SetEnabled(ink.Owned&&!p.State.Value.ForSeat(p.Seat.Value).Stunned&&(itemTime||p.State.Value.ForSeat(p.Seat.Value).PreparedItem==(int)DuelItem.Ink+1)&&(ink.Armed||resolved&&!DuelInk.Busy(ink,now)));
            itemButtons[2].EnableInClassList("item-used",ink.Armed);
            var a=p.State.Value.ForSeat(p.Seat.Value);
            if(itemTime&&p.State.Value.Clock.MiniGame==PartyMiniGame.QuickHands)
            {itemButtons[2].SetEnabled(ink.Owned&&!a.QuickSpent);itemButtons[2].EnableInClassList("item-used",a.QuickItem==3);}
        }
    }
}
