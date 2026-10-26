using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

// Isolated-project real owner RPC / third-person equipment checks.
public sealed class NoruFaceRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");DuelClockContest Clock=>Field<DuelClockContest>(D,"clock");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--noru-qa");if(i<0)return;
        var q=new GameObject("NoruFace QA").AddComponent<NoruFaceRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--noru-dir")+1];
        Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+150;Application.targetFrameRate=60;
    }
    void Finish(string text,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),text);Debug.Log(text);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string reason){checks++;if(!ok){Finish("FAIL noru "+role+": "+reason,1);throw new Exception(reason);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL noru timeout "+role,1);}
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");
    bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    void Prep(int iteration)
    {
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});
        var a=Field<DuelActionState[]>(D,"actions");a[1].TaserReadyIteration=0;
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.Countdown,Iteration=iteration,First=0,ParticipantsMask=D.Game.AliveMask,PhaseAt=Now,StartedAt=Now+30});Publish();
    }
    void StartShot(){var s=D.Clock;s.StartedAt=Now+.05;typeof(DuelClockContest).GetProperty("State").SetValue(Clock,s);Publish();}
    void Model()
    {
        Check(DuelLanRooms.Protocol==54,"protocol version");var s=DuelSnapshot.From(D);
        s.Seat0.HasNoruFace=true;s.Seat0.NoruFaceActive=true;s.Seat0.NoruFaceReadyIteration=13;
        s.Seat5.HasNoruFace=true;s.Seat5.NoruFaceActive=false;s.Seat5.NoruFaceReadyIteration=42;
        using(var w=new FastBufferWriter(16384,Allocator.Temp)){w.WriteNetworkSerializable(s);using(var r=new FastBufferReader(w,Allocator.Temp)){r.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(s)&&copy.Seat5.NoruFaceReadyIteration==42&&copy.Seat0.NoruFaceActive,"snapshot preserves ownership, activation and cooldown");}}
        var icon=DuelItemArt.Icon(DuelItem.NoruFace);int pixels=0;foreach(var p in icon.GetPixels())if(p.a>.9f)pixels++;
        Check(icon.name=="노루페이스"&&pixels>3000&&pixels<12000,"padded jacket thumbnail");File.WriteAllBytes(Path.Combine(path,"NoruFace-Icon.png"),icon.EncodeToPNG());Destroy(icon);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role!="host"){while(!Seen("host","listening"))yield return null;if(role=="victim")while(!Seen("gunner","joined"))yield return null;}
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","18993");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;Mark("joined");
        Check(player.Seat.Value==(role=="host"?0:role=="gunner"?1:2),"owned seat");
        if(role=="host")yield return Host();else yield return Client();Finish("PASS noru "+role+": "+checks+" checks");
    }
    IEnumerator Host()
    {
        Model();Mark("listening");while(D.Game.ConnectedMask!=7)yield return null;D.Game.SetShotTime(0);D.SetAutoSelectItems(true);for(int i=0;i<3;i++)D.Ready(i,Now);
        for(int phase=1;phase<=3;phase++)
        {
            int iteration=phase==3?13:9+phase;Prep(iteration);
            if(phase!=2){Check(D.UseItem(0,DuelItem.NoruFace,iteration,Now),"host shooter may wear "+phase);Publish();}
            while(!Seen("gunner","prep"+phase)||!Seen("victim","prep"+phase))yield return null;
            Check(D.ForSeat(1).TaserArmed&&D.ForSeat(2).NoruFaceActive==(phase==1),"owner activations and cooldown validated "+phase);
            yield return new WaitForSecondsRealtime(.15f);
            var parts=Field<Transform[][]>(manager.Table,"noruParts");
            if(phase==1)
            {
                Check(parts[2].Length==5&&parts[2][0].gameObject.activeInHierarchy,"remote body and four sleeves visible");
                foreach(var part in parts[2])Check(part.GetComponentsInChildren<Collider>(true).Length==0,"jacket has no hit colliders");
                CaptureJacket("NoruFace-Worn.png",2);
            }
            StartShot();while(!Seen("gunner","hit"+phase)||!Seen("victim","hit"+phase))yield return null;
            int target=phase==3?0:2;
            Check(D.Game.HealthForSeat(target)==(phase==2?95:100)&&D.ForSeat(target).Stunned==(phase==2),"actual projectile immunity or five-damage stun "+phase);
            if(phase==2){Check(D.ForSeat(2).ShotsFired==0,"non-shooter shock never fires held weapon");}
            Clock.Return(Now);D.Tick(Now);Publish();
            Check(!D.ForSeat(0).NoruFaceActive&&!D.ForSeat(2).NoruFaceActive&&!D.ForSeat(2).Stunned,"all turn effects clear immediately "+phase);
            if(phase==1)
            {
                yield return null; // The model follows the published state in LateUpdate.
                Check(D.ForSeat(0).HitAt<0&&D.ForSeat(0).NoruFaceReadyIteration==13&&!parts[0][0].gameObject.activeSelf,
                    "host never hit but still undresses and retains two-game cooldown: hit="+D.ForSeat(0).HitAt+", ready="+D.ForSeat(0).NoruFaceReadyIteration+", worn="+parts[0][0].gameObject.activeSelf);
            }
            while(!Seen("gunner","clear"+phase)||!Seen("victim","clear"+phase))yield return null;
        }
        Mark("finished");while(!Seen("gunner","finished")||!Seen("victim","finished"))yield return null;
    }
    IEnumerator Client()
    {
        for(int phase=1;phase<=3;phase++)
        {
            int iteration=phase==3?13:9+phase;
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation||player.State.Value.Clock.Iteration!=iteration)yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            var a=player.State.Value.ForSeat(player.Seat.Value);var buttons=Field<Button[]>(view,"itemButtons");
            Check(a.HasNoruFace&&Field<Button[]>(view,"loadoutButtons").Length==5,"five selectable items and jacket owned "+phase);
            if(role=="gunner")
            {
                player.UseItem(DuelItem.Taser);while(!player.State.Value.Seat1.TaserArmed)yield return null;
            }
            else if(phase==1)
            {
                Check(buttons[4].enabledSelf,"non-shooter can wear jacket");player.UseItem(DuelItem.NoruFace);player.UseItem(DuelItem.NoruFace);
                while(!player.State.Value.Seat2.NoruFaceActive)yield return null;yield return new WaitForSecondsRealtime(.12f);
                Check(player.State.Value.Seat2.NoruFaceReadyIteration==13&&buttons[4].ClassListContains("item-used")&&!buttons[4].enabledSelf,"yellow worn border and duplicate rejection");
            }
            else if(phase==2)
            {
                player.UseItem(DuelItem.NoruFace);yield return new WaitForSecondsRealtime(.15f);
                Check(!player.State.Value.Seat2.NoruFaceActive&&!buttons[4].enabledSelf&&Field<Label[]>(view,"itemStatuses")[4].text=="2게임 뒤 사용가능","cooldown remains disabled and displays games");
            }
            else Check(buttons[4].enabledSelf,"reusable after two following games");
            Mark("prep"+phase);while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
            int previous=(phase-1);int target=phase==3?0:2;
            if(role=="gunner")
            {
                var point=DuelGunplay.Point(target,DuelHitPart.Head);DuelGunplay.Angles(point-DuelGunplay.Eye(1),out float y,out float x);
                for(int i=0;i<4;i++)DuelGunplay.Angles(point-DuelGunplay.MuzzleOrigin(1,DuelWeapon.Taser,false,DuelGunplay.Direction(y,x)),out y,out x);
                var ray=DuelGunplay.Direction(y,x);Check(DuelGunplay.TraceFrom(1,player.State.Value.AliveMask,DuelGunplay.MuzzleOrigin(1,DuelWeapon.Taser,false,ray),ray,player.State.Value.Tabletop).Seat==target,"unoccluded free target "+phase);
                double sample=Now;DuelGunplay.Sway(1,DuelWeapon.Taser,false,sample,0,out float sy,out float sp);player.Fire(y-sy,x-sp,false,sample);
            }
            while(player.State.Value.Seat1.ShotsFired<=previous)yield return null;
            // A shot count is published at launch; wait for actual contact rather than assuming arrival.
            if(phase==2)while(!player.State.Value.Seat2.Stunned)yield return null;else yield return new WaitForSecondsRealtime(.6f);
            var hit=player.State.Value.ForSeat(target);
            Check(hit.Health==(phase==2?95:100)&&hit.Stunned==(phase==2),"confirmed target health and shock "+phase);
            if(role=="victim")
            {
                yield return new WaitForSecondsRealtime(.12f);
                Check((Field<VisualElement>(view,"electricOverlay").style.display.value==DisplayStyle.Flex)==(phase==2),"owner electrical overlay respects immunity "+phase);
            }
            Mark("hit"+phase);
            while(player.State.Value.Clock.Phase!=ClockPhase.Returning||player.State.Value.Seat2.Stunned||player.State.Value.Seat0.NoruFaceActive||player.State.Value.Seat2.NoruFaceActive)yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            Check(Field<VisualElement>(view,"electricOverlay").style.display.value==DisplayStyle.None,"electrical overlay ends with shooting "+phase);
            Check(!Field<Transform[][]>(manager.Table,"noruParts")[2][0].gameObject.activeSelf,"third-person jacket removed on turn end "+phase);
            Mark("clear"+phase);
        }
        while(!Seen("host","finished"))yield return null;Mark("finished");
    }
    void CaptureJacket(string name,int seat)
    {
        var go=new GameObject("QA jacket camera");var c=go.AddComponent<Camera>();c.CopyFrom(Camera.main);c.enabled=false;c.fieldOfView=45;c.aspect=1;
        var v=DuelGunplay.Point(seat,DuelHitPart.Body);var point=new Vector3(v.X,v.Y,v.Z);
        var inward=new Vector3(-point.x,0,-point.z).normalized;c.transform.position=point+inward*2.5f+Vector3.up*.7f;c.transform.LookAt(point+Vector3.up*.1f);
        var rt=new RenderTexture(700,700,24);rt.Create();RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(700,700,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,700,700),0,0);t.Apply();File.WriteAllBytes(Path.Combine(path,name),t.EncodeToPNG());RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(t);Destroy(go);
    }
}
