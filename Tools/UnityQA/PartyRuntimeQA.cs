using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class PartyRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;int seat,stoppedRound,stoppedIteration,shotIteration,trace=-1;
    double deadline;bool spinCapture,settleCapture,hiddenCapture,timeoutCapture;int checks;
    sealed class FixedRandom:System.Random{public override int Next(int min,int max)=>min;public override int Next(int max)=>0;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--party-qa");if(index<0)return;var q=new GameObject("PartyQA").AddComponent<PartyRuntimeQA>();q.role=args[index+1];q.path=Path.Combine(Application.dataPath,"..","party-");q.deadline=Time.realtimeSinceStartupAsDouble+270;Application.targetFrameRate=30;}
    void Check(bool condition,string text){checks++;if(!condition){Finish("FAIL party "+role+" seat "+seat+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void Mark(string name)=>File.WriteAllText(path+role+"-"+name,"ready");
    bool All(string name){foreach(string r in new[]{"host","c1","c2","c3","c4","c5"})if(!File.Exists(path+r+"-"+name))return false;return true;}
    VisualElement Element(string field)=>(VisualElement)typeof(RouletteView).GetField(field,F).GetValue(view);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;
        bool solo=role=="ai"||role=="visual";
        
        if(role=="ai"){m.AiCount=5;m.Difficulty=AiDifficulty.Hard;}
        if(role=="visual")m.AiCount=1;
        m.Begin(solo?DuelMode.AI:DuelMode.PvP,role!="host"&&!solo,"127.0.0.1","17883");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();
        while(p.State.Value.ConnectedMask!=(role=="visual"?3:63))yield return null;
        
        if(role=="host")
        {var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var rng=new FixedRandom();typeof(DuelDirector).GetField("random",F).SetValue(d,rng);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,rng);}
        p.Rematch();
        for(int round=1;round<=2;round++)
        {
            while(p.State.Value.Phase!=DuelPhase.Playing||p.State.Value.Round!=round)yield return null;
            while(p.State.Value.Phase==DuelPhase.Playing)
            {
                var s=p.State.Value;var c=s.Clock;double now=p.NetworkManager.ServerTime.Time;
                int key=s.Round*1000+c.Iteration*10+(int)c.Phase;
                if(trace!=key){trace=key;Debug.Log($"PartyQA {role} round {s.Round} game {c.Iteration} phase {c.Phase} alive {s.AliveMask}");}
                if(c.Phase==ClockPhase.Selecting)
                {
                    Check(Element("partySelection").style.display.value==DisplayStyle.Flex,"selection panel visible");
                    Check(Element("clockPanel").style.display.value==DisplayStyle.None,"countdown not exposed during reel");
                    Check(PartyGames.Count==2 && (c.MiniGame==PartyMiniGame.Countdown||c.MiniGame==PartyMiniGame.QuickHands),"only implemented game selected");
                    double age=now-c.PhaseAt;
                    if(role=="visual" && age>2.5)
                    {Check(Element("nameplateLayer").style.display.value==DisplayStyle.Flex,"nickname overlay persists on selection");yield return Capture("selected-final");Finish("PASS party visual: selector settles, fades neighbours, nickname overlay persists");yield break;}
                    if(role=="host"&&round==1&&c.Iteration==1&&!spinCapture&&age>.4&&age<1.2){spinCapture=true;yield return Capture("spinning");}
                    if(role=="host"&&round==1&&c.Iteration==1&&!settleCapture&&age>2.5&&age<2.95){settleCapture=true;yield return Capture("selected");}
                }
                else if(c.Phase==ClockPhase.Counting)
                {
                    Check(c.Target>=700&&c.Target<=1500&&c.HideAfter<3,"clock bounds");
                    for(int i=0;i<6;i++)Check(c.ForSeat(i).Centiseconds==-1,"records private");
                    if(role=="host"&&round==1&&c.Iteration==1&&!hiddenCapture&&now-c.StartedAt>3.1){hiddenCapture=true;yield return Capture("hidden");}
                    bool shouldStop=role!="ai"&&round==1&&(seat==0||(seat==5&&c.Iteration==1));
                    double sample=c.StartedAt+c.Target/100.0+seat*.02;
                    if(shouldStop && s.Alive(seat)&&(stoppedRound!=round||stoppedIteration!=c.Iteration)&&now>=sample)
                    {stoppedRound=round;stoppedIteration=c.Iteration;p.StopClock(sample);}
                }
                else if(c.Phase==ClockPhase.Ranking && role!="ai"&&round==1&&c.Iteration==1)
                {
                    Check(s.AliveMask==33&&s.ConnectedMask==63,"four unchecked eliminated together, seats retained");
                    Check(c.First==0&&c.Last==5&&c.EliminatedMask==30,"shot ranking excludes timeouts");
                    for(int i=1;i<5;i++)Check(s.ForSeat(i).DeathCause==DuelDeathCause.ClockTimeout&&s.ForSeat(i).DiedAt>=0,"timeout death per remote seat");
                    Check(s.Shots==0&&s.Seat0.Kills==0&&s.Seat5.Kills==0,"timeouts not fake gun kills");
                    if(seat>0&&seat<5)
                    {
                        Check(Element("deathDetails").style.display.value==DisplayStyle.Flex,"owner timeout cause visible without bullet report");
                        if(!timeoutCapture){timeoutCapture=true;yield return Capture("timeout-seat"+seat);}
                    }
                }
                else if(c.Phase==ClockPhase.Shooting&&role!="ai"&&seat==c.First&&shotIteration!=c.Iteration)
                {shotIteration=c.Iteration;p.Fire(0,65,true,now);}
                yield return null;
            }
            var ended=p.State.Value;
            Check(ended.Results.IsValid,"persistent result published");
            if(role!="ai")
            {
                Check(ended.Shots==(round==1?1:0),"only real miss counted as shot");
                Check(ended.Winner==(round==1?0:-1)&&ended.AliveMask==(round==1?1:0),"one stopped wins, all missing draws");
                if(seat>0||round==2)Check(ended.ForSeat(seat).DeathCause==DuelDeathCause.ClockTimeout&&ended.ForSeat(seat).Health==0,"timeout cause and zero HP");
                if(round==2)Check(ended.Results.Highlight.ToString().Contains("모두 탈락"),"no invented draw winner");
            }
            else Check(ended.Seat0.DeathCause==DuelDeathCause.ClockTimeout,"AI human missing stop eliminated");
            while(p.State.Value.Stage!=DuelStage.Idle)yield return null;
            yield return new WaitForSecondsRealtime(3.1f);
            if(role=="host")yield return Capture(round==1?"winner-results":"draw-results");
            if(round==2)Check(((Label)Element("victoryText")).text=="전원 탈락","draw end panel");
            Mark("round"+round+"-viewed");
            if(role!="ai")while(!All("round"+round+"-viewed"))yield return null;
            p.Rematch();while(p.State.Value.Round==round)yield return null;
            Check(p.State.Value.AliveMask==63&&p.State.Value.Seat0.DeathCause==DuelDeathCause.None,"dead included in all-ready restart");
            if(role=="ai")break;
        }
        Mark("complete");if(role!="ai")while(!All("complete"))yield return null;
        Finish("PASS party "+role+" seat "+seat+": "+checks+" selector/timeout/owner-cause/draw/ready checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL party timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase.ToString()),1);}
    IEnumerator Capture(string name)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.15f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+name+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
}
