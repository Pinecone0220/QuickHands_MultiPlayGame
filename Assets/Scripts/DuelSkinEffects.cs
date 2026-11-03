using UnityEngine;
namespace HelloWorld
{
    // Presentation helpers use only public snapshots and already-replicated cosmetic choices.
    public static class DuelSkinEffects
    {
        public static int Skin(int seat,DuelItem item)=>DuelSkins.FromPacked(HelloWorldPlayer.SkinsForSeat(seat),(int)item);
        public static int Tier(int skin)=>skin>0&&DuelSkins.Grade(skin)>=2?DuelSkins.Grade(skin):0;
        public static Color Glow(int skin)=>Color.Lerp(DuelSkins.Tint(skin),Color.white,.18f);
        public static int WeaponSkin(int seat,DuelWeapon weapon)
        {
            for(int i=0;i<WeaponRules.Selectable.Length;i++)
                if(WeaponRules.Selectable[i]==weapon)return DuelSkins.WeaponFromPacked(HelloWorldPlayer.SkinsForSeat(seat),i);
            return 0;
        }
        public static int WeaponTier(int skin)=>skin>0&&DuelSkins.WeaponGrade(skin)>=2?DuelSkins.WeaponGrade(skin):0;
        public static Color WeaponGlow(int skin,float time)
        {
            if(WeaponTier(skin)==0)return Color.white;
            Color color=DuelSkins.WeaponTint(skin);
            if(DuelSkins.WeaponGrade(skin)==4)
            {
                float wave=.5f+.5f*Mathf.Sin(time*2.4f);
                color=skin==8?DuelSkins.FadeColor(Mathf.Lerp(.28f,.76f,wave)):
                    Color.Lerp(color,new Color(1f,.88f,.50f),.12f+.25f*wave);
            }
            return Color.Lerp(color,Color.white,.18f);
        }
        public static int InkSource(DuelSnapshot state,int victim)
        {
            double at=state.ForSeat(victim).Ink.InkedAt;if(at<0)return -1;
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++){var ink=state.ForSeat(seat).Ink;if(state.Connected(seat)&&ink.Target==victim&&ink.ImpactAt>=0&&System.Math.Abs(ink.ImpactAt-at)<.025)return seat;}
            return -1;
        }
        public static int TaserSource(DuelSnapshot state,int victim)
        {
            double at=state.ForSeat(victim).StunnedAt;if(at<0)return -1;
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++){var a=state.ForSeat(seat);if(state.Connected(seat)&&a.FiredWeapon==DuelWeapon.Taser&&a.FiredTarget==victim&&a.FiredAt>=0&&System.Math.Abs(a.FiredAt-at)<.025)return seat;}
            return -1;
        }
        public static int SourceSkin(DuelSnapshot state,int victim,DuelItem item)
        {var target=state.ForSeat(victim);return item==DuelItem.Ink?target.InkEffectSkin:target.TaserEffectSkin;}
    }
}
