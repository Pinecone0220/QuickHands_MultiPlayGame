using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Standalone graphics QA: Editor GameView cannot validate desktop fullscreen resolution.
public sealed class DisplayPracticeRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string path;int checks;double deadline;Keyboard keyboard;
    HelloWorldManager manager;RouletteView view;DuelFirstPerson fps;HelloWorldPlayer player;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--display-practice-qa");if(i<0)return;
        var q=new GameObject("Display/practice QA").AddComponent<DisplayPracticeRuntimeQA>();q.path=args[i+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=30;
    }
    void Finish(string message,int code=0)
    {
        Debug.Log(message);File.WriteAllText(Path.Combine(path,"done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL display/practice: "+message,1);throw new Exception(message);}Debug.Log("Display/practice check: "+message);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL display/practice: timeout",1);}
    void Keys(params Key[] keys)
    {InputState.Change(keyboard,new KeyboardState(keys));keyboard.MakeCurrent();}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void ClickFullscreen(EventModifiers modifiers)
    {
        var button=Field<Button>(view,"fullscreen");var pos=button.worldBound.center;
        using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,clickCount=1,mousePosition=pos,modifiers=modifiers}))button.SendEvent(e);
        using(var e=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,clickCount=1,mousePosition=pos,modifiers=modifiers}))button.SendEvent(e);
    }
    IEnumerator Resolution(int width,int height,bool fullscreen)
    {
        double end=Time.realtimeSinceStartupAsDouble+8;
        while((Screen.width!=width||Screen.height!=height||Screen.fullScreen!=fullscreen)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        Check(Screen.width==width&&Screen.height==height&&Screen.fullScreen==fullscreen,$"render size/mode {width}x{height} fullscreen={fullscreen}, actual {Screen.width}x{Screen.height}/{Screen.fullScreenMode}");
        var camera=Camera.main;Check(camera.pixelWidth==width&&camera.pixelHeight==height,"3D camera fills current render area");
        Check(Mathf.Abs(camera.aspect-(float)width/height)<.01f,"camera aspect matches render area");
        var root=view.GetComponent<UIDocument>().rootVisualElement;
        Check(Mathf.Abs(root.resolvedStyle.width/root.resolvedStyle.height-(float)width/height)<.02f,"UI root follows current aspect");
        Check(Field<Button>(view,"fullscreen").text==(fullscreen?"전체화면 끄기":"전체화면 켜기"),"fullscreen button label follows actual mode");
    }
    Quaternion BaseRotation()=>Quaternion.Euler(-fps.Pitch,fps.Yaw,0);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        Check(!Application.isEditor,"test runs in actual Windows player");keyboard=InputSystem.AddDevice<Keyboard>();Keys();
        Screen.SetResolution(960,720,FullScreenMode.Windowed);yield return Resolution(960,720,false);
        var display=Screen.mainWindowDisplayInfo;Check(display.width>0&&display.height>0,"current monitor resolution available");
        ClickFullscreen(EventModifiers.None);yield return Resolution(display.width,display.height,true);
        ScreenCapture.CaptureScreenshot(Path.Combine(path,"Menu-Fullscreen.png"));yield return new WaitForSecondsRealtime(.3f);
        ClickFullscreen(EventModifiers.None);yield return Resolution(960,720,false);
        manager.CreateRoom("시점과 화면 검증","");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        D.Join(1,Now);D.SetAutoSelectItems(true);Publish();yield return new WaitForSecondsRealtime(.2f);
        Check(Quaternion.Angle(fps.Rotation(Now),BaseRotation())<.001f,"unready lobby camera has no weapon sway");
        D.Ready(0,Now);Publish();yield return new WaitForSecondsRealtime(.2f);
        Check(player.State.Value.IsPracticing(0),"ready player enters weapon practice");
        Check(Quaternion.Angle(fps.Rotation(Now),fps.Rotation(Now+.37))>.05f,"practice retains intended weapon sway");
        D.Ready(0,Now);Publish();yield return new WaitForSecondsRealtime(.2f);
        Check(!player.State.Value.IsPracticing(0)&&player.State.Value.ForSeat(0).Action==DuelAction.Aim,"cancel leaves stale Aim action but exits practice");
        for(int i=0;i<6;i++)Check(Quaternion.Angle(fps.Rotation(Now+i*.19),BaseRotation())<.001f,"cancelled practice stays steady sample "+i);
        D.Ready(0,Now);Publish();yield return new WaitForSecondsRealtime(.2f);
        Check(Quaternion.Angle(fps.Rotation(Now),fps.Rotation(Now+.37))>.05f,"ready again restores practice sway");
        Keys(Key.LeftAlt);yield return new WaitForSecondsRealtime(.2f);
        Screen.SetResolution(1000,700,FullScreenMode.Windowed);yield return Resolution(1000,700,false);
        ClickFullscreen(EventModifiers.Alt);yield return Resolution(display.width,display.height,true);
        ScreenCapture.CaptureScreenshot(Path.Combine(path,"Practice-Fullscreen.png"));yield return new WaitForSecondsRealtime(.3f);
        ClickFullscreen(EventModifiers.Alt);yield return Resolution(1000,700,false);
        Keys();Finish("PASS display/practice: "+checks+" checks");
    }
}
