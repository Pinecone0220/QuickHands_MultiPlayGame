using System;
using System.Collections.Generic;
using UnityEngine;

namespace HelloWorld
{
    // Local presentation only. Timing comes from the same public clock as the UI.
    public sealed class DuelGameAudio : MonoBehaviour
    {
        private AudioSource cues,timer,score,hits;
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
        }
        private AudioSource Source(string name,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;
            source.spatialBlend=0;source.volume=volume;source.priority=110;return source;
        }
        private AudioClip Clip(string name)=>clips.TryGetValue(name,out var clip)?clip:null;
        private void Play(string name,float gain=1)
        {var clip=Clip(name);if(clip==null)return;cues.PlayOneShot(clip,gain);Played?.Invoke(name);}
        public void UiClick()=>Play("UiClick",.55f);
        public void KeyInput(bool mistake=false)=>Play(mistake?"KeyError":"KeyTap",mistake?.65f:.5f);
        public void ConfirmHit(bool headshot)
        {
            string name=headshot?"HitHead":"HitConfirm";var clip=Clip(name);if(clip==null)return;
            hits.PlayOneShot(clip);Played?.Invoke(name);
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
            source.volume=volume;
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
            if(timer!=null)timer.Stop();if(score!=null)score.Stop();
            round=iteration=-1;pending=confirmed=signalSeen=burstSeen=false;
        }
        public void Reset()
        {ResetMiniGame();countdownAt=-1;countdownRound=-1;countdownKind=0;countdownNumber=-1;}
        private void OnDisable()=>Reset();
    }
}
