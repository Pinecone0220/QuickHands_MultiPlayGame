using UnityEngine;
using UnityEngine.Rendering;
namespace HelloWorld
{
    // Cosmetic feedback only; authoritative item use, damage and inventory stay on the server.
    [DefaultExecutionOrder(115)]
    public sealed class DuelRareItemPresentation : MonoBehaviour
    {
        private const int Count=RouletteRules.MaxPlayers;
        private readonly bool[] seen=new bool[Count],bulletUsed=new bool[Count];
        private readonly int[] lives=new int[Count],iterations=new int[Count],bulletSkin=new int[Count],practiceShots=new int[Count],shots=new int[Count];
        private readonly DuelWeapon[] bulletWeapons=new DuelWeapon[Count];
        private readonly double[] healedAt=new double[Count];
        private readonly float[] bulletFxAt=new float[Count];
        private readonly LineRenderer[] bulletRings=new LineRenderer[Count];
        private readonly AudioSource[] sounds=new AudioSource[Count];
        private HelloWorldPlayer session;private int round=-1;private Transform visualRoot;
        private Material glowMaterial;private AudioClip healSound,bulletSound;
        private RouletteTable table;
        private void Awake()
        {
            visualRoot=new GameObject("Rare item cosmetic effects").transform;visualRoot.SetParent(transform,false);
            table=GetComponent<RouletteTable>();
            glowMaterial=new Material(Resources.Load<Shader>("DuelBullet")){name="Rare item glow"};glowMaterial.SetColor("_BaseColor",Color.white);
            healSound=MakeSound(true);bulletSound=MakeSound(false);
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
        private AudioSource Source(int seat)
        {
            if(sounds[seat]!=null)return sounds[seat];var go=new GameObject("Rare item sound "+seat);go.transform.SetParent(visualRoot,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=1.5f;source.maxDistance=18;source.priority=115;sounds[seat]=source;return source;
        }
        private void Play(int seat,int localSeat,int skin,DuelItem item)
        {
            int tier=DuelSkinEffects.Tier(skin);if(tier==0)return;var source=Source(seat);var pos=DuelGunplay.Point(seat,DuelHitPart.Body);source.transform.position=new Vector3(pos.X,pos.Y,pos.Z);
            source.spatialBlend=seat==localSeat?0:.85f;source.pitch=.96f+.012f*tier+.008f*(skin%4);
            source.PlayOneShot(item==DuelItem.Recovery?healSound:bulletSound,(.24f+.025f*tier)*DuelSettings.Effects);
        }
        private LineRenderer Ring(int seat)
        {
            if(bulletRings[seat]!=null)return bulletRings[seat];var go=new GameObject("Rare extra bullet flare "+seat);go.transform.SetParent(visualRoot,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glowMaterial;line.useWorldSpace=true;line.loop=true;line.positionCount=32;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;bulletRings[seat]=line;return line;
        }
        private void RenderBullet(int seat,DuelActionState a,float now)
        {
            float age=now-bulletFxAt[seat];int tier=DuelSkinEffects.Tier(bulletSkin[seat]);var ring=bulletRings[seat];
            if(tier==0||age<0||age>=.8f){if(ring!=null)ring.enabled=false;return;}
            if(table==null||!table.TryVisibleMuzzle(seat,bulletWeapons[seat],out var center,out var f)){if(ring!=null)ring.enabled=false;return;}
            ring=Ring(seat);ring.enabled=true;ring.startWidth=ring.endWidth=.012f+.003f*tier;
            Color color=DuelSkinEffects.Glow(bulletSkin[seat]);color.a=(1-age/.8f)*.8f;ring.startColor=ring.endColor=color;
            Vector3 right=Vector3.Cross(Vector3.up,f).normalized;if(right.sqrMagnitude<.001f)right=Vector3.right;Vector3 up=Vector3.Cross(f,right).normalized;
            center+=f*.015f;float radius=.035f+age*.16f;
            for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32+age*3;float r=radius*(1+.10f*Mathf.Sin(angle*5+age*9));ring.SetPosition(i,center+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*r);}
        }
        private void ResetAll()
        {for(int i=0;i<Count;i++){seen[i]=false;bulletFxAt[i]=-10;if(bulletRings[i]!=null)bulletRings[i].enabled=false;if(sounds[i]!=null)sounds[i].Stop();}}
        private void LateUpdate()
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||p.Seat.Value<0){ResetAll();session=null;round=-1;return;}
            var state=p.State.Value;double now=p.NetworkManager.ServerTime.Time;
            if(session!=p||round!=state.Round){ResetAll();session=p;round=state.Round;}
            for(int seat=0;seat<Count;seat++)
            {
                if(!state.Connected(seat)){seen[seat]=false;bulletFxAt[seat]=-10;if(bulletRings[seat]!=null)bulletRings[seat].enabled=false;continue;}
                var a=state.ForSeat(seat);if(!seen[seat]||lives[seat]!=a.Life)
                {seen[seat]=true;lives[seat]=a.Life;iterations[seat]=state.Clock.Iteration;bulletUsed[seat]=a.BulletUsed;practiceShots[seat]=a.PracticeShotsFired;shots[seat]=a.ShotsFired;healedAt[seat]=a.HealAt;bulletFxAt[seat]=-10;continue;}
                bool newIteration=iterations[seat]!=state.Clock.Iteration;if(newIteration){iterations[seat]=state.Clock.Iteration;bulletUsed[seat]=false;bulletFxAt[seat]=-10;}
                if(a.HealAt>=0&&a.HealAt!=healedAt[seat]&&now-a.HealAt>=0&&now-a.HealAt<.55)Play(seat,p.Seat.Value,DuelSkinEffects.Skin(seat,DuelItem.Recovery),DuelItem.Recovery);
                healedAt[seat]=a.HealAt;
                if(a.BulletUsed&&!bulletUsed[seat]&&state.Phase==DuelPhase.Playing&&(state.Clock.Phase==ClockPhase.ItemPreparation||state.Clock.Phase==ClockPhase.Shooting))
                {int skin=DuelSkinEffects.Skin(seat,DuelItem.Bullet);if(DuelSkinEffects.Tier(skin)>0){bulletSkin[seat]=skin;bulletWeapons[seat]=a.Weapon;bulletFxAt[seat]=Time.unscaledTime;Play(seat,p.Seat.Value,skin,DuelItem.Bullet);}}
                // In ready-room practice the bullet item is cosmetic and previews every firearm shot.
                if(state.IsPracticing(seat)&&a.PracticeShotsFired>practiceShots[seat]&&a.FiredWeapon!=DuelWeapon.Taser&&a.FiredAt>=0&&now-a.FiredAt>=0&&now-a.FiredAt<.5)
                {int skin=DuelSkinEffects.Skin(seat,DuelItem.Bullet);if(DuelSkinEffects.Tier(skin)>0){bulletSkin[seat]=skin;bulletWeapons[seat]=a.FiredWeapon;bulletFxAt[seat]=Time.unscaledTime;Play(seat,p.Seat.Value,skin,DuelItem.Bullet);}}
                if(!state.IsPracticing(seat)&&a.BulletUsed&&a.ShotsFired>shots[seat]&&a.FiredWeapon!=DuelWeapon.Taser&&a.FiredAt>=0&&now-a.FiredAt>=0&&now-a.FiredAt<.5)
                {int skin=DuelSkinEffects.Skin(seat,DuelItem.Bullet);if(DuelSkinEffects.Tier(skin)>0){bulletSkin[seat]=skin;bulletWeapons[seat]=a.FiredWeapon;bulletFxAt[seat]=Time.unscaledTime;}}
                practiceShots[seat]=a.PracticeShotsFired;shots[seat]=a.ShotsFired;
                bulletUsed[seat]=a.BulletUsed;RenderBullet(seat,a,Time.unscaledTime);
            }
        }
        private void OnDisable()=>ResetAll();
        private void OnDestroy(){if(visualRoot!=null)Destroy(visualRoot.gameObject);if(glowMaterial!=null)Destroy(glowMaterial);if(healSound!=null)Destroy(healSound);if(bulletSound!=null)Destroy(bulletSound);}
    }
}
