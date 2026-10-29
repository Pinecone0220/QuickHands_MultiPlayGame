using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HelloWorld
{
    // Local foley from confirmed public state. Never writes input, ammo, or network state.
    public sealed class DuelWeaponAudio : MonoBehaviour
    {
        private sealed class SeatAudio
        {
            public AudioSource Handling,Report,Contact,Props;
            public DuelActionState Previous;
            public int Round=-1,Iteration=-1;
            public bool Initialized,Active;
        }
        private readonly SeatAudio[] seats=new SeatAudio[RouletteRules.MaxPlayers];
        private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        internal event Action<int,string> Played;
        private bool disabled;
        private void Awake()
        {
            disabled=SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null;
            if(!disabled)foreach(var clip in Resources.LoadAll<AudioClip>("WeaponAudio"))clips[clip.name]=clip;
        }
        private AudioSource Source(Transform parent,string label,int priority)
        {
            var source=new GameObject(label).AddComponent<AudioSource>();source.transform.SetParent(parent,false);
            source.playOnAwake=false;source.spatialBlend=.95f;source.spread=35;source.dopplerLevel=0;
            source.minDistance=1.5f;source.maxDistance=18;source.rolloffMode=AudioRolloffMode.Linear;
            source.priority=priority;source.reverbZoneMix=.4f;return source;
        }
        private SeatAudio ForSeat(int seat)
        {
            if(seats[seat]!=null)return seats[seat];
            var root=new GameObject("Weapon audio seat "+(seat+1)).transform;root.SetParent(transform,false);
            return seats[seat]=new SeatAudio{Handling=Source(root,"Mechanical foley",120),Report=Source(root,"Muzzle report",50),Contact=Source(root,"Electrical contact",160),Props=Source(transform,"Glass impacts",100)};
        }
        private AudioClip Clip(string key)
        {
            if(!clips.TryGetValue(key,out var clip))
            {clip=Resources.Load<AudioClip>("WeaponAudio/"+key);clips[key]=clip;if(clip==null)Debug.LogWarning("Missing weapon audio: "+key);}
            return clip;
        }
        private void Play(int seat,SeatAudio a,string key,float gain=.32f,bool report=false)
        {
            var clip=Clip(key);if(clip==null)return;
            var source=report?a.Report:a.Handling;
            // Small deterministic differences preserve the mechanism's character without a chorus.
            int variation=(a.Previous.Version+seat*7)&7;
            source.pitch=report?1:.98f+variation*.005f;
            if(key.StartsWith("Taser")){int skin=DuelSkinEffects.Skin(seat,DuelItem.Taser);int tier=DuelSkinEffects.Tier(skin);if(tier>0)source.pitch=1+.012f*tier+.006f*(skin%3);}
            source.PlayOneShot(clip,gain*DuelSettings.Effects);Played?.Invoke(seat,key);
        }
        private static string Prefix(DuelWeapon w)=>w==DuelWeapon.BAS?"Bas":w==DuelWeapon.SawedOff?"Saw":w==DuelWeapon.Taser?"Taser":"Revolver";
        private static string Mechanism(DuelWeapon w,DuelAction action)
        {
            if(w==DuelWeapon.Taser)return null;
            if(w==DuelWeapon.BAS)return action==DuelAction.OpenBarrel||action==DuelAction.CloseBarrel?"BasBolt":action==DuelAction.Pickup?"BasPickup":null;
            string prefix=Prefix(w);
            switch(action)
            {
                case DuelAction.Pickup:return prefix+"Pickup";
                case DuelAction.CockHammer:return prefix+"Hammer";
                case DuelAction.UnlockBarrel:return prefix+"Unlock";
                case DuelAction.OpenBarrel:return prefix+"Open";
                case DuelAction.CloseBarrel:return w==DuelWeapon.SawedOff?"SawClose":"RevolverRod";
                default:return null;
            }
        }
        private void Complete(int seat,SeatAudio a,DuelActionState previous)
        {
            string key=Mechanism(previous.Weapon,previous.Action);
            if(key!=null)Play(seat,a,key);
            if(previous.Weapon==DuelWeapon.Revolver&&previous.Action==DuelAction.CloseBarrel)
                for(int i=0;i<RouletteRules.Count(previous.SpentMask);i++)Play(seat,a,"RevolverEject",.25f);
            if(previous.Weapon==DuelWeapon.SawedOff&&previous.Action==DuelAction.OpenBarrel&&previous.SpentMask!=0)Play(seat,a,"SawEject",.28f);
        }
        public void Observe(int seat,int round,int iteration,DuelActionState state,double now,bool present,bool active,bool combat,Vector3 position)
        {
            if(disabled||seat<0||seat>=seats.Length)return;
            if(!present){ResetSeat(seat);return;}
            var a=ForSeat(seat);a.Handling.transform.parent.position=position;
            bool baseline=!a.Initialized||a.Round!=round||a.Iteration!=iteration||a.Previous.Life!=state.Life;
            if(baseline)
            {
                a.Handling.Stop();a.Report.Stop();a.Contact.Stop();a.Props.Stop();a.Initialized=true;a.Round=round;a.Iteration=iteration;
                a.Active=active;a.Previous=state;
            }
            else
            {
                var previous=a.Previous;
                if(state.Version<previous.Version)return;
                bool shot=state.ShotSerial>previous.ShotSerial;
                bool newAction=state.Action!=previous.Action||state.StartedAt!=previous.StartedAt;
                bool fresh=now-state.StartedAt<.75&&now-state.StartedAt>=-.1;
                if(state.TaserArmed&&!previous.TaserArmed||state.QuickEquipped&&state.QuickItem==2&&!previous.QuickEquipped)Play(seat,a,"TaserSafety",.25f);
                if(active&&!shot&&state.Weapon==DuelWeapon.Taser&&(!a.Active||state.Weapon!=previous.Weapon)&&fresh)
                    Play(seat,a,"TaserDraw",.28f);
                if(active&&a.Active&&!shot&&state.Weapon==previous.Weapon)
                {
                    if(newAction)
                    {
                        // A wrong-code reset is not a completed mechanical action.
                        bool completed=state.CorrectInputs>previous.CorrectInputs||state.Weapon==DuelWeapon.BAS&&previous.Action==DuelAction.OpenBarrel&&state.Action==DuelAction.Aim&&state.PracticeShotsFired>0&&state.PracticeSession==previous.PracticeSession;
                        if(completed&&fresh)Complete(seat,a,previous);
                        if(state.Action==DuelAction.Aim&&state.Weapon!=DuelWeapon.Taser)
                        {
                            if(state.Weapon==DuelWeapon.Revolver&&previous.Action==DuelAction.Load)Play(seat,a,"RevolverClose",.22f);
                            Play(seat,a,Prefix(state.Weapon)+"Aim",.12f);
                        }
                    }
                    if(fresh)for(int i=0;i<RouletteRules.Count(state.LoadedMask&~previous.LoadedMask);i++)Play(seat,a,Prefix(state.Weapon)+"Load",.30f);
                    if(state.Aiming!=previous.Aiming&&now-state.FiredAt>.35&&state.Weapon!=DuelWeapon.Taser)
                    {
                        if(state.Aiming)Play(seat,a,Prefix(state.Weapon)+"Aim",.12f);
                        if(state.Weapon==DuelWeapon.Revolver&&state.Action!=DuelAction.Aim)
                            Play(seat,a,state.Aiming?"RevolverClose":"RevolverOpen",.22f);
                    }
                }
                if(shot&&state.FiredAt>=0&&now-state.FiredAt<=.5&&now-state.FiredAt>=-.1)
                {
                    int variant=(state.ShotSerial+seat)%3;
                    Play(seat,a,Prefix(state.FiredWeapon)+"Fire"+variant,state.FiredWeapon==DuelWeapon.Taser?.55f:.74f,true);
                }
                a.Previous=state;a.Active=active;
            }
            bool contact=combat&&state.IsStunned(now);
            if(contact)
            {a.Contact.volume=.18f*DuelSettings.Effects;if(!a.Contact.isPlaying){a.Contact.clip=Clip("TaserContactLoop");a.Contact.loop=true;a.Contact.Play();}}
            else if(a.Contact.isPlaying)a.Contact.Stop();
        }
        private void ResetSeat(int seat)
        {var a=seats[seat];if(a==null)return;a.Handling.Stop();a.Report.Stop();a.Contact.Stop();a.Initialized=false;}
        public void Clear(){for(int i=0;i<seats.Length;i++){ResetSeat(i);if(seats[i]!=null)seats[i].Props.Stop();}}
        public void PlayTable(int seat,string key,Vector3 position)
        {if(disabled||seat<0||seat>=seats.Length)return;var a=ForSeat(seat);var clip=Clip(key);if(clip==null)return;a.Props.transform.position=position;a.Props.PlayOneShot(clip,.42f*DuelSettings.Effects);Played?.Invoke(seat,key);}
        private void OnDisable()=>Clear();
    }
}
