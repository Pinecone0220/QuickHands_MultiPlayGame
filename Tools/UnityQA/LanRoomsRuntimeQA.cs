using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public sealed class LanRoomsRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    const string Title="LAN QA 20261022";
    string role,path;int checks;double deadline;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelFirstPerson fps;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--lan-qa");if(i<0)return;var q=new GameObject("LAN QA").AddComponent<LanRoomsRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--lan-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+150;Application.targetFrameRate=30;}
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL LAN "+role+": "+s,1);throw new Exception(s);}}
    void Finish(string s,int code=0)
    {
        Debug.Log(s);File.WriteAllText(Path.Combine(path,role+"-done"),s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");
    bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    DuelLanRoom Find(string title){foreach(var r in manager.Rooms.Rooms)if(r.title==title)return r;return null;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        if(role=="model")yield return Model();else if(role=="preview")yield return Preview();else yield return Peers();Finish("PASS LAN "+role+": "+checks+" checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL LAN timeout "+role,1);}
    IEnumerator Model()
    {
        string id=Guid.NewGuid().ToString("N"),proof=DuelRoomAdmission.PasswordProof(id,"secret123");var bytes=DuelRoomAdmission.Encode(id,"테스터",proof);
        Check(!Encoding.UTF8.GetString(bytes).Contains("secret123"),"admission does not send plaintext password");
        Check(DuelRoomAdmission.TryRead(bytes,id,proof,out var nickname,out var error)&&nickname=="테스터","valid password proof and Korean nickname");
        Check(!DuelRoomAdmission.TryRead(bytes,Guid.NewGuid().ToString("N"),proof,out _,out _),"different room rejected");
        Check(!DuelRoomAdmission.TryRead(bytes,id,"bad",out _,out error)&&error.Contains("비밀번호"),"wrong password rejected");
        Check(!DuelRoomAdmission.TryRead(new byte[513],id,proof,out _,out _),"oversize admission rejected");
        Check(!DuelRoomAdmission.TryRead(new byte[]{255,255},id,proof,out _,out _),"invalid UTF8 rejected");
        Check(DuelRoomAdmission.TryRead(DuelRoomAdmission.Encode(id,"", ""),id,"",out _,out _),"open rooms permit empty nickname/password");
        Check(DuelLanRooms.HexId(id)&&!DuelLanRooms.HexId("bad"),"discovery identifiers bounded");
        using(var a=new DuelLanRooms())using(var b=new DuelLanRooms())using(var browser=new DuelLanRooms())
        {
            Check(a.StartHosting()&&b.StartHosting(),"multiple rooms bind distinct automatic discovery ports");Check(browser.Search(0),"browser starts without user IP or port");
            var ra=new DuelLanRoom{id=id,title="QA discovery A",port=18001,count=1,max=6,protocol=DuelLanRooms.Protocol,locked=true,joinable=true};
            var rb=new DuelLanRoom{id=Guid.NewGuid().ToString("N"),title="QA discovery B",port=18002,count=2,max=6,protocol=DuelLanRooms.Protocol,joinable=true};
            for(int i=0;i<40;i++){double t=i*.1;browser.Tick(null,t);a.Tick(ra,t);b.Tick(rb,t);yield return null;}
            int foundA=0,foundB=0;foreach(var r in browser.Rooms){if(r.id==ra.id)foundA++;if(r.id==rb.id)foundB++;}
            Check(foundA==1&&foundB==1,"broadcast and loopback discover each room only once");
            ra.count=6;ra.joinable=false;int revision=browser.Revision;
            for(int i=0;i<20;i++){double t=4+i*.1;browser.Tick(null,t);a.Tick(ra,t);b.Tick(rb,t);yield return null;}
            bool updated=false;foreach(var r in browser.Rooms)if(r.id==ra.id)updated=r.count==6&&!r.joinable&&r.locked;
            Check(updated&&browser.Revision>revision,"full/in-progress room metadata updates");
            a.StopHosting();for(int i=0;i<10;i++){browser.Tick(null,6+i*.1);b.Tick(rb,6+i*.1);yield return null;}
            browser.Tick(null,14);bool stale=false;foreach(var r in browser.Rooms)if(r.id==ra.id)stale=true;
            Check(!stale,"closed rooms expire from list");
        }
    }
    IEnumerator Peers()
    {
        if(role=="host")
        {
            manager.CreateRoom(Title,"1234");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
            Check(manager.Rooms.Hosting&&manager.SessionPort>0,"host automatically opens game and discovery ports");Check(player.NetworkManager.NetworkConfig.ProtocolVersion==DuelLanRooms.Protocol,"current protocol");Mark("hosted");
            while(D.Game.ConnectedMask!=3||!Seen("observer","listed"))yield return null;
            D.SetAutoSelectItems(true);Publish();player.Rematch();Mark("ready");
            while(D.Game.Phase!=DuelPhase.Playing)yield return null;Mark("started");
            while(!Seen("observer","rejected"))yield return null;
            Check(D.Game.ConnectedMask==3,"rejected guests do not reserve seats");manager.Leave();Check(!manager.Rooms.Hosting,"leave stops advertising immediately");Mark("closed");
            while(!Seen("observer","expired")||!Seen("client","left"))yield return null;
        }
        else
        {
            while(!Seen("host","hosted"))yield return null;
            if(role=="observer")while(!Seen("client","joined"))yield return null;
            manager.BrowseRooms();while(Find(Title)==null)yield return null;var room=Find(Title);
            Check(room.locked&&room.joinable&&room.port>0&&!string.IsNullOrEmpty(room.address),"LAN room includes title count password flag and automatic endpoint");
            if(role=="client")
            {
                manager.JoinRoom(room,"wrong");while(manager.Busy)yield return null;Check(manager.Status.Contains("비밀번호"),"wrong password rejected before spawn");
                manager.BrowseRooms();while(Find(Title)==null)yield return null;manager.JoinRoom(Find(Title),"1234");
                while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
                Check(player.Seat.Value==1&&player.State.Value.ConnectedMask==3,"correct password joins advertised host");Check(!manager.Rooms.Browsing,"joining closes discovery browser");Mark("joined");
                while(!Seen("host","ready")||player.State.Value.Seat1.SelectedItems!=DuelLoadout.All)yield return null;player.Rematch();
                while(!Seen("host","closed"))yield return null;while(manager.Busy)yield return null;Mark("left");
            }
            else
            {
                Check(room.count==2,"browser sees connected player count");Mark("listed");while(!Seen("host","started"))yield return null;
                while(Find(Title)==null||Find(Title).joinable)yield return null;room=Find(Title);Check(!room.joinable,"running game disables entry in discovery");
                var stale=new DuelLanRoom{id=room.id,title=room.title,address=room.address,port=room.port,count=2,max=6,locked=true,joinable=true};
                manager.JoinRoom(stale,"1234");while(manager.Busy)yield return null;Check(manager.Status.Contains("진행 중"),"server also rejects stale-list entry while running");Mark("rejected");
                manager.BrowseRooms();while(!Seen("host","closed"))yield return null;while(Find(Title)!=null)yield return null;
                Check(manager.Rooms.Rooms.Count==0||Find(Title)==null,"closed host disappears without manual refresh");Mark("expired");
            }
        }
    }
    void Keys(Keyboard keyboard,params Key[] keys)
    {
        var state=new KeyboardState(keys);InputState.Change(keyboard,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(keyboard,state,InputUpdateType.Editor);
#endif
        keyboard.MakeCurrent();
    }
    void SetAim(float yaw,float pitch=0){typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,yaw);typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,pitch);}
    IEnumerator Preview()
    {
        using(var listing=new DuelLanRooms())
        {
            Check(listing.StartHosting(),"preview listing available");var r=new DuelLanRoom{id=Guid.NewGuid().ToString("N"),title="위스키 한 잔",count=3,max=6,port=18009,locked=true,joinable=true,protocol=DuelLanRooms.Protocol};
            typeof(RouletteView).GetMethod("SetRoomBrowser",F).Invoke(view,new object[]{true});
            while(Field<VisualElement>(view,"roomRows").childCount==0){listing.Tick(r,Time.realtimeSinceStartupAsDouble);yield return null;}
            Check(Field<TextField>(view,"roomPasswordInput").isPasswordField,"creation password masked");
            foreach(var f in view.GetComponent<UIDocument>().rootVisualElement.Query<TextField>().ToList())Check(f.label!="호스트 IP"&&f.label!="포트","menu removes manual endpoint fields");
            yield return Capture("LAN-Room-List.png");typeof(RouletteView).GetMethod("SetRoomBrowser",F).Invoke(view,new object[]{false});
        }
        manager.CreateRoom("연습방","");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        D.Join(1,Now);D.Join(2,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);D.Ready(1,Now);Publish();yield return null;yield return null;fps.enabled=false;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();Keys(keyboard);
        Check(!DuelPointer.AltHeld&&!DuelPointer.BlocksActions,"default mode stays in gameplay");
        var root=view.GetComponent<UIDocument>().rootVisualElement;var target=new VisualElement();root.Add(target);int clicks=0;target.RegisterCallback<ClickEvent>(e=>clicks++);
        using(var e=ClickEvent.GetPooled()){e.target=target;target.SendEvent(e);}Check(clicks==0,"locked cursor cannot click overlay UI");
        Keys(keyboard,Key.LeftAlt);keyboard.MakeCurrent();var pointer=manager.GetComponent<DuelPointer>();typeof(DuelPointer).GetMethod("Update",F).Invoke(pointer,null);
        Check(DuelPointer.AltHeld&&DuelPointer.BlocksActions,"Alt enters UI and suppresses gameplay");
        using(var e=ClickEvent.GetPooled()){e.target=target;target.SendEvent(e);}Check(clicks==1,"Alt permits UI pointer events");
        Keys(keyboard);typeof(DuelPointer).GetMethod("Update",F).Invoke(pointer,null);Check(DuelPointer.BlocksActions,"Alt release frame cannot fire");target.RemoveFromHierarchy();yield return null;
        Check(!DuelPointer.BlocksActions,"next frame resumes gameplay automatically");
        SetAim(0);Check(D.Fire(0,0,0,false,D.ForSeat(0).Version,Now,Now),"local practice shot");Publish();
        var table=manager.Table;var rigs=Field<Array>(table,"rigs");var rig=rigs.GetValue(0,0);var gun=(Transform)rig.GetType().GetField("Gun").GetValue(rig);
        var late=typeof(RouletteTable).GetMethod("LateUpdate",F);late.Invoke(table,null);var prior=gun.rotation;var pos=gun.position;float fired=D.ForSeat(0).FiredYaw;
        SetAim(35);late.Invoke(table,null);Check(Quaternion.Angle(prior,gun.rotation)>20&&Vector3.Distance(pos,gun.position)>.2f,"gun follows camera position and angle during muzzle flash");
        Check(D.ForSeat(0).FiredYaw==fired,"view changes never steer bullets already fired");
        typeof(DuelFirstPerson).GetField("<Ads>k__BackingField",F).SetValue(fps,true);late.Invoke(table,null);var local=Quaternion.Inverse(fps.Rotation(Now))*(gun.position-new Vector3(DuelGunplay.Eye(0).X,DuelGunplay.Eye(0).Y,DuelGunplay.Eye(0).Z));
        Check(Mathf.Abs(local.x)<.01f,"current ADS position applies even during a hip-fire shot");
        D.Ready(2,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,Iteration=1,First=1,ParticipantsMask=7,PhaseAt=Now,StartedAt=Now+5});Publish();
        Keys(keyboard);keyboard.MakeCurrent();fps.enabled=true;yield return null;Check(!DuelPointer.AltHeld&&D.Clock.Phase==ClockPhase.ItemPreparation,"item turn does not force UI mode");
        var lookMouse=InputSystem.AddDevice<Mouse>();var lookState=new MouseState{delta=new Vector2(20,0)};InputState.Change(lookMouse,lookState,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(lookMouse,lookState,InputUpdateType.Editor);
#endif
        lookMouse.MakeCurrent();float before=fps.Yaw;typeof(DuelFirstPerson).GetMethod("Update",F).Invoke(fps,null);Check(Mathf.Abs(Mathf.DeltaAngle(before,fps.Yaw))>1,"item turn permits normal first-person mouse look");
        Keys(keyboard,Key.RightAlt);keyboard.MakeCurrent();yield return null;Check(DuelPointer.AltHeld&&DuelPointer.BlocksActions&&D.Clock.Phase==ClockPhase.ItemPreparation,"right Alt also enables item interaction");
        D.Game.Shoot(1,0,DuelWeapon.Revolver,DuelHitPart.Body,true);Publish();player.UseItem(DuelItem.Recovery);yield return null;Check(D.Game.HealthForSeat(0)==100&&!D.ForSeat(0).HasRecovery,"Alt item context retains owner item consumption and healing");yield return Capture("Alt-Item-Use.png");
        Keys(keyboard);keyboard.MakeCurrent();yield return null;yield return null;Check(!DuelPointer.BlocksActions,"releasing Alt exits item UI mode");
    }
    IEnumerator Capture(string name)
    {
        const int w=1200,h=800;var doc=view.GetComponent<UIDocument>();var uiTarget=new RenderTexture(w,h,24);uiTarget.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=uiTarget;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;
        yield return new WaitForSecondsRealtime(.15f);var previous=RenderTexture.active;RenderTexture.active=uiTarget;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var rt=new RenderTexture(w,h,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;doc.panelSettings.targetTexture=null;rt.Release();uiTarget.Release();Destroy(rt);Destroy(uiTarget);Destroy(ui);Destroy(scene);
    }
}
