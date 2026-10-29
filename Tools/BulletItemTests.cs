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
        typeof(DuelDirector).GetMethod("ResetQuickItems",Hidden).Invoke(d,null);
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
        Check(d.UseItem(0,DuelItem.Bullet,iteration,3)&&d.ForSeat(0).BulletItems==2&&!d.ForSeat(0).BulletUsed,"selection waits until item time ends");
        Check(d.UseItem(0,DuelItem.Bullet,iteration,3.01)&&d.ForSeat(0).PreparedItem==0,"same selection cancels");
        Check(d.UseItem(0,DuelItem.Bullet,iteration,3.02),"select again");
        d.Tick(7);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(0).Ammo==1,"first single chamber prepared");
        Check(!d.UseItem(0,DuelItem.Bullet,iteration,7.01),"already activated item cannot be used again during shooting");
        Check(!Miss(d,7.49),"shooter is blocked during item head start");
        Check(Miss(d,7.6)&&!Miss(d,7.6),"one in-flight shot and stale version reject duplicates");
        d.Tick(8.3);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(0).Ammo==1&&d.ForSeat(0).Action==DuelAction.Aim,"first miss retains extra shooting opportunity");
        Check(Miss(d,8.4),"extra shot can fire");d.Tick(9.1);
        Check(d.ForSeat(0).ShotsFired==2&&d.Clock.Phase==ClockPhase.Returning&&!Miss(d,9.2),"exactly two shots then turn ends");
        d.Tick(12.2);Check(d.ForSeat(0).BulletItems==1&&!d.ForSeat(0).BulletUsed,"next minigame preserves charge but clears use flag");
        Prep(d,13);Check(d.UseItem(0,DuelItem.Bullet,d.Clock.Iteration,14)&&d.ForSeat(0).BulletItems==1,"second charge selected on a later turn");
        d.Tick(18);Check(d.ForSeat(0).BulletItems==0,"second charge consumed at deadline");Miss(d,18.6);d.Tick(19.3);Miss(d,19.4);d.Tick(20.1);d.Tick(23.2);
        Prep(d,24);Check(!d.UseItem(0,DuelItem.Bullet,d.Clock.Iteration,25),"empty inventory rejected");
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Idle,26d});
        for(int i=0;i<3;i++)d.Ready(i,26);
        Check(d.ForSeat(0).BulletItems==2&&!d.ForSeat(0).BulletUsed,"new party replenishes selected two-pack");
        var absent=Party(selected:DuelItem.Taser);Prep(absent,2);
        Check(absent.ForSeat(0).BulletItems==0&&!absent.UseItem(0,DuelItem.Bullet,absent.Clock.Iteration,3),"unselected item grants no stock or effect");
        var ordinary=Party();Prep(ordinary,2);ordinary.Tick(7);Check(Miss(ordinary,7.6),"normal shot fires without item");ordinary.Tick(8.3);
        Check(ordinary.Clock.Phase==ClockPhase.Returning&&ordinary.ForSeat(0).ShotsFired==1,"unmodified turns still allow only one shot");
        var live=Party();Prep(live,2);live.Tick(7);
        Check(!live.UseItem(1,DuelItem.Bullet,live.Clock.Iteration,7.01),"non-shooter cannot activate");
        Check(!live.UseItem(0,DuelItem.Bullet,live.Clock.Iteration,7.02),"unselected item cannot be activated after preparation");
        var late=Party();Prep(late,2);late.Tick(7);Check(Miss(late,7.6),"unmodified first shot fires");
        Check(!late.UseItem(0,DuelItem.Bullet,late.Clock.Iteration,7.7),"cannot add an opportunity after firing");
        var quick=Party();Prep(quick,2,0,PartyMiniGame.QuickHands);
        Check(!quick.UseItem(0,DuelItem.Bullet,quick.Clock.Iteration,3)&&quick.ForSeat(0).BulletItems==2,"quick hands cannot use bullet");
        var headStart=Party(selected:DuelItem.Taser,limit:1);Prep(headStart,2);
        Check(headStart.UseItem(1,DuelItem.Taser,headStart.Clock.Iteration,3),"non-shooter arms taser during preparation");
        Check(headStart.Look(0,20,10,false,4)&&headStart.Look(1,75,-8,false,4),"human players set look before shooting");
        headStart.Tick(7);
        Check(headStart.ForSeat(0).AimYaw==20&&headStart.ForSeat(0).AimPitch==10,"shooter view survives transition");
        Check(headStart.ForSeat(1).AimYaw==75&&headStart.ForSeat(1).AimPitch==-8,"taser user's view survives transition");
        Check(!headStart.Fire(0,0,55,false,headStart.ForSeat(0).Version,7.499,7.499),"shooter cannot fire before 0.5 seconds");
        Check(headStart.Fire(1,60,55,false,headStart.ForSeat(1).Version,7.1,7.1),"taser can fire during head start");
        Check(headStart.Fire(0,0,55,false,headStart.ForSeat(0).Version,7.5,7.5),"shooter can fire at head start boundary");
        foreach(var game in new[]{PartyMiniGame.Countdown,PartyMiniGame.Signal,PartyMiniGame.Greed,PartyMiniGame.Memory})
        {
            var opportunity=Party(selected:DuelItem.Taser);Prep(opportunity,2,game:game);
            Check(opportunity.UseItem(1,DuelItem.Taser,opportunity.Clock.Iteration,3),"taser preparation "+game);
            opportunity.Tick(7);
            Check(!opportunity.Fire(0,0,55,false,opportunity.ForSeat(0).Version,7.499,7.499),"head start blocks shooter "+game);
            Check(opportunity.Fire(1,60,55,false,opportunity.ForSeat(1).Version,7.1,7.1),"head start permits taser "+game);
            Check(opportunity.Fire(0,0,55,false,opportunity.ForSeat(0).Version,7.5,7.5),"shooter begins at boundary "+game);
        }
        var timeout=Party(limit:1);Prep(timeout,2);timeout.Tick(7);timeout.Tick(8.49);
        Check(timeout.Clock.Phase==ClockPhase.Shooting,"shot limit starts after head start");
        timeout.Tick(8.5);Check(timeout.Clock.Phase==ClockPhase.Returning,"shot limit expires after full shooting time");
        var cheat=Party(true);Prep(cheat,2);
        Check(cheat.UseItem(0,DuelItem.Bullet,cheat.Clock.Iteration,3)&&cheat.ForSeat(0).BulletItems==2,"unlimited item selected");
        cheat.Tick(7);Check(cheat.ForSeat(0).BulletItems==2&&cheat.ForSeat(0).BulletUsed,"unlimited items preserves quantity on commit");
        var timed=Party(limit:1);Prep(timed,2);timed.UseItem(0,DuelItem.Bullet,timed.Clock.Iteration,3);timed.Tick(7);
        Check(Miss(timed,7.6),"time limited first shot fires");timed.Tick(8.2);
        Check(timed.Clock.Phase==ClockPhase.Shooting&&timed.ForSeat(0).Ammo==1,"extra shot readied before original deadline");
        Check(!Miss(timed,8.51),"bonus does not extend original deadline");timed.Tick(8.52);
        Check(timed.Clock.Phase==ClockPhase.Returning,"unused extra expires at original deadline");
        var unused=Party(limit:1);Prep(unused,2);unused.UseItem(0,DuelItem.Bullet,unused.Clock.Iteration,3);unused.Tick(7);unused.Tick(8.6);
        Check(unused.Clock.Phase==ClockPhase.Returning&&unused.ForSeat(0).ShotsFired==0&&unused.ForSeat(0).BulletItems==1,"zero shots still consumes exactly one item");
        unused.Tick(12);Check(unused.ForSeat(0).BulletItems==1&&!unused.ForSeat(0).BulletUsed,"unused bonus is not refunded in next minigame");
        Check(timed.ForSeat(0).ShotsFired==1&&timed.ForSeat(0).BulletItems==1,"one shot and unused bonus still costs exactly one item");
        var untouched=Party(limit:1);Prep(untouched,2);untouched.Tick(7);untouched.Tick(8.6);
        Check(untouched.ForSeat(0).BulletItems==2,"merely owning item without activation does not consume it");
        foreach(var game in new[]{PartyMiniGame.Countdown,PartyMiniGame.Signal,PartyMiniGame.Greed,PartyMiniGame.Memory})
        foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff,DuelWeapon.BAS})
        {
            var gun=Party(weapon:weapon);Prep(gun,2,game:game);gun.UseItem(0,DuelItem.Bullet,gun.Clock.Iteration,3);gun.Tick(7);
            Check(gun.ForSeat(0).Weapon==weapon,"selected weapon used in shooting "+weapon);
            Check(Miss(gun,7.6),"first shot "+weapon);gun.Tick(8.3);
            Check(gun.ForSeat(0).FiredWeapon==weapon,"first shot fires selected weapon "+weapon);
            Check(gun.ForSeat(0).Action==DuelAction.Aim&&gun.ForSeat(0).Ammo==1&&Miss(gun,8.4),"shared extra shot needs no quick-hands commands "+weapon);
            gun.Tick(9.1);Check(gun.ForSeat(0).ShotsFired==2&&gun.Clock.Phase==ClockPhase.Returning,"two-shot cap "+weapon);
        }
        var ai=Party(false,DuelMode.AI);Prep(ai,2,1);ai.Tick(2.8);
        Check(ai.ForSeat(1).PreparedItem==(int)DuelItem.Bullet+1&&ai.ForSeat(1).BulletItems==2,"AI shooter selects bullet");
        ai.Tick(7);for(double time=7.1;time<11;time+=.05)ai.Tick(time);
        Check(ai.ForSeat(1).ShotsFired==2,"AI fires both allowed shots");
        var equality=d.ForSeat(0);var different=equality;different.BulletItems++;
        Check(!equality.Equals(different),"quantity participates in snapshot equality");different=equality;different.BulletUsed=!different.BulletUsed;
        Check(!equality.Equals(different),"activation participates in snapshot equality");
        Console.WriteLine("PASS bullet items: "+checks+" checks");
    }
}
