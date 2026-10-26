using System;
using System.Reflection;
using HelloWorld;
class BulletItemTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static DuelDirector Party(bool unlimited=false,DuelMode mode=DuelMode.PvP,float limit=0,DuelWeapon weapon=DuelWeapon.Revolver,DuelItem selected=DuelItem.Bullet)
    {
        var d=new DuelDirector(mode,new Random(17));for(int i=0;i<3;i++)d.Join(i,0);
        d.Game.SetShotTime(limit);d.Game.SetUnlimitedItems(unlimited);
        for(int i=0;i<3;i++){d.SelectWeapon(i,weapon,0);if(mode==DuelMode.PvP||i==0){d.SelectItem(i,DuelItem.Recovery);d.SelectItem(i,selected);}}
        for(int i=0;i<3;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,1d});return d;
    }
    static void Prep(DuelDirector d,double now,int first=0,PartyMiniGame game=PartyMiniGame.Countdown)
    {
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.ItemPreparation,PhaseAt=now,StartedAt=now+5,MiniGame=game,Iteration=d.Clock.Iteration+1,First=first,ParticipantsMask=d.Game.AliveMask});
    }
    static bool Miss(DuelDirector d,double now)=>d.Fire(0,0,55,true,d.ForSeat(0).Version,now,now);
    static void Main()
    {
        Check(DuelLoadout.ItemCount==5&&DuelLoadout.Selectable(DuelItem.Bullet)&&DuelLoadout.Complete(9),"bullet is one distinct loadout choice");
        Check(!DuelLoadout.Selectable((DuelItem)255)&&!DuelLoadout.Complete(16),"unknown item bits rejected");
        var d=Party();for(int i=0;i<3;i++)Check(d.ForSeat(i).BulletItems==2,"two consumables granted per selected seat");
        Prep(d,2);int iteration=d.Clock.Iteration;
        Check(!d.UseItem(-1,DuelItem.Bullet,iteration,3)&&!d.UseItem(6,DuelItem.Bullet,iteration,3),"bad seats rejected");
        Check(!d.UseItem(1,DuelItem.Bullet,iteration,3)&&!d.UseItem(0,DuelItem.Bullet,iteration-1,3),"non-shooter and stale iteration rejected");
        Check(!d.UseItem(0,DuelItem.Bullet,iteration,7),"item deadline enforced");
        Check(d.UseItem(0,DuelItem.Bullet,iteration,3)&&d.ForSeat(0).BulletItems==1&&d.ForSeat(0).BulletUsed,"activation consumes one charge");
        Check(!d.UseItem(0,DuelItem.Bullet,iteration,3.01)&&d.ForSeat(0).BulletItems==1,"same turn replay cannot consume or grant twice");
        d.Tick(7);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(0).Ammo==1,"first single chamber prepared");
        Check(!d.UseItem(0,DuelItem.Bullet,iteration,7.01),"cannot activate outside item time");
        Check(Miss(d,7.1)&&!Miss(d,7.1),"one in-flight shot and stale version reject duplicates");
        d.Tick(7.8);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(0).Ammo==1&&d.ForSeat(0).Action==DuelAction.Aim,"first miss retains extra shooting opportunity");
        Check(Miss(d,7.9),"extra shot can fire");d.Tick(8.6);
        Check(d.ForSeat(0).ShotsFired==2&&d.Clock.Phase==ClockPhase.Returning&&!Miss(d,8.7),"exactly two shots then turn ends");
        d.Tick(12);Check(d.ForSeat(0).BulletItems==1&&!d.ForSeat(0).BulletUsed,"next minigame preserves charge but clears use flag");
        Prep(d,13);Check(d.UseItem(0,DuelItem.Bullet,d.Clock.Iteration,14)&&d.ForSeat(0).BulletItems==0,"second charge consumed on a later turn");
        d.Tick(18);Miss(d,18.1);d.Tick(18.8);Miss(d,18.9);d.Tick(19.6);d.Tick(23);
        Prep(d,24);Check(!d.UseItem(0,DuelItem.Bullet,d.Clock.Iteration,25),"empty inventory rejected");
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Idle,26d});
        for(int i=0;i<3;i++)d.Ready(i,26);
        Check(d.ForSeat(0).BulletItems==2&&!d.ForSeat(0).BulletUsed,"new party replenishes selected two-pack");
        var absent=Party(selected:DuelItem.Taser);Prep(absent,2);
        Check(absent.ForSeat(0).BulletItems==0&&!absent.UseItem(0,DuelItem.Bullet,absent.Clock.Iteration,3),"unselected item grants no stock or effect");
        var ordinary=Party();Prep(ordinary,2);ordinary.Tick(7);Check(Miss(ordinary,7.1),"normal shot fires without item");ordinary.Tick(7.8);
        Check(ordinary.Clock.Phase==ClockPhase.Returning&&ordinary.ForSeat(0).ShotsFired==1,"unmodified turns still allow only one shot");
        var quick=Party();Prep(quick,2,0,PartyMiniGame.QuickHands);
        Check(!quick.UseItem(0,DuelItem.Bullet,quick.Clock.Iteration,3)&&quick.ForSeat(0).BulletItems==2,"quick hands cannot use bullet");
        var cheat=Party(true);Prep(cheat,2);
        Check(cheat.UseItem(0,DuelItem.Bullet,cheat.Clock.Iteration,3)&&cheat.ForSeat(0).BulletItems==2,"unlimited items preserves quantity");
        Check(!cheat.UseItem(0,DuelItem.Bullet,cheat.Clock.Iteration,3.01),"unlimited items still enforces once per turn");
        var timed=Party(limit:1);Prep(timed,2);timed.UseItem(0,DuelItem.Bullet,timed.Clock.Iteration,3);timed.Tick(7);
        Check(Miss(timed,7.1),"time limited first shot fires");timed.Tick(7.7);
        Check(timed.Clock.Phase==ClockPhase.Shooting&&timed.ForSeat(0).Ammo==1,"extra shot readied before original deadline");
        Check(!Miss(timed,8.01),"bonus does not extend original deadline");timed.Tick(8.02);
        Check(timed.Clock.Phase==ClockPhase.Returning,"unused extra expires at original deadline");
        var unused=Party(limit:1);Prep(unused,2);unused.UseItem(0,DuelItem.Bullet,unused.Clock.Iteration,3);unused.Tick(7);unused.Tick(8.1);
        Check(unused.Clock.Phase==ClockPhase.Returning&&unused.ForSeat(0).ShotsFired==0&&unused.ForSeat(0).BulletItems==1,"zero shots still consumes exactly one item");
        unused.Tick(12);Check(unused.ForSeat(0).BulletItems==1&&!unused.ForSeat(0).BulletUsed,"unused bonus is not refunded in next minigame");
        Check(timed.ForSeat(0).ShotsFired==1&&timed.ForSeat(0).BulletItems==1,"one shot and unused bonus still costs exactly one item");
        var untouched=Party(limit:1);Prep(untouched,2);untouched.Tick(7);untouched.Tick(8.1);
        Check(untouched.ForSeat(0).BulletItems==2,"merely owning item without activation does not consume it");
        foreach(var weapon in new[]{DuelWeapon.SawedOff,DuelWeapon.BAS})
        {
            var gun=Party(weapon:weapon);Prep(gun,2);gun.UseItem(0,DuelItem.Bullet,gun.Clock.Iteration,3);gun.Tick(7);
            Check(Miss(gun,7.1),"first shot "+weapon);gun.Tick(7.8);
            Check(gun.ForSeat(0).Action==DuelAction.Aim&&gun.ForSeat(0).Ammo==1&&Miss(gun,7.9),"shared extra shot needs no quick-hands commands "+weapon);
            gun.Tick(8.6);Check(gun.ForSeat(0).ShotsFired==2&&gun.Clock.Phase==ClockPhase.Returning,"two-shot cap "+weapon);
        }
        var ai=Party(false,DuelMode.AI);Prep(ai,2,1);ai.Tick(2.8);
        Check(ai.ForSeat(1).BulletUsed&&ai.ForSeat(1).BulletItems==1,"AI shooter activates selected bullet");
        ai.Tick(7);for(double time=7.1;time<11;time+=.05)ai.Tick(time);
        Check(ai.ForSeat(1).ShotsFired==2,"AI fires both allowed shots");
        var equality=d.ForSeat(0);var different=equality;different.BulletItems++;
        Check(!equality.Equals(different),"quantity participates in snapshot equality");different=equality;different.BulletUsed=!different.BulletUsed;
        Check(!equality.Equals(different),"activation participates in snapshot equality");
        Console.WriteLine("PASS bullet items: "+checks+" checks");
    }
}
