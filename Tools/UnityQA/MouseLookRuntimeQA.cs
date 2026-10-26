using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Cursor=UnityEngine.Cursor;

public sealed class MouseLookRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    HelloWorldManager manager;HelloWorldPlayer player;DuelDirector director;DuelFirstPerson fps;RouletteView view;
    int checks;double deadline;
    double Now=>player.NetworkManager.ServerTime.Time;
    T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,F).GetValue(obj);
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--mouse-look-qa")<0)return;
        new GameObject("Mouse look QA").AddComponent<MouseLookRuntimeQA>();Application.targetFrameRate=30;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    }
    void Finish(string message,int code=0)
    {
        Debug.Log(message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool value,string why){checks++;if(!value){Finish("FAIL mouse look: "+why,1);throw new Exception(why);}}
    IEnumerator MouseInput(float dx=0,float dy=0,bool left=false,bool right=false,bool release=true)
    {
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        var state=new MouseState{position=new Vector2(1080,550),delta=new Vector2(dx,dy)};
        if(left)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
        if(right)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right);
        InputSystem.QueueStateEvent(Mouse.current,state);yield return null;yield return null;
        if(release){InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(1080,550)});yield return null;yield return null;}
    }
    IEnumerator Space()
    {
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Space));yield return null;yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;
    }
    IEnumerator Look(string phase,bool expected=true)
    {
        float yaw=fps.Yaw,pitch=fps.Pitch;Quaternion camera=Camera.main.transform.rotation;
        yield return MouseInput(70,20);
        Check((Mathf.Abs(Mathf.DeltaAngle(yaw,fps.Yaw))>1)==expected,phase+" yaw policy");
        Check((Mathf.Abs(pitch-fps.Pitch)>.5f)==expected,phase+" pitch policy");
        Check((Quaternion.Angle(camera,Camera.main.transform.rotation)>1)==expected,phase+" rendered camera policy");
        if(expected&&!fps.Active)Check(Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.z,0))<.05f,phase+" preserves a level horizon");
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+150;yield return new WaitForSecondsRealtime(1);
        manager=HelloWorldManager.Instance;fps=manager.GetComponent<DuelFirstPerson>();view=manager.GetComponent<RouletteView>();
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","17955");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;
        director=Field<DuelDirector>(manager,"director");yield return null;yield return null;
        yield return Look("lobby");Check(!fps.Active&&Cursor.lockState==CursorLockMode.None,"lobby keeps selection UI cursor without enabling combat input");
        var eye=DuelGunplay.Eye(player.Seat.Value);Check(Vector3.Distance(Camera.main.transform.position,new Vector3(eye.X,eye.Y,eye.Z))<.001f,"lobby uses gameplay first-person eye position");
        var chat=manager.GetComponent<DuelChat>();chat.Open();yield return null;yield return Look("chat",false);chat.Close();yield return null;yield return null;
        manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);manager.SetRoomShotTime(1);
        director.Join(1,Now);director.SelectItem(1,DuelItem.Recovery);director.SelectItem(1,DuelItem.Taser);director.Ready(1,Now);Publish();
        player.SelectLoadoutItem(DuelItem.Recovery);player.SelectLoadoutItem(DuelItem.Taser);
        while(player.State.Value.Seat0.SelectedItems!=3)yield return null;player.Rematch();
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;yield return null;
        yield return Look("manual selection");Check(Cursor.lockState==CursorLockMode.None,"manual choice keeps clickable UI cursor");
        foreach(var game in new[]{PartyMiniGame.Countdown,PartyMiniGame.Signal,PartyMiniGame.Greed})
        {
            manager.ChooseMiniGame(game);while(player.State.Value.Clock.Phase!=ClockPhase.Counting||Now<player.State.Value.Clock.StartedAt+.05)yield return null;
            yield return Look(game+" counting");
            var hint=Field<Label>(view,"clockStop");Check(hint.pickingMode==PickingMode.Ignore&&hint.text.Contains("Space")&&!hint.text.Contains("클릭"),game+" uses non-clickable Space hint");
            yield return MouseInput(left:true);
            using(var click=ClickEvent.GetPooled())hint.SendEvent(click);
            yield return null;
            Check(!Field<bool>(view,"clockStopped")&&director.SubmittedEntry(0).Centiseconds<0,game+" scene and hint clicks cannot confirm");
            if(game!=PartyMiniGame.Signal){Check(director.StopClock(1,director.Clock.Iteration,Now,Now),"partner submits");Publish();}
            yield return Space();Check(Field<bool>(view,"clockStopped"),game+" Space confirms");
            if(game==PartyMiniGame.Signal)
            {
                var contest=Field<DuelClockContest>(director,"clock");while(Now<contest.ReactionGoAt+.05)yield return null;
                Check(director.StopClock(1,director.Clock.Iteration,Now,Now),"partner reacts to actual green");Publish();
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Ranking)yield return null;yield return Look(game+" results");
            while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;yield return Look(game+" item turn",false);
            Check(Cursor.lockState==CursorLockMode.None,"item turn releases UI cursor");
            while(player.State.Value.Clock.Phase!=ClockPhase.Shooting)yield return null;yield return null;
            if(game==PartyMiniGame.Countdown)
            {
                Check(player.State.Value.Clock.First==0,"local owner receives shooting chance");
                yield return MouseInput(right:true,release:false);Check(fps.Ads,"right mouse still aims");
                yield return MouseInput(left:true,right:true);
                Check(player.State.Value.Shots==1&&player.State.Value.Seat0.Ammo==0,"left mouse still fires actual shooting turn");
            }
            while(player.State.Value.Clock.Phase!=ClockPhase.Returning)yield return null;yield return Look(game+" return");
            while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        }
        director.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",F).Invoke(director,new object[]{DuelStage.AfterShot,Now});Publish();
        while(player.State.Value.Stage!=DuelStage.Idle)yield return null;yield return null;yield return Look("final results",false);
        Check(!fps.Active&&Cursor.lockState==CursorLockMode.None,"final result keeps third-person presentation and UI cursor");
        Finish("PASS mouse look: "+checks+" checks");
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL mouse look timeout after "+checks+" checks; "+player?.State.Value.Clock.Phase,1);}
}
