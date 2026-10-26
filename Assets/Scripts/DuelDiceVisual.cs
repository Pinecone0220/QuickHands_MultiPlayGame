using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HelloWorld
{
    // Local render-to-texture model. It has no collider or authority over the rolled number.
    public sealed class DuelDiceVisual:MonoBehaviour
    {
        const int Layer=31;
        Transform rig,die;
        Camera diceCamera,mainCamera;
        int oldMask;
        Mesh mesh;
        Font numberFont;
        Material numberMaterial;
        bool warming;
        readonly List<Material> materials=new List<Material>();
        readonly Quaternion[] faces=new Quaternion[8];
        public RenderTexture Texture {get;private set;}
        public void Initialize(int slot=0,int resolution=384)
        {
            if(rig!=null)return;
            var root=new GameObject("D8 local preview");rig=root.transform;rig.SetParent(transform,false);rig.position=new Vector3(1000+slot*12,1000,1000);
            // Only the main preview owns the scene camera mask; small previews use separate frusta.
            if(slot==0){mainCamera=Camera.main;if(mainCamera!=null){oldMask=mainCamera.cullingMask;mainCamera.cullingMask&=~(1<<Layer);}}
            die=new GameObject("Numbered octahedron").transform;die.SetParent(rig,false);die.gameObject.layer=Layer;
            var vertices=new List<Vector3>();var indices=new List<int[]>();
            var top=Vector3.up;var bottom=Vector3.down;
            var ring=new[]{Vector3.right,Vector3.forward,Vector3.left,Vector3.back};
            numberFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");numberFont.RequestCharactersInTexture("12345678",64);
            numberMaterial=new Material(Resources.Load<Shader>("DuelDiceNumber"));numberMaterial.mainTexture=numberFont.material.mainTexture;
            materials.Add(numberMaterial);Font.textureRebuilt+=RefreshAtlas;
            var faceMaterials=new List<Material>();
            for(int face=0;face<8;face++)
            {
                Vector3 a=face<4?top:bottom,b=ring[face%4],c=ring[(face+1)%4];
                var center=(a+b+c)/3;var normal=Vector3.Cross(b-a,c-a).normalized;
                if(Vector3.Dot(normal,center)<0){var swap=b;b=c;c=swap;normal=-normal;}
                int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(new[]{start,start+1,start+2});
                var material=new Material(Resources.Load<Shader>("DuelDice"));
                float shade=.65f+.35f*Mathf.Abs(Vector3.Dot(normal,new Vector3(-.4f,.8f,-.5f).normalized));
                material.SetColor("_BaseColor",new Color(.74f*shade,.57f*shade,.31f*shade,1));materials.Add(material);faceMaterials.Add(material);
                var label=new GameObject("Face "+(face+1));label.layer=Layer;label.transform.SetParent(die,false);label.transform.localPosition=center+normal*.008f;
                var up=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;
                label.transform.localRotation=Quaternion.LookRotation(-normal,up);faces[face]=Quaternion.Inverse(label.transform.localRotation);
                var text=label.AddComponent<TextMesh>();text.font=numberFont;text.text=(face+1).ToString();text.fontSize=64;text.characterSize=.06f;
                text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.08f,.07f,.045f);
                label.GetComponent<MeshRenderer>().sharedMaterial=numberMaterial;
            }
            mesh=new Mesh{name="D8 eight triangular faces"};mesh.SetVertices(vertices);mesh.subMeshCount=8;
            for(int i=0;i<8;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();
            die.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            die.gameObject.AddComponent<MeshRenderer>().sharedMaterials=faceMaterials.ToArray();
            var cameraGo=new GameObject("D8 texture camera");cameraGo.transform.SetParent(rig,false);cameraGo.transform.localPosition=new Vector3(0,0,-4);
            diceCamera=cameraGo.AddComponent<Camera>();diceCamera.cullingMask=1<<Layer;diceCamera.clearFlags=CameraClearFlags.SolidColor;diceCamera.backgroundColor=Color.clear;
            diceCamera.orthographic=true;diceCamera.orthographicSize=1.32f;diceCamera.nearClipPlane=.1f;diceCamera.farClipPlane=10;diceCamera.allowHDR=false;diceCamera.allowMSAA=false;
            var cameraData=diceCamera.GetUniversalAdditionalCameraData();cameraData.renderShadows=false;cameraData.renderPostProcessing=false;
            cameraData.requiresDepthOption=CameraOverrideOption.Off;cameraData.requiresColorOption=CameraOverrideOption.Off;
            int size=Mathf.Clamp(resolution,64,384);
            Texture=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32){name="D8 UI texture"};Texture.Create();diceCamera.targetTexture=Texture;
            rig.gameObject.SetActive(false);
        }
        void RefreshAtlas(Font font){if(font==numberFont&&numberMaterial!=null)numberMaterial.mainTexture=numberFont.material.mainTexture;}
        IEnumerator Start()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)yield break;
            Initialize();warming=true;rig.gameObject.SetActive(true);
            yield return null;yield return null;warming=false;rig.gameObject.SetActive(false);
        }
        public void Show(bool visible,bool rolling,int face,int seat,double animationTime=-1)
        {
            if(rig==null&&visible)Initialize();if(rig==null||warming&&!visible)return;rig.gameObject.SetActive(visible);if(!visible)return;
            float time=animationTime>=0?(float)animationTime:Time.unscaledTime;
            if(rolling)die.localRotation=Quaternion.Euler(time*241+seat*37,time*317,time*173);
            else if(face>=1&&face<=8)die.localRotation=Quaternion.Slerp(die.localRotation,faces[face-1],1-Mathf.Exp(-Time.unscaledDeltaTime*18));
        }
        void OnDestroy()
        {
            Font.textureRebuilt-=RefreshAtlas;
            if(mainCamera!=null)mainCamera.cullingMask=oldMask;
            if(rig!=null)Destroy(rig.gameObject);if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
            if(Texture!=null){Texture.Release();Destroy(Texture);}
        }
    }
}
