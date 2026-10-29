using UnityEngine;
namespace HelloWorld
{
    // Cosmetics are kept on this device; zero is the original appearance.
    public static class DuelSkins
    {
        public const int Colors=10;
        private static readonly string[] names={"기본", "코발트", "루비", "사파이어", "에메랄드", "자수정", "황금", "진주", "흑요석", "산호", "청록"};
        private static readonly Color[] colors={Color.white,new Color(.25f,.75f,1),new Color(.94f,.12f,.15f),new Color(.12f,.3f,.95f),new Color(.12f,.8f,.38f),new Color(.65f,.24f,.85f),new Color(1,.73f,.18f),new Color(.88f,.90f,.94f),new Color(.23f,.25f,.31f),new Color(1,.42f,.34f),new Color(.06f,.8f,.75f)};
        public static string ColorName(int color)=>names[Mathf.Clamp(color,0,Colors)];
        public static Color Tint(int color)=>colors[Mathf.Clamp(color,0,Colors)];
        public static int Grade(int color) { int c=Mathf.Clamp(color,1,Colors);return c==6||c==7?4:c==9?2:c==10?3:(c-1)/2; }
        public static string GradeName(int grade)=>new[]{"N","R","SR","SSR","Mythic"}[Mathf.Clamp(grade,0,4)];
        public static Color GradeColor(int grade)=>new[]{new Color(.7f,.72f,.72f),new Color(.28f,.55f,1),new Color(.72f,.4f,1),new Color(1,.79f,.25f),new Color(1,.25f,.22f)}[Mathf.Clamp(grade,0,4)];
        private static string Key(int item,int color)=>"qh.skin."+item+"."+color;
        public static bool Owned(int item,int color)=>color==0||PlayerPrefs.GetInt(Key(item,color),0)==1;
        public static bool AwardPending(int item,int color){bool duplicate=Owned(item,color);PlayerPrefs.SetInt(Key(item,color),1);return duplicate;}
        public static bool Award(int item,int color){bool duplicate=AwardPending(item,color);PlayerPrefs.Save();return duplicate;}
        public static int Equipped(int item){int c=PlayerPrefs.GetInt("qh.skin.equipped."+item,0);return c>=0&&c<=Colors&&Owned(item,c)?c:0;}
        public static void Equip(int item,int color){if(color<0||color>Colors||!Owned(item,color))return;PlayerPrefs.SetInt("qh.skin.equipped."+item,color);PlayerPrefs.Save();}
        public const int WeaponSkinCount=8;
        public static readonly string[] WeaponSkinNames={"기본","블루 스틸","적동","황혼의 무법자","빙하의 서약","심연의 별","황금 장미","태양의 왕관","황혼의 페이드"};
        public static readonly int[] WeaponGrades={0,0,0,1,1,2,3,4,4};
        public static int WeaponGrade(int skin)=>WeaponGrades[Mathf.Clamp(skin,0,WeaponSkinCount)];
        public static string WeaponSkinName(int skin)=>WeaponSkinNames[Mathf.Clamp(skin,0,WeaponSkinCount)];
        public static Color WeaponTint(int skin)
        {
            switch(skin){case 1:return new Color(.25f,.39f,.55f);case 2:return new Color(.66f,.27f,.15f);case 3:return new Color(.42f,.18f,.26f);case 4:return new Color(.26f,.55f,.67f);case 5:return new Color(.24f,.20f,.48f);case 6:return new Color(.67f,.43f,.21f);case 7:return new Color(.79f,.58f,.18f);case 8:return new Color(.93f,.34f,.53f);default:return Color.white;}
        }
        public static Color FadeColor(float t)
        {
            t=Mathf.Clamp01(t);Color gold=new Color(1f,.73f,.22f),rose=new Color(.98f,.25f,.48f),violet=new Color(.43f,.16f,.78f);
            return t<.48f?Color.Lerp(gold,rose,Mathf.SmoothStep(0,1,t/.48f)):Color.Lerp(rose,violet,Mathf.SmoothStep(0,1,(t-.48f)/.52f));
        }
        public static int WeaponEquipped(int weapon)=>Equipped(DuelLoadout.ItemCount+weapon);
        public static bool WeaponOwned(int weapon,int skin)=>Owned(DuelLoadout.ItemCount+weapon,skin);
        public static bool AwardWeapon(int weapon,int skin)=>Award(DuelLoadout.ItemCount+weapon,skin);
        public static void EquipWeapon(int weapon,int skin){if(weapon>=0&&weapon<WeaponRules.Selectable.Length&&skin>=0&&skin<=WeaponSkinCount)Equip(DuelLoadout.ItemCount+weapon,skin);}
        public static int WeaponFromPacked(int packed,int weapon){int c=(packed>>((DuelLoadout.ItemCount+weapon)*4))&15;return c<=WeaponSkinCount?c:0;}
        public static int Packed(){int packed=0;for(int i=0;i<DuelLoadout.ItemCount+WeaponRules.Selectable.Length;i++)packed|=Equipped(i)<<(i*4);return packed;}
        public static int FromPacked(int packed,int item){int c=(packed>>(item*4))&15;return c<=Colors?c:0;}
        public static void TintIcon(Texture2D target,DuelItem item,int color)
        {
            var original=DuelItemArt.CreateIcon(item);var px=original.GetPixels();Object.Destroy(original);
            if(color>0){Color tint=Tint(color);for(int i=0;i<px.Length;i++){var c=px[i];if(c.a<.01f)continue;Color.RGBToHSV(c,out float h,out float s,out float v);if(s<.18f||v<.08f)continue;px[i]=new Color(tint.r*v,tint.g*v,tint.b*v,c.a);}}
            target.SetPixels(px);target.Apply();
        }
    }
}


namespace HelloWorld
{
    // Owns the temporary UV-mapped meshes for one procedural gun model.
    public sealed class DuelWeaponFade : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<MeshFilter> filters=new System.Collections.Generic.List<MeshFilter>();
        private readonly System.Collections.Generic.List<Mesh> originals=new System.Collections.Generic.List<Mesh>();
        private readonly System.Collections.Generic.List<Mesh> copies=new System.Collections.Generic.List<Mesh>();
        private Texture2D gradient;
        private Texture2D Gradient()
        {
            if(gradient!=null)return gradient;
            gradient=new Texture2D(256,2,TextureFormat.RGBA32,false){name="Weapon fade gradient",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[512];for(int x=0;x<256;x++)pixels[x]=pixels[x+256]=DuelSkins.FadeColor(x/255f);
            gradient.SetPixels(pixels);gradient.Apply(false,true);return gradient;
        }
        public void Apply(Renderer renderer,Transform gun,DuelWeapon weapon,MaterialPropertyBlock block)
        {
            float min=weapon==DuelWeapon.BAS?-.95f:weapon==DuelWeapon.SawedOff?-.60f:-.45f;
            float max=weapon==DuelWeapon.BAS?1.35f:weapon==DuelWeapon.SawedOff?.90f:.73f;
            var filter=renderer.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
            if(mesh==null||!mesh.isReadable)
            {
                float z=gun.InverseTransformPoint(renderer.bounds.center).z;
                block.SetColor("_BaseColor",DuelSkins.FadeColor((z-min)/(max-min)));return;
            }
            var copy=Instantiate(mesh);copy.name=mesh.name+" fade UV";
            var vertices=copy.vertices;var uv=new Vector2[vertices.Length];var toGun=gun.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int i=0;i<vertices.Length;i++)uv[i]=new Vector2(Mathf.Clamp01((toGun.MultiplyPoint3x4(vertices[i]).z-min)/(max-min)),.5f);
            copy.uv=uv;filters.Add(filter);originals.Add(mesh);copies.Add(copy);filter.sharedMesh=copy;
            block.SetTexture("_BaseMap",Gradient());block.SetColor("_BaseColor",Color.white);
        }
        public void Clear()
        {
            for(int i=0;i<copies.Count;i++){if(filters[i]!=null&&filters[i].sharedMesh==copies[i])filters[i].sharedMesh=originals[i];if(copies[i]!=null)Destroy(copies[i]);}
            filters.Clear();originals.Clear();copies.Clear();if(gradient!=null)Destroy(gradient);gradient=null;
        }
        private void OnDestroy()=>Clear();
    }
    // Cosmetic treatment of the existing firearm; geometry never participates in hit detection.
    internal static class DuelWeaponSkins
    {
        private static bool Excluded(Transform t,Transform root)
        {
            while(t!=null&&t!=root)
            {
                string n=t.name.ToLowerInvariant();
                if(n.Contains("hand")||n.Contains("finger")||n.Contains("bas right grip")||n.Contains("bas left support")||n.Contains("cartridge")||n.Contains("chamber")||n.Contains("bullet")||n.Contains("muzzle")||n.Contains("flash")||n.Contains("bore")||n.Contains("sight")||n.Contains("skin ornament"))return true;
                t=t.parent;
            }
            return false;
        }
        public static void Apply(Transform gun,DuelWeapon weapon,int skin)
        {
            if(gun==null)return;
            var fade=gun.GetComponent<DuelWeaponFade>();if(fade!=null)fade.Clear();
            if(skin==8&&fade==null)fade=gun.gameObject.AddComponent<DuelWeaponFade>();
            var old=gun.Find("Skin ornament");if(old!=null){old.gameObject.SetActive(false);Object.Destroy(old.gameObject);}
            Material ornamentMaterial=null;
            foreach(var renderer in gun.GetComponentsInChildren<Renderer>(true))
            {
                if(Excluded(renderer.transform,gun)||renderer.sharedMaterial==null)continue;
                var mat=renderer.sharedMaterial;
                if(!mat.HasProperty("_BaseColor"))continue;
                renderer.SetPropertyBlock(null);
                if(skin==0)continue;
                Color original=mat.GetColor("_BaseColor"),tint=DuelSkins.WeaponTint(skin);
                bool wood=mat.name.ToLowerInvariant().Contains("wood")||mat.name.ToLowerInvariant().Contains("walnut");
                float luminance=original.grayscale;
                Color result=wood?Color.Lerp(original,tint,skin<=2?.52f:.38f):
                    Color.Lerp(original,tint,skin<=2?.54f:luminance<.12f?.40f:.72f);
                var block=new MaterialPropertyBlock();
                if(skin==8)
                {
                    fade.Apply(renderer,gun,weapon,block);
                    block.SetFloat("_Metallic",.88f);
                    block.SetFloat("_Smoothness",.93f);
                }
                else block.SetColor("_BaseColor",result);
                renderer.SetPropertyBlock(block);
                if(ornamentMaterial==null)ornamentMaterial=mat;
            }
            if(skin<3||skin==8||ornamentMaterial==null)return;
            var ornament=new GameObject("Skin ornament").transform;ornament.SetParent(gun,false);
            Color accent=skin==4?new Color(.77f,.94f,1):skin==5?new Color(.64f,.7f,1):skin==6?new Color(1,.73f,.38f):skin==7?new Color(1,.88f,.45f):new Color(1,.63f,.44f);
            float side=weapon==DuelWeapon.BAS?.073f:weapon==DuelWeapon.SawedOff?.099f:.084f;
            float z=weapon==DuelWeapon.SawedOff?-.15f:weapon==DuelWeapon.Revolver?-.24f:-.04f;
            int count=skin==7?7:skin>=5?5:3;
            for(int face=-1;face<=1;face+=2)
            for(int i=0;i<count;i++)
            {
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name="Engraved inlay";part.transform.SetParent(ornament,false);
                float offset=(i-(count-1)*.5f);
                float height=skin==4?Mathf.Abs(offset)*.012f:skin==5?Mathf.Sin(i*2.4f)*.034f:skin==6?Mathf.Sin(i*1.57f)*.025f:skin==7?Mathf.Abs(offset)*.008f:(i%2)*.025f;
                part.transform.localPosition=new Vector3(face*side,-.015f+height,z+offset*.038f);
                part.transform.localScale=new Vector3(.004f,skin==5?.014f:.009f,skin==4?.035f:skin>=5?.060f:.042f);
                float angle=skin==4?face*(offset<0?-50:50):skin==5?face*(i*73):skin==6?face*(i*45):face*(i%2==0?38:-38);
                part.transform.localRotation=Quaternion.Euler(angle,0,0);
                var collider=part.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
                var renderer=part.GetComponent<Renderer>();renderer.sharedMaterial=ornamentMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",accent);renderer.SetPropertyBlock(block);
            }
        }
    }
}


