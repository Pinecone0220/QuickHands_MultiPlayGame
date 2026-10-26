using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;

// QA-only; copy into the isolated Unity project, not production Assets.
public sealed class GreedRulesRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string path;int checks;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--greed-rules-qa");if(i<0)return;
        var q=new GameObject("Greed rules QA").AddComponent<GreedRulesRuntimeQA>();q.path=args[i+1];Directory.CreateDirectory(q.path);
    }
    void Finish(string result,int code=0)
    {
        Debug.Log(result);File.WriteAllText(Path.Combine(path,"done"),result);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string why){checks++;if(!ok){Finish("FAIL greed rules: "+why,1);throw new Exception(why);}}
    sealed class Rolls:System.Random
    {
        readonly int threshold;readonly double sample;readonly Queue<int> dice;
        public Rolls(int threshold=150000,double sample=1,params int[] faces):base(81){this.threshold=threshold;this.sample=sample;dice=new Queue<int>(faces);}
        public override double NextDouble()=>sample;
        public override int Next(int min,int max)=>min==50&&max==150001?threshold:min==1&&max==9&&dice.Count>0?dice.Dequeue():base.Next(min,max);
    }
    DuelClockContest Begin(Rolls random,int mask=7){var c=new DuelClockContest(random);c.Begin(mask,100,PartyMiniGame.Greed);return c;}
    void Stop(DuelClockContest c,int seat,double elapsed){double t=c.State.StartedAt+elapsed;Check(c.Stop(seat,c.State.Iteration,t,t),"record accepted before actual burst");}
    void Model()
    {
        Check(DuelClockContest.GreedScoreAt(100,99)==0&&DuelClockContest.GreedScoreAt(100,100)==0,"score remains zero until start");
        Check(DuelClockContest.GreedScoreAt(100,110)==5555&&DuelClockContest.GreedScoreAt(100,115)==18750&&DuelClockContest.GreedScoreAt(100,120)==44444,"shared score follows accelerating curve");
        Check(DuelClockContest.GreedScoreAt(100,130)==150000&&DuelClockContest.GreedScoreAt(100,500)==150000,"score reaches ceiling at thirty seconds and stops");
        int previous=0,previousGain=0;for(int t=1;t<=30;t++){int score=DuelClockContest.GreedScoreAt(0,t),gain=score-previous;Check(gain>previousGain,"score speed rises each second");previous=score;previousGain=gain;}
        foreach(int threshold in new[]{50,1000,150000})foreach(double sample in new[]{0.0,.0625,.4096,.95,1})
        {
            var sampled=Begin(new Rolls(threshold,sample));double elapsed=sampled.GreedBurstAt-sampled.State.StartedAt;int score=sampled.ScoreAt(sampled.GreedBurstAt);
            Check(elapsed>0&&elapsed<=30&&score>=50&&score<=threshold,"sampled explosion respects minimum/hidden threshold/time cap");
            Check(DuelClockContest.GreedBurstChance(threshold,threshold)==1&&DuelClockContest.GreedBurstChance(49,threshold)==0,"threshold forces explosion and points below fifty are safe");
        }
        Check(Math.Abs(DuelClockContest.GreedBurstChance(525,1000)-.0625)<1e-9&&DuelClockContest.GreedBurstChance(999,1000)>.99,"cumulative probability rises toward certainty near hidden threshold");
        var early=Begin(new Rolls(1000,.0625));var late=Begin(new Rolls(1000,1));Check(early.GreedBurstAt<late.GreedBurstAt&&early.ScoreAt(early.GreedBurstAt)==525,"chance can burst before threshold");
        var hiddenA=Begin(new Rolls(50,0));var hiddenB=Begin(new Rolls(150000,1));hiddenA.Tick(104,7);hiddenB.Tick(104,7);
        Check(hiddenA.PublicState().Equals(hiddenB.PublicState())&&hiddenA.PublicState().Target==0,"public state hides threshold and future burst time");
        var c0=Begin(new Rolls());Stop(c0,0,5);var privateState=c0.PublicState();Check(privateState.Seat0.Points==-1&&privateState.Seat0.Centiseconds==-1,"private stop and points remain hidden during counting");
        var c=Begin(new Rolls(150000,1,8,2));Stop(c,2,9);Stop(c,0,10);Stop(c,1,10);
        Check(c.State.Phase==ClockPhase.Dice&&c.State.Dice.ParticipantsMask==3&&!c.StopDice(2,c.State.Iteration,1,114),"only equal top scores roll for shooting right");
        c.StopDice(0,c.State.Iteration,1,114);c.StopDice(1,c.State.Iteration,1,114);c.Tick(115.21,7);
        Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0,"one highest die becomes sole normal shooter");c.Tick(c.State.PhaseAt+3.01,7);Check(c.State.Phase==ClockPhase.ItemPreparation,"dice result remains three seconds before items");c.Tick(c.State.StartedAt+.01,7);Check(c.State.Phase==ClockPhase.Shooting&&c.State.First==0,"single winner reaches normal shooting phase");
        c=Begin(new Rolls(),15);Stop(c,1,9);Stop(c,2,9);Stop(c,3,9);Stop(c,0,10);
        Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0&&c.State.Dice.ParticipantsMask==0,"lower equal scores never force dice when first is clear");
        c=Begin(new Rolls(50,1,8,2,2));double at=c.GreedBurstAt+.91;c.Tick(at,7);for(int seat=0;seat<3;seat++)c.StopDice(seat,c.State.Iteration,1,at+.1);c.Tick(at+1.31,7);
        Check(c.State.Phase==ClockPhase.Ranking&&c.State.First==0&&c.State.Dice.Round==1,"all burst still awards one shooter without rerolling lower equal faces");
        c=Begin(new Rolls(150000,1));double burst=c.GreedBurstAt;Check(c.Stop(0,c.State.Iteration,burst-.001,burst-.001),"stop just before burst accepted");Check(c.Stop(1,c.State.Iteration,burst,burst)&&c.State.Seat1.Failure==MiniFailure.Burst,"stop exactly at burst fails");Check(!c.Stop(2,c.State.Iteration,burst-.001,burst+.301),"old packet outside arrival grace rejected");c.Tick(burst+.91,7);
        Check(c.State.First==0&&c.State.EliminatedMask==0,"burst alone never costs a life");
        var dice=new DuelDiceContest(new Rolls(150000,1,7,1));dice.Begin(new List<List<int>>{new List<int>{0,1},new List<int>{2,3}},0,true);dice.Tick(.1,12);dice.Tick(1.31,12);
        Check(dice.State.PendingMask==12&&dice.State.ParticipantsMask==12&&dice.State.CanStop(2),"disconnected leaders promote remaining tied contenders with visible participation");
        dice.Stop(2,dice.State.Round,1.4);dice.Stop(3,dice.State.Round,1.4);dice.Tick(2.61,12);Check(dice.Order[0]==2,"promoted contender can win with authoritative dice");
        foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})
        {
            double[] schedule=new double[2];int j=0;
            foreach(int threshold in new[]{50,150000})
            {
                var d=new DuelDirector(DuelMode.AI,new Rolls(threshold,.5),difficulty);d.Join(0,0);d.Join(1,0);d.SetAutoSelectItems(true);d.Ready(0,0);d.Tick(0);
                var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);clock.Begin(3,100,PartyMiniGame.Greed);
                typeof(DuelDirector).GetMethod("ScheduleMiniAi",F).Invoke(d,new object[]{1});schedule[j++]=((double[])typeof(DuelDirector).GetField("nextAi",F).GetValue(d))[1]-clock.State.StartedAt;
            }
            Check(schedule[0]==schedule[1]&&schedule[0]>5&&schedule[0]<30,"AI schedule scales with new duration without reading hidden threshold");
        }
    }
    IEnumerator Start(){yield return null;Model();Finish("PASS greed rules: "+checks+" checks");}
}
