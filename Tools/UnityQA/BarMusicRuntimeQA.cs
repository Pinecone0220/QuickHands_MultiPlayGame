using System;
using System.Collections;
using System.Reflection;
using HelloWorld;
using UnityEngine;

public sealed class BarMusicRuntimeQA : MonoBehaviour
{
    int checks;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--bar-music-qa")>=0)new GameObject("Bar music QA").AddComponent<BarMusicRuntimeQA>();}
    void Check(bool ok,string message)
    {checks++;if(!ok){Debug.LogError("FAIL bar music: "+message);Finish(1);throw new Exception(message);}}
    static void Finish(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+180;
        yield return new WaitForSecondsRealtime(3);
        var manager=HelloWorldManager.Instance;
        var controller=manager.GetComponent<DuelBackgroundMusic>();
        Check(controller!=null,"music controller present");
        var music=(AudioSource)typeof(DuelBackgroundMusic).GetField("music",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
        Check(music.clip!=null&&music.clip.channels==2&&Math.Abs(music.clip.length-112.941)<.1,"stereo score imports at correct duration");
        Check(music.clip.loadType==AudioClipLoadType.Streaming,"long score streams instead of decoding whole track in memory");
        Check(music.loop&&music.spatialBlend==0&&music.priority>120,"2D loop leaves priority to weapon sounds");
        Check(music.isPlaying&&Math.Abs(music.volume-.28f)<.01,"menu playback fades in");
        Debug.Log("Bar music menu checks passed");float before=music.time;
        manager.Begin(DuelMode.AI,false,"127.0.0.1","17891");
        Debug.Log("Bar music AI start: "+manager.Status);while(HelloWorldPlayer.Local==null)yield return null;Debug.Log("Bar music local player connected");
        yield return new WaitForSecondsRealtime(.3f);HelloWorldPlayer.Local.Rematch();Debug.Log("Bar music ready requested");
        while(HelloWorldPlayer.Local.State.Value.Phase!=DuelPhase.Playing)yield return null;
        yield return new WaitForSecondsRealtime(2);
        Check(music.isPlaying&&music.time>before,"same track continues into the game");
        Check(Math.Abs(music.volume-.16f)<.01,"gameplay has quieter mix");
        // Seek across the real streaming decoder's loop boundary.
        music.time=music.clip.length-.35f;yield return new WaitForSecondsRealtime(.8f);
        Check(music.isPlaying&&music.time<2,"streaming loop wraps without stopping");
        manager.Leave();yield return new WaitForSecondsRealtime(3);
        Check(music.isPlaying&&Math.Abs(music.volume-.28f)<.01,"leaving restores menu mix without restarting music");
        Debug.Log("PASS bar music: "+checks+" duration, streaming, loop, fades and real AI-session transitions");Finish(0);
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL bar music timeout after "+checks+" checks; "+HelloWorldManager.Instance.Status);Finish(1);}}
}
