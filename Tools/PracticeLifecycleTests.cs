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
        Console.WriteLine("PASS practice lifecycle: 5 checks");
    }
}
