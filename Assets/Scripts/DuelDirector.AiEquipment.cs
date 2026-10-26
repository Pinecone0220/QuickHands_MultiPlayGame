using System;
namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        private readonly int[] aiInkUsedIteration=new int[RouletteRules.MaxPlayers];
        private readonly int[] aiQuickUsedIteration=new int[RouletteRules.MaxPlayers];
        private static readonly DuelItem[] aiQuickItems={DuelItem.Taser,DuelItem.Ink,DuelItem.NoruFace};
        private void ConfigureBotLoadout(int seat,int round)
        {
            if(!IsBot(seat))return;
            selectedWeapons[seat]=WeaponRules.Selectable[(seat-1+(Math.Max(1,round)-1)%WeaponRules.Selectable.Length)%WeaponRules.Selectable.Length];
            selectedItems[seat]=Game.AutoSelectItems?DuelLoadout.All:DuelLoadout.AiSelection(seat,round);
            actions[seat].Weapon=selectedWeapons[seat];aiInkUsedIteration[seat]=aiQuickUsedIteration[seat]=-1;
        }
        private bool AiTaserThreat(int seat)
        {
            for(int other=0;other<actions.Length;other++)if(other!=seat&&RouletteRules.Has(Game.AliveMask,other)&&actions[other].HasTaser&&
                (actions[other].TaserArmed||Game.UnlimitedItems||clock.State.Iteration>=actions[other].TaserReadyIteration)&&
                (clock.State.MiniGame==PartyMiniGame.QuickHands||clock.State.First!=other))return true;
            return false;
        }
        private bool TickAiItemPreparation(double now)
        {
            var c=clock.State;if(BotMask==0||c.Phase!=ClockPhase.ItemPreparation||now-c.PhaseAt<=.75)return false;
            bool changed=false;
            for(int bot=1;bot<actions.Length;bot++)if(IsBot(bot)&&RouletteRules.Has(Game.AliveMask,bot)&&!actions[bot].Stunned)
            {
                if(c.MiniGame==PartyMiniGame.QuickHands)
                {
                    if(actions[bot].QuickItem!=0||actions[bot].QuickSpent)continue;
                    if(Game.HealthForSeat(bot)<=50&&UseItem(bot,DuelItem.Recovery,c.Iteration,now)){changed=true;continue;}
                    for(int pick=0;pick<aiQuickItems.Length;pick++)
                    {
                        var item=aiQuickItems[(bot+c.Iteration+pick)%aiQuickItems.Length];
                        if(item==DuelItem.NoruFace&&!AiTaserThreat(bot))continue;
                        if(UseItem(bot,item,c.Iteration,now)){changed=true;break;}
                    }
                }
                else
                {
                    if(Game.HealthForSeat(bot)<=50)changed|=UseItem(bot,DuelItem.Recovery,c.Iteration,now);
                    if(AiTaserThreat(bot))changed|=UseItem(bot,DuelItem.NoruFace,c.Iteration,now);
                    changed|=UseItem(bot,DuelItem.Taser,c.Iteration,now);changed|=UseItem(bot,DuelItem.Bullet,c.Iteration,now);
                    if(actions[bot].Ink.Owned&&!actions[bot].Ink.Armed)changed|=UseItem(bot,DuelItem.Ink,c.Iteration,now);
                }
            }
            return changed;
        }
        private void AimAiWeapon(int seat,int target,double now,out float yaw,out float pitch)
        {
            var a=actions[seat];bool ads=WeaponRules.SupportsAds(a.Weapon)&&a.Weapon!=DuelWeapon.Taser;
            var part=a.Weapon==DuelWeapon.BAS?DuelHitPart.Head:a.Weapon!=DuelWeapon.Revolver?DuelHitPart.Body:
                random.NextDouble()<(Difficulty==AiDifficulty.Hard?.55:Difficulty==AiDifficulty.Normal?.3:.12)?DuelHitPart.Head:DuelHitPart.Body;
            var point=DuelGunplay.Point(target,part);DuelGunplay.Angles(point-DuelGunplay.Eye(seat),out yaw,out pitch);
            if(!ads)for(int i=0;i<3;i++)DuelGunplay.Angles(point-DuelGunplay.MuzzleOrigin(seat,a.Weapon,false,DuelGunplay.Direction(yaw,pitch)),out yaw,out pitch);
            DuelGunplay.Sway(seat,a.Weapon,ads,now,a.AimStartedAt,out float sy,out float sp);
            float error=Difficulty==AiDifficulty.Hard?.35f:Difficulty==AiDifficulty.Normal?1.25f:3f;
            yaw-=sy;pitch-=sp;yaw+=(float)(random.NextDouble()*2-1)*error;pitch+=(float)(random.NextDouble()*2-1)*error;
        }
        private void AimAiInk(int seat,int target,out float yaw,out float pitch)
        {
            var point=DuelGunplay.Point(target,DuelHitPart.Head);DuelGunplay.Angles(point-DuelGunplay.Eye(seat),out yaw,out pitch);
            for(int i=0;i<4;i++)
            {
                var origin=DuelInk.ThrowOrigin(seat,yaw,pitch);var delta=point-origin;
                float distance=(float)Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z);
                float t=distance/(DuelInk.Speed*(float)Math.Cos(pitch*Math.PI/180));
                var correction=new AimVector(0,DuelInk.UpwardSpeed*t-.5f*DuelInk.Gravity*t*t,0);
                DuelGunplay.Angles(point-origin-correction,out yaw,out pitch);
            }
        }
    }
}
