using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
public sealed class BasHandsRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    string role,path;int checks;double deadline,nextLook;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelFirstPerson fps;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    void Property(object o,string name,object value)=>o.GetType().GetProperty(name,F).SetValue(o,value);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    object Rig=>Field<Array>(manager.Table,"rigs").GetValue(0,3);object Puppet(int seat)=>Field<Array>(manager.Table,"puppets").GetValue(seat);
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL BAS hands "+role+": "+why,1);throw new Exception(why);}}
    void Done(string text,int code=0){Debug.Log(text);File.WriteAllText(Path.Combine(path,role+"-done"),text);deadline=double.MaxValue;Application.Quit(code);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--bas-hands-qa");if(i<0)return;var qa=new GameObject("BAS hands QA").AddComponent<BasHandsRuntimeQA>();qa.role=a[i+1];qa.path=a[Array.IndexOf(a,"--bas-hands-dir")+1];Directory.CreateDirectory(qa.path);qa.deadline=Time.realtimeSinceStartupAsDouble+150;Application.targetFrameRate=60;Application.runInBackground=true;}
    void Update()
    {
        if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL BAS hands timeout "+role,1);
        if(role=="host"&&fps!=null&&!fps.enabled&&player!=null&&player.IsSpawned&&Time.realtimeSinceStartupAsDouble>=nextLook)
        {player.Look(fps.Yaw,fps.Pitch,fps.Ads);nextLook=Time.realtimeSinceStartupAsDouble+.05;}
    }
    void View(float yaw,float pitch,bool ads=false)
    {Property(fps,"Yaw",yaw);Property(fps,"Pitch",pitch);Property(fps,"Ads",ads);Property(fps,"Active",true);player.Look(yaw,pitch,ads);}
    void ArmsFollow(bool loading)
    {
        var rig=Rig;var puppet=Puppet(0);var wrist=Field<Transform>(rig,loading?"Loading":"Support");var arm=Field<Transform>(puppet,"LeftForearm");
        Check(Vector3.Distance(arm.position+arm.up*.25f,wrist.position)<.002f,"left forearm meets wrist without view lag");
        var right=Field<Transform>(puppet,"Forearm");var grip=Field<Transform>(rig,"Holding");Check(Vector3.Distance(right.position+right.up*.25f,grip.position)<.002f,"right forearm meets grip");
        Check(Mathf.Abs(arm.localScale.y-.25f)<.001f&&Mathf.Abs(right.localScale.y-.25f)<.001f,"view forearms retain extended fixed length");
        var facing=Quaternion.Euler(-fps.Pitch,fps.Yaw,0);
        var local=Quaternion.Inverse(facing)*arm.rotation;
        var expected=(Quaternion)typeof(RouletteTable).GetMethod("ArmRotation",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{new Vector3(.28f,.40f,.80f).normalized,Quaternion.identity});
        Check(Quaternion.Angle(local,expected)<.05f,"forearm roll stays in view space rather than spinning around world up");
        var upper=Field<Transform>(puppet,"LeftArm");
        Check(upper.GetComponent<Renderer>().enabled&&Vector3.Distance(upper.position+upper.up*.20f,arm.position-arm.up*.25f)<.002f,"upper arm continues into forearm without a floating stump");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18228");while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        if(role=="client")
        {
            while(!Seen("host","scoped")||player.State.Value.Seat0.Weapon!=DuelWeapon.BAS||!player.State.Value.Seat0.Aiming)yield return null;
            yield return new WaitForSecondsRealtime(.1f);Check(Field<Transform>(Rig,"Gun").gameObject.activeInHierarchy,"remote scoped rifle stays visible");
            Check(Field<Transform>(Rig,"Support").gameObject.activeInHierarchy,"remote support hand stays visible");
            bool visible=true;foreach(var r in Field<Renderer[]>(Puppet(0),"ArmRenderers"))visible&=!r.forceRenderingOff;Check(visible,"owner scope never hides arms on remote camera");Mark("scope-verified");
            while(!Seen("host","finished"))yield return null;manager.Leave();Done("PASS BAS hands client: "+checks+" checks");yield break;
        }
        Mark("listening");while(player.State.Value.ConnectedMask!=3)yield return null;
        D.Game.SetShotTime(0);D.SetAutoSelectItems(true);D.SelectWeapon(0,DuelWeapon.BAS,Now);D.Ready(0,Now);Publish();while(!player.State.Value.IsPracticing(0))yield return null;
        yield return null;fps.enabled=false;RoomCameraCaptureQA.Setup(view);View(0,0);yield return null;yield return null;
        var gun=Field<Transform>(Rig,"Gun");var support=Field<Transform>(Rig,"Support");Check(support.gameObject.activeInHierarchy,"dedicated support hand active");Check(!Field<Transform>(Puppet(0),"LeftHand").gameObject.activeSelf,"generic floating palm suppressed");
        Check(Field<Transform>(Rig,"Holding").Find("Trigger finger 0")!=null&&support.Find("Wrapped finger 0 0")!=null,"trigger and wrapping fingers modeled");
        yield return new WaitForSecondsRealtime(.15f);
        var image=Field<UnityEngine.UIElements.Image>(view,"viewModelImage");var root=Field<UnityEngine.UIElements.VisualElement>(view,"root");
        var names=Field<UnityEngine.UIElements.VisualElement>(view,"nameplateLayer");
        Check(image.image!=null&&image.pickingMode==UnityEngine.UIElements.PickingMode.Ignore,"model composite has texture and never intercepts UI clicks");
        Check(root.IndexOf(names)<root.IndexOf(image)&&root.IndexOf(image)<root.IndexOf(Field<UnityEngine.UIElements.VisualElement>(view,"table")),"spatial labels then held model then interaction UI");
        Check((Camera.main.cullingMask&(1<<30))==0&&gun.GetComponentInChildren<Renderer>(true).gameObject.layer==30,"owner gun rendered once in dedicated layer");
        var texture=(RenderTexture)image.image;var old=RenderTexture.active;RenderTexture.active=texture;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();
        int solid=0,clear=0;foreach(var color in pixels.GetPixels32()){if(color.a>240)solid++;if(color.a==0)clear++;}
        File.WriteAllText(Path.Combine(path,"texture-counts"),$"solid={solid} clear={clear} total={texture.width*texture.height} size={texture.width}/{texture.height}");File.WriteAllBytes(Path.Combine(path,"view-model-rgba.png"),pixels.EncodeToPNG());
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-Composite.png"));
        Check(solid>1000&&clear>texture.width*texture.height/2,"held model texture has opaque models and transparent surroundings");
        RenderTexture.active=old;Destroy(pixels);
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-Hip.png"));
        foreach(var angles in new[]{new Vector2(60,35),new Vector2(-65,-35),new Vector2(20,-50),new Vector2(65,50),new Vector2(-65,50),Vector2.zero})
        {View(angles.x,angles.y);yield return null;ArmsFollow(false);Check(Quaternion.Angle(gun.rotation,Quaternion.Euler(-angles.y,angles.x,0))<.01f,"view rotation follows in one frame");}
        View(0,0,true);yield return null;yield return new WaitForSecondsRealtime(.15f);
        Check(!gun.gameObject.activeInHierarchy,"scope hides local rifle");bool hidden=true;foreach(var r in Field<Renderer[]>(Puppet(0),"ArmRenderers"))hidden&=r.forceRenderingOff;Check(hidden,"scope hides all local arm and hand renderers");
        Check(!Field<Transform>(Puppet(0),"Torso").GetComponent<Renderer>().forceRenderingOff,"scope keeps torso visibility policy");
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-Scope.png"));Mark("scoped");while(!Seen("client","scope-verified"))yield return null;
        View(0,-35,true);yield return null;yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-Scope-Down.png"));
        View(0,0);yield return null;Check(gun.gameObject.activeInHierarchy&&support.gameObject.activeInHierarchy,"unscoping restores rifle and support");hidden=false;foreach(var r in Field<Renderer[]>(Puppet(0),"ArmRenderers"))hidden|=r.forceRenderingOff;Check(!hidden,"unscoping restores body arms");
        foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff})
        {
            D.Ready(0,Now);Publish();yield return null;yield return null;
            Check(Field<UnityEngine.UIElements.Image>(view,"viewModelImage").image==null,"cancel readiness clears held-model overlay");
            D.SelectWeapon(0,weapon,Now);D.Ready(0,Now);Publish();View(35,-15);yield return null;yield return null;
            var current=Field<Array>(manager.Table,"rigs").GetValue(0,(int)weapon);
            Check(Field<Transform>(current,"Gun").GetComponentInChildren<Renderer>(true).gameObject.layer==30,"other held gun uses local composition "+weapon);
            Check(Field<Transform>(Puppet(0),"Forearm").GetComponent<Renderer>().enabled,"connected visible arm "+weapon);
        }
        D.Ready(0,Now);D.SelectWeapon(0,DuelWeapon.BAS,Now);D.Ready(0,Now);Publish();View(0,0);yield return null;yield return null;
        var oldRect=Camera.main.rect;Camera.main.rect=new Rect(0,0,.8f,.8f);yield return null;yield return null;
        var resized=(RenderTexture)Field<UnityEngine.UIElements.Image>(view,"viewModelImage").image;
        Check(resized.width==Camera.main.pixelWidth&&resized.height==Camera.main.pixelHeight,"composite follows camera resolution changes");
        Camera.main.rect=oldRect;yield return null;yield return null;
        // Exercise actual preparation and lever inputs, without changing any live game protocol.
        D.Ready(1,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        var clock=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=3,StartedAt=Now});Publish();
        foreach(var action in new[]{DuelAction.Pickup,DuelAction.OpenBarrel,DuelAction.Load,DuelAction.CloseBarrel})
        {
            Check(D.ForSeat(0).Action==action,"expected rifle preparation stage "+action);
            for(int i=0;i<4;i++)
            {
                var a=D.ForSeat(0);Check(D.Input(0,a.Digit(a.Progress),a.Version,Now),"preparation digit "+action);Publish();yield return null;yield return null;
                if(i==1){View(25,-12);yield return null;ArmsFollow(action==DuelAction.Load);yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-"+action+".png"));}
            }
        }
        Check(D.ForSeat(0).Action==DuelAction.Aim&&D.ForSeat(0).Ammo==1,"rifle reaches loaded aim");View(0,0);yield return new WaitForSecondsRealtime(.3f);
        Check(D.Fire(0,0,0,false,D.ForSeat(0).Version,Now,Now),"rifle fires normally");Publish();yield return new WaitForSecondsRealtime(.35f);
        for(int i=0;i<3;i++){var a=D.ForSeat(0);Check(D.Input(0,a.Digit(a.Progress),a.Version,Now),"reload lever digit");Publish();yield return null;}
        yield return new WaitForSecondsRealtime(.2f);ArmsFollow(false);var pivot=Field<Transform>(Rig,"Barrel");var grip=Field<Transform>(Rig,"Holding");
        Check(Vector3.Distance(grip.position,pivot.TransformPoint(new Vector3(.075f,-.125f,-.335f)))<.001f,"grip follows current lever transform");
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"BAS-Lever-Reload.png"));
        var last=D.ForSeat(0);Check(D.Input(0,last.Digit(last.Progress),last.Version,Now),"lever extracts spent cartridge");Publish();yield return null;yield return null;
        Check(Field<Transform>(Rig,"EjectedCase").gameObject.activeSelf,"ejected cartridge animation preserved");Mark("finished");yield return new WaitForSecondsRealtime(.2f);manager.Leave();while(manager.Busy)yield return null;yield return null;Check(Field<UnityEngine.UIElements.Image>(view,"viewModelImage").image==null,"leaving clears held-model overlay");Check(gun.GetComponentInChildren<Renderer>(true).gameObject.layer==0,"leaving restores original render layers");Done("PASS BAS hands host: "+checks+" checks");
    }
}
