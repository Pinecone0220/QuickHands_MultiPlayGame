using System;
using System.Collections.Generic;

namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        private readonly List<DuelProjectileVolley> practiceProjectiles=new List<DuelProjectileVolley>();
        public int PracticeMask=>Stage==DuelStage.Idle&&Game.Phase!=DuelPhase.Playing?Game.ConnectedMask&Game.ReadyMask:0;
        public bool IsPracticing(int seat)=>RouletteRules.Has(PracticeMask,seat);
        public event Action<int,int,int,bool> PracticeHitConfirmed;

        private void ChangePracticeSession(int seat,double now)
        {
            var a=actions[seat];a.PracticeSession++;a.Version++;
            a.Weapon=selectedWeapons[seat];a.Action=DuelAction.Aim;a.Progress=0;a.SpentMask=0;a.Aiming=false;a.Target=-1;a.Stunned=a.TaserArmed=false;
            a.FiredAt=a.HitAt=-1;a.AimStartedAt=now;actions[seat]=a;lastLook[seat]=now-.1;
        }
        private void InvalidatePracticeFlights()
        {
            // Roster changes must not let an old flight hit a reused seat.
            // Keep existing players' ready state, aim and reload progress.
            practiceProjectiles.Clear();
        }
        private bool FirePractice(int seat,float yaw,float pitch,bool ads,int version,double shotTime,double now)
        {
            var a=actions[seat];var weapon=selectedWeapons[seat];
            ads&=WeaponRules.SupportsAds(weapon);
            double interval=weapon==DuelWeapon.BAS?.8:weapon==DuelWeapon.SawedOff?.30:.16;
            if(!WeaponRules.IsSelectable(weapon)||weapon==DuelWeapon.BAS&&a.Action!=DuelAction.Aim||version!=a.Version||!DuelGunplay.ValidSeatAngles(seat,yaw,pitch)||
                double.IsNaN(shotTime)||double.IsInfinity(shotTime)||shotTime<now-.3||shotTime>now+.05||
                a.FiredAt>=0&&now-a.FiredAt<interval||practiceProjectiles.Count>=32)return false;
            ApplyLook(seat,yaw,pitch,ads,now);a=actions[seat];
            DuelGunplay.Sway(seat,weapon,ads,shotTime,a.AimStartedAt,out float sy,out float sp);
            float firedYaw=yaw+sy,firedPitch=pitch+sp,spread=DuelGunplay.Spread(weapon,ads);
            var bore=DuelGunplay.Direction(firedYaw,firedPitch);
            if(WeaponRules.IsPrecision(weapon))
            {
                double angle=random.NextDouble()*Math.PI*2;float radius=DuelGunplay.PrecisionSpreadRadius(weapon,ads,random.NextDouble());
                firedYaw+=(float)Math.Cos(angle)*radius;firedPitch+=(float)Math.Sin(angle)*radius;spread=0;
            }
            var ray=DuelGunplay.Direction(firedYaw,firedPitch);var origin=DuelGunplay.MuzzleOrigin(seat,weapon,ads,bore);
            int seed=weapon==DuelWeapon.SawedOff?random.Next(int.MaxValue):0;
            a.PracticeShotsFired++;a.Version++;a.FiredAt=now;a.FiredTarget=-1;a.FiredWeapon=weapon;
            a.FiredYaw=firedYaw;a.FiredPitch=firedPitch;a.FiredSpread=spread;a.FiredSeed=seed;
            a.FiredAliveMask=PracticeMask;a.FiredOrigin=origin;a.FiredAds=ads;
            a.FiredBottleMask=tabletop.State.BottlesBrokenMask;a.FiredGlassMask=tabletop.State.GlassesBrokenMask;
            a.NextChamber=(a.NextChamber+1)%WeaponRules.Capacity(weapon);actions[seat]=a;
            if(weapon==DuelWeapon.BAS){actions[seat].SpentMask=1;ResetAction(seat,DuelAction.OpenBarrel,now);}
            practiceProjectiles.Add(new DuelProjectileVolley(seat,a.PracticeSession,clock.State.Iteration,weapon,
                origin,ray,spread,seed,now,PracticeMask,a.PracticeShotsFired));
            Sequence++;return true;
        }
        private bool InputPracticeLever(int seat,int digit,int version,double now)
        {
            var a=actions[seat];
            if(a.Weapon!=DuelWeapon.BAS||a.Action!=DuelAction.OpenBarrel||digit<1||digit>5||a.Version!=version||now<a.BlockedUntil)return false;
            a.Version++;
            if(digit!=a.Digit(a.Progress)){a.Progress=0;a.BlockedUntil=now+MistakePenalty;}
            else a.Progress++;
            actions[seat]=a;
            if(a.Progress==a.Length){actions[seat].SpentMask=0;ResetAction(seat,DuelAction.Aim,now);}
            Sequence++;return true;
        }
        private bool TickPractice(double now)
        {
            int eligible=PracticeMask;
            if(eligible==0){practiceProjectiles.Clear();return false;}
            bool changed=false;
            for(int i=practiceProjectiles.Count-1;i>=0;i--)
            {
                var shot=practiceProjectiles[i];
                if(!RouletteRules.Has(eligible,shot.Actor)||shot.Life!=actions[shot.Actor].PracticeSession)
                {practiceProjectiles.RemoveAt(i);continue;}
                int targets=eligible&shot.EligibleMask;
                // Props still block shots, but warm-up cannot break persistent table decorations.
                if(!shot.Tick(targets,now,out var hit,null,tabletop.State))continue;
                practiceProjectiles.RemoveAt(i);
                if(!RouletteRules.Has(targets,hit.Seat)||WeaponRules.Damage(shot.Weapon,hit.Part)==0)continue;
                actions[hit.Seat].HitAt=now;
                if(shot.Weapon==DuelWeapon.SawedOff)
                    for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                        if(seat!=shot.Actor&&RouletteRules.Has(targets,seat)&&WeaponRules.AreAdjacent(hit.Seat,seat))actions[seat].HitAt=now;
                actions[shot.Actor].FiredTarget=hit.Seat;Sequence++;changed=true;
                PracticeHitConfirmed?.Invoke(shot.Actor,shot.Life,shot.Sequence,WeaponRules.IsPrecision(shot.Weapon)&&hit.Part==DuelHitPart.Head);
            }
            return changed;
        }
    }
}
