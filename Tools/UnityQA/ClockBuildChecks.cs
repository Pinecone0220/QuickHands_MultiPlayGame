using System;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
public static class ClockBuildChecks
{
    public static void Validate()
    {
        ValidateTabletop();
        ValidateMemory();
        var s=new DuelSnapshot{PartyAliveMask=62,Clock=new ClockState{Phase=ClockPhase.QuickResults,MiniGame=PartyMiniGame.QuickHands,EliminatedMask=1,Iteration=4,ParticipantsMask=63,Target=1500,First=5,Last=0,StartedAt=17.5,PhaseAt=33.5,HideAfter=2.89,Seat5=new ClockEntry{Centiseconds=1499,Error=1,Rank=1,Dice=6,Rolls=3}},Seat5=new DuelActionState{ClockWins=3,ClockStops=4,ClockErrorTotal=11,Life=4,SelectedWeapon=DuelWeapon.SawedOff,DeathCause=DuelDeathCause.ClockTimeout},Results=new DuelRoundResults{Seat5=new DuelResultEntry{ClockWins=3,ClockStops=4,ClockErrorTotal=11}}};
        using(var w=new FastBufferWriter(8192,Allocator.Temp))
        {w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy))throw new Exception("clock snapshot roundtrip");}}
        s.ManualMiniGame=s.UnlimitedItems=true;s.Seat5.HasRecovery=s.Seat5.HasTaser=s.Seat5.TaserArmed=s.Seat5.Stunned=true;s.Seat5.TaserReadyIteration=9;s.Seat5.HealAt=12.3;s.Seat5.StunnedAt=15.6;s.Seat5.Weapon=s.Seat5.FiredWeapon=DuelWeapon.Taser;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy))throw new Exception("item/manual selection snapshot roundtrip");}}
        var itemChange=s;itemChange.ManualMiniGame=false;if(s.Equals(itemChange))throw new Exception("manual selection equality");
        itemChange=s;itemChange.UnlimitedItems=false;if(s.Equals(itemChange))throw new Exception("unlimited item setting equality");
        itemChange=s;itemChange.Seat5.Stunned=false;if(s.Equals(itemChange))throw new Exception("stun equality");
        itemChange=s;itemChange.Seat5.TaserReadyIteration++;if(s.Equals(itemChange))throw new Exception("item cooldown equality");
        var history=new DuelDamageReport{Round=3,Life=2};for(int i=0;i<30;i++)history.Add(new DuelDamageEntry{Attacker=new FixedString64Bytes(new string('a',60)),Weapon=DuelWeapon.Taser,Part=DuelHitPart.Head,Damage=5,Shot=i,Fatal=i==29});
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(history);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelDamageReport copy);if(!history.Equals(copy)||copy.Count!=30||!copy.ForHit(29).Fatal)throw new Exception("healed life taser history retains fatal final contact");}}
        var changed=s;changed.Clock.Seat5.Dice=5;if(s.Equals(changed))throw new Exception("clock equality dice");
        var pre=s;pre.Clock.Phase=ClockPhase.QuickCountdown;pre.Clock.PhaseAt=40;pre.Clock.StartedAt=43;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(pre);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!pre.Equals(copy))throw new Exception("quick countdown deadline roundtrip");}}
        if(s.Equals(pre))throw new Exception("quick countdown equality");
        pre.Clock.Phase=ClockPhase.ItemPreparation;pre.Clock.PhaseAt=35;pre.Clock.StartedAt=40;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(pre);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!pre.Equals(copy))throw new Exception("item preparation deadline roundtrip");}}
        changed=pre;changed.Clock.StartedAt++;if(pre.Equals(changed))throw new Exception("item deadline equality");
        changed=s;changed.Seat5.ClockWins++;if(s.Equals(changed))throw new Exception("action equality stats");
        changed=s;changed.Results.Seat5.ClockErrorTotal++;if(s.Equals(changed))throw new Exception("results equality stats");
        changed=s;changed.Clock.EliminatedMask=2;if(s.Equals(changed))throw new Exception("timeout mask equality");
        changed=s;changed.Seat5.DeathCause=DuelDeathCause.Gunshot;if(s.Equals(changed))throw new Exception("death cause equality");
        changed=s;changed.Seat5.Life++;if(s.Equals(changed))throw new Exception("revival life equality");
        changed=s;changed.PartyAliveMask=60;if(s.Equals(changed))throw new Exception("persistent party mask equality");
        changed=s;changed.Seat5.SelectedWeapon=DuelWeapon.Revolver;if(s.Equals(changed))throw new Exception("selected weapon equality");
        s.Seat5.FiredYaw=143.75f;s.Seat5.FiredPitch=-3.5f;s.Seat5.FiredSpread=6;s.Seat5.FiredAliveMask=63;s.Seat5.FiredSeed=457;s.Seat5.FiredAds=true;s.Seat5.FiredOrigin=new AimVector(1,2,3);s.ShotTimeLimit=7;s.StartWithItems=true;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy))throw new Exception("cosmetic shot ray roundtrip");}}
        changed=s;changed.Seat5.FiredYaw++;if(s.Equals(changed))throw new Exception("shot ray equality");
        changed=s;changed.Seat5.FiredPitch++;if(s.Equals(changed))throw new Exception("shot pitch equality");
        changed=s;changed.Seat5.FiredSpread++;if(s.Equals(changed))throw new Exception("shot cone equality");
        changed=s;changed.Seat5.FiredAliveMask=62;if(s.Equals(changed))throw new Exception("shot alive mask equality");
        changed=s;changed.Seat5.FiredSeed++;if(s.Equals(changed))throw new Exception("shot seed equality");
        changed=s;changed.Seat5.FiredOrigin.X++;if(s.Equals(changed))throw new Exception("shot origin equality");
        changed=s;changed.Seat5.FiredAds=false;if(s.Equals(changed))throw new Exception("shot ads equality");
        changed=s;changed.ShotTimeLimit=0;if(s.Equals(changed))throw new Exception("room limit equality");
        changed=s;changed.StartWithItems=false;if(s.Equals(changed))throw new Exception("start items setting equality");
        s.Seat5.LoadedMask=21;s.Seat5.SpentMask=10;s.Seat5.NextChamber=3;s.Seat5.ShootingStartedAt=42.5;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy)||copy.Seat5.Ammo!=3)throw new Exception("revolver chamber roundtrip");}}
        changed=s;changed.Seat5.LoadedMask=20;if(s.Equals(changed))throw new Exception("loaded chamber equality");
        changed=s;changed.Seat5.SpentMask=8;if(s.Equals(changed))throw new Exception("spent chamber equality");
        changed=s;changed.Seat5.NextChamber=4;if(s.Equals(changed))throw new Exception("cylinder cursor equality");
        changed=s;changed.Seat5.ShootingStartedAt++;if(s.Equals(changed))throw new Exception("partial shot window equality");
        s.Seat5.LivesRemaining=2;s.Seat5.ReviveAt=48.75;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy)||copy.Seat5.LivesRemaining!=2||copy.Seat5.ReviveAt!=48.75)throw new Exception("lives recovery roundtrip");}}
        changed=s;changed.Seat5.LivesRemaining--;if(s.Equals(changed))throw new Exception("remaining lives equality");
        changed=s;changed.Seat5.ReviveAt++;if(s.Equals(changed))throw new Exception("recovery deadline equality");
        s.Clock.MiniGame=PartyMiniGame.Signal;s.Clock.Signal=MiniSignal.Go;s.Clock.SignalAt=61.125;s.Clock.Points=189;
        s.Clock.Seat5.Points=125;s.Clock.Seat5.Failure=MiniFailure.Burst;
        using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy)||copy.Clock.SignalAt!=61.125||copy.Clock.Seat5.Failure!=MiniFailure.Burst)throw new Exception("signal greed snapshot roundtrip");}}
        changed=s;changed.Clock.Signal=MiniSignal.Fake;if(s.Equals(changed))throw new Exception("signal equality");
        changed=s;changed.Clock.SignalAt++;if(s.Equals(changed))throw new Exception("cue time equality");
        changed=s;changed.Clock.Points++;if(s.Equals(changed))throw new Exception("live greed score equality");
        changed=s;changed.Clock.Seat5.Points++;if(s.Equals(changed))throw new Exception("banked score equality");
        changed=s;changed.Clock.Seat5.Failure=MiniFailure.None;if(s.Equals(changed))throw new Exception("mini failure equality");
        var damage=new DuelDamageReport{Round=1,Life=4,Count=1,Hit0=new DuelDamageEntry{Attacker=new FixedString64Bytes("actor"),Damage=50}};
        using(var w=new FastBufferWriter(2048,Allocator.Temp)){w.WriteNetworkSerializable(damage);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelDamageReport copy);if(!copy.Equals(damage))throw new Exception("life damage report roundtrip");}}
        var result=new DuelRoundResults{Round=1,ParticipantsMask=3,Winner=0,Duration=60,Seat0=new DuelResultEntry{Name=new FixedString64Bytes("clock"),ClockWins=3,ClockStops=3,ClockErrorTotal=6}};
        if(!DuelHighlights.Select(result).Contains("0.02초"))throw new Exception("factual clock highlight");
        result.Winner=-1;if(!result.IsValid || !DuelHighlights.Select(result).Contains("모두 탈락"))throw new Exception("all-timeout result");
        Debug.Log("PASS clock snapshot, equality and highlight checks");
    }
    static void ValidateMemory()
    {
        var s=new DuelSnapshot{Clock=new ClockState{Phase=ClockPhase.Memory,MiniGame=PartyMiniGame.Memory,Iteration=7,Memory=new ArrowMemoryState{Phase=ArrowMemoryPhase.Results,Round=4,Length=6,ActiveMask=21,RoundMask=31,EliminatedMask=10,AnswerSeed=781253,Winner=-1,PhaseAt=12.25,InputEndsAt=22.25}}};
        for(int seat=0;seat<6;seat++)s.Clock.Memory.Set(seat,new ArrowMemoryEntry{Count=seat+1,Errors=seat,Dice=6-seat,Rolls=seat+2,Rank=6-seat,Locked=seat%2==0});
        MemoryRoundtrip(s,"memory result entries and answer");
        var c=s;c.Clock.Memory.Phase=ArrowMemoryPhase.Input;MemoryChanged(s,c,"phase");
        c=s;c.Clock.Memory.Round++;MemoryChanged(s,c,"round");
        c=s;c.Clock.Memory.Length++;MemoryChanged(s,c,"length");
        c=s;c.Clock.Memory.ActiveMask^=1;MemoryChanged(s,c,"active mask");
        c=s;c.Clock.Memory.RoundMask^=1;MemoryChanged(s,c,"round mask");
        c=s;c.Clock.Memory.EliminatedMask^=1;MemoryChanged(s,c,"eliminated mask");
        c=s;c.Clock.Memory.AnswerSeed++;MemoryChanged(s,c,"answer seed");
        c=s;c.Clock.Memory.Winner=2;MemoryChanged(s,c,"winner");
        c=s;c.Clock.Memory.PhaseAt++;MemoryChanged(s,c,"phase deadline");
        c=s;c.Clock.Memory.InputEndsAt++;MemoryChanged(s,c,"input deadline");
        for(int seat=0;seat<6;seat++)
        {
            c=s;var e=c.Clock.Memory.ForSeat(seat);e.Count++;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"count "+seat);
            c=s;e=c.Clock.Memory.ForSeat(seat);e.Errors++;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"errors "+seat);
            c=s;e=c.Clock.Memory.ForSeat(seat);e.Dice=e.Dice%6+1;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"dice "+seat);
            c=s;e=c.Clock.Memory.ForSeat(seat);e.Rolls++;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"rolls "+seat);
            c=s;e=c.Clock.Memory.ForSeat(seat);e.Rank++;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"rank "+seat);
            c=s;e=c.Clock.Memory.ForSeat(seat);e.Locked=!e.Locked;c.Clock.Memory.Set(seat,e);MemoryChanged(s,c,"locked "+seat);
        }
        // Hidden phases serialize explicit empty values instead of omitting the previous answer/results.
        var hidden=s;hidden.Clock.Memory.Phase=ArrowMemoryPhase.Input;hidden.Clock.Memory.AnswerSeed=0;
        for(int seat=0;seat<6;seat++)hidden.Clock.Memory.Set(seat,new ArrowMemoryEntry{Errors=-1});
        using(var w=new FastBufferWriter(8192,Allocator.Temp))
        {w.WriteNetworkSerializable(hidden);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!hidden.Equals(copy)||copy.Clock.Memory.AnswerSeed!=0)throw new Exception("memory hidden answer roundtrip");for(int seat=0;seat<6;seat++){var e=copy.Clock.Memory.ForSeat(seat);if(e.Count!=0||e.Errors!=-1||e.Locked)throw new Exception("memory private input leak "+seat);}}}
        s.Clock.Memory.Phase=ArrowMemoryPhase.Finished;s.Clock.Memory.Winner=4;MemoryRoundtrip(s,"memory finished winner");
        Debug.Log("PASS memory snapshot, six private/result entries and equality checks");
    }
    static void MemoryChanged(DuelSnapshot before,DuelSnapshot after,string field)
    {if(before.Equals(after))throw new Exception("memory equality "+field);}
    static void MemoryRoundtrip(DuelSnapshot s,string label)
    {using(var w=new FastBufferWriter(8192,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy))throw new Exception(label+" roundtrip");}}}
    static void ValidateTabletop()
    {
        var s=new DuelSnapshot{Tabletop=new DuelTabletopState{BottlesBrokenMask=9,GlassesBrokenMask=20,FilledMask=2,DrankMask=32},Seat0=new DuelActionState{FiredBottleMask=10,FiredGlassMask=17}};
        for(int seat=0;seat<6;seat++)s.Tabletop.SetSeat(seat,new DuelTableEntry{Action=(DuelDrinkAction)(seat%4),StartedAt=20+seat*.25,BottleBrokenAt=30+seat*.5,GlassBrokenAt=40+seat*.75});
        using(var w=new FastBufferWriter(8192,Allocator.Temp))
        {w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);if(!s.Equals(copy))throw new Exception("table props/action/break times/shot masks roundtrip");}}
        for(int seat=0;seat<6;seat++)
        {var changed=s;var e=changed.Tabletop.ForSeat(seat);e.StartedAt++;changed.Tabletop.SetSeat(seat,e);if(s.Equals(changed))throw new Exception("table entry equality seat "+seat);}
        var c=s;c.Tabletop.BottlesBrokenMask^=1;if(s.Equals(c))throw new Exception("bottle mask equality");
        c=s;c.Tabletop.GlassesBrokenMask^=1;if(s.Equals(c))throw new Exception("glass mask equality");
        c=s;c.Tabletop.FilledMask^=1;if(s.Equals(c))throw new Exception("drink fill equality");
        c=s;c.Tabletop.DrankMask^=1;if(s.Equals(c))throw new Exception("drank mask equality");
        c=s;c.Seat0.FiredBottleMask^=1;if(s.Equals(c))throw new Exception("fired bottle mask equality");
        c=s;c.Seat0.FiredGlassMask^=1;if(s.Equals(c))throw new Exception("fired glass mask equality");
        Debug.Log("PASS tabletop snapshot, six activity entries, shot props and equality checks");
    }
}
