using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HelloWorld;
using UnityEngine;
public sealed class WeaponAudioRuntimeQA:MonoBehaviour
{
    readonly List<string> cues=new List<string>();DuelWeaponAudio audio;int checks;double now;double deadline;
    sealed class QuickRandom:System.Random{public override int Next(int min,int max)=>min==0&&max==PartyGames.Count?1:min;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--weapon-audio-qa")<0)return;new GameObject("WeaponAudioQA").AddComponent<WeaponAudioRuntimeQA>();}
    void Check(bool ok,string text){checks++;if(!ok){Debug.LogError("FAIL weapon audio: "+text);Exit(1);throw new Exception(text);}}
    static void Exit(int result)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(result);
#else
        Application.Quit(result);
#endif
    }
    DuelDirector Director(DuelWeapon weapon)
    {
        var d=new DuelDirector(DuelMode.PvP,new QuickRandom());for(int i=0;i<6;i++)d.Join(i,-16);
        d.Game.SetShotTime(0);d.SelectWeapon(0,weapon,-16);for(int i=0;i<6;i++)d.Ready(i,-16);
        d.Tick(-13.5);d.Tick(-7.69);d.Tick(-2.68);d.Tick(.33);Check(d.QuickHandsActive,"test reached QuickHands");return d;
    }
    void Observe(DuelDirector d,bool active=true)
    {audio.Observe(0,d.Game.Round,d.Clock.Iteration,d.ForSeat(0),now,true,active,true,new Vector3(0,1.4f,3));}
    void Digit(DuelDirector d)
    {var a=d.ForSeat(0);int cueCount=cues.Count;now+=.025;Check(d.Input(0,a.Digit(a.Progress),a.Version,now),"real server accepts numeric action");Observe(d);if(a.Progress+1<a.Length)Check(cues.Count==cueCount,"unfinished input remains silent");}
    void Load(DuelDirector d){int guard=60;while(d.ForSeat(0).Action!=DuelAction.Aim&&guard-->0)Digit(d);Check(guard>0,"load completes");}
    void Miss(DuelDirector d)
    {now+=.7;Check(d.Fire(0,0,65,false,d.ForSeat(0).Version,now,now),"real server confirms shot");Observe(d);d.Tick(now+.6);Observe(d);}
    int Count(string key)=>cues.FindAll(s=>s==key).Count;
    int Reports(string prefix)=>cues.FindAll(s=>s.StartsWith(prefix+"Fire",StringComparison.Ordinal)).Count;
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+90;yield return new WaitForSecondsRealtime(1);
        var table=HelloWorldManager.Instance.Table;table.enabled=false;audio=HelloWorldManager.Instance.GetComponent<DuelWeaponAudio>();audio.Clear();audio.Played+=(seat,key)=>cues.Add(key);
        var assets=Resources.LoadAll<AudioClip>("WeaponAudio");Check(assets.Length==29,"all recorded clips bundled");
        foreach(var clip in assets)
        {
            Check(clip.channels>=1&&clip.frequency>=22050&&clip.length>.01f&&clip.length<10f,"short mono resident clip: "+clip.name);
            var data=new float[clip.samples*clip.channels];Check(clip.GetData(data,0),"clip decoded: "+clip.name);float peak=0;double energy=0;
            bool finite=true;foreach(float x in data){finite&=!float.IsNaN(x)&&!float.IsInfinity(x);peak=Mathf.Max(peak,Mathf.Abs(x));energy+=x*x;}Check(finite,"finite PCM: "+clip.name);
            Check(peak>.001f&&peak<=1.001f&&energy/data.Length>.0000001,"non-silent headroom: "+clip.name);
        }
        now=.4;var rev=Director(DuelWeapon.Revolver);Observe(rev,false);Observe(rev);
        Check(Count("RevolverPickup")==0,"no pickup sound before action completion");
        Digit(rev);Digit(rev);Digit(rev);int beforeWrong=cues.Count;var partial=rev.ForSeat(0);now+=.025;
        rev.Input(0,partial.Digit(partial.Progress)%5+1,partial.Version,now);Observe(rev);
        Check(cues.Count==beforeWrong,"wrong input restart has no completion sound");now+=.6;
        Load(rev);Check(Count("RevolverPickup")==1,"pickup sounds once on completion");
        foreach(string key in new[]{"RevolverHammer","RevolverUnlock","RevolverOpen","RevolverRod"})Check(Count(key)==1,"distinct initial action: "+key);
        Check(Count("RevolverLoad")==6,"one insertion cue per completed cartridge");
        Check(Count("RevolverClose")==1&&Count("RevolverAim")==1,"full cylinder closes gate and raises gun audibly");
        Miss(rev);Miss(rev);Check(Reports("Revolver")==2&&Reports("Saw")==0,"revolver report and no duplicate snapshot reports");
        int before=Count("RevolverEject");for(int i=0;i<6;i++)Digit(rev);
        Check(Count("RevolverEject")-before==2,"only two spent cases make ejection sounds");
        Load(rev);Check(Count("RevolverLoad")==8,"top-up sounds only for two newly inserted rounds");
        int unchanged=cues.Count;Observe(rev);Observe(rev);Check(cues.Count==unchanged,"same snapshot is silent");
        var stale=rev.ForSeat(0);stale.Version--;stale.Progress=3;
        audio.Observe(0,rev.Game.Round,rev.Clock.Iteration,stale,now,true,true,true,Vector3.zero);Check(cues.Count==unchanged,"old version cannot replay foley");
        audio.Clear();cues.Clear();now=.4;var saw=Director(DuelWeapon.SawedOff);Observe(saw,false);Observe(saw);Load(saw);
        Check(Count("SawPickup")==1&&Count("SawOpen")==1&&Count("SawLoad")==1&&Count("SawClose")==1,"sawed-off initial handling sequence");
        Miss(saw);Check(Reports("Saw")==1&&Reports("Revolver")==0,"shotgun gets separate recorded report");
        Load(saw);Check(Count("SawPickup")==1&&Count("SawOpen")==2&&Count("SawEject")==1&&Count("SawLoad")==2&&Count("SawClose")==2,"repeated reload starts at opening and automatically ejects once");
        audio.Clear();cues.Clear();var state=new DuelActionState{Weapon=DuelWeapon.Revolver,FiredAt=-1,Life=1,Version=1};
        audio.Observe(2,9,1,state,10,true,false,false,Vector3.zero);state.TaserArmed=true;state.Version++;
        audio.Observe(2,9,1,state,10.1,true,false,false,Vector3.zero);Check(Count("TaserSafety")==1,"taser arming switch");
        state.Weapon=DuelWeapon.Taser;state.Action=DuelAction.Aim;state.StartedAt=10.2;state.Version++;
        audio.Observe(2,9,1,state,10.2,true,true,true,Vector3.zero);Check(Count("TaserDraw")==1,"taser drawn at shooting turn");
        state.ShotsFired=1;state.FiredAt=10.3;state.FiredWeapon=DuelWeapon.Taser;state.Weapon=DuelWeapon.Revolver;state.Version++;
        audio.Observe(2,9,1,state,10.3,true,true,true,Vector3.zero);Check(Reports("Taser")==1&&Reports("Revolver")==0,"actual fired taser retained after weapon restoration");
        audio.Clear();cues.Clear();audio.Observe(2,9,1,state,10.31,true,true,true,Vector3.zero);Check(cues.Count==0,"late initial snapshot never replays past shot");
        state.ShotsFired++;state.Version++;state.FiredAt=8;
        audio.Observe(2,9,1,state,10.4,true,true,true,Vector3.zero);Check(cues.Count==0,"stale gunshot skipped");
        state.Stunned=true;audio.Observe(2,9,1,state,10.5,true,false,true,new Vector3(4,1.4f,0));yield return null;
        var sources=audio.GetComponentsInChildren<AudioSource>();bool contact=false;
        foreach(var source in sources)
        {
            if(source.name!="Mechanical foley"&&source.name!="Muzzle report"&&source.name!="Electrical contact")continue;
            Check(source.spatialBlend>.9f&&source.dopplerLevel==0&&source.rolloffMode==AudioRolloffMode.Linear,"seat-based 3D playback without motion pitch artifacts");
            if(source.name=="Electrical contact"&&source.clip!=null){contact|=source.loop&&source.isPlaying;Check(source.transform.position==new Vector3(4,1.4f,0),"remote electrical contact at victim seat");}
        }
        Check(contact,"electrical contact starts while stunned");state.Stunned=false;audio.Observe(2,9,1,state,10.6,true,false,false,Vector3.zero);
        foreach(var source in sources)if(source.name=="Electrical contact")Check(!source.isPlaying,"contact stops at effect end");
        audio.PlayTable(5,"BottleBreak",new Vector3(0,1,4));audio.Observe(5,9,1,default,11,false,false,false,Vector3.zero);yield return null;
        sources=audio.GetComponentsInChildren<AudioSource>();bool emptyImpact=false;
        foreach(var source in sources)if(source.name=="Glass impacts"&&source.isPlaying)emptyImpact=true;
        Check(emptyImpact,"empty-seat prop break survives missing-player cleanup");
        audio.Clear();foreach(var source in sources)Check(!source.isPlaying,"disconnect cleanup stops all audio");
        Debug.Log("PASS weapon audio: "+checks+" PCM, server-action cadence, shot variants, actual taser, remote seat, stale-state and cleanup checks");Exit(0);
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL weapon audio timeout");Exit(1);}}
}
