using System;
using UnityEngine;

namespace HelloWorld
{
    // Cosmetic one-shots from confirmed timestamps. No collision, input or damage authority.
    [DefaultExecutionOrder(110)]
    public sealed class DuelInkAudio:MonoBehaviour
    {
        private readonly AudioSource[] winds=new AudioSource[6],splashes=new AudioSource[6];
        private readonly bool[] initialized=new bool[6];
        private readonly int[] lives=new int[6],serials=new int[6];
        private readonly double[] thrown=new double[6],impacted=new double[6],release=new double[6],contact=new double[6];
        private HelloWorldPlayer session;private int round=-1;private Transform root;
        private AudioClip wind,splash;
        internal event Action<int,string> Played;
        private void Start()
        {
            wind=Resources.Load<AudioClip>("GameFeedback/InkThrow");splash=Resources.Load<AudioClip>("GameFeedback/InkImpact");
            root=new GameObject("Ink pouch sounds").transform;root.SetParent(transform,false);
            for(int i=0;i<6;i++){winds[i]=Source("Throw "+i,.26f);splashes[i]=Source("Splash "+i,.55f);}
            Reset();
        }
        private AudioSource Source(string name,float gain)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=false;source.volume=gain;source.priority=100;
            source.spatialBlend=.85f;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=2;source.maxDistance=20;source.dopplerLevel=0;
            return source;
        }
        private void ResetSeat(int seat)
        {initialized[seat]=false;release[seat]=contact[seat]=-1;if(winds[seat]!=null)winds[seat].Stop();if(splashes[seat]!=null)splashes[seat].Stop();}
        private void Reset(){for(int i=0;i<6;i++)ResetSeat(i);}
        private void Play(int seat,AudioSource source,AudioClip clip,AimVector position,bool local,string name)
        {
            if(clip==null)return;source.transform.position=new Vector3(position.X,position.Y,position.Z);
            int skin=DuelSkinEffects.Skin(seat,DuelItem.Ink);int tier=DuelSkinEffects.Tier(skin);source.pitch=tier>0?1+.015f*tier+.008f*(skin%3):1;source.spatialBlend=local?0:.85f;source.PlayOneShot(clip,DuelSettings.Effects);Played?.Invoke(seat,name);
        }
        internal void Observe(DuelSnapshot state,int localSeat,double now)
        {
            for(int seat=0;seat<6;seat++)
            {
                if(!state.Connected(seat)){ResetSeat(seat);continue;}
                var a=state.ForSeat(seat);var ink=a.Ink;
                if(!initialized[seat]||lives[seat]!=a.Life)
                {
                    ResetSeat(seat);initialized[seat]=true;lives[seat]=a.Life;serials[seat]=ink.Serial;
                    thrown[seat]=ink.ThrownAt;impacted[seat]=ink.ImpactAt;continue;
                }
                bool shooting=state.IsPracticing(seat)||state.Phase==DuelPhase.Playing&&DuelInk.Shooting(state.Clock.Phase);
                if(serials[seat]!=ink.Serial||thrown[seat]!=ink.ThrownAt)
                {
                    serials[seat]=ink.Serial;thrown[seat]=ink.ThrownAt;release[seat]=-1;
                    if(shooting&&ink.Serial>0&&ink.ThrownAt>=0&&now-ink.ThrownAt<.5)release[seat]=ink.ThrownAt+DuelInk.ReleaseDelay;
                }
                if(!shooting)release[seat]=-1;
                if(release[seat]>=0&&now>=release[seat])
                {
                    if(now-release[seat]<.35)Play(seat,winds[seat],wind,ink.Origin,seat==localSeat,"InkThrow");
                    release[seat]=-1;
                }
                if(impacted[seat]!=ink.ImpactAt)
                {
                    impacted[seat]=ink.ImpactAt;contact[seat]=-1;
                    if(ink.Serial>0&&ink.ThrownAt>=0&&ink.ImpactAt>=ink.ThrownAt&&now-ink.ImpactAt<.45)contact[seat]=ink.ImpactAt;
                }
                if(contact[seat]>=0&&now>=contact[seat])
                {
                    if(now-contact[seat]<.45)Play(seat,splashes[seat],splash,ink.Impact,ink.Target==localSeat,"InkImpact");
                    contact[seat]=-1;
                }
            }
        }
        private void LateUpdate()
        {
            if(root==null)return;var player=HelloWorldPlayer.Local;
            if(player==null||!player.IsSpawned||player.Seat.Value<0){Reset();session=null;round=-1;return;}
            var state=player.State.Value;if(session!=player||round!=state.Round){Reset();session=player;round=state.Round;}
            Observe(state,player.Seat.Value,player.NetworkManager.ServerTime.Time);
        }
        private void OnDisable()=>Reset();
        private void OnDestroy(){if(root!=null)Destroy(root.gameObject);}
    }
}
