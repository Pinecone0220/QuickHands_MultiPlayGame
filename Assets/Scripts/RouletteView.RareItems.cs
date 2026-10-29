using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        readonly Texture2D[] rareHealEffects=new Texture2D[DuelSkins.Colors+1],rareElectricEffects=new Texture2D[DuelSkins.Colors+1];
        private Texture2D ItemEffectTexture(bool electric,int skin)
        {
            int tier=DuelSkinEffects.Tier(skin);if(tier==0&&(!electric||skin==0))return electric?electricTexture:healTexture;
            var cache=electric?rareElectricEffects:rareHealEffects;
            if(cache[skin]==null)cache[skin]=DuelItemArt.Effect(electric,DuelSkinEffects.Glow(skin),tier);
            return cache[skin];
        }
        private void DestroyRareItemTextures()
        {foreach(var t in rareHealEffects)if(t!=null)Destroy(t);foreach(var t in rareElectricEffects)if(t!=null)Destroy(t);DestroyRareInkTextures();}
    }
}
