using UnityEngine;
using UnityEngine.Rendering;
namespace HelloWorld
{
    // Pooled cosmetic wires and electrical discharge. The server has already resolved the hit.
    internal sealed class DuelTaserArc
    {
        private const int Points=36;
        private const float Duration=.42f;
        private readonly LineRenderer[] cores=new LineRenderer[2],halos=new LineRenderer[2],branches=new LineRenderer[4],sparks=new LineRenderer[4];
        private readonly LineRenderer[] spirals=new LineRenderer[3];
        private readonly Vector3[] corePoints=new Vector3[Points],branchPoints=new Vector3[7];
        private readonly Light impactLight;
        private float born;
        private int seed;
        private bool contact,skinColored;private int skinTier;private Color skinGlow;
        public bool Visible {get;private set;}
        public Vector3 Start {get;private set;}
        public Vector3 End {get;private set;}
        public DuelTaserArc(Transform parent,Material material,int seat)
        {
            var root=new GameObject("Taser electrical discharge "+seat).transform;root.SetParent(parent,false);
            for(int i=0;i<2;i++){halos[i]=Line(root,material,"Wire halo "+i,Points,.033f);cores[i]=Line(root,material,"Wire core "+i,Points,.0045f);}
            for(int i=0;i<4;i++){branches[i]=Line(root,material,"Branch arc "+i,7,.012f);sparks[i]=Line(root,material,"Contact spark "+i,3,.009f);}
            for(int i=0;i<spirals.Length;i++)spirals[i]=Line(root,material,"Rare electrical spiral "+i,Points,.011f);
            impactLight=new GameObject("Contact glow").AddComponent<Light>();impactLight.transform.SetParent(root,false);impactLight.type=LightType.Point;
            impactLight.color=new Color(.12f,.55f,1);impactLight.range=.65f;impactLight.shadows=LightShadows.None;impactLight.enabled=false;
        }
        private static LineRenderer Line(Transform parent,Material material,string name,int count,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.sharedMaterial=material;
            line.useWorldSpace=true;line.positionCount=count;line.startWidth=width;line.endWidth=width*.65f;
            line.numCapVertices=4;line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        public void Play(Vector3 from,Vector3 to,bool hasContact,int pattern,int skin=0)
        {
            Start=from;End=from+Vector3.ClampMagnitude(to-from,DuelGunplay.TaserRange);born=Time.unscaledTime;seed=pattern;contact=hasContact;skinTier=DuelSkinEffects.Tier(skin);skinColored=skin>0;skinGlow=skinColored?DuelSkinEffects.Glow(skin):new Color(.12f,.55f,1);impactLight.color=skinGlow;Visible=true;Step(born);
        }
        public void Step(float now)
        {
            if(!Visible)return;float age=now-born;
            if(age<0||age>=Duration){Hide();return;}
            var delta=End-Start;float length=delta.magnitude;var forward=length>.001f?delta/length:Vector3.forward;
            var right=Vector3.Cross(forward,Mathf.Abs(forward.y)>.95f?Vector3.forward:Vector3.up).normalized;
            var up=Vector3.Cross(right,forward).normalized;
            float extension=1-Mathf.Pow(1-Mathf.Clamp01(age/.055f),3);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,Duration,age));
            float phase=seed*.73f+age*115,crackle=Mathf.Exp(-age*17);
            for(int wire=0;wire<2;wire++)
            {
                float sign=wire==0?-1:1;
                for(int i=0;i<Points;i++)
                {
                    float t=extension*i/(Points-1);float envelope=Mathf.Sin(t*Mathf.PI);
                    var jitter=(right*Mathf.Sin(t*49+phase+wire*2.1f)+up*Mathf.Cos(t*63-phase))*(.003f+.018f*crackle)*envelope;
                    corePoints[i]=Start+delta*t+right*(sign*.012f*envelope)+Vector3.down*(.035f*envelope*extension)+jitter;
                }
                halos[wire].SetPositions(corePoints);cores[wire].SetPositions(corePoints);
                Tint(halos[wire],skinColored?skinGlow:new Color(.06f,.46f,1),fade*(skinTier>0?.75f:.58f));Tint(cores[wire],skinColored?Color.Lerp(skinGlow,Color.white,.55f):new Color(.72f,.92f,1),fade);
            }
            for(int b=0;b<4;b++)
            {
                float start=.14f+b*.19f,span=.075f;
                bool active=extension>start+span&&age<.24f;branches[b].enabled=active;
                if(!active)continue;
                for(int i=0;i<7;i++)
                {
                    float t=i/6f,u=start+t*span;var branch=right*(Mathf.Sin(t*Mathf.PI)*(b%2==0?1:-1))+
                        up*(Mathf.Sin(t*11+phase+b)*Mathf.Sin(t*Mathf.PI));
                    branchPoints[i]=Start+delta*u+branch*(.035f+.035f*crackle);
                }
                branches[b].SetPositions(branchPoints);Tint(branches[b],skinColored?skinGlow:new Color(.20f,.67f,1),fade*(skinTier>0?.8f:.55f)*(.65f+.35f*Mathf.Sin(phase+b)));
            }
            for(int spiral=0;spiral<spirals.Length;spiral++)
            {
                if(skinTier<3||spiral>=(skinTier==4?3:2)){spirals[spiral].enabled=false;continue;}
                for(int i=0;i<Points;i++)
                {
                    float t=extension*i/(Points-1),angle=t*Mathf.PI*12+phase*.30f+spiral*Mathf.PI*2/3;
                    float radius=(.035f+.012f*spiral)*Mathf.Sin(t*Mathf.PI);
                    corePoints[i]=Start+delta*t+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius;
                }
                spirals[spiral].SetPositions(corePoints);
                Tint(spirals[spiral],Color.Lerp(skinGlow,Color.white,.25f),fade*(skinTier==4?.85f:.65f));
            }
            float flash=contact?Mathf.Clamp01((age-.025f)/.025f)*Mathf.Exp(-Mathf.Max(0,age-.055f)*24):0;
            for(int i=0;i<4;i++)
            {
                if(flash<.015f){sparks[i].enabled=false;continue;}
                float angle=i*Mathf.PI*.5f+phase*.06f;var axis=right*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                float radius=(.045f+age*.09f)*(i%2==0?1:.65f);
                sparks[i].SetPosition(0,End-axis*radius);sparks[i].SetPosition(1,End);sparks[i].SetPosition(2,End+axis*radius);
                Tint(sparks[i],skinColored?Color.Lerp(skinGlow,Color.white,.4f):new Color(.66f,.88f,1),flash);
            }
            impactLight.transform.position=End-forward*.045f;impactLight.enabled=flash>.03f;impactLight.intensity=flash*(skinTier>0?2.2f+skinTier*.25f:1.8f);
        }
        private static void Tint(LineRenderer line,Color color,float alpha)
        {color.a=Mathf.Clamp01(alpha);line.startColor=color;color.a*=.75f;line.endColor=color;line.enabled=alpha>.005f;}
        public void Hide()
        {
            Visible=false;foreach(var line in cores)line.enabled=false;foreach(var line in halos)line.enabled=false;
            foreach(var line in spirals)line.enabled=false;foreach(var line in branches)line.enabled=false;foreach(var line in sparks)line.enabled=false;impactLight.enabled=false;
        }
    }
}
