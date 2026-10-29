using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteTable
    {
        private static readonly Vector3 BasGripWrist=new Vector3(.075f,-.20f,-.36f);
        private static readonly Vector3 BasSupportWrist=new Vector3(-.11f,-.16f,.36f);
        private Transform BasHandRoot(string name,Transform parent,Vector3 position)
        {var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=position;return root;}
        private void BasFinger(Transform parent,string name,Material skin,float width,params Vector3[] points)
        {
            for(int i=0;i<points.Length-1;i++)
            {
                var a=points[i];var b=points[i+1];
                var segment=MeshPart(name+" "+i,sleeveMesh,parent,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b)*.5f,width),skin);
                segment.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            }
        }
        private Transform BuildBasGrip(Material skin)
        {
            var hand=BasHandRoot("BAS right grip",gun,BasGripWrist);
            MeshPart("Palm",handMesh,hand,new Vector3(0,.025f,.06f),new Vector3(.075f,.14f,.16f),skin);
            for(int i=0;i<3;i++)
            {
                float z=.015f+i*.045f;
                BasFinger(hand,"Curled finger "+i,skin,.034f,new Vector3(-.005f,-.02f,z),new Vector3(-.035f,-.055f,z),new Vector3(-.10f,-.055f,z),new Vector3(-.13f,-.005f,z));
            }
            BasFinger(hand,"Trigger finger",skin,.030f,new Vector3(-.005f,.055f,.14f),new Vector3(-.025f,.045f,.19f),new Vector3(-.065f,.04f,.20f));
            BasFinger(hand,"Thumb",skin,.040f,new Vector3(-.025f,.095f,.01f),new Vector3(-.065f,.10f,.04f),new Vector3(-.08f,.08f,.08f));
            return hand;
        }
        private Transform BuildBasSupport(Material skin)
        {
            var hand=BasHandRoot("BAS left support",gun,BasSupportWrist);
            MeshPart("Supporting palm",handMesh,hand,new Vector3(.08f,.025f,.065f),new Vector3(.18f,.065f,.22f),skin);
            for(int i=0;i<4;i++)
            {
                float z=-.03f+i*.05f;
                BasFinger(hand,"Wrapped finger "+i,skin,.033f,new Vector3(.11f,.015f,z),new Vector3(.195f,.03f,z),new Vector3(.22f,.095f,z+.005f),new Vector3(.19f,.135f,z+.015f));
            }
            BasFinger(hand,"Support thumb",skin,.040f,new Vector3(.02f,.075f,.14f),new Vector3(.015f,.13f,.10f),new Vector3(.065f,.15f,.065f));
            return hand;
        }
        private Transform BuildBasPinch(Material skin)
        {
            var hand=BasHandRoot("BAS cartridge hand",world,Vector3.zero);hand.localScale=Vector3.one*.60f;
            MeshPart("Palm",handMesh,hand,new Vector3(.06f,.035f,.04f),new Vector3(.13f,.065f,.17f),skin);
            BasFinger(hand,"Pinching index",skin,.032f,new Vector3(.12f,.07f,.10f),new Vector3(.16f,.08f,.13f),new Vector3(.17f,.07f,.16f));
            BasFinger(hand,"Pinching thumb",skin,.038f,new Vector3(.08f,.015f,.08f),new Vector3(.12f,.025f,.13f),new Vector3(.13f,.04f,.16f));
            for(int i=0;i<3;i++)
            {
                float z=-.005f+i*.035f;
                BasFinger(hand,"Resting finger "+i,skin,.030f,new Vector3(.11f,.015f,z),new Vector3(.14f,-.015f,z),new Vector3(.10f,-.03f,z));
            }
            return hand;
        }
        private static void PlaceBasView(GunRig rig,Vector3 eye,Quaternion facing,Vector3 offset,Quaternion pose)
        {
            // Smooth the raising/reload motion in view space; mouse rotation is never delayed.
            float blend=1-Mathf.Exp(-Time.deltaTime*18);
            rig.ViewOffset=rig.ViewPoseInitialized?Vector3.Lerp(rig.ViewOffset,offset,blend):offset;
            rig.ViewRotation=rig.ViewPoseInitialized?Quaternion.Slerp(rig.ViewRotation,pose,blend):pose;
            rig.ViewPoseInitialized=true;
            rig.Gun.position=eye+facing*rig.ViewOffset;
            rig.Gun.rotation=facing*rig.ViewRotation;
        }
    }
}
