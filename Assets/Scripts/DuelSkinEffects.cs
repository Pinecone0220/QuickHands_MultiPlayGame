using UnityEngine;
namespace HelloWorld
{
    // Presentation helpers use only public snapshots and already-replicated cosmetic choices.
    public static class DuelSkinEffects
    {
        public static int Skin(int seat,DuelItem item)=>DuelSkins.FromPacked(HelloWorldPlayer.SkinsForSeat(seat),(int)item);
        public static int Tier(int skin)=>skin>0&&DuelSkins.Grade(skin)>=2?DuelSkins.Grade(skin):0;
        public static Color Glow(int skin)=>Color.Lerp(DuelSkins.Tint(skin),Color.white,.18f);
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
