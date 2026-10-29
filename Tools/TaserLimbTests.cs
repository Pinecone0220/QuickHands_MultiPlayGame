using System;
using HelloWorld;
class TaserLimbTests
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Main()
    {
        for(int target=0;target<6;target++)
        foreach(float side in new[]{-1f,1f})
        foreach(var point in new[]{new AimVector(.55f,1.18f,.12f),new AimVector(.52f,.94f,.50f),new AimVector(.37f,.23f,.56f)})
        {
            int actor=(target+1)%6;
            var local=DuelGunplay.Local(target,side*point.X,point.Y,point.Z);
            var origin=local+DuelGunplay.Forward(target)*1.2f;
            var dir=DuelGunplay.Forward(target)*-1;
            var taser=DuelGunplay.TraceHitscan(actor,1<<target,origin,dir);
            Check(taser.Seat==target,"taser contacts limb, seat "+target+" side "+side);
            Check(taser.Part==(point.Y<.5f?DuelHitPart.Legs:DuelHitPart.Body),"limb uses correct fixed-damage contact part");
            var gun=DuelGunplay.TraceFrom(actor,1<<target,origin,dir);
            Check(gun.Seat!=target,"gun geometry remains unchanged");
        }
        for(int target=0;target<6;target++)
        {
            int actor=(target+1)%6;
            var shoulder=DuelGunplay.Local(target,-.60f,1.25f,.45f);
            var vector=shoulder-DuelGunplay.Eye(actor);
            float length=(float)Math.Sqrt(AimVector.Dot(vector,vector));
            var shot=vector*(1/length);
            var actualOrigin=DuelGunplay.MuzzleOrigin(actor,DuelWeapon.Taser,false,shot);
            var armHit=DuelGunplay.TraceHitscan(actor,1<<target,actualOrigin,shot);
            Check(armHit.Seat==target&&armHit.Part==DuelHitPart.Body,"adjacent player can be hit in extended arm");
            Check(DuelGunplay.TraceHitscan(actor,1<<target,actualOrigin,shot).Distance<=DuelGunplay.TraceFrom(actor,1<<target,actualOrigin,shot).Distance+.001f,"taser finds the nearest limb contact");
            var outside=DuelGunplay.Local(target,.82f,1.1f,.35f);
            var aim=DuelGunplay.Forward(target)*-1;
            Check(DuelGunplay.TraceHitscan(actor,1<<target,outside+DuelGunplay.Forward(target)*1.2f,aim).Seat<0,"outside limb envelope misses");
            var behind=DuelGunplay.Local(target,.32f,1.0f,-1.0f);
            Check(DuelGunplay.TraceHitscan(actor,1<<target,behind,DuelGunplay.Forward(target)).Seat<0,"chair continues to block taser");
            var distant=outside+DuelGunplay.Forward(target)*7f;
            Check(DuelGunplay.TraceHitscan(actor,1<<target,distant,aim).Distance<=DuelGunplay.TaserRange,"taser remains range limited");
        }
        Console.WriteLine("PASS taser limbs: "+checks+" checks");
    }
}
