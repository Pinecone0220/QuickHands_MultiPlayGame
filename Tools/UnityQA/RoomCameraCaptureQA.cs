using System.IO;
using System.Collections;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class RoomCameraCaptureQA
{
    public static void Setup(RouletteView view)
    {
        var doc=view.GetComponent<UIDocument>();var s=doc.panelSettings;s.scaleMode=PanelScaleMode.ConstantPixelSize;s.scale=1;
        s.targetTexture=new RenderTexture(1200,800,24);s.targetTexture.Create();s.clearColor=true;s.colorClearValue=Color.clear;
        doc.rootVisualElement.style.width=1200;doc.rootVisualElement.style.height=800;Camera.main.aspect=1.5f;
    }
    public static IEnumerator Capture(RouletteView view,string path)
    {
        yield return new WaitForSecondsRealtime(.12f);var old=RenderTexture.active;RenderTexture.active=view.GetComponent<UIDocument>().panelSettings.targetTexture;
        var ui=new Texture2D(1200,800,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,1200,800),0,0);ui.Apply();
        var rt=new RenderTexture(1200,800,24);rt.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var scene=new Texture2D(1200,800,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,1200,800),0,0);scene.Apply();var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();File.WriteAllBytes(path,scene.EncodeToPNG());RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(ui);Object.Destroy(scene);
    }
}
