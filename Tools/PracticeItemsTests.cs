using System;
using System.Reflection;
using HelloWorld;
class PracticeItemsTests
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static bool Shoot(DuelDirector d,int target,double now)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(0),out float yaw,out float pitch);
        return d.Fire(0,yaw,pitch,false,d.ForSeat(0).Version,now,now);
    }
    static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(31));
        d.Join(0,0);d.Join(1,0);d.Join(2,0);d.SetAutoSelectItems(true);
        Check(!d.UseItem(0,DuelItem.Taser,0,.1),"unready cannot use practice item");
        Check(d.Ready(0,.2)&&d.Ready(1,.2)&&d.PracticeMask==3,"two players practice while third waits");
        Check(d.UseItem(0,DuelItem.Recovery,0,.3)&&d.UseItem(0,DuelItem.Recovery,0,.4),"recovery can be reused");
        Check(d.ForSeat(0).HasRecovery&&d.ForSeat(0).Health==100,"recovery does not consume game inventory or change practice health");
        Check(d.UseItem(0,DuelItem.Bullet,0,.5)&&d.ForSeat(0).BulletUsed&&d.ForSeat(0).BulletItems==2,"bonus bullet toggles without consumption");
        Check(d.UseItem(1,DuelItem.NoruFace,0,.6)&&d.ForSeat(1).NoruFaceActive,"padding equips");
        Check(d.UseItem(0,DuelItem.Taser,0,.7)&&d.ForSeat(0).Weapon==DuelWeapon.Taser,"taser equipped");
        Check(Shoot(d,1,1)&&!d.ForSeat(1).Stunned&&d.ForSeat(1).Health==100,"padding blocks practice taser");
        Check(d.UseItem(1,DuelItem.NoruFace,0,1.1)&&!d.ForSeat(1).NoruFaceActive,"padding removed");
        Check(Shoot(d,1,1.3)&&d.ForSeat(1).Stunned&&d.ForSeat(1).Health==100,"taser hits instantly without game damage");
        Check(!d.Fire(1,60,0,false,d.ForSeat(1).Version,1.4,1.4),"stunned participant cannot fire");
        Check(!d.UseItem(1,DuelItem.Recovery,0,1.5),"stunned participant cannot use item");
        d.Tick(2.7);Check(!d.ForSeat(1).Stunned,"practice shock expires after 1.3 seconds");
        var item=d.ForSeat(0);Check(d.SwitchQuickItem(0,false,0,item.Life,item.QuickItemVersion,2.8)&&d.ForSeat(0).Weapon==DuelWeapon.Revolver,"switch back to gun");
        Check(d.UseItem(0,DuelItem.Ink,0,3)&&d.ForSeat(0).Ink.Armed,"ink equipped");
        var args=new object[]{0,1,0f,0f};typeof(DuelDirector).GetMethod("AimAiInk",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(d,args);
        item=d.ForSeat(0);int oldQuickVersion=item.QuickItemVersion;
        Check(d.ThrowInk(0,(float)args[2],(float)args[3],0,item.Life,item.Ink.Version,3.1,3.1),"ink throw accepted");
        item=d.ForSeat(0);Check(item.QuickEquipped&&item.QuickItem==3&&item.Ink.Armed,"ink remains equipped after throwing");
        Check(!d.ThrowInk(0,(float)args[2],(float)args[3],0,item.Life,item.Ink.Version,3.2,3.2),"cannot throw again while first pouch flies");
        Check(!d.SwitchQuickItem(0,false,0,item.Life,oldQuickVersion,3.2),"stale equipment request rejected after throw");
        for(double t=3.2;t<5;t+=.05)d.Tick(t);
        Check(d.ForSeat(1).Ink.InkedAt>=3.1&&d.ForSeat(0).Ink.Owned,"ink hits another ready player without consuming item");
        Check(d.ForSeat(1).Health==100&&d.ForSeat(1).LivesRemaining==3,"items leave practice health and lives intact");
        item=d.ForSeat(0);Check(d.ThrowInk(0,(float)args[2],(float)args[3],0,item.Life,item.Ink.Version,5,5),"equipped pouch can be thrown again");
        item=d.ForSeat(0);Check(item.QuickEquipped&&item.Ink.Armed&&item.Ink.Serial==2,"repeat throw remains equipped");
        Check(d.SwitchQuickItem(0,false,0,item.Life,item.QuickItemVersion,5.1)&&!d.ForSeat(0).Ink.Armed,"switch back to gun while pouch flies");
        Check(!d.UseItem(0,DuelItem.Ink,0,5.2),"cannot re-equip until previous flight resolves");
        d.Tick(7.3);Check(d.UseItem(0,DuelItem.Ink,0,7.4)&&d.ForSeat(0).Ink.Armed,"can re-equip after flight");
        Check(d.Ready(0,7.5)&&!d.ForSeat(0).Ink.Armed&&!d.IsPracticing(0),"cancel readiness clears practice effects");
        Check(d.Ready(0,7.6)&&d.Ready(2,7.7)&&d.Stage==DuelStage.Countdown,"all prepared start party");
        var initial=d.ForSeat(0);Check(initial.HasRecovery&&initial.HasTaser&&initial.Ink.Owned&&initial.BulletItems==2&&initial.HasNoruFace,"party starts with full selected items");
        Check(!initial.NoruFaceActive&&!initial.BulletUsed&&!initial.Ink.Armed&&initial.TaserReadyIteration==0,"practice effects and cooldowns do not leak into game");
        var limited=new DuelDirector(DuelMode.PvP,new Random(2));limited.Join(0,0);limited.Join(1,0);
        limited.SelectItem(0,DuelItem.Recovery);limited.SelectItem(0,DuelItem.Bullet);limited.Ready(0,0);
        Check(!limited.UseItem(0,DuelItem.Taser,0,1)&&!limited.UseItem(0,DuelItem.Ink,0,1),"unselected items are rejected by server");
        Console.WriteLine("PASS practice items: "+checks+" checks");
    }
}
