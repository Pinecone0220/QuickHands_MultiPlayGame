using System;
using System.Reflection;
using HelloWorld;
class NoruFaceTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static DuelActionState[] Actions(DuelDirector d)=>(DuelActionState[])typeof(DuelDirector).GetField("actions",Hidden).GetValue(d);
    static DuelDirector Party(bool unlimited=false,bool all=true,DuelMode mode=DuelMode.PvP,bool noCooldown=false)
    {
        var d=new DuelDirector(mode,new Random(73));for(int i=0;i<3;i++)d.Join(i,0);
        d.Game.SetShotTime(0);d.Game.SetUnlimitedItems(unlimited);d.Game.SetNoItemCooldown(noCooldown);d.SetAutoSelectItems(all);
        if(!all)for(int i=0;i<3;i++){d.SelectItem(i,DuelItem.Recovery);d.SelectItem(i,DuelItem.Bullet);}
        for(int i=0;i<3;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,1d});return d;
    }
    static void Prep(DuelDirector d,int iteration=10,double now=2,PartyMiniGame game=PartyMiniGame.Countdown)
    {
        typeof(DuelDirector).GetMethod("ResetQuickItems",Hidden).Invoke(d,null);
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=game,Iteration=iteration,First=0,PhaseAt=now,StartedAt=now+5,ParticipantsMask=d.Game.AliveMask});
    }
    static void Hit(DuelDirector d,int target,DuelHitPart part=DuelHitPart.Body,DuelWeapon weapon=DuelWeapon.Taser,double now=7.2)
    {
        var volley=new DuelProjectileVolley(1,d.ForSeat(1).Life,d.Clock.Iteration,weapon,DuelGunplay.Eye(1),new AimVector(0,0,1),0,0,now);
        typeof(DuelDirector).GetMethod("ResolveProjectile",Hidden).Invoke(d,new object[]{volley,new DuelRayHit{Seat=target,Part=part},now});
    }
    static void Main()
    {
        Check(DuelLoadout.ItemCount==5&&DuelLoadout.Selectable(DuelItem.NoruFace)&&DuelLoadout.Complete(18),"jacket occupies one of two distinct loadout choices");
        var d=Party();Prep(d);
        for(int i=0;i<3;i++)Check(d.ForSeat(i).HasNoruFace&&!d.ForSeat(i).NoruFaceActive,"selected jacket granted inactive");
        Check(!d.UseItem(-1,DuelItem.NoruFace,10,3)&&!d.UseItem(6,DuelItem.NoruFace,10,3),"invalid seats rejected");
        Check(!d.UseItem(0,DuelItem.NoruFace,9,3)&&!d.UseItem(0,DuelItem.NoruFace,10,7),"iteration and item deadline enforced");
        Check(d.UseItem(0,DuelItem.NoruFace,10,3)&&d.UseItem(2,DuelItem.NoruFace,10,3),"shooter and non-shooter both may wear");
        Check(d.ForSeat(0).PreparedItem==(int)DuelItem.NoruFace+1&&!d.ForSeat(0).NoruFaceActive,"selection waits until deadline");
        d.Tick(7);
        Check(d.ForSeat(0).NoruFaceReadyIteration==13&&d.ForSeat(0).NoruFaceActive,"commit starts two-game cooldown");
        Check(!d.UseItem(1,DuelItem.NoruFace,10,7.1),"cannot wear during live shooting");
        Hit(d,0);Check(d.Game.HealthForSeat(0)==100&&!d.ForSeat(0).Stunned&&d.Game.LastHitMask==0,"protected shooter blocks damage, stun and hit feedback");
        Hit(d,2,DuelHitPart.Head);Check(d.Game.HealthForSeat(2)==100&&!d.ForSeat(2).Stunned,"protected non-shooter also immune at head");
        Hit(d,0,DuelHitPart.Body,DuelWeapon.Revolver);Check(d.Game.HealthForSeat(0)==50,"jacket does not block ordinary gunfire");
        Clock(d).Return(8);d.Tick(8.1);
        Check(!d.ForSeat(0).NoruFaceActive&&!d.ForSeat(2).NoruFaceActive&&d.ForSeat(0).HasNoruFace,"turn ending removes protection but retains equipment");
        Prep(d,11,9);Check(!d.UseItem(0,DuelItem.NoruFace,11,10),"first following game blocked");
        Prep(d,12,11);Check(!d.UseItem(0,DuelItem.NoruFace,12,12),"second following game blocked");
        Prep(d,13,13);Check(d.UseItem(0,DuelItem.NoruFace,13,14),"available again after two games");
        var absent=Party(all:false);Prep(absent);Check(!absent.ForSeat(0).HasNoruFace&&!absent.UseItem(0,DuelItem.NoruFace,10,3),"unselected jacket cannot be activated");
        var unused=Party();Prep(unused);unused.UseItem(0,DuelItem.NoruFace,10,3);unused.Tick(7);Clock(unused).Return(8);unused.Tick(8.1);
        Check(unused.Game.HealthForSeat(0)==100&&unused.ForSeat(0).HitAt<0&&!unused.ForSeat(0).NoruFaceActive&&unused.ForSeat(0).HasNoruFace&&unused.ForSeat(0).NoruFaceReadyIteration==13,"unused protection still removed and cooldown retained");
        Prep(unused,11,9);Check(!unused.UseItem(0,DuelItem.NoruFace,11,10),"no-hit use blocks next game");
        Prep(unused,12,11);Check(!unused.UseItem(0,DuelItem.NoruFace,12,12),"no-hit use blocks second game");
        Prep(unused,13,13);Check(unused.UseItem(0,DuelItem.NoruFace,13,14),"no-hit use recovers after two games");
        var cheat=Party(noCooldown:true);Prep(cheat);Check(cheat.UseItem(0,DuelItem.NoruFace,10,3),"no-cooldown selection");
        cheat.Tick(7);Clock(cheat).Return(8);cheat.Tick(8.1);Prep(cheat,11,9);
        Check(cheat.UseItem(0,DuelItem.NoruFace,11,10),"no-cooldown permits next game reuse");
        var quick=Party();Prep(quick,10,2,PartyMiniGame.QuickHands);
        Check(quick.UseItem(0,DuelItem.NoruFace,10,3)&&!quick.ForSeat(0).NoruFaceActive,"quick hands jacket selected");
        Check(!quick.UseItem(0,DuelItem.Taser,10,3.1)&&!quick.UseItem(0,DuelItem.Ink,10,3.1),"cannot also select held equipment");
        quick.Tick(7);Check(quick.ForSeat(0).QuickSpent&&quick.ForSeat(0).QuickItem==1,"quick jacket commits the one item slot");quick.Tick(10);Hit(quick,0,now:10.1);Check(quick.Game.HealthForSeat(0)==100&&!quick.ForSeat(0).Stunned,"jacket persists through countdown and quick-hands combat");
        Clock(quick).Return(11);quick.Tick(11.1);Check(!quick.ForSeat(0).NoruFaceActive,"quick-hands protection clears on exit");
        foreach(var part in new[]{DuelHitPart.Head,DuelHitPart.Body,DuelHitPart.Legs})
        {
            var free=Party();Prep(free);free.Tick(7);Hit(free,2,part);
            Check(free.Game.HealthForSeat(2)==95&&free.ForSeat(2).Stunned,"non-shooter receives fixed five damage at "+part);
            Check(!free.Look(2,120,0,false,7.3),"non-shooter camera input locked "+part);
            var a=Actions(free)[2];a.Weapon=DuelWeapon.Taser;a.TaserArmed=true;a.LoadedMask=1;Actions(free)[2]=a;float yaw=a.AimYaw,pitch=a.AimPitch;
            for(double time=7.5;time<9.5;time+=.1)typeof(DuelDirector).GetMethod("TickShock",Hidden).Invoke(free,new object[]{time});
            Check(free.ForSeat(2).ShotsFired==0&&free.ForSeat(2).Ammo==1&&free.ForSeat(2).AimYaw==yaw&&free.ForSeat(2).AimPitch==pitch,"non-shooter stun does not force shots or random aiming "+part);
            Clock(free).Return(10);free.Tick(10.1);
            Check(!free.ForSeat(2).Stunned&&free.ForSeat(2).StunnedAt<0&&free.Look(2,120,0,false,10.2),"shooting end removes stun and restores camera input "+part);
        }
        var shooter=Party();Prep(shooter);shooter.Tick(7);Hit(shooter,0);
        Check(shooter.Game.HealthForSeat(0)==95&&shooter.ForSeat(0).Stunned,"unprotected shooter retains shock effect");
        var before=shooter.ForSeat(0);for(double time=7.5;time<8.5;time+=.1)typeof(DuelDirector).GetMethod("TickShock",Hidden).Invoke(shooter,new object[]{time});
        Check(shooter.ForSeat(0).AimYaw==before.AimYaw&&shooter.ForSeat(0).AimPitch==before.AimPitch,"stunned shooter keeps aim without forced movement");
        var ai=Party(mode:DuelMode.AI);Prep(ai);ai.Tick(2.8);ai.Tick(7);Check(ai.ForSeat(1).NoruFaceActive&&ai.ForSeat(2).NoruFaceActive,"AI can wear before shooting");
        var equality=d.ForSeat(0);var other=equality;other.HasNoruFace=!other.HasNoruFace;Check(!equality.Equals(other),"ownership equality");
        other=equality;other.NoruFaceActive=!other.NoruFaceActive;Check(!equality.Equals(other),"active state equality");
        other=equality;other.NoruFaceReadyIteration++;Check(!equality.Equals(other),"cooldown equality");
        Console.WriteLine("PASS NoruFace and free taser: "+checks+" checks");
    }
}
