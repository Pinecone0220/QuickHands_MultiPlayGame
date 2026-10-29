using System;
using HelloWorld;
public static class ItemRulesTests
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    sealed class Fixed:Random{public override int Next(int min,int max)=>min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    static void GrantForTest(DuelDirector d,int seat){var states=(DuelActionState[])typeof(DuelDirector).GetField("actions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(d);states[seat].HasRecovery=states[seat].HasTaser=true;}
    static DuelDirector Room(bool items=true,bool grantOthers=true,bool unlimited=false)
    {var d=new DuelDirector(DuelMode.PvP,new Fixed());for(int i=0;i<3;i++)d.Join(i,0);d.Game.SetManualMiniGame(true);d.Game.SetStartWithItems(items);d.Game.SetUnlimitedItems(unlimited);d.Game.SetShotTime(0);for(int i=0;i<3;i++)d.Ready(i,0);d.Tick(3);if(items&&grantOthers){GrantForTest(d,1);GrantForTest(d,2);}return d;}
    static double Items(DuelDirector d,double now)
    {
        Check(d.ChooseMiniGame(PartyMiniGame.Countdown,d.Clock.Iteration,now),"host chooses countdown immediately");
        var c=d.Clock;double at=c.StartedAt+c.Target/100.0;
        for(int i=0;i<3;i++)if(RouletteRules.Has(d.Game.AliveMask,i))Check(d.StopClock(i,c.Iteration,at+i*.02,at+i*.02),"valid direct-selection mini input");
        now=d.Clock.PhaseAt+6.01;d.Tick(now);Check(d.Clock.Phase==ClockPhase.ItemPreparation,"selected mini reaches item turn");return now;
    }
    static bool FireAt(DuelDirector d,int actor,int target,DuelHitPart part,double now)
    {var a=d.ForSeat(actor);DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(actor),out float y,out float p);DuelGunplay.Sway(actor,a.Weapon,true,now,now,out float sy,out float sp);return d.Fire(actor,y-sy,p-sp,true,a.Version,now,now);}
    static double FinishMiss(DuelDirector d,double now)
    {d.Tick(d.Clock.StartedAt+.01);now=d.Clock.PhaseAt+.1;int actor=d.Clock.First;var a=d.ForSeat(actor);Check(d.Fire(actor,0,60,false,a.Version,now,now),"shooter may end turn with a miss");d.Tick(now+.6);Check(d.Clock.Phase==ClockPhase.Returning,"shot finishes item effects even without hit");now=d.Clock.PhaseAt+3.01;d.Tick(now);Check(d.Clock.Phase==ClockPhase.Selecting,"manual selection repeats after return");return now;}
    public static void Validate()
    {
        var cheat=Room(unlimited:true);
        Check(cheat.Game.UnlimitedItems&&!cheat.Game.SetUnlimitedItems(false),"unlimited items setting persists and cannot change during play");
        double ct=4;
        for(int turn=0;turn<3;turn++)
        {
            ct=Items(cheat,ct);
            cheat.Game.Shoot(0,1,DuelWeapon.SawedOff,DuelHitPart.Body);
            Check(cheat.UseItem(1,DuelItem.Recovery,cheat.Clock.Iteration,ct)&&cheat.Game.HealthForSeat(1)==75&&cheat.ForSeat(1).HasRecovery,"unlimited recovery retains inventory");
            Check(cheat.UseItem(1,DuelItem.Recovery,cheat.Clock.Iteration,ct+.01)&&cheat.Game.HealthForSeat(1)==100&&cheat.ForSeat(1).HasRecovery,"unlimited recovery can be reused in the same item turn");
            Check(!cheat.UseItem(1,DuelItem.Recovery,cheat.Clock.Iteration,ct+.02),"unlimited recovery still rejects full health");
            Check(cheat.UseItem(1,DuelItem.Taser,cheat.Clock.Iteration,ct)&&cheat.ForSeat(1).TaserReadyIteration==0,"unlimited taser has no cooldown every mini");
            Check(!cheat.UseItem(1,DuelItem.Taser,cheat.Clock.Iteration,ct)&&!cheat.UseItem(0,DuelItem.Taser,cheat.Clock.Iteration,ct),"unlimited taser still allows one activation and excludes shooter");
            ct=FinishMiss(cheat,ct);
            Check(!cheat.ForSeat(1).TaserArmed&&cheat.ForSeat(1).HasTaser&&cheat.ForSeat(1).HasRecovery,"turn effects expire without removing unlimited inventory");
        }
        var noGrant=Room(false,false,true);ct=Items(noGrant,4);
        Check(!noGrant.UseItem(1,DuelItem.Recovery,noGrant.Clock.Iteration,ct)&&!noGrant.UseItem(1,DuelItem.Taser,noGrant.Clock.Iteration,ct),"unlimited items never grants unowned items");
        var cheatQuick=Room(unlimited:true);Check(cheatQuick.ChooseMiniGame(PartyMiniGame.QuickHands,cheatQuick.Clock.Iteration,4)&&!cheatQuick.UseItem(1,DuelItem.Taser,cheatQuick.Clock.Iteration,4),"unlimited taser remains forbidden in quick hands");
        for(int i=0;i<240;i++)
        {var instant=Room(false);double time=3.123+i*.371;var game=PartyGames.At(i%PartyGames.Count);Check(instant.ChooseMiniGame(game,instant.Clock.Iteration,time)&&instant.Clock.Phase==(game==PartyMiniGame.QuickHands?ClockPhase.ItemPreparation:game==PartyMiniGame.Memory?ClockPhase.Memory:ClockPhase.Counting),"manual choice starts immediately across floating-point server times");}
        var hostOnly=Room(true,false);Check(hostOnly.ForSeat(0).HasRecovery&&hostOnly.ForSeat(0).HasTaser&&!hostOnly.ForSeat(1).HasRecovery&&!hostOnly.ForSeat(2).HasTaser,"starting items belong only to room host");
        Check(hostOnly.ChooseMiniGame(PartyMiniGame.Signal,hostOnly.Clock.Iteration,4),"host selects reaction game");var hc=hostOnly.Clock;double cue=hc.StartedAt+2.5;
        Check(hostOnly.StopClock(1,hc.Iteration,cue+.1,cue+.1)&&hostOnly.StopClock(0,hc.Iteration,cue+.4,cue+.4)&&hostOnly.StopClock(2,hc.Iteration,cue+.5,cue+.5),"host may lose a mini-game normally");
        double hi=hostOnly.Clock.PhaseAt+6.01;hostOnly.Tick(hi);Check(hostOnly.UseItem(0,DuelItem.Taser,hostOnly.Clock.Iteration,hi)&&!hostOnly.UseItem(2,DuelItem.Taser,hostOnly.Clock.Iteration,hi),"non-shooter host uses item while ungranted remote cannot");
        hostOnly.Tick(hostOnly.Clock.StartedAt+.01);double hs=hostOnly.Clock.PhaseAt+.1;Check(FireAt(hostOnly,0,1,DuelHitPart.Head,hs),"host's original inventory fires taser");hostOnly.Tick(hs+.3);Check(hostOnly.ForSeat(1).Stunned&&hostOnly.ForSeat(1).Health==95&&hostOnly.ForSeat(0).FiredWeapon==DuelWeapon.Taser,"host taser records true weapon and stuns remote winner");
        var d=Room();Check(d.Game.ManualMiniGame&&d.Clock.Phase==ClockPhase.Selecting,"manual room opens selection");d.Tick(300);Check(d.Clock.Phase==ClockPhase.Selecting,"manual choice waits indefinitely without roulette");
        Check(!d.ChooseMiniGame((PartyMiniGame)200,d.Clock.Iteration,300)&&!d.ChooseMiniGame(PartyMiniGame.Greed,d.Clock.Iteration-1,300),"invalid and stale host choices rejected");
        Check(!d.UseItem(1,DuelItem.Recovery,d.Clock.Iteration,300),"items forbidden during selection");double now=Items(d,301);int iteration=d.Clock.Iteration;
        Check(!d.ChooseMiniGame(PartyMiniGame.Greed,iteration,now),"cannot replace a running mini");
        Check(!d.UseItem(0,DuelItem.Recovery,iteration,now)&&d.ForSeat(0).HasRecovery,"full health does not consume recovery");
        d.Game.Shoot(0,1,DuelWeapon.SawedOff,DuelHitPart.Head);Check(d.Game.HealthForSeat(1)==25,"uniform shotgun baseline");
        Check(d.UseItem(1,DuelItem.Recovery,iteration,now)&&d.Game.HealthForSeat(1)==75&&!d.ForSeat(1).HasRecovery&&d.ForSeat(1).HealAt==now,"heal fifty before shooting, consume once");
        Check(!d.UseItem(1,DuelItem.Recovery,iteration,now)&&!d.UseItem(1,(DuelItem)200,iteration,now)&&!d.UseItem(2,DuelItem.Recovery,iteration-1,now),"duplicate invalid stale item requests rejected");
        Check(d.UseItem(2,DuelItem.Recovery,iteration,now)&&d.Game.HealthForSeat(2)==100,"healing clamps to max health");
        Check(!d.UseItem(0,DuelItem.Taser,iteration,now)&&d.UseItem(1,DuelItem.Taser,iteration,now)&&d.UseItem(2,DuelItem.Taser,iteration,now),"only non-shooters arm their tasers");
        Check(!d.UseItem(1,DuelItem.Taser,iteration,now)&&d.ForSeat(1).HasTaser&&d.ForSeat(1).TaserReadyIteration==iteration+2,"reusable taser gets two-game cooldown without consumption");
        d.Tick(d.Clock.StartedAt+.01);now=d.Clock.PhaseAt+.1;Check(d.ForSeat(1).Weapon==DuelWeapon.Taser&&d.ForSeat(1).Ammo==1&&d.ForSeat(0).Weapon==DuelWeapon.Revolver,"item equips one taser shot without replacing winner gun");
        Check(FireAt(d,1,0,DuelHitPart.Head,now),"taser owner fires directly");d.Tick(now+.3);
        Check(d.Game.HealthForSeat(0)==95&&d.ForSeat(0).Stunned&&d.Clock.Phase==ClockPhase.Shooting,"head contact deals five, stuns but does not end shooter turn HP="+d.Game.HealthForSeat(0)+" stun="+d.ForSeat(0).Stunned+" phase="+d.Clock.Phase+" target="+d.Game.LastTarget);
        Check(!d.Look(0,90,0,true,now+.31)&&!d.Fire(0,90,0,true,d.ForSeat(0).Version,now+.31,now+.31),"stunned shooter cannot control aim or voluntarily fire");
        Check(!d.Fire(1,0,0,false,d.ForSeat(1).Version,now+.31,now+.31),"taser has only one shot");
        d.Tick(now+.6);Check(d.ForSeat(0).Ammo==1&&d.ForSeat(0).FiredAt<0,"stun does not consume ammunition or force a shot");d.Tick(now+1.2);
        Check(d.Clock.Phase==ClockPhase.Returning&&!d.ForSeat(0).Stunned&&!d.ForSeat(2).TaserArmed&&d.ForSeat(2).Weapon!=DuelWeapon.Taser,"stun and unused taser expire after shot");
        now=d.Clock.PhaseAt+3.01;d.Tick(now);now=Items(d,now+.1);Check(!d.UseItem(1,DuelItem.Taser,d.Clock.Iteration,now),"next mini remains in cooldown");now=FinishMiss(d,now);now=Items(d,now+.1);Check(d.UseItem(1,DuelItem.Taser,d.Clock.Iteration,now),"two games later taser becomes reusable, same mini may repeat");
        var empty=Room(false);now=Items(empty,4);Check(!empty.ForSeat(1).HasRecovery&&!empty.ForSeat(1).HasTaser&&!empty.UseItem(1,DuelItem.Taser,empty.Clock.Iteration,now),"disabled starting items grants no inventory");
        var trade=Room();now=Items(trade,4);Check(trade.UseItem(1,DuelItem.Taser,trade.Clock.Iteration,now),"trade fixture arms taser");trade.Game.Shoot(2,0,DuelWeapon.SawedOff,DuelHitPart.Body);for(int i=0;i<4;i++)trade.Game.Shoot(2,0,DuelWeapon.Taser,DuelHitPart.Body);trade.Tick(trade.Clock.StartedAt+.01);now=trade.Clock.PhaseAt+.1;
        Check(FireAt(trade,0,2,DuelHitPart.Head,now)&&FireAt(trade,1,0,DuelHitPart.Head,now+.01),"both projectiles launched before contact");trade.Tick(now+.12);Check(trade.ForSeat(0).DiedAt>=0&&trade.Clock.Phase==ClockPhase.Shooting,"fatal taser does not cancel already airborne winner bullet");trade.Tick(now+.5);Check(trade.ForSeat(2).DiedAt>=0&&trade.Clock.Phase==ClockPhase.Returning&&!trade.ForSeat(1).TaserArmed,"posthumous impact settles then item turn ends");
        var quick=Room();Check(quick.ChooseMiniGame(PartyMiniGame.QuickHands,quick.Clock.Iteration,4),"direct quick selection");Check(quick.Clock.Phase==ClockPhase.ItemPreparation&&!quick.UseItem(1,DuelItem.Taser,quick.Clock.Iteration,4),"quick participants are all shooters, taser unavailable");quick.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body);Check(quick.UseItem(1,DuelItem.Recovery,quick.Clock.Iteration,4),"quick supports recovery before combat");
        foreach(var part in new[]{DuelHitPart.Head,DuelHitPart.Body,DuelHitPart.Legs}){var g=new RouletteRules(DuelMode.PvP);g.Join(0);g.Join(1);g.SetReady(0);g.SetReady(1);g.Start();g.Shoot(0,1,DuelWeapon.Taser,part);Check(g.HealthForSeat(1)==95,"every taser region deals five");}
        DuelGunplay.Sway(1,DuelWeapon.SawedOff,true,1,0,out float sawY,out float sawP);DuelGunplay.Sway(1,DuelWeapon.Taser,true,1,0,out float taserY,out float taserP);Check(Math.Abs(taserY-sawY*.1)<.00001&&Math.Abs(taserP-sawP*.1)<.00001&&DuelGunplay.AdsFov(DuelWeapon.Taser)==DuelGunplay.BaseFov,"taser sway is ten percent without zoom");
        Check(!d.SelectWeapon(1,DuelWeapon.Taser,now)&&!WeaponRules.IsSelectable(DuelWeapon.Taser),"item weapon cannot be chosen as regular gun");
        foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})
        {
            var ai=new DuelDirector(DuelMode.AI,new Fixed(),difficulty);for(int i=0;i<3;i++)ai.Join(i,0);ai.Game.SetManualMiniGame(true);ai.Game.SetStartWithItems(true);ai.Game.SetShotTime(0);for(int i=0;i<3;i++)ai.Ready(i,0);ai.Tick(3);GrantForTest(ai,1);GrantForTest(ai,2);
            Check(ai.ChooseMiniGame(PartyMiniGame.Countdown,ai.Clock.Iteration,4),"AI room uses manual catalogue");var ac=ai.Clock;double stop=ac.StartedAt+ac.Target/100.0;Check(ai.StopClock(0,ac.Iteration,stop,stop),"human banks exact AI-room time");ai.Tick(stop+.1);
            ai.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body);ai.Tick(ai.Clock.PhaseAt+6.01);ai.Tick(ai.Clock.PhaseAt+.8);
            Check(ai.ForSeat(1).Health==100&&!ai.ForSeat(1).HasRecovery&&ai.ForSeat(1).TaserArmed&&ai.ForSeat(2).TaserArmed,"all AI difficulties heal and activate eligible item");
            ai.Tick(ai.Clock.StartedAt+.01);ai.Tick(ai.Clock.PhaseAt+.41);Check(ai.ForSeat(1).ShotsFired==1&&ai.ForSeat(2).ShotsFired==1,"both AI tasers use server fire path");
        }
        Console.WriteLine("PASS manual selection and items: "+checks+" checks");
    }
}
