using UnityEngine;
namespace HelloWorld
{
    // Cosmetic neck motion only; raw camera angles still drive server gunplay.
    internal struct DuelHeadLook
    {
        public float Yaw,Pitch,Roll;
        private float yawSpeed,pitchSpeed,rollSpeed;
        private bool initialized;
        public void Step(float yaw,float pitch,float delta)
        {
            yaw=Mathf.Clamp(yaw,-85,85);pitch=Mathf.Clamp(pitch,-45,55);
            if(!initialized){Yaw=yaw;Pitch=pitch;Roll=-yaw*.025f;initialized=true;return;}
            if(delta<=0)return;
            // Critically damped follow keeps velocity between network samples and never overshoots.
            float dt=Mathf.Min(delta,.05f);
            Yaw=Mathf.Clamp(Mathf.SmoothDampAngle(Yaw,yaw,ref yawSpeed,.085f,600,dt),-85,85);
            Pitch=Mathf.Clamp(Mathf.SmoothDamp(Pitch,pitch,ref pitchSpeed,.11f,360,dt),-45,55);
            Roll=Mathf.Clamp(Mathf.SmoothDamp(Roll,-Yaw*.025f,ref rollSpeed,.16f,24,dt),-2.2f,2.2f);
        }
    }
}
