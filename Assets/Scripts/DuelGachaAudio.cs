using UnityEngine;
namespace HelloWorld
{
    // Dedicated two-dimensional UI sounds; independent of the minigame reel volume.
    public sealed class DuelGachaAudio : MonoBehaviour
    {
        AudioSource source,ballRoll;AudioClip tick,normal,ssr,mythic,ballRolling,ballContact;bool ballStarted;
        void Awake(){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.8f;source.priority=40;tick=MakeTick();normal=MakeAward(0);ssr=MakeAward(1);mythic=MakeAward(2);ballRolling=MakeBallSound(false);ballContact=MakeBallSound(true);ballRoll=gameObject.AddComponent<AudioSource>();ballRoll.playOnAwake=false;ballRoll.spatialBlend=0;ballRoll.loop=true;ballRoll.clip=ballRolling;ballRoll.volume=0;ballRoll.priority=60;}
        static AudioClip MakeTick()
        {
            const int rate=22050;var data=new float[2205];var random=new System.Random(287);
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;float env=Mathf.Exp(-t*32)*(1-Mathf.Exp(-t*180));data[i]=env*(.30f*Mathf.Sin(2*Mathf.PI*310*t)+.12f*Mathf.Sin(2*Mathf.PI*460*t)+(float)(random.NextDouble()*2-1)*.035f);}
            var clip=AudioClip.Create("Gacha wheel click",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        static AudioClip MakeBallSound(bool contact)
        {
            const int rate=22050;int length=contact?3308:rate;var data=new float[length];var random=new System.Random(contact?291:162);float smooth=0;
            for(int i=0;i<length;i++)
            {
                float t=i/(float)rate;smooth=Mathf.Lerp(smooth,(float)(random.NextDouble()*2-1),.09f);
                if(contact){float env=Mathf.Exp(-t*28)*(1-Mathf.Exp(-t*150));data[i]=env*(.24f*Mathf.Sin(2*Mathf.PI*480*t)+smooth*.12f);}
                else {float pulse=.55f+.45f*Mathf.Sin(2*Mathf.PI*7*t);float seam=Mathf.Sin(Mathf.PI*i/(length-1f));data[i]=smooth*pulse*seam*.35f;}
            }
            var clip=AudioClip.Create(contact?"Roulette ball contact":"Roulette ball rolling",length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void ResetBall(){ballStarted=false;if(ballRoll!=null){ballRoll.Stop();ballRoll.volume=0;}}
        public void UpdateBall(float progress)
        {
            if(ballRoll==null)return;
            if(progress<.16f)return;
            if(!ballStarted){ballStarted=true;source.PlayOneShot(ballContact,.32f*DuelSettings.Effects);ballRoll.Play();}
            float fadeIn=Mathf.Clamp01((progress-.16f)/.10f),fadeOut=Mathf.Clamp01((1-progress)/.20f);
            ballRoll.volume=.12f*fadeIn*fadeOut*DuelSettings.Effects;ballRoll.pitch=Mathf.Lerp(1.1f,.72f,progress);
        }
        static AudioClip MakeAward(int rank)
        {
            const int rate=22050;float duration=rank==2?1.35f:rank==1?1.05f:.52f;var data=new float[(int)(rate*duration)];
            float[] tones=rank==2?new[]{392f,523.25f,659.25f,783.99f,1046.5f}:rank==1?new[]{523.25f,659.25f,783.99f,1046.5f}:new[]{659.25f,880f};
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,value=0;for(int j=0;j<tones.Length;j++){float age=t-j*(rank==0?.10f:.12f);if(age<0)continue;float env=Mathf.Exp(-age*(rank==0?9f:4.5f));value+=env*(Mathf.Sin(2*Mathf.PI*tones[j]*age)+.32f*Mathf.Sin(2*Mathf.PI*tones[j]*2*age))*.19f;}
                data[i]=Mathf.Clamp(value,-.95f,.95f);
            }
            var clip=AudioClip.Create(rank==2?"Gacha mythic":rank==1?"Gacha SSR":"Gacha prize",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Tick(){if(source!=null)source.PlayOneShot(tick,.4f*DuelSettings.Effects);}
        public void Award(int grade){if(source!=null)source.PlayOneShot(grade==4?mythic:grade==3?ssr:normal,DuelSettings.Effects);}
        void OnDestroy(){if(tick!=null)Destroy(tick);if(normal!=null)Destroy(normal);if(ssr!=null)Destroy(ssr);if(mythic!=null)Destroy(mythic);if(ballRolling!=null)Destroy(ballRolling);if(ballContact!=null)Destroy(ballContact);}
    }
}
