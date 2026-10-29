using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace HelloWorld
{
    // Local render-only roulette. The selected reward remains owned by the UI logic.
    public sealed class DuelGachaWheel3D : MonoBehaviour
    {
        const int Layer=31,Segments=20;
        readonly List<Material> materials=new List<Material>();readonly List<Mesh> meshes=new List<Mesh>();
        Transform rig,wheel,ball;Camera wheelCamera;Font font;Material textMaterial;bool ballSettling;float ballTargetOffset;
        public RenderTexture Texture { get; private set; }
        public int LandingSegment { get; private set; }=-1;
        Material Mat(string name,Color color,float metallic=0)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.45f);materials.Add(m);return m;
        }
        Transform Part(string name,PrimitiveType primitive,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(primitive);go.name=name;go.layer=Layer;Destroy(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        static Vector3 Point(float angle,float radius,float y)=>new Vector3(Mathf.Sin(angle)*radius,y,-Mathf.Cos(angle)*radius);
        void RingSection(int i,Material mat)
        {
            float a=i*Mathf.PI*2/Segments,b=(i+1)*Mathf.PI*2/Segments;
            var mesh=new Mesh{name="Roulette sector "+i};mesh.vertices=new[]{Point(a,.74f,.14f),Point(a,1.05f,.14f),Point(b,1.05f,.14f),Point(b,.74f,.14f)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();meshes.Add(mesh);
            var go=new GameObject("Sector "+i,typeof(MeshFilter),typeof(MeshRenderer));go.layer=Layer;go.transform.SetParent(wheel,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
            var divider=Part("Pocket divider",PrimitiveType.Cube,wheel,Point(a,.9f,.165f),new Vector3(.012f,.045f,.31f),materials[2]);divider.localRotation=Quaternion.Euler(0,-i*360f/Segments,0);
            var label=new GameObject("Grade "+i);label.layer=Layer;label.transform.SetParent(wheel,false);label.transform.localPosition=Point((a+b)*.5f,.89f,.172f);label.transform.localRotation=Quaternion.Euler(90,-(i+.5f)*360f/Segments,0);
            var text=label.AddComponent<TextMesh>();text.font=font;text.text=DuelSkins.GradeName(DuelSkins.Grade(i%10+1));text.fontSize=64;text.characterSize=text.text.Length>3?.012f:.020f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=Color.white;label.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;
        }
        public void Initialize()
        {
            if(rig!=null)return;rig=new GameObject("3D gacha roulette preview").transform;rig.SetParent(transform,false);rig.position=new Vector3(1400,1400,1400);
            wheel=new GameObject("Roulette wheel").transform;wheel.SetParent(rig,false);wheel.gameObject.layer=Layer;
            var dark=Mat("Dark walnut",new Color(.18f,.07f,.028f));var wood=Mat("Polished wood",new Color(.36f,.15f,.045f));var gold=Mat("Brass",new Color(.75f,.53f,.20f),.7f);var ivory=Mat("Roulette ball",new Color(.94f,.90f,.78f));
            Part("Roulette base",PrimitiveType.Cylinder,wheel,new Vector3(0,-.07f,0),new Vector3(2.5f,.11f,2.5f),dark);
            Part("Wood ring",PrimitiveType.Cylinder,wheel,new Vector3(0,.04f,0),new Vector3(2.42f,.07f,2.42f),wood);
            Part("Outer brass ring",PrimitiveType.Cylinder,wheel,new Vector3(0,.10f,0),new Vector3(2.28f,.025f,2.28f),gold);
            Part("Pocket backing",PrimitiveType.Cylinder,wheel,new Vector3(0,.126f,0),new Vector3(2.17f,.012f,2.17f),dark);
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture("NRS Mythic",64);textMaterial=new Material(Resources.Load<Shader>("DuelDiceNumber"));textMaterial.mainTexture=font.material.mainTexture;materials.Add(textMaterial);Font.textureRebuilt+=RefreshFont;
            var grades=new Material[5];for(int i=0;i<5;i++)grades[i]=Mat("Grade "+i,Color.Lerp(new Color(.025f,.03f,.035f),DuelSkins.GradeColor(i),.48f));
            for(int i=0;i<Segments;i++)RingSection(i,grades[DuelSkins.Grade(i%10+1)]);
            Part("Inner metal ring",PrimitiveType.Cylinder,wheel,new Vector3(0,.145f,0),new Vector3(1.44f,.015f,1.44f),gold);
            Part("Inner wood cone",PrimitiveType.Cylinder,wheel,new Vector3(0,.16f,0),new Vector3(1.36f,.025f,1.36f),wood);
            Part("Center spindle base",PrimitiveType.Cylinder,wheel,new Vector3(0,.21f,0),new Vector3(.20f,.05f,.20f),gold);
            Part("Center spindle",PrimitiveType.Cylinder,wheel,new Vector3(0,.35f,0),new Vector3(.09f,.12f,.09f),gold);
            Part("Center cap",PrimitiveType.Sphere,wheel,new Vector3(0,.48f,0),new Vector3(.16f,.1f,.16f),gold);
            ball=Part("Ivory ball",PrimitiveType.Sphere,rig,Vector3.zero,Vector3.one*.10f,ivory);ball.gameObject.SetActive(false);
            var lightObj=new GameObject("Roulette light");lightObj.transform.SetParent(rig,false);lightObj.transform.localRotation=Quaternion.Euler(48,-25,0);var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<Layer;light.intensity=1.5f;
            var cameraObj=new GameObject("Roulette camera");cameraObj.transform.SetParent(rig,false);cameraObj.transform.localPosition=new Vector3(0,3.5f,-4.2f);cameraObj.transform.LookAt(rig.position+Vector3.up*.12f);
            wheelCamera=cameraObj.AddComponent<Camera>();wheelCamera.cullingMask=1<<Layer;wheelCamera.clearFlags=CameraClearFlags.SolidColor;wheelCamera.backgroundColor=Color.clear;wheelCamera.orthographic=true;wheelCamera.orthographicSize=1.58f;wheelCamera.nearClipPlane=.1f;wheelCamera.farClipPlane=12;wheelCamera.allowHDR=false;wheelCamera.allowMSAA=false;
            var data=wheelCamera.GetUniversalAdditionalCameraData();data.renderShadows=false;data.renderPostProcessing=false;data.requiresDepthOption=CameraOverrideOption.Off;data.requiresColorOption=CameraOverrideOption.Off;
            Texture=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32){name="3D roulette"};Texture.Create();wheelCamera.targetTexture=Texture;rig.gameObject.SetActive(false);
        }
        void RefreshFont(Font updated){if(updated==font&&textMaterial!=null)textMaterial.mainTexture=font.material.mainTexture;}
        public void SetVisible(bool visible){if(visible)Initialize();if(rig!=null)rig.gameObject.SetActive(visible);}
        public void SetAngle(float angle){if(wheel!=null)wheel.localRotation=Quaternion.Euler(0,-angle,0);}
        public void PrepareSpin(){LandingSegment=-1;if(ball!=null)ball.gameObject.SetActive(false);}
        public void BeginBall(int segment){LandingSegment=Mathf.Clamp(segment,0,Segments-1);ballSettling=false;ball.SetParent(rig,false);ball.gameObject.SetActive(true);ball.localPosition=new Vector3(1.5f,1.3f,-.3f);}
        public void AnimateBall(float progress,float angle)
        {
            if(ball==null||LandingSegment<0)return;ball.SetParent(rig,false);
            // Circle the outer track, slow down, then follow the chosen pocket without cutting through the centre.
            float targetDegrees=(LandingSegment+.5f)*360f/Segments+angle;
            float rollTime=Mathf.Clamp01((progress-.16f)/.84f);
            float orbitDegrees=90f-1080f*(1-Mathf.Pow(1-rollTime,2));
            float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1f,progress));
            if(progress>=.72f&&!ballSettling){ballSettling=true;ballTargetOffset=Mathf.DeltaAngle(orbitDegrees,targetDegrees)-(targetDegrees-orbitDegrees);}
            float degrees=Mathf.Lerp(orbitDegrees,targetDegrees+ballTargetOffset,settle);
            float radius=Mathf.Lerp(1.10f,.91f,settle);
            float bounce=progress>=.16f?Mathf.Abs(Mathf.Sin((progress-.16f)*55f))*.025f*Mathf.Clamp01((.38f-progress)/.22f):0;
            Vector3 rolling=Point(degrees*Mathf.Deg2Rad,radius,.195f+bounce);
            if(progress<.16f){float t=Mathf.SmoothStep(0,1,progress/.16f);ball.localPosition=Vector3.Lerp(new Vector3(1.5f,1.3f,-.3f),rolling,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.22f);}
            else ball.localPosition=rolling;
            ball.localRotation=Quaternion.Euler(0,0,-orbitDegrees*4);
        }
        public void LandBall(){if(ball==null||LandingSegment<0)return;ball.SetParent(wheel,false);ball.localPosition=Point((LandingSegment+.5f)*Mathf.PI*2/Segments,.91f,.195f);}
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;if(rig!=null)Destroy(rig.gameObject);foreach(var m in materials)if(m!=null)Destroy(m);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);if(Texture!=null){Texture.Release();Destroy(Texture);}}
    }
}
