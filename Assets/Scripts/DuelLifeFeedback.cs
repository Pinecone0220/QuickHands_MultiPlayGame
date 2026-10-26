using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HelloWorld
{
    // Owner-only presentation driven by confirmed life/death times, never by a predicted hit.
    [DefaultExecutionOrder(80)]
    public sealed class DuelLifeFeedback:MonoBehaviour
    {
        internal const float RecoveryFadeDuration=.75f;
        internal const float PermanentRecoveryDuration=1.6f+RecoveryFadeDuration;
        private HelloWorldPlayer session;
        private RouletteView view;
        private Camera cameraTarget;
        private UniversalAdditionalCameraData cameraData;
        private bool originalPost;
        private AudioLowPassFilter lowPass;
        private AudioReverbFilter reverb;
        private AudioSource heart,breath;
        private Volume blur;
        private int round=-1,life=-1,previousLives=3;
        private double seenDeath=-1;
        private bool gasped;
        internal float Strength {get;private set;}
        internal float LifeOpacity {get;private set;}
        internal float NumberScroll {get;private set;}
        internal int FromLives {get;private set;}
        internal int RemainingLives {get;private set;}
        internal bool SuppressDeathBanner {get;private set;}
        internal event Action<string> Played;
        private void Start()
        {
            view=GetComponent<RouletteView>();
            heart=Source("Fading heartbeat","LifeHeartbeat",.62f);
            breath=Source("Recovery breath","LifeGasp",.65f);
            var go=new GameObject("Local collapse blur");go.transform.SetParent(transform,false);
            blur=go.AddComponent<Volume>();blur.isGlobal=true;blur.priority=10000;blur.weight=0;
            blur.sharedProfile=Resources.Load<VolumeProfile>("DuelLifeBlur");
        }
        private AudioSource Source(string name,string clip,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
            source.spatialBlend=0;source.priority=40;source.volume=volume;source.bypassListenerEffects=true;
            source.clip=Resources.Load<AudioClip>("GameFeedback/"+clip);return source;
        }
        private void Play(AudioSource source,string name,double age=0)
        {
            if(source==null||source.clip==null||age>=source.clip.length)return;
            source.time=(float)Math.Max(0,age);source.Play();Played?.Invoke(name);
        }
        private void BindCamera()
        {
            var target=Camera.main;if(target==cameraTarget)return;ReleaseCamera();cameraTarget=target;
            if(target==null)return;
            cameraData=target.GetUniversalAdditionalCameraData();originalPost=cameraData.renderPostProcessing;cameraData.renderPostProcessing=true;
            var listener=target.GetComponent<AudioListener>();if(listener==null)return;
            lowPass=listener.gameObject.AddComponent<AudioLowPassFilter>();lowPass.cutoffFrequency=22000;lowPass.lowpassResonanceQ=1;
            reverb=listener.gameObject.AddComponent<AudioReverbFilter>();reverb.reverbPreset=AudioReverbPreset.User;
            reverb.dryLevel=0;reverb.roomHF=-5000;reverb.decayTime=1.1f;reverb.decayHFRatio=.35f;
            reverb.reflectionsLevel=-10000;reverb.reverbDelay=.045f;reverb.diffusion=65;reverb.density=50;
            // Keep filters enabled at neutral settings, avoiding enable/disable audio discontinuities.
            Apply(0);
        }
        private void ReleaseCamera()
        {
            if(cameraData!=null)cameraData.renderPostProcessing=originalPost;
            if(lowPass!=null){lowPass.cutoffFrequency=22000;Destroy(lowPass);}
            if(reverb!=null){reverb.room=reverb.reverbLevel=-10000;Destroy(reverb);}
            cameraTarget=null;cameraData=null;lowPass=null;reverb=null;
        }
        private void Apply(float amount)
        {
            Strength=amount;if(blur!=null)blur.weight=amount;
            if(lowPass!=null)lowPass.cutoffFrequency=Mathf.Exp(Mathf.Lerp(Mathf.Log(22000),Mathf.Log(850),amount));
            if(reverb!=null){reverb.room=Mathf.Lerp(-10000,-1800,amount);reverb.reverbLevel=Mathf.Lerp(-10000,-2300,amount);}
        }
        internal static float Envelope(double now,double diedAt,double reviveAt,bool permanentOrFinal)
        {
            if(diedAt<0||now<diedAt)return 0;
            float enter=Mathf.SmoothStep(0,1,(float)(now-diedAt)/.3f);
            double release=permanentOrFinal?diedAt+PermanentRecoveryDuration-RecoveryFadeDuration:reviveAt;
            return enter*(release<0?1:1-Mathf.SmoothStep(0,1,(float)(now-release)/RecoveryFadeDuration));
        }
        private void Clear()
        {
            Apply(0);LifeOpacity=NumberScroll=0;SuppressDeathBanner=false;
            if(heart!=null)heart.Stop();if(breath!=null)breath.Stop();
            if(view!=null)view.SetLifeFeedback(0,0,0,0,0);
        }
        private void LateUpdate()
        {
            BindCamera();var p=HelloWorldPlayer.Local;
            if(p==null||!p.IsSpawned||p.Seat.Value<0)
            {session=null;round=life=-1;seenDeath=-1;Clear();return;}
            var s=p.State.Value;var a=s.ForSeat(p.Seat.Value);double now=p.NetworkManager.ServerTime.Time;
            if(session!=p||round!=s.Round||life!=a.Life)
            {
                Clear();session=p;round=s.Round;life=a.Life;previousLives=a.LivesRemaining;
                FromLives=RemainingLives=a.LivesRemaining;
                seenDeath=a.DiedAt;gasped=false;
            }
            bool down=a.DiedAt>=0&&!s.IsPracticing(p.Seat.Value);
            if(down&&seenDeath!=a.DiedAt)
            {
                seenDeath=a.DiedAt;gasped=false;FromLives=Mathf.Clamp(previousLives,a.LivesRemaining,3);RemainingLives=a.LivesRemaining;
                Play(heart,"LifeHeartbeat",now-a.DiedAt);
            }
            previousLives=a.LivesRemaining;
            if(!down){seenDeath=-1;Clear();return;}
            bool final=s.Phase==DuelPhase.Finished;
            // Elimination restores the senses without a revival cue, even if a previous breath is still playing.
            if(a.LivesRemaining==0&&breath.isPlaying)breath.Stop();
            SuppressDeathBanner=true;
            float amount=Envelope(now,a.DiedAt,a.ReviveAt,a.LivesRemaining==0||final);Apply(amount);
            double age=now-a.DiedAt;
            LifeOpacity=final?0:Mathf.SmoothStep(0,1,(float)(age-.25)/.25f)*(1-Mathf.SmoothStep(0,1,(float)(age-1.7)/.5f));
            NumberScroll=Mathf.SmoothStep(0,1,(float)(age-.65)/.65f);
            if(!final&&a.LivesRemaining>0&&a.ReviveAt>=0&&now>=a.ReviveAt&&!gasped)
            {
                gasped=true;heart.Stop();Play(breath,"LifeGasp",now-a.ReviveAt);
            }
            if(view!=null)view.SetLifeFeedback(amount,LifeOpacity,FromLives,RemainingLives,NumberScroll);
        }
        private void OnDisable(){Clear();ReleaseCamera();}
        private void OnDestroy(){if(blur!=null)Destroy(blur.gameObject);if(heart!=null)Destroy(heart.gameObject);if(breath!=null)Destroy(breath.gameObject);}
    }
}
