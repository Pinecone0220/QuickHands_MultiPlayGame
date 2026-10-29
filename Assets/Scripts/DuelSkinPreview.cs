using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace HelloWorld
{
    // Isolated, local-only model display; it has no colliders or game state.
    public sealed class DuelSkinPreview : MonoBehaviour
    {
        const int Layer=31;
        Transform rig,model; Camera previewCamera; Light previewLight;
        readonly List<Material> materials=new List<Material>();readonly List<Mesh> previewMeshes=new List<Mesh>();
        public RenderTexture Texture { get; private set; }
        public void Initialize()
        {
            if(rig!=null)return;
            rig=new GameObject("Skin preview rig").transform;rig.SetParent(transform,false);rig.position=new Vector3(1200,1200,1200);
            var cameraObject=new GameObject("Skin preview camera");cameraObject.transform.SetParent(rig,false);cameraObject.transform.localPosition=new Vector3(0,0,-4);
            previewCamera=cameraObject.AddComponent<Camera>();previewCamera.cullingMask=1<<Layer;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;
            previewCamera.orthographic=true;previewCamera.orthographicSize=.85f;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=10;previewCamera.allowHDR=false;previewCamera.allowMSAA=false;
            var data=previewCamera.GetUniversalAdditionalCameraData();data.renderShadows=false;data.renderPostProcessing=false;data.requiresDepthOption=CameraOverrideOption.Off;data.requiresColorOption=CameraOverrideOption.Off;
            var lightObject=new GameObject("Skin preview light");lightObject.transform.SetParent(rig,false);lightObject.transform.localRotation=Quaternion.Euler(35,-30,0);previewLight=lightObject.AddComponent<Light>();previewLight.type=LightType.Directional;previewLight.cullingMask=1<<Layer;previewLight.intensity=1.6f;
            Texture=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){name="Item skin preview"};Texture.Create();previewCamera.targetTexture=Texture;rig.gameObject.SetActive(false);
        }
        static void SetLayer(Transform t){t.gameObject.layer=Layer;foreach(Transform child in t)SetLayer(child);}
        Material Mat(Color color,float metallic=0,float smoothness=.35f){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);materials.Add(m);return m;}
        Transform Part(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;Destroy(go.GetComponent<Collider>());go.transform.SetParent(model,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        Mesh Shape(string name,params Vector4[] sections)
        {var mesh=DuelFacetedMesh.Sections(name,sections);previewMeshes.Add(mesh);return mesh;}
        Transform MeshPart(string name,Mesh mesh,Vector3 position,Material material)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(model,false);go.transform.localPosition=position;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;}
        public void Show(DuelItem item,int skin)
        {
            Initialize();if(model!=null)Destroy(model.gameObject);foreach(var m in materials)if(m!=null)Destroy(m);materials.Clear();foreach(var mesh in previewMeshes)if(mesh!=null)Destroy(mesh);previewMeshes.Clear();
            model=new GameObject("Preview item").transform;model.SetParent(rig,false);
            Color tint=skin==0?(item==DuelItem.Recovery?new Color(.1f,.84f,.4f):item==DuelItem.Bullet?new Color(.72f,.51f,.19f):new Color(.09f,.16f,.21f)):DuelSkins.Tint(skin);
            GameObject original=null;
            if(item==DuelItem.Taser)original=GetComponent<RouletteTable>()?.CloneTaserForPreview();
            if(item==DuelItem.Ink)original=GetComponent<DuelInkVisuals>()?.ClonePouchForPreview();
            if(item==DuelItem.NoruFace)original=GetComponent<RouletteTable>()?.CloneNoruForPreview();
            if(original!=null)
            {
                original.transform.SetParent(model,false);original.transform.localPosition=item==DuelItem.Taser?new Vector3(0,.12f,0):Vector3.zero;original.transform.localRotation=Quaternion.identity;original.transform.localScale=item==DuelItem.Taser?Vector3.one*1.65f:item==DuelItem.Ink?Vector3.one*4f:Vector3.one;
                foreach(var renderer in original.GetComponentsInChildren<Renderer>(true))
                {
                    bool colored=renderer.sharedMaterial!=null&&renderer.sharedMaterial.name.Contains(item==DuelItem.Taser?"Taser safety yellow":item==DuelItem.Ink?"Ink pouch fabric":"NoruFace padded fabric");
                    if(colored&&skin>0){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",DuelSkins.Tint(skin)*(item==DuelItem.Ink?.55f:item==DuelItem.NoruFace?.5f:1f));renderer.SetPropertyBlock(block);}
                }
            }
            else if(item==DuelItem.Bullet)
            {
                var body=Mat(tint,.65f,.52f);var tip=Mat(new Color(.70f,.38f,.20f),.65f,.48f);var dark=Mat(new Color(.16f,.14f,.11f),.3f,.3f);
                var shell=Shape("Faceted cartridge case",new Vector4(-.39f,.10f,.10f,0),new Vector4(-.36f,.115f,.115f,0),new Vector4(-.31f,.10f,.10f,0),new Vector4(.19f,.10f,.10f,0),new Vector4(.23f,.085f,.085f,0));
                var projectile=Shape("Tapered bullet",new Vector4(.22f,.085f,.085f,0),new Vector4(.31f,.078f,.078f,0),new Vector4(.42f,.040f,.040f,0),new Vector4(.46f,.003f,.003f,0));
                for(int i=0;i<2;i++)
                {
                    float x=(i-.5f)*.36f;MeshPart("Cartridge case",shell,new Vector3(x,0,0),body);MeshPart("Bullet tip",projectile,new Vector3(x,0,0),tip);
                    Part("Cartridge rim",PrimitiveType.Cylinder,new Vector3(x,-.375f,0),new Vector3(.225f,.014f,.225f),body);
                    Part("Primer",PrimitiveType.Cylinder,new Vector3(x,-.395f,0),new Vector3(.065f,.005f,.065f),dark);
                    Part("Case highlight",PrimitiveType.Cube,new Vector3(x-.046f,-.04f,-.083f),new Vector3(.015f,.40f,.006f),Mat(Color.Lerp(tint,Color.white,.28f),.45f,.6f));
                }
            }
            else if(item==DuelItem.Recovery)
            {
                var vial=Mat(tint,.12f,.55f);var liquid=Mat(Color.Lerp(tint,Color.white,.22f),.05f,.6f);var cap=Mat(new Color(.68f,.69f,.62f),.45f,.5f);var dark=Mat(new Color(.10f,.15f,.14f),.05f,.3f);var cross=Mat(new Color(.94f,.98f,.91f),.05f,.35f);
                var glass=Shape("Ampoule rounded body",new Vector4(-.39f,.16f,.13f,0),new Vector4(-.35f,.22f,.17f,0),new Vector4(.29f,.22f,.17f,0),new Vector4(.36f,.15f,.12f,0));
                MeshPart("Ampoule body",glass,Vector3.zero,vial);
                Part("Ampoule neck",PrimitiveType.Cylinder,new Vector3(0,.365f,0),new Vector3(.27f,.055f,.24f),liquid);
                Part("Ampoule metal cap",PrimitiveType.Cylinder,new Vector3(0,.445f,0),new Vector3(.31f,.04f,.28f),cap);
                Part("Cap top",PrimitiveType.Cylinder,new Vector3(0,.488f,0),new Vector3(.26f,.008f,.24f),dark);
                Part("Label backing",PrimitiveType.Cube,new Vector3(0,-.025f,-.163f),new Vector3(.27f,.34f,.012f),liquid);
                Part("Cross upright",PrimitiveType.Cube,new Vector3(0,-.025f,-.173f),new Vector3(.07f,.23f,.012f),cross);
                Part("Cross bar",PrimitiveType.Cube,new Vector3(0,-.025f,-.175f),new Vector3(.20f,.07f,.012f),cross);
                Part("Rear label backing",PrimitiveType.Cube,new Vector3(0,-.025f,.163f),new Vector3(.27f,.34f,.012f),liquid);
                Part("Rear cross upright",PrimitiveType.Cube,new Vector3(0,-.025f,.173f),new Vector3(.07f,.23f,.012f),cross);
                Part("Rear cross bar",PrimitiveType.Cube,new Vector3(0,-.025f,.175f),new Vector3(.20f,.07f,.012f),cross);
                Part("Glass reflection",PrimitiveType.Cube,new Vector3(-.125f,.02f,-.135f),new Vector3(.018f,.48f,.01f),cross);
                foreach(float y in new[]{-.29f,.24f})Part("Ampoule band",PrimitiveType.Cube,new Vector3(0,y,-.16f),new Vector3(.28f,.012f,.01f),cap);
            }
            else
            {
                var jacket=Mat(tint*.5f);var zipper=Mat(new Color(.55f,.58f,.58f));
                for(int row=0;row<5;row++)Part("Padded body",PrimitiveType.Cube,new Vector3(0,(row-2)*.16f,0),new Vector3(.68f,.15f,.31f),jacket);
                foreach(float side in new[]{-1f,1f})Part("Padded sleeve",PrimitiveType.Cube,new Vector3(side*.46f,-.1f,0),new Vector3(.22f,.65f,.27f),jacket);
                Part("Zipper",PrimitiveType.Cube,new Vector3(0,0,-.16f),new Vector3(.025f,.76f,.02f),zipper);
            }
            SetLayer(model);rig.gameObject.SetActive(true);
        }
        public void ShowWeapon(DuelWeapon weapon,int skin)
        {
            Initialize();if(model!=null)Destroy(model.gameObject);foreach(var m in materials)if(m!=null)Destroy(m);materials.Clear();foreach(var mesh in previewMeshes)if(mesh!=null)Destroy(mesh);previewMeshes.Clear();
            model=new GameObject("Preview weapon").transform;model.SetParent(rig,false);
            var original=GetComponent<RouletteTable>()?.CloneWeaponForPreview(weapon,skin);
            if(original!=null)
            {
                original.transform.SetParent(model,false);original.transform.localRotation=Quaternion.Euler(0,90,0);
                original.transform.localPosition=new Vector3(weapon==DuelWeapon.BAS?-.12f:-.08f,.08f,0);
                original.transform.localScale=Vector3.one*(weapon==DuelWeapon.BAS?.65f:weapon==DuelWeapon.Revolver?1.15f:.95f);
                SetLayer(model);
            }
            rig.gameObject.SetActive(true);
        }
        public void Hide(){if(rig!=null)rig.gameObject.SetActive(false);}
        void Update(){if(model!=null&&rig!=null&&rig.gameObject.activeSelf)model.localRotation=Quaternion.Euler(-10,Time.unscaledTime*25,0);}
        void OnDestroy(){if(rig!=null)Destroy(rig.gameObject);foreach(var m in materials)if(m!=null)Destroy(m);foreach(var mesh in previewMeshes)if(mesh!=null)Destroy(mesh);if(Texture!=null){Texture.Release();Destroy(Texture);}}
    }
}
