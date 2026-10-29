using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
public sealed class ItemRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelDirector director;int seat,checks;double deadline;
    sealed class Fixed:System.Random{readonly double sample;public Fixed(double v=0){sample=v;}public override int Next(int min,int max)=>min;public override int Next(int max)=>0;public override double NextDouble()=>sample;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--items-qa");if(i<0)return;var q=new GameObject("ItemsRuntimeQA").AddComponent<ItemRuntimeQA>();q.role=a[i+1];q.path=Path.Combine(Application.dataPath,"..","items-qa-");q.deadline=Time.realtimeSinceStartupAsDouble+200;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    T V<T>(string field)=>(T)typeof(RouletteView).GetField(field,F).GetValue(view);
    double Now=>player.NetworkManager.ServerTime.Time;
    void Mark(string stage)=>File.WriteAllText(path+role+"-"+stage,"ok");bool Seen(string actor,string stage)=>File.Exists(path+actor+"-"+stage);
    void Finish(string text,int code=0){Debug.Log(text);File.WriteAllText(path+role+"-done",text);deadline=double.MaxValue;Application.Quit(code);}
    void Check(bool v,string text){checks++;if(!v){Finish("FAIL items "+role+": "+text,1);throw new Exception(text);}}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Random(double sample)=>typeof(DuelDirector).GetField("random",F).SetValue(director,new Fixed(sample));
    IEnumerator Click(Button button)
    {yield return new WaitForSecondsRealtime(.1f);if(Mouse.current==null)InputSystem.AddDevice<Mouse>();var p=button.worldBound.center;var point=new Vector2(p.x,Screen.height-p.y);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point+new Vector2(15,10),buttons=0});yield return null;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point,buttons=0});yield return null;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point,buttons=1});yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point,buttons=0});yield return new WaitForSecondsRealtime(.1f);}
    IEnumerator Shoot(int target,DuelHitPart part)
    {var a=player.State.Value.ForSeat(seat);double now=Now;DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(seat),out float y,out float x);DuelGunplay.Sway(seat,a.Weapon,true,now,now,out float sy,out float sp);player.Fire(y-sy,x-sp,true,now);yield return null;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(role=="host"?1:role=="client"?2:3);manager=HelloWorldManager.Instance;
        if(role=="host")
        {view=manager.GetComponent<RouletteView>();typeof(RouletteView).GetMethod("SetWeaponInfo",F).Invoke(view,new object[]{true});typeof(RouletteView).GetMethod("SetInfoMode",F).Invoke(view,new object[]{true});yield return null;Check(V<Label>("infoTitle").text=="아이템 정보"&&V<VisualElement[]>("itemInfoCards")[1].style.display.value==DisplayStyle.Flex&&V<VisualElement[]>("weaponInfoCards")[0].style.display.value==DisplayStyle.None,"main menu separates item explanations from gun cards");yield return Capture("item-info");typeof(RouletteView).GetMethod("SetWeaponInfo",F).Invoke(view,new object[]{false});}
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17935");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;seat=player.Seat.Value;view=manager.GetComponent<RouletteView>();while(RouletteRules.Count(player.State.Value.ConnectedMask)<3)yield return null;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==38,"updated protocol is active");
        if(role=="host")
        {director=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(manager);var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",F).GetValue(director);typeof(DuelClockContest).GetField("random",F).SetValue(clock,new Fixed());Random(.9);if(!manager.DeveloperMode)manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);manager.SetRoomStartWithItems(true);manager.SetRoomShotTime(0);Mark("setup");}
        while(!Seen("host","setup")||!player.State.Value.ManualMiniGame||!player.State.Value.StartWithItems||player.State.Value.ShotTimeLimit!=0)yield return null;
        if(role!="host"){manager.SetRoomManualMiniGame(false);manager.SetRoomStartWithItems(false);yield return null;Check(player.State.Value.ManualMiniGame&&player.State.Value.StartWithItems,"remote setting mutation rejected");}
        player.Rematch();
        while(player.State.Value.Stage!=DuelStage.Countdown)yield return null;
        Check(player.State.Value.Seat0.HasTaser&&player.State.Value.Seat0.HasRecovery&&!player.State.Value.Seat1.HasTaser&&!player.State.Value.Seat2.HasRecovery,"only host receives starting inventory");Mark("host-only");
        if(role=="host")
        {while(!Seen("client","host-only")||!Seen("witness","host-only"))yield return null;var states=(DuelActionState[])typeof(DuelDirector).GetField("actions",F).GetValue(director);for(int i=1;i<3;i++)states[i].HasRecovery=states[i].HasTaser=true;Publish();Mark("fixture-grants");}
        while(!Seen("host","fixture-grants")||!player.State.Value.ForSeat(seat).HasTaser)yield return null;
        for(int round=1;round<=3;round++)
        {
            while(player.State.Value.Clock.Phase!=ClockPhase.Selecting||player.State.Value.Clock.Iteration!=round)yield return null;yield return null;
            Check(V<VisualElement>("partySelection").style.display.value==DisplayStyle.None,"manual turn never spins roulette");
            Check(player.State.Value.ForSeat(seat).HasTaser,"reusable inventory persists across mini-games");
            if(role!="host"){manager.ChooseMiniGame(PartyMiniGame.Greed);yield return null;Check(player.State.Value.Clock.Phase==ClockPhase.Selecting,"remote mini choice rejected");Mark("select"+round);}
            if(role=="host")
            {
                while(!Seen("client","select"+round)||!Seen("witness","select"+round))yield return null;
                if(round==1){yield return new WaitForSecondsRealtime(6.1f);Check(player.State.Value.Clock.Phase==ClockPhase.Selecting,"host selection remains pending beyond roulette duration");yield return Capture("manual-selection");}
                Check(UnityEngine.Cursor.lockState==CursorLockMode.None&&V<VisualElement>("manualSelection").style.display.value==DisplayStyle.Flex,"host selector releases cursor");
                foreach(var b in V<VisualElement>("manualSelection").Query<Button>().ToList())if(b.text=="초읽기"){yield return Click(b);break;}
                Check(player.State.Value.Clock.MiniGame==PartyMiniGame.Countdown&&player.State.Value.Clock.Phase==ClockPhase.Counting,"real selector button starts chosen mini, repeat allowed round="+round+" phase="+player.State.Value.Clock.Phase+" game="+player.State.Value.Clock.MiniGame);
                if(round==1){director.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body);Publish();}
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;var c=player.State.Value.Clock;
            while(Now<c.StartedAt+c.Target/100.0+seat*.35)yield return null;player.StopClock(Now);
            while(V<bool>("clockAwaiting"))yield return null;
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;
            Check(player.State.Value.Clock.First==0,"host wins fixture mini");
            Check(V<VisualElement>("itemRow").style.display.value==DisplayStyle.Flex,"item UI appears only in item turn");
            Check(V<Button[]>("itemButtons")[1].Query<Label>().First().text=="테이저건"&&V<Button[]>("itemButtons")[1].Query<Image>().First().image!=null,"item button contains thumbnail and final name");
            if(round==1)
            {
                if(role=="host")Check(!V<Button[]>("itemButtons")[1].enabledInHierarchy,"winner cannot activate taser");
                else
                {
                    if(role=="client")
                    {Check(player.State.Value.ForSeat(seat).Health==50,"fixture starts injured");yield return Click(V<Button[]>("itemButtons")[0]);while(player.State.Value.ForSeat(seat).HasRecovery)yield return null;yield return null;Check(player.State.Value.ForSeat(seat).Health==100&&V<VisualElement>("healOverlay").style.display.value==DisplayStyle.Flex&&V<Button[]>("itemButtons")[0].style.display.value==DisplayStyle.None,"remote healing consumes item with immediate aura");yield return Capture("healing");}
                    yield return Click(V<Button[]>("itemButtons")[1]);while(!player.State.Value.ForSeat(seat).TaserArmed)yield return null;Check(player.State.Value.ForSeat(seat).TaserReadyIteration==3&&player.State.Value.ForSeat(seat).HasTaser,"activation retains inventory and sets cooldown");yield return null;Check(V<Button[]>("itemButtons")[1].ClassListContains("item-used"),"selected taser has distinct yellow border");
                }
            }
            else if(round==2&&role!="host")
            {Check(!V<Button[]>("itemButtons")[1].enabledInHierarchy,"next mini cooldown disables button");player.UseItem(DuelItem.Taser);yield return new WaitForSecondsRealtime(.15f);Check(!player.State.Value.ForSeat(seat).TaserArmed,"server rejects cooldown activation");}
            else if(round==3&&role=="client")
            {Check(V<Button[]>("itemButtons")[1].enabledInHierarchy,"two games later taser becomes available");yield return Click(V<Button[]>("itemButtons")[1]);while(!player.State.Value.ForSeat(seat).TaserArmed)yield return null;}
            Mark("items"+round);
            while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;yield return null;
            Check(V<VisualElement>("itemRow").style.display.value==DisplayStyle.None,"item choices hidden during shooting");
            if(round==1)
            {
                if(role=="client")
                {Check(player.State.Value.ForSeat(seat).Weapon==DuelWeapon.Taser&&player.State.Value.ForSeat(seat).Ammo==1,"remote taser has one shot");yield return Shoot(0,DuelHitPart.Head);}
                while(!player.State.Value.Seat0.Stunned)yield return null;yield return null;Check(player.State.Value.Seat1.FiredWeapon==DuelWeapon.Taser&&Resources.Load<AudioClip>("WeaponAudio/TaserFire0").length>0,"taser has independent electric audio and persistent fired weapon");
                Check(player.State.Value.Seat0.Health==95&&player.State.Value.Clock.Phase==ClockPhase.Shooting,"taser deals five without ending shooting turn");
                if(role=="host")
                {Check(V<VisualElement>("electricOverlay").style.display.value==DisplayStyle.Flex,"victim receives electrical screen effect");var fps=manager.GetComponent<DuelFirstPerson>();Check(!fps.Ads,"stunned owner cannot aim");player.Look(90,0,true);yield return new WaitForSecondsRealtime(.1f);Check(!player.State.Value.Seat0.Aiming,"server rejects voluntary aim while stunned");yield return Capture("electrical");}
                else Check(V<VisualElement>("electricOverlay").style.display.value==DisplayStyle.None,"electrical effect belongs only to victim");
                Mark("stunned");
                if(role=="host")
                {while(!Seen("client","stunned")||!Seen("witness","stunned"))yield return null;Random(0);}
            }
            else if(round==2&&role=="host")
            {while(!Seen("client","items2")||!Seen("witness","items2"))yield return null;player.Fire(0,60,false,Now);}
            else if(round==3)
            {
                if(role=="host")
                {director.Game.Shoot(1,0,DuelWeapon.SawedOff,DuelHitPart.Body);Publish();for(int i=0;i<3;i++){director.Game.Shoot(1,0,DuelWeapon.Taser,DuelHitPart.Body);Publish();}Check(director.Game.HealthForSeat(0)==5,"fatal taser fixture has five HP");Mark("fatal-ready");}
                if(role=="client"){while(!Seen("host","fatal-ready")||player.State.Value.Seat0.Health!=5)yield return null;yield return Shoot(0,DuelHitPart.Head);}
                while(player.State.Value.Seat0.LivesRemaining!=2)yield return null;yield return new WaitForSecondsRealtime(.1f);
                Check(V<VisualElement>("killFeed").Query<Image>().ToList().Exists(i=>i.image.name=="테이저건"),"taser kill uses taser thumbnail");
                Check(V<VisualElement>("killFeed").Query<Label>().ToList().Exists(l=>l.text=="헤드샷"),"kill feed labels physical head contact beside icon");
                if(role=="host"){Check(V<Label>("victoryText").text.Contains("목숨을 잃었습니다!")&&player.DeathReport.Value.Count>=5&&player.DeathReport.Value.ForHit(player.DeathReport.Value.Count-1).Fatal,"owner gets full damage history and correct life-loss banner");yield return Capture("taser-kill");}
                else if(role=="witness")yield return Capture("kill-feed");
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;yield return null;
            Check(!player.State.Value.ForSeat(seat).Stunned&&!player.State.Value.ForSeat(seat).TaserArmed&&V<VisualElement>("electricOverlay").style.display.value==DisplayStyle.None,"all used and unused turn effects expire");
            if(round==1)Check(player.State.Value.Seat0.Ammo==1&&player.State.Value.Seat0.FiredAt<0,"shock preserves shooter ammunition");
        }
        Mark("checked");while(!Seen("host","checked")||!Seen("client","checked")||!Seen("witness","checked"))yield return null;
        Finish("PASS items "+role+": "+checks+" host-choice/repeat/inventory/heal/taser/stun/cooldown/kill-log checks");
    }
    IEnumerator Capture(string label)
    {
        var doc=FindFirstObjectByType<UIDocument>();int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.06f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path+label+".png",scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL items timeout "+role+" "+(player==null?"not joined":player.State.Value.Clock.MiniGame+" "+player.State.Value.Clock.Phase),1);}
}
