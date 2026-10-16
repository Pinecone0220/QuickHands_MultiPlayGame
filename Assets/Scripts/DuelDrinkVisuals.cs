using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HelloWorld
{
    public sealed class DuelDrinkVisuals:MonoBehaviour
    {
        sealed class Setting
        {
            public Transform Bottle,Glass,BottleLiquid,Drink;
            public LineRenderer Pour;
            public Transform[] BottleShards,GlassShards;
            public float RightGrip,LeftGrip;
            public Vector3 RightOffset,LeftOffset;
            public Renderer[] BottleRenderers,GlassRenderers;
        }
        readonly Setting[] settings=new Setting[RouletteRules.MaxPlayers];
        readonly List<Object> owned=new List<Object>();
        Transform scene;Material amber,brown,glass;Mesh shard;
        DuelTabletopState state;double now;int round=-1;bool initialized;
        static Vector3 V(AimVector v)=>new Vector3(v.X,v.Y,v.Z);
        static Vector3 BottleRest=>new Vector3(-.66f,1.10f,-.82f+DuelGunplay.SeatAdvance);
        static Vector3 GlassRest=>new Vector3(.18f,.995f,-.86f+DuelGunplay.SeatAdvance);
        public void Initialize(Transform world)
        {
            scene=new GameObject("Whiskey and tumblers").transform;scene.SetParent(world,false);
            amber=Material("Whiskey amber",new Color(.48f,.20f,.035f),.65f);
            brown=Material("Unlabelled brown glass",new Color(.34f,.20f,.075f,.48f),.88f,true);
            glass=Material("Faceted drinking glass",new Color(.62f,.70f,.67f,.28f),.92f,true);
            var rim=Material("Glass thick rim",new Color(.39f,.43f,.36f),.85f);
            var bottleMesh=Shape("Octagonal whiskey bottle",new Vector4(-.20f,.075f,.06f,0),new Vector4(-.18f,.105f,.075f,0),new Vector4(.07f,.105f,.075f,0),new Vector4(.13f,.045f,.04f,0),new Vector4(.16f,.031f,.031f,0),new Vector4(.23f,.031f,.031f,0));
            var bottleLiquid=Shape("Whiskey inside bottle",new Vector4(-.17f,.086f,.058f,0),new Vector4(.045f,.086f,.058f,0));
            var glassMesh=Shape("Open octagonal tumbler",new Vector4(-.095f,.070f,.070f,0),new Vector4(.095f,.085f,.085f,0),new Vector4(.095f,.071f,.071f,0),new Vector4(-.073f,.059f,.059f,0));
            var drinkMesh=Shape("Drink surface",new Vector4(-.060f,.058f,.058f,0),new Vector4(.035f,.065f,.065f,0));
            var mouth=Shape("Bottle mouth",new Vector4(0,.034f,.034f,0),new Vector4(.018f,.034f,.034f,0),new Vector4(.018f,.022f,.022f,0),new Vector4(0,.022f,.022f,0));
            var a=new Vector3(-.5f,0,-.35f);var b=new Vector3(.55f,0,-.25f);var c=new Vector3(.1f,0,.6f);
            shard=new Mesh{name="Double sided glass fragment"};shard.vertices=new[]{a,b,c,a,c,b};shard.triangles=new[]{0,1,2,3,4,5};shard.RecalculateNormals();owned.Add(shard);
            for(int seat=0;seat<settings.Length;seat++)
            {
                var s=new Setting();settings[seat]=s;
                s.Bottle=Part("Whiskey bottle "+(seat+1),scene,bottleMesh,brown);s.Bottle.position=V(DuelTabletop.Rest(seat,DuelTableProp.Bottle));
                var lip=Part("Unlabelled open neck",s.Bottle,mouth,rim);lip.localPosition=new Vector3(0,.218f,0);
                s.BottleLiquid=Part("Whiskey",s.Bottle,bottleLiquid,amber);
                s.Glass=Part("Tumbler "+(seat+1),scene,glassMesh,glass);s.Glass.position=V(DuelTabletop.Rest(seat,DuelTableProp.Glass));
                s.Drink=Part("Drink",s.Glass,drinkMesh,amber);s.Drink.gameObject.SetActive(false);
                s.BottleRenderers=s.Bottle.GetComponentsInChildren<Renderer>(true);s.GlassRenderers=s.Glass.GetComponentsInChildren<Renderer>(true);
                s.BottleShards=Fragments(seat,"Bottle",brown);s.GlassShards=Fragments(seat,"Glass",glass);
                s.Pour=new GameObject("Whiskey pour stream").AddComponent<LineRenderer>();s.Pour.transform.SetParent(scene,false);s.Pour.sharedMaterial=amber;s.Pour.positionCount=2;s.Pour.startWidth=.012f;s.Pour.endWidth=.008f;s.Pour.numCapVertices=0;s.Pour.enabled=false;
            }
        }
        Material Material(string name,Color color,float smooth,bool transparent=false)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);
            if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;m.SetOverrideTag("RenderType","Transparent");}
            owned.Add(m);return m;
        }
        Mesh Shape(string name,params Vector4[] rings){var mesh=DuelFacetedMesh.Sections(name,rings);owned.Add(mesh);return mesh;}
        Transform Part(string name,Transform parent,Mesh mesh,Material mat)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;}
        Transform[] Fragments(int seat,string kind,Material material)
        {var pieces=new Transform[9];for(int i=0;i<pieces.Length;i++){pieces[i]=Part(kind+" fragment "+seat+"-"+i,scene,shard,material);pieces[i].gameObject.SetActive(false);}return pieces;}
        public void Observe(DuelTabletopState current,int currentRound,double serverNow,DuelWeaponAudio audio,bool ready)
        {
            bool baseline=!initialized||round!=currentRound;state=current;now=serverNow;round=currentRound;initialized=ready;
            for(int seat=0;seat<settings.Length;seat++)
            {
                var s=settings[seat];var e=state.ForSeat(seat);
                bool bottle=state.Broken(seat,DuelTableProp.Bottle),cup=state.Broken(seat,DuelTableProp.Glass);
                if(!baseline&&ready)
                {
                    if(bottle&&s.Bottle.gameObject.activeSelf&&now-e.BottleBrokenAt<.6)audio.PlayTable(seat,"BottleBreak",V(DuelTabletop.Rest(seat,DuelTableProp.Bottle)));
                    if(cup&&s.Glass.gameObject.activeSelf&&now-e.GlassBrokenAt<.6)audio.PlayTable(seat,"GlassBreak",V(DuelTabletop.Rest(seat,DuelTableProp.Glass)));
                }
                s.Bottle.gameObject.SetActive(!bottle);s.Glass.gameObject.SetActive(!cup);s.Pour.enabled=false;
                s.RightGrip=s.LeftGrip=0;s.Bottle.rotation=s.Glass.rotation=Quaternion.Euler(0,180+seat*60,0);
                s.Bottle.position=V(DuelTabletop.Rest(seat,DuelTableProp.Bottle));s.Glass.position=V(DuelTabletop.Rest(seat,DuelTableProp.Glass));
                s.Drink.gameObject.SetActive(!cup&&RouletteRules.Has(state.FilledMask,seat));
                RenderFragments(s.BottleShards,seat,DuelTableProp.Bottle,bottle,e.BottleBrokenAt);
                RenderFragments(s.GlassShards,seat,DuelTableProp.Glass,cup,e.GlassBrokenAt);
            }
        }
        void RenderFragments(Transform[] pieces,int seat,DuelTableProp kind,bool broken,double at)
        {
            var centre=V(DuelTabletop.Rest(seat,kind));float age=Mathf.Clamp((float)(now-at),0,1.4f);
            for(int i=0;i<pieces.Length;i++)
            {
                var p=pieces[i];p.gameObject.SetActive(broken);if(!broken)continue;
                float angle=(i*137+seat*31)*Mathf.Deg2Rad,t=Mathf.Min(age,.6f),speed=.35f+(i%4)*.14f;
                var point=centre+new Vector3(Mathf.Sin(angle)*speed*t,(1.1f+(i%3)*.23f)*t-4.9f*t*t,Mathf.Cos(angle)*speed*t);
                point.y=Mathf.Max(.904f,point.y);float radius=new Vector2(point.x,point.z).magnitude;if(radius>3.67f){point.x*=3.67f/radius;point.z*=3.67f/radius;}
                p.position=point;p.rotation=point.y<=.905f?Quaternion.Euler(0,i*63,0):Quaternion.Euler(t*270+i*31,t*330+i*63,t*180);p.localScale=Vector3.one*(.05f+(i%3)*.025f);
            }
        }
        static float Range(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        public void Targets(int seat,DuelDrinkAction action,float t,Transform root,Transform head,out Vector3 right,out Vector3 left,out Quaternion rightRotation,out Quaternion leftRotation)
        {
            var s=settings[seat];right=root.TransformPoint(BottleRest+new Vector3(-.08f,0,0));left=root.TransformPoint(GlassRest+new Vector3(.075f,0,0));
            rightRotation=root.rotation*Quaternion.Euler(-12,0,-80);leftRotation=root.rotation*Quaternion.Euler(-10,0,75);
            s.Bottle.position=root.TransformPoint(BottleRest);s.Glass.position=root.TransformPoint(GlassRest);s.Bottle.rotation=s.Glass.rotation=root.rotation;
            if(action==DuelDrinkAction.None)return;
            float grip=Range(.07f,.20f,t)*(1-Range(.87f,1,t));
            float lift=Range(.20f,.40f,t)*(1-Range(.70f,.93f,t));
            if(action==DuelDrinkAction.Pour)
            {
                s.RightGrip=s.LeftGrip=grip;
                s.Bottle.position=root.TransformPoint(Vector3.Lerp(BottleRest,new Vector3(-.06f,1.27f,-.86f+DuelGunplay.SeatAdvance),lift));
                s.Bottle.rotation=root.rotation*Quaternion.Euler(0,0,-76*Range(.33f,.47f,t)*(1-Range(.66f,.79f,t)));
                if(!state.Broken(seat,DuelTableProp.Glass))s.Drink.gameObject.SetActive(t>.46f||RouletteRules.Has(state.FilledMask,seat));
            }
            else if(action==DuelDrinkAction.Sip)
            {
                s.LeftGrip=grip;s.Glass.rotation=Quaternion.Slerp(root.rotation,head.rotation*Quaternion.Euler(48,0,0),lift);
                Vector3 lip=head.TransformPoint(new Vector3(0,-.14f,-.18f));Vector3 raised=lip-s.Glass.rotation*new Vector3(0,.085f,.025f);
                s.Glass.position=Vector3.Lerp(root.TransformPoint(GlassRest),raised,lift);
            }
            else
            {
                s.RightGrip=grip;s.Bottle.position=root.TransformPoint(Vector3.Lerp(BottleRest,new Vector3(-.30f,1.39f-DuelGunplay.SeatedDrop,-.35f),lift));
                s.Bottle.rotation=root.rotation*Quaternion.Euler(Mathf.Sin(t*12)*9*lift,Mathf.Sin(t*10)*45*lift,Mathf.Sin(t*8)*12*lift);
            }
            s.RightOffset=root.TransformDirection(new Vector3(.08f,0,0));s.LeftOffset=root.TransformDirection(new Vector3(-.075f,0,0));
            right=s.Bottle.position-s.RightOffset;left=s.Glass.position-s.LeftOffset;
        }
        public float RightGrip(int seat)=>settings[seat].RightGrip;
        public float LeftGrip(int seat)=>settings[seat].LeftGrip;
        public void SetLocalVisibility(int localSeat,bool hideOwnAnimation)
        {
            for(int seat=0;seat<settings.Length;seat++)
            {
                var s=settings[seat];bool own=hideOwnAnimation&&seat==localSeat;
                foreach(var renderer in s.BottleRenderers)renderer.forceRenderingOff=own&&s.RightGrip>0;
                foreach(var renderer in s.GlassRenderers)renderer.forceRenderingOff=own&&s.LeftGrip>0;
                s.Pour.forceRenderingOff=own;
            }
        }
        public void AlignHeld(int seat,Transform right,Transform left,DuelDrinkAction action,float t)
        {
            var s=settings[seat];s.Bottle.position=Vector3.Lerp(s.Bottle.position,right.position+s.RightOffset,s.RightGrip);
            s.Glass.position=Vector3.Lerp(s.Glass.position,left.position+s.LeftOffset,s.LeftGrip);
            s.Pour.enabled=action==DuelDrinkAction.Pour&&t>.44f&&t<.66f&&!state.Broken(seat,DuelTableProp.Bottle)&&!state.Broken(seat,DuelTableProp.Glass);
            if(s.Pour.enabled){s.Pour.SetPosition(0,s.Bottle.TransformPoint(new Vector3(0,.24f,0)));s.Pour.SetPosition(1,s.Glass.TransformPoint(new Vector3(0,.04f,0)));}
        }
        void OnDestroy(){foreach(var o in owned)if(o!=null)Destroy(o);}
    }
}
