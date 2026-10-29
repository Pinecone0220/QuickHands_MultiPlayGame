using System;
using System.Reflection;
using HelloWorld;
public static class DeveloperScopeAndHealthTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static DuelActionState[] Actions(DuelDirector d)=>(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(d);
    static void Stage(DuelDirector d,DuelStage stage,double now)=>typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{stage,now});
    static void State(DuelDirector d,ClockState state)=>typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),state);
    static DuelDirector Room()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(11));for(int i=0;i<3;i++)Check(d.Join(i,0),"join");
        Check(d.SetAutoSelectItems(true)&&d.Game.SetUnlimitedItems(true)&&d.Game.SetNoItemCooldown(true)&&d.Game.SetShotTime(0),"host settings");
        Check(d.ForSeat(0).SelectedItems==DuelLoadout.All&&d.ForSeat(1).SelectedItems==0,"auto-selection only host");
        for(int i=1;i<3;i++)Check(d.SelectItem(i,DuelItem.Recovery)&&d.SelectItem(i,DuelItem.NoruFace)&&d.Ready(i,0),"guest selects and readies");
        Check(d.StartParty(0,0),"host starts");Stage(d,DuelStage.Racing,1);return d;
    }
    static void Main()
    {
        var botRoom=new DuelDirector(DuelMode.AI,new Random(9));Check(botRoom.Join(0,0)&&botRoom.SetAutoSelectItems(true)&&botRoom.AddBot(1,0),"host and bot lobby");
        Check(botRoom.ForSeat(0).SelectedItems==DuelLoadout.All&&DuelLoadout.Count(botRoom.ForSeat(1).SelectedItems)==2,"host auto-selection does not grant all items to bot");
        var d=Room();
        Check(DuelLoadout.Count(d.ForSeat(0).SelectedItems)==5&&DuelLoadout.Count(d.ForSeat(1).SelectedItems)==2,"only host receives all items");
        var shotLimit=typeof(DuelDirector).GetMethod("ShotLimitFor",Hidden);
        Check((float)shotLimit.Invoke(d,new object[]{0})==0&&(float)shotLimit.Invoke(d,new object[]{1})==10&&(float)shotLimit.Invoke(d,new object[]{2})==10,"shot timer is personal");
        Check(d.Game.Shoot(2,0,DuelWeapon.Revolver,DuelHitPart.Body,true)&&d.Game.Shoot(2,1,DuelWeapon.Revolver,DuelHitPart.Body,true),"damage both players");
        State(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,First=2,Last=1,Iteration=1,PhaseAt=10,StartedAt=15});
        Check(d.UseItem(0,DuelItem.Recovery,1,11)&&d.UseItem(1,DuelItem.Recovery,1,11),"both choose heal");d.Tick(15);
        Check(d.ForSeat(0).HasRecovery&&!d.ForSeat(1).HasRecovery,"only host retains consumable");
        var actions=Actions(d);actions[0].PreparedItem=actions[1].PreparedItem=0;actions[0].NoruFaceReadyIteration=8;actions[1].NoruFaceReadyIteration=8;
        State(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,First=2,Last=1,Iteration=2,PhaseAt=16,StartedAt=21});
        Check(d.UseItem(0,DuelItem.NoruFace,2,17)&&!d.UseItem(1,DuelItem.NoruFace,2,17),"cooldown exemption only host");
        var canShoot=typeof(DuelDirector).GetMethod("CanClockShoot",Hidden);
        State(d,new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,First=0,Iteration=3,PhaseAt=30});
        Check((bool)canShoot.Invoke(d,new object[]{0,41d}),"host has unlimited shooting time");
        State(d,new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,First=1,Iteration=3,PhaseAt=30});
        Check(!(bool)canShoot.Invoke(d,new object[]{1,41d}),"guest shooting time still expires");
        actions[0].Action=actions[1].Action=DuelAction.Aim;actions[0].ShootingStartedAt=actions[1].ShootingStartedAt=30;
        State(d,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=7,Iteration=4,PhaseAt=30});d.Tick(41);
        Check(d.ForSeat(0).Action==DuelAction.Aim&&d.ForSeat(1).Action==DuelAction.Pickup,"quickhands resets only guest when host has unlimited shooting time");
        var health=Room();
        Check(health.Game.Shoot(2,0,DuelWeapon.Revolver,DuelHitPart.Body,true)&&health.Game.Shoot(2,1,DuelWeapon.Revolver,DuelHitPart.Head,true),"survivor injured and other player dies");
        typeof(DuelDirector).GetMethod("LoseLife",Hidden).Invoke(health,new object[]{1,DuelDeathCause.Gunshot,5d,1});
        int survivorLife=health.ForSeat(0).Life,deadLife=health.ForSeat(1).Life;
        State(health,new ClockState{Phase=ClockPhase.QuickResults,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=7,First=0,Last=1,Iteration=1,PhaseAt=10});
        health.Tick(13.1);
        Check(health.ForSeat(0).Health==50&&health.ForSeat(0).Life==survivorLife,"quick results preserve survivor health and life");
        Check(health.ForSeat(1).Health==100&&health.ForSeat(1).Life==deadLife+1&&health.ForSeat(1).LivesRemaining==2,"dead player revives with remaining life");
        State(health,new ClockState{Phase=ClockPhase.Returning,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=7,First=0,Last=1,Iteration=2,PhaseAt=20});health.Tick(23.1);
        Check(health.ForSeat(0).Health==50&&health.ForSeat(0).Life==survivorLife,"shooting turn return also preserves survivor");
        Console.WriteLine("Developer scope and health: "+checks+" checks passed.");
    }
}
