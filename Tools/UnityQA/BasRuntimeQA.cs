using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class BasRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelFirstPerson fps;
    readonly List<string> heard=new List<string>();
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--bas-qa");if(i<0)return;
        var q=new GameObject("BAS QA").AddComponent<BasRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--bas-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+150;Application.targetFrameRate=30;
    }
    void Finish(string s,int code=0)
    {
        Debug.Log(s);File.WriteAllText(Path.Combine(path,role+"-done"),s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL BAS "+role+": "+s,1);throw new Exception(s);}}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");
    bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    bool All(string s)=>Seen("host",s)&&Seen("client",s)&&Seen("observer",s);
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Aim(int actor,int target,DuelHitPart part,double now,out float y,out float p)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(actor),out y,out p);
        DuelGunplay.Sway(actor,player.State.Value.ForSeat(actor).Weapon,true,now,0,out float sy,out float sp);y=(y-sy+360)%360;p-=sp;
    }
    IEnumerator Shot(DuelHitPart part,bool practice)
    {
        while(practice&&player.State.Value.ForSeat(player.Seat.Value).Action==DuelAction.OpenBarrel)
        {
            var lever=player.State.Value.ForSeat(player.Seat.Value);player.InputDigit(lever.Digit(lever.Progress),lever.Version);
            while(player.State.Value.ForSeat(player.Seat.Value).Version<=lever.Version)yield return null;
        }
        double now=Now;int seat=player.Seat.Value;var before=player.State.Value.ForSeat(seat);int n=practice?before.PracticeShotsFired:before.ShotsFired;
        Aim(seat,0,part,now,out float y,out float p);player.Fire(y,p,true,now);player.Fire(y,p,true,now);
        while((practice?player.State.Value.ForSeat(seat).PracticeShotsFired:player.State.Value.ForSeat(seat).ShotsFired)==n)yield return null;
        yield return new WaitForSecondsRealtime(.7f);
        Check((practice?player.State.Value.ForSeat(seat).PracticeShotsFired:player.State.Value.ForSeat(seat).ShotsFired)==n+1,"duplicate owner fire accepted once");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        manager.GetComponent<DuelGameAudio>().Played+=s=>{if(s=="HitHead"||s=="HitConfirm")heard.Add(s);};
        if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS BAS "+role+": "+checks+" checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL BAS timeout "+role,1);}
    IEnumerator Peers()
    {
        if(role!="host")while(!Seen("host",role=="client"?"listening":"client-connected"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17978");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==47,"protocol47");
        if(role=="host"){Mark("listening");while(!Seen("client","connected"))yield return null;Mark("client-connected");}else if(role=="client")Mark("connected");
        while(player.State.Value.ConnectedMask!=7)yield return null;
        if(role=="host"){D.SetAutoSelectItems(true);Publish();}
        while(player.State.Value.ForSeat(player.Seat.Value).SelectedItems!=DuelLoadout.All)yield return null;
        if(role=="client"){player.SelectWeapon(DuelWeapon.BAS);while(player.State.Value.Seat1.SelectedWeapon!=DuelWeapon.BAS)yield return null;Mark("selected");}
        while(!Seen("client","selected")||player.State.Value.Seat1.SelectedWeapon!=DuelWeapon.BAS)yield return null;
        Check(player.State.Value.Seat1.Weapon==DuelWeapon.BAS,"BAS owner selection replicated");
        Check(Field<Button[]>(view,"weaponButtons").Length==3&&Field<Texture2D[]>(view,"weaponIcons")[3]!=null,"third weapon has button and icon");
        if(role!="observer")player.Rematch();while(player.State.Value.PracticeMask!=3)yield return null;yield return null;yield return null;fps.enabled=false;
        Mark("ready");while(!All("ready"))yield return null;
        if(role=="client")
        {
            yield return Shot(DuelHitPart.Body,true);Check(heard.Count==0,"BAS body gives no owner hit confirmation");Mark("body");
            yield return new WaitForSecondsRealtime(.3f);yield return Shot(DuelHitPart.Head,true);Check(heard.Count==1&&heard[0]=="HitHead","BAS head plays owner head feedback");Mark("head");
        }
        while(!Seen("client","head"))yield return null;
        Check(player.State.Value.Seat1.Action==DuelAction.OpenBarrel&&!player.State.Value.Seat1.Aiming,"all peers see required practice lever after firing");
        Check(player.State.Value.Seat0.Health==100&&player.State.Value.Seat0.LivesRemaining==3&&player.State.Value.Shots==0,"BAS practice preserves HP lives and records");
        if(role!="client")Check(heard.Count==0,"foreign BAS hits have no owner hit sound");
        Check(Field<VisualElement>(view,"killFeed").childCount==0,"practice has no kill log");Mark("practice");while(!All("practice"))yield return null;
        if(role=="observer")player.Rematch();while(player.State.Value.Round<1)yield return null;
        if(role=="host")
        {
            typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
            typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=7,StartedAt=Now});
            var a=Field<DuelActionState[]>(D,"actions");a[1].Weapon=DuelWeapon.BAS;a[1].Action=DuelAction.Aim;a[1].LoadedMask=1;a[1].SpentMask=0;a[1].ShootingStartedAt=Now;a[1].Version++;Publish();Mark("match");
        }
        while(!Seen("host","match")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands||player.State.Value.Seat1.Ammo!=1)yield return null;
        if(role=="client"){yield return Shot(DuelHitPart.Head,false);Mark("kill");}
        while(!Seen("client","kill")||player.State.Value.Seat0.LivesRemaining!=1)yield return null;yield return null;
        Check(player.State.Value.Seat0.Health==0&&player.State.Value.Seat0.LivesRemaining==1&&player.State.Value.InParty(0),"real remote headshot removes two lives");
        Check(player.State.Value.Seat1.Kills==1&&player.State.Value.Seat1.DamageDealt==100,"one kill and actual damage replicated");
        Check(player.State.Value.Seat1.Action==DuelAction.OpenBarrel&&player.State.Value.Seat1.SpentMask==1,"remote reload resumes at lever open");
        Check(Field<VisualElement>(view,"killFeed").childCount==1,"BAS kill shown once on every peer");
        if(role=="client")Check(heard.Count==2,"real headshot confirmation delivered to remote owner");
        Mark("verified");while(!All("verified"))yield return null;
    }
    IEnumerator Preview()
    {
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","17979");while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        D.Join(1,Now);D.Join(2,Now);D.SetAutoSelectItems(true);D.SelectWeapon(0,DuelWeapon.BAS,Now);D.SelectWeapon(1,DuelWeapon.BAS,Now);D.Ready(0,Now);D.Ready(1,Now);Publish();yield return null;yield return null;fps.enabled=false;
        Check(fps.Active&&Field<Button[]>(view,"weaponButtons").Length==3,"BAS practice active with three selectable guns");
        foreach(var key in new[]{"BasBolt","BasLoad","BasFire0","BasFire1","BasFire2","BasPickup","BasAim"})
        {var clip=Resources.Load<AudioClip>("WeaponAudio/"+key);Check(clip!=null&&clip.length>.05f,"valid imported "+key);}
        var audio=manager.GetComponent<DuelWeaponAudio>();if(audio==null)audio=manager.Table.GetComponent<DuelWeaponAudio>();
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out float y,out float p);
        typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,y);typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,p);
        yield return Capture("BAS-Hip.png");
        typeof(DuelFirstPerson).GetField("<Ads>k__BackingField",F).SetValue(fps,true);yield return null;yield return null;
        Check(Field<VisualElement>(view,"basScope").style.display.value==DisplayStyle.Flex,"scope overlay visible only during BAS ADS");
        Check(Mathf.Abs(Camera.main.fieldOfView-DuelGunplay.AdsFov(DuelWeapon.BAS))<.01f,"camera uses three-times telescopic FOV");
        var allGuns=Field<Array>(manager.Table,"rigs");var own=allGuns.GetValue(0,3);var gun=(Transform)own.GetType().GetField("Gun").GetValue(own);
        Check(!gun.gameObject.activeSelf,"own rifle cannot obstruct scope picture");yield return Capture("BAS-Scope.png");
        fps.enabled=true;double practiceShotAt=Now;Aim(0,1,DuelHitPart.Head,practiceShotAt,out float practiceYaw,out float practicePitch);
        player.Fire(practiceYaw,practicePitch,true,practiceShotAt);yield return null;yield return null;
        Check(!fps.Ads&&D.ForSeat(0).PracticeShotsFired==1,"owner BAS fire drops ADS and scope on authoritative update");
        D.Look(0,practiceYaw,practicePitch,true,Now);Publish();yield return null;
        Check(!D.ForSeat(0).Aiming&&D.ForSeat(0).Action==DuelAction.OpenBarrel,"held ADS request cannot bypass lever input");
        Check(Field<VisualElement>(view,"inputPanel").style.display.value==DisplayStyle.Flex&&Field<Label>(view,"turn").text.Contains("레버 조작"),"practice displays lever action digits and name");
        yield return Capture("BAS-Practice-Lever.png");
        while(D.ForSeat(0).Action==DuelAction.OpenBarrel)
        {var a=D.ForSeat(0);typeof(RouletteView).GetMethod("Press",F).Invoke(view,new object[]{a.Digit(a.Progress)});yield return null;}
        Check(D.ForSeat(0).CorrectInputs==0&&D.ForSeat(0).Mistakes==0,"practice UI input leaves real results untouched");
        fps.enabled=false;D.Look(0,practiceYaw,practicePitch,true,Now);Publish();yield return null;
        Check(D.ForSeat(0).Aiming&&D.ForSeat(0).Action==DuelAction.Aim,"ADS requests become available after all four lever digits");
        typeof(DuelFirstPerson).GetField("<Ads>k__BackingField",F).SetValue(fps,false);yield return null;
        Check(Field<VisualElement>(view,"basScope").style.display.value==DisplayStyle.None,"scope hides on release");
        D.Ready(2,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=7,StartedAt=Now});
        typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,330f);typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,-30f);
        var actions=Field<DuelActionState[]>(D,"actions");
        while(D.ForSeat(0).Action!=DuelAction.Aim){var a=D.ForSeat(0);Check(D.Input(0,a.Digit(a.Progress),a.Version,Now),"initial lever preparation input");}
        Publish();yield return null;yield return null;
        var casing=(Transform)own.GetType().GetField("EjectedCase").GetValue(own);Check(!casing.gameObject.activeSelf,"initial empty action cannot eject a cartridge");
        double shotAt=Now;Aim(0,1,DuelHitPart.Head,shotAt,out float shotYaw,out float shotPitch);
        Check(D.Fire(0,shotYaw,shotPitch,true,D.ForSeat(0).Version,shotAt,shotAt),"first loaded shot accepted");Publish();
        while(D.Game.Shots<1)yield return null;yield return new WaitForSecondsRealtime(.35f);
        Check(D.ForSeat(0).SpentMask==1,"first shot leaves one spent cartridge");
        actions[0].Progress=3;Publish();yield return Capture("BAS-Lever.png");
        var lever=(Transform)own.GetType().GetField("Barrel").GetValue(own);
        Check(Mathf.Abs(Mathf.DeltaAngle(0,lever.localEulerAngles.x))>40&&Mathf.Abs(Mathf.DeltaAngle(0,lever.localEulerAngles.z))<1,"lower lever swings down without bolt-handle rotation");
        var finalDigit=D.ForSeat(0);Check(D.Input(0,finalDigit.Digit(finalDigit.Progress),finalDigit.Version,Now),"lever completion extracts spent case");Publish();yield return null;yield return null;
        Check(casing.gameObject.activeSelf,"first shot case launches on lever-open completion");
        double ejectedAt=(double)own.GetType().GetField("CaseAt").GetValue(own);Vector3 initialCase=casing.position;
        actions[0].Progress=2;Publish();yield return Capture("BAS-Ejection.png");
        Check(casing.gameObject.activeSelf&&Vector3.Distance(initialCase,casing.position)>.05f,"case travels independently through world space");
        Check((double)own.GetType().GetField("CaseAt").GetValue(own)==ejectedAt,"repeated snapshots do not eject twice");
        yield return Capture("BAS-Loading.png");yield return new WaitForSecondsRealtime(.9f);
        Check(!casing.gameObject.activeSelf,"cosmetic case expires");
        Check(gun.Find("BAS hand-fed rifle cartridge").gameObject.activeSelf,"single cartridge visible at the open ejection port");
    }
    IEnumerator Capture(string name)
    {
        const int w=1200,h=800;var doc=view.GetComponent<UIDocument>();var uiTarget=new RenderTexture(w,h,24);uiTarget.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=uiTarget;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;
        yield return new WaitForSecondsRealtime(.15f);var previous=RenderTexture.active;RenderTexture.active=uiTarget;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var target=new RenderTexture(w,h,24);target.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});RenderTexture.active=target;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;doc.panelSettings.targetTexture=null;target.Release();uiTarget.Release();Destroy(target);Destroy(uiTarget);Destroy(ui);Destroy(scene);
    }
}
