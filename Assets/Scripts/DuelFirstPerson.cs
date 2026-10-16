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
        private bool itemPreparing;
        private int clockIteration=-1;
        private int life=-1;
        public Quaternion Rotation(double now)
        {
            var p = HelloWorldPlayer.Local;
            if (p == null || !p.IsSpawned || p.Seat.Value < 0 ||
                (!p.State.Value.ForSeat(p.Seat.Value).Stunned&&p.State.Value.ForSeat(p.Seat.Value).Action != DuelAction.Aim && !(Ads&&p.State.Value.ForSeat(p.Seat.Value).Weapon==DuelWeapon.Revolver)))
                return Quaternion.Euler(-Pitch, Yaw, 0);
            var own=p.State.Value.ForSeat(p.Seat.Value);
            if(own.Stunned)return Quaternion.Euler(-own.AimPitch+Mathf.Sin((float)now*39)*.45f,own.AimYaw+Mathf.Sin((float)now*47)*.6f,Mathf.Sin((float)now*31)*.25f);
            double start = adsStarted;
            if (p != null && p.IsSpawned && p.Seat.Value >= 0 && p.State.Value.ForSeat(p.Seat.Value).Aiming)
                start = p.State.Value.ForSeat(p.Seat.Value).AimStartedAt;
            DuelGunplay.Sway(seat,p.State.Value.ForSeat(p.Seat.Value).Weapon, Ads, now, start, out float y, out float x);
            return Quaternion.Euler(-Pitch - x, Yaw + y, 0);
        }
        private void Update()
        {
            var p = HelloWorldPlayer.Local;
            bool valid = p != null && p.IsSpawned && p.Seat.Value >= 0;
            var s = valid ? p.State.Value : default;
            Active = valid && s.Phase == DuelPhase.Playing && s.Stage == DuelStage.Racing && s.Alive(p.Seat.Value);
            if (!Active) { Ads = false; captured = false; itemPreparing=false; SetCursor(false); return; }
            if (session != p || round != s.Round || seat != p.Seat.Value || life!=s.ForSeat(p.Seat.Value).Life)
            {
                session = p; round = s.Round; seat = p.Seat.Value;
                life=s.ForSeat(seat).Life;
                Yaw = seat * 60; Pitch = 0; captured = true; pendingVersion = -1; nextLook = 0; sentAds = false;
                clockIteration=-1;
            }
            if(s.Clock.Phase==ClockPhase.ItemPreparation || s.ManualMiniGame&&s.Clock.Phase==ClockPhase.Selecting)
            {itemPreparing=true;Ads=false;sentAds=false;captured=false;SetCursor(false);return;}
            bool resumedFromItems=itemPreparing;
            if(itemPreparing){itemPreparing=false;captured=true;nextLook=0;pendingVersion=-1;}
            if(s.Clock.MiniGame!=PartyMiniGame.QuickHands && s.Clock.Phase==ClockPhase.Shooting && (s.Clock.First==seat||s.ForSeat(seat).TaserArmed) && clockIteration!=s.Clock.Iteration)
            { var aim=s.ForSeat(seat);Yaw=aim.AimYaw;Pitch=aim.AimPitch;clockIteration=s.Clock.Iteration; }
            var current=s.ForSeat(seat);
            if(current.Stunned){Yaw=current.AimYaw;Pitch=current.AimPitch;Ads=false;captured=true;SetCursor(true);return;}
            var mouse = Mouse.current;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) captured = false;
            // Click the scene to recapture. That first click cannot also shoot.
            bool recapture = !captured && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                mouse.position.ReadValue().y > 150 && mouse.position.ReadValue().x < Screen.width - 240;
            if (recapture) captured = true;
            SetCursor(captured);
            if (captured && !resumedFromItems && mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                Yaw = Mathf.Repeat(Yaw + delta.x * .096f, 360);
                Pitch = Mathf.Clamp(Pitch + delta.y * .096f, -65, 65);
            }
            var a = s.ForSeat(seat); double now = p.NetworkManager.ServerTime.Time;
            bool freeRevolverAim=s.Clock.Phase==ClockPhase.QuickHands&&a.Weapon==DuelWeapon.Revolver;
            bool ads = captured && mouse != null && mouse.rightButton.isPressed && (a.Action==DuelAction.Aim||freeRevolverAim);
            if (ads && !Ads) adsStarted = now;
            Ads = ads;
            if (now >= nextLook || Ads != sentAds)
            { p.Look(Yaw, Pitch, Ads); sentAds = Ads; nextLook = now + .05; }
            if (pendingVersion != a.Version || now - pendingAt > .75) pendingVersion = -1;
            if (captured && !recapture && !resumedFromItems && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (a.Action==DuelAction.Aim||freeRevolverAim&&(Ads||a.SpentMask!=0)) && a.Ammo>0 && pendingVersion < 0)
            { pendingVersion = a.Version; pendingAt = now; p.Fire(Yaw, Pitch, Ads, now); }
        }
        private static void SetCursor(bool locked)
        {
            var mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            Cursor.visible = !locked;
        }
        private void OnDisable() { SetCursor(false); }
    }
}
