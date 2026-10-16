using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public sealed class MemoryScrollPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    RouletteView view;ScrollView scroll;UIDocument document;int checks;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--memory-scroll-preview")>=0)new GameObject("MemoryScrollPreviewQA").AddComponent<MemoryScrollPreviewQA>();}
    T Field<T>(string name)=>(T)typeof(RouletteView).GetField(name,F).GetValue(view);
    void Check(bool value,string reason){checks++;if(!value){Debug.LogError("FAIL memory scroll: "+reason);Finish(1);throw new Exception(reason);}}
    void Finish(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Draw(int length,bool showing,int progress,double age)
    {
        typeof(RouletteView).GetField("memoryCount",F).SetValue(view,progress);
        var m=new ArrowMemoryState{Length=length,AnswerSeed=74813,PhaseAt=100,Phase=showing?ArrowMemoryPhase.Showing:ArrowMemoryPhase.Input};
        for(int frame=0;frame<5;frame++)
        {
            typeof(RouletteView).GetMethod("RenderMemoryArrows",F).Invoke(view,new object[]{m,showing,!showing,100+age});
            yield return null;
        }
        Field<Label>("clockTitle").text=$"뭐였더라? · {length}개";
        Field<Label>("clockTimer").text=showing?"순서를 기억하세요.":$"입력 {progress}/{length}";
    }
    Label Slot(int index)
    {
        foreach(var label in Field<List<Label>>("memoryArrowLabels"))if(label.style.display.value!=DisplayStyle.None&&(int)label.userData==index)return label;
        return null;
    }
    void Visible(int index)
    {
        var label=Slot(index);Check(label!=null,"current slot exists: "+index);
        var a=label.worldBound;var b=scroll.contentViewport.worldBound;
        Check(a.xMin>=b.xMin-.5f&&a.xMax<=b.xMax+.5f,"current slot stays fully in the horizontal viewport: "+index);
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+90;Time.captureDeltaTime=1f/60;yield return new WaitForSecondsRealtime(1);
        view=HelloWorldManager.Instance.GetComponent<RouletteView>();view.enabled=false;
        document=FindFirstObjectByType<UIDocument>();var root=document.rootVisualElement;root.style.width=1200;root.style.height=800;
        var table=Field<VisualElement>("table");foreach(var child in root.Children())child.style.display=child==table?DisplayStyle.Flex:DisplayStyle.None;
        var panel=Field<VisualElement>("clockPanel");foreach(var child in table.Children())child.style.display=child==panel?DisplayStyle.Flex:DisplayStyle.None;
        panel.EnableInClassList("memory-panel",true);
        foreach(string name in new[]{"shootingNotice","clockRanking","clockRankingRows","clockStop","miniInstruction"})Field<VisualElement>(name).style.display=DisplayStyle.None;
        scroll=Field<ScrollView>("memoryScroll");scroll.style.display=DisplayStyle.Flex;yield return null;yield return null;
        yield return Draw(3,true,0,0);Visible(0);Visible(2);Check(scroll.scrollOffset.x<.1f,"short sequence stays centered without scrolling");
        yield return Draw(30,true,0,0);Visible(0);float first=scroll.scrollOffset.x;
        yield return Draw(30,true,0,1.5);Check(scroll.scrollOffset.x>first+100,"reveal automatically moves right");
        yield return Capture("Memory-Horizontal-Showing.png");
        yield return Draw(30,true,0,2.9);Visible(29);Check(scroll.scrollOffset.y==0,"reveal never scrolls vertically");
        yield return Draw(30,false,0,0);Visible(0);Check(scroll.scrollOffset.x<.1f,"input resets to the beginning after reveal");
        foreach(int progress in new[]{1,9,10,15,29,30})
        {
            yield return Draw(30,false,progress,0);Visible(Math.Min(progress,29));
            foreach(var label in Field<List<Label>>("memoryArrowLabels"))if(label.style.display.value!=DisplayStyle.None)
            {
                int index=(int)label.userData;Check(label.text==(index<progress?"●":"•"),"input reveals only completion markers, never directions");
                Check(label.ClassListContains("memory-arrow-entered")== (index<progress),"completion styling follows global input index");
            }
        }
        yield return Draw(30,false,17,0);yield return Capture("Memory-Horizontal-Input.png");
        yield return Draw(10000,false,9999,0);Visible(9999);
        Check(Field<List<Label>>("memoryArrowLabels").Count<30,"long sequences reuse a bounded cell pool");
        root.style.width=800;yield return null;yield return null;yield return Draw(50,false,24,0);Visible(24);
        yield return Draw(3,true,0,0);Visible(0);Visible(2);Check(scroll.scrollOffset.x<.1f,"new short round clears prior horizontal position");
        Debug.Log("PASS memory scroll: "+checks+" completion privacy, reveal travel, input follow, resize and bounded cells checks");Finish(0);
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
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL memory scroll timeout");Finish(1);}}
}
