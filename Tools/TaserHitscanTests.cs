using System;
using System.Reflection;
using HelloWorld;
class TaserHitscanTests
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);
    static DuelActionState[] Actions(DuelDirector d)=>(DuelActionState[])typeof(DuelDirector).GetField("actions",F).GetValue(d);
    static DuelDirector Party(int players=3)
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(67));for(int i=0;i<players;i++)d.Join(i,0);d.Game.SetShotTime(0);d.SetAutoSelectItems(true);for(int i=0;i<players;i++)d.Ready(i,0);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,1d});
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=d.Game.AliveMask,PhaseAt=10,StartedAt=15});
        Check(d.UseItem(0,DuelItem.Taser,1,10),"quick taser selected through item rules");
        typeof(DuelDirector).GetMethod("CommitPreparedItems",F).Invoke(d,new object[]{15d});
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=d.Game.AliveMask,PhaseAt=10.1,StartedAt=10.1});
        var a=d.ForSeat(0);Check(d.SwitchQuickItem(0,true,1,a.Life,a.QuickItemVersion,10.2),"taser equipped through inventory");return d;
    }
    static bool FireAt(DuelDirector d,int target,double now=10.3)
    {DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(0),out float y,out float p);return d.Fire(0,y,p,true,d.ForSeat(0).Version,now,now);}
    static void LowHealth(DuelDirector d,int target)
    {d.Game.Shoot(0,target,DuelWeapon.SawedOff,DuelHitPart.Body,true);for(int i=0;i<4;i++)d.Game.Shoot(0,target,DuelWeapon.Taser,DuelHitPart.Body,true);Check(d.ForSeat(target).Health==5,"five-health victim");}
    static void Main()
    {
        Check(WeaponRules.IsHitscan(DuelWeapon.Taser)&&!WeaponRules.IsHitscan(DuelWeapon.Revolver),"only taser is instant");
        Check(!WeaponRules.SupportsAds(DuelWeapon.Taser)&&DuelGunplay.Spread(DuelWeapon.Taser,false)==0&&DuelGunplay.Spread(DuelWeapon.Taser,true)==0,"item weapon has accurate normal fire only");
        var d=Party();int version=d.ForSeat(0).Version;Check(FireAt(d,1),"valid shot accepted");
        Check(d.ForSeat(1).Health==95&&d.ForSeat(1).Stunned&&d.ForSeat(1).StunnedAt==10.3,"damage and shock happen inside Fire without Tick");
        Check(!d.ForSeat(0).FiredAds&&d.ForSeat(0).FiredSpread==0&&d.ForSeat(0).FiredTarget==1,"normal-fire contact recorded");
        var flights=(DuelProjectileVolley[])typeof(DuelDirector).GetField("projectiles",F).GetValue(d);Check(flights[0]==null,"taser never enters projectile queue");
        Check(d.ForSeat(0).Ammo==0&&d.ForSeat(0).QuickSpent&&d.ForSeat(0).TaserReadyIteration==4,"shot consumption and two-game cooldown preserved");
        Check(!d.Fire(0,0,0,false,version,10.31,10.31),"duplicate request cannot apply damage twice");d.Tick(11.601);Check(!d.ForSeat(1).Stunned,"shock expires after 1.3 seconds");
        var shield=Party();Actions(shield)[1].NoruFaceActive=true;Check(FireAt(shield,1)&&shield.ForSeat(1).Health==100&&!shield.ForSeat(1).Stunned,"padding immediately blocks damage and shock");Check(shield.ForSeat(0).Ammo==0,"immune hit still consumes shot");
        var miss=Party();Check(miss.Fire(0,0,55,false,miss.ForSeat(0).Version,10.3,10.3),"miss still fires");Check(miss.Game.Shots==1&&miss.ForSeat(1).Health==100&&miss.ForSeat(0).FiredTarget<0,"miss is resolved immediately without target attraction");
        var far=DuelGunplay.TraceHitscan(0,63,new AimVector(0,10,0),new AimVector(1,0,0));Check(far.Seat<0&&far.Prop==DuelTableProp.None&&far.Distance==DuelGunplay.TaserRange,"open-space trace stops at range");
        for(int actor=0;actor<6;actor++)for(int target=0;target<6;target++)if(actor!=target)
        {
            int separation=Math.Abs(actor-target);bool adjacent=separation==1||separation==5;
            DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor),out float hy,out float hp);var ray=DuelGunplay.Direction(hy,hp);
            var hit=DuelGunplay.TraceHitscan(actor,1<<target,DuelGunplay.MuzzleOrigin(actor,DuelWeapon.Taser,false,ray),ray);
            Check(adjacent?hit.Seat==target:hit.Seat<0,"only physical neighbours within range "+actor+"/"+target);
        }
        var aiTargets=Party();var pick=typeof(DuelDirector).GetMethod("RandomTarget",F);
        for(int i=0;i<8;i++)Check((int)pick.Invoke(aiTargets,new object[]{0,true})==1,"AI taser avoids the distant target");
        aiTargets.Game.Eliminate(2);Check((int)pick.Invoke(aiTargets,new object[]{0,true})==-1,"AI waits when no target is within range");
        var ai=new DuelDirector(DuelMode.AI,new Random(9));ai.Join(0,0);ai.Join(1,0);ai.Join(3,0);ai.Game.SetShotTime(0);ai.SetAutoSelectItems(true);ai.Ready(0,0);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(ai,new object[]{DuelStage.Racing,1d});
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(ai),new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=11,PhaseAt=10,StartedAt=15});
        Check(ai.UseItem(1,DuelItem.Taser,1,10),"AI fixture selects taser");
        typeof(DuelDirector).GetMethod("CommitPreparedItems",F).Invoke(ai,new object[]{15d});
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock(ai),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=11,StartedAt=10.1});
        var bot=ai.ForSeat(1);Check(ai.SwitchQuickItem(1,true,1,bot.Life,bot.QuickItemVersion,10.2),"AI fixture equips taser");ai.Game.Eliminate(1);
        typeof(DuelDirector).GetMethod("TickQuickItemAi",F).Invoke(ai,new object[]{10.8d});
        Check(!ai.ForSeat(1).QuickEquipped&&ai.ForSeat(1).Weapon!=DuelWeapon.Taser,"AI holsters unreachable taser and resumes normal gun");
        var distant=Party();Check(FireAt(distant,2)&&distant.ForSeat(2).Health==100&&!distant.ForSeat(2).Stunned,"next-nearest player cannot be damaged or shocked");
        var props=Party();var propPoint=DuelGunplay.Local(0,.66f,1.1f,.82f-DuelGunplay.SeatAdvance);DuelGunplay.Angles(propPoint-DuelGunplay.Eye(0),out float py,out float pp);
        Check(props.Fire(0,py,pp,false,props.ForSeat(0).Version,10.3,10.3),"prop shot accepted");Check(props.Tabletop.Broken(0,DuelTableProp.Bottle)&&props.ForSeat(1).Health==100,"prop blocks and breaks instantly");
        var fatal=Party(2);LowHealth(fatal,1);Actions(fatal)[1].LivesRemaining=1;Check(FireAt(fatal,1),"fatal taser accepted");Check(fatal.ForSeat(1).LivesRemaining==0&&fatal.ForSeat(1).DiedAt==10.3&&fatal.Game.Phase==DuelPhase.Finished&&fatal.Game.Winner==0,"last life and party victory settle immediately");
        var trade=Party();LowHealth(trade,1);var a=Actions(trade);a[1].Weapon=DuelWeapon.Revolver;a[1].Action=DuelAction.Aim;a[1].LoadedMask=1;
        DuelGunplay.Angles(DuelGunplay.Point(0,DuelHitPart.Head)-DuelGunplay.Eye(1),out float y,out float p);
        Check(trade.Fire(1,y,p,false,trade.ForSeat(1).Version,10.3,10.3),"ordinary bullet already airborne");Check(FireAt(trade,1,10.31)&&trade.ForSeat(1).DiedAt==10.31&&trade.Clock.Phase==ClockPhase.QuickHands,"instant lethal hit preserves pending bullet");
        trade.Tick(10.7);Check(trade.ForSeat(0).DiedAt>=0,"dead shooter's existing bullet still lands");
        Console.WriteLine("PASS taser hitscan: "+checks+" checks");
    }
}
