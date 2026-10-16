using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
public sealed class RevolverRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;int seat,checks;double deadline;
    sealed class QuickRandom:System.Random{public override int Next(int min,int max)=>min==0&&max==2?1:min;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--revolver-qa");if(i<0)return;var q=new GameObject("RevolverQA").AddComponent<RevolverRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","revolver-");q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    object V(string field)=>typeof(RouletteView).GetField(field,F).GetValue(view);
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");
    bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Check(bool v,string text){checks++;if(!v){Finish("FAIL "+role+": "+text,1);throw new Exception(text);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void MouseButtons(ushort buttons)=>InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(Screen.width/2f,Screen.height/2f),buttons=buttons});
    IEnumerator Digit()
    {
        while(!(bool)typeof(RouletteView).GetMethod("CanAct",F).Invoke(view,null))yield return null;
        var a=p.State.Value.ForSeat(seat);typeof(RouletteView).GetMethod("Press",F).Invoke(view,new object[]{a.Digit(a.Progress)});
        while(p.State.Value.ForSeat(seat).Version==a.Version)yield return null;yield return null;
    }
    IEnumerator Prepare(int rounds)
    {
        while(p.State.Value.ForSeat(seat).Action!=DuelAction.Load)yield return Digit();
        Check(p.State.Value.ForSeat(seat).Length==3,"loading UI uses three digits");
        for(int i=0;i<rounds*3;i++)yield return Digit();
        Check(p.State.Value.ForSeat(seat).Ammo==rounds,"cartridge inserted once per three digits");
    }
    IEnumerator Aim(bool hold)
    {MouseButtons((ushort)(hold?2:0));yield return new WaitForSecondsRealtime(.12f);Check(m.GetComponent<DuelFirstPerson>().Ads==hold,"actual right mouse switches ADS even during preparation");}
    IEnumerator MissByMouse()
    {
        var fps=m.GetComponent<DuelFirstPerson>();typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,65f);
        int shots=p.State.Value.Shots,ammo=p.State.Value.ForSeat(seat).Ammo;MouseButtons(3);
        while(p.State.Value.ForSeat(seat).Ammo==ammo)yield return null;MouseButtons(2);
        while(p.State.Value.Shots==shots)yield return null;
        Check(p.State.Value.ForSeat(seat).Ammo==ammo-1,"mouse shot consumes exactly one chamber");
        Check((int)V("lastHitShot")==0,"miss has no hit marker");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17927");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();while(p.State.Value.ConnectedMask!=3)yield return null;
        if(role=="host")
        {var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var c=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(c,new QuickRandom());if(!m.DeveloperMode)m.ToggleDeveloperMode();m.SetRoomShotTime(0);}
        while(p.State.Value.ShotTimeLimit!=0)yield return null;p.Rematch();while(p.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;yield return null;
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        Check(Mouse.current!=null,"test mouse input available");yield return Aim(true);MouseButtons(3);yield return new WaitForSecondsRealtime(.15f);MouseButtons(2);
        Check(p.State.Value.ForSeat(seat).Ammo==0&&p.State.Value.ForSeat(seat).ShotsFired==0,"empty actual mouse click never fires");
        Check(((VisualElement)V("inputPanel")).style.display.value==DisplayStyle.None,"ADS hides numeric preparation");yield return Aim(false);
        if(role=="host")
        {
            yield return Prepare(1);Check(p.State.Value.Seat0.Action==DuelAction.Load&&p.State.Value.Seat0.CorrectInputs==33,"early first shot after retained preparation and one cartridge");
            yield return Capture("one-loaded");yield return Aim(true);yield return MissByMouse();Check(p.State.Value.Seat0.Action==DuelAction.CloseBarrel,"last round starts spent-case ejection");yield return Aim(false);
            yield return Prepare(6);Check(p.State.Value.Seat0.Ammo==6&&p.State.Value.Seat0.Action==DuelAction.Aim,"six rounds ready");yield return Capture("six-loaded");
            yield return Aim(true);int inputs=p.State.Value.Seat0.CorrectInputs;for(int i=0;i<6;i++)yield return MissByMouse();
            Check(p.State.Value.Seat0.CorrectInputs==inputs&&p.State.Value.Seat0.SpentMask==63,"six successive clicks without commands retain six cases");
            MouseButtons(3);yield return new WaitForSecondsRealtime(.15f);MouseButtons(2);Check(p.State.Value.Shots==7,"seventh click on empty cylinder is denied");yield return Aim(false);Mark("burst");
            while(p.State.Value.Phase!=DuelPhase.Finished)yield return null;Check(!p.State.Value.Alive(0),"remote partial revolver fire kills host");
        }
        else
        {
            while(!Seen("host","burst"))yield return null;
            Check(p.State.Value.Seat0.Ammo==0&&p.State.Value.Seat0.SpentMask==63&&p.State.Value.Seat0.ShotsFired==7,"all chamber updates replicated");
            yield return Prepare(2);yield return Aim(true);m.GetComponent<DuelFirstPerson>().enabled=false;
            int inputs=p.State.Value.Seat1.CorrectInputs;
            for(int i=0;i<2;i++)
            {
                double now=p.NetworkManager.ServerTime.Time;DuelGunplay.Angles(DuelGunplay.Point(0,DuelHitPart.Body)-DuelGunplay.Eye(1),out float y,out float x);DuelGunplay.Sway(1,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);
                int shots=p.State.Value.Shots;p.Fire(y-sy,x-sp,true,now);while(p.State.Value.Shots==shots)yield return null;
                Check(p.State.Value.ForSeat(1).Ammo==1-i,"remote partial load supports successive fire");
            }
            Check(p.State.Value.Phase==DuelPhase.Finished&&p.State.Value.Winner==1&&p.State.Value.Seat1.CorrectInputs==inputs,"two body hits win without another numeric action");
        }
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked"))yield return null;
        Finish("PASS revolver "+role+": "+checks+" mouse/empty/partial/full/burst/replication/damage/UI/mesh checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL revolver timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase+" shots "+p.State.Value.Shots+" ammo "+p.State.Value.ForSeat(seat).Ammo),1);}
    IEnumerator Capture(string label)
    {
        var rigs=(Array)typeof(RouletteTable).GetField("rigs",F).GetValue(m.Table);var rig=rigs.GetValue(seat,0);var type=rig.GetType();var gun=(Transform)type.GetField("Gun").GetValue(rig);var bullets=(Transform[])type.GetField("Cartridges").GetValue(rig);
        Check(bullets.Length==6&&gun.Find("Long blued barrel")!=null&&gun.Find("Six-chamber cylinder pivot")!=null,"reference revolver has six chambers and long barrel");
        yield return new WaitForSecondsRealtime(.12f);
        var camera=new GameObject("Revolver QA camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
        camera.transform.position=gun.TransformPoint(new Vector3(1.25f,.38f,-.25f));camera.transform.LookAt(gun.TransformPoint(new Vector3(0,-.05f,.12f)),gun.up);camera.fieldOfView=42;
        var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var prev=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();File.WriteAllBytes(path+label+".png",image.EncodeToPNG());RenderTexture.active=prev;rt.Release();Destroy(rt);Destroy(image);Destroy(camera.gameObject);
    }
}
