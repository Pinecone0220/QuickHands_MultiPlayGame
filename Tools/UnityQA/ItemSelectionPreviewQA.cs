using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
public sealed class ItemSelectionPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    HelloWorldManager manager;HelloWorldPlayer player;RouletteView view;double deadline;int checks;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--item-selection-preview")<0)return;var q=new GameObject("ItemSelectionPreviewQA").AddComponent<ItemSelectionPreviewQA>();q.deadline=Time.realtimeSinceStartupAsDouble+80;Application.targetFrameRate=30;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;}
    T V<T>(string name)=>(T)typeof(RouletteView).GetField(name,F).GetValue(view);
    void Check(bool value,string label){checks++;if(!value){Debug.LogError("FAIL selected-item preview: "+label);Application.Quit(1);throw new Exception(label);}}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);manager=HelloWorldManager.Instance;manager.Begin(DuelMode.AI,false,"127.0.0.1","17936");
        while(HelloWorldPlayer.Local==null||!HelloWorldPlayer.Local.IsSpawned||HelloWorldPlayer.Local.Seat.Value<0)yield return null;player=HelloWorldPlayer.Local;view=manager.GetComponent<RouletteView>();if(!manager.DeveloperMode)manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);manager.SetRoomStartWithItems(true);yield return null;player.Rematch();
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;manager.ChooseMiniGame(PartyMiniGame.Countdown);
        while(player.State.Value.Clock.Phase!=ClockPhase.Counting)yield return null;var c=player.State.Value.Clock;
        while(player.NetworkManager.ServerTime.Time<c.StartedAt+c.Target/100.0+1.2)yield return null;player.StopClock(player.NetworkManager.ServerTime.Time);
        while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;yield return null;
        Check(player.State.Value.Clock.First!=0&&player.State.Value.Seat0.HasTaser&&!player.State.Value.Seat1.HasTaser,"host alone owns inventory and may use taser as non-winner");
        var buttons=V<Button[]>("itemButtons");Check(!buttons[0].enabledInHierarchy&&buttons[1].enabledInHierarchy,"full-health amp disabled while taser selectable");
        yield return new WaitForSecondsRealtime(.1f);if(Mouse.current==null)InputSystem.AddDevice<Mouse>();var p=buttons[1].worldBound.center;var point=new Vector2(p.x,Screen.height-p.y);
        InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point,buttons=1});yield return new WaitForSecondsRealtime(.1f);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=point,buttons=0});
        while(!player.State.Value.Seat0.TaserArmed)yield return null;yield return null;yield return null;
        var color=buttons[1].resolvedStyle.borderTopColor;Check(color.r>.9f&&color.g>.7f&&color.b<.4f&&buttons[1].resolvedStyle.opacity>.9f,"armed taser has bright yellow border and full opacity");
        Check(buttons[0].resolvedStyle.opacity<.5f&&buttons[1].Query<Label>().ToList().Count==1,"disabled amp remains grey; item card contains only name and image");
        yield return Capture();Debug.Log("PASS selected-item preview: "+checks+" host-only/yellow-border/disabled-amp/name-only checks");deadline=double.MaxValue;Application.Quit(0);
    }
    IEnumerator Capture()
    {
        var doc=FindFirstObjectByType<UIDocument>();const int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();doc.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;doc.panelSettings.scale=1;doc.panelSettings.targetTexture=target;doc.panelSettings.clearColor=true;doc.panelSettings.colorClearValue=Color.clear;doc.rootVisualElement.style.width=w;doc.rootVisualElement.style.height=h;doc.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.06f);
        var prev=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"..","Item-Selected-Yellow.png"),scene.EncodeToPNG());RenderTexture.active=prev;doc.panelSettings.targetTexture=null;target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL selected-item preview timeout");deadline=double.MaxValue;Application.Quit(1);}}
}
