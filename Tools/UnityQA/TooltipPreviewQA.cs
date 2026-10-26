using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Graphics integration preview; mouse coordinates drive the production picking path.
public sealed class TooltipPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    string path;int checks;HelloWorldManager manager;RouletteView view;VisualElement root;Keyboard keys;Mouse mouse;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--tooltip-qa");if(i<0)return;var q=new GameObject("Tooltip QA").AddComponent<TooltipPreviewQA>();q.path=a[i+1];Directory.CreateDirectory(q.path);Application.targetFrameRate=30;}
    void Check(bool ok,string message){checks++;if(!ok){Finish("FAIL tooltip: "+message,1);throw new Exception(message);}}
    void Finish(string message,int code=0)
    {
        File.WriteAllText(Path.Combine(path,"done"),message);Debug.Log(message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Alt(bool held)
    {
        var state=held?new KeyboardState(Key.LeftAlt):new KeyboardState();InputState.Change(keys,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(keys,state,InputUpdateType.Editor);
#endif
        keys.MakeCurrent();DuelPointer.ApplyCursor();
    }
    void Move(Vector2 panelPoint)
    {
        var zero=RuntimePanelUtils.ScreenToPanel(root.panel,Vector2.zero);var one=RuntimePanelUtils.ScreenToPanel(root.panel,Vector2.one);
        var screen=new Vector2((panelPoint.x-zero.x)/(one.x-zero.x),(panelPoint.y-zero.y)/(one.y-zero.y));
        var state=new MouseState{position=new Vector2(screen.x,Screen.height-screen.y)};InputState.Change(mouse,state,InputUpdateType.Dynamic);
#if UNITY_EDITOR
        InputState.Change(mouse,state,InputUpdateType.Editor);
#endif
        mouse.MakeCurrent();
    }
    IEnumerator Hover(VisualElement target,string title,string contains)
    {
        Move(target.worldBound.center);yield return new WaitForSecondsRealtime(.3f);
        Check(Field<VisualElement>(view,"hoverTip").resolvedStyle.display!=DisplayStyle.None,"hover visible: "+title);
        Check(Field<Label>(view,"hoverTipTitle").text==title&&Field<Label>(view,"hoverTipBody").text.Contains(contains),"correct short description: "+title);
        Check(Field<VisualElement>(view,"hoverTip").pickingMode==PickingMode.Ignore,"tooltip never captures pointer");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();root=view.GetComponent<UIDocument>().rootVisualElement;
        var settings=view.GetComponent<UIDocument>().panelSettings;settings.scaleMode=PanelScaleMode.ConstantPixelSize;settings.scale=1;settings.targetTexture=new RenderTexture(1200,800,24);settings.targetTexture.Create();settings.clearColor=true;settings.colorClearValue=Color.clear;root.style.width=1200;root.style.height=800;
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","19111");while(HelloWorldPlayer.Local==null)yield return null;
        keys=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Alt(true);yield return new WaitForSecondsRealtime(.3f);
        var weapons=Field<Button[]>(view,"weaponButtons");yield return Hover(weapons[0],"6연발 리볼버","몸통 50");yield return Hover(weapons[1],"단발 소드오프","양옆 25");yield return Hover(weapons[2],WeaponRules.Name(DuelWeapon.BAS),"목숨 2개");yield return Capture("Ready-Weapon-Hover.png");
        var items=Field<Button[]>(view,"loadoutButtons");yield return Hover(items[0],"응급 앰플","50 회복");yield return Hover(items[1],"테이저건","퀵핸즈 사용 불가");yield return Capture("Ready-Item-Hover.png");
        var d=Field<DuelDirector>(manager,"director");d.SetAutoSelectItems(true);typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);yield return new WaitForSecondsRealtime(.2f);
        Check(!items[0].enabledInHierarchy,"selected item control disabled by auto selection");yield return Hover(items[0],"응급 앰플","사용 후 소모");
        HelloWorldPlayer.Local.SelectWeapon(DuelWeapon.SawedOff);yield return new WaitForSecondsRealtime(.2f);
        var hud=root.Q<VisualElement>(className:"local-weapon-panel");yield return Hover(hud,"단발 소드오프","직격 75");
        var tip=Field<VisualElement>(view,"hoverTip");Check(tip.worldBound.xMin>=0&&tip.worldBound.xMax<=1200&&tip.worldBound.yMin>=0&&tip.worldBound.yMax<=800,"HUD tip stays inside right/bottom screen edges");yield return Capture("HUD-Weapon-Hover.png");
        HelloWorldPlayer.Local.SelectWeapon(DuelWeapon.BAS);yield return new WaitForSecondsRealtime(.2f);yield return Hover(hud,WeaponRules.Name(DuelWeapon.BAS),"3배율");
        Check(manager.ServerGame.ReadyMask==0,"reading hints never changes readiness");
        Move(new Vector2(10,10));yield return null;Check(tip.resolvedStyle.display==DisplayStyle.None,"leaving target hides hint");
        yield return Hover(weapons[0],"6연발 리볼버","머리 100");Alt(false);yield return null;Check(tip.resolvedStyle.display==DisplayStyle.None,"releasing Alt immediately hides hint");
        Finish("PASS tooltip: "+checks+" checks");
    }
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);var uiTarget=view.GetComponent<UIDocument>().panelSettings.targetTexture;var previous=RenderTexture.active;RenderTexture.active=uiTarget;var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(Path.Combine(path,name),scene.EncodeToPNG());RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(ui);Destroy(scene);
    }
}
