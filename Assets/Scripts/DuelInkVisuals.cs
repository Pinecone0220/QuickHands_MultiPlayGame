using System.Collections.Generic;
using UnityEngine;
namespace HelloWorld
{
    [DefaultExecutionOrder(100)]
    public sealed class DuelInkVisuals:MonoBehaviour
    {
        private RouletteTable table;private Transform root;
        private readonly Transform[] held=new Transform[6],flight=new Transform[6],coats=new Transform[6];
        private readonly Transform[,] drops=new Transform[6,24];
        private readonly Material[] coatMaterials=new Material[6],dropMaterials=new Material[6];
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Mesh> stainMeshes=new List<Mesh>();
        private readonly DuelInkFade[] coatFades=new DuelInkFade[6];
        private Texture2D stainTexture;private Mesh pouchMesh;
        private Material fabric,seam;
        private readonly int[] skinCache={-1,-1,-1,-1,-1,-1};
        private static Vector3 V(AimVector v)=>new Vector3(v.X,v.Y,v.Z);
        private Material Mat(string name,Color color,bool transparent=false)
        {
            var m=new Material(transparent?Resources.Load<Shader>("DuelInk"):Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);
            materials.Add(m);return m;
        }
        private Transform Block(string name,Transform parent,Vector3 position,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Destroy(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        private Transform Pouch(string name)
        {
            var pouch=new GameObject(name).transform;pouch.SetParent(root,false);
            var body=new GameObject("Ink pouch",typeof(MeshFilter),typeof(MeshRenderer));body.transform.SetParent(pouch,false);body.GetComponent<MeshFilter>().sharedMesh=pouchMesh;body.GetComponent<MeshRenderer>().sharedMaterial=fabric;body.transform.localRotation=Quaternion.Euler(0,0,-5);
            Block("Folded shoulder",pouch,new Vector3(0,.09f,0),new Vector3(.13f,.05f,.09f),fabric);
            Block("Tied cord",pouch,new Vector3(0,.115f,0),new Vector3(.10f,.025f,.085f),seam);
            Block("Gathered fabric",pouch,new Vector3(0,.15f,0),new Vector3(.075f,.055f,.06f),fabric).localRotation=Quaternion.Euler(0,0,12);
            foreach(float side in new[]{-1f,1f})
            {
                var fold=Block("Fabric fold",pouch,new Vector3(side*.055f,-.012f,-.043f),new Vector3(.010f,.13f,.008f),fabric);fold.localRotation=Quaternion.Euler(0,0,side*8);
                var knot=Block("Loose cord end",pouch,new Vector3(side*.045f,.105f,-.047f),new Vector3(.014f,.075f,.012f),seam);knot.localRotation=Quaternion.Euler(0,0,side*30);
                for(int i=0;i<4;i++)Block("Stitch",pouch,new Vector3(side*.058f,-.065f+i*.038f,-.046f),new Vector3(.007f,.014f,.005f),seam);
            }
            Block("Knot",pouch,new Vector3(0,.12f,-.052f),new Vector3(.035f,.03f,.02f),seam);
            return pouch;
        }
        public GameObject ClonePouchForPreview()
        {
            if(held[0]==null)return null;
            var clone=Instantiate(held[0].gameObject);clone.SetActive(true);return clone;
        }
        private void Start()
        {
            table=GetComponent<RouletteTable>();root=new GameObject("Ink pouches and splashes - presentation").transform;root.SetParent(transform,false);
            stainTexture=DuelInkFace.Texture();
            fabric=Mat("Ink pouch fabric",new Color(.22f,.10f,.31f));seam=Mat("Ink pouch cord",new Color(.63f,.48f,.25f));
            pouchMesh=DuelFacetedMesh.Sections("Ink pouch sculpted cloth",new Vector4(-.10f,.046f,.033f,0),new Vector4(-.08f,.077f,.05f,0),new Vector4(.015f,.084f,.055f,0),new Vector4(.08f,.061f,.043f,0),new Vector4(.10f,.040f,.03f,0));
            for(int seat=0;seat<6;seat++)
            {
                held[seat]=Pouch("Held ink pouch "+seat);flight[seat]=Pouch("Flying ink pouch "+seat);
                coats[seat]=new GameObject("Ink stains "+seat).transform;coats[seat].SetParent(table.InkHead(seat),false);coatMaterials[seat]=Mat("Ink stain "+seat,new Color(.025f,.008f,.05f,1),true);dropMaterials[seat]=Mat("Ink burst "+seat,new Color(.035f,.012f,.06f,1),true);
                var skin=table.InkHead(seat).Find("Head").GetComponent<MeshFilter>();
                var stain=DuelInkFace.Surface(skin.sharedMesh);stainMeshes.Add(stain);
                var face=new GameObject("Face ink surface",typeof(MeshFilter),typeof(MeshRenderer));face.transform.SetParent(coats[seat],false);
                face.transform.localPosition=skin.transform.localPosition;face.transform.localRotation=skin.transform.localRotation;face.transform.localScale=skin.transform.localScale;
                face.GetComponent<MeshFilter>().sharedMesh=stain;face.GetComponent<MeshRenderer>().sharedMaterial=coatMaterials[seat];
                coatMaterials[seat].SetTexture("_BaseMap",stainTexture);
                for(int i=0;i<24;i++)
                {
                    drops[seat,i]=Block("Burst droplet",root,Vector3.zero,Vector3.one*.045f,dropMaterials[seat]);
                }
            }
        }
        private void LateUpdate()
        {
            if(root==null)return;var p=HelloWorldPlayer.Local;bool valid=p!=null&&p.IsSpawned&&p.Seat.Value>=0;
            var state=valid?p.State.Value:default;double now=valid?p.NetworkManager.ServerTime.Time:0;var fps=GetComponent<DuelFirstPerson>();var spectator=GetComponent<DuelSpectator>();
            for(int seat=0;seat<6;seat++)
            {
                int inkSkin=valid?DuelSkins.FromPacked(HelloWorldPlayer.SkinsForSeat(seat),(int)DuelItem.Ink):0;
                if(skinCache[seat]!=inkSkin){skinCache[seat]=inkSkin;var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",inkSkin==0?new Color(.22f,.10f,.31f):DuelSkins.Tint(inkSkin)*.55f);foreach(var pouch in new[]{held[seat],flight[seat]})foreach(var renderer in pouch.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial==fabric)renderer.SetPropertyBlock(block);}
                var a=state.ForSeat(seat);var ink=a.Ink;double age=now-ink.ThrownAt;
                bool practice=valid&&state.IsPracticing(seat);
                bool preview=valid&&state.CanPreviewEquipment(seat)&&a.PreviewItem==(int)DuelWheelSlot.Ink;
                bool showing=preview||practice||valid&&state.Phase==DuelPhase.Playing&&state.Alive(seat)&&state.Stage==DuelStage.Racing&&(DuelInk.Usable(state.Clock.Phase)||state.Clock.Phase==ClockPhase.QuickCountdown);
                bool local=valid&&seat==p.Seat.Value;bool holding=preview||showing&&(ink.Armed||ink.ThrownAt>=0&&age>=0&&age<DuelInk.ReleaseDelay);
                held[seat].gameObject.SetActive(holding);
                float yaw=local?fps.Yaw:a.AimYaw,pitch=local?fps.Pitch:a.AimPitch;
                Vector3 hand=table.InkHand(seat).position;
                var facing=Quaternion.Euler(-pitch,yaw,0);held[seat].position=hand+facing*new Vector3(0,.07f,.035f);held[seat].rotation=facing*Quaternion.Euler(-8,0,-12);
                bool flying=showing&&ink.ThrownAt>=0&&age>=DuelInk.ReleaseDelay&&age<DuelInk.ReleaseDelay+DuelInk.Lifetime&&ink.ImpactAt<ink.ThrownAt;
                flight[seat].gameObject.SetActive(flying);if(flying){flight[seat].position=V(DuelInk.Position(ink,now));flight[seat].rotation=Quaternion.Euler((float)age*340,(float)age*140,seat*37);}
                float coat=0;
                if(valid&&(practice||state.Alive(seat)))coat=coatFades[seat].Observe(ink,now,practice?DuelPhase.Playing:state.Phase,practice?ClockPhase.Shooting:state.Clock.Phase,state.Clock.PhaseAt,state.StageStartedAt);
                else coatFades[seat]=default;
                                coats[seat].gameObject.SetActive(coat>0&&!local&&!(spectator!=null&&spectator.Watching&&spectator.TargetSeat==seat));
                int stainSkin=valid?DuelSkinEffects.SourceSkin(state,seat,DuelItem.Ink):0;Color stain=stainSkin>0?DuelSkins.Tint(stainSkin)*.70f:new Color(.025f,.008f,.05f);stain.a=coat;coatMaterials[seat].SetColor("_BaseColor",stain);
                float impact=ink.ImpactAt>=0?(float)(now-ink.ImpactAt):10;bool bursting=valid&&impact>=0&&impact<.45f;
                for(int i=0;i<24;i++)
                {
                    var d=drops[seat,i];int tier=DuelSkinEffects.Tier(inkSkin);bool visible=bursting&&(i<8||tier>=3&&i<16||tier>=4);d.gameObject.SetActive(visible);
                    if(visible){var dir=new Vector3(Mathf.Sin(i*2.7f+seat),.5f+Mathf.Sin(i*4.3f)*.4f,Mathf.Cos(i*2.7f+seat));d.position=V(ink.Impact)+dir*impact*(i<8?1.3f:1.8f)+Vector3.down*impact*impact*2;d.localScale=Vector3.one*Mathf.Lerp(DuelSkinEffects.Tier(inkSkin)>0?.12f:.075f,.015f,impact/.45f);}
                }
                Color drop=inkSkin>0?DuelSkins.Tint(inkSkin)*.65f:new Color(.035f,.012f,.06f);drop.a=Mathf.Clamp01(1-impact/.45f);dropMaterials[seat].SetColor("_BaseColor",drop);
            }
        }
        private void OnDestroy()
        {
            if(root!=null)Destroy(root.gameObject);foreach(var coat in coats)if(coat!=null)Destroy(coat.gameObject);
            foreach(var m in materials)if(m!=null)Destroy(m);foreach(var mesh in stainMeshes)if(mesh!=null)Destroy(mesh);
            if(stainTexture!=null)Destroy(stainTexture);if(pouchMesh!=null)Destroy(pouchMesh);
        }
    }
}
