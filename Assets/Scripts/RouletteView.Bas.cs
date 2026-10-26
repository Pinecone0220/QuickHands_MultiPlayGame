using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement basScope;
        private Texture2D basScopeTexture;
        private float basScopeAspect;
        private void BuildBasScope()
        {
            basScope=new VisualElement{pickingMode=PickingMode.Ignore};basScope.AddToClassList("aim-warning");
            root.Insert(0,basScope);Show(basScope,false);
        }
        private void UpdateBasScope(bool visible)
        {
            Show(basScope,visible);if(!visible)return;
            float width=root.resolvedStyle.width,height=root.resolvedStyle.height;
            if(width<=0||height<=0)return;
            float aspect=width/height;
            if(basScopeTexture!=null&&Mathf.Abs(aspect-basScopeAspect)<.005f)return;
            if(basScopeTexture!=null)Destroy(basScopeTexture);basScopeAspect=aspect;
            const int size=768;int w=aspect>=1?size:Mathf.Max(1,Mathf.RoundToInt(size*aspect)),h=aspect>=1?Mathf.Max(1,Mathf.RoundToInt(size/aspect)):size;
            basScopeTexture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="BAS telescopic scope",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[w*h];float radius=Mathf.Min(w,h)*.455f;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float dx=x+.5f-w*.5f,dy=y+.5f-h*.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                float edge=Mathf.Clamp01((r-radius+2)/3);
                bool reticle=Mathf.Abs(dx)<.65f||Mathf.Abs(dy)<.65f;
                // Stadia marks belong to the telescopic sight; no hip-fire crosshair is added.
                bool stadia=Mathf.Abs(dx)<4&&Mathf.Abs(dy)%24<1&&Mathf.Abs(dy)>12||Mathf.Abs(dy)<4&&Mathf.Abs(dx)%24<1&&Mathf.Abs(dx)>12;
                float ring=Mathf.Clamp01((r-radius*.91f)/(radius*.09f))*.36f;
                pixels[y*w+x]=new Color(0,0,0,Mathf.Max(edge,reticle||stadia?.92f:ring));
            }
            basScopeTexture.SetPixels32(pixels);basScopeTexture.Apply(false,true);basScope.style.backgroundImage=new StyleBackground(basScopeTexture);
        }
    }
}
