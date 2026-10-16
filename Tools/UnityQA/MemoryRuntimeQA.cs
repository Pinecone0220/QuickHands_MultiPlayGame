using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Copy into the isolated QA project's Assets; never installed in the actual game.
public sealed class MemoryRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    int seat,checks,seed;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--memory-qa");if(i<0)return;
        var q=new GameObject("MemoryQA").AddComponent<MemoryRuntimeQA>();q.role=args[i+1];
        q.path=Path.Combine(Application.dataPath,"..","memory-");q.deadline=Time.realtimeSinceStartupAsDouble+100;
        Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
    }
    T V<T>(string name)=>(T)typeof(RouletteView).GetField(name,F).GetValue(view);
    ArrowMemoryState Memory=>player.State.Value.Clock.Memory;
    double Now=>player.NetworkManager.ServerTime.Time;
    void Mark(string label)=>File.WriteAllText(path+role+"-"+label,"ok");
    bool Seen(string actor,string label)=>File.Exists(path+actor+"-"+label);
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL memory "+role+": "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0){Debug.Log(message);File.WriteAllText(path+role+"-done",message);deadline=double.MaxValue;Application.Quit(code);}
    IEnumerator Press(int direction,int expected)
    {
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        var keys=new[]{Key.UpArrow,Key.DownArrow,Key.LeftArrow,Key.RightArrow};
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys[direction]));yield return null;yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;
        while(V<int>("memoryConfirmed")<expected)yield return null;
        Check(V<int>("memoryCount")==expected&&V<int>("memoryConfirmed")==expected,"keyboard input receives owner-only server acknowledgement "+expected);
    }
    void CheckPrivate()
    {
        Check(Memory.AnswerSeed==0,"answer removed from input snapshot");
        for(int i=0;i<2;i++){var e=Memory.ForSeat(i);Check(e.Count==0&&e.Errors==-1&&!e.Locked,"partial input/errors/lock remain private for seat "+i);}
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);manager=HelloWorldManager.Instance;
        if(role!="host")while(!Seen("host","configured"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17949");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        player=HelloWorldPlayer.Local;seat=player.Seat.Value;view=manager.GetComponent<RouletteView>();
        if(role=="host"){manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(5);Mark("configured");}
        while(player.State.Value.ConnectedMask!=3||!player.State.Value.ManualMiniGame)yield return null;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==38,"matching memory protocol");player.Rematch();
        if(role=="host"){while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;manager.ChooseMiniGame(PartyMiniGame.Memory);}
        while(player.State.Value.Clock.Phase!=ClockPhase.Memory||Memory.Phase!=ArrowMemoryPhase.Showing)yield return null;
        manager.GetComponent<DuelFirstPerson>().enabled=false;yield return null;yield return null;
        seed=Memory.AnswerSeed;int memoryRound=Memory.Round;
        Check(Memory.Length==3&&Memory.Round==1&&Memory.ActiveMask==3&&seed!=0,"first round shows three shared arrows");
        var labels=V<List<Label>>("memoryArrowLabels");
        Check(labels.Count>=3&&labels[0].text!="•","showing panel reveals arrows");
        Check(V<ScrollView>("memoryScroll").style.display.value==DisplayStyle.Flex,"arrow panel visible during memorization");
        player.InputMemory(0,memoryRound,0);yield return new WaitForSecondsRealtime(.1f);
        Check(V<int>("memoryConfirmed")==0,"input rejected before reveal ends");Mark("showing");
        if(role=="host")yield return Capture("showing");
        while(Memory.Phase!=ArrowMemoryPhase.Input)yield return null;yield return null;yield return null;
        Check(Now>=Memory.PhaseAt&&Math.Abs(Memory.InputEndsAt-Memory.PhaseAt-10)<.001,"server provides ten-second input window");
        CheckPrivate();Check(V<List<Label>>("memoryArrowLabels")[0].text=="•","hidden answer not left in UI");
        player.InputMemory(-1,memoryRound,0);player.InputMemory(0,memoryRound+1,0);yield return new WaitForSecondsRealtime(.1f);
        Check(V<int>("memoryConfirmed")==0,"invalid direction and stale memory round rejected");
        if(role=="host")
        {
            yield return Press(DuelArrowMemory.DirectionAt(seed,0),1);yield return Press(DuelArrowMemory.DirectionAt(seed,1),2);
            CheckPrivate();Mark("partial");yield return Capture("input");
            while(!Seen("client","locked"))yield return null;CheckPrivate();
            yield return Press(DuelArrowMemory.DirectionAt(seed,2),3);
        }
        else
        {
            while(!Seen("host","partial"))yield return null;CheckPrivate();
            for(int i=0;i<3;i++)yield return Press((DuelArrowMemory.DirectionAt(seed,i)+1)%4,i+1);
            player.InputMemory(0,memoryRound,3);yield return new WaitForSecondsRealtime(.1f);
            Check(V<int>("memoryConfirmed")==3,"completed count blocks extra input");CheckPrivate();Mark("locked");
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return null;yield return null;
        var ranked=player.State.Value;double rankedAt=ranked.Clock.PhaseAt;
        Check(ranked.Clock.First==0&&ranked.Clock.Memory.Winner==0,"correct player earns first place");
        Check(ranked.Clock.Memory.Seat0.Errors==0&&ranked.Clock.Memory.Seat1.Errors==3,"server reveals actual wrong arrow counts at results");
        Check(ranked.Clock.Memory.Seat0.Rank==1&&ranked.Clock.Memory.Seat1.Rank==2,"both players see identical memory ranking");
        Check(ranked.Clock.Memory.EliminatedMask==2&&ranked.AliveMask==3&&ranked.PartyAliveMask==3&&ranked.Seat0.LivesRemaining==3&&ranked.Seat1.LivesRemaining==3,"memory elimination consumes no party life");
        int own=0;foreach(var label in V<Label[]>("clockRankingEntries"))if(label.ClassListContains("clock-ranking-own")&&label.style.display.value!=DisplayStyle.None)own++;
        Check(own==1,"result highlights local player");if(role=="host")yield return Capture("results");
        while(player.State.Value.Clock.Phase==ClockPhase.Ranking)yield return null;
        Check(Now>=rankedAt+2.9&&player.State.Value.Clock.Phase==ClockPhase.ItemPreparation,"three-second result proceeds to item use");
        Check(player.State.Value.AliveMask==3,"memory loser remains available in shooting turn");
        while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;Mark("shooting");
        if(role=="host")
        {
            while(!Seen("client","shooting"))yield return null;
            player.Fire(0,65,false,Now);
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;
        Check(player.State.Value.Shots==1&&player.State.Value.Seat0.LivesRemaining==3&&player.State.Value.Seat1.LivesRemaining==3,"winner can miss one real shot without harming either player");
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        Check(player.State.Value.ManualMiniGame&&player.State.Value.AliveMask==3&&player.State.Value.Clock.Memory.Phase==ArrowMemoryPhase.None,"next manual selection resets private memory state");
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        Finish("PASS memory "+role+": "+checks+" keyboard/owner ack/privacy/results/lives/items/shot/next selection checks");
    }
    IEnumerator Capture(string label)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;
        var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;
        doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;
        doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.07f);
        var previous=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});
        RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();
        var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();
        File.WriteAllBytes(path+label+".png",scene.EncodeToPNG());RenderTexture.active=previous;doc.panelSettings.targetTexture=null;
        target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL memory timeout "+role+" "+(player==null?"not joined":player.State.Value.Clock.Phase+" / "+Memory.Phase+" own count "+V<int>("memoryConfirmed")),1);}
}
