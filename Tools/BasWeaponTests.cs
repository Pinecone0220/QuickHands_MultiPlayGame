using System;
using System.Reflection;
using HelloWorld;
class BasWeaponTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    static DuelDirector Match(DuelWeapon weapon)
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(791));for(int i=0;i<4;i++)d.Join(i,0);
        d.SetAutoSelectItems(true);d.SelectWeapon(0,weapon,0);for(int i=0;i<4;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,0d});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(d,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=15});return d;
    }
    static int Complete(DuelDirector d,ref double now)
    {
        int inputs=0,guard=0;
        while(d.ForSeat(0).Action!=DuelAction.Aim&&guard++<100)
        {var a=d.ForSeat(0);now+=.03;Check(d.Input(0,a.Digit(a.Progress),a.Version,now),"valid BAS action digit accepted");inputs++;}
        Check(d.ForSeat(0).Action==DuelAction.Aim,"BAS preparation reaches aim");return inputs;
    }
    static void Aim(DuelDirector d,int target,DuelHitPart part,double now,out float y,out float p)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(0),out y,out p);
        DuelGunplay.Sway(0,d.ForSeat(0).Weapon,true,now,0,out float sy,out float sp);y=(y-sy+360)%360;p-=sp;
    }
    static void FireAt(DuelDirector d,int target,DuelHitPart part,ref double now)
    {
        now+=.1;Aim(d,target,part,now,out float y,out float p);
        Check(d.Fire(0,y,p,true,d.ForSeat(0).Version,now,now),"server accepts loaded aimed shot");d.Tick(now+.6);now+=.6;
    }
    static void Main()
    {
        Check((int)DuelWeapon.Taser==2&&(int)DuelWeapon.BAS==3,"existing weapon wire IDs preserved");
        Check(WeaponRules.IsSelectable(DuelWeapon.BAS)&&!WeaponRules.IsSelectable(DuelWeapon.Taser)&&!WeaponRules.IsValid((DuelWeapon)255),"selection rejects item/invalid IDs");
        Check(WeaponRules.Capacity(DuelWeapon.BAS)==1,"BAS single cartridge");
        foreach(DuelWeapon w in Enum.GetValues(typeof(DuelWeapon)))Check(DuelGunplay.Spread(w,true)==0,"all ADS has zero random spread");
        Check(DuelGunplay.Spread(DuelWeapon.BAS,false)>60&&DuelGunplay.Spread(DuelWeapon.Revolver,false)>4.5f&&DuelGunplay.Spread(DuelWeapon.SawedOff,false)>6,"hip-fire spreads increased");
        DuelGunplay.Sway(0,DuelWeapon.BAS,true,1,0,out float by,out float bp);DuelGunplay.Sway(0,DuelWeapon.Revolver,true,1,0,out float ry,out float rp);
        Check(Math.Abs(by)>Math.Abs(ry)&&Math.Abs(bp)>Math.Abs(rp),"BAS sway exceeds revolver");
        Check(DuelGunplay.AdsFov(DuelWeapon.BAS)<DuelGunplay.AdsFov(DuelWeapon.Revolver),"scope magnifies more than irons");
        foreach(var part in new[]{DuelHitPart.Body,DuelHitPart.Legs})Check(WeaponRules.Damage(DuelWeapon.BAS,part)==0,"BAS non-head has no damage");
        Check(WeaponRules.LivesLost(DuelWeapon.BAS,DuelHitPart.Head)==2&&WeaponRules.LivesLost(DuelWeapon.Revolver,DuelHitPart.Head)==1,"double loss limited to BAS head");
        foreach(int target in new[]{1,2,3})
        {
            var delta=DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(0);DuelGunplay.Angles(delta,out float y,out float p);var ray=DuelGunplay.Direction(y,p);
            foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff,DuelWeapon.BAS})
            {
                var v=new DuelProjectileVolley(0,0,1,weapon,DuelGunplay.MuzzleOrigin(0,weapon,true,ray),ray,DuelGunplay.Spread(weapon,true),77,0);
                Check(v.Tick(1<<target,1,out var h)&&h.Seat==target&&h.Part==DuelHitPart.Head,"ADS projectile follows sight with no parallax");
            }
        }
        var d=Match(DuelWeapon.BAS);double now=1;
        Check(Complete(d,ref now)==16,"first BAS shot needs four actions of four digits");Check(d.ForSeat(0).Ammo==1,"one round loaded");
        FireAt(d,1,DuelHitPart.Head,ref now);
        Check(d.ForSeat(1).LivesRemaining==1&&d.Game.HealthForSeat(1)==0&&!RouletteRules.Has(d.Game.AliveMask,1)&&RouletteRules.Has(d.PartyAliveMask,1),"head removes two lives and only current mini participation");
        Check(d.ForSeat(0).Kills==1&&d.ForSeat(0).DamageDealt==100,"double life loss is one kill and actual HP damage");
        Check(d.ForSeat(0).Action==DuelAction.OpenBarrel&&d.ForSeat(0).SpentMask==1&&d.ForSeat(0).Ammo==0,"spent round waits for bolt open");
        Check(!d.ForSeat(0).Aiming,"BAS shot forcibly clears ADS");
        var loadedDuringLever=Field<DuelActionState[]>(d,"actions");loadedDuringLever[0].LoadedMask=1;
        d.Look(0,0,0,true,now+.05);Check(!d.ForSeat(0).Aiming,"lever blocks ADS even when a round exists");
        Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,now+.06,now+.06),"lever blocks fire even when a round exists");loadedDuringLever[0].LoadedMask=0;
        int stale=d.ForSeat(0).Version;Check(!d.Fire(0,0,0,true,stale,now,now),"empty reload cannot fire");
        Check(Complete(d,ref now)==12&&d.ForSeat(0).SpentMask==0&&d.ForSeat(0).Ammo==1,"repeat bolt open/load/close clears spent case");
        FireAt(d,2,DuelHitPart.Body,ref now);
        Check(d.Game.HealthForSeat(2)==100&&d.ForSeat(2).LivesRemaining==3&&d.Game.LastHitMask==0&&d.Game.LastDamage==0&&d.ForSeat(2).HitAt<0,"body contact produces no damage/life loss/hit feedback");
        Complete(d,ref now);FireAt(d,2,DuelHitPart.Legs,ref now);
        Check(d.Game.HealthForSeat(2)==100&&d.Game.LastHitMask==0,"leg contact has no damage");
        typeof(DuelDirector).GetMethod("RestoreParty",F).Invoke(d,new object[]{now,true});
        Complete(d,ref now);FireAt(d,1,DuelHitPart.Head,ref now);
        Check(d.ForSeat(1).LivesRemaining==0&&!RouletteRules.Has(d.PartyAliveMask,1),"double loss clamps at zero and permanently eliminates");
        var eye=DuelGunplay.Eye(0);DuelGunplay.Angles(DuelGunplay.Point(3,DuelHitPart.Head)-eye,out float ay,out float ap);var centre=DuelGunplay.Direction(ay,ap);
        var hip=new DuelProjectileVolley(0,0,1,DuelWeapon.SawedOff,eye,centre,12,93,0);hip.Tick(8,1,out var farHit);
        Check(farHit.Seat<0&&DuelGunplay.ShotRange(DuelWeapon.SawedOff,false)==6&&DuelGunplay.ShotRange(DuelWeapon.SawedOff,true)==20,"hip shotgun has shorter actual pellet reach");
        var practice=new DuelDirector(DuelMode.PvP,new Random(18));for(int i=0;i<3;i++)practice.Join(i,0);practice.SetAutoSelectItems(true);practice.SelectWeapon(0,DuelWeapon.BAS,0);practice.Ready(0,0);practice.Ready(1,0);
        int confirms=0;practice.PracticeHitConfirmed+=(a,s,n,h)=>{Check(h,"BAS practice head confirmation classified");confirms++;};
        now=1;for(int i=0;i<3;i++){now+=1;Aim(practice,1,DuelHitPart.Head,now,out float y,out float p);Check(practice.Fire(0,y,p,true,practice.ForSeat(0).Version,now,now),"BAS unlimited practice");practice.Tick(now+.6);now+=.6;
            Check(practice.ForSeat(0).Action==DuelAction.OpenBarrel&&!practice.ForSeat(0).Aiming,"practice BAS shot requires a lever action");
            Check(!practice.Fire(0,y,p,true,practice.ForSeat(0).Version,now,now),"infinite reserve cannot bypass lever");
            practice.Look(0,y,p,true,now);Check(!practice.ForSeat(0).Aiming,"held right click cannot bypass practice lever");
            Check(Complete(practice,ref now)==4,"practice lever is one four-digit action");}
        Check(confirms==3&&practice.Game.HealthForSeat(1)==100&&practice.ForSeat(1).LivesRemaining==3,"practice never applies BAS lethal rules");
        Check(practice.ForSeat(0).CorrectInputs==0&&practice.ForSeat(0).Mistakes==0&&practice.ForSeat(0).ShotsFired==0,"practice lever excluded from real results");
        for(int i=0;i<60;i++)
        {
            now+=1;Check(practice.Fire(0,0,0,false,practice.ForSeat(0).Version,now,now),"BAS hip shot accepted");var a=practice.ForSeat(0);
            DuelGunplay.Sway(0,DuelWeapon.BAS,false,now,0,out float sy,out float sp);
            var muzzle=DuelGunplay.MuzzleOrigin(0,DuelWeapon.BAS,false,DuelGunplay.Direction(sy,sp));
            Check(Math.Abs(a.FiredOrigin.X-muzzle.X)<.0001&&Math.Abs(a.FiredOrigin.Y-muzzle.Y)<.0001&&Math.Abs(a.FiredOrigin.Z-muzzle.Z)<.0001,"extreme spread does not teleport the muzzle");
            double distance=Math.Sqrt(Math.Pow(a.FiredYaw-sy,2)+Math.Pow(a.FiredPitch-sp,2));Check(distance>=34.99&&distance<=70.01,"BAS hip excludes centre of huge spread");practice.Tick(now+.6);now+=.6;Check(Complete(practice,ref now)==4,"each hip shot also requires lever input");
        }
        Console.WriteLine("PASS BAS weapons: "+checks+" checks");
    }
}
