using System;
using System.Reflection;
using HelloWorld;
public static class BulletPracticePreviewTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(41));
        for(int i=0;i<2;i++){Check(d.Join(i,0),"join");Check(d.SelectItem(i,DuelItem.Recovery),"select recovery");Check(d.SelectItem(i,i==0?DuelItem.Bullet:DuelItem.Taser),"select item");}
        Check(d.ToggleHostPractice(0)&&d.Ready(1,0),"both practice");
        int before=d.ForSeat(0).BulletItems;
        Check(d.Fire(0,0,50,false,d.ForSeat(0).Version,1,1),"practice shot");
        Check(!d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==before&&d.ForSeat(0).PracticeShotsFired==1,"practice does not use or grant bullet item");
        Check(d.StartParty(0,2),"start while host practices");Check(d.ForSeat(0).BulletItems==2&&!d.ForSeat(0).BulletUsed,"match starts with normal inventory and no effect");
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,2d});
        var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);
        typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.ItemPreparation,PhaseAt=2,StartedAt=7,MiniGame=PartyMiniGame.Countdown,Iteration=1,First=0,ParticipantsMask=d.Game.AliveMask});
        Check(d.UseItem(0,DuelItem.Bullet,1,3)&&!d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==2,"selecting waits until deadline");
        d.Tick(7);Check(d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==1&&d.Clock.Phase==ClockPhase.Shooting,"confirmed item consumes one");
        Check(d.Fire(0,0,55,false,d.ForSeat(0).Version,7.6,7.6),"first match shot");d.Tick(8.3);
        Check(d.ForSeat(0).Ammo==1&&d.Clock.Phase==ClockPhase.Shooting,"one extra shot available");
        Check(d.Fire(0,0,55,false,d.ForSeat(0).Version,8.4,8.4),"extra shot");d.Tick(9.1);
        Check(d.ForSeat(0).ShotsFired==2&&d.Clock.Phase==ClockPhase.Returning,"exactly two shots");
        Console.WriteLine("Bullet practice preview: "+checks+" checks passed.");
    }
}
