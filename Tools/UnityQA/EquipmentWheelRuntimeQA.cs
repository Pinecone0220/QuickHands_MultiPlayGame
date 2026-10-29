using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class EquipmentWheelRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;Mouse mouse;Keyboard keyboard;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    void Mark(string name)=>File.WriteAllText(Path.Combine(path,role+"-"+name),"ok");
    bool Seen(string who,string name)=>File.Exists(Path.Combine(path,who+"-"+name));
    void Check(bool ok,string name){checks++;if(!ok){Done("FAIL wheel "+role+": "+name,1);throw new Exception(name);}}
    void Done(string message,int code=0){Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;Application.Quit(code);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Done("FAIL wheel timeout "+role,1);}
    IEnumerator Wheel(float y)
    {
        mouse.MakeCurrent();InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,y));
        yield return null;yield return null;
        InputSystem.QueueDeltaStateEvent(mouse.scroll,Vector2.zero);yield return null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--equipment-wheel-qa");if(i<0)return;
        var q=new GameObject("Equipment wheel QA").AddComponent<EquipmentWheelRuntimeQA>();q.role=args[i+1];q.path=args[Array.IndexOf(args,"--equipment-wheel-dir")+1];
        Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+100;Application.targetFrameRate=60;Application.runInBackground=true;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1","18983");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        if(role=="client")
        {
            Mark("connected");while(!Seen("host","ink"))yield return null;
            Check(player.State.Value.Seat0.QuickEquipped&&player.State.Value.Seat0.Ink.Armed,"remote observer sees wheel-selected ink");
            Mark("ink-seen");
            while(!Seen("host","finished"))yield return null;manager.Leave();Done("PASS wheel client: "+checks+" checks");yield break;
        }
        Mark("listening");while(!Seen("client","connected")||player.State.Value.ConnectedMask!=3)yield return null;
        var d=Field<DuelDirector>(manager,"director");d.Join(2,player.NetworkManager.ServerTime.Time);
        typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
        manager.ToggleDeveloperMode();manager.SetRoomAutoSelectItems(true);player.Rematch();
        while(!player.State.Value.IsPracticing(0))yield return null;yield return null;yield return null;
        mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();mouse.MakeCurrent();keyboard.MakeCurrent();
        yield return Wheel(-120);while(player.State.Value.Seat0.Weapon!=DuelWeapon.Taser)yield return null;
        Check(player.State.Value.Seat0.QuickEquipped,"wheel equips taser without Alt");
        yield return Wheel(-120);while(!player.State.Value.Seat0.Ink.Armed)yield return null;
        Check(player.State.Value.Seat0.QuickEquipped&&player.State.Value.Seat0.QuickItem==3,"wheel cycles taser to ink");Mark("ink");
        while(!Seen("client","ink-seen"))yield return null;
        var chat=manager.GetComponent<DuelChat>();typeof(DuelChat).GetMethod("Open",F).Invoke(chat,null);yield return null;
        Check(DuelChat.BlocksGameplay,"chat active");yield return Wheel(-120);
        Check(player.State.Value.Seat0.Ink.Armed,"chat scrolling does not change equipment");
        typeof(DuelChat).GetMethod("Close",F).Invoke(chat,null);yield return null;yield return null;
        keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftAlt));yield return null;yield return null;
        Check(DuelPointer.AltHeld,"Alt input active");yield return Wheel(-120);
        Check(player.State.Value.Seat0.Ink.Armed,"UI-mode scroll does not change equipment");
        keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        yield return Wheel(-120);while(player.State.Value.Seat0.QuickEquipped)yield return null;
        Check(player.State.Value.Seat0.Weapon==DuelWeapon.Revolver,"wheel returns to gun");
        yield return Wheel(120);while(!player.State.Value.Seat0.Ink.Armed)yield return null;
        Check(player.State.Value.Seat0.QuickItem==3,"reverse wheel goes to ink");
        Mark("finished");yield return new WaitForSecondsRealtime(.2f);manager.Leave();Done("PASS wheel host: "+checks+" checks");
    }
}
