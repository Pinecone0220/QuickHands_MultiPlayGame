using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteTable
    {
        private Transform BuildBasCase(Material brass,Material black)
        {
            var shell=new GameObject("BAS cosmetic ejected case").transform;shell.SetParent(world,false);
            Axial("Empty brass case",shell,Vector3.zero,.027f,.10f,brass);
            Axial("Case rim",shell,new Vector3(0,0,-.05f),.032f,.008f,brass);
            Axial("Empty case mouth",shell,new Vector3(0,0,.051f),.020f,.003f,black);
            shell.gameObject.SetActive(false);return shell;
        }
        private void ObserveBasCase(GunRig rig,DuelActionState state,int round,double now,bool eligible)
        {
            bool baseline=!rig.BasSeen||rig.BasRound!=round||rig.BasLife!=state.Life||rig.BasSession!=state.PracticeSession;
            bool extracted=state.SpentMask==0&&(state.Action==DuelAction.Load||state.Action==DuelAction.CloseBarrel||state.Action==DuelAction.Aim);
            if(!eligible||baseline)rig.CaseAt=-1;
            else if(extracted&&(rig.BasSpent!=0||state.ShotSerial>rig.BasShots)&&now-state.StartedAt>=-.1&&now-state.StartedAt<.75)
            {
                rig.CaseAt=now;rig.CaseOrigin=rig.Gun.TransformPoint(new Vector3(.08f,.075f,-.01f));rig.CaseRotation=rig.Gun.rotation;
            }
            rig.BasSeen=eligible;rig.BasRound=round;rig.BasLife=state.Life;rig.BasSession=state.PracticeSession;
            rig.BasSpent=state.SpentMask;rig.BasShots=state.ShotSerial;
            float age=(float)(now-rig.CaseAt);bool visible=eligible&&rig.CaseAt>=0&&age>=0&&age<.85f;
            rig.EjectedCase.gameObject.SetActive(visible);if(!visible)return;
            rig.EjectedCase.position=rig.CaseOrigin+rig.CaseRotation*new Vector3(age,age*2.4f,-age*.25f)+Vector3.down*(2.8f*age*age);
            rig.EjectedCase.rotation=rig.CaseRotation*Quaternion.Euler(age*420,age*650,age*100);
        }
        // Side-profile prisms keep the characteristic straight-stock lever-action silhouette.
        private Transform BasProfile(string name,Material material,float halfWidth,params Vector2[] outline)
        {
            int n=outline.Length;var vertices=new Vector3[n*2];var triangles=new System.Collections.Generic.List<int>();
            for(int i=0;i<n;i++){vertices[i]=new Vector3(-halfWidth,outline[i].y,outline[i].x);vertices[i+n]=new Vector3(halfWidth,outline[i].y,outline[i].x);}
            for(int i=1;i<n-1;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);triangles.Add(n);triangles.Add(n+i+1);triangles.Add(n+i);}
            for(int i=0;i<n;i++){int j=(i+1)%n;triangles.Add(i);triangles.Add(i+n);triangles.Add(j+n);triangles.Add(i);triangles.Add(j+n);triangles.Add(j);}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            return MeshPart(name,mesh,gun,Vector3.zero,Vector3.one,material);
        }
        private void BuildBas(Material black,Material steel,Material brass)
        {
            var stock=Mat("BAS dark walnut stock",new Color(.21f,.13f,.085f),.05f,.25f);
            var receiver=Mat("BAS brushed silver receiver",new Color(.47f,.49f,.47f),.85f,.55f);
            var pad=Mat("BAS rubber",new Color(.055f,.06f,.055f),0,.15f);
            gun=new GameObject("BAS single-load lever-action rifle").transform;gun.SetParent(world,false);gun.localScale=Vector3.one*.60f;
            BasProfile("Straight walnut fore-end",stock,.069f,new Vector2(.14f,-.09f),new Vector2(.72f,-.09f),new Vector2(.76f,-.065f),new Vector2(.76f,.025f),new Vector2(.15f,.025f));
            BasProfile("Silver lever-action receiver",receiver,.07f,new Vector2(-.25f,-.065f),new Vector2(-.20f,-.10f),new Vector2(.12f,-.10f),new Vector2(.18f,-.065f),new Vector2(.18f,.11f),new Vector2(-.20f,.11f),new Vector2(-.25f,.075f));
            foreach(float z in new[]{-.19f,.15f})Part("Receiver screw",PrimitiveType.Sphere,gun,new Vector3(.073f,.01f,z),Vector3.one*.025f,black);
            Part("Open right ejection port",PrimitiveType.Cube,gun,new Vector3(.069f,.075f,-.005f),new Vector3(.008f,.075f,.19f),black);
            Axial("Free-floating rifle barrel",gun,new Vector3(0,.075f,.79f),.055f,1.10f,black);
            Axial("Under-barrel tube",gun,new Vector3(0,-.015f,.80f),.052f,.91f,black);
            Part("Fore-end barrel band",PrimitiveType.Cube,gun,new Vector3(0,.005f,.72f),new Vector3(.15f,.16f,.035f),steel);
            Part("Front barrel band",PrimitiveType.Cube,gun,new Vector3(0,.025f,1.24f),new Vector3(.078f,.15f,.038f),steel);
            Axial("BAS dark muzzle bore",gun,new Vector3(0,.075f,1.342f),.035f,.008f,black);
            Ring("BAS crown",gun,new Vector3(0,.075f,1.347f),.026f,.006f,steel,Quaternion.identity);
            BasProfile("Lever-action walnut shoulder stock",stock,.075f,
                new Vector2(-.95f,-.28f),new Vector2(-.68f,-.24f),new Vector2(-.37f,-.14f),new Vector2(-.23f,-.095f),
                new Vector2(-.25f,.005f),new Vector2(-.43f,-.045f),new Vector2(-.66f,-.01f),new Vector2(-.95f,.015f));
            var buttPad=Part("Butt recoil pad",PrimitiveType.Cube,gun,new Vector3(0,-.13f,-.955f),new Vector3(.165f,.30f,.025f),pad);buttPad.localRotation=Quaternion.Euler(0,0,0);
            Part("BAS trigger",PrimitiveType.Cube,gun,new Vector3(0,-.15f,-.16f),new Vector3(.02f,.085f,.025f),steel);
            // Compact open sights at equal height make the bore line visible in ADS.
            Part("Rear sight base",PrimitiveType.Cube,gun,new Vector3(0,.112f,-.18f),new Vector3(.065f,.018f,.035f),black);
            foreach(float x in new[]{-.025f,.025f})Part("Rear sight notch",PrimitiveType.Cube,gun,new Vector3(x,.135f,-.18f),new Vector3(.015f,.032f,.020f),steel);
            Part("Front sight base",PrimitiveType.Cube,gun,new Vector3(0,.111f,1.20f),new Vector3(.04f,.016f,.050f),black);
            Part("Front sight blade",PrimitiveType.Cube,gun,new Vector3(0,.135f,1.20f),new Vector3(.012f,.032f,.020f),steel);
            barrelPivot=new GameObject("BAS lower lever pivot").transform;barrelPivot.SetParent(gun,false);barrelPivot.localPosition=new Vector3(0,-.075f,-.025f);
            var loop=new[]{new Vector3(0,-.02f,.035f),new Vector3(0,-.16f,.005f),new Vector3(0,-.22f,-.08f),new Vector3(0,-.22f,-.25f),new Vector3(0,-.16f,-.33f),new Vector3(0,-.045f,-.31f),new Vector3(0,-.025f,-.13f)};
            for(int i=0;i<loop.Length;i++)
            {
                var a=loop[i];var b=loop[(i+1)%loop.Length];var segment=Part("Lever loop "+i,PrimitiveType.Cube,barrelPivot,(a+b)*.5f,new Vector3(.028f,(b-a).magnitude,.026f),steel);
                segment.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }
            Part("Lever hinge pin",PrimitiveType.Sphere,gun,barrelPivot.localPosition+new Vector3(.075f,0,0),Vector3.one*.04f,steel);
            barrelLatch=new GameObject("BAS internal breech slide").transform;barrelLatch.SetParent(gun,false);

            hammer=new GameObject("BAS internal striker").transform;hammer.SetParent(gun,false);
            cartridges[0]=new GameObject("BAS hand-fed rifle cartridge").transform;cartridges[0].SetParent(gun,false);
            Axial("Brass rifle case",cartridges[0],Vector3.zero,.038f,.15f,brass);
            Axial("Cartridge rim",cartridges[0],new Vector3(0,0,-.08f),.046f,.012f,brass);
            Part("Bullet tip",PrimitiveType.Sphere,cartridges[0],new Vector3(0,0,.09f),new Vector3(.033f,.033f,.065f),liveTip);
            cartridges[0].gameObject.SetActive(false);
            muzzle=new GameObject("BAS muzzle").transform;muzzle.SetParent(gun,false);muzzle.localPosition=new Vector3(0,.075f,1.34f);
            var glow=Mat("BAS muzzle flash",new Color(1,.7f,.15f),0,0);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(8,3,.3f));
            flash=Part("BAS flash",PrimitiveType.Sphere,muzzle,Vector3.zero,new Vector3(.10f,.10f,.30f),glow).gameObject;flash.SetActive(false);
            muzzleLight=muzzle.gameObject.AddComponent<Light>();muzzleLight.type=LightType.Point;muzzleLight.color=new Color(1,.6f,.18f);muzzleLight.range=3;
        }
    }
}
