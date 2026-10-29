using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.UIElements;
public sealed class PatchNotesRuntimeQA:MonoBehaviour
{
    [Serializable] class Document { public Entry[] entries; }
    [Serializable] class Entry { public string date; public string[] notes; }
    int NoteCount => JsonUtility.FromJson<Document>(Resources.Load<TextAsset>("QuickHandsPatchNotes").text).entries.Sum(x => x.notes.Length);
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    string path;int checks;HelloWorldManager manager;RouletteView view;
    T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
    void Check(bool ok,string why){checks++;if(!ok){Done("FAIL patch notes: "+why,1);throw new Exception(why);}}
    void Done(string text,int code=0){Debug.Log(text);File.WriteAllText(Path.Combine(path,"done"),text);Application.Quit(code);}
    void Click(Button button)=>typeof(Clickable).GetMethod("SimulateSingleClick",F).Invoke(button.clickable,new object[]{null,0});
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--patch-notes-dir");if(i<0)return;var qa=new GameObject("Patch notes QA").AddComponent<PatchNotesRuntimeQA>();qa.path=args[i+1];Directory.CreateDirectory(qa.path);Application.targetFrameRate=45;Application.runInBackground=true;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.8f);manager=HelloWorldManager.Instance;view=manager.GetComponent<RouletteView>();RoomCameraCaptureQA.Setup(view);
        var layer=Field<VisualElement>(view,"patchNotesLayer");var close=Field<Button>(view,"patchNotesClose");var root=Field<VisualElement>(view,"root");
        Check(Field<bool>(view,"patchNotesOpen")&&layer.style.display.value!=DisplayStyle.None,"window automatically opens on a fresh launch");
        var text=string.Join("\n",layer.Query<Label>().ToList().Select(x=>x.text));
        Check(text.Contains("2026.10.27"),"recorded date displayed");Check(layer.Query<VisualElement>(className:"patch-note-card").ToList().Count==NoteCount,"all accumulated patch entries");
        Check(text.Contains("저격소총을 제외한 총기의 정조준 기능을 삭제했습니다")&&text.Contains("지향사격"),"shooting patch text");
        Check(text.Contains("효과가 종료되지 않는 문제를 수정했습니다"),"stun fix patch text");Check(text.Contains("1.3초")&&text.Contains("강제로 격발시키지 않습니다")&&text.Contains("양옆 자리"),"taser rebalance patch text");
        yield return RoomCameraCaptureQA.Capture(view,Path.Combine(path,"Patch-Notes-Startup.png"));
        root.style.width=800;root.style.height=600;yield return null;yield return null;
        var panel=layer.Q<VisualElement>(className:"patch-notes-panel");var bounds=panel.worldBound;
        Check(bounds.xMin>=-.1&&bounds.xMax<=800.1&&bounds.yMin>=-.1&&bounds.yMax<=600.1,"panel fits smaller screen");
        Check(panel.worldBound.Contains(close.worldBound.center),"close button remains inside panel");
        root.style.width=1200;root.style.height=800;Click(close);yield return null;Check(!Field<bool>(view,"patchNotesOpen")&&layer.style.display.value==DisplayStyle.None,"close button dismisses window");
        var open=root.Query<Button>().ToList().First(x=>x.text=="패치노트");Click(open);yield return null;Check(Field<bool>(view,"patchNotesOpen"),"menu button reopens notes");
        using(var key=KeyDownEvent.GetPooled('\0',KeyCode.Escape,EventModifiers.None)){close.SendEvent(key);}yield return null;Check(!Field<bool>(view,"patchNotesOpen"),"Escape closes window");
        Click(open);yield return null;Check(layer.Query<VisualElement>(className:"patch-note-card").ToList().Count==NoteCount,"reopening does not duplicate entries");
        manager.Begin(DuelMode.AI,false,"127.0.0.1","7777");while(HelloWorldPlayer.Local==null)yield return null;yield return null;
        Check(manager.Busy&&!Field<bool>(view,"patchNotesOpen"),"notes do not cover an entered lobby");
        manager.Leave();while(manager.Busy)yield return null;Check(!Field<bool>(view,"patchNotesOpen"),"returning to menu does not reopen automatically");
        Done("PASS patch notes: "+checks+" checks");
    }
}
