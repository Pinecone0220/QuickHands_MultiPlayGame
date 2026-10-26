using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Isolated-project AI host, actual numeric actions/projectiles and published owner state.
public sealed class AiEquipmentRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--ai-equipment-qa");if(i<0)return;
        var q=new GameObject("AI equipment QA").AddComponent<AiEquipmentRuntimeQA>();q.path=args[i+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=60;
    }
    void Done(string text,int code=0)
    {
        Debug.Log(text);File.WriteAllText(Path.Combine(path,"done"),text);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool value,string why){checks++;if(!value){Done("FAIL AI equipment: "+why,1);throw new Exception(why);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL AI equipment timeout",1);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;manager.AiCount=5;manager.Difficulty=AiDifficulty.Hard;
        manager.Begin(DuelMode.AI,false,"127.0.0.1","18995");while(HelloWorldPlayer.Local==null||player==null){player=HelloWorldPlayer.Local;yield return null;}
        while(D.Game.ConnectedMask!=63)yield return null;yield return new WaitForSecondsRealtime(.2f);
        var guns=new HashSet<DuelWeapon>();var pairs=new HashSet<int>();var before=new DuelActionState[6];int union=0;
        for(int bot=1;bot<6;bot++)
        {
            before[bot]=player.State.Value.ForSeat(bot);var a=before[bot];guns.Add(a.SelectedWeapon);pairs.Add(a.SelectedItems);union|=a.SelectedItems;
            Check(DuelLoadout.Count(a.SelectedItems)==2&&RouletteRules.Has(player.State.Value.ReadyMask,bot),"published bot has two chosen items and auto-ready "+bot);
        }
        Check(guns.Count==3&&pairs.Count==5&&union==DuelLoadout.All,"lobby publishes all three guns and all item kinds across five bots");Capture();
        D.SelectItem(0,DuelItem.Recovery);D.SelectItem(0,DuelItem.Taser);D.Game.SetShotTime(0);D.Ready(0,Now);
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});
        var clock=Field<DuelClockContest>(D,"clock");typeof(DuelClockContest).GetProperty("State").SetValue(clock,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,Iteration=1,PhaseAt=Now,StartedAt=Now+5,ParticipantsMask=63,First=-1});Publish();
        while(Now-D.Clock.PhaseAt<1||D.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;
        Check(D.ForSeat(2).QuickItem==2&&D.ForSeat(3).QuickItem==3&&D.ForSeat(5).NoruFaceActive,"AI chooses taser, ink and protective jacket in prep");
        Check(D.ForSeat(1).QuickItem==0&&!D.ForSeat(1).BulletUsed,"healthy recovery/bullet bot does not waste items in quick hands");
        while(D.Clock.Phase!=ClockPhase.QuickHands)yield return null;double started=Now;bool taser=false,ink=false,gun=false;
        while(Now-started<28&&!(taser&&ink&&gun))
        {
            for(int bot=1;bot<6;bot++)
            {
                var a=D.ForSeat(bot);if(a.ShotsFired>0){if(a.FiredWeapon==DuelWeapon.Taser)taser=true;else gun=true;}if(a.Ink.Serial>0)ink=true;
            }
            yield return null;
        }
        Check(taser,"actual equipped AI taser fires");Check(ink,"actual AI ink pouch is thrown");Check(gun,"AI completes numeric preparation and fires chosen gun");
        yield return new WaitForSecondsRealtime(.15f);
        Check(D.ForSeat(3).Ink.Serial==1&&!D.ForSeat(3).Ink.Owned,"ink is consumed once");
        Check(player.State.Value.Seat3.Ink.Serial==1&&player.State.Value.Seat2.ShotsFired>0,"owner snapshot receives bot item usage and fire");
        Check(D.ForSeat(2).Weapon==D.ForSeat(2).SelectedWeapon&&!D.ForSeat(2).QuickEquipped,"taser bot restores selected weapon after use");
        int round=D.Game.Round;D.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Idle,Now});
        for(int bot=1;bot<6;bot++)D.Ready(bot,Now);D.Ready(0,Now);Publish();yield return new WaitForSecondsRealtime(.15f);
        Check(D.Game.Round==round+1,"final rematch starts with existing ready policy");
        for(int bot=1;bot<6;bot++)
        {
            var a=player.State.Value.ForSeat(bot);Check(a.SelectedWeapon!=before[bot].SelectedWeapon&&a.SelectedItems!=before[bot].SelectedItems,"rematch rotates gun and item pair "+bot);
        }
        Check(player.State.Value.Seat0.SelectedItems==DuelLoadout.Default,"human item choices preserved");
        Done("PASS AI equipment runtime: "+checks+" checks");
    }
    void Capture()
    {
        typeof(RouletteTable).GetMethod("ApplyLocalVisibility",Hidden).Invoke(manager.Table,new object[]{0,false,false});
        var go=new GameObject("QA AI lobby overview");var c=go.AddComponent<Camera>();c.CopyFrom(Camera.main);c.enabled=false;c.fieldOfView=56;c.aspect=1.5f;
        c.transform.position=new Vector3(0,5.6f,-8.4f);c.transform.LookAt(new Vector3(0,1,0));
        var rt=new RenderTexture(1200,800,24);rt.Create();RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1200,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,800),0,0);t.Apply();File.WriteAllBytes(Path.Combine(path,"AI-Loadouts.png"),t.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(t);Destroy(go);
    }
}
