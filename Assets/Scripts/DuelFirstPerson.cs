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
        private int clockIteration=-1;
        private int life=-1;
        private int pendingInk=-1;private double pendingInkAt;
        public Quaternion OverviewRotation(Quaternion basis)=>Quaternion.AngleAxis(Mathf.DeltaAngle(seat*60,Yaw),Vector3.up)*basis*Quaternion.Euler(-Pitch,0,0);
        private void MoveLook(Mouse mouse)
        {
            if(mouse==null)return;
            var delta=mouse.delta.ReadValue();
            Yaw=DuelGunplay.MoveLookYaw(seat,Yaw,delta.x*.096f);
            Pitch=Mathf.Clamp(Pitch+delta.y*.096f,-65,65);
        }
        public Quaternion Rotation(double now)
        {
            var p = HelloWorldPlayer.Local;
            if (p == null || !p.IsSpawned || p.Seat.Value < 0)
                return Quaternion.Euler(-Pitch, Yaw, 0);
            var state=p.State.Value;int owner=p.Seat.Value;var own=state.ForSeat(owner);
            bool shooting=state.IsPracticing(owner)||state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&state.Alive(owner)&&
                (state.Clock.Phase==ClockPhase.QuickHands||state.Clock.Phase==ClockPhase.Shooting);
            if(!shooting||!own.Stunned&&(DuelInk.Busy(own.Ink,now)||own.Action!=DuelAction.Aim&&!(Ads&&own.Weapon==DuelWeapon.Revolver)))
                return Quaternion.Euler(-Pitch,Yaw,0);
            if(own.Stunned)return Quaternion.Euler(-own.AimPitch+Mathf.Sin((float)now*39)*.45f,own.AimYaw+Mathf.Sin((float)now*47)*.6f,Mathf.Sin((float)now*31)*.25f);
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
                clockIteration=-1;pendingInk=-1;
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
            if(!Active)
            {
                Ads=false;if(!recovering&&!falling)MoveLook(Mouse.current);return;
            }
            captured=true;
            if(!practice&&s.Clock.MiniGame!=PartyMiniGame.QuickHands&&s.Clock.Phase==ClockPhase.Shooting&&(s.Clock.First==seat||own.TaserArmed)&&clockIteration!=s.Clock.Iteration)
            {Yaw=own.AimYaw;Pitch=own.AimPitch;clockIteration=s.Clock.Iteration;}
            var current=s.ForSeat(seat);
            if(current.Stunned){Yaw=current.AimYaw;Pitch=current.AimPitch;Ads=false;captured=true;SetCursor(true);return;}
            var mouse = Mouse.current;
            bool manualSelecting=!practice&&s.ManualMiniGame&&s.Clock.Phase==ClockPhase.Selecting;
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
                if(a.Ink.Armed&&DuelInk.Shooting(s.Clock.Phase)&&captured&&mouse!=null&&mouse.leftButton.wasPressedThisFrame&&pendingInk<0)
                {pendingInk=a.Ink.Version;pendingInkAt=now;p.ThrowInk(Yaw,Pitch,now);}
                return;
            }
            if (!preparing && !enteredPractice && !manualSelecting && captured && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (a.Action==DuelAction.Aim||freeRevolverAim&&(Ads||a.SpentMask!=0)) && a.Ammo>0 && pendingVersion < 0)
            {
                pendingVersion = a.Version; pendingAt = now;bool firedAds=Ads;
                if(a.Weapon==DuelWeapon.BAS){Ads=false;sentAds=false;}
                p.Fire(Yaw, Pitch, firedAds, now);
            }
        }
        private static void SetCursor(bool locked)
        {
            DuelPointer.ApplyCursor();
        }
        private void OnDisable() { SetCursor(false); }
    }
}
