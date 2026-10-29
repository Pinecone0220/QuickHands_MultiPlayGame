using System;
using System.Reflection;
using HelloWorld;
public static class TaserHolsterTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static void State(DuelDirector d,ClockState state)=>typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),state);
    static DuelDirector Room(int players=3)
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(8));for(int seat=0;seat<players;seat++){d.Join(seat,0);d.SelectItem(seat,DuelItem.Taser);d.SelectItem(seat,DuelItem.Recovery);if(seat>0)d.Ready(seat,0);}
        Check(d.StartParty(0,0),"start");typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,1d});return d;
    }
    static DuelDirector Quick(int players=3)
    {
        var d=Room(players);State(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=d.Game.AliveMask,Iteration=1,PhaseAt=2,StartedAt=7});
        Check(d.UseItem(0,DuelItem.Taser,1,3),"select taser");typeof(DuelDirector).GetMethod("CommitPreparedItems",Hidden).Invoke(d,new object[]{7d});
        State(d,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=d.Game.AliveMask,Iteration=1,PhaseAt=7,StartedAt=7});
        var a=d.ForSeat(0);Check(d.SwitchQuickItem(0,true,1,a.Life,a.QuickItemVersion,7.1),"equip taser");return d;
    }
    static void Main()
    {
        foreach(bool hit in new[]{false,true})
        {
            var d=Quick();float yaw=0,pitch=55;
            if(hit)DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out yaw,out pitch);
            Check(d.Fire(0,yaw,pitch,false,d.ForSeat(0).Version,8,8),"fire");
            Check(d.ForSeat(0).Weapon==DuelWeapon.Taser&&d.ForSeat(0).QuickEquipped,"taser remains immediately after shot");
            d.Tick(8+DuelDirector.TaserHolsterDelay-.01);Check(d.ForSeat(0).Weapon==DuelWeapon.Taser,"taser remains during delay");
            d.Tick(8+DuelDirector.TaserHolsterDelay+.01);var a=d.ForSeat(0);
            Check(a.Weapon==a.SelectedWeapon&&!a.QuickEquipped&&a.QuickSpent&&a.FiredWeapon==DuelWeapon.Taser,"primary weapon restored after delay");
        }
        var fatal=Quick(2);fatal.Game.Shoot(0,1,DuelWeapon.SawedOff,DuelHitPart.Body,true);
        for(int i=0;i<4;i++)fatal.Game.Shoot(0,1,DuelWeapon.Taser,DuelHitPart.Body,true);
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out float fatalYaw,out float fatalPitch);
        Check(fatal.Fire(0,fatalYaw,fatalPitch,false,fatal.ForSeat(0).Version,8,8),"fatal shot");
        Check(fatal.QuickHandsActive&&fatal.ForSeat(0).Weapon==DuelWeapon.Taser,"winning shot keeps taser during delay");
        fatal.Tick(8.41);Check(fatal.ForSeat(0).Weapon==fatal.ForSeat(0).SelectedWeapon&&!fatal.QuickHandsActive,"winning shot restores primary then ends minigame");
        var normal=Room();State(normal,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,Iteration=1,First=0,Last=1,PhaseAt=10,StartedAt=15});
        Check(normal.UseItem(1,DuelItem.Taser,1,11),"normal turn chooses taser");normal.Tick(15);
        Check(normal.ForSeat(1).Weapon==DuelWeapon.Taser&&normal.Fire(1,60,55,false,normal.ForSeat(1).Version,15.1,15.1),"normal turn fires taser");
        normal.Tick(15.49);Check(normal.ForSeat(1).Weapon==DuelWeapon.Taser,"normal turn keeps taser during delay");
        normal.Tick(15.51);Check(normal.ForSeat(1).Weapon==normal.ForSeat(1).SelectedWeapon&&!normal.ForSeat(1).TaserArmed&&normal.ForSeat(1).FiredWeapon==DuelWeapon.Taser,"normal turn restores primary");
        var practice=new DuelDirector(DuelMode.AI,new Random(2));practice.Join(0,0);practice.SelectItem(0,DuelItem.Taser);practice.SelectItem(0,DuelItem.Recovery);
        Check(practice.ToggleHostPractice(0)&&practice.UseItem(0,DuelItem.Taser,practice.Clock.Iteration,1),"practice equips taser");
        Check(practice.Fire(0,0,55,false,practice.ForSeat(0).Version,2,2),"practice fires taser");practice.Tick(3);
        Check(practice.ForSeat(0).Weapon==DuelWeapon.Taser&&practice.ForSeat(0).QuickEquipped,"practice stays equipped");
        Console.WriteLine("Taser holster: "+checks+" checks passed.");
    }
}
