using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

// Install only in an isolated QA project. Real owner RPCs and replicated snapshots.
public sealed class BulletItemsRuntimeQA:MonoBehaviour
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden).GetValue(value);
    DuelDirector D=>Field<DuelDirector>(manager,"director");
    DuelClockContest Clock=>Field<DuelClockContest>(D,"clock");
    double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--bullet-qa");if(index<0)return;
        var q=new GameObject("Bullet item QA").AddComponent<BulletItemsRuntimeQA>();q.role=args[index+1];q.path=args[Array.IndexOf(args,"--bullet-dir")+1];
        Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=60;
    }
    void Done(string message,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),message);Debug.Log(message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string reason){checks++;if(!ok){Done("FAIL bullet "+role+": "+reason,1);throw new Exception(reason);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL bullet timeout "+role,1);}
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");
    bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",Hidden).Invoke(manager,null);
    void Prep(int first)
    {
        typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Clock,new ClockState{Phase=ClockPhase.ItemPreparation,PhaseAt=Now,StartedAt=Now+30,Iteration=D.Clock.Iteration+1,First=first,MiniGame=PartyMiniGame.Countdown,ParticipantsMask=D.Game.AliveMask});Publish();
    }
    void StartShooting(){var state=D.Clock;state.StartedAt=Now+.05;typeof(DuelClockContest).GetProperty("State").SetValue(Clock,state);Publish();}
    void Model()
    {
        Check(DuelLanRooms.Protocol==54,"network compatibility version bumped");
        var s=DuelSnapshot.From(D);for(int i=0;i<6;i++)
        {
            var a=s.ForSeat(i);a.BulletItems=i%3;a.BulletUsed=i%2==1;
            switch(i){case 0:s.Seat0=a;break;case 1:s.Seat1=a;break;case 2:s.Seat2=a;break;case 3:s.Seat3=a;break;case 4:s.Seat4=a;break;case 5:s.Seat5=a;break;}
        }
        using(var writer=new FastBufferWriter(16384,Allocator.Temp))
        {writer.WriteNetworkSerializable(s);using(var reader=new FastBufferReader(writer,Allocator.Temp)){reader.ReadNetworkSerializable(out DuelSnapshot copy);Check(copy.Equals(s)&&copy.Seat5.BulletItems==2&&copy.Seat5.BulletUsed,"six inventories and activation flags serialize atomically");}}
        var icon=DuelItemArt.Icon(DuelItem.Bullet);int opaque=0;foreach(var pixel in icon.GetPixels())if(pixel.a>.9f)opaque++;
        Check(icon.name=="총알"&&opaque>2000&&opaque<10000,"two-cartridge thumbnail and Korean name");File.WriteAllBytes(Path.Combine(path,"Bullet-Icon.png"),icon.EncodeToPNG());Destroy(icon);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role!="host"){while(!Seen("host","listening"))yield return null;if(role=="observer")while(!Seen("shooter","joined"))yield return null;}
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","18992");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        Mark("joined");Check(player.Seat.Value==(role=="host"?0:role=="shooter"?1:2),"expected owned seat");
        if(role=="host")yield return Host();else yield return Client();
        Done("PASS bullet "+role+": "+checks+" checks");
    }
    IEnumerator Host()
    {
        Model();Mark("listening");while(D.Game.ConnectedMask!=7)yield return null;
        D.Game.SetShotTime(0);D.SetAutoSelectItems(true);for(int i=0;i<3;i++)D.Ready(i,Now);Prep(1);
        while(!D.ForSeat(1).BulletUsed||!Seen("shooter","activated")||!Seen("observer","activated"))yield return null;
        Check(D.ForSeat(1).BulletItems==1&&D.ForSeat(0).BulletItems==2&&D.ForSeat(2).BulletItems==2,"owner spam consumes exactly one, non-shooter has no effect");
        StartShooting();while(D.Clock.Phase!=ClockPhase.Returning)yield return null;
        Check(D.ForSeat(1).ShotsFired==2&&D.ForSeat(1).BulletItems==1,"remote shooter gets exactly two opportunities");
        while(!Seen("shooter","two-shots")||!Seen("observer","two-shots"))yield return null;
        typeof(DuelDirector).GetMethod("BeginPartySelection",Hidden).Invoke(D,new object[]{Now});Prep(0);
        Check(D.ForSeat(1).BulletItems==1&&!D.ForSeat(1).BulletUsed,"next minigame keeps remote stock and resets activation");
        Check(D.UseItem(0,DuelItem.Bullet,D.Clock.Iteration,Now),"host can activate its own bullet");Publish();Mark("host-prep");
        while(!Seen("shooter","host-prep")||!Seen("observer","host-prep"))yield return null;
        StartShooting();while(D.Clock.Phase!=ClockPhase.Shooting)yield return null;
        Check(D.Fire(0,0,55,true,D.ForSeat(0).Version,Now,Now),"host first shot");Publish();
        while(D.ForSeat(0).Ammo==0)yield return null;
        Check(D.Fire(0,0,55,true,D.ForSeat(0).Version,Now,Now),"host extra shot");Publish();
        while(D.Clock.Phase!=ClockPhase.Returning)yield return null;
        Check(D.ForSeat(0).ShotsFired==2&&D.ForSeat(0).BulletItems==1,"host also gets two shots at one charge");Publish();Mark("finished");
        while(!Seen("shooter","finished")||!Seen("observer","finished"))yield return null;
    }
    IEnumerator Client()
    {
        while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation||player.State.Value.Clock.First!=1)yield return null;yield return new WaitForSecondsRealtime(.15f);
        var buttons=Field<Button[]>(view,"itemButtons");var labels=Field<Label[]>(view,"itemStatuses");
        Check(player.State.Value.ForSeat(player.Seat.Value).BulletItems==2&&Field<Button[]>(view,"loadoutButtons").Length==5,"five choices and initial two-pack replicate");
        Check(buttons[3].style.display.value==DisplayStyle.Flex&&buttons[3].enabledSelf==(role=="shooter"),"only designated shooter can click item");
        player.UseItem(DuelItem.Bullet);player.UseItem(DuelItem.Bullet);player.UseItem(DuelItem.Bullet);
        while(!player.State.Value.Seat1.BulletUsed)yield return null;yield return new WaitForSecondsRealtime(.15f);
        Check(player.State.Value.Seat1.BulletItems==1&&player.State.Value.Seat2.BulletItems==2,"quantity and activation agree across clients");
        if(role=="shooter")Check(buttons[3].ClassListContains("item-used")&&!buttons[3].enabledSelf&&labels[3].text=="1개","activated border, remaining quantity and duplicate lock visible");
        Mark("activated");while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
        if(role=="shooter")
        {
            player.Fire(60,55,true,Now);
            while(player.State.Value.Seat1.ShotsFired!=1||player.State.Value.Seat1.Ammo!=1)yield return null;
            Check(player.State.Value.Clock.Phase==ClockPhase.Shooting,"first miss keeps shared shooting turn alive");
            player.Fire(60,55,true,Now);
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;
        Check(player.State.Value.Seat1.ShotsFired==2,"all peers observe two shots and end of turn");
        if(role=="shooter"){player.Fire(60,55,true,Now);yield return new WaitForSecondsRealtime(.15f);Check(player.State.Value.Seat1.ShotsFired==2,"third owner request rejected");}
        Mark("two-shots");while(!Seen("host","host-prep")||player.State.Value.Clock.First!=0||!player.State.Value.Seat0.BulletUsed)yield return null;
        Check(player.State.Value.Seat0.BulletItems==1&&player.State.Value.Seat1.BulletItems==1&&!player.State.Value.Seat1.BulletUsed,"new shooter and preserved stock replicate");Mark("host-prep");
        while(!Seen("host","finished")||player.State.Value.Seat0.ShotsFired!=2||player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;
        Check(player.State.Value.Clock.Phase==ClockPhase.Returning&&player.State.Value.Seat0.BulletItems==1,"host's extra shot replicates to all clients");Mark("finished");
    }
}
