using System;
using System.Reflection;
using HelloWorld;
class PreparedItemsTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static DuelDirector Party(bool unlimited=false,bool noCooldown=false)
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(17));
        for(int i=0;i<3;i++)d.Join(i,0);
        d.Game.SetShotTime(0);d.Game.SetUnlimitedItems(unlimited);d.Game.SetNoItemCooldown(noCooldown);
        d.SetAutoSelectItems(true);for(int i=0;i<3;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,1d});
        return d;
    }
    static void Prep(DuelDirector d,double now,int iteration=1,PartyMiniGame game=PartyMiniGame.Countdown)
    {
        typeof(DuelDirector).GetMethod("ResetQuickItems",Hidden).Invoke(d,null);
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{
            Phase=ClockPhase.ItemPreparation,PhaseAt=now,StartedAt=now+5,MiniGame=game,Iteration=iteration,
            First=0,Last=1,ParticipantsMask=d.Game.AliveMask});
    }
    static void Main()
    {
        var lobby=new DuelDirector(DuelMode.PvP);lobby.Join(0,0);lobby.Join(1,0);
        lobby.SelectItem(0,DuelItem.Bullet);lobby.SetAutoSelectItems(true);
        Check(lobby.ForSeat(0).SelectedItems==DuelLoadout.All,"automatic selection visible");
        Check(lobby.Ready(0,1),"automatic selection allows readiness");
        Check(lobby.SetAutoSelectItems(false),"automatic selection can be disabled");
        Check(lobby.ForSeat(0).SelectedItems==(1<<(int)DuelItem.Bullet),"previous manual selection restored");
        Check(!RouletteRules.Has(lobby.Game.ReadyMask,0),"incomplete restored selection cancels readiness");
        lobby.SetAutoSelectItems(true);lobby.ResetDeveloperSettings(2);
        Check(lobby.ForSeat(0).SelectedItems==(1<<(int)DuelItem.Bullet),"F11 reset restores selection");
        var complete=new DuelDirector(DuelMode.PvP);complete.Join(0,0);complete.Join(1,0);
        complete.SelectItem(0,DuelItem.Bullet);complete.SelectItem(0,DuelItem.Recovery);
        complete.SetAutoSelectItems(true);complete.Ready(0,1);complete.Join(2,1);
        Check(complete.ForSeat(2).SelectedItems==DuelLoadout.All,"new participant sees automatic selection");
        complete.SetAutoSelectItems(false);
        Check(complete.ForSeat(2).SelectedItems==0,"new participant does not retain automatic items");
        Check(DuelLoadout.Complete(complete.ForSeat(0).SelectedItems)&&RouletteRules.Has(complete.Game.ReadyMask,0),"complete manual selection and readiness preserved");
        var d=Party();Prep(d,2);
        Check(d.UseItem(0,DuelItem.Bullet,1,3),"select bullet");
        Check(d.ForSeat(0).PreparedItem==(int)DuelItem.Bullet+1&&!d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==2,"selection does not consume");
        Check(!d.UseItem(0,DuelItem.NoruFace,1,3.1),"second choice rejected");
        Check(d.UseItem(0,DuelItem.Bullet,1,3.2)&&d.ForSeat(0).PreparedItem==0,"same choice cancels");
        Check(d.ForSeat(0).BulletItems==2&&!d.ForSeat(0).BulletUsed,"cancellation has no effect");
        Check(d.UseItem(0,DuelItem.Bullet,1,3.3),"reselect bullet");
        d.Tick(6.999);Check(!d.ForSeat(0).BulletUsed,"effect waits for deadline");
        d.Tick(7);Check(d.Clock.Phase==ClockPhase.Shooting&&d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==1,"deadline commits bullet");
        Check(d.Fire(0,0,55,false,d.ForSeat(0).Version,7.6,7.6),"first shot");d.Tick(8.3);
        Check(d.Fire(0,0,55,false,d.ForSeat(0).Version,8.4,8.4),"bonus shot");
        var quick=Party();Prep(quick,2,1,PartyMiniGame.QuickHands);
        Check(quick.UseItem(1,DuelItem.Taser,1,3),"quick taser select");
        Check(quick.UseItem(1,DuelItem.Taser,1,3.1)&&quick.ForSeat(1).PreparedItem==0,"quick taser cancel");
        Check(quick.UseItem(1,DuelItem.NoruFace,1,3.2)&&!quick.ForSeat(1).NoruFaceActive,"quick jacket delayed");
        quick.Tick(7);Check(quick.ForSeat(1).NoruFaceActive&&quick.ForSeat(1).QuickSpent,"quick jacket committed once");
        var unlimited=Party(true,false);Prep(unlimited,2,4);
        Check(unlimited.UseItem(1,DuelItem.Taser,4,3),"unlimited taser selected");
        unlimited.Tick(7);Check(unlimited.ForSeat(1).TaserReadyIteration==7,"unlimited consumables keeps taser cooldown");
        Clock(unlimited).Return(8);unlimited.Tick(8.1);Prep(unlimited,9,5);
        Check(!unlimited.UseItem(1,DuelItem.Taser,5,10),"unlimited consumables does not bypass taser cooldown");
        var cheat=Party(false,true);Prep(cheat,2,4);
        Check(cheat.UseItem(0,DuelItem.Bullet,4,3),"cooldown option bullet select");
        cheat.Tick(7);Check(cheat.ForSeat(0).BulletItems==1,"cooldown option does not preserve consumables");
        var noCooldown=Party(false,true);Prep(noCooldown,2,4);
        Check(noCooldown.UseItem(1,DuelItem.Taser,4,3),"cooldown exemption selects taser");noCooldown.Tick(7);
        Clock(noCooldown).Return(8);noCooldown.Tick(8.1);Prep(noCooldown,9,5);
        Check(noCooldown.UseItem(1,DuelItem.Taser,5,10),"cooldown exemption permits next-game taser");
        var heal=Party();Prep(heal,2);
        heal.Game.Shoot(2,0,DuelWeapon.Revolver,DuelHitPart.Body,true);
        Check(heal.UseItem(0,DuelItem.Recovery,1,3)&&heal.Game.HealthForSeat(0)==50,"healing selection is deferred");
        Check(heal.UseItem(0,DuelItem.Recovery,1,3.1)&&heal.Game.HealthForSeat(0)==50,"healing cancels without effect");
        Check(heal.UseItem(0,DuelItem.Recovery,1,3.2),"healing can be reselected");
        heal.Tick(7);Check(heal.Game.HealthForSeat(0)==100&&!heal.ForSeat(0).HasRecovery,"healing applies and consumes at deadline");
        var ink=Party();Prep(ink,2);
        Check(ink.UseItem(1,DuelItem.Ink,1,3)&&!ink.ForSeat(1).Ink.Armed,"ink selection is deferred");
        Check(ink.UseItem(1,DuelItem.Ink,1,3.1)&&!ink.ForSeat(1).Ink.Armed,"ink selection cancels");
        Check(ink.UseItem(1,DuelItem.Ink,1,3.2),"ink can be reselected");
        ink.Tick(7);Check(ink.ForSeat(1).Ink.Armed,"ink arms at deadline");
        Console.WriteLine("PASS prepared items: "+checks+" checks");
    }
}
