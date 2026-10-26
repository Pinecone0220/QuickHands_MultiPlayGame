using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;

// QA-only: copy into the isolated project's Assets, never the production scene.
public sealed class InkFaceRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--ink-face-qa");if(i<0)return;
        var q=new GameObject("Ink face QA").AddComponent<InkFaceRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--ink-face-dir")+1];Directory.CreateDirectory(q.path);
        q.deadline=Time.realtimeSinceStartupAsDouble+75;Application.targetFrameRate=60;
    }
    void Finish(string s,int code=0)
    {
        File.WriteAllText(Path.Combine(path,role+"-done"),s);Debug.Log(s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL ink face "+role+": "+s,1);throw new Exception(s);}}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ink face timeout "+role,1);}
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Portrait(string name)
    {
        if(role!="preview")return;
        var head=manager.GetComponent<RouletteTable>().InkHead(3);var forward=DuelGunplay.Forward(3);
        var go=new GameObject("QA ink portrait camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=36;camera.aspect=1.5f;
        camera.transform.position=head.position+new Vector3(forward.X,0,forward.Z)*1.1f;camera.transform.LookAt(head.position+Vector3.down*.035f);
        var rt=new RenderTexture(1200,800,24);rt.Create();UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1200,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,800),0,0);t.Apply();File.WriteAllBytes(Path.Combine(path,name),t.EncodeToPNG());RenderTexture.active=old;rt.Release();Destroy(rt);Destroy(t);Destroy(go);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;
        manager.Begin(DuelMode.PvP,false,"127.0.0.1","18987");while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        D.Join(3,Now);D.SetAutoSelectItems(true);D.Game.SetShotTime(0);D.Ready(0,Now);D.Ready(3,Now);
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(D,new object[]{DuelStage.Racing,Now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(D,"clock"),new ClockState{Phase=ClockPhase.Shooting,MiniGame=PartyMiniGame.Countdown,Iteration=1,ParticipantsMask=9,First=0,PhaseAt=Now,StartedAt=Now});
        var a=Field<DuelActionState[]>(D,"actions");a[3].AimYaw=180;Publish();yield return new WaitForSecondsRealtime(.4f);
        var visuals=manager.GetComponent<DuelInkVisuals>();var coats=Field<Transform[]>(visuals,"coats");var mats=Field<Material[]>(visuals,"coatMaterials");
        Check(!coats[3].gameObject.activeInHierarchy,"clean victim has no ink");Portrait("Ink-Face-Clean.png");
        var mesh=coats[3].GetComponentInChildren<MeshFilter>(true).sharedMesh;var skin=manager.GetComponent<RouletteTable>().InkHead(3).Find("Head").GetComponent<MeshFilter>().sharedMesh;
        var vertices=mesh.vertices;var normals=skin.normals;var original=skin.vertices;bool attached=true;
        for(int i=0;i<vertices.Length;i++)
        {
            var offset=vertices[i]-original[i];attached&=Mathf.Abs(offset.magnitude-.0015f)<.000001&&Vector3.Dot(offset,normals[i])>0;
            for(int j=0;j<i;j++)if(original[j]==original[i])attached&=vertices[j]==vertices[i];
        }
        Check(attached&&mesh.triangles.Length>0&&mesh.triangles.Length<skin.triangles.Length,"stain follows actual front face facets with 1.5mm offset");
        Check(coats[3].GetComponentsInChildren<Collider>(true).Length==0,"cosmetic stain adds no hit collider");Check(mats[3].GetTexture("_BaseMap")!=null&&mats[3].shader.name=="Roulette/Ink","splatter texture and included shader available");
        var delta=DuelGunplay.Point(3,DuelHitPart.Head)-DuelGunplay.Eye(0);float distance=Mathf.Sqrt(delta.X*delta.X+delta.Z*delta.Z),t=(distance-.65f)/DuelInk.Speed;
        DuelGunplay.Angles(delta,out float yaw,out float pitch);yaw-=Mathf.Atan2(.24f,distance)*Mathf.Rad2Deg;pitch=Mathf.Atan2(delta.Y+.1f-DuelInk.UpwardSpeed*t+.5f*DuelInk.Gravity*t*t,distance)*Mathf.Rad2Deg;
        Check(D.UseItem(0,DuelItem.Ink,1,Now),"normal shooting equips pouch");Check(D.ThrowInk(0,yaw,pitch,1,a[0].Life,a[0].Ink.Version,Now,Now),"server accepts real throw");Publish();
        while(D.ForSeat(0).Ink.ImpactAt<0)yield return null;yield return new WaitForSecondsRealtime(.1f);
        Check(D.ForSeat(0).Ink.Target==3&&player.State.Value.Seat3.Ink.InkedAt>=0,"actual swept hit reaches replicated victim state");
        Check(coats[3].gameObject.activeInHierarchy&&!coats[0].gameObject.activeInHierarchy,"only hit victim shows facial stain");Check(D.Game.HealthForSeat(3)==100,"visual effect does not add damage");Portrait("Ink-Face-Hit.png");
        a[3].AimYaw=215;a[3].AimPitch=12;Publish();yield return new WaitForSecondsRealtime(.45f);
        Check(coats[3].parent==manager.GetComponent<RouletteTable>().InkHead(3)&&coats[3].localRotation==Quaternion.identity,"ink remains attached while head turns");Portrait("Ink-Face-Turned.png");
        while(Now-a[3].Ink.InkedAt<4.7)yield return null;Check(coats[3].gameObject.activeInHierarchy&&mats[3].GetColor("_BaseColor").a==1,"full stain persists during five second hold");
        while(Now-a[3].Ink.InkedAt<5.2)yield return null;yield return null;Check(coats[3].gameObject.activeInHierarchy&&mats[3].GetColor("_BaseColor").a>0&&mats[3].GetColor("_BaseColor").a<1,"stain fades with victim screen after five seconds");
        while(Now-a[3].Ink.InkedAt<5.6)yield return null;yield return null;Check(!coats[3].gameObject.activeInHierarchy,"stain clears after half second fade");Portrait("Ink-Face-Cleared.png");
        Finish("PASS ink face "+role+": "+checks+" checks");
    }
}
