using System;
using HelloWorld;
class InkAimTests
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Main()
    {
        foreach(int seat in new[]{0,1,2,3,4,5})
        foreach(float relativeYaw in new[]{-100f,-45f,0f,45f,100f})
        foreach(float pitch in new[]{-50f,0f,50f})
        {
            float yaw=seat*60+relativeYaw;
            var eye=DuelGunplay.Eye(seat);var right=DuelGunplay.Direction(yaw+90,0);
            var ink=DuelInkState.Empty;ink.Origin=DuelInk.ThrowOrigin(seat,yaw,pitch);
            ink.Direction=DuelGunplay.Direction(yaw,pitch);ink.ThrownAt=1;
            foreach(double elapsed in new[]{0.0,.2,.6,1.2})
            {
                var point=DuelInk.Position(ink,1+DuelInk.ReleaseDelay+elapsed);
                Check(Math.Abs(AimVector.Dot(point-eye,right))<.00002,"throw remains horizontally centered at every distance");
            }
            double t=.6;
            var straight=ink.Origin+ink.Direction*(DuelInk.Speed*(float)t);
            var arc=DuelInk.Position(ink,1+DuelInk.ReleaseDelay+t);
            Check(Math.Abs((arc.Y-straight.Y)-(DuelInk.UpwardSpeed*t-.5*DuelInk.Gravity*t*t))<.0001,"gravity and initial lift preserved");
        }
        for(int actor=0;actor<6;actor++)for(int target=0;target<6;target++)if(actor!=target)
        {
            var delta=DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor);
            DuelGunplay.Angles(delta,out float yaw,out float pitch);
            var ink=DuelInkState.Empty;ink.Origin=DuelInk.ThrowOrigin(actor,yaw,pitch);
            ink.Direction=DuelGunplay.Direction(yaw,pitch);ink.ThrownAt=1;
            var flight=new DuelInkFlight(actor,1,1,ink);
            bool finished=flight.Tick(1<<target,2.5,default,out var hit,out var point);
            Check(finished&&hit.Seat==target,"center aim contacts intended seat "+actor+" -> "+target);
        }
        Console.WriteLine("PASS ink aim: "+checks+" checks");
    }
}
