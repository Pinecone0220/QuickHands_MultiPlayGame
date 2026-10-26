using UnityEngine;
using UnityEngine.InputSystem;
namespace HelloWorld
{
    // Camera-only selection. Never sends aim, input or fire requests for the watched player.
    [DefaultExecutionOrder(-100)]
    public sealed class DuelSpectator:MonoBehaviour
    {
        public bool Active {get;private set;}
        public int TargetSeat {get;private set;}=-1;
        public bool Watching=>Active&&TargetSeat>=0;
        public float Yaw {get;private set;}
        public float Pitch {get;private set;}
        private HelloWorldPlayer session;private int round=-1,lookSeat=-1;private double death=-1;
        public static bool CanSpectate(DuelSnapshot s,int owner,double now)
        {
            if(owner<0||owner>=RouletteRules.MaxPlayers||!s.Connected(owner)||s.Phase!=DuelPhase.Playing)return false;
            var a=s.ForSeat(owner);return a.LivesRemaining==0&&a.DiedAt>=0&&now-a.DiedAt>=RouletteTable.SpectatorTransitionStart(0)+.4f;
        }
        public static int EligibleMask(DuelSnapshot s,int owner)
        {
            int mask=0;for(int i=0;i<RouletteRules.MaxPlayers;i++)if(i!=owner&&s.Connected(i)&&s.Alive(i)&&s.ForSeat(i).LivesRemaining>0&&s.ForSeat(i).DiedAt<0)mask|=1<<i;return mask;
        }
        public static int NextSeat(int mask,int current,int direction)
        {
            if(direction==0)return current>=0&&RouletteRules.Has(mask,current)?current:-1;
            int start=current<0?0:current+1;
            for(int step=1;step<=7;step++){int slot=(start+(direction>0?step:-step)+14)%7;if(slot==0)return -1;if(RouletteRules.Has(mask,slot-1))return slot-1;}
            return -1;
        }
        internal bool Cycle(int direction)
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||!CanSpectate(p.State.Value,p.Seat.Value,p.NetworkManager.ServerTime.Time)||DuelChat.BlocksGameplay||DuelPointer.BlocksActions)return false;
            int next=NextSeat(EligibleMask(p.State.Value,p.Seat.Value),TargetSeat,direction);bool changed=next!=TargetSeat;TargetSeat=next;if(changed)lookSeat=-1;return changed;
        }
        private void Update()
        {
            var p=HelloWorldPlayer.Local;
            if(p==null||!p.IsSpawned||p.Seat.Value<0){Active=false;TargetSeat=lookSeat=-1;session=null;return;}
            var s=p.State.Value;var own=s.ForSeat(p.Seat.Value);
            if(session!=p||round!=s.Round||death!=own.DiedAt){session=p;round=s.Round;death=own.DiedAt;TargetSeat=lookSeat=-1;}
            Active=CanSpectate(s,p.Seat.Value,p.NetworkManager.ServerTime.Time);
            if(!Active){TargetSeat=lookSeat=-1;return;}
            if(TargetSeat>=0&&!RouletteRules.Has(EligibleMask(s,p.Seat.Value),TargetSeat))TargetSeat=lookSeat=-1;
            var keyboard=Keyboard.current;
            if(keyboard!=null&&!DuelChat.BlocksGameplay&&!DuelPointer.BlocksActions)
            {if(keyboard.qKey.wasPressedThisFrame&&!keyboard.eKey.wasPressedThisFrame)Cycle(-1);else if(keyboard.eKey.wasPressedThisFrame&&!keyboard.qKey.wasPressedThisFrame)Cycle(1);}
            if(!Watching)return;var a=s.ForSeat(TargetSeat);
            float blend=lookSeat!=TargetSeat?1:1-Mathf.Exp(-25*Time.unscaledDeltaTime);
            Yaw=Mathf.LerpAngle(Yaw,a.AimYaw,blend);Pitch=Mathf.Lerp(Pitch,a.AimPitch,blend);lookSeat=TargetSeat;
        }
        public bool Ads(DuelSnapshot s)=>Watching&&s.ForSeat(TargetSeat).Aiming;
        public Quaternion Rotation(DuelSnapshot s,double now)
        {
            var a=s.ForSeat(TargetSeat);bool shooting=s.Stage==DuelStage.Racing&&DuelInk.Shooting(s.Clock.Phase);
            if(!shooting||!a.IsStunned(now)&&(DuelInk.Busy(a.Ink,now)||a.Action!=DuelAction.Aim&&!(a.Aiming&&a.Weapon==DuelWeapon.Revolver)))return Quaternion.Euler(-Pitch,Yaw,0);
            if(a.IsStunned(now))return Quaternion.Euler(-a.AimPitch+Mathf.Sin((float)now*39)*.45f,a.AimYaw+Mathf.Sin((float)now*47)*.6f,Mathf.Sin((float)now*31)*.25f);
            DuelGunplay.Sway(TargetSeat,a.Weapon,a.Aiming,now,a.AimStartedAt,out float y,out float x);return Quaternion.Euler(-Pitch-x,Yaw+y,0);
        }
    }
}
