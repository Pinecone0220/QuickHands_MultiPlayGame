using System;
using System.Collections.Generic;

namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        private readonly List<DuelProjectileVolley> practiceProjectiles=new List<DuelProjectileVolley>();
        private readonly DuelInkFlight[] practiceInkFlights=new DuelInkFlight[RouletteRules.MaxPlayers];
        private readonly int[] practiceInkTargets=new int[RouletteRules.MaxPlayers];
        public int PracticeMask=>Stage==DuelStage.Idle&&Game.Phase!=DuelPhase.Playing?Game.ConnectedMask&Game.ReadyMask:0;
        public bool IsPracticing(int seat)=>RouletteRules.Has(PracticeMask,seat);
        public event Action<int,int,int,bool> PracticeHitConfirmed;

        private void ChangePracticeSession(int seat,double now)
        {
            var a=actions[seat];a.PracticeSession++;a.Version++;
            a.Weapon=selectedWeapons[seat];a.Action=DuelAction.Aim;a.Progress=0;a.SpentMask=0;a.Aiming=false;a.Target=-1;a.Stunned=a.TaserArmed=false;
            a.StunnedAt=a.HealAt=-1;a.NoruFaceActive=a.BulletUsed=false;
            a.QuickEquipped=a.QuickSpent=false;a.QuickItem=0;a.QuickItemVersion++;
            bool owned=a.Ink.Owned;int serial=a.Ink.Serial,version=a.Ink.Version+1;
            a.Ink=DuelInkState.Empty;a.Ink.Owned=owned;a.Ink.Serial=serial;a.Ink.Version=version;
            practiceInkFlights[seat]=null;
            a.FiredAt=a.HitAt=-1;a.AimStartedAt=now;actions[seat]=a;lastLook[seat]=now-.1;
        }
        private void InvalidatePracticeFlights()
        {
            // Roster changes must not let an old flight hit a reused seat.
            // Keep existing players' ready state, aim and reload progress.
            practiceProjectiles.Clear();
            Array.Clear(practiceInkFlights,0,practiceInkFlights.Length);
        }
        private bool UsePracticeItem(int seat,DuelItem item,int iteration,double now)
        {
            if(!IsPracticing(seat)||iteration!=clock.State.Iteration||!DuelLoadout.Has(ForSeat(seat).SelectedItems,item)||actions[seat].IsStunned(now))return false;
            var a=actions[seat];
            if(item==DuelItem.Recovery){a.HealAt=now;actions[seat]=a;Sequence++;return true;}
            if(item==DuelItem.NoruFace){a.NoruFaceActive=!a.NoruFaceActive;actions[seat]=a;Sequence++;return true;}
            if(item==DuelItem.Bullet){a.BulletUsed=!a.BulletUsed;actions[seat]=a;Sequence++;return true;}
            if(item!=DuelItem.Taser&&item!=DuelItem.Ink)return false;
            bool wasEquipped=a.QuickEquipped&&a.QuickItem==(int)item+1;
            if(!wasEquipped&&(DuelInk.Busy(a.Ink,now)&&!a.Ink.Armed||item==DuelItem.Ink&&practiceInkFlights[seat]!=null))return false;
            HolsterQuickItem(seat);a=actions[seat];
            if(wasEquipped){a.QuickItem=0;a.QuickItemVersion++;actions[seat]=a;Sequence++;return true;}
            a.QuickItem=(int)item+1;a.QuickEquipped=true;a.QuickSpent=false;a.QuickItemVersion++;
            if(item==DuelItem.Taser)
            {
                holsteredGuns[seat]=a;a.Weapon=DuelWeapon.Taser;a.Action=DuelAction.Aim;
                a.LoadedMask=1;a.SpentMask=0;a.Progress=0;a.Version++;
            }
            else {a.Ink.Armed=true;a.Ink.Version++;}
            a.Aiming=false;a.Target=-1;actions[seat]=a;Sequence++;return true;
        }
        private bool ThrowPracticeInk(int seat,float yaw,float pitch,int iteration,int life,int version,double sample,double now)
        {
            if(!IsPracticing(seat)||iteration!=clock.State.Iteration||life!=actions[seat].Life||actions[seat].IsStunned(now)||
                !DuelGunplay.ValidSeatAngles(seat,yaw,pitch)||double.IsNaN(sample)||double.IsInfinity(sample)||sample<now-.3||sample>now+.05)return false;
            var a=actions[seat];var ink=a.Ink;
            if(a.QuickItem!=3||!a.QuickEquipped||!ink.Armed||ink.Version!=version||practiceInkFlights[seat]!=null||
                ink.ThrownAt>=0&&now-ink.ThrownAt<DuelInk.ThrowDuration)return false;
            ink.Version++;ink.Serial++;ink.ThrownAt=now;ink.ImpactAt=-1;ink.Target=-1;
            ink.Origin=DuelInk.ThrowOrigin(seat,yaw,pitch);ink.Direction=DuelGunplay.Direction(yaw,pitch);
            a.Ink=ink;a.QuickItemVersion++;a.AimYaw=yaw;a.AimPitch=pitch;a.Aiming=false;a.Target=-1;
            actions[seat]=a;practiceInkTargets[seat]=PracticeMask;
            practiceInkFlights[seat]=new DuelInkFlight(seat,a.PracticeSession,iteration,ink);Sequence++;return true;
        }
        private bool FirePractice(int seat,float yaw,float pitch,bool ads,int version,double shotTime,double now)
        {
            var a=actions[seat];var weapon=a.Weapon;
            ads&=WeaponRules.SupportsAds(weapon);
            double interval=weapon==DuelWeapon.BAS?.8:weapon==DuelWeapon.SawedOff?.30:.16;
            if(!WeaponRules.IsValid(weapon)||a.IsStunned(now)||weapon==DuelWeapon.BAS&&a.Action!=DuelAction.Aim||version!=a.Version||!DuelGunplay.ValidSeatAngles(seat,yaw,pitch)||
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
            var volley=new DuelProjectileVolley(seat,a.PracticeSession,clock.State.Iteration,weapon,origin,ray,spread,seed,now,PracticeMask,a.PracticeShotsFired);
            if(WeaponRules.IsHitscan(weapon))ResolvePracticeHit(volley,weapon==DuelWeapon.Taser?DuelGunplay.TraceHitscan(seat,PracticeMask,origin,ray,tabletop.State):DuelGunplay.TraceRifleHitscan(seat,PracticeMask,origin,ray,tabletop.State),PracticeMask,now);
            else practiceProjectiles.Add(volley);
            Sequence++;return true;
        }
        private bool ResolvePracticeHit(DuelProjectileVolley shot,DuelRayHit hit,int targets,double now)
        {
            if(!RouletteRules.Has(targets,hit.Seat)||WeaponRules.Damage(shot.Weapon,hit.Part)==0||shot.Weapon==DuelWeapon.Taser&&actions[hit.Seat].NoruFaceActive)return false;
            actions[hit.Seat].HitAt=now;
            if(shot.Weapon==DuelWeapon.Taser)
            {HolsterQuickItem(hit.Seat);actions[hit.Seat].Stunned=true;actions[hit.Seat].StunnedAt=now;actions[hit.Seat].TaserEffectSkin=EffectSkinFor(shot.Actor,DuelItem.Taser);actions[hit.Seat].Aiming=false;actions[hit.Seat].Target=-1;}
            if(shot.Weapon==DuelWeapon.SawedOff)
                for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                    if(seat!=shot.Actor&&RouletteRules.Has(targets,seat)&&WeaponRules.AreAdjacent(hit.Seat,seat))actions[seat].HitAt=now;
            actions[shot.Actor].FiredTarget=hit.Seat;Sequence++;
            PracticeHitConfirmed?.Invoke(shot.Actor,shot.Life,shot.Sequence,WeaponRules.IsPrecision(shot.Weapon)&&hit.Part==DuelHitPart.Head);return true;
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
            if(eligible==0){practiceProjectiles.Clear();Array.Clear(practiceInkFlights,0,practiceInkFlights.Length);return false;}
            bool changed=false;
            for(int i=0;i<practiceInkFlights.Length;i++)
            {
                var flight=practiceInkFlights[i];if(flight==null)continue;
                if(!RouletteRules.Has(eligible,i)||flight.Life!=actions[i].PracticeSession){practiceInkFlights[i]=null;continue;}
                int targets=eligible&practiceInkTargets[i];
                if(!flight.Tick(targets,now,tabletop.State,out var hit,out var point))continue;
                practiceInkFlights[i]=null;actions[i].Ink.ImpactAt=now;actions[i].Ink.Impact=point;actions[i].Ink.Target=hit.Seat;
                if(RouletteRules.Has(targets,hit.Seat)){actions[hit.Seat].Ink.InkedAt=now;actions[hit.Seat].InkEffectSkin=EffectSkinFor(i,DuelItem.Ink);}
                Sequence++;changed=true;
            }
            for(int i=practiceProjectiles.Count-1;i>=0;i--)
            {
                var shot=practiceProjectiles[i];
                if(!RouletteRules.Has(eligible,shot.Actor)||shot.Life!=actions[shot.Actor].PracticeSession)
                {practiceProjectiles.RemoveAt(i);continue;}
                int targets=eligible&shot.EligibleMask;
                // Props still block shots, but warm-up cannot break persistent table decorations.
                if(!shot.Tick(targets,now,out var hit,null,tabletop.State))continue;
                practiceProjectiles.RemoveAt(i);
                changed|=ResolvePracticeHit(shot,hit,targets,now);
            }
            return changed;
        }
    }
}
