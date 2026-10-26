using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteTable
    {
        public Transform InkHand(int seat)=>puppets[seat].Hand;
        public Transform InkHead(int seat)=>puppets[seat].Head;
        public Transform CreateInkViewHand(Transform parent,int seat)=>AvatarHand("Ink view hand",parent,Vector3.zero,new Vector3(.13f,.10f,.16f),skinMaterials[seat],true);
        public static Vector3 InkHandPoint(int seat,float yaw,float pitch,float age)
        {
            var eye=Vec(DuelGunplay.Eye(seat));var facing=Quaternion.Euler(-pitch,yaw,0);
            var held=new Vector3(.24f,-.30f,.40f);var wind=new Vector3(.25f,-.14f,.20f);var release=new Vector3(.24f,-.10f,.65f);
            Vector3 offset=held;
            if(age>=0&&age<.08f)offset=Vector3.Lerp(held,wind,Smooth(age/.08f));
            else if(age>=.08f&&age<.16f)offset=Vector3.Lerp(wind,release,Smooth((age-.08f)/.08f));
            else if(age>=.16f&&age<DuelInk.ThrowDuration)offset=Vector3.Lerp(release,held,Smooth((age-.16f)/(.65f-.16f)));
            return eye+facing*offset;
        }
    }
}
