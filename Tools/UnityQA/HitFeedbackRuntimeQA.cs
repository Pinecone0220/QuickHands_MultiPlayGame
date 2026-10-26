using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;

public sealed class HitFeedbackRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager manager;HelloWorldPlayer player;int checks;double deadline;
    readonly List<string> heard=new List<string>();
    T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    DuelDirector Director=>Field<DuelDirector>(manager,"director");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--hit-feedback-qa");if(i<0)return;
        var qa=new GameObject("Hit feedback QA").AddComponent<HitFeedbackRuntimeQA>();qa.role=args[i+1];
        qa.path=args[Array.IndexOf(args,"--hit-feedback-dir")+1];Directory.CreateDirectory(qa.path);
        qa.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=30;
    }
    void Mark(string stage)=>File.WriteAllText(Path.Combine(path,role+"-"+stage),"ok");
    bool Seen(string who,string stage)=>File.Exists(Path.Combine(path,who+"-"+stage));
    bool All(string stage)=>Seen("host",stage)&&Seen("client",stage)&&Seen("observer",stage);
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL "+role+": "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Shot(int actor,int target,DuelWeapon weapon,DuelHitPart part)
    {
        Field<DuelActionState[]>(Director,"actions")[actor].LastHitPart=part;
        Check(Director.Game.Shoot(actor,target,weapon,part,true),"server damage result accepted");Publish();Publish();
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;
        var audio=manager.GetComponent<DuelGameAudio>();audio.Played+=key=>{if(key=="HitConfirm"||key=="HitHead")heard.Add(key);};
        foreach(string name in new[]{"HitConfirm","HitHead"})
        {
            var clip=Resources.Load<AudioClip>("GameFeedback/"+name);Check(clip!=null&&clip.length<.25f,"short bundled "+name);
            var pcm=new float[clip.samples];Check(clip.GetData(pcm,0),"decodes "+name);
            float peak=0;bool finite=true;foreach(float sample in pcm){finite&=!float.IsNaN(sample)&&!float.IsInfinity(sample);peak=Mathf.Max(peak,Mathf.Abs(sample));}
            Check(finite&&peak>.1f&&peak<.7f,"finite audible unclipped "+name);
        }
        var source=Field<AudioSource>(audio,"hits");Check(source.spatialBlend==0&&!source.playOnAwake&&!source.loop,"owner feedback is nonpositional and does not autoplay");
        if(role!="host")while(!Seen("host",role=="client"?"listening":"client-joined"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17967");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==44,"protocol44");
        if(role=="host")Mark("listening");if(role=="client")Mark("joined");
        if(role=="host"){while(!Seen("client","joined"))yield return null;Mark("client-joined");}
        while(player.State.Value.ConnectedMask!=7)yield return null;
        if(role=="host")
        {
            Director.SetAutoSelectItems(true);for(int seat=0;seat<3;seat++)Director.Ready(seat,player.NetworkManager.ServerTime.Time);
            manager.enabled=false;Publish();
        }
        while(player.State.Value.Phase!=DuelPhase.Playing)yield return null;
        manager.GetComponent<DuelFirstPerson>().enabled=false;Check(heard.Count==0,"joining and initial snapshots are silent");
        Mark("ready");while(!All("ready"))yield return null;
        for(int stage=0;stage<7;stage++)
        {
            if(role=="host")
            {
                if(stage==0)Shot(0,-1,DuelWeapon.Revolver,DuelHitPart.None);
                if(stage==1)Shot(0,2,DuelWeapon.Revolver,DuelHitPart.Body);
                if(stage==2)Shot(1,2,DuelWeapon.Revolver,DuelHitPart.Head);
                if(stage==3)Shot(1,0,DuelWeapon.SawedOff,DuelHitPart.Head);
                if(stage==4)Shot(1,0,DuelWeapon.Taser,DuelHitPart.Head);
                if(stage>=5)
                    foreach(var p in Field<Dictionary<ulong,HelloWorldPlayer>>(manager,"players").Values)
                        if(p.Seat.Value==1)p.ReportHitRpc(Director.Game.Round-(stage==6?1:0),Director.ForSeat(1).Life-(stage==5?1:0),999,true);
                Mark("stage"+stage);
            }
            while(!Seen("host","stage"+stage))yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            int expected=role=="host"?(stage>=1?1:0):role=="client"?Math.Min(3,Math.Max(0,stage-1)):0;
            Check(heard.Count==expected,"stage "+stage+" owner-only, miss/duplicate/stale suppression");
            if(role=="host"&&heard.Count>0)Check(heard[0]=="HitConfirm","host body feedback");
            if(role=="client"&&heard.Count>0){Check(heard[0]=="HitHead","remote revolver head feedback");if(heard.Count>1)Check(heard[heard.Count-1]=="HitConfirm","fixed-damage weapons retain normal feedback");}
            Mark("checked"+stage);while(!All("checked"+stage))yield return null;
        }
        Mark("complete");while(!All("complete"))yield return null;
        Finish("PASS hit feedback "+role+": "+checks+" checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL hit feedback timeout "+role,1);}
}
