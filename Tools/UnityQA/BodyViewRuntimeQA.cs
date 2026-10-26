using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Isolated project: actual seat cameras and rendered first-person body/equipment.
public sealed class BodyViewRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;DuelFirstPerson fps;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    T Member<T>(object o,string name)=>(T)o.GetType().GetField(name).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--body-view-qa");if(i<0)return;
        var q=new GameObject("First-person body QA").AddComponent<BodyViewRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--body-view-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=60;
    }
    void Finish(string message,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string reason){checks++;if(!ok){Finish("FAIL body view "+role+": "+reason,1);throw new Exception(reason);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL body view timeout "+role,1);}
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");
    bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    void View(float pitch,bool ads=false)
    {
        fps.enabled=false;typeof(DuelFirstPerson).GetProperty("Yaw").SetValue(fps,player.Seat.Value*60f);
        typeof(DuelFirstPerson).GetProperty("Pitch").SetValue(fps,pitch);typeof(DuelFirstPerson).GetProperty("Ads").SetValue(fps,ads);
    }
    void Visibility(bool firstPerson,string stage)
    {
        int seat=player.Seat.Value;var puppets=Field<Array>(manager.Table,"puppets");
        for(int i=0;i<6;i++)
        {
            var p=puppets.GetValue(i);var heads=Member<Renderer[]>(p,"HeadRenderers");bool correct=true;
            foreach(var r in Member<Renderer[]>(p,"Renderers"))correct&=r.forceRenderingOff==(firstPerson&&i==seat&&Array.IndexOf(heads,r)>=0);
            Check(correct,stage+" only local head hidden at seat "+i);
        }
        if(firstPerson)
        {
            var eye=DuelGunplay.Eye(seat);Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))<.001f,stage+" camera remains at seated eye");
            Check(!manager.Table.TryGetNameViewport(seat,out _),stage+" own floating name stays hidden");
        }
    }
    void Capture(string name)
    {
        var rt=new RenderTexture(1200,800,24);rt.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1200,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,800),0,0);t.Apply();File.WriteAllBytes(Path.Combine(path,role+"-"+name),t.EncodeToPNG());RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(t);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;fps=manager.GetComponent<DuelFirstPerson>();
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18994");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        if(role=="host")Mark("listening");yield return new WaitForSecondsRealtime(.3f);
        Visibility(true,"lobby");View(-55);yield return new WaitForSecondsRealtime(.15f);Capture("Lobby-Body.png");
        var props=Field<Array>(manager.Table.GetComponent<DuelDrinkVisuals>(),"settings");bool visibleProps=true;
        foreach(var r in Member<Renderer[]>(props.GetValue(player.Seat.Value),"BottleRenderers"))visibleProps&=!r.forceRenderingOff;
        foreach(var r in Member<Renderer[]>(props.GetValue(player.Seat.Value),"GlassRenderers"))visibleProps&=!r.forceRenderingOff;
        Check(visibleProps,"own drink props remain visible with body");fps.enabled=true;Mark("lobby");
        if(role=="host")
        {
            while(!Seen("client","lobby"))yield return null;D.Join(2,Now);D.Game.SetShotTime(0);D.SetAutoSelectItems(true);for(int i=0;i<3;i++)D.Ready(i,Now);
            typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});
            var clock=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,Iteration=1,First=0,ParticipantsMask=7,PhaseAt=Now,StartedAt=Now+30});D.UseItem(0,DuelItem.NoruFace,1,Now);Publish();
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;fps.enabled=true;yield return new WaitForSecondsRealtime(.15f);
        if(role=="client"){player.UseItem(DuelItem.NoruFace);while(!player.State.Value.Seat1.NoruFaceActive)yield return null;}
        View(-50);yield return new WaitForSecondsRealtime(.15f);Visibility(true,"worn jacket");
        var jacket=Field<Transform[][]>(manager.Table,"noruParts")[player.Seat.Value];
        Check(jacket[0].gameObject.activeInHierarchy,"own jacket actually worn");bool bodyVisible=true;
        foreach(var r in jacket[0].GetComponentsInChildren<Renderer>())bodyVisible&=!r.forceRenderingOff;
        Check(bodyVisible,"own jacket visible in first person");Capture("Noru-Body.png");fps.enabled=true;Mark("worn");
        if(role=="host")
        {
            while(!Seen("client","worn"))yield return null;var clock=Field<DuelClockContest>(D,"clock");var c=D.Clock;c.StartedAt=Now+.05;typeof(DuelClockContest).GetProperty("State").SetValue(clock,c);Publish();
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;fps.enabled=true;yield return new WaitForSecondsRealtime(.15f);
        View(0,role=="host");yield return new WaitForSecondsRealtime(.15f);Visibility(true,"shooting");Capture("Aim-Body.png");fps.enabled=true;Mark("aim");
        if(role=="host")
        {
            while(!Seen("client","aim"))yield return null;D.Game.FinishParty(1);D.Tick(Now);Publish();
        }
        while(player.State.Value.Phase!=DuelPhase.Finished)yield return null;yield return new WaitForSecondsRealtime(.15f);
        Visibility(false,"final overview");var eye=DuelGunplay.Eye(player.Seat.Value);
        Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))>1&&Mathf.Abs(Camera.main.fieldOfView-57)<.01f,"final overview still fixed third-person");Mark("finished");
        if(role=="host")while(!Seen("client","finished"))yield return null;
        Finish("PASS body view "+role+": "+checks+" checks");
    }
}
