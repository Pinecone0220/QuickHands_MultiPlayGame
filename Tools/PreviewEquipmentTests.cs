using System;
using System.Reflection;
using HelloWorld;
public static class PreviewEquipmentTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Stage(DuelDirector d,DuelStage stage,double now)=>typeof(DuelDirector).GetMethod("SetStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(d,new object[]{stage,now});
    static void State(DuelDirector d,ClockState state)
    {var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);typeof(DuelClockContest).GetProperty("State").SetValue(c,state);}
    static bool Preview(DuelDirector d,int seat,int item,double now){var a=d.ForSeat(seat);return d.SetPreviewItem(seat,item,d.Clock.Iteration,a.Life,a.QuickItemVersion,now);}
    static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(1));Check(d.Join(0,0)&&d.Join(1,0),"players join");
        Check(d.SelectItem(0,DuelItem.Taser)&&d.SelectItem(0,DuelItem.Ink),"host selects held items");
        Check(d.SelectItem(1,DuelItem.Recovery)&&d.SelectItem(1,DuelItem.Taser)&&d.Ready(1,0),"guest ready");
        Check(Preview(d,0,1,0)&&d.ForSeat(0).PreviewItem==1&&d.ForSeat(0).Weapon==DuelWeapon.Revolver,"preview taser without changing gameplay weapon");
        var old=d.ForSeat(0);Check(Preview(d,0,2,0)&&d.ForSeat(0).PreviewItem==2&&!d.ForSeat(0).Ink.Armed,"preview ink without arming");
        Check(!d.SetPreviewItem(0,1,d.Clock.Iteration,old.Life,old.QuickItemVersion,0),"old version rejected");
        Check(Preview(d,0,0,0)&&d.ForSeat(0).PreviewItem==0,"switch back to weapon");
        Check(d.StartParty(0,1),"host starts");Check(Preview(d,0,1,1.1),"preview during start countdown");
        Stage(d,DuelStage.Racing,3);State(d,new ClockState{Phase=ClockPhase.Selecting,ParticipantsMask=3,Iteration=1});
        Check(Preview(d,0,2,3)&&d.ForSeat(0).PreviewItem==2,"switch during minigame selection");
        State(d,new ClockState{Phase=ClockPhase.Counting,ParticipantsMask=3,Iteration=1,StartedAt=100,Target=1000});d.Tick(3.1);
        Check(d.ForSeat(0).PreviewItem==2,"preview persists through noncombat phase change");
        State(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=3,Iteration=1,StartedAt=8,First=0,Last=1});
        Check(Preview(d,0,1,4)&&d.ForSeat(0).PreviewItem==1,"preview available during item preparation");d.Tick(4);
        Check(d.ForSeat(0).PreviewItem==1&&!d.ForSeat(0).TaserArmed&&d.ForSeat(0).HasTaser,"preview does not use item");
        Check(!Preview(d,1,2,4),"unowned item cannot be previewed");
        State(d,new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=3,Iteration=1,PhaseAt=5,First=0,Last=1});
        Check(!Preview(d,0,2,5),"preview cannot bypass shooting rules");d.Tick(5);
        Check(d.ForSeat(0).PreviewItem==0&&!d.ForSeat(0).Ink.Armed&&d.ForSeat(0).Ink.Owned,"preview removed at shooting without consuming item");
        Console.WriteLine("Preview equipment: "+checks+" checks passed.");
    }
}
