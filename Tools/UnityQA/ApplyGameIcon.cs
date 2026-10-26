#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
public static class ApplyGameIcon
{
    public static void Run()
    {
        const string path="Assets/Art/Icons/QuickHands-AppIcon.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null||texture.width!=texture.height||texture.width<512)throw new Exception("Invalid game icon asset");
#pragma warning disable CS0618
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{texture},IconKind.Any);
        var sizes=PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone,IconKind.Any);
        var icons=new Texture2D[sizes.Length];for(int i=0;i<icons.Length;i++)icons[i]=texture;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone,icons,IconKind.Any);
        var assigned=PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown,IconKind.Any);
        if(assigned.Length==0||assigned[0]!=texture)throw new Exception("Default game icon was not assigned");
        var desktop=PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone,IconKind.Any);
        if(desktop.Length==0||Array.Exists(desktop,t=>t!=texture))throw new Exception("Desktop icon sizes were not assigned");
#pragma warning restore CS0618
        AssetDatabase.SaveAssets();Debug.Log("PASS game icon: "+texture.width+"x"+texture.height+", desktop sizes "+string.Join(",",sizes));
        ChatBuild.Run();
    }
}
#endif
