using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class ProjectileRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager m;HelloWorldPlayer p;RouletteView view;int seat,checks;double deadline;
    sealed class TestRandom:System.Random
    {int seed=100;public override int Next(int min,int max)=>min==0&&max==2?1:min;public override int Next(int max)=>max==int.MaxValue?seed++:0;public override double NextDouble()=>0;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--projectile-qa");if(i<0)return;var q=new GameObject("ProjectileQA").AddComponent<ProjectileRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","projectile-");q.deadline=Time.realtimeSinceStartupAsDouble+180;Application.targetFrameRate=30;}
    void Check(bool v,string message){checks++;if(!v){Finish("FAIL "+role+" seat "+seat+": "+message,1);throw new Exception(message);}}
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    VisualElement E(string field)=>(VisualElement)typeof(RouletteView).GetField(field,F).GetValue(view);
    void Mark(string label)=>File.WriteAllText(path+role+"-"+label,"ok");
    bool All(string label){foreach(var r in new[]{"host","c1","c2","c3","c4","c5"})if(!File.Exists(path+r+"-"+label))return false;return true;}
    IEnumerator Load()
    {
        while(p.State.Value.ForSeat(seat).Action!=DuelAction.Aim)
        {var a=p.State.Value.ForSeat(seat);p.InputDigit(a.Digit(a.Progress),a.Version);while(p.State.Value.ForSeat(seat).Version==a.Version)yield return null;yield return null;}
    }
    IEnumerator Fire(int target,DuelHitPart part,bool capture=false)
    {
        yield return Load();var a=p.State.Value.ForSeat(seat);double now=p.NetworkManager.ServerTime.Time;
        DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(seat),out float y,out float x);
        DuelGunplay.Sway(seat,a.Weapon,true,now,now,out float sy,out float sp);
        var fps=m.GetComponent<DuelFirstPerson>();
        typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,y);
        typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,x);
        typeof(DuelFirstPerson).GetField("<Ads>k__BackingField",F).SetValue(fps,true);
        int shots=p.State.Value.Shots;int oldHealth=p.State.Value.ForSeat(target).Health;p.Fire(y-sy,x-sp,true,now);
        while(p.State.Value.ForSeat(seat).ShotsFired==a.ShotsFired)yield return null;
        Check(p.State.Value.ForSeat(target).Health==oldHealth,"launch has no instant damage");
        yield return null;
        var visual=m.GetComponent<DuelShotVisuals>();
        foreach(var line in visual.GetComponentsInChildren<LineRenderer>())Check(line.GetComponent<Collider>()==null&&line.GetComponent<Rigidbody>()==null&&line.GetComponent<Unity.Netcode.NetworkObject>()==null,"visual ray never owns damage physics or authority");
        if(capture){Check(visual.ActiveCount>0,"shot has visible flying pellets");yield return Capture("shotgun-flight");}
        while(p.State.Value.Shots==shots)yield return null;
    }
    IEnumerator Start()
    {
        if(role=="preview"){yield return Preview();yield break;}
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.Begin(DuelMode.PvP,role!="host","127.0.0.1","17923");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();
        while(p.State.Value.ConnectedMask!=63)yield return null;if(role=="host")m.ToggleDeveloperMode();yield return null;
        Check((E("roomSettings").style.display.value==DisplayStyle.Flex)==(role=="host"),"host-only right settings panel");
        if(role=="host")
        {
            var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var rng=new TestRandom();
            typeof(DuelDirector).GetField("random",F).SetValue(d,rng);var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(clock,rng);
            ((IntegerField)E("shotSeconds")).value=7;
            while(p.State.Value.ReadyMask==0)yield return null;
            int readyBefore=p.State.Value.ReadyMask;((Toggle)E("unlimitedShot")).value=true;Check(p.State.Value.ShotTimeLimit==0&&p.State.Value.ReadyMask==readyBefore,"host changes setting, preserves every ready flag");
            yield return Capture("host-room-settings");
            yield return new WaitForSecondsRealtime(.3f);((Toggle)E("unlimitedShot")).value=false;((IntegerField)E("shotSeconds")).value=2;
            p.SelectWeapon(DuelWeapon.SawedOff);while(p.State.Value.Seat0.SelectedWeapon!=DuelWeapon.SawedOff)yield return null;
        }
        else
        {
            if(role=="c1"){while(p.State.Value.ShotTimeLimit!=7)yield return null;p.Rematch();}
            while(p.State.Value.ShotTimeLimit!=0)yield return null;
            Check(E("roomSettings").style.display.value==DisplayStyle.None,"remote cannot see host controls");
            m.SetRoomShotTime(60);Check(p.State.Value.ShotTimeLimit==0,"remote cannot alter settings");
        }
        while(p.State.Value.ShotTimeLimit!=2)yield return null;p.Rematch();
        yield return PrepareItems();
        while(p.State.Value.Clock.Phase!=ClockPhase.QuickCountdown)yield return null;
        double begin=p.State.Value.Clock.StartedAt;int countMask=0;bool countCaptured=false;var before=p.State.Value.ForSeat(seat);
        p.InputDigit(before.Digit(0),before.Version);yield return null;
        while(p.State.Value.Clock.Phase==ClockPhase.QuickCountdown)
        {
            Check(Math.Abs(p.State.Value.Clock.StartedAt-begin)<.00001,"shared countdown deadline remains fixed");
            Check(E("quickStartLayer").style.display.value==DisplayStyle.Flex,"countdown shown to every participant");
            Check(E("inputPanel").style.display.value==DisplayStyle.None,"numeric panel hidden before start");
            var label=((Label)E("quickStartNumber")).text;if(int.TryParse(label,out int n)&&n>=1&&n<=3)countMask|=1<<(n-1);
            Check(p.State.Value.ForSeat(seat).Progress==0&&p.State.Value.ForSeat(seat).CorrectInputs==0,"early numeric request is denied");
            if(role=="host"&&!countCaptured&&countMask==4){countCaptured=true;yield return Capture("quick-countdown");}
            yield return null;
        }
        Check(countMask==7&&p.State.Value.Clock.Phase==ClockPhase.QuickHands,"3-2-1 completes before quick hands");
        Check(p.State.Value.ForSeat(seat).Version!=before.Version,"commands renewed on actual start");
        m.GetComponent<DuelFirstPerson>().enabled=false;
        yield return CheckLookPoses();
        if(role=="host")
        {
            yield return Load();yield return null;Check(E("shotTimer").style.display.value==DisplayStyle.Flex,"quick hands timer after loading");
            yield return new WaitForSecondsRealtime(2.2f);Check(p.State.Value.Seat0.Action==DuelAction.Pickup&&p.State.Value.Shots==0,"limited aim expires to reload, not a fake shot");
            m.SetRoomShotTime(0);Check(p.State.Value.ShotTimeLimit==2,"settings frozen during match");
            yield return Fire(3,DuelHitPart.Body,true);
            var s=p.State.Value;Check(s.Seat3.Health==25&&s.Seat2.Health==75&&s.Seat4.Health==75,"one direct75 and two splash25");
            Check(s.Seat0.LastHitPart==DuelHitPart.Body&&s.Seat0.FiredSeed==100,"authoritative random volley and unified direct category");
            yield return Fire(3,DuelHitPart.Head);
            s=p.State.Value;Check(!s.Alive(3)&&s.Seat2.Health==50&&s.Seat4.Health==50&&s.Seat0.FiredSeed==101,"head is same damage, new random pattern each shot");
            while(p.State.Value.Shots<3)yield return null;
            Check(!p.State.Value.Alive(5)&&p.State.Value.Seat1.Kills==1,"remote derringer projectile applies headshot");
            yield return Fire(2,DuelHitPart.Body);yield return Fire(4,DuelHitPart.Body);yield return Fire(1,DuelHitPart.Body);
        }
        else
        {
            while(p.State.Value.Shots<1)yield return null;
            Check(p.State.Value.Seat3.Health==25&&p.State.Value.Seat2.Health==75&&p.State.Value.Seat4.Health==75,"remote replicated damage");
            if(seat==5){var a=p.State.Value.ForSeat(seat);p.InputDigit(a.Digit(0),a.Version);while(p.State.Value.ForSeat(seat).Progress!=1)yield return null;Check(p.State.Value.ForSeat(seat).Progress==1,"peer input continues after foreign volley");}
            while(p.State.Value.Shots<2)yield return null;
            if(seat==1)yield return Fire(5,DuelHitPart.Head);
            if(seat==3)
            {while(p.DeathReport.Value.Count==0)yield return null;var r=p.DeathReport.Value;Check(r.Count==2&&r.Hit0.Damage==75&&r.Hit1.Damage==25&&r.Hit1.Weapon==DuelWeapon.SawedOff&&r.Hit1.Part==DuelHitPart.Body,"remote owner shotgun death report");}
            if(seat==5)
            {while(p.DeathReport.Value.Count==0)yield return null;Check(p.DeathReport.Value.Hit0.Damage==100&&p.DeathReport.Value.Hit0.Part==DuelHitPart.Head,"remote owner derringer headshot report");}
        }
        while(p.State.Value.Clock.Phase!=ClockPhase.QuickResults)yield return null;
        double resultAt=p.State.Value.Clock.PhaseAt;
        Check(p.State.Value.Clock.First==0&&p.State.Value.Clock.Last==3&&p.State.Value.PartyAliveMask==55,"projectile deaths preserve mini-game loser rule");
        yield return new WaitForSecondsRealtime(1.1f);
        for(int i=1;i<6;i++)
        {var body=PosePart(i,"Body");Check(Mathf.Abs(Mathf.DeltaAngle(0,body.localEulerAngles.y))<.1f&&Mathf.Abs(Mathf.DeltaAngle(0,body.localEulerAngles.x)+82)<.1f,"death fall overrides seated look twist");}
        Mark("checked");while(!All("checked"))yield return null;
        while(p.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        Check(p.State.Value.AliveMask==55&&p.State.Value.ShotTimeLimit==2,"revival and room limit persist to next selection");
        Check(p.State.Value.Clock.PhaseAt-resultAt>=5&&p.State.Value.Clock.PhaseAt-resultAt<5.5,"five second result hold before roulette");
        Mark("complete");while(!All("complete"))yield return null;
        Finish("PASS projectile "+role+" seat "+seat+": "+checks+" settings/timeout/pellets/flight/damage/owner/revival checks");
    }
    IEnumerator Preview()
    {
        yield return new WaitForSecondsRealtime(1);m=HelloWorldManager.Instance;m.AiCount=5;m.Begin(DuelMode.AI,false,"127.0.0.1","17924");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        p=HelloWorldPlayer.Local;seat=p.Seat.Value;view=m.GetComponent<RouletteView>();
        var d=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(m);var rng=new TestRandom();
        typeof(DuelDirector).GetField("random",F).SetValue(d,rng);var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(d);typeof(DuelClockContest).GetField("random",F).SetValue(clock,rng);
        ((Toggle)E("unlimitedShot")).value=true;yield return new WaitForSecondsRealtime(.4f);yield return Capture("host-room-settings");
        p.SelectWeapon(DuelWeapon.SawedOff);p.Rematch();yield return PrepareItems();while(p.State.Value.Clock.Phase!=ClockPhase.QuickCountdown)yield return null;yield return null;
        Check(E("quickStartLayer").style.display.value==DisplayStyle.Flex&&E("nameplateLayer").style.display.value==DisplayStyle.Flex,"countdown preserves player nameplates");yield return Capture("quick-countdown");
        while(p.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;m.GetComponent<DuelFirstPerson>().enabled=false;
        yield return Fire(3,DuelHitPart.Body,true);
        ((double[])typeof(DuelDirector).GetField("nextAi",F).GetValue(d))[1]=double.PositiveInfinity;
        while(p.NetworkManager.ServerTime.Time<d.ForSeat(1).BlockedUntil)yield return null;
        double now=p.NetworkManager.ServerTime.Time;while(d.ForSeat(1).Action!=DuelAction.Aim){var a=d.ForSeat(1);Check(d.Input(1,a.Digit(a.Progress),a.Version,now),"prepare AI derringer");}
        DuelGunplay.Angles(DuelGunplay.Point(4,DuelHitPart.Head)-DuelGunplay.Eye(1),out float y,out float x);DuelGunplay.Sway(1,DuelWeapon.Revolver,true,now,now,out float sy,out float sp);
        Check(d.Fire(1,y-sy,x-sp,true,d.ForSeat(1).Version,now,now),"AI derringer launches");typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(m,null);
        yield return null;yield return Capture("derringer-flight");
        Finish("PASS projectile preview: "+checks+" visible shotgun and derringer pixels, shader, muzzle/flight checks");
    }
    IEnumerator PrepareItems()
    {
        while(p.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;
        var c=p.State.Value.Clock;var a=p.State.Value.ForSeat(seat);var fps=m.GetComponent<DuelFirstPerson>();
        Check(Math.Abs(c.StartedAt-c.PhaseAt-5)<.00001,"five second item window");
        p.InputDigit(a.Digit(0),a.Version);p.Fire(0,0,true,p.NetworkManager.ServerTime.Time);
        bool captured=false;
        while(p.State.Value.Clock.Phase==ClockPhase.ItemPreparation)
        {
            Check(p.State.Value.Clock.StartedAt==c.StartedAt,"common preparation deadline fixed");
            Check(E("clockPanel").style.display.value==DisplayStyle.Flex&&((Label)E("clockTitle")).text=="아이템 사용","preparation title shown");
            Check(E("inputPanel").style.display.value==DisplayStyle.None&&!fps.Ads&&!(bool)typeof(DuelFirstPerson).GetField("captured",F).GetValue(fps),"no numeric panel or mouse capture during items");
            Check(p.State.Value.ForSeat(seat).CorrectInputs==a.CorrectInputs&&p.State.Value.Shots==0,"early requests denied");
            if(role=="preview")for(int i=1;i<6;i++)Check(p.State.Value.ForSeat(i).CorrectInputs==0,"AI waits through item window");
            if(role=="preview"&&!captured){captured=true;yield return Capture("item-preparation");}
            yield return null;
        }
        Check(p.State.Value.Clock.Phase==ClockPhase.QuickCountdown&&p.State.Value.Clock.PhaseAt>=c.StartedAt,"countdown starts after items");
        yield return null;
        Check((bool)typeof(DuelFirstPerson).GetField("captured",F).GetValue(fps),"mouse control resumes after items");
        Check(p.State.Value.ForSeat(seat).Action==DuelAction.Pickup,"preparation does not spend aiming limit");
    }
    Transform PosePart(int i,string name)
    {
        var puppets=(Array)typeof(RouletteTable).GetField("puppets",F).GetValue(m.Table);var puppet=puppets.GetValue(i);
        return (Transform)puppet.GetType().GetField(name).GetValue(puppet);
    }
    static float PoseYaw(int i,int step)=>Mathf.Repeat(i*60+(step==0?(i%2==0?45:-45):step==1?(i%2==0?-70:70):step==2?-1:1),360);
    static float PosePitch(int i,int step)=>step<2?(i%2==0?1:-1)*(step==0?25:40):0;
    IEnumerator CheckLookPoses()
    {
        var feet=new Vector3[6];for(int i=0;i<6;i++)feet[i]=PosePart(i,"Root").Find("Shoe").position;
        var fps=m.GetComponent<DuelFirstPerson>();
        for(int step=0;step<4;step++)
        {
            float yaw=PoseYaw(seat,step),pitch=PosePitch(seat,step);
            typeof(DuelFirstPerson).GetField("<Yaw>k__BackingField",F).SetValue(fps,yaw);
            typeof(DuelFirstPerson).GetField("<Pitch>k__BackingField",F).SetValue(fps,pitch);
            double until=p.NetworkManager.ServerTime.Time+1;
            while(p.NetworkManager.ServerTime.Time<until){p.Look(yaw,pitch,false);yield return new WaitForSecondsRealtime(.05f);}
            Mark("pose-set"+step);while(!All("pose-set"+step))yield return null;
            for(int i=0;i<6;i++)
            {
                Check(Mathf.Abs(Mathf.DeltaAngle(p.State.Value.ForSeat(i).AimYaw,PoseYaw(i,step)))<.1f,"owner look replicated at every seat");
                var head=PosePart(i,"Head");var expected=Quaternion.Euler(-PosePitch(i,step),PoseYaw(i,step),0)*Vector3.forward;
                Check(Vector3.Angle(head.TransformDirection(Vector3.back),expected)<2.5f,"face points in replicated horizontal and vertical direction, including yaw wrap");
                float bodyYaw=Mathf.DeltaAngle(0,PosePart(i,"Body").localEulerAngles.y);
                Check(Mathf.Abs(bodyYaw)<=25,"upper body has restrained seated twist");
                if(step<2)Check(Mathf.Abs(bodyYaw)>5&&Mathf.Sign(bodyYaw)==Mathf.Sign(Mathf.DeltaAngle(i*60,PoseYaw(i,step))),"body follows correct side of look");
                Check(Vector3.Distance(feet[i],PosePart(i,"Root").Find("Shoe").position)<.0001f,"feet remain seated");
                Check(p.State.Value.ForSeat(i).CorrectInputs==0&&p.State.Value.ForSeat(i).Progress==0,"looking does not consume action input");
            }
            if(role=="host"&&step==0)yield return Capture("look-pose");
            Mark("pose-checked"+step);while(!All("pose-checked"+step))yield return null;
        }
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL projectile timeout "+role+" "+(p==null?"not joined":p.State.Value.Clock.Phase+" shots "+p.State.Value.Shots),1);}
    IEnumerator Capture(string name)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(name.Contains("room")?.2f:.05f);
        var cam=Camera.main;GameObject shotCamera=null;
        if(role=="preview"&&name.Contains("flight")){shotCamera=new GameObject("Projectile pixel QA camera");cam=shotCamera.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.enabled=false;cam.transform.position=new Vector3(-2,2.4f,-3);cam.transform.LookAt(new Vector3(0,1.3f,0));}
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();
        if(shotCamera!=null)
        {
            var visible=new System.Collections.Generic.List<LineRenderer>();foreach(var line in m.GetComponentsInChildren<LineRenderer>())if(line.enabled){visible.Add(line);line.enabled=false;}
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var without=new Texture2D(w,h,TextureFormat.RGB24,false);without.ReadPixels(new Rect(0,0,w,h),0,0);without.Apply();var before=without.GetPixels();int bright=0;for(int i=0;i<bg.Length;i++)if(bg[i].r-before[i].r>.025f&&bg[i].g-before[i].g>.02f)bright++;
            foreach(var line in visible)line.enabled=true;Destroy(without);Check(bright>0,"projectiles actually produce visible pixels: "+name+" / "+bright);Debug.Log("Projectile visible pixels "+name+": "+bright);Destroy(shotCamera);
        }
        for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+name+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
}
