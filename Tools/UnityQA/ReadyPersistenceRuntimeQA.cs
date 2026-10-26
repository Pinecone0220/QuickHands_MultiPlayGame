using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;

// Copied into the isolated QA project only.
public sealed class ReadyPersistenceRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;
    DuelDirector D=>(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(manager);
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--ready-qa");if(i<0)return;
        var q=new GameObject("Ready persistence QA").AddComponent<ReadyPersistenceRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--ready-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+110;Application.targetFrameRate=30;
    }
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Finish(string s,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),s);Debug.Log(s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL ready "+role+": "+s,1);throw new Exception(s);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ready timeout "+role,1);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;
        if(role!="host")while(!Seen("host",role=="joiner"?"ready":"listening"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","18991");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=3)yield return null;D.Join(5,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);Publish();Mark("setup");
            while(!Seen("client","ready"))yield return null;int hostSession=D.ForSeat(0).PracticeSession,remoteSession=D.ForSeat(1).PracticeSession;Mark("ready");
            while(D.Game.ConnectedMask!=39)yield return null;Check(D.Game.ReadyMask==3&&D.PracticeMask==3,"late join preserves ready/practice mask");
            Check(D.ForSeat(0).PracticeSession==hostSession&&D.ForSeat(1).PracticeSession==remoteSession,"late join preserves practice sessions");
            manager.ToggleDeveloperMode();manager.SetRoomShotTime(17);manager.SetRoomUnlimitedItems(true);manager.SetRoomManualMiniGame(true);manager.SetRoomAutoSelectItems(false);manager.SetRoomAutoSelectItems(true);
            Check(D.Game.ShotTimeLimit==17&&D.Game.UnlimitedItems&&D.Game.ManualMiniGame&&D.Game.AutoSelectItems&&D.Game.ReadyMask==3,"host setting APIs preserve ready");
            manager.ToggleDeveloperMode();Check(D.Game.ShotTimeLimit==10&&!D.Game.UnlimitedItems&&!D.Game.ManualMiniGame&&!D.Game.AutoSelectItems&&D.Game.ReadyMask==3,"F11 defaults preserve ready");Mark("settings");
            while(!Seen("client","preserved")||!Seen("joiner","observed"))yield return null;Mark("leave");while(D.Game.ConnectedMask!=35)yield return null;Check(D.Game.ReadyMask==3,"departure preserves existing ready");Mark("departed");
            while(!Seen("client","cancelled"))yield return null;Check(D.Game.ReadyMask==1,"remote cancel affects owner only");Mark("cancel-verified");while(!Seen("client","reready"))yield return null;
            Check(D.Game.ReadyMask==3&&D.Game.Phase!=DuelPhase.Playing,"unready remaining participant still blocks start");D.Disconnect(5,Now);Publish();Check(D.Game.Phase==DuelPhase.Playing&&D.Stage==DuelStage.Countdown,"last unready departure starts remaining ready players");Mark("started");while(!Seen("client","finished"))yield return null;
        }
        else if(role=="client")
        {
            while(!Seen("host","setup")||player.State.Value.Seat1.SelectedItems!=DuelLoadout.All)yield return null;player.Rematch();while(player.State.Value.ReadyMask!=3)yield return null;int session=player.State.Value.Seat1.PracticeSession;Mark("ready");
            while(!Seen("host","settings")||player.State.Value.ConnectedMask!=39||player.State.Value.UnlimitedItems||player.State.Value.ManualMiniGame||player.State.Value.AutoSelectItems)yield return null;
            Check(player.State.Value.ReadyMask==3&&player.State.Value.IsPracticing(1),"remote snapshot preserves ready and practice after join/settings");Check(player.State.Value.Seat1.PracticeSession==session&&manager.GetComponent<DuelFirstPerson>().Active,"remote first-person practice continues");Mark("preserved");
            while(!Seen("host","departed")||player.State.Value.ConnectedMask!=35)yield return null;Check(player.State.Value.ReadyMask==3,"remote readiness survives departure");player.Rematch();while(player.State.Value.ReadyMask!=1)yield return null;Mark("cancelled");while(!Seen("host","cancel-verified"))yield return null;player.Rematch();while(player.State.Value.ReadyMask!=3)yield return null;Mark("reready");
            while(!Seen("host","started")||player.State.Value.Stage!=DuelStage.Countdown)yield return null;Check(player.State.Value.Phase==DuelPhase.Playing,"remote receives normal all-ready start");Mark("finished");
        }
        else
        {
            while(player.State.Value.ConnectedMask!=39)yield return null;Check(player.Seat.Value==2&&player.State.Value.ReadyMask==3&&!player.State.Value.IsPracticing(2),"new player starts unready while seeing existing ready state");Mark("observed");while(!Seen("host","leave"))yield return null;manager.Leave();yield return new WaitForSecondsRealtime(.1f);
        }
        Finish("PASS ready "+role+": "+checks+" checks");
    }
}
