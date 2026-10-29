using UnityEngine;
using UnityEngine.InputSystem;

namespace HelloWorld
{
    // Local mouse presentation; the server receives only angles, never a hit or damage.
    public sealed class DuelFirstPerson : MonoBehaviour
    {
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool Ads { get; private set; }
        public bool Active { get; private set; }
        private int round = -1, seat = -1, pendingVersion = -1;
        private HelloWorldPlayer session;
        private double nextLook, pendingAt, adsStarted;
        private bool captured, sentAds;
        private int life=-1;
        private int pendingInk=-1;private double pendingInkAt;
        private int wheelQuickVersion=-1,wheelInkVersion=-1;
        private double wheelPendingAt;
        public Quaternion OverviewRotation(Quaternion basis)=>Quaternion.AngleAxis(Mathf.DeltaAngle(seat*60,Yaw),Vector3.up)*basis*Quaternion.Euler(-Pitch,0,0);
        private void MoveLook(Mouse mouse)
        {
            if(mouse==null)return;
            var delta=mouse.delta.ReadValue();
            Yaw=DuelGunplay.MoveLookYaw(seat,Yaw,delta.x*.096f*DuelSettings.Sensitivity);
            Pitch=Mathf.Clamp(Pitch+delta.y*.096f*DuelSettings.Sensitivity,-65,65);
        }
        public Quaternion Rotation(double now)
        {
            var p = HelloWorldPlayer.Local;
            if (p == null || !p.IsSpawned || p.Seat.Value < 0)
                return Quaternion.Euler(-Pitch, Yaw, 0);
            var state=p.State.Value;int owner=p.Seat.Value;var own=state.ForSeat(owner);
            bool shooting=state.IsPracticing(owner)||state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&state.Alive(owner)&&
                (state.Clock.Phase==ClockPhase.QuickHands||state.Clock.Phase==ClockPhase.Shooting);
            if(!shooting||!own.IsStunned(now)&&(DuelInk.Busy(own.Ink,now)||own.Action!=DuelAction.Aim&&!(Ads&&own.Weapon==DuelWeapon.Revolver)))
                return Quaternion.Euler(-Pitch,Yaw,0);
            if(own.IsStunned(now))return Quaternion.Euler(-own.AimPitch+Mathf.Sin((float)now*39)*.45f,own.AimYaw+Mathf.Sin((float)now*47)*.6f,Mathf.Sin((float)now*31)*.25f);
            double start = adsStarted;
            if (own.Aiming) start = own.AimStartedAt;
            DuelGunplay.Sway(owner,own.Weapon, Ads, now, start, out float y, out float x);
            return Quaternion.Euler(-Pitch - x, Yaw + y, 0);
        }
        private void Update()
        {
            var p = HelloWorldPlayer.Local;
            bool valid = p != null && p.IsSpawned && p.Seat.Value >= 0;
            var s = valid ? p.State.Value : default;
            bool practice=valid&&s.IsPracticing(p.Seat.Value);
            bool wasActive=Active;
            bool enteredPractice=practice&&!wasActive;
            Active = practice || valid && s.Phase == DuelPhase.Playing && s.Stage == DuelStage.Racing && s.Alive(p.Seat.Value);
            if (!valid) { Ads = false; captured = false; SetCursor(false); return; }
            if (session != p || round != s.Round || seat != p.Seat.Value || life!=s.ForSeat(p.Seat.Value).Life)
            {
                session = p; round = s.Round; seat = p.Seat.Value;
                life=s.ForSeat(seat).Life;
                Yaw = seat * 60; Pitch = 0; captured = true; pendingVersion = -1; nextLook = 0; sentAds = false;
                pendingInk=-1;
                wheelQuickVersion=wheelInkVersion=-1;
            }
            if(wasActive&&!Active){Yaw=seat*60;Pitch=0;}
            if(Active&&!wasActive){captured=true;nextLook=0;}
            if(!practice&&(s.Phase==DuelPhase.Finished||s.Phase==DuelPhase.Playing&&s.ForSeat(seat).LivesRemaining==0))
            {
                Yaw=seat*60;Pitch=0;Ads=false;captured=false;sentAds=false;
                pendingVersion=-1;SetCursor(false);return;
            }
            bool blocked=DuelChat.BlocksGameplay||DuelPointer.BlocksActions;
            bool preparing=s.Phase==DuelPhase.Playing&&s.Clock.Phase==ClockPhase.ItemPreparation;
            var own=s.ForSeat(seat);double time=p.NetworkManager.ServerTime.Time;
            bool recovering=own.DiedAt>=0&&own.LivesRemaining>0&&own.ReviveAt>=0&&time>=own.ReviveAt;
            bool falling=own.DiedAt>=0&&s.Mode==DuelMode.PvP&&time-own.DiedAt<RouletteTable.SpectatorLookDelay;
            SetCursor(true);
            if(blocked)
            {
                Ads=false;if(sentAds){p.Look(Yaw,Pitch,false);sentAds=false;}return;
            }
            if(s.CanPreviewEquipment(seat))
            {
                if(TryEquipmentKey(p,s,false,time))return;
                if(Mouse.current!=null&&TryWheel(p,s,false,Mouse.current.scroll.ReadValue().y,time))return;
            }
            if(!Active)
            {
                Ads=false;if(!recovering&&!falling)MoveLook(Mouse.current);
                bool idleLook=s.Phase==DuelPhase.Waiting&&s.Stage==DuelStage.Idle||s.Phase==DuelPhase.Playing&&s.Stage==DuelStage.Countdown&&s.Alive(seat);
                if(idleLook&&time>=nextLook)
                {p.Look(Yaw,Pitch,false);sentAds=false;nextLook=time+.05;}
                return;
            }
            captured=true;
            var current=s.ForSeat(seat);
            if(current.IsStunned(time)&&(practice||DuelInk.Shooting(s.Clock.Phase))){Yaw=current.AimYaw;Pitch=current.AimPitch;Ads=false;captured=true;SetCursor(true);return;}
            var mouse = Mouse.current;
            bool manualSelecting=!practice&&s.ManualMiniGame&&s.Clock.Phase==ClockPhase.Selecting;
            if(!preparing&&!manualSelecting&&TryEquipmentKey(p,s,practice,time))return;
            if(mouse!=null&&!preparing&&!manualSelecting&&TryWheel(p,s,practice,mouse.scroll.ReadValue().y,time))return;
            MoveLook(mouse);
            var a = s.ForSeat(seat); double now = p.NetworkManager.ServerTime.Time;
            if(pendingInk!=a.Ink.Version||now-pendingInkAt>.75)pendingInk=-1;
            bool inkBusy=DuelInk.Busy(a.Ink,now);
            bool freeRevolverAim=s.Clock.Phase==ClockPhase.QuickHands&&a.Weapon==DuelWeapon.Revolver;
            bool basPending=a.Weapon==DuelWeapon.BAS&&pendingVersion==a.Version&&now-pendingAt<.75;
            bool ads = !inkBusy&&WeaponRules.SupportsAds(a.Weapon) && !preparing && !basPending && !manualSelecting && captured && mouse != null && mouse.rightButton.isPressed && (a.Action==DuelAction.Aim||freeRevolverAim);
            if (ads && !Ads) adsStarted = now;
            Ads = ads;
            if (now >= nextLook || Ads != sentAds)
            { p.Look(Yaw, Pitch, Ads); sentAds = Ads; nextLook = now + .05; }
            if (pendingVersion != a.Version || now - pendingAt > .75) pendingVersion = -1;
            if(inkBusy)
            {
                bool practiceReady=!practice||a.Ink.ThrownAt<0||now-a.Ink.ThrownAt>=DuelInk.ThrowDuration&&
                    (a.Ink.ImpactAt>=a.Ink.ThrownAt||now-a.Ink.ThrownAt>=DuelInk.ReleaseDelay+DuelInk.Lifetime);
                if(a.Ink.Armed&&practiceReady&&(practice||DuelInk.Shooting(s.Clock.Phase))&&captured&&mouse!=null&&mouse.leftButton.wasPressedThisFrame&&pendingInk<0)
                {pendingInk=a.Ink.Version;pendingInkAt=now;p.ThrowInk(Yaw,Pitch,now);}
                return;
            }
            bool shooterDelay=!practice&&s.Clock.Phase==ClockPhase.Shooting&&s.Clock.First==seat&&now<DuelClockContest.ShootingStartsAt(s.Clock);
            if (!preparing && !shooterDelay && !enteredPractice && !manualSelecting && captured && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                WeaponRules.CanFireLoaded(a.Weapon,a.Action,a.Ammo) && pendingVersion < 0)
            {
                pendingVersion = a.Version; pendingAt = now;bool firedAds=Ads;
                if(a.Weapon==DuelWeapon.BAS){Ads=false;sentAds=false;}
                p.Fire(Yaw, Pitch, firedAds, now);
            }
        }
        private bool TryEquipmentKey(HelloWorldPlayer player,DuelSnapshot state,bool practice,double now)
        {
            var keyboard=Keyboard.current;if(keyboard==null)return false;
            var a=state.ForSeat(player.Seat.Value);
            bool enteringCode=!a.QuickEquipped&&!DuelInk.Busy(a.Ink,now)&&
                (practice&&a.Weapon==DuelWeapon.BAS&&a.Action==DuelAction.OpenBarrel||
                 !practice&&state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&state.Clock.Phase==ClockPhase.QuickHands&&state.Alive(player.Seat.Value)&&a.Action!=DuelAction.Aim&&a.Action!=DuelAction.Fire);
            if(enteringCode)return false;
            int slot=keyboard.digit1Key.wasPressedThisFrame?0:keyboard.digit2Key.wasPressedThisFrame?1:keyboard.digit3Key.wasPressedThisFrame?2:-1;
            return slot>=0&&TryWheel(player,state,practice,0,now,(DuelWheelSlot)slot);
        }
        private bool TryWheel(HelloWorldPlayer player,DuelSnapshot state,bool practice,float wheel,double now,DuelWheelSlot? requested=null)
        {
            var a=state.ForSeat(player.Seat.Value);
            if(wheelQuickVersion>=0&&(wheelQuickVersion!=a.QuickItemVersion||now-wheelPendingAt>.75))wheelQuickVersion=-1;
            if(wheelInkVersion>=0&&(wheelInkVersion!=a.Ink.Version||now-wheelPendingAt>.75))wheelInkVersion=-1;
            if(!requested.HasValue&&wheel==0||wheelQuickVersion>=0||wheelInkVersion>=0)return false;
            if(state.CanPreviewEquipment(player.Seat.Value))
            {
                bool lobby=state.Phase==DuelPhase.Waiting;
                var preview=DuelEquipmentWheel.NextPreview(a,lobby,requested.HasValue?1:wheel<0?1:-1);
                if(requested.HasValue&&preview!=requested.Value)preview=DuelEquipmentWheel.NextPreview(a,lobby,-1);
                if(requested.HasValue&&preview!=requested.Value)return false;
                if((int)preview==a.PreviewItem)return false;
                wheelQuickVersion=a.QuickItemVersion;wheelPendingAt=now;player.PreviewEquipment((int)preview);Ads=false;return true;
            }
            bool quick=!practice&&state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&state.Clock.Phase==ClockPhase.QuickHands&&state.Alive(player.Seat.Value);
            bool shooting=!practice&&state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&state.Clock.Phase==ClockPhase.Shooting&&state.Alive(player.Seat.Value);
            var current=DuelEquipmentWheel.Current(a);
            var next=DuelEquipmentWheel.Next(a,practice,quick,shooting,state.NoCooldownFor(player.Seat.Value),state.Clock.Iteration,now,requested.HasValue?1:wheel<0?1:-1);
            if(requested.HasValue&&next!=requested.Value)next=DuelEquipmentWheel.Next(a,practice,quick,shooting,state.NoCooldownFor(player.Seat.Value),state.Clock.Iteration,now,-1);
            if(requested.HasValue&&next!=requested.Value)return false;
            if(next==current)return false;
            if(practice||quick)
            {
                wheelQuickVersion=a.QuickItemVersion;
                if(next==DuelWheelSlot.Weapon)player.SwitchQuickItem(false);
                else if(practice)player.UseItem(next==DuelWheelSlot.Taser?DuelItem.Taser:DuelItem.Ink);
                else player.SwitchQuickItem(true);
            }
            else {wheelInkVersion=a.Ink.Version;player.UseItem(DuelItem.Ink);}
            wheelPendingAt=now;Ads=false;return true;
        }
        private static void SetCursor(bool locked)
        {
            DuelPointer.ApplyCursor();
        }
        private void OnDisable() { SetCursor(false); }
    }
}
