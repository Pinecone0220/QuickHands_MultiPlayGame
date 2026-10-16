using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
public sealed class TabletopPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    RouletteTable table;DuelDrinkVisuals drinks;DuelWeaponAudio audio;Array puppets;DuelTabletopState state;int checks;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--tabletop-preview")<0)return;new GameObject("TabletopPreviewQA").AddComponent<TabletopPreviewQA>();}
    T Field<T>(object o,string key)=>(T)o.GetType().GetField(key,F).GetValue(o);
    void Check(bool ok,string text){checks++;if(!ok){Debug.LogError("FAIL tabletop preview: "+text);Exit(1);throw new Exception(text);}}
    static void Exit(int result)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(result);
#else
        Application.Quit(result);
#endif
    }
    void Frame(int seat,float t,float turn=0)
    {
        var p=puppets.GetValue(seat);var root=Field<Transform>(p,"Root");var action=state.ForSeat(seat).Action;
        var context=new DuelPoseContext{Seat=seat,Time=10,DrinkAction=action,DrinkProgress=t};
        typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{seat,DuelWeapon.Revolver});
        typeof(RouletteTable).GetMethod("AnimatePuppetFrame",F).Invoke(table,new object[]{p,0f,0f,0f,root.eulerAngles.y+180+turn,8f,0f,context});
    }
    void Bones(int seat)
    {
        var p=puppets.GetValue(seat);
        var root=Field<Transform>(p,"Root");
        var waist=root.InverseTransformPoint(Field<Transform>(p,"Torso").TransformPoint(new Vector3(0,-.36f,0)));
        Check(Vector3.Distance(waist,new Vector3(0,.83f-DuelGunplay.SeatedDrop,0))<.005f,"drinking keeps the jacket attached to the seated hips");
        foreach(var names in new[]{new[]{"Upper","Forearm","Hand"},new[]{"LeftArm","LeftForearm","LeftHand"}})
        {
            var upper=Field<Transform>(p,names[0]);var fore=Field<Transform>(p,names[1]);var hand=Field<Transform>(p,names[2]);
            Check(Mathf.Abs(upper.localScale.y*2-DuelArmIK.Upper)<.00001f&&Mathf.Abs(fore.localScale.y*2-DuelArmIK.Forearm)<.00001f,"drinking never stretches arm meshes");
            Check(Vector3.Distance(fore.position+fore.up*DuelArmIK.Forearm*.5f,hand.position)<.001f,"drinking wrist joins forearm");
            Check(Vector3.Distance(upper.position+upper.up*DuelArmIK.Upper*.5f,fore.position-fore.up*DuelArmIK.Forearm*.5f)<.001f,"drinking elbow remains joined");
        }
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+90;Time.captureDeltaTime=1f/60;yield return new WaitForSecondsRealtime(1);
        table=HelloWorldManager.Instance.Table;table.enabled=false;drinks=HelloWorldManager.Instance.GetComponent<DuelDrinkVisuals>();audio=HelloWorldManager.Instance.GetComponent<DuelWeaponAudio>();
        FindFirstObjectByType<UIDocument>().rootVisualElement.style.display=DisplayStyle.None;
        puppets=Field<Array>(table,"puppets");foreach(var rig in Field<Array>(table,"rigs")){Field<Transform>(rig,"Holding").gameObject.SetActive(false);Field<Transform>(rig,"Loading").gameObject.SetActive(false);}
        state=new DuelTabletopState{BottlesBrokenMask=24,GlassesBrokenMask=12,FilledMask=18,DrankMask=32};
        for(int i=0;i<6;i++)state.SetSeat(i,new DuelTableEntry{Action=i==0?DuelDrinkAction.Pour:i==1?DuelDrinkAction.Sip:i==2?DuelDrinkAction.Inspect:DuelDrinkAction.None,StartedAt=8,BottleBrokenAt=1,GlassBrokenAt=1});
        for(int frame=0;frame<55;frame++){drinks.Observe(state,1,10,audio,true);for(int i=0;i<6;i++)Frame(i,.54f);yield return null;}
        var settings=Field<Array>(drinks,"settings");
        for(int i=0;i<6;i++)
        {
            Bones(i);var s=settings.GetValue(i);
            Check(Field<Transform>(s,"Bottle").gameObject.activeSelf==!state.Broken(i,DuelTableProp.Bottle),"bottle visibility matches server state");
            Check(Field<Transform>(s,"Glass").gameObject.activeSelf==!state.Broken(i,DuelTableProp.Glass),"glass visibility matches server state");
            foreach(var m in Field<Transform>(s,"Bottle").GetComponentsInChildren<MeshFilter>(true))foreach(var n in m.sharedMesh.normals)Check(n.sqrMagnitude>.9f,"bottle flat normals valid");
        }
        Check(Field<LineRenderer>(settings.GetValue(0),"Pour").enabled,"pour stream joins bottle and cup");
        Check(drinks.RightGrip(2)>.99f&&drinks.LeftGrip(2)==0&&!Field<LineRenderer>(settings.GetValue(2),"Pour").enabled,"broken glass permits bottle inspection only");
        foreach(int i in new[]{0,2})Check(Vector3.Distance(Field<Transform>(settings.GetValue(i),"Bottle").position,Field<Transform>(puppets.GetValue(i),"Hand").position)<.081f,"bottle remains held at hand rather than floating");
        Check(Vector3.Distance(Field<Transform>(settings.GetValue(1),"Glass").position,Field<Transform>(puppets.GetValue(1),"LeftHand").position)<.076f,"sip glass remains in hand");
        var cam=Camera.main;cam.transform.position=new Vector3(7.4f,4,-7.4f);cam.transform.LookAt(new Vector3(0,1.12f,0));cam.fieldOfView=46;
        yield return Capture("Tabletop-Whiskey-Overview.png",1600,1000);
        var root=Field<Transform>(puppets.GetValue(0),"Root");cam.transform.position=root.TransformPoint(new Vector3(-1.55f,2.0f,-1.95f));cam.transform.LookAt(root.TransformPoint(new Vector3(0,1.24f,-.55f)));cam.fieldOfView=43;
        yield return Capture("Tabletop-Pouring.png",1300,1000);
        cam.transform.position=root.TransformPoint(new Vector3(-2.5f,1.7f,-.35f));cam.transform.LookAt(root.TransformPoint(new Vector3(0,1.12f,-.32f)));cam.fieldOfView=42;
        yield return Capture("Tabletop-Seated-Pouring.png",1300,1000);
        var root1=Field<Transform>(puppets.GetValue(1),"Root");cam.transform.position=root1.TransformPoint(new Vector3(-1.35f,1.9f,-1.6f));cam.transform.LookAt(root1.TransformPoint(new Vector3(0,1.4f,-.25f)));cam.fieldOfView=42;
        yield return Capture("Tabletop-Drinking.png",1300,1000);
        var visibility=typeof(RouletteTable).GetMethod("ApplyLocalVisibility",F);
        visibility.Invoke(table,new object[]{0,true,false});
        foreach(var r in Field<Renderer[]>(puppets.GetValue(0),"Renderers"))Check(r.forceRenderingOff,"own avatar animation is hidden even when its head moves away from the eye");
        foreach(var r in Field<Renderer[]>(settings.GetValue(0),"BottleRenderers"))Check(r.forceRenderingOff,"own held bottle is hidden in first person");
        foreach(var r in Field<Renderer[]>(settings.GetValue(0),"GlassRenderers"))Check(r.forceRenderingOff,"own held cup is hidden in first person");
        foreach(var r in Field<Renderer[]>(settings.GetValue(1),"GlassRenderers"))Check(!r.forceRenderingOff,"remote player drinking remains visible");
        var eye=DuelGunplay.Eye(0);cam.transform.position=new Vector3(eye.X,eye.Y,eye.Z);cam.transform.rotation=Quaternion.Euler(15,0,0);cam.fieldOfView=DuelGunplay.BaseFov;
        yield return Capture("Character-First-Person-Clean.png",1300,900);
        visibility.Invoke(table,new object[]{0,false,false});
        foreach(var r in Field<Renderer[]>(settings.GetValue(0),"BottleRenderers"))Check(!r.forceRenderingOff,"changing view restores prop rendering");
        for(int frame=0;frame<30;frame++){drinks.Observe(state,1,10,audio,true);for(int i=0;i<3;i++)Frame(i,.54f,40);yield return null;}
        for(int i=0;i<3;i++)Bones(i);
        Check(Field<Transform>(puppets.GetValue(1),"Head").localRotation.eulerAngles.y>10,"camera look can continue while drinking");
        for(int i=0;i<3;i++)
        {
            var head=Field<Transform>(puppets.GetValue(i),"Head");var pose=DuelCharacterPose.Sample(new DuelPoseContext{Seat=i,Time=10,DrinkAction=state.ForSeat(i).Action,DrinkProgress=.54f});
            var direction=DuelGunplay.Direction(i*60+40,8+pose.HeadLift);
            Check(Vector3.Angle(-head.forward,new Vector3(direction.X,direction.Y,direction.Z))<1,"animated head follows camera direction with only the drink's small nod");
        }
        Check(drinks.LeftGrip(1)>.99f&&Vector3.Distance(Field<Transform>(settings.GetValue(1),"Glass").position,Field<Transform>(puppets.GetValue(1),"LeftHand").position)<.076f,"sip persists while turning head");
        foreach(var action in new[]{DuelDrinkAction.Pour,DuelDrinkAction.Sip,DuelDrinkAction.Inspect})
        {
            var rest=DuelCharacterPose.Sample(new DuelPoseContext{Seat=0,Time=10});
            foreach(float t in new[]{0f,.001f,.999f,1f})
            {
                var transition=DuelCharacterPose.Sample(new DuelPoseContext{Seat=0,Time=10,DrinkAction=action,DrinkProgress=t});
                Check(Mathf.Abs(transition.Lean-rest.Lean)<.01f&&Mathf.Abs(transition.RightChin-rest.RightChin)<.001f,"drink entry and release have no posture jump");
            }
            for(int frame=0;frame<=60;frame++)
            {
                state.SetSeat(0,new DuelTableEntry{Action=action,StartedAt=8});drinks.Observe(state,1,10,audio,true);Frame(0,frame/60f);Bones(0);yield return null;
            }
        }
        foreach(var renderer in drinks.GetComponentsInChildren<Renderer>(true))Check(renderer.GetComponent<Collider>()==null,"props are presentation without client damage colliders");
        state=default;drinks.Observe(state,2,11,audio,true);for(int i=0;i<6;i++)Frame(i,0);
        foreach(var s in settings){Check(Field<Transform>(s,"Bottle").gameObject.activeSelf&&Field<Transform>(s,"Glass").gameObject.activeSelf,"new party restores props");}
        Debug.Log("PASS tabletop preview: "+checks+" low-poly, fixed arm, hand contact, head look, broken visibility and reset checks");Exit(0);
    }
    IEnumerator Capture(string file,int width,int height)
    {
        yield return null;var cam=Camera.main;var target=new RenderTexture(width,height,24);target.Create();float aspect=cam.aspect;cam.aspect=(float)width/height;
        RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"..",file),texture.EncodeToPNG());RenderTexture.active=previous;cam.aspect=aspect;target.Release();Destroy(target);Destroy(texture);
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL tabletop preview timeout");Exit(1);}}
}
