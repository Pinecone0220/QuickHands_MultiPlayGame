using UnityEngine;

namespace HelloWorld
{
    // Presentation only: never read hidden mini-game records or write gameplay state.
    internal struct DuelPoseContext
    {
        public int Seat;
        public double Time;
        public DuelAction Action;
        public DuelWeapon Weapon;
        public bool Active,GunReady,Aiming,Attentive,Stunned,Winner,ViewModel,Combat;
        public bool Ink;public float InkAge;
        public float Progress,ActionAge,Recoil,Healing;
        public DuelDrinkAction DrinkAction;
        public float DrinkProgress;
    }
    internal struct DuelCharacterPose
    {
        public Vector3 Right,Left,RightElbow,LeftElbow,RightEuler,LeftEuler;
        public float Lean,Roll,Breath,Relaxed,Support,Drum,RightCurl,LeftCurl,RightChin,LeftChin;
        public float HeadLift;
        public static float DrinkWeight(float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.20f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,t)));
        public static DuelCharacterPose Blend(DuelCharacterPose a,DuelCharacterPose b,float t)
        {
            return new DuelCharacterPose{
                Right=Vector3.Lerp(a.Right,b.Right,t),Left=Vector3.Lerp(a.Left,b.Left,t),
                RightElbow=Vector3.Lerp(a.RightElbow,b.RightElbow,t),LeftElbow=Vector3.Lerp(a.LeftElbow,b.LeftElbow,t),
                RightEuler=Vector3.Lerp(a.RightEuler,b.RightEuler,t),LeftEuler=Vector3.Lerp(a.LeftEuler,b.LeftEuler,t),
                Lean=Mathf.Lerp(a.Lean,b.Lean,t),Roll=Mathf.Lerp(a.Roll,b.Roll,t),Breath=Mathf.Lerp(a.Breath,b.Breath,t),RightChin=Mathf.Lerp(a.RightChin,b.RightChin,t),LeftChin=Mathf.Lerp(a.LeftChin,b.LeftChin,t),HeadLift=Mathf.Lerp(a.HeadLift,b.HeadLift,t),
                Relaxed=Mathf.Lerp(a.Relaxed,b.Relaxed,t),Support=Mathf.Lerp(a.Support,b.Support,t),Drum=Mathf.Lerp(a.Drum,b.Drum,t),RightCurl=Mathf.Lerp(a.RightCurl,b.RightCurl,t),LeftCurl=Mathf.Lerp(a.LeftCurl,b.LeftCurl,t)};
        }
        static DuelCharacterPose Idle(int style,float time)
        {
            var p=new DuelCharacterPose{Right=new Vector3(-.32f,.03f,-.50f),Left=new Vector3(.32f,.03f,-.50f),RightElbow=new Vector3(-.32f,.15f,-.32f),LeftElbow=new Vector3(.32f,.15f,-.32f),Relaxed=1};
            switch(style%6)
            {
                case 0: // Lean onto both elbows, with clasped hands below the chin.
                    p.Right=new Vector3(-.07f,.57f,-.28f);p.Left=new Vector3(.07f,.58f,-.29f);
                    p.RightEuler=new Vector3(-20,-70,-15);p.LeftEuler=new Vector3(-20,70,15);p.RightCurl=p.LeftCurl=18;p.Lean=-15;p.RightChin=p.LeftChin=1;break;
                case 1: // Chin support and one resting hand.
                    p.Right=new Vector3(-.13f,.66f,-.20f);p.Left=new Vector3(.20f,.03f,-.50f);
                    p.RightEuler=new Vector3(-28,-12,-62);p.LeftEuler=new Vector3(0,20,0);p.RightCurl=50;p.Lean=-15;p.RightChin=1;break;
                case 2: // Crossed forearms at different depths.
                    p.Right=new Vector3(.19f,.035f,-.54f);p.Left=new Vector3(-.19f,.12f,-.47f);
                    p.RightEuler=new Vector3(0,-70,0);p.LeftEuler=new Vector3(0,70,0);p.Lean=-6;break;
                case 3: // Palms on the table, attentive posture.
                    p.Right=new Vector3(-.32f,.025f,-.50f);p.Left=new Vector3(.32f,.025f,-.50f);
                    p.RightEuler=new Vector3(0,-12,0);p.LeftEuler=new Vector3(0,12,0);p.Lean=-8;break;
                case 4: // Cosmetic finger drumming, unrelated to player input.
                    p.Right=new Vector3(-.25f,.03f,-.50f);p.Left=new Vector3(.30f,.03f,-.50f);
                    p.RightEuler=new Vector3(0,-18,0);p.Drum=1;p.Roll=.7f;p.Lean=-6;break;
                case 5:
                    p.Right=new Vector3(-.28f,.03f,-.50f);p.Left=new Vector3(.15f,.66f,-.22f);
                    p.LeftEuler=new Vector3(-25,15,62);p.LeftCurl=50;p.Lean=-15;p.LeftChin=1;break;
            }
            p.Breath=Mathf.Sin(time*1.45f)*.004f;p.Roll+=Mathf.Sin(time*.53f)*.35f;return p;
        }
        public static DuelCharacterPose Sample(DuelPoseContext c)
        {
            double cycle=c.Time/18+c.Seat*.09;int step=(int)System.Math.Floor(cycle);float fraction=(float)(cycle-step);
            float change=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,1,fraction));float time=(float)(c.Time%10000)+c.Seat*1.7f;
            var p=Blend(Idle((c.Seat+step)%6,time),Idle((c.Seat+step+1)%6,time),change);
            if(c.Combat&&!c.Active&&!c.Winner)p=Idle(3,time);
            if(c.DrinkAction!=DuelDrinkAction.None&&!c.Active&&!c.Combat)
            {
                float t=c.DrinkProgress,weight=DrinkWeight(t);
                float raised=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.40f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,.93f,t)));
                float lean=c.DrinkAction==DuelDrinkAction.Pour?1:1-raised*.8f;
                var drinking=new DuelCharacterPose{
                    Right=new Vector3(-.32f,.03f,-.50f),Left=new Vector3(.32f,.03f,-.50f),
                    RightCurl=38,LeftCurl=38,Lean=Mathf.Lerp(-5,-12,lean),Breath=p.Breath,Roll=p.Roll*.2f};
                drinking.HeadLift=c.DrinkAction==DuelDrinkAction.Sip?raised*8:-3;
                p=Blend(p,drinking,weight);
            }
            if(c.Attentive)p.Lean-=.7f;
            if(c.Active)
            {
                p.Relaxed=0;p.Drum=0;p.RightChin=p.LeftChin=0;p.RightCurl=p.LeftCurl=0;p.Roll*=.2f;p.Lean=-6;
                p.Right=new Vector3(-.32f,.09f,-.54f);p.Left=new Vector3(.30f,.03f,-.53f);p.RightEuler=Vector3.zero;p.LeftEuler=new Vector3(0,15,0);
                if(!c.GunReady)
                {
                    if(c.Action==DuelAction.CockHammer){p.Lean=-7;p.Roll=-1;}
                    if(c.Action==DuelAction.UnlockBarrel||c.Action==DuelAction.OpenBarrel){p.Lean=-8;p.Roll=1;}
                    if(c.Action==DuelAction.Load){p.Lean=-9;p.Roll=-1.3f;}
                    if(c.Action==DuelAction.CloseBarrel){p.Lean=-7;p.Roll=.8f;}
                }
                p.Support=c.GunReady&&(c.Weapon==DuelWeapon.SawedOff||c.Weapon==DuelWeapon.BAS||c.Aiming)?1:0;p.LeftCurl=p.Support*30;p.Lean+=c.Recoil*3;
            }
            if(c.Ink)
            {
                p.RightChin=p.LeftChin=p.Support=p.Drum=p.Relaxed=0;p.RightCurl=55;p.LeftCurl=15;
                p.Right=new Vector3(-.28f,.30f,-.4f);p.Left=new Vector3(.28f,.03f,-.38f);
                p.Lean=-3-(c.InkAge>=0&&c.InkAge<.4f?Mathf.Sin(c.InkAge/.4f*Mathf.PI)*5:0);
            }
            if(c.Healing>0&&!c.Active)
            {
                float weight=Mathf.Sin(c.Healing*Mathf.PI);p.Left=Vector3.Lerp(p.Left,new Vector3(-.12f,.36f,-.24f),weight);
                p.LeftEuler=Vector3.Lerp(p.LeftEuler,new Vector3(-12,-60,10),weight);p.Lean+=weight;p.Drum*=1-weight;p.LeftChin*=1-weight;
            }
            if(c.Winner)
            {
                p.Right=new Vector3(-.27f,.83f,-.20f);p.RightElbow=new Vector3(-.40f,.42f,-.25f);
                p.RightEuler=new Vector3(-35,0,-35);p.Left=new Vector3(.30f,.03f,-.53f);p.RightCurl=55;p.LeftCurl=0;p.Lean=1.5f;p.Drum=0;p.RightChin=p.LeftChin=0;
            }
            if(c.Stunned){p.Roll+=Mathf.Sin(time*37)*.8f;p.Lean+=Mathf.Sin(time*31)*.6f;}return p;
        }
        public static void Preparation(DuelAction action,DuelWeapon weapon,float progress,out Vector3 offset,out Vector3 euler)
        {
            offset=Vector3.zero;euler=Vector3.zero;float t=Mathf.SmoothStep(0,1,progress);
            switch(action)
            {
                case DuelAction.CockHammer:offset=new Vector3(0,.055f,.025f);euler=new Vector3(8,-6,-12);break;
                case DuelAction.UnlockBarrel:offset=new Vector3(.035f,-.025f,.055f);euler=new Vector3(4,8,-24);break;
                case DuelAction.OpenBarrel:offset=new Vector3(0,-.04f,-.025f);euler=new Vector3(-10,3,-28);break;
                case DuelAction.Load:offset=new Vector3(.02f,-.025f,.02f);euler=new Vector3(-6,18,-25);break;
                case DuelAction.CloseBarrel:offset=new Vector3(.025f,.015f,-.025f);euler=weapon==DuelWeapon.Revolver?new Vector3(10,-15,-42):new Vector3(Mathf.Lerp(-6,6,t),0,8*(1-t));break;
            }
        }
    }
    internal static class DuelArmIK
    {
        public const float Upper=.40f,Forearm=.36f,Reach=Upper+Forearm-.01f;
        // The sleeve's underside, rather than its centre, touches the .90 m tabletop.
        public const float TableElbowY=.97f;
        public static readonly Vector3 RightBrace=new Vector3(-.34f,.96f,-.58f),LeftBrace=new Vector3(.34f,.96f,-.58f);
        public static Vector3 Limit(Vector3 shoulder,Vector3 target)
        {
            var delta=target-shoulder;float length=delta.magnitude;
            return shoulder+(length>.00001f?delta/length:Vector3.down)*Mathf.Clamp(length,Mathf.Abs(Upper-Forearm)+.01f,Reach);
        }
        public static void Solve(Vector3 shoulder,Vector3 target,Vector3 hint,out Vector3 elbow,out Vector3 wrist)
        {
            wrist=Limit(shoulder,target);var delta=wrist-shoulder;float d=delta.magnitude;var axis=delta/d;
            var pole=Vector3.ProjectOnPlane(hint-shoulder,axis);
            if(pole.sqrMagnitude<.000001f)pole=Vector3.Cross(axis,Vector3.forward);
            if(pole.sqrMagnitude<.000001f)pole=Vector3.Cross(axis,Vector3.right);
            float along=(Upper*Upper-Forearm*Forearm+d*d)/(2*d);
            elbow=shoulder+axis*along+pole.normalized*Mathf.Sqrt(Mathf.Max(0,Upper*Upper-along*along));
        }
        public static bool TableElbow(Vector3 shoulder,Vector3 wrist,float height,Vector3 hint,out Vector3 elbow)
        {
            // Intersect the two bone-length spheres with the tabletop plane.
            elbow=hint;float a=Upper*Upper-Mathf.Pow(shoulder.y-height,2),b=Forearm*Forearm-Mathf.Pow(wrist.y-height,2);
            var delta=new Vector3(wrist.x-shoulder.x,0,wrist.z-shoulder.z);float d=delta.magnitude;
            if(a<0||b<0||d<.00001f)return false;
            float along=(a-b+d*d)/(2*d),sideSquared=a-along*along;
            if(sideSquared<0)return false;
            var axis=delta/d;var centre=new Vector3(shoulder.x,height,shoulder.z)+axis*along;
            var offset=Vector3.Cross(Vector3.up,axis)*Mathf.Sqrt(sideSquared);
            elbow=(centre+offset-hint).sqrMagnitude<(centre-offset-hint).sqrMagnitude?centre+offset:centre-offset;
            return true;
        }
    }
}
