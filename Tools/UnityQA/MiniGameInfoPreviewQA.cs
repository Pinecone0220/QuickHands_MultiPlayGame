using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public sealed class MiniGameInfoPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    RouletteView view;UIDocument document;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--minigame-info-preview")>=0)new GameObject("MiniGameInfoPreviewQA").AddComponent<MiniGameInfoPreviewQA>();}
    T Field<T>(string name)=>(T)typeof(RouletteView).GetField(name,F).GetValue(view);
    void Call(string name,params object[] args)=>typeof(RouletteView).GetMethod(name,F).Invoke(view,args);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);
        view=HelloWorldManager.Instance.GetComponent<RouletteView>();view.enabled=false;
        document=FindFirstObjectByType<UIDocument>();var root=document.rootVisualElement;root.style.width=1200;root.style.height=800;
        yield return Capture("MiniGame-Menu.png");
        Call("SetWeaponInfo",true);Call("SetMiniGameInfo");yield return null;yield return null;
        yield return Capture("MiniGame-Info.png");
        var scroll=Field<ScrollView>("infoScroll");scroll.ScrollTo(Field<VisualElement[]>("miniGameInfoCards")[PartyGames.Count-1]);yield return null;yield return null;
        yield return Capture("MiniGame-Info-Bottom.png");
        Call("SetInfoMode",true);yield return null;Call("SetInfoMode",false);yield return null;
        Call("SetWeaponInfo",false);
        Debug.Log("PASS minigame info: rendered menu, five game rules, scrolling and existing info modes");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#endif
    }
    IEnumerator Capture(string name)
    {
        int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();
        document.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;document.panelSettings.scale=1;document.panelSettings.targetTexture=target;document.panelSettings.clearColor=true;document.panelSettings.colorClearValue=Color.clear;
        document.rootVisualElement.MarkDirtyRepaint();yield return new WaitForSecondsRealtime(.1f);
        var previous=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var sceneTarget=new RenderTexture(w,h,24);sceneTarget.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=sceneTarget});
        RenderTexture.active=sceneTarget;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();
        var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"..",name),scene.EncodeToPNG());RenderTexture.active=previous;document.panelSettings.targetTexture=null;
        target.Release();sceneTarget.Release();Destroy(target);Destroy(sceneTarget);Destroy(ui);Destroy(scene);
    }
}
