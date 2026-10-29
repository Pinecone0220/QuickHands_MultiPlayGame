using System;
using System.Reflection;
using HelloWorld;
public static class WinchesterHitscanTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static DuelDirector Party(int seed=67)
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(seed));
        for(int i=0;i<3;i++){Check(d.Join(i,0),"join");Check(d.SelectItem(i,DuelItem.Recovery)&&d.SelectItem(i,DuelItem.Taser),"loadout");if(i>0)Check(d.Ready(i,0),"guest ready");}
        Check(d.SelectWeapon(0,DuelWeapon.BAS,0),"winchester chosen");Check(d.StartParty(0,1),"host starts");
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,2d});
        var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
        typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=d.Game.AliveMask,PhaseAt=2,StartedAt=2});
        var actions=(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(d);actions[0].Weapon=DuelWeapon.BAS;actions[0].Action=DuelAction.Aim;actions[0].LoadedMask=1;actions[0].ShootingStartedAt=2;
        return d;
    }
    static void Main()
    {
        Check(WeaponRules.IsHitscan(DuelWeapon.BAS)&&WeaponRules.IsHitscan(DuelWeapon.Taser),"winchester and taser immediate");
        Check(!WeaponRules.IsHitscan(DuelWeapon.Revolver)&&!WeaponRules.IsHitscan(DuelWeapon.SawedOff),"other weapons use projectiles");
        Check(DuelGunplay.ShotRange(DuelWeapon.BAS,false)==20&&DuelGunplay.ShotRange(DuelWeapon.Taser,false)==5.5f,"weapon-specific ranges");
        var empty=DuelGunplay.TraceRifleHitscan(0,0,new AimVector(0,10,0),new AimVector(1,0,0));Check(empty.Seat<0&&empty.Distance==20,"miss ends at twenty metres");
        for(int i=0;i<=20;i++)
        {
            double sample=i/20.0;float spread=DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,false,sample);
            Check(spread>=0&&spread<=10,"winchester hip spread stays in cone");
            Check(DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,true,sample)==0,"ADS has zero random spread");
            Check(DuelGunplay.PrecisionSpreadRadius(DuelWeapon.Revolver,false,sample)==0,"revolver remains unchanged");
        }
        Check(Math.Abs(DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,false,0))<.001&&Math.Abs(DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,false,1)-10)<.001,"new spread boundaries");
        var hip=Party();Check(hip.Fire(0,0,0,false,hip.ForSeat(0).Version,2.1,2.1),"hip shot accepted");var hipShot=hip.ForSeat(0);
        double hipRadius=Math.Sqrt(hipShot.FiredYaw*hipShot.FiredYaw+hipShot.FiredPitch*hipShot.FiredPitch);
        Check(hipRadius>=0&&hipRadius<=10.001&&!hipShot.FiredAds,"server records actual dispersed direction");
        var bore=DuelGunplay.MuzzleOrigin(0,DuelWeapon.BAS,false,DuelGunplay.Direction(0,0));Check(Math.Abs(hipShot.FiredOrigin.X-bore.X)<.001&&Math.Abs(hipShot.FiredOrigin.Y-bore.Y)<.001&&Math.Abs(hipShot.FiredOrigin.Z-bore.Z)<.001,"spread does not move muzzle");
        var d=Party();DuelGunplay.Angles(DuelGunplay.Point(2,DuelHitPart.Head)-DuelGunplay.Eye(0),out float yaw,out float pitch);int v=d.ForSeat(0).Version;
        Check(d.Fire(0,yaw,pitch,true,v,2.1,2.1),"shot accepted");
        Check(d.ForSeat(2).Health==0&&d.ForSeat(2).LivesRemaining==1,"head damage and two lives applied in Fire");
        Check(d.ForSeat(0).FiredTarget==2&&d.ForSeat(0).LastHitPart==DuelHitPart.Head,"immediate hit recorded");
        Check(Math.Abs(d.ForSeat(0).FiredYaw-yaw)<.001&&Math.Abs(d.ForSeat(0).FiredPitch-pitch)<.001,"ADS direction exactly matches aim");
        var queued=(DuelProjectileVolley[])typeof(DuelDirector).GetField("projectiles",Hidden).GetValue(d);Check(queued[0]==null,"no queued winchester projectile");
        Check(!d.Fire(0,yaw,pitch,true,v,2.1,2.1),"stale duplicate rejected");
        var body=Party();DuelGunplay.Angles(DuelGunplay.Point(2,DuelHitPart.Body)-DuelGunplay.Eye(0),out yaw,out pitch);Check(body.Fire(0,yaw,pitch,true,body.ForSeat(0).Version,2.1,2.1),"body shot accepted");Check(body.ForSeat(2).Health==100&&body.ForSeat(2).LivesRemaining==3,"body shot causes no damage");
        var props=Party();var bottle=DuelGunplay.Local(0,.66f,1.1f,.82f-DuelGunplay.SeatAdvance);DuelGunplay.Angles(bottle-DuelGunplay.Eye(0),out yaw,out pitch);
        Check(props.Fire(0,yaw,pitch,true,props.ForSeat(0).Version,2.1,2.1),"prop shot accepted");
        Check(props.Tabletop.Broken(0,DuelTableProp.Bottle)&&props.ForSeat(1).Health==100,"bottle blocks and breaks immediately");
        var revolver=Party();var revolverActions=(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(revolver);revolverActions[0].Weapon=DuelWeapon.Revolver;revolverActions[0].Action=DuelAction.Aim;revolverActions[0].LoadedMask=1;
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out yaw,out pitch);
        Check(revolver.Fire(0,yaw,pitch,false,revolver.ForSeat(0).Version,2.1,2.1),"revolver shot accepted");
        Check(revolver.ForSeat(1).Health==100,"revolver hit is not instant");revolver.Tick(2.7);Check(revolver.ForSeat(1).Health==0,"revolver projectile hits later");
        var taser=Party();var taserActions=(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(taser);taserActions[0].Weapon=DuelWeapon.Taser;taserActions[0].Action=DuelAction.Aim;taserActions[0].LoadedMask=1;
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out yaw,out pitch);
        Check(taser.Fire(0,yaw,pitch,false,taser.ForSeat(0).Version,2.1,2.1),"taser shot accepted");
        Check(taser.ForSeat(1).Health==95&&taser.ForSeat(1).Stunned,"taser damage and shock remain immediate");
        var farTaser=DuelGunplay.TraceHitscan(0,0,new AimVector(0,10,0),new AimVector(1,0,0));Check(farTaser.Distance==DuelGunplay.TaserRange,"taser range unchanged");
        int hits=0;const int attempts=400;
        for(int i=0;i<attempts;i++)
        {
            var trial=Party(i+100);DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out yaw,out pitch);
            Check(trial.Fire(0,yaw,pitch,false,trial.ForSeat(0).Version,2.1,2.1),"trial hip shot accepted");
            if(trial.ForSeat(1).Health==0)hits++;
        }
        Check(hits>0&&hits<attempts/3,"hip fire sometimes hits but remains inaccurate");
        var warmup=new DuelDirector(DuelMode.PvP,new Random(11));
        for(int i=0;i<2;i++){Check(warmup.Join(i,0),"practice join");Check(warmup.SelectItem(i,DuelItem.Recovery)&&warmup.SelectItem(i,DuelItem.Taser),"practice loadout");}
        Check(warmup.SelectWeapon(0,DuelWeapon.BAS,0)&&warmup.ToggleHostPractice(0)&&warmup.Ready(1,0),"both enter practice");
        int confirms=0;warmup.PracticeHitConfirmed+=(actor,session,sequence,head)=>{if(actor==0&&head)confirms++;};
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out yaw,out pitch);
        Check(warmup.Fire(0,yaw,pitch,true,warmup.ForSeat(0).Version,1,1),"practice shot accepted");
        Check(confirms==1&&warmup.ForSeat(1).HitAt==1&&warmup.ForSeat(1).Health==100,"practice hit immediate without damage");
        var pending=(System.Collections.ICollection)typeof(DuelDirector).GetField("practiceProjectiles",Hidden).GetValue(warmup);Check(pending.Count==0,"no queued practice projectile");
        var practiceHip=new DuelDirector(DuelMode.PvP,new Random(31));
        for(int i=0;i<2;i++){Check(practiceHip.Join(i,0),"hip practice join");Check(practiceHip.SelectItem(i,DuelItem.Recovery)&&practiceHip.SelectItem(i,DuelItem.Taser),"hip practice loadout");}
        Check(practiceHip.SelectWeapon(0,DuelWeapon.BAS,0)&&practiceHip.ToggleHostPractice(0)&&practiceHip.Ready(1,0),"hip practice starts");
        Check(practiceHip.Fire(0,0,0,false,practiceHip.ForSeat(0).Version,1,1),"hip practice shot accepted");var practiceShot=practiceHip.ForSeat(0);
        double practiceRadius=Math.Sqrt(practiceShot.FiredYaw*practiceShot.FiredYaw+practiceShot.FiredPitch*practiceShot.FiredPitch);
        Check(practiceRadius>=0&&practiceRadius<=10.001,"practice uses same spread");
        Console.WriteLine("Winchester hitscan: "+checks+" checks passed.");
    }
}
