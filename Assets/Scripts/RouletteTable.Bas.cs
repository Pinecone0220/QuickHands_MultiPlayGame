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
        private void BuildBas(Material black,Material steel,Material brass)
        {
            var stock=Mat("BAS dark walnut stock",new Color(.21f,.13f,.085f),.05f,.25f);
            var receiver=Mat("BAS brushed silver receiver",new Color(.47f,.49f,.47f),.85f,.55f);
            var pad=Mat("BAS rubber",new Color(.055f,.06f,.055f),0,.15f);
            gun=new GameObject("BAS single-load lever-action rifle").transform;gun.SetParent(world,false);gun.localScale=Vector3.one*.60f;
            Part("Sculpted walnut fore-end",PrimitiveType.Capsule,gun,new Vector3(0,-.035f,.38f),new Vector3(.145f,.36f,.14f),stock).localRotation=Quaternion.Euler(90,0,0);
            Part("Silver receiver",PrimitiveType.Cube,gun,new Vector3(0,.04f,-.035f),new Vector3(.14f,.16f,.42f),receiver);
            foreach(float z in new[]{-.19f,.15f})Part("Receiver screw",PrimitiveType.Sphere,gun,new Vector3(.073f,.01f,z),Vector3.one*.025f,black);
            Part("Open right ejection port",PrimitiveType.Cube,gun,new Vector3(.069f,.075f,-.005f),new Vector3(.008f,.075f,.19f),black);
            Axial("Free-floating rifle barrel",gun,new Vector3(0,.075f,.79f),.055f,1.10f,black);
            Axial("Under-barrel tube",gun,new Vector3(0,-.015f,.80f),.052f,.91f,black);
            Part("Front barrel band",PrimitiveType.Cube,gun,new Vector3(0,.025f,1.12f),new Vector3(.078f,.15f,.038f),steel);
            Axial("BAS dark muzzle bore",gun,new Vector3(0,.075f,1.342f),.035f,.008f,black);
            Ring("BAS crown",gun,new Vector3(0,.075f,1.347f),.026f,.006f,steel,Quaternion.identity);
            var neck=Part("Swept stock wrist",PrimitiveType.Cube,gun,new Vector3(0,-.14f,-.34f),new Vector3(.12f,.17f,.30f),stock);neck.localRotation=Quaternion.Euler(-30,0,0);
            var butt=Part("Angled shoulder stock",PrimitiveType.Cube,gun,new Vector3(0,-.15f,-.64f),new Vector3(.16f,.23f,.47f),stock);butt.localRotation=Quaternion.Euler(9,0,0);
            Part("Cheek rest",PrimitiveType.Cube,gun,new Vector3(0,-.02f,-.62f),new Vector3(.17f,.075f,.32f),pad);
            Part("Butt recoil pad",PrimitiveType.Cube,gun,new Vector3(0,-.15f,-.89f),new Vector3(.18f,.26f,.045f),pad);
            Part("BAS trigger",PrimitiveType.Cube,gun,new Vector3(0,-.15f,-.16f),new Vector3(.02f,.085f,.025f),steel);
            foreach(float z in new[]{-.19f,.22f})
            {
                Part("Scope riser",PrimitiveType.Cube,gun,new Vector3(0,.16f,z),new Vector3(.075f,.12f,.065f),black);
                Ring("Scope mounting ring",gun,new Vector3(0,.265f,z),.066f,.014f,steel,Quaternion.identity);
            }
            Axial("Telescopic sight tube",gun,new Vector3(0,.265f,.015f),.10f,.63f,black);
            Axial("Scope ocular",gun,new Vector3(0,.265f,-.33f),.15f,.14f,pad);
            Axial("Scope objective bell",gun,new Vector3(0,.265f,.38f),.17f,.16f,black);
            var lens=Mat("BAS blue-green glass",new Color(.10f,.22f,.20f),.55f,.8f);
            Axial("Objective glass",gun,new Vector3(0,.265f,.464f),.145f,.008f,lens);
            Axial("Ocular glass",gun,new Vector3(0,.265f,-.405f),.125f,.008f,lens);
            Part("Elevation turret",PrimitiveType.Cylinder,gun,new Vector3(0,.35f,.04f),new Vector3(.08f,.035f,.08f),black);
            barrelPivot=new GameObject("BAS lower lever pivot").transform;barrelPivot.SetParent(gun,false);barrelPivot.localPosition=new Vector3(0,-.075f,-.025f);
            var loop=new[]{new Vector3(0,-.02f,.035f),new Vector3(0,-.16f,.005f),new Vector3(0,-.22f,-.08f),new Vector3(0,-.22f,-.25f),new Vector3(0,-.16f,-.33f),new Vector3(0,-.045f,-.31f),new Vector3(0,-.025f,-.13f)};
            for(int i=0;i<loop.Length;i++)
            {
                var a=loop[i];var b=loop[(i+1)%loop.Length];var segment=Part("Lever loop "+i,PrimitiveType.Cube,barrelPivot,(a+b)*.5f,new Vector3(.028f,(b-a).magnitude,.026f),steel);
                segment.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }
            Part("Lever hinge pin",PrimitiveType.Sphere,gun,barrelPivot.localPosition+new Vector3(.075f,0,0),Vector3.one*.04f,steel);
            barrelLatch=new GameObject("BAS internal breech slide").transform;barrelLatch.SetParent(gun,false);
            Axial("Internal breech",barrelLatch,new Vector3(0,.075f,-.12f),.065f,.22f,steel);
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
