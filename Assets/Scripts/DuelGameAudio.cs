using System;
using System.Collections.Generic;
using UnityEngine;

namespace HelloWorld
{
    // Local presentation only. Timing comes from the same public clock as the UI.
    public sealed class DuelGameAudio : MonoBehaviour
    {
        private AudioSource cues,timer,score,hits,diceRoll;
        private AudioClip diceRollClip,diceLandClip;
        private int diceRound=-1,diceLocked;
        private bool diceSeen;
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        private int round=-1,iteration=-1;
        private bool pending,confirmed,signalSeen,burstSeen;
        private MiniSignal lastSignal;
        private double signalAt;
        private double countdownAt=-1;
        private int countdownRound=-1,countdownKind,countdownNumber=-1;
        internal event Action<string> Played;
        private void Awake()
        {
            foreach(var clip in Resources.LoadAll<AudioClip>("GameFeedback"))clips[clip.name]=clip;
            cues=Source("UI and minigame cues",.5f);
            hits=Source("Confirmed hit feedback",.8f);hits.priority=60;
            timer=Source("Hidden timer ticks",0);timer.loop=true;timer.clip=Clip("ClockLoop");
            score=Source("Greed score ticks",0);score.loop=true;score.clip=Clip("GreedLoop");
            diceRoll=Source("Dice rolling",0);diceRoll.loop=true;
            diceRollClip=MakeDiceSound(false);diceLandClip=MakeDiceSound(true);diceRoll.clip=diceRollClip;
        }
        private AudioSource Source(string name,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;
            source.spatialBlend=0;source.volume=volume;source.priority=110;return source;
        }
        private AudioClip Clip(string name)=>clips.TryGetValue(name,out var clip)?clip:null;
        private static AudioClip MakeDiceSound(bool land)
        {
            const int rate=22050;float duration=land?.28f:1f;var data=new float[(int)(rate*duration)];var random=new System.Random(land?183:92);
            for(int i=0;i<data.Length;i++)
            {
                float time=i/(float)rate;float value=0;int count=land?3:7;
                for(int j=0;j<count;j++)
                {
                    float start=land?j*.037f:j*.14f+(j%2)*.016f;float age=time-start;if(age<0||age>.1f)continue;
                    float envelope=Mathf.Exp(-age*(land?35:47));
                    value+=envelope*(Mathf.Sin(age*(land?1350:1000)+j*.6f)*.28f+(float)(random.NextDouble()*2-1)*.12f)*(land?1-j*.23f:.7f);
                }
                data[i]=Mathf.Clamp(value,-1,1);
            }
            var clip=AudioClip.Create(land?"DiceLand":"DiceRoll",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void Play(string name,float gain=1)
        {var clip=Clip(name);if(clip==null)return;cues.PlayOneShot(clip,gain*DuelSettings.Effects);Played?.Invoke(name);}
        public void UiClick()=>Play("UiClick",.55f);
        public void ResultCue(int stage)=>Play(stage==0?"CountdownStart":stage==1?"GreedConfirm":"ClockConfirm",stage==0?.8f:.65f);
        public void KeyInput(bool mistake=false)=>Play(mistake?"KeyError":"KeyTap",mistake?.65f:.5f);
        public void ConfirmHit(bool headshot)
        {
            string name=headshot?"HitHead":"HitConfirm";var clip=Clip(name);if(clip==null)return;
            hits.PlayOneShot(clip,DuelSettings.Effects);Played?.Invoke(name);
        }
        public void StopPending()=>pending=true;
        public void ReportStop(int partyRound,int gameIteration,bool accepted,PartyMiniGame game,MiniFailure failure)
        {
            if(partyRound!=round||gameIteration!=iteration)return;
            pending=accepted;
            if(!accepted||confirmed)return;
            confirmed=true;
            Play(failure!=MiniFailure.None?"KeyError":game==PartyMiniGame.Greed?"GreedConfirm":"ClockConfirm",.8f);
        }
        private static void Loop(AudioSource source,float volume,double elapsed)
        {
            source.volume=volume*DuelSettings.Effects;
            if(volume<=0){if(source.isPlaying)source.Stop();return;}
            if(source.clip!=null&&!source.isPlaying)
            {source.time=(float)(Math.Max(0,elapsed)%source.clip.length);source.Play();}
        }
        public void Observe(DuelSnapshot state,int seat,double now)
        {
            var c=state.Clock;
            if(round!=state.Round||iteration!=c.Iteration)
            {ResetMiniGame();round=state.Round;iteration=c.Iteration;}
            ObserveCountdown(state,now);
            ObserveDice(state,now);
            bool counting=state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&c.Phase==ClockPhase.Counting;
            double elapsed=now-c.StartedAt;
            bool running=counting&&elapsed>=0;
            bool personal=running&&state.Alive(seat)&&!pending;
            float ticking=personal&&c.MiniGame==PartyMiniGame.Countdown&&elapsed<=c.Target/100.0+2?
                .17f*Mathf.Clamp01((float)((c.HideAfter+.75-elapsed)/.75)):0;
            Loop(timer,ticking,elapsed);
            bool growing=personal&&c.MiniGame==PartyMiniGame.Greed&&c.Signal!=MiniSignal.Burst;
            score.pitch=1+Mathf.Clamp((float)elapsed*.025f,0,.4f);
            Loop(score,growing?.11f:0,elapsed);
            if(!running)return;
            if(c.MiniGame==PartyMiniGame.Signal)
            {
                MiniSignal visible=c.Signal==MiniSignal.Go&&now<c.SignalAt?MiniSignal.Wait:c.Signal;
                double at=visible==MiniSignal.Wait?c.StartedAt:c.SignalAt;
                if(!signalSeen||lastSignal!=visible||signalAt!=at)
                {
                    // Initial snapshots must not replay historical signals.
                    if(signalSeen||now-at<=.5)
                        Play(visible==MiniSignal.Go?"SignalGo":visible==MiniSignal.Fake?"SignalFake":"SignalWait",.8f);
                    signalSeen=true;lastSignal=visible;signalAt=at;
                }
            }
            if(c.MiniGame==PartyMiniGame.Greed&&c.Signal==MiniSignal.Burst&&!burstSeen)
            {burstSeen=true;if(now-c.SignalAt<=1.2)Play("GreedBurst");}
        }
        private void ObserveDice(DuelSnapshot state,double now)
        {
            var c=state.Clock;var d=c.Dice;
            bool visible=state.Phase==DuelPhase.Playing&&c.Phase==ClockPhase.Dice;
            if(!visible){Loop(diceRoll,0,0);diceSeen=false;diceRound=-1;diceLocked=0;return;}
            bool rolling=d.Phase==DuelDicePhase.Rolling&&(d.PendingMask&~d.LockedMask)!=0;
            if(rolling&&!diceRoll.isPlaying)Played?.Invoke("DiceRoll");
            Loop(diceRoll,rolling?.22f:0,now-d.StartedAt);
            if(!diceSeen||diceRound!=d.Round){diceSeen=true;diceRound=d.Round;diceLocked=d.LockedMask;return;}
            int newlyLocked=d.LockedMask&~diceLocked;diceLocked=d.LockedMask;
            if(newlyLocked!=0&&diceLandClip!=null){cues.PlayOneShot(diceLandClip,.7f*DuelSettings.Effects);Played?.Invoke("DiceLand");}
        }
        private void ObserveCountdown(DuelSnapshot state,double now)
        {
            var c=state.Clock;
            int kind=state.Phase!=DuelPhase.Playing?0:state.Stage==DuelStage.Countdown?1:
                state.Stage!=DuelStage.Racing?0:c.Phase==ClockPhase.QuickCountdown?2:
                c.Phase==ClockPhase.Counting&&c.MiniGame==PartyMiniGame.Greed&&now<c.StartedAt?3:0;
            double end=kind==1?state.StageStartedAt+DuelDirector.CountdownDuration:c.StartedAt;
            if(kind==0)
            {
                bool started=countdownKind==1&&state.Stage==DuelStage.Racing||
                    countdownKind==2&&c.Phase==ClockPhase.QuickHands||
                    countdownKind==3&&c.Phase==ClockPhase.Counting&&c.MiniGame==PartyMiniGame.Greed;
                if(started&&state.Phase==DuelPhase.Playing&&state.Round==countdownRound&&
                    countdownNumber>0&&now>=countdownAt&&now-countdownAt<=.35)Play("CountdownStart",.85f);
                countdownAt=-1;countdownNumber=-1;countdownKind=0;return;
            }
            if(countdownRound!=state.Round||countdownKind!=kind||countdownAt!=end)
            {countdownRound=state.Round;countdownKind=kind;countdownAt=end;countdownNumber=-1;}
            int number=Math.Clamp((int)Math.Ceiling(end-now),0,3);
            if(countdownNumber>=0&&number>=countdownNumber)return;
            if(number>0)Play("CountdownTick",.75f);
            else if(countdownNumber>0&&now-end<=.35)Play("CountdownStart",.85f);
            countdownNumber=number;
        }
        private void ResetMiniGame()
        {
            if(timer!=null)timer.Stop();if(score!=null)score.Stop();if(diceRoll!=null)diceRoll.Stop();
            diceSeen=false;diceRound=-1;diceLocked=0;
            round=iteration=-1;pending=confirmed=signalSeen=burstSeen=false;
        }
        public void Reset()
        {ResetMiniGame();countdownAt=-1;countdownRound=-1;countdownKind=0;countdownNumber=-1;}
        private void OnDisable()=>Reset();
        private void OnDestroy(){if(diceRollClip!=null)Destroy(diceRollClip);if(diceLandClip!=null)Destroy(diceLandClip);}
    }
}
