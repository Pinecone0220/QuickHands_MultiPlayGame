using System;
using System.Reflection;
using HelloWorld;
public static class HostStartTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Choose(DuelDirector d,int seat){Check(d.SelectItem(seat,DuelItem.Recovery)&&d.SelectItem(seat,DuelItem.Taser),"choose items");}
    public static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(3));
        Check(d.Join(0,0)&&d.Join(1,0),"join host and guest");Choose(d,1);
        Check(!d.Ready(0,0)&&!d.StartParty(1,0)&&!d.StartParty(0,0),"host cannot ready, guest cannot start, guest is not ready");
        Check(d.Ready(1,1)&&d.Game.Phase==DuelPhase.Waiting,"guest ready does not auto-start");
        Check(!d.StartParty(0,1),"host must choose items");Choose(d,0);
        Check(d.Join(2,1)&&!d.StartParty(0,1),"new guest blocks start");Choose(d,2);
        Check(d.Ready(2,1)&&d.Ready(1,1)&&!d.StartParty(0,1),"guest cancellation blocks start");
        Check(d.Ready(1,1)&&d.Game.Phase==DuelPhase.Waiting&&d.StartParty(0,2),"host starts after every guest ready");
        Check(d.Game.Phase==DuelPhase.Playing&&!d.StartParty(0,2),"cannot start twice");
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(d,new object[]{DuelStage.Idle,10d});
        Check(d.Ready(1,10)&&d.Ready(2,10)&&d.Game.Phase==DuelPhase.Finished,"rematch waits for host start");
        Check(d.SelectItem(0,DuelItem.Taser)&&!d.StartParty(0,10),"incomplete host rematch selection blocks start");
        Check(d.SelectItem(0,DuelItem.Bullet)&&d.StartParty(0,10)&&d.ForSeat(0).BulletItems==2,"rematch uses host selection");
        var bots=new DuelDirector(DuelMode.AI,new Random(5));Check(bots.Join(0,0),"solo host joins");Choose(bots,0);
        Check(bots.AddBot(1,0)&&bots.Game.Phase==DuelPhase.Waiting&&bots.Game.CanStart,"bot auto-ready waits for host");
        Check(bots.StartParty(0,1)&&bots.Game.Phase==DuelPhase.Playing,"host starts solo bot match");
        var practice=new DuelDirector(DuelMode.PvP,new Random(7));practice.Join(0,0);practice.Join(1,0);Choose(practice,0);Choose(practice,1);
        Check(practice.ToggleHostPractice(0)&&practice.IsPracticing(0)&&!practice.IsPracticing(1),"host may enter practice independently");
        Check(!practice.CanStartParty,"guest readiness still required");
        Check(practice.Ready(1,0)&&practice.CanStartParty&&practice.Game.Phase==DuelPhase.Waiting,"host practice does not auto-start");
        Check(practice.ToggleHostPractice(0)&&!practice.IsPracticing(0)&&practice.CanStartParty,"host practice cancellation does not affect start");
        Check(practice.ToggleHostPractice(0)&&practice.StartParty(0,1)&&!practice.IsPracticing(0)&&practice.Game.Phase==DuelPhase.Playing,"host can start directly from practice");
        var preserved=new DuelDirector(DuelMode.PvP,new Random(6));preserved.Join(0,0);preserved.Join(1,0);Choose(preserved,0);
        Check(preserved.SelectItem(1,DuelItem.Bullet)&&preserved.SetAutoSelectItems(true)&&!preserved.Ready(1,0),"host automatic items do not fill guest loadout");
        Check(preserved.SelectItem(1,DuelItem.Recovery)&&preserved.Ready(1,0),"guest chooses and readies independently");
        Check(preserved.SetAutoSelectItems(false)&&RouletteRules.Has(preserved.Game.ReadyMask,1),"disabling automatic items preserves readiness");
        Check(preserved.CanStartParty&&DuelLoadout.Count(preserved.ForSeat(1).SelectedItems)==2,"guest selection stays intact");
        Check(preserved.StartParty(0,1),"host starts normally");
        var reset=new DuelDirector(DuelMode.PvP);reset.Join(0,0);reset.Join(1,0);Choose(reset,0);Choose(reset,1);reset.SetAutoSelectItems(true);reset.Ready(1,0);reset.ResetDeveloperSettings(0);
        Check(RouletteRules.Has(reset.Game.ReadyMask,1)&&reset.CanStartParty,"developer reset preserves guest readiness");
        Console.WriteLine("Host start: "+checks+" checks passed.");
    }
}
