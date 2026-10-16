using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
public sealed class AvatarPreviewQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    RouletteTable table;Array puppets;int checks;double deadline;bool motion;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--avatar-preview")<0&&Array.IndexOf(Environment.GetCommandLineArgs(),"--avatar-motion-preview")<0)return;new GameObject("AvatarPreviewQA").AddComponent<AvatarPreviewQA>();}
    T Field<T>(object p,string key)=>(T)p.GetType().GetField(key,F).GetValue(p);
    void Check(bool ok,string message){checks++;if(!ok){Debug.LogError("FAIL avatar: "+message);Finish(1);throw new Exception(message);}}
    void Pose(int seat,float lift=0,float hit=0,float fall=0,float yaw=180,float pitch=0,float recovery=0)
    {typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{seat,DuelWeapon.Revolver});typeof(RouletteTable).GetMethod("AnimatePuppet",F).Invoke(table,new object[]{puppets.GetValue(seat),lift,hit,fall,yaw,pitch,recovery});}
    void Frame(DuelPoseContext c,float turn=0,float pitch=0)
    {typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{c.Seat,c.Weapon});float yaw=Field<Transform>(puppets.GetValue(c.Seat),"Root").eulerAngles.y+180+turn;typeof(RouletteTable).GetMethod("AnimatePuppetFrame",F).Invoke(table,new object[]{puppets.GetValue(c.Seat),c.Active?1f:0f,0f,0f,yaw,pitch,0f,c});}
    void Bones(int seat,bool viewModel=false)
    {
        var p=puppets.GetValue(seat);
        foreach(var names in new[]{new[]{"Upper","Forearm","Hand"},new[]{"LeftArm","LeftForearm","LeftHand"}})
        {
            var upper=Field<Transform>(p,names[0]);var fore=Field<Transform>(p,names[1]);var hand=Field<Transform>(p,names[2]);
            Check(Mathf.Abs(upper.localScale.y*2-DuelArmIK.Upper)<.00001f&&Mathf.Abs(fore.localScale.y*2-DuelArmIK.Forearm)<.00001f,"upper and lower arm lengths never change");
            Check(Vector3.Distance(fore.position+fore.up*DuelArmIK.Forearm*.5f,hand.position)<.001f,"fixed forearm joins wrist");
            if(!viewModel)Check(Vector3.Distance(upper.position+upper.up*DuelArmIK.Upper*.5f,fore.position-fore.up*DuelArmIK.Forearm*.5f)<.001f,"fixed elbow segments stay joined");
        }
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+90;Time.captureDeltaTime=1f/60;motion=Array.IndexOf(Environment.GetCommandLineArgs(),"--avatar-motion-preview")>=0;yield return new WaitForSecondsRealtime(1);
        table=HelloWorldManager.Instance.Table;table.enabled=false;
        var doc=FindFirstObjectByType<UIDocument>();doc.rootVisualElement.style.display=DisplayStyle.None;
        puppets=Field<Array>(table,"puppets");Check(puppets.Length==6,"all six avatars created");
        for(int i=0;i<6;i++)
        {
            var p=puppets.GetValue(i);var root=Field<Transform>(p,"Root");root.gameObject.SetActive(true);
            var head=Field<Transform>(p,"Head");var skin=head.Find("Head").GetComponent<MeshFilter>().sharedMesh;
            Check(skin.triangles.Length/3<=100&&skin.vertexCount==skin.triangles.Length,"head uses low polygon flat geometry");
            foreach(var normal in skin.normals)Check(Mathf.Abs(normal.magnitude-1)<.001f,"nondegenerate flat normals");
            Check(head.Find("Eye")==null&&head.Find("Mouth")==null,"abstract unpainted face");
            Check(root.GetComponentsInChildren<Collider>().Length==0,"presentation adds no hit collider");
            foreach(var r in Field<Renderer[]>(p,"HeadRenderers"))r.enabled=true;
            Pose(i,yaw:i*60);var rest=head.position;Pose(i,fall:1,yaw:i*60);Check(head.position.y<rest.y-.25f&&head.position.y<1.25f,"collapse head follows upper body even from forward lean");
            Pose(i,fall:.6f,yaw:i*60,recovery:.3f);
            Check(Vector3.Distance(Field<Transform>(p,"Hand").position,root.TransformPoint(DuelArmIK.RightBrace))<.001f,"right recovery hand braces table within reach");
            Check(Vector3.Distance(Field<Transform>(p,"LeftHand").position,root.TransformPoint(DuelArmIK.LeftBrace))<.001f,"left recovery hand braces table within reach");
            Bones(i);
            Pose(i,yaw:i*60);Check(Vector3.Distance(head.localPosition,new Vector3(0,.92f,-.04f))<.001f,"head returns to original eye anchor despite idle breathing");
        }
        for(int frame=0;frame<30;frame++){for(int i=0;i<6;i++)Pose(i,yaw:i*60);yield return null;}
        for(int frame=0;frame<45;frame++){for(int i=0;i<6;i++)Frame(new DuelPoseContext{Seat=i,Time=4});yield return null;}
        foreach(int i in new[]{0,1,5})
        {
            var p=puppets.GetValue(i);var root=Field<Transform>(p,"Root");
            var waist=root.InverseTransformPoint(Field<Transform>(p,"Torso").TransformPoint(new Vector3(0,-.36f,0)));
            Check(Vector3.Distance(waist,new Vector3(0,.83f-DuelGunplay.SeatedDrop,0))<.005f,"lean keeps the jacket attached to the seated hips");
            foreach(bool right in i==0?new[]{true,false}:new[]{i==1})
            {
                var fore=Field<Transform>(p,right?"Forearm":"LeftForearm");
                var elbow=root.InverseTransformPoint(fore.position-fore.up*DuelArmIK.Forearm*.5f);
                Check(Mathf.Abs(elbow.y-DuelArmIK.TableElbowY)<.002f,"chin-support sleeve rests on tabletop");
                Check(new Vector2(root.TransformPoint(elbow).x,root.TransformPoint(elbow).z).magnitude<3.68f,"supported elbow is inside circular tabletop");
                var target=Field<Transform>(p,"Head").TransformPoint(new Vector3(right?-.055f:.055f,-.27f,-.04f));
                Check(Vector3.Distance(Field<Transform>(p,right?"Hand":"LeftHand").position,target)<.002f,"supporting hand meets chin without stretching");
            }
            Bones(i);
        }
        var cam=Camera.main;cam.transform.position=new Vector3(7.5f,4.5f,-7.5f);cam.transform.LookAt(new Vector3(0,1.1f,0));cam.fieldOfView=45;
        yield return Capture("Low-Poly-Characters-Ingame.png",1600,900);
        Check(GameObject.Find("Cold fill")==null&&GameObject.Find("Dealer rim")==null,"no seat-specific blue or red spot lighting");
        var fillLight=GameObject.Find("Neutral table fill").GetComponent<Light>();
        Check(new Vector2(fillLight.transform.position.x,fillLight.transform.position.z).sqrMagnitude<.00001f,"shared fill is centered for all seats");
        var first=Field<Transform>(puppets.GetValue(0),"Root");cam.transform.position=first.TransformPoint(new Vector3(-1.6f,1.80f,-1.6f));cam.transform.LookAt(first.TransformPoint(new Vector3(0,1.38f,-.1f)));cam.fieldOfView=44;
        yield return Capture("Character-Proportions-Ingame.png",1300,1000);
        var second=Field<Transform>(puppets.GetValue(1),"Root");cam.transform.position=second.TransformPoint(new Vector3(-1.4f,1.85f,-2.1f));cam.transform.LookAt(second.TransformPoint(new Vector3(0,1.38f,-.1f)));
        yield return Capture("Character-Balanced-Light-Ingame.png",1300,1000);
        cam.transform.position=first.TransformPoint(new Vector3(-2.5f,1.7f,-.35f));cam.transform.LookAt(first.TransformPoint(new Vector3(0,1.12f,-.32f)));cam.fieldOfView=42;
        yield return Capture("Character-Seated-Support.png",1300,1000);
        var visibility=typeof(RouletteTable).GetMethod("ApplyLocalVisibility",F);
        foreach(int owner in new[]{0,1,5})
        {
            visibility.Invoke(table,new object[]{owner,true,false});
            for(int i=0;i<6;i++)foreach(var r in Field<Renderer[]>(puppets.GetValue(i),"Renderers"))Check(r.forceRenderingOff==(i==owner),"only the local avatar is hidden, independent of camera distance");
            Check(!table.TryGetNameViewport(owner,out _),"local hidden avatar never shows its own floating name");
            foreach(var r in Field<Transform>(Field<Array>(table,"rigs").GetValue(owner,0),"Gun").GetComponentsInChildren<Renderer>(true))Check(!r.forceRenderingOff,"local weapon and weapon hands remain separate from hidden avatar");
        }
        visibility.Invoke(table,new object[]{0,false,false});
        for(int i=0;i<6;i++)foreach(var r in Field<Renderer[]>(puppets.GetValue(i),"Renderers"))Check(!r.forceRenderingOff,"spectator view restores the avatar");
        foreach(float turn in new[]{-75f,0f,75f})
        {
            for(int frame=0;frame<40;frame++){for(int i=0;i<6;i++)Frame(new DuelPoseContext{Seat=i,Time=4},turn,30);yield return null;}
            for(int i=0;i<6;i++)
            {
                var root=Field<Transform>(puppets.GetValue(i),"Root");var head=Field<Transform>(puppets.GetValue(i),"Head");
                var d=DuelGunplay.Direction(root.eulerAngles.y+180+turn,30);
                Check(Vector3.Angle(-head.forward,new Vector3(d.X,d.Y,d.Z))<1,"head gaze follows world yaw and pitch without tilted neck axes");Bones(i);
            }
        }
        // Use the game's actual meshes and animations, with a neutral preview stage.
        var world=Field<Transform>(table,"world");var chairs=Field<Transform[]>(table,"chairs");
        foreach(Transform child in world)child.gameObject.SetActive(false);
        var stage=new GameObject("Avatar preview lighting and floor").transform;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(stage);floor.transform.position=new Vector3(0,-.06f,0);floor.transform.localScale=new Vector3(20,.1f,15);
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.05f,.065f,.065f));floor.GetComponent<Renderer>().sharedMaterial=mat;
        var key=new GameObject("Preview warm key").AddComponent<Light>();key.transform.SetParent(stage);key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(38,-35,0);key.color=new Color(1,.80f,.60f);key.intensity=2.2f;key.shadows=LightShadows.Soft;
        var fill=new GameObject("Preview neutral fill").AddComponent<Light>();fill.transform.SetParent(stage);fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(18,155,0);fill.color=new Color(.83f,.83f,.80f);fill.intensity=.65f;
        RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.28f,.30f,.31f);
        for(int i=0;i<6;i++)
        {
            var root=Field<Transform>(puppets.GetValue(i),"Root");root.gameObject.SetActive(true);root.position=new Vector3((i-2.5f)*1.25f,0,0);root.rotation=Quaternion.identity;
            puppets.GetValue(i).GetType().GetField("HandsInitialized",F).SetValue(puppets.GetValue(i),false);
            puppets.GetValue(i).GetType().GetField("PoseInitialized",F).SetValue(puppets.GetValue(i),false);
            chairs[i].gameObject.SetActive(true);chairs[i].position=root.position;chairs[i].rotation=root.rotation;
        }
        for(int frame=0;frame<30;frame++){for(int i=0;i<6;i++)Pose(i,yaw:180+(i%2==0?-12:12));yield return null;}
        cam.transform.position=new Vector3(0,1.45f,-8.4f);cam.transform.LookAt(new Vector3(0,1.10f,-.05f));cam.fieldOfView=30;cam.backgroundColor=new Color(.025f,.04f,.045f);
        yield return Capture(motion?"Character-Idle-Poses.png":"Low-Poly-Characters.png",1600,900);
        if(motion)yield return MotionChecks();
        for(int i=1;i<6;i++){Field<Transform>(puppets.GetValue(i),"Root").gameObject.SetActive(false);chairs[i].gameObject.SetActive(false);}
        var hero=Field<Transform>(puppets.GetValue(0),"Root");hero.position=Vector3.zero;chairs[0].position=Vector3.zero;
        puppets.GetValue(0).GetType().GetField("HandsInitialized",F).SetValue(puppets.GetValue(0),false);
        puppets.GetValue(0).GetType().GetField("PoseInitialized",F).SetValue(puppets.GetValue(0),false);
        for(int frame=0;frame<25;frame++){Pose(0,yaw:155,pitch:5);yield return null;}
        cam.transform.position=new Vector3(-1.05f,1.55f,-2.6f);cam.transform.LookAt(new Vector3(0,1.33f,-.03f));cam.fieldOfView=42;
        yield return Capture(motion?"Character-Idle-Closeup.png":"Low-Poly-Character-Closeup.png",1100,1100);
        Pose(0,fall:1);yield return Capture(motion?"Character-Motion-Collapse.png":"Low-Poly-Character-Collapse.png",1100,1100);
        Pose(0,fall:.6f,recovery:.3f);yield return Capture(motion?"Character-Motion-Recovery.png":"Low-Poly-Character-Recovery.png",1100,1100);
        Debug.Log("PASS faceted avatar: "+checks+" geometry, six seats, no colliders, head/collapse/recovery anchor checks");
        Finish(0);
    }
    IEnumerator MotionChecks()
    {
        foreach(float distance in new[]{0f,.02f,.3f,1f,5f})for(int direction=0;direction<12;direction++)
        {
            var target=Quaternion.Euler(direction*23,direction*41,0)*Vector3.forward*distance;
            DuelArmIK.Solve(Vector3.zero,target,Vector3.down,out var elbow,out var wrist);
            Check(Mathf.Abs(elbow.magnitude-DuelArmIK.Upper)<.00001f&&Mathf.Abs((wrist-elbow).magnitude-DuelArmIK.Forearm)<.00001f,"unreachable and folded targets preserve anatomical segment lengths");
        }
        var hands=new Vector3[6];
        for(int frame=0;frame<25;frame++){for(int i=0;i<6;i++)Frame(new DuelPoseContext{Seat=i,Time=1});yield return null;}
        for(int i=0;i<6;i++)hands[i]=Field<Transform>(puppets.GetValue(i),"Head").position;
        for(int frame=0;frame<25;frame++){for(int i=0;i<6;i++)Frame(new DuelPoseContext{Seat=i,Time=4});yield return null;}
        for(int i=0;i<6;i++){Check(Vector3.Distance(hands[i],Field<Transform>(puppets.GetValue(i),"Head").position)>.0001f,"idle has live breathing and weight motion");Bones(i);}
        var before=DuelCharacterPose.Sample(new DuelPoseContext{Time=17.999});var after=DuelCharacterPose.Sample(new DuelPoseContext{Time=18.001});
        Check(Vector3.Distance(before.Right,after.Right)<.003f&&Vector3.Distance(before.Left,after.Left)<.003f,"idle cycle changes without a position jump");
        for(int i=0;i<6;i++){var underFire=DuelCharacterPose.Sample(new DuelPoseContext{Seat=i,Time=4,Combat=true});Check(underFire.RightChin==0&&underFire.LeftChin==0&&underFire.Lean>=-9,"shootable opponents sit upright near server hit regions");}
        var actions=new[]{DuelAction.Pickup,DuelAction.CockHammer,DuelAction.UnlockBarrel,DuelAction.OpenBarrel,DuelAction.Load,DuelAction.Aim};
        var contexts=new DuelPoseContext[6];
        for(int i=0;i<6;i++)
        {
            var weapon=i==5?DuelWeapon.SawedOff:DuelWeapon.Revolver;
            contexts[i]=new DuelPoseContext{Seat=i,Time=4,Action=actions[i],Weapon=weapon,Active=true,GunReady=i==5,Aiming=i==5,Progress=.5f,ActionAge=1};
            typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{i,weapon});
            var root=Field<Transform>(puppets.GetValue(i),"Root");var gun=Field<Transform>(table,"gun");gun.gameObject.SetActive(true);
            DuelCharacterPose.Preparation(actions[i],weapon,.5f,out var offset,out var euler);
            gun.position=root.TransformPoint(new Vector3(-.35f,1.45f,-.65f)+offset);gun.rotation=root.rotation*Quaternion.Euler(new Vector3(-22,155,0)+euler);
            if(i==5){gun.position=root.TransformPoint(new Vector3(-.28f,1.55f,-.75f));gun.rotation=root.rotation*Quaternion.Euler(0,180,0);}
            var holding=Field<Transform>(table,"holdingHand");holding.gameObject.SetActive(true);
            var loading=Field<Transform>(table,"loadingHand");loading.gameObject.SetActive(i>0&&i<5);
            loading.position=gun.TransformPoint(i==1?new Vector3(.05f,.11f,-.20f):new Vector3(.10f,.06f,-.04f));loading.rotation=gun.rotation*Quaternion.Euler(-10,70,0);
            var latch=Field<Transform>(table,"barrelLatch");latch.localRotation=Quaternion.Euler(0,i>=3?-80:0,0);
        }
        for(int frame=0;frame<45;frame++){for(int i=0;i<6;i++)Frame(contexts[i]);yield return null;}
        for(int i=0;i<6;i++)
        {
            typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{i,contexts[i].Weapon});var p=puppets.GetValue(i);
            var forearm=Field<Transform>(p,"Forearm");var holding=Field<Transform>(table,"holdingHand");
            Bones(i);
            Check(Vector3.Distance(forearm.position+forearm.up*forearm.localScale.y,holding.position)<.001f,"right forearm stays attached to actual grip");
            Check(!Field<Transform>(p,"Hand").gameObject.activeSelf,"only attached grip hand is visible");
            if(i>0&&i<5)
            {var left=Field<Transform>(p,"LeftForearm");Check(Vector3.Distance(left.position+left.up*left.localScale.y,Field<Transform>(table,"loadingHand").position)<.001f,"left forearm stays attached to action contact");}
        }
        yield return Capture("Character-Action-Poses.png",1600,900);
        typeof(RouletteTable).GetMethod("UseRig",F).Invoke(table,new object[]{5,DuelWeapon.SawedOff});
        var viewGun=Field<Transform>(table,"gun");var sightPosition=viewGun.position;var sightRotation=viewGun.rotation;var vm=contexts[5];vm.ViewModel=true;
        for(int frame=0;frame<15;frame++){Frame(vm);yield return null;}
        Check(Vector3.Distance(viewGun.position,sightPosition)<.00001f&&Quaternion.Angle(viewGun.rotation,sightRotation)<.001f,"first person sight placement is not altered by reach fitting");
        Bones(5,true);Check(!Field<Transform>(puppets.GetValue(5),"Upper").GetComponent<Renderer>().enabled,"view model forearm has no stretched shoulder segment");
        foreach(var rig in Field<Array>(table,"rigs"))
        {Field<Transform>(rig,"Gun").gameObject.SetActive(false);Field<Transform>(rig,"Holding").gameObject.SetActive(false);Field<Transform>(rig,"Loading").gameObject.SetActive(false);}
        for(int frame=0;frame<30;frame++){for(int i=0;i<6;i++)Frame(new DuelPoseContext{Seat=i,Time=4});yield return null;}
        for(int i=0;i<6;i++)Check(Field<Transform>(puppets.GetValue(i),"Hand").gameObject.activeSelf,"release returns to visible idle hand");
        Debug.Log("PASS character motion: six idle variants, time motion, cycle continuity, preparation/aim grip contact and release");
    }
    IEnumerator Capture(string name,int w,int h)
    {
        yield return null;var cam=Camera.main;var target=new RenderTexture(w,h,24);target.Create();float before=cam.aspect;cam.aspect=(float)w/h;
        RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(w,h,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"..",name),image.EncodeToPNG());RenderTexture.active=previous;cam.aspect=before;target.Release();Destroy(target);Destroy(image);
    }
    static void Finish(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline){Debug.LogError("FAIL avatar preview timeout");Finish(1);}}
}
