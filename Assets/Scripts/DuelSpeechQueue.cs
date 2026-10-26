using System;
using System.Collections.Generic;

namespace HelloWorld
{
    // Presentation time only. Chat identity/body are already validated by the server.
    public sealed class DuelSpeechQueue
    {
        public const int Capacity=3;
        public const double HoldDuration=3,FadeDuration=1;
        public sealed class Line
        {
            public string Text { get; internal set; }
            internal double Remaining=FadeDuration;
            internal double HoldUntil;
            public float Opacity=>(float)Math.Clamp(Remaining/FadeDuration,0,1);
        }
        private readonly List<Line> lines=new List<Line>(Capacity);
        private double updatedAt;
        private bool sequential;
        public IReadOnlyList<Line> Lines=>lines;
        public DuelSpeechQueue(double now){updatedAt=now;}
        public void Add(string text,double now)
        {
            Advance(now);
            if(lines.Count==Capacity)lines.RemoveAt(0);
            lines.Add(new Line{Text=text,HoldUntil=now+HoldDuration});
            if(lines.Count==Capacity)sequential=true;
        }
        public void Advance(double now)
        {
            double from=updatedAt;now=Math.Max(updatedAt,now);updatedAt=now;
            if(sequential)
            {
                while(lines.Count>0)
                {
                    var first=lines[0];double start=Math.Max(from,first.HoldUntil);
                    if(now<=start)break;
                    double consumed=Math.Min(first.Remaining,now-start);
                    first.Remaining-=consumed;from=start+consumed;
                    if(first.Remaining<=0)lines.RemoveAt(0);else break;
                }
            }
            else
            {
                for(int i=lines.Count-1;i>=0;i--)
                {lines[i].Remaining-=Math.Max(0,now-Math.Max(from,lines[i].HoldUntil));if(lines[i].Remaining<=0)lines.RemoveAt(i);}
            }
            if(lines.Count==0)sequential=false;
        }
    }
}
