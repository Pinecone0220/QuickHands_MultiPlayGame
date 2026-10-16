using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class UnlimitedItemsRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;DuelDirector director;int checks;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--unlimited-items-qa");if(i<0)return;var q=new GameObject("UnlimitedItemsQA").AddComponent<UnlimitedItemsRuntimeQA>();q.role=args[i+1];q.path=Path.Combine(Application.dataPath,"..","unlimited-items-");q.deadline=Time.realtimeSinceStartupAsDouble+110;Application.targetFrameRate=30;}
    VisualElement E(string name)=>(VisualElement)typeof(RouletteView).GetField(name,F).GetValue(view);
    void Mark(string name)=>File.WriteAllText(path+role+"-"+name,"ok");
    bool Seen(string who,string name)=>File.Exists(path+who+"-"+name);
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL unlimited items "+role+": "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0){Debug.Log(message);File.WriteAllText(path+role+"-done",message);deadline=double.MaxValue;Application.Quit(code);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    double Now=>player.NetworkManager.ServerTime.Time;
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(role=="host"?1:2);
        manager=HelloWorldManager.Instance;
        if(role!="host")while(!Seen("host","configured"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17937");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        player=HelloWorldPlayer.Local;view=manager.GetComponent<RouletteView>();yield return null;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==38,"protocol 38 active");
        if(role=="host")
        {
            manager.ToggleDeveloperMode();director=(DuelDirector)typeof(HelloWorldManager).GetField("director",F).GetValue(manager);
            Check(!player.State.Value.UnlimitedItems,"new rooms default to limited items");
            ((Toggle)E("unlimitedItems")).value=true;((Toggle)E("startWithItems")).value=true;((Toggle)E("manualMiniGame")).value=true;
            Mark("configured");while(player.State.Value.ConnectedMask!=3||player.State.Value.ReadyMask!=2)yield return null;
            ((Toggle)E("unlimitedItems")).value=false;Check(!player.State.Value.UnlimitedItems&&player.State.Value.ReadyMask==2,"unlimited toggle off preserves remote readiness");
            ((Toggle)E("unlimitedItems")).value=true;((Toggle)E("startWithItems")).value=false;((Toggle)E("startWithItems")).value=true;
            ((Toggle)E("manualMiniGame")).value=false;((Toggle)E("manualMiniGame")).value=true;((IntegerField)E("shotSeconds")).value=12;
            Check(player.State.Value.UnlimitedItems&&player.State.Value.ReadyMask==2&&player.State.Value.ShotTimeLimit==12,"every room setting preserves readiness");Mark("changed");
            while(!Seen("client","checked"))yield return null;player.Rematch();
        }
        else
        {
            while(!player.State.Value.UnlimitedItems||!player.State.Value.StartWithItems||!player.State.Value.ManualMiniGame)yield return null;
            Check(E("roomSettings").style.display.value==DisplayStyle.None,"remote has no room controls");
            manager.SetRoomUnlimitedItems(false);yield return null;Check(player.State.Value.UnlimitedItems,"remote cannot change unlimited setting");
            player.Rematch();while(!Seen("host","changed")||player.State.Value.ShotTimeLimit!=12)yield return null;
            Check(player.State.Value.ReadyMask==2&&player.State.Value.UnlimitedItems,"preserved ready and setting replicated to remote");Mark("checked");
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        Check(player.State.Value.Seat0.HasRecovery&&player.State.Value.Seat0.HasTaser&&!player.State.Value.Seat1.HasRecovery&&!player.State.Value.Seat1.HasTaser,"unlimited does not change host-only grants");
        Mark("stock");
        for(int turn=1;turn<=2;turn++)
        {
            if(role=="host")
            {
                while(!Seen("client",turn==1?"stock":"return1"))yield return null;
                // Explicit server fixture grant tests the same owner RPC on a remote inventory.
                var actions=(DuelActionState[])typeof(DuelDirector).GetField("actions",F).GetValue(director);actions[1].HasRecovery=actions[1].HasTaser=true;
                manager.ChooseMiniGame(PartyMiniGame.Countdown);
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;
            var clock=player.State.Value.Clock;double stop=clock.StartedAt+clock.Target/100.0+(role=="host"?0:.3);
            while(Now<stop)yield return null;player.StopClock(Now);
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;
            if(role=="host")
            {director.Game.Shoot(0,1,DuelWeapon.SawedOff,DuelHitPart.Body);Publish();manager.SetRoomUnlimitedItems(false);Check(player.State.Value.UnlimitedItems,"room setting locked during active match");}
            else
            {
                while(player.State.Value.Seat1.Health!=25)yield return null;yield return null;
                var buttons=(Button[])typeof(RouletteView).GetField("itemButtons",F).GetValue(view);
                Check(buttons[0].enabledInHierarchy&&buttons[1].enabledInHierarchy,"remote can use retained inventory every mini");
                player.UseItem(DuelItem.Recovery);while(player.State.Value.Seat1.Health!=75)yield return null;
                Check(player.State.Value.Seat1.HasRecovery,"first recovery remains in remote inventory");
                player.UseItem(DuelItem.Recovery);while(player.State.Value.Seat1.Health!=100)yield return null;yield return null;
                Check(player.State.Value.Seat1.HasRecovery&&buttons[0].style.display.value==DisplayStyle.Flex&&!buttons[0].enabledInHierarchy,"recovery remains visible and disables only at full HP");
                player.UseItem(DuelItem.Taser);while(!player.State.Value.Seat1.TaserArmed)yield return null;yield return null;
                Check(player.State.Value.Seat1.TaserReadyIteration==0&&buttons[1].ClassListContains("item-used"),"taser has no cooldown and retains yellow selected state");Mark("used"+turn);
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;
            if(role=="host")
            {while(!Seen("client","used"+turn))yield return null;player.Fire(0,60,false,Now);}
            while(player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;
            Check(player.State.Value.Seat1.HasRecovery&&player.State.Value.Seat1.HasTaser&&!player.State.Value.Seat1.TaserArmed,"effects expire but inventory and unlimited setting remain");
            while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;Mark("return"+turn);
        }
        if(role=="host")while(!Seen("client","return2"))yield return null;
        Finish("PASS unlimited items "+role+": "+checks+" checks");
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL unlimited items "+role+": timeout",1);}
}
