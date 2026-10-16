using System;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
// Run Validate from a Unity editor validation build; uses the actual game result and serializer types.
public static class DuelHighlightsChecks
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static DuelRoundResults Base()
    {
        return new DuelRoundResults{Round=1,ParticipantsMask=15,Winner=0,Duration=35,
            Seat0=new DuelResultEntry{Name=new FixedString64Bytes("승자"),Kills=1,DamageDealt=100,ShotsFired=1,RemainingHealth=50,CorrectInputs=36,Mistakes=1},
            Seat1=new DuelResultEntry{Name=new FixedString64Bytes("대상"),CorrectInputs=36},
            Seat2=new DuelResultEntry{Name=new FixedString64Bytes("다른사람")},Seat3=new DuelResultEntry{Name=new FixedString64Bytes("참가자")}};
    }
    public static void Validate()
    {
        var d=Base();Check(DuelHighlights.Select(d).Contains("총 1발"),"neutral ordinary round");
        foreach(int amount in new[]{0,25,50,75,99}){d=Base();d.Seat1.DamageDealt=amount;Check(!DuelHighlights.Select(d).Contains("대상"),"ordinary nonlethal damage not mocked "+amount);}
        foreach(int amount in new[]{100,125,149,150,175,300}){d=Base();d.Seat1.DamageDealt=amount;Check(DuelHighlights.Select(d).Contains(amount+" 피해")&&DuelHighlights.Select(d).Contains("0킬"),"zero kills max health100 threshold "+amount);}
        d=Base();d.Seat1.DamageDealt=100;d.Seat1.Kills=1;Check(!DuelHighlights.Select(d).Contains("대상"),"not zero kill if one kill");
        d=Base();d.Seat1.DamageDealt=100;d.Round=2;Check(DuelHighlights.Select(d).Contains("처치 0명"),"wording variety stays factual");
        d=Base();d.Seat1.Eliminated=true;d.Seat1.ShotsFired=2;Check(!DuelHighlights.Select(d).Contains("대상"),"two misses not special");d.Seat1.ShotsFired=3;Check(DuelHighlights.Select(d).Contains("3발")&&DuelHighlights.Select(d).Contains("피해 0"),"three shots zero damage");
        d.Seat1.DamageDealt=25;Check(!DuelHighlights.Select(d).Contains("피해 0"),"hit disqualifies all misses");
        d=Base();d.Seat1.Kills=2;d.Seat1.DamageDealt=200;d.Seat1.BestMultiKill=2;Check(DuelHighlights.Select(d).Contains("한 발")&&DuelHighlights.Select(d).Contains("2명"),"double kill fact");
        d.Seat1.BestMultiKill=3;d.Seat1.Kills=3;d.Seat1.DamageDealt=300;d.ParticipantsMask=63;d.Seat2.DamageDealt=150;Check(DuelHighlights.Select(d).Contains("3명"),"triple outranks less unusual event");
        d=Base();d.Seat0.RemainingHealth=15;Check(!DuelHighlights.Select(d).Contains("체력 15"),"ordinary low HP excluded");d.Seat0.RemainingHealth=5;Check(DuelHighlights.Select(d).Contains("체력 5"),"rare clutch");
        d=Base();d.Seat1.Eliminated=true;d.Seat1.CorrectInputs=0;Check(!DuelHighlights.Select(d).Contains("첫 숫자"),"normal length idle death not special");d.Duration=9;Check(DuelHighlights.Select(d).Contains("첫 숫자"),"fast zero input death");d.Seat1.Mistakes=1;Check(!DuelHighlights.Select(d).Contains("입력 기록 0회"),"mistake counts as input");
        d=Base();d.Seat1.Eliminated=true;d.Seat1.Mistakes=9;Check(!DuelHighlights.Select(d).Contains("숫자와"),"nine mistakes excluded");d.Seat1.Mistakes=10;d.Seat1.CorrectInputs=200;Check(!DuelHighlights.Select(d).Contains("숫자와"),"many inputs reasonable accuracy not special");d.Seat1.CorrectInputs=30;Check(DuelHighlights.Select(d).Contains("10번"),"many mistakes low accuracy");
        d=Base();d.Seat0.Mistakes=0;Check(!DuelHighlights.Select(d).Contains("틀리지"),"one kill perfect input ordinary");d.Seat0.Kills=3;d.Seat0.DamageDealt=300;Check(DuelHighlights.Select(d).Contains("3킬")&&DuelHighlights.Select(d).Contains("틀리지"),"multi kill flawless inputs");
        d=Base();d.Seat0.Kills=3;d.Seat0.DamageDealt=300;d.Seat0.RemainingHealth=100;Check(DuelHighlights.Select(d).Contains("체력 100"),"three kills full health");
        d=Base();d.Seat1.Kills=2;d.Seat1.DamageDealt=70;Check(DuelHighlights.Select(d).Contains("70 피해로 2킬"),"unusually cheap finishes");d.Seat1.DamageDealt=71;Check(!DuelHighlights.Select(d).Contains("마무리만큼"),"finish threshold excludes ordinary amount");
        d=Base();d.Seat0.ShotsFired=d.Seat0.Kills=d.Seat0.DamageDealt=0;d.Seat1.Kills=2;d.Seat1.DamageDealt=200;d.Seat1.ShotsFired=2;Check(DuelHighlights.Select(d).Contains("한 발도 쏘지"),"combat happened and non-shooting survivor");d.Seat1.Kills=0;Check(!DuelHighlights.Select(d).Contains("한 발도 쏘지"),"quiet disconnect lobby not special");
        d=Base();d.Seat0.Kills=3;d.Seat0.DamageDealt=300;d.Duration=11;Check(DuelHighlights.Select(d).Contains("4명이")&&DuelHighlights.Select(d).Contains("11.0초"),"fast four player combat");d.Seat0.Kills=1;Check(!DuelHighlights.Select(d).Contains("한 명만"),"early disconnect not called fast fight");
        d=Base();d.Duration=120;d.Seat0.ShotsFired=10;Check(DuelHighlights.Select(d).Contains("120.0초 동안 10발"),"long gunfight only if shooting happened");
        d=Base();d.Seat5.Name=new FixedString64Bytes("유령");d.Seat5.DamageDealt=400;Check(!DuelHighlights.Select(d).Contains("유령"),"absent player ignored");
        d=Base();d.Seat1.Name=new FixedString64Bytes("가나다라마바사아자차카타파하가나");d.Seat1.DamageDealt=150;d.Highlight=new FixedString512Bytes(DuelHighlights.Select(d));
        var snapshot=new DuelSnapshot{Results=d};using(var writer=new FastBufferWriter(4096,Allocator.Temp)){writer.WriteNetworkSerializable(snapshot);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(snapshot),"new message snapshot roundtrip");}}
        string frozen=d.Highlight.ToString();d.Seat1.DamageDealt=0;Check(d.Highlight.ToString()==frozen,"existing frozen highlight not rewritten");
        Debug.Log("PASS improved highlights: "+checks+" threshold, fact, rarity priority and serialization checks");
    }
}
