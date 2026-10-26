using System;
namespace HelloWorld
{
    public struct DuelInkState:IEquatable<DuelInkState>
    {
        public bool Owned,Armed;
        public int Version,Serial,Target;
        public double ThrownAt,ImpactAt,InkedAt;
        public AimVector Origin,Direction,Impact;
        public static DuelInkState Empty=>new DuelInkState{ThrownAt=-1,ImpactAt=-1,InkedAt=-1,Target=-1};
        public bool Equals(DuelInkState o)=>Owned==o.Owned&&Armed==o.Armed&&Version==o.Version&&Serial==o.Serial&&Target==o.Target&&ThrownAt==o.ThrownAt&&ImpactAt==o.ImpactAt&&InkedAt==o.InkedAt&&Origin.Equals(o.Origin)&&Direction.Equals(o.Direction)&&Impact.Equals(o.Impact);
    }
    public static class DuelInk
    {
        public const double HoldDuration=5,FadeDuration=.5,ReleaseDelay=.16,ThrowDuration=.65,Lifetime=2;
        public const float Speed=14,UpwardSpeed=1.6f,Gravity=8;
        public static bool Shooting(ClockPhase phase)=>phase==ClockPhase.QuickHands||phase==ClockPhase.Shooting;
        public static bool Usable(ClockPhase phase)=>phase==ClockPhase.ItemPreparation||Shooting(phase);
        public static bool Busy(DuelInkState state,double now)=>state.Armed||state.ThrownAt>=0&&now-state.ThrownAt<ThrowDuration;
        public static float Opacity(double age)=>age<0||age>=HoldDuration+FadeDuration?0:age<=HoldDuration?1:(float)(1-(age-HoldDuration)/FadeDuration);
        public static double EndedAt(DuelPhase phase,ClockPhase clockPhase,double phaseAt,double stageAt)
        {
            bool ended=clockPhase==ClockPhase.Returning||clockPhase==ClockPhase.QuickResults||clockPhase==ClockPhase.Dice;
            return phase==DuelPhase.Finished?ended?Math.Min(stageAt,phaseAt):stageAt:phase==DuelPhase.Playing&&ended?phaseAt:-1;
        }
        public static AimVector ThrowOrigin(int seat,float yaw,float pitch)=>DuelGunplay.Eye(seat)+DuelGunplay.Direction(yaw,pitch)*.65f+DuelGunplay.Direction(yaw+90,0)*.24f+new AimVector(0,-.10f,0);
        public static AimVector Position(DuelInkState state,double now)
        {
            float t=(float)Math.Max(0,now-state.ThrownAt-ReleaseDelay);
            return state.Origin+state.Direction*(Speed*t)+new AimVector(0,UpwardSpeed*t-.5f*Gravity*t*t,0);
        }
    }
    // Remember the first exit so Dice -> Results or a later final result cannot revive an old stain.
    public struct DuelInkFade
    {
        private double stamp,endedAt;
        private bool seen,ending;
        public float Observe(DuelInkState ink,double now,DuelPhase phase,ClockPhase clockPhase,double phaseAt,double stageAt)
        {
            if(ink.InkedAt<0){this=default;return 0;}
            if(!seen||stamp!=ink.InkedAt){stamp=ink.InkedAt;seen=true;ending=false;}
            if(phase!=DuelPhase.Playing||!DuelInk.Shooting(clockPhase))
            {
                double end=DuelInk.EndedAt(phase,clockPhase,phaseAt,stageAt);
                if(end<stamp)end=stamp-1;
                if(!ending||end<endedAt){endedAt=end;ending=true;}
            }
            float natural=DuelInk.Opacity(now-stamp);
            return ending?Math.Min(natural,DuelInk.Opacity(endedAt-stamp)*(float)Math.Max(0,Math.Min(1,1-(now-endedAt)/DuelInk.FadeDuration))):natural;
        }
    }
    // The same analytic arc is rendered locally; only the server traces its swept segments.
    public sealed class DuelInkFlight
    {
        public readonly int Actor,Life,Iteration;
        public readonly DuelInkState State;
        private double sampledAt;
        public DuelInkFlight(int actor,int life,int iteration,DuelInkState state)
        {Actor=actor;Life=life;Iteration=iteration;State=state;sampledAt=state.ThrownAt+DuelInk.ReleaseDelay;}
        public bool Tick(int alive,double now,DuelTabletopState props,out DuelRayHit hit,out AimVector point)
        {
            hit=new DuelRayHit{Seat=-1};point=DuelInk.Position(State,now);
            double end=Math.Min(now,State.ThrownAt+DuelInk.ReleaseDelay+DuelInk.Lifetime);
            while(sampledAt<end)
            {
                double next=Math.Min(end,sampledAt+1.0/60);var from=DuelInk.Position(State,sampledAt);var to=DuelInk.Position(State,next);var delta=to-from;float length=(float)Math.Sqrt(AimVector.Dot(delta,delta));
                if(length>.00001f)
                {
                    var direction=delta*(1/length);var contact=DuelGunplay.TraceFrom(Actor,alive,from,direction,props);
                    if(contact.Distance<=length){hit=contact;point=from+direction*contact.Distance;return true;}
                }
                sampledAt=next;
            }
            return now>=State.ThrownAt+DuelInk.ReleaseDelay+DuelInk.Lifetime;
        }
    }
}
