#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class ChatBuild
{
    public static void Run()
    {
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/SampleScene.unity"},
            locationPathName="Build/ChatQA.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        bool ok=report.summary.result==BuildResult.Succeeded;Debug.Log(ok?"PASS chat build":"FAIL chat build");EditorApplication.Exit(ok?0:1);
    }
}
#endif
