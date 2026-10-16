using System;
namespace HelloWorld
{
    public enum DuelHitPart : byte { None, Head, Body, Legs }
    public struct AimVector
    {
        public float X, Y, Z;
        public AimVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static AimVector operator +(AimVector a, AimVector b) => new AimVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static AimVector operator -(AimVector a, AimVector b) => new AimVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static AimVector operator *(AimVector a, float b) => new AimVector(a.X*b,a.Y*b,a.Z*b);
        public static float Dot(AimVector a, AimVector b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    }
    public struct DuelRayHit { public int Seat; public DuelHitPart Part; public float Distance; public DuelTableProp Prop;public int PropSeat; }
    public static class DuelGunplay
    {
        public const float SeatAdvance=.15f,SeatedDrop=.25f;
        public const float SeatRadius = 4.2f-SeatAdvance, EyeHeight = 1.91f-SeatedDrop, BaseFov = 75f;
        private const double Deg = Math.PI / 180;
        public static AimVector SeatPosition(int seat) => new AimVector(-(float)Math.Sin(seat*60*Deg)*SeatRadius,0,-(float)Math.Cos(seat*60*Deg)*SeatRadius);
        public static AimVector Forward(int seat) => Direction(seat*60,0);
        public static AimVector Right(int seat) => Direction(seat*60+90,0);
        public static AimVector Local(int seat, float x, float y, float forward) => SeatPosition(seat)+Right(seat)*x+new AimVector(0,y,0)+Forward(seat)*forward;
        public static AimVector Eye(int seat) => Local(seat,0,EyeHeight,.17f);
        public static AimVector Point(int seat, DuelHitPart part) => Local(seat,0,part==DuelHitPart.Head?1.87f-SeatedDrop:part==DuelHitPart.Legs?.36f:1.33f-SeatedDrop,.04f);
        public static AimVector Direction(float yaw,float pitch) => new AimVector((float)(Math.Sin(yaw*Deg)*Math.Cos(pitch*Deg)),(float)Math.Sin(pitch*Deg),(float)(Math.Cos(yaw*Deg)*Math.Cos(pitch*Deg)));
        public static void Angles(AimVector direction,out float yaw,out float pitch)
        { yaw=(float)(Math.Atan2(direction.X,direction.Z)/Deg); pitch=(float)(Math.Atan2(direction.Y,Math.Sqrt(direction.X*direction.X+direction.Z*direction.Z))/Deg); }
        public static bool ValidAngles(float yaw,float pitch) => !float.IsNaN(yaw)&&!float.IsInfinity(yaw)&&!float.IsNaN(pitch)&&!float.IsInfinity(pitch)&&Math.Abs(yaw)<=720&&Math.Abs(pitch)<=65;
        public static void Sway(int seat,bool ads,double now,double started,out float yaw,out float pitch)
            => Sway(seat,DuelWeapon.Revolver,ads,now,started,out yaw,out pitch);
        public static void Sway(int seat,DuelWeapon weapon,bool ads,double now,double started,out float yaw,out float pitch)
        {
            // ADS and its duration never reduce sway, including for future weapons.
            float strength=weapon==DuelWeapon.Taser?1.8f*1.75f*.1f:1.8f*(weapon==DuelWeapon.SawedOff?1.75f:3.5f);
            yaw=(float)(Math.Sin(now*7.1+seat*1.7)*strength);
            pitch=(float)(Math.Sin(now*5.3+seat*.9)*strength*.7);
        }
        public const int ShotgunPellets = 37;
        public const float ProjectileSpeed = 40f, ProjectileRange = 20f;
        public static float Spread(DuelWeapon weapon,bool ads) => weapon==DuelWeapon.Taser?0:weapon==DuelWeapon.SawedOff?(ads?3f:6f):(ads?.65f:4.5f);
        private static AimVector Unit(AimVector v) => v*(1/(float)Math.Sqrt(AimVector.Dot(v,v)));
        private static uint Mix(uint x){x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;return x^(x>>16);}
        // The server chooses a new seed per shot; clients reproduce its random circular pattern.
        public static AimVector PelletDirection(AimVector centre,float halfAngle,int pellet,int seed=0)
        {
            uint sample=Mix(unchecked((uint)seed+(uint)(pellet+1)*0x9e3779b9u));
            double angle=sample/(double)uint.MaxValue*Math.PI*2;
            var right=Unit(new AimVector(centre.Z,0,-centre.X));
            var up=new AimVector(centre.Y*right.Z,centre.Z*right.X-centre.X*right.Z,-centre.Y*right.X);
            float radius=(float)(Math.Tan(halfAngle*Deg)*Math.Sqrt(Mix(sample^0xa511e9b3u)/(double)uint.MaxValue));
            return Unit(centre+(right*(float)Math.Cos(angle)+up*(float)Math.Sin(angle))*radius);
        }
        public static float CentreScore(int actor,int target,AimVector centre)=>AimVector.Dot(Unit(Point(target,DuelHitPart.Body)-Eye(actor)),centre);
        public static AimVector MuzzleOrigin(int seat,DuelWeapon weapon,bool ads,AimVector centre)
        {
            bool saw=weapon==DuelWeapon.SawedOff;
            var right=Unit(new AimVector(centre.Z,0,-centre.X));
            var up=new AimVector(centre.Y*right.Z,centre.Z*right.X-centre.X*right.Z,-centre.Y*right.X);
            float sightY=(.075f+(saw?.15f:.10f)*.6f+.0175f)*.78f;bool taser=weapon==DuelWeapon.Taser;
            return Eye(seat)+centre*((ads?(saw?.90f:taser?.5f:.65f):.75f)+(saw?.90f:taser?.35f:.72f)*.78f)+
                up*((ads?-sightY:-.23f)+.075f*.78f)+right*(ads?0:.28f);
        }
        public static DuelRayHit TraceShotgun(int actor,int alive,AimVector centre,float halfAngle,int seed=0,DuelTabletopState? props=null)
        {
            var best=new DuelRayHit{Seat=-1,Part=DuelHitPart.None};float score=float.NegativeInfinity;
            for(int i=0;i<ShotgunPellets;i++)
            {
                var hit=Trace(actor,alive,PelletDirection(centre,halfAngle,i,seed),props);
                if(hit.Seat<0)continue;
                float candidate=CentreScore(actor,hit.Seat,centre);
                if(candidate>score){best=hit;score=candidate;}
            }
            // Shotgun geometry includes the head, but direct damage has no headshot category.
            // Preserve the physical contact region for the kill feed; damage is uniform.
            return best;
        }
        public static float AdsFov(DuelWeapon weapon) => weapon==DuelWeapon.Taser?BaseFov:(float)(2*Math.Atan(Math.Tan(BaseFov*Deg/2)/(weapon==DuelWeapon.SawedOff?1.2:1.4))/Deg);
        private static float Sphere(AimVector origin,AimVector direction,AimVector centre,float radius)
        {
            var oc=origin-centre; float b=AimVector.Dot(oc,direction), c=AimVector.Dot(oc,oc)-radius*radius, disc=b*b-c;
            if(disc<0)return float.PositiveInfinity;
            float t=-b-(float)Math.Sqrt(disc); return t>=0?t:float.PositiveInfinity;
        }
        private static float Box(AimVector origin,AimVector direction,AimVector min,AimVector max)
        {
            float near=0,far=100;
            if(!Slab(origin.X,direction.X,min.X,max.X,ref near,ref far)||!Slab(origin.Y,direction.Y,min.Y,max.Y,ref near,ref far)||!Slab(origin.Z,direction.Z,min.Z,max.Z,ref near,ref far))return float.PositiveInfinity;
            return near;
        }
        private static bool Slab(float p,float d,float min,float max,ref float near,ref float far)
        {
            if(Math.Abs(d)<.00001)return p>=min&&p<=max;
            float a=(min-p)/d,b=(max-p)/d; if(a>b){float swap=a;a=b;b=swap;}
            near=Math.Max(near,a);far=Math.Min(far,b);return far>=near;
        }
        private static AimVector SeatSpace(AimVector p,int seat,bool direction=false)
        {
            if(!direction)p=p-SeatPosition(seat);
            return new AimVector(AimVector.Dot(p,Right(seat)),p.Y,AimVector.Dot(p,Forward(seat)));
        }
        private static float Table(AimVector o,AimVector d)
        {
            float hit=float.PositiveInfinity;
            foreach(float y in new[]{.69f,.89f})
                if(Math.Abs(d.Y)>.00001){float t=(y-o.Y)/d.Y;var p=o+d*t;if(t>=0&&p.X*p.X+p.Z*p.Z<=3.75f*3.75f)hit=Math.Min(hit,t);}
            float a=d.X*d.X+d.Z*d.Z,b=2*(o.X*d.X+o.Z*d.Z),c=o.X*o.X+o.Z*o.Z-3.75f*3.75f,disc=b*b-4*a*c;
            if(a>.00001&&disc>=0)
                foreach(float t in new[]{(-b-(float)Math.Sqrt(disc))/(2*a),(-b+(float)Math.Sqrt(disc))/(2*a)})
                    if(t>=0){float y=o.Y+d.Y*t;if(y>=.69f&&y<=.89f)hit=Math.Min(hit,t);}
            return hit;
        }
        public static DuelRayHit Trace(int actor,int alive,AimVector direction,DuelTabletopState? props=null)
            => TraceFrom(actor,alive,Eye(actor),direction,props);
        public static DuelRayHit TraceFrom(int actor,int alive,AimVector origin,AimVector direction,DuelTabletopState? props=null)
        {
            var result=new DuelRayHit{Seat=-1,Part=DuelHitPart.None,Distance=100};
            float obstacle=Table(origin,direction);
            // The floor and walls stop rays; chairs are real cover behind seated players.
            if(direction.Y<-.00001f)obstacle=Math.Min(obstacle,-origin.Y/direction.Y);
            foreach(float edge in new[]{-10f,10f})
            {
                if(Math.Abs(direction.X)>.00001f){float t=(edge-origin.X)/direction.X;var p=origin+direction*t;if(t>=0&&Math.Abs(p.Z)<=10&&p.Y>=0&&p.Y<=7)obstacle=Math.Min(obstacle,t);}
                if(Math.Abs(direction.Z)>.00001f){float t=(edge-origin.Z)/direction.Z;var p=origin+direction*t;if(t>=0&&Math.Abs(p.X)<=10&&p.Y>=0&&p.Y<=7)obstacle=Math.Min(obstacle,t);}
            }
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
            {
                var o=SeatSpace(origin,seat);var d=SeatSpace(direction,seat,true);
                if(props.HasValue)
                {
                    for(int kindIndex=1;kindIndex<=2;kindIndex++)
                    {
                        var kind=(DuelTableProp)kindIndex;
                        if(props.Value.Broken(seat,kind))continue;
                        bool bottle=kind==DuelTableProp.Bottle;float x=bottle?.66f:-.18f,z=(bottle?.82f:.86f)-SeatAdvance,r=bottle?.105f:.085f;
                        float t=Box(o,d,new AimVector(x-r,.90f,z-r),new AimVector(x+r,bottle?1.21f:1.09f,z+r));
                        if(bottle)t=Math.Min(t,Box(o,d,new AimVector(x-.04f,1.21f,z-.04f),new AimVector(x+.04f,1.33f,z+.04f)));
                        if(t<result.Distance){result=new DuelRayHit{Seat=-1,Part=DuelHitPart.None,Prop=kind,PropSeat=seat,Distance=t};}
                    }
                }
                if(seat!=actor)
                {
                    obstacle=Math.Min(obstacle,Box(o,d,new AimVector(-.38f,.69f-SeatedDrop,-.40f),new AimVector(.38f,1.65f-SeatedDrop,-.26f)));
                    obstacle=Math.Min(obstacle,Box(o,d,new AimVector(-.40f,.60f-SeatedDrop,-.35f),new AimVector(.40f,.72f-SeatedDrop,.35f)));
                }
                if(seat==actor||!RouletteRules.Has(alive,seat))continue;
                Offer(ref result,seat,DuelHitPart.Head,Sphere(origin,direction,Point(seat,DuelHitPart.Head),.205f));
                Offer(ref result,seat,DuelHitPart.Body,Box(o,d,new AimVector(-.30f,.78f-SeatedDrop,-.20f),new AimVector(.30f,1.66f-SeatedDrop,.23f)));
                foreach(float x in new[]{-.22f,.22f})
                    Offer(ref result,seat,DuelHitPart.Legs,Box(o,d,new AimVector(x-.12f,.10f,-.10f),new AimVector(x+.12f,.78f-SeatedDrop,.48f)));
            }
            if(obstacle<result.Distance-.001f)return new DuelRayHit{Seat=-1,Part=DuelHitPart.None,Distance=obstacle};
            return result;
        }
        private static void Offer(ref DuelRayHit hit,int seat,DuelHitPart part,float t)
        { if(t<hit.Distance){hit.Seat=seat;hit.Part=part;hit.Distance=t;hit.Prop=DuelTableProp.None;hit.PropSeat=-1;} }
    }
    // Swept finite-speed server projectiles avoid tunnelling between network ticks.
    // A shotgun volley gathers pellet contacts, then chooses one central direct victim.
    public sealed class DuelProjectileVolley
    {
        public readonly int Actor, Life, Iteration;
        public readonly DuelWeapon Weapon;
        public readonly double StartedAt;
        private readonly AimVector origin, centre;
        private readonly AimVector[] rays;
        private readonly bool[] stopped;
        private readonly DuelRayHit[] contacts;
        public DuelProjectileVolley(int actor,int life,int iteration,DuelWeapon weapon,AimVector origin,AimVector centre,float spread,int seed,double now)
        {
            Actor=actor;Life=life;Iteration=iteration;Weapon=weapon;StartedAt=now;this.origin=origin;this.centre=centre;
            int count=weapon==DuelWeapon.SawedOff?DuelGunplay.ShotgunPellets:1;
            rays=new AimVector[count];stopped=new bool[count];contacts=new DuelRayHit[count];
            for(int i=0;i<count;i++){rays[i]=count==1?centre:DuelGunplay.PelletDirection(centre,spread,i,seed);contacts[i].Seat=-1;}
        }
        public bool Tick(int alive,double now,out DuelRayHit result,DuelTabletop tabletop=null)
        {
            float travel=(float)System.Math.Max(0,now-StartedAt)*DuelGunplay.ProjectileSpeed;
            bool allStopped=true;
            for(int i=0;i<rays.Length;i++)
            {
                if(stopped[i])continue;
                var hit=DuelGunplay.TraceFrom(Actor,alive,origin,rays[i],tabletop==null?(DuelTabletopState?)null:tabletop.State);
                if(hit.Distance<=System.Math.Min(travel,DuelGunplay.ProjectileRange))
                {
                    if(hit.Prop!=DuelTableProp.None&&tabletop!=null)tabletop.Break(hit.PropSeat,hit.Prop,now);
                    contacts[i]=hit;stopped[i]=true;
                }
                else if(travel>=DuelGunplay.ProjectileRange)stopped[i]=true;
                allStopped&=stopped[i];
            }
            result=new DuelRayHit{Seat=-1,Part=DuelHitPart.None};
            // Every seated player is within 11.2m of a muzzle. Wait for all possible body contacts,
            // rather than favouring the first/nearest pellet over the screen-centred opponent.
            if(!allStopped && travel<(Weapon==DuelWeapon.SawedOff?11.2f:DuelGunplay.ProjectileRange))return false;
            float best=float.NegativeInfinity;
            foreach(var hit in contacts)
            {
                if(!RouletteRules.Has(alive,hit.Seat))continue;
                float score=DuelGunplay.CentreScore(Actor,hit.Seat,centre);
                if(score>best){best=score;result=hit;}
            }
            // Sawed-off and taser damage is uniform, while impact regions remain available for logs.
            return true;
        }
    }
}
