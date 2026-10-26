using System;
using HelloWorld;
class PracticeLifecycleProbe
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP);for(int i=0;i<3;i++)d.Join(i,0);d.SetAutoSelectItems(true);d.Ready(0,0);d.Ready(1,0);
        int oldSession=d.ForSeat(0).PracticeSession;double now=1;
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out float y,out float p);
        DuelGunplay.Sway(0,DuelWeapon.Revolver,true,now,0,out float sy,out float sp);
        Require(d.Fire(0,(y-sy+360)%360,p-sp,true,d.ForSeat(0).Version,now,now),"queued practice shot");
        int reports=0;d.PracticeHitConfirmed+=(a,s,n,h)=>reports++;
        d.Ready(0,1.01);d.Ready(1,1.01);d.Disconnect(0,1.02);d.Join(0,1.03);d.SetAutoSelectItems(true);d.Ready(0,1.04);d.Ready(1,1.04);
        Require(d.ForSeat(0).PracticeSession>oldSession,"slot reuse must not recycle practice session");
        d.Tick(1.6);
        Require(d.PracticeMask==3&&reports==0,"old flight cannot confirm after disconnect and immediate slot reuse");
        Require(d.ForSeat(1).HitAt<0,"old flight cannot flash on new ready target");
        Require(d.Game.Shots==0&&d.Game.HealthForSeat(0)==100&&d.Game.HealthForSeat(1)==100,"practice remains outside real match records");
        var room=new DuelDirector(DuelMode.PvP);room.Join(0,0);room.Join(1,0);room.Join(5,0);room.SetAutoSelectItems(true);room.SelectWeapon(0,DuelWeapon.BAS,0);room.Ready(0,0);room.Ready(1,0);
        Require(room.Fire(0,0,30,false,room.ForSeat(0).Version,1,1),"ready BAS can practice");
        var lever=room.ForSeat(0);Require(room.Input(0,lever.Digit(0),lever.Version,1.01),"partial lever input");lever=room.ForSeat(0);
        Require(room.Join(2,1.02)&&room.Game.ReadyMask==3&&room.PracticeMask==3,"join preserves existing ready players and leaves newcomer unready");
        var afterJoin=room.ForSeat(0);Require(afterJoin.PracticeSession==lever.PracticeSession&&afterJoin.Version==lever.Version&&afterJoin.Action==lever.Action&&afterJoin.Progress==lever.Progress&&afterJoin.AimYaw==lever.AimYaw,"join preserves practice session and partial lever action");
        Require(!room.Join(0,1.03)&&room.Game.ReadyMask==3,"rejected duplicate join does not alter readiness");
        Require(room.Game.SetShotTime(17)&&room.Game.SetUnlimitedItems(true)&&room.Game.SetManualMiniGame(true)&&room.SetAutoSelectItems(false)&&room.SetAutoSelectItems(true),"room options changed");
        Require(room.Game.ReadyMask==3&&room.ForSeat(0).PracticeSession==lever.PracticeSession,"all room settings preserve ready and practice state");
        Require(room.Game.ResetDeveloperSettings()&&room.Game.ReadyMask==3,"F11 defaults preserve ready");
        Require(room.Disconnect(2,1.04)&&room.Game.ReadyMask==3&&room.ForSeat(0).Progress==lever.Progress,"unready departure preserves ready and practice progress");
        Require(room.Ready(1,1.05)&&room.Game.ReadyMask==1,"manual cancel only affects its owner");
        Require(room.Ready(1,1.06)&&room.Disconnect(1,1.07)&&room.Game.ReadyMask==1,"ready departure only removes departed bit");
        Require(room.Join(1,1.08)&&room.Game.ReadyMask==1&&!RouletteRules.Has(room.Game.ReadyMask,1),"reused seat starts unready");
        room.SetAutoSelectItems(true);room.Disconnect(5,1.09);
        Require(room.Game.Phase!=DuelPhase.Playing&&room.Game.ReadyMask==1,"new participant still must ready");
        Require(room.Ready(1,1.1)&&room.Game.Phase==DuelPhase.Playing&&room.Game.Round==1,"all connected ready starts once");
        var remaining=new DuelDirector(DuelMode.PvP);remaining.Join(0,0);remaining.Join(1,0);remaining.Join(2,0);remaining.SetAutoSelectItems(true);remaining.Ready(0,0);remaining.Ready(1,0);
        Require(remaining.Disconnect(2,1)&&remaining.Game.Phase==DuelPhase.Playing&&remaining.Stage==DuelStage.Countdown,"last unready departure starts remaining ready players without re-ready");
        var bots=new DuelDirector(DuelMode.AI);bots.Join(0,0);bots.SetAutoSelectItems(true);bots.Ready(0,0);
        Require(bots.Join(1,1)&&bots.Game.Phase==DuelPhase.Playing,"auto-ready AI joins already-ready host without requiring re-ready");
        Console.WriteLine("PASS practice lifecycle and ready persistence: 21 checks");
    }
}
