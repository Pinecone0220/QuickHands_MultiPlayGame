using System;
using System.Reflection;
using HelloWorld;
public static class RematchLoadoutTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(1));
        for(int seat=0;seat<2;seat++)
        {Check(d.Join(seat,0),"join");Check(d.SelectItem(seat,DuelItem.Recovery)&&d.SelectItem(seat,DuelItem.Taser),"initial loadout");}
        Check(d.Ready(1,1)&&d.StartParty(0,1)&&d.Game.Phase==DuelPhase.Playing,"start");
        Check(d.ForSeat(0).LoadoutLocked&&!d.SelectItem(0,DuelItem.Taser),"locked during play");
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(d,new object[]{DuelStage.Idle,10d});
        Check(!d.ForSeat(0).LoadoutLocked,"loadout editable in finished state");
        Check(d.SelectItem(0,DuelItem.Taser)&&!d.StartParty(0,11),"incomplete loadout blocks start");
        Check(d.SelectItem(0,DuelItem.Bullet),"choose replacement item");
        Check(d.SelectWeapon(0,DuelWeapon.BAS,11),"change weapon");
        Check(d.SelectItem(0,DuelItem.Bullet)&&d.SelectItem(0,DuelItem.Ink),"host may reselect before start");
        Check(d.Ready(1,11)&&!d.SelectItem(1,DuelItem.Taser),"guest ready locks choices");
        Check(d.Ready(1,11)&&d.SelectItem(1,DuelItem.Taser)&&d.SelectItem(1,DuelItem.Bullet),"guest cancel ready allows changes");
        Check(d.Ready(1,11)&&d.StartParty(0,11)&&d.Game.Phase==DuelPhase.Playing,"host starts rematch after guests ready");
        Check(d.ForSeat(0).LoadoutLocked&&d.ForSeat(0).HasRecovery&&d.ForSeat(0).Ink.Owned&&!d.ForSeat(0).HasTaser&&d.ForSeat(0).SelectedWeapon==DuelWeapon.BAS,"rematch uses final selected equipment");
        Console.WriteLine("Rematch loadout: "+checks+" checks passed.");
    }
}
