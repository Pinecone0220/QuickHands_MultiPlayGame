using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public sealed class ClockRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldPlayer p;HelloWorldManager m;RouletteView view;DuelFirstPerson fps;
    int seat,stopped,shot;double deadline;bool capturedVisible,capturedHidden,capturedRanking;int checks,lastTrace=-1;
    sealed class FixedRandom:System.Random{public override int Next(int min,int max)=>min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--clock-qa");if(i<0)return;var q=new GameObject("ClockQA").AddComponent<ClockRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","clock-");q.deadline=Time.realtimeSinceStartupAsDouble+230;Application.targetFrameRate=30;}
    void Check(bool value,string text){checks++;if(!value){Finish("FAIL clock "+role+" seat "+seat+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;
        if(role=="ai"){m.AiCount=5;m.Difficulty=AiDifficulty.Hard;}
        m.Begin(role=="ai"?DuelMode.AI:DuelMode.PvP,role!="host"&&role!="ai","127.0.0.1","17879");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();fps=m.GetComponent<DuelFirstPerson>();
        while(p.State.Value.ConnectedMask!=63)yield return null;
        if(role=="host")
        {var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var rng=new FixedRandom();typeof(DuelDirector).GetField("random",F).SetValue(d,rng);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,rng);}
        p.Rematch();while(p.State.Value.Stage!=DuelStage.Racing)yield return null;fps.enabled=false;
        while(p.State.Value.Phase==DuelPhase.Playing)
        {
            var s=p.State.Value;var c=s.Clock;double now=p.NetworkManager.ServerTime.Time;
            int trace=c.Iteration*10+(int)c.Phase;
            if(trace!=lastTrace){lastTrace=trace;Debug.Log($"ClockQA {role} iteration {c.Iteration} phase {c.Phase} alive {s.AliveMask} shots {s.Shots}");}
            Check((c.Phase==ClockPhase.Selecting || (c.Target>=700&&c.Target<=1500&&c.HideAfter<3)),"mode and timing");
            Check(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null),"no number commands");
            int guns=0;foreach(var t in m.Table.GetComponentsInChildren<Transform>())if(t.name.StartsWith("데린저 seat")||t.name.StartsWith("단발 소드오프 seat"))guns++;
            Check(guns==1,"one shared visible gun count "+guns);
            if(c.Phase==ClockPhase.Counting)
            {
                for(int i=0;i<6;i++)Check(c.ForSeat(i).Centiseconds==-1&&c.ForSeat(i).Dice==0,"private stop record");
                if(role=="host"&&c.Iteration==1 && !capturedVisible&&now-c.StartedAt>.5&&now-c.StartedAt<1.5){capturedVisible=true;yield return Capture("visible");}
                if(role=="host"&&c.Iteration==1&&!capturedHidden&&now-c.StartedAt>3.1){capturedHidden=true;Check(((Label)typeof(RouletteView).GetField("clockTimer",F).GetValue(view)).text=="••.••","hidden timer UI");yield return Capture("hidden");}
                double offset=role=="ai"?2.1:c.Iteration==1?.1:seat==5?0:.1+(5-seat)*.1;
                double sample=c.StartedAt+c.Target/100.0+offset;
                if(s.Alive(seat)&&stopped!=c.Iteration&&now>=sample)
                {
                    stopped=c.Iteration;
                    if(role!="ai"&&c.Iteration==2)typeof(RouletteView).GetMethod("StopClock",F).Invoke(view,null);
                    else p.StopClock(sample);
                }
            }
            else if(c.Phase==ClockPhase.Ranking)
            {
                if(role!="ai"&&c.Iteration==1){Check(c.First==5&&c.Last==0,"dice decides six-way tie");for(int i=0;i<6;i++)Check(c.ForSeat(i).Rank==6-i&&c.ForSeat(i).Dice==i+1,"same dice ranks across peers");}
                if(role=="host"&&!capturedRanking){capturedRanking=true;yield return Capture("ranking");}
            }
            else if(c.Phase==ClockPhase.Shooting && c.First==seat && shot!=c.Iteration)
            {
                shot=c.Iteration;Check(s.ForSeat(seat).Action==DuelAction.Aim,"winner can aim");yield return new WaitForSecondsRealtime(.15f);
                now=p.NetworkManager.ServerTime.Time;float yaw=0,pitch=65;
                if(c.Iteration>1){DuelGunplay.Angles(DuelGunplay.Point(c.Last,c.Iteration==2?DuelHitPart.Body:DuelHitPart.Head)-DuelGunplay.Eye(seat),out yaw,out pitch);DuelGunplay.Sway(seat,true,now,now,out float sy,out float sp);yaw-=sy;pitch-=sp;}
                if(c.Iteration==1)
                {
                    fps.enabled=true;yield return null;
                    typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,65f);
                    var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
                    InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right).WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                    yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState());fps.enabled=false;
                }
                else p.Fire(yaw,pitch,true,now);
            }
            yield return null;
        }
        var final=p.State.Value;Check(final.Results.IsValid,"frozen results received");
        if(role!="ai"){Check(final.Winner==5&&final.Shots==7,"miss body then five kills");Check(final.Results.Seat5.Kills==5&&final.Results.Seat5.ClockWins==7,"clock results stats");}
        if(!final.Alive(seat)){while(p.DeathReport.Value.Count==0&&final.ForSeat(seat).DeathCause!=DuelDeathCause.ClockTimeout)yield return null;Check(p.DeathReport.Value.Round==final.Round,"remote owner death report");}
        while(p.State.Value.Stage!=DuelStage.Idle)yield return null;
        yield return new WaitForSecondsRealtime(3.1f);
        if(role=="host")yield return Capture("results");
        File.WriteAllText(path+role+"-ready","ready");
        if(role=="host")while(!File.Exists(path+"c1-ready")||!File.Exists(path+"c2-ready")||!File.Exists(path+"c3-ready")||!File.Exists(path+"c4-ready")||!File.Exists(path+"c5-ready"))yield return null;
        p.Rematch();while(p.State.Value.Round==final.Round)yield return null;
        Check(p.State.Value.AliveMask==63&&p.State.Value.Seat5.ClockWins==0,"all ready resets including dead");
        File.WriteAllText(path+role+"-rematched","ready");
        if(role!="ai")
        {
            while(!File.Exists(path+"host-rematched")||!File.Exists(path+"c1-rematched")||!File.Exists(path+"c2-rematched")||!File.Exists(path+"c3-rematched")||!File.Exists(path+"c4-rematched")||!File.Exists(path+"c5-rematched"))yield return null;
        }
        Finish("PASS clock "+role+" seat "+seat+": "+checks+" timing/privacy/dice/gun/results/owner death/rematch checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL clock timeout "+role+" state "+(p==null?"none":p.State.Value.Clock.Phase+" "+p.State.Value.Clock.Iteration),1);}
    IEnumerator Capture(string name)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.15f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+name+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
}
