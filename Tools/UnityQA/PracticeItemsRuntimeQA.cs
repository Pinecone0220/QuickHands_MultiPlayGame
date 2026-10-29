using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PracticeItemsRuntimeQA : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    void Mark(string n)=>File.WriteAllText(Path.Combine(path,role+"-"+n),"ok");
    bool Seen(string r,string n)=>File.Exists(Path.Combine(path,r+"-"+n));
    void Check(bool ok,string label){checks++;if(!ok){Done("FAIL practice "+role+": "+label,1);throw new Exception(label);}}
    void Done(string message,int code=0){Debug.Log(message);Mark("done");File.WriteAllText(Path.Combine(path,role+"-result"),message);deadline=double.MaxValue;Application.Quit(code);}
    void Click(Button b)=>typeof(Clickable).GetMethod("SimulateSingleClick",F).Invoke(b.clickable,new object[]{null,0});
    double Now=>player.NetworkManager.ServerTime.Time;
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL practice timeout "+role,1);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--practice-items-qa");if(i<0)return;
        var q=new GameObject("Practice items QA").AddComponent<PracticeItemsRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--practice-items-dir")+1];
        Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=60;Application.runInBackground=true;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();
        if(role!="host")while(!Seen(role=="guest"?"host":"guest","connected"))yield return null;
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","18981");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        Mark("connected");Check(player.Seat.Value==(role=="host"?0:role=="guest"?1:2),"seat assigned");
        if(role=="host")
        {
            while(player.State.Value.ConnectedMask!=7)yield return null;
            manager.ToggleDeveloperMode();manager.SetRoomAutoSelectItems(true);player.SelectWeapon(DuelWeapon.BAS);
            while(player.State.Value.Seat0.SelectedWeapon!=DuelWeapon.BAS)yield return null;player.Rematch();Mark("options");
        }
        else if(role=="guest"){while(!Seen("host","options")||player.State.Value.ForSeat(1).SelectedItems!=DuelLoadout.All)yield return null;player.Rematch();}
        if(role=="standby")
        {
            while(!Seen("guest","shocked"))yield return null;
            Check(player.State.Value.ForSeat(1).Stunned,"remote shock replicated");
            var arcs=Field<LineRenderer[,]>(manager.Table,"stunArcs");Check(arcs[1,0]!=null&&arcs[1,0].enabled,"third party sees shock arcs");
            Mark("observed");while(!Seen("guest","inked"))yield return null;yield return null;
            Check(player.State.Value.ForSeat(1).Ink.InkedAt>=0,"ink impact replicated to observer");
            var held=Field<Transform[]>(manager.GetComponent<DuelInkVisuals>(),"held");
            Check(player.State.Value.Seat0.QuickEquipped&&player.State.Value.Seat0.Ink.Armed&&held[0].gameObject.activeInHierarchy,"observer sees pouch held after throw");
            Mark("ink-observed");while(!Seen("host","finished"))yield return null;manager.Leave();Done("PASS practice standby: "+checks+" checks");yield break;
        }
        while(player.State.Value.PracticeMask!=3)yield return null;
        var buttons=Field<Button[]>(view,"itemButtons");var row=Field<VisualElement>(view,"itemRow");yield return null;
        Check(row.style.display.value!=DisplayStyle.None&&row.ClassListContains("practice-items"),"practice inventory visible");
        for(int i=0;i<buttons.Length;i++)Check(buttons[i].enabledInHierarchy&&buttons[i].style.display.value!=DisplayStyle.None,"selected item shown "+i);
        if(role=="guest")
        {
            Click(buttons[(int)DuelItem.NoruFace]);while(!player.State.Value.ForSeat(1).NoruFaceActive)yield return null;Mark("padded");
            while(!Seen("host","blocked"))yield return null;Check(!player.State.Value.ForSeat(1).Stunned&&player.State.Value.ForSeat(1).Health==100,"padding prevents shock");
            Click(buttons[(int)DuelItem.NoruFace]);while(player.State.Value.ForSeat(1).NoruFaceActive)yield return null;Mark("unpadded");
            while(!player.State.Value.ForSeat(1).IsStunned(Now))yield return null;yield return null;
            Check(Field<VisualElement>(view,"electricOverlay").style.display.value!=DisplayStyle.None,"owner sees shock overlay");Mark("shocked");
            while(!Seen("standby","observed"))yield return null;
            while(player.State.Value.ForSeat(1).IsStunned(Now))yield return null;Mark("recovered");
            while(player.State.Value.ForSeat(1).Ink.InkedAt<0)yield return null;yield return null;yield return null;
            Check(Field<VisualElement>(view,"inkOverlay").style.display.value!=DisplayStyle.None,"ink obscures local view");
            Check(player.State.Value.ForSeat(1).Health==100&&player.State.Value.ForSeat(1).LivesRemaining==3,"practice causes no game health or life loss");Mark("inked");
            while(!Seen("host","finished"))yield return null;manager.Leave();Done("PASS practice guest: "+checks+" checks");yield break;
        }
        RoomCameraCaptureQA.Setup(view);yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Practice-Items.png"));
        var input=Field<VisualElement>(view,"inputPanel");var digits=Field<VisualElement>(view,"digits");
        player.Fire(0,0,false,Now);while(player.State.Value.Seat0.Action!=DuelAction.OpenBarrel)yield return null;yield return null;
        Check(input.style.display.value!=DisplayStyle.None&&digits.style.display.value!=DisplayStyle.None,"rifle reload input appears in lobby");
        var reload=player.State.Value.Seat0;player.InputDigit(reload.Digit(0),reload.Version);
        while(player.State.Value.Seat0.Progress<1)yield return null;int reloadCode=player.State.Value.Seat0.Code;
        int shotsBeforeTaser=player.State.Value.Seat0.PracticeShotsFired;
        Click(buttons[(int)DuelItem.Recovery]);while(player.State.Value.Seat0.HealAt<0)yield return null;
        Check(player.State.Value.Seat0.HasRecovery,"practice ampoule remains available");
        Click(buttons[(int)DuelItem.Bullet]);while(!player.State.Value.Seat0.BulletUsed)yield return null;
        Check(player.State.Value.Seat0.BulletItems==2,"practice bullet is not consumed");
        Click(buttons[(int)DuelItem.Taser]);while(player.State.Value.Seat0.Weapon!=DuelWeapon.Taser)yield return null;yield return null;
        Check(buttons[(int)DuelItem.Taser].ClassListContains("item-used"),"equipped item highlighted");
        Check(input.style.display.value==DisplayStyle.None&&digits.style.display.value==DisplayStyle.None,"rifle reload input hidden while taser equipped in lobby");
        while(!Seen("guest","padded"))yield return null;yield return new WaitForSecondsRealtime(.5f);
        DuelGunplay.Angles(DuelGunplay.Point(1,DuelHitPart.Head)-DuelGunplay.Eye(0),out float yaw,out float pitch);
        var current=player.State.Value.Seat0;player.Fire(yaw,pitch,false,Now);while(player.State.Value.Seat0.PracticeShotsFired<shotsBeforeTaser+1)yield return null;
        Check(!player.State.Value.Seat1.Stunned,"padded target resists taser");Mark("blocked");
        while(!Seen("guest","unpadded"))yield return null;yield return new WaitForSecondsRealtime(.25f);
        player.Fire(yaw,pitch,false,Now);while(player.State.Value.Seat0.PracticeShotsFired<shotsBeforeTaser+2)yield return null;
        Check(player.State.Value.Seat1.Stunned,"practice taser hits without delay");
        while(!Seen("guest","recovered"))yield return null;
        Click(buttons[(int)DuelItem.Ink]);while(!player.State.Value.Seat0.Ink.Armed)yield return null;yield return null;
        Check(input.style.display.value==DisplayStyle.None&&digits.style.display.value==DisplayStyle.None,"rifle reload input hidden while ink equipped in lobby");
        var d=Field<DuelDirector>(manager,"director");var args=new object[]{0,1,0f,0f};typeof(DuelDirector).GetMethod("AimAiInk",F).Invoke(d,args);
        player.ThrowInk((float)args[2],(float)args[3],Now);while(player.State.Value.Seat1.Ink.InkedAt<0)yield return null;
        Check(player.State.Value.Seat0.Ink.Owned&&player.State.Value.Seat0.Ink.Armed&&player.State.Value.Seat0.QuickEquipped,"practice ink stays equipped and available");
        yield return null;Check(buttons[(int)DuelItem.Ink].ClassListContains("item-used"),"held pouch remains highlighted in inventory");
        while(!Seen("standby","ink-observed"))yield return null;
        while(Now-player.State.Value.Seat0.Ink.ThrownAt<DuelInk.ThrowDuration)yield return null;
        player.ThrowInk((float)args[2],(float)args[3],Now);while(player.State.Value.Seat0.Ink.Serial<2)yield return null;
        Check(player.State.Value.Seat0.QuickEquipped&&player.State.Value.Seat0.Ink.Armed,"repeat throw keeps pouch equipped");
        player.SwitchQuickItem(false);while(player.State.Value.Seat0.QuickEquipped)yield return null;
        Check(player.State.Value.Seat0.Weapon==DuelWeapon.BAS,"player can switch back to the gun");
        while(DuelInk.Busy(player.State.Value.Seat0.Ink,Now))yield return null;yield return null;
        Check(input.style.display.value!=DisplayStyle.None&&digits.style.display.value!=DisplayStyle.None,"rifle reload input reappears after returning to gun");
        Check(player.State.Value.Seat0.Code==reloadCode&&player.State.Value.Seat0.Progress==1,"lobby reload progress is preserved");
        int previousSession=player.State.Value.Seat0.PracticeSession;
        player.Rematch();while(player.State.Value.IsPracticing(0))yield return null;yield return null;
        Check(row.style.display.value==DisplayStyle.None,"cancel readiness hides practice inventory");
        player.Rematch();while(!player.State.Value.IsPracticing(0))yield return null;
        var state=player.State.Value;
        manager.RequestItem(player.OwnerClientId,DuelItem.Recovery,state.Round,state.Clock.Iteration,previousSession);
        Check(player.State.Value.Seat0.HealAt<0,"old practice session item request rejected");
        Mark("finished");yield return new WaitForSecondsRealtime(.3f);manager.Leave();Done("PASS practice host: "+checks+" checks");
    }
}
