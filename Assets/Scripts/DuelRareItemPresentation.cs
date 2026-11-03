using UnityEngine;
using UnityEngine.Rendering;
namespace HelloWorld
{
    // Cosmetic feedback only; authoritative item use, damage and inventory stay on the server.
    [DefaultExecutionOrder(115)]
    public sealed class DuelRareItemPresentation : MonoBehaviour
    {
        private const int Count=RouletteRules.MaxPlayers;
        private readonly bool[] seen=new bool[Count];
        private readonly int[] lives=new int[Count],iterations=new int[Count],bulletSkin=new int[Count],practiceShots=new int[Count],shots=new int[Count];
        private readonly DuelWeapon[] bulletWeapons=new DuelWeapon[Count];
        private readonly double[] healedAt=new double[Count];
        private readonly float[] bulletFxAt=new float[Count];
        private readonly LineRenderer[] bulletRings=new LineRenderer[Count];
        private readonly LineRenderer[,] bulletCrowns=new LineRenderer[Count,2],bulletSparks=new LineRenderer[Count,8];
        private readonly AudioSource[] sounds=new AudioSource[Count];
        private HelloWorldPlayer session;private int round=-1;private Transform visualRoot;
        private Material glowMaterial;private AudioClip healSound,healSsrSound,healMythicSound;
        private RouletteTable table;
        private void Awake()
        {
            visualRoot=new GameObject("Rare item cosmetic effects").transform;visualRoot.SetParent(transform,false);
            table=GetComponent<RouletteTable>();
            glowMaterial=new Material(Resources.Load<Shader>("DuelShotBullet")){name="Rare item glow"};glowMaterial.SetColor("_BaseColor",Color.white);
            healSound=MakeSound(true);healSsrSound=MakeRecoverySound(false);healMythicSound=MakeRecoverySound(true);
            for(int i=0;i<Count;i++)bulletFxAt[i]=-10;
        }
        private static AudioClip MakeSound(bool heal)
        {
            const int rate=22050;float duration=heal?.65f:.22f;var samples=new float[(int)(rate*duration)];var random=new System.Random(heal?247:368);float filtered=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;filtered=Mathf.Lerp(filtered,(float)(random.NextDouble()*2-1),.10f);
                float fade=Mathf.Exp(-t*(heal?5:20))*(1-Mathf.Exp(-t*80));
                samples[i]=heal?fade*(.18f*Mathf.Sin(2*Mathf.PI*440*t)+.09f*Mathf.Sin(2*Mathf.PI*660*t)):
                    fade*(.18f*Mathf.Sin(2*Mathf.PI*620*t)+.11f*filtered);
            }
            var clip=AudioClip.Create(heal?"Rare recovery shimmer":"Rare bullet mechanical glint",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private static AudioClip MakeRecoverySound(bool mythic)
        {
            const int rate=22050;float duration=mythic?1.05f:.82f;var samples=new float[(int)(rate*duration)];
            float[] notes=mythic?new[]{440f,587.33f,739.99f,880f}:new[]{440f,587.33f,739.99f};
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,baseEnvelope=Mathf.Exp(-t*5)*(1-Mathf.Exp(-t*80));
                float value=baseEnvelope*(.18f*Mathf.Sin(2*Mathf.PI*440*t)+.09f*Mathf.Sin(2*Mathf.PI*660*t));
                for(int j=0;j<notes.Length;j++)
                {
                    float age=t-j*.10f;if(age<0)continue;float envelope=Mathf.Exp(-age*(mythic?5.5f:7f))*(1-Mathf.Exp(-age*80));
                    value+=envelope*(.10f*Mathf.Sin(2*Mathf.PI*notes[j]*age)+.035f*Mathf.Sin(4*Mathf.PI*notes[j]*age));
                }
                samples[i]=Mathf.Clamp(value,-.9f,.9f);
            }
            var clip=AudioClip.Create(mythic?"Mythic recovery shimmer":"SSR recovery shimmer",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private AudioSource Source(int seat)
        {
            if(sounds[seat]!=null)return sounds[seat];var go=new GameObject("Rare item sound "+seat);go.transform.SetParent(visualRoot,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.5f;source.maxDistance=18;source.priority=115;sounds[seat]=source;return source;
        }
        private void PlayHealing(int seat,int localSeat,int skin)
        {
            int tier=DuelSkinEffects.Tier(skin);if(tier==0)return;var source=Source(seat);var pos=DuelGunplay.Point(seat,DuelHitPart.Body);source.transform.position=new Vector3(pos.X,pos.Y,pos.Z);
            source.spatialBlend=seat==localSeat?0:.85f;source.pitch=.96f+.012f*tier;
            source.PlayOneShot(tier==4?healMythicSound:tier==3?healSsrSound:healSound,(tier>=3?.48f:.30f)*DuelSettings.Effects);
        }
        private LineRenderer Ring(int seat)
        {
            if(bulletRings[seat]!=null)return bulletRings[seat];var go=new GameObject("Rare extra bullet flare "+seat);go.transform.SetParent(visualRoot,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glowMaterial;line.useWorldSpace=true;line.loop=true;line.positionCount=32;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;bulletRings[seat]=line;return line;
        }
        private LineRenderer Ornament(int seat,int index,bool spark)
        {
            var existing=spark?bulletSparks[seat,index]:bulletCrowns[seat,index];if(existing!=null)return existing;
            var go=new GameObject((spark?"Muzzle glint ":"Muzzle crown ")+seat+"-"+index);go.transform.SetParent(visualRoot,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glowMaterial;line.useWorldSpace=true;line.loop=!spark;line.positionCount=spark?2:32;
            line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
            if(spark)bulletSparks[seat,index]=line;else bulletCrowns[seat,index]=line;return line;
        }
        private void HideBullet(int seat)
        {
            if(bulletRings[seat]!=null)bulletRings[seat].enabled=false;
            for(int i=0;i<2;i++)if(bulletCrowns[seat,i]!=null)bulletCrowns[seat,i].enabled=false;
            for(int i=0;i<8;i++)if(bulletSparks[seat,i]!=null)bulletSparks[seat,i].enabled=false;
        }
        private void RenderBullet(int seat,DuelActionState a,float now)
        {
            float age=now-bulletFxAt[seat];int tier=DuelSkinEffects.WeaponTier(bulletSkin[seat]);var ring=bulletRings[seat];
            if(tier==0||age<0||age>=.8f){HideBullet(seat);return;}
            if(table==null||!table.TryVisibleMuzzle(seat,bulletWeapons[seat],out var center,out var f)){HideBullet(seat);return;}
            ring=Ring(seat);ring.enabled=true;ring.startWidth=ring.endWidth=.012f+.003f*tier;
            Color color=DuelSkinEffects.WeaponGlow(bulletSkin[seat],now);color.a=(1-age/.8f)*.8f;ring.startColor=ring.endColor=color;
            Vector3 right=Vector3.Cross(Vector3.up,f).normalized;if(right.sqrMagnitude<.001f)right=Vector3.right;Vector3 up=Vector3.Cross(f,right).normalized;
            center+=f*.015f;float radius=.035f+age*.16f;
            int sides=tier==2?4:tier==3?8:12;
            for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI*2/32+age*(tier==4?5:3);
                float polygon=Mathf.Cos(Mathf.PI/sides)/Mathf.Cos(Mathf.Repeat(angle+Mathf.PI/sides,2*Mathf.PI/sides)-Mathf.PI/sides);
                float r=radius*polygon*(tier==4?1+.16f*Mathf.Cos(angle*6):1);
                ring.SetPosition(i,center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r);
            }
            for(int c=0;c<2;c++)
            {
                bool active=tier>=3&&(c==0||tier==4);var crown=bulletCrowns[seat,c];
                if(!active){if(crown!=null)crown.enabled=false;continue;}
                crown=Ornament(seat,c,false);crown.enabled=true;crown.startWidth=crown.endWidth=tier==4?.010f:.007f;
                Color halo=Color.Lerp(color,Color.white,c==0?.32f:.55f);halo.a=color.a*(c==0?.72f:.52f);crown.startColor=crown.endColor=halo;
                float crownRadius=radius*(c==0?1.35f:1.72f);
                for(int i=0;i<32;i++)
                {
                    float angle=i*Mathf.PI*2/32-age*(c==0?2.8f:-4f);
                    float faceted=1+(tier==4?.16f:.08f)*Mathf.Cos(angle*(tier==4?8:6));
                    crown.SetPosition(i,center+f*(.018f+c*.025f)+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*crownRadius*faceted);
                }
            }
            int sparkCount=tier==2?4:tier==3?6:8;float sparkFade=Mathf.Clamp01(1-age/.38f);
            for(int i=0;i<8;i++)
            {
                var spark=bulletSparks[seat,i];if(i>=sparkCount||sparkFade<=0){if(spark!=null)spark.enabled=false;continue;}
                spark=Ornament(seat,i,true);spark.enabled=true;spark.startWidth=tier==4?.012f:.008f;spark.endWidth=.001f;
                Color glint=Color.Lerp(color,Color.white,.55f);glint.a=sparkFade*.8f;spark.startColor=spark.endColor=glint;
                float angle=i*Mathf.PI*2/sparkCount+age*(tier==4?3:1.5f);Vector3 axis=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                float spread=radius*(tier==4?1.8f:1.4f);spark.SetPosition(0,center+axis*spread);spark.SetPosition(1,center+axis*(spread+.025f+age*.09f)+f*(.025f+age*.12f));
            }
        }
        private void ResetAll()
        {for(int i=0;i<Count;i++){seen[i]=false;bulletFxAt[i]=-10;HideBullet(i);if(sounds[i]!=null)sounds[i].Stop();}}
        private void LateUpdate()
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||p.Seat.Value<0){ResetAll();session=null;round=-1;return;}
            var state=p.State.Value;double now=p.NetworkManager.ServerTime.Time;
            if(session!=p||round!=state.Round){ResetAll();session=p;round=state.Round;}
            for(int seat=0;seat<Count;seat++)
            {
                if(!state.Connected(seat)){seen[seat]=false;bulletFxAt[seat]=-10;HideBullet(seat);continue;}
                var a=state.ForSeat(seat);if(!seen[seat]||lives[seat]!=a.Life)
                {seen[seat]=true;lives[seat]=a.Life;iterations[seat]=state.Clock.Iteration;practiceShots[seat]=a.PracticeShotsFired;shots[seat]=a.ShotsFired;healedAt[seat]=a.HealAt;bulletFxAt[seat]=-10;continue;}
                bool newIteration=iterations[seat]!=state.Clock.Iteration;if(newIteration){iterations[seat]=state.Clock.Iteration;bulletFxAt[seat]=-10;}
                if(a.HealAt>=0&&a.HealAt!=healedAt[seat]&&now-a.HealAt>=0&&now-a.HealAt<.55)PlayHealing(seat,p.Seat.Value,DuelSkinEffects.Skin(seat,DuelItem.Recovery));
                healedAt[seat]=a.HealAt;
                bool fired=state.IsPracticing(seat)?a.PracticeShotsFired>practiceShots[seat]:a.ShotsFired>shots[seat];
                if(fired&&a.FiredWeapon!=DuelWeapon.Taser&&a.FiredAt>=0&&now-a.FiredAt>=0&&now-a.FiredAt<.5)
                {int skin=DuelSkinEffects.WeaponSkin(seat,a.FiredWeapon);if(DuelSkinEffects.WeaponTier(skin)>0){bulletSkin[seat]=skin;bulletWeapons[seat]=a.FiredWeapon;bulletFxAt[seat]=Time.unscaledTime;}}
                practiceShots[seat]=a.PracticeShotsFired;shots[seat]=a.ShotsFired;
                RenderBullet(seat,a,Time.unscaledTime);
            }
        }
        private void OnDisable()=>ResetAll();
        private void OnDestroy(){if(visualRoot!=null)Destroy(visualRoot.gameObject);if(glowMaterial!=null)Destroy(glowMaterial);if(healSound!=null)Destroy(healSound);if(healSsrSound!=null)Destroy(healSsrSound);if(healMythicSound!=null)Destroy(healMythicSound);}
    }
}
