using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
public sealed class TaserHitscanRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string role,path,step="start";int checks;double deadline,nextState;HelloWorldManager manager;HelloWorldPlayer player;DuelFirstPerson fps;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    void Property(object o,string n,object value)=>o.GetType().GetProperty(n,F).SetValue(o,value);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL hitscan "+role+": "+why,1);throw new Exception(why);}}
    void Done(string message,int code=0){Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;Application.Quit(code);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Phase(ClockPhase phase,PartyMiniGame mini,int iteration,int first,double until)
    {var clock=Field<DuelClockContest>(D,"clock");Property(clock,"State",new ClockState{Phase=phase,MiniGame=mini,Iteration=iteration,First=first,ParticipantsMask=7,PhaseAt=Now,StartedAt=until});Publish();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--taser-hitscan-qa");if(i<0)return;var q=new GameObject("Taser hitscan QA").AddComponent<TaserHitscanRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--taser-hitscan-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+130;Application.targetFrameRate=60;Application.runInBackground=true;}
    void Update()
    {
        if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL hitscan timeout "+role,1);
        if(player!=null&&player.IsSpawned&&Time.realtimeSinceStartupAsDouble>=nextState)
        {var s=player.State.Value;var a=s.Seat1;File.WriteAllText(Path.Combine(path,role+"-state"),$"{step} {s.Phase}/{s.Stage}/{s.Clock.Phase} round={s.Round} iteration={s.Clock.Iteration} alive={s.AliveMask} weapon={a.Weapon} action={a.Action} ammo={a.Ammo} version={a.Version} quick={a.QuickItem}/{a.QuickEquipped}/{a.QuickSpent}/{a.QuickItemVersion} shots={a.ShotsFired} stunned={a.Stunned} aim={a.AimYaw}/{a.AimPitch} hp0={s.Seat0.Health}");nextState=Time.realtimeSinceStartupAsDouble+.5;}
    }
    void AimAt(int target,bool arm=false)
    {DuelGunplay.Angles((arm?DuelGunplay.Local(target,-.60f,1.25f,.45f):DuelGunplay.Point(target,DuelHitPart.Head))-DuelGunplay.Eye(player.Seat.Value),out float y,out float p);Property(fps,"Yaw",y);Property(fps,"Pitch",p);Property(fps,"Ads",false);player.Look(y,p,false);}
    IEnumerator Fire(bool arm=false)
    {AimAt(0,arm);yield return new WaitForSecondsRealtime(.15f);File.WriteAllText(Path.Combine(path,"fire-input"),$"seat={player.Seat.Value} yaw={fps.Yaw} pitch={fps.Pitch} sample={Now}");player.Fire(fps.Yaw,fps.Pitch,true,Now);}
    IEnumerator Wheel(Mouse mouse,float amount)
    {mouse.MakeCurrent();InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,amount));yield return null;yield return null;InputSystem.QueueDeltaStateEvent(mouse.scroll,Vector2.zero);yield return null;}
    IEnumerator Arc()
    {
        var effects=manager.GetComponent<DuelShotVisuals>();while(effects.TaserActiveCount==0)yield return null;
        var arc=Field<Array>(effects,"tasers").GetValue(1);Check(arc!=null,"taser arc appears on "+role);
        var start=(Vector3)arc.GetType().GetProperty("Start").GetValue(arc);var end=(Vector3)arc.GetType().GetProperty("End").GetValue(arc);
        Check(Vector3.Distance(start,end)<=DuelGunplay.TaserRange+.001f,"effect stays inside taser range");
        var cores=Field<LineRenderer[]>(arc,"cores");Check(cores.Length==2&&cores[0].positionCount==36&&cores[0].sharedMaterial.shader.isSupported,"paired segmented wires and supported electric shader");
        Check(cores[0].transform.parent.GetComponentsInChildren<Collider>(true).Length==0,"cosmetic arcs have no collision authority");
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,role+"-Taser-Arc.png"));
        yield return new WaitForSecondsRealtime(.5f);Check(effects.TaserActiveCount==0,"arc fades and does not replay old state");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();fps=manager.GetComponent<DuelFirstPerson>();
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18338");while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        RoomCameraCaptureQA.Setup(view);Check(player.NetworkManager.NetworkConfig.ProtocolVersion==DuelNetworkProtocol.Version,"hitscan protocol");
        if(role=="host")
        {
            Mark("listening");while(player.State.Value.ConnectedMask!=3)yield return null;
            D.Join(2,Now);D.Game.SetShotTime(0);D.SetAutoSelectItems(true);D.Ready(0,Now);Publish();Mark("ready-options");while(!Seen("client","ready"))yield return null;
            D.Ready(2,Now);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});Phase(ClockPhase.ItemPreparation,PartyMiniGame.QuickHands,1,0,Now+30);
            while(D.ForSeat(1).QuickItem!=2)yield return null;Phase(ClockPhase.QuickHands,PartyMiniGame.QuickHands,1,0,Now);
            var states=Field<DuelActionState[]>(D,"actions");states[1].Weapon=DuelWeapon.BAS;states[1].Action=DuelAction.OpenBarrel;states[1].Code=1234;states[1].Progress=1;states[1].Version++;
            states[1].QuickItem=3;states[1].QuickItemVersion++;Publish();Mark("quick");
            while(!Seen("client","ink-panel-checked"))yield return null;
            states[1].QuickItem=2;states[1].QuickItemVersion++;Publish();
            fps.enabled=false;Property(fps,"Yaw",300f);Property(fps,"Pitch",0f);Property(fps,"Ads",false);Property(fps,"Active",true);
            while(D.ForSeat(1).ShotsFired==0)yield return null;
            Check(D.ForSeat(0).Health==95&&D.ForSeat(0).Stunned&&D.ForSeat(1).LastHitPart==DuelHitPart.Body,"remote arm shot applies damage and shock");
            Check(D.ForSeat(0).HitAt==D.ForSeat(1).FiredAt&&D.ForSeat(0).StunnedAt==D.ForSeat(1).FiredAt,"server hit uses firing tick without travel delay");
            Check(Field<DuelProjectileVolley[]>(D,"projectiles")[1]==null,"no server taser projectile");yield return Arc();
            while(!Seen("client","arc-verified"))yield return null;yield return new WaitForSecondsRealtime(2.1f);Check(!D.ForSeat(0).Stunned,"shock expires by 1.3 seconds");
            D.Game.Shoot(2,0,DuelWeapon.SawedOff,DuelHitPart.Body,true);for(int i=0;i<3;i++)D.Game.Shoot(2,0,DuelWeapon.Taser,DuelHitPart.Body,true);Check(D.ForSeat(0).Health==5,"fatal fixture");
            Phase(ClockPhase.ItemPreparation,PartyMiniGame.Countdown,4,2,Now+5);Mark("next-items");while(!D.ForSeat(1).TaserArmed)yield return null;
            while(D.Clock.Phase!=ClockPhase.Shooting)yield return null;Mark("fatal-ready");while(D.ForSeat(0).DiedAt<0)yield return null;
            Check(D.ForSeat(0).LivesRemaining==2&&D.ForSeat(1).Kills>=1&&D.ForSeat(1).FiredWeapon==DuelWeapon.Taser,"fatal taser preserves life loss and kill weapon");Mark("fatal-verified");while(!Seen("client","finished"))yield return null;
        }
        else
        {
            while(!Seen("host","ready-options")||player.State.Value.Seat1.SelectedItems!=DuelLoadout.All)yield return null;player.Rematch();while(!player.State.Value.IsPracticing(1))yield return null;Mark("ready");
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;player.UseItem(DuelItem.Taser);while(player.State.Value.Seat1.QuickItem!=2)yield return null;
            while(!Seen("host","quick")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands||player.State.Value.Seat1.QuickItem!=3)yield return null;yield return null;
            var panel=Field<VisualElement>(view,"inputPanel");var digits=Field<VisualElement>(view,"digits");
            var wheelMouse=InputSystem.AddDevice<Mouse>();wheelMouse.MakeCurrent();
            Check(panel.style.display.value!=DisplayStyle.None&&digits.style.display.value!=DisplayStyle.None,"quick hands reload input visible");
            yield return Wheel(wheelMouse,-120);while(!player.State.Value.Seat1.Ink.Armed)yield return null;yield return null;
            Check(panel.style.display.value==DisplayStyle.None&&digits.style.display.value==DisplayStyle.None,"quick hands reload hidden while ink equipped");
            yield return Wheel(wheelMouse,120);while(player.State.Value.Seat1.QuickEquipped)yield return null;yield return null;
            Check(panel.style.display.value!=DisplayStyle.None&&digits.style.display.value!=DisplayStyle.None&&player.State.Value.Seat1.Progress==1,"quick hands reload restored with progress");
            Mark("ink-panel-checked");while(player.State.Value.Seat1.QuickItem!=2)yield return null;
            step="switch";player.SwitchQuickItem(true);while(player.State.Value.Seat1.Weapon!=DuelWeapon.Taser)yield return null;yield return null;fps.enabled=false;Property(fps,"Active",true);yield return new WaitForSecondsRealtime(1);step="fire";yield return Fire(true);
            step="wait-shot";while(player.State.Value.Seat1.ShotsFired==0)yield return null;Check(!player.State.Value.Seat1.FiredAds&&player.State.Value.Seat1.FiredSpread==0,"owner sees accurate normal fire");
            yield return Arc();Check(player.State.Value.Seat1.TaserReadyIteration==4&&player.State.Value.Seat1.QuickSpent,"owner cooldown and one-shot state");Mark("arc-verified");
            while(!Seen("host","next-items")||player.State.Value.Clock.Iteration!=4)yield return null;player.UseItem(DuelItem.Taser);while(!player.State.Value.Seat1.TaserArmed)yield return null;
            while(!Seen("host","fatal-ready")||player.State.Value.Clock.Phase!=ClockPhase.Shooting||player.State.Value.Seat1.Ammo!=1)yield return null;
            fps.enabled=true;yield return null;yield return Wheel(wheelMouse,-120);while(!player.State.Value.Seat1.Ink.Armed)yield return null;
            Check(player.State.Value.Seat1.TaserArmed,"wheel equips ink during shooting without losing taser");
            yield return Wheel(wheelMouse,120);while(player.State.Value.Seat1.Ink.Armed)yield return null;
            Check(player.State.Value.Seat1.Weapon==DuelWeapon.Taser,"wheel returns to taser during shooting");fps.enabled=false;yield return Fire();
            while(player.State.Value.Seat0.DiedAt<0)yield return null;yield return new WaitForSecondsRealtime(.1f);
            Check(player.State.Value.Seat1.TaserReadyIteration==7&&Field<VisualElement>(view,"killFeed").childCount>0,"fatal shot has kill feed and cooldown");
            while(!Seen("host","fatal-verified"))yield return null;Mark("finished");
        }
        manager.Leave();Done("PASS hitscan "+role+": "+checks+" checks");
    }
}
