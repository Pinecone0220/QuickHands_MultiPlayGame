namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        public const double TaserHolsterDelay=.4;
        private bool TaserHolsterPending(double now)
        {
            for(int seat=0;seat<actions.Length;seat++)
            {
                var a=actions[seat];
                if(RouletteRules.Has(Game.AliveMask,seat)&&a.Weapon==DuelWeapon.Taser&&a.FiredWeapon==DuelWeapon.Taser&&a.FiredAt>=0&&a.Ammo==0&&now<a.FiredAt+TaserHolsterDelay&&(a.TaserArmed||a.QuickEquipped&&a.QuickItem==2))return true;
            }
            return false;
        }
        private bool TickTaserHolster(double now)
        {
            if(Game.Phase!=DuelPhase.Playing||Stage!=DuelStage.Racing||!DuelInk.Shooting(clock.State.Phase))return false;
            bool changed=false;
            for(int seat=0;seat<actions.Length;seat++)
            {
                var a=actions[seat];
                if(IsPracticing(seat)||!RouletteRules.Has(Game.AliveMask,seat)||a.Weapon!=DuelWeapon.Taser||a.FiredWeapon!=DuelWeapon.Taser||a.FiredAt<0||a.Ammo>0||now<a.FiredAt+TaserHolsterDelay)continue;
                if(a.QuickEquipped&&a.QuickItem==2)HolsterQuickItem(seat);
                else if(a.TaserArmed)
                {
                    a.TaserArmed=false;a.Weapon=selectedWeapons[seat];a.Action=DuelAction.Pickup;a.Progress=0;
                    a.LoadedMask=a.SpentMask=a.NextChamber=0;a.ShootingStartedAt=-1;a.Aiming=false;a.Target=-1;a.Version++;actions[seat]=a;
                }
                else continue;
                Sequence++;changed=true;
            }
            return changed;
        }
        private bool SelectPreparedItem(int seat,DuelItem item,int iteration,double now)
        {
            if(seat<0||seat>=actions.Length||!DuelLoadout.Selectable(item)||Stage!=DuelStage.Racing||
                Game.Phase!=DuelPhase.Playing||clock.State.Phase!=ClockPhase.ItemPreparation||
                iteration!=clock.State.Iteration||now>=clock.State.StartedAt||
                !RouletteRules.Has(Game.AliveMask,seat)||actions[seat].DiedAt>=0||actions[seat].Stunned)return false;
            var a=actions[seat];int chosen=(int)item+1;
            if(a.PreparedItem==chosen){actions[seat].PreparedItem=0;Sequence++;return true;}
            if(a.PreparedItem!=0)return false;
            bool quick=clock.State.MiniGame==PartyMiniGame.QuickHands;
            bool eligible=item==DuelItem.Recovery?a.HasRecovery&&Game.HealthForSeat(seat)<RouletteRules.MaxHealth:
                item==DuelItem.Taser?a.HasTaser&&(NoCooldownFor(seat)||iteration>=a.TaserReadyIteration)&&(quick||clock.State.First>=0&&clock.State.First!=seat):
                item==DuelItem.Ink?a.Ink.Owned:
                item==DuelItem.Bullet?!quick&&clock.State.First==seat&&a.BulletItems>0:
                a.HasNoruFace&&(NoCooldownFor(seat)||iteration>=a.NoruFaceReadyIteration);
            if(!eligible)return false;
            actions[seat].PreparedItem=chosen;Sequence++;return true;
        }
        private void CommitPreparedItems(double now)
        {
            if(clock.State.MiniGame!=PartyMiniGame.QuickHands&&
                (!RouletteRules.Has(Game.AliveMask,clock.State.First)||RouletteRules.Count(Game.AliveMask)<2))return;
            double effectiveTime=System.Math.Min(now,clock.State.StartedAt-0.000001);
            for(int seat=0;seat<actions.Length;seat++)
            {
                int selected=actions[seat].PreparedItem;
                if(selected>0&&RouletteRules.Has(Game.AliveMask,seat))
                    ApplySelectedItem(seat,(DuelItem)(selected-1),clock.State.Iteration,effectiveTime);
            }
        }
        private readonly DuelActionState[] holsteredGuns=new DuelActionState[RouletteRules.MaxPlayers];
        private bool SelectQuickItem(int seat,DuelItem item,int iteration,double now)
        {
            if(seat<0||seat>=actions.Length||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||clock.State.MiniGame!=PartyMiniGame.QuickHands||
                clock.State.Phase!=ClockPhase.ItemPreparation||iteration!=clock.State.Iteration||now>=clock.State.StartedAt||!RouletteRules.Has(Game.AliveMask,seat))return false;
            var a=actions[seat];if(a.QuickSpent||a.Stunned)return false;
            if(item==DuelItem.Recovery)
            {
                if(a.QuickItem!=0||!a.HasRecovery||!Game.Heal(seat,50))return false;
                if(!UnlimitedFor(seat))a.HasRecovery=false;a.HealAt=now;a.QuickItem=1;a.QuickSpent=true;
            }
            else
            {
                if(item==DuelItem.Taser?(!a.HasTaser||!NoCooldownFor(seat)&&iteration<a.TaserReadyIteration):item!=DuelItem.Ink||!a.Ink.Owned)return false;
                a.QuickItem=a.QuickItem==(int)item+1?0:(int)item+1;
            }
            a.QuickEquipped=false;a.QuickItemVersion++;actions[seat]=a;Sequence++;return true;
        }
        public bool SwitchQuickItem(int seat,bool equip,int iteration,int life,int version,double now)
        {
            if(seat>=0&&seat<actions.Length&&IsPracticing(seat))
            {
                var practice=actions[seat];
                if(iteration!=clock.State.Iteration||life!=practice.Life||version!=practice.QuickItemVersion||practice.IsStunned(now)||equip||!practice.QuickEquipped)return false;
                HolsterQuickItem(seat);actions[seat].QuickItem=0;actions[seat].QuickItemVersion++;Sequence++;return true;
            }
            if(seat<0||seat>=actions.Length||!QuickHandsActive||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||
                !RouletteRules.Has(Game.AliveMask,seat)||iteration!=clock.State.Iteration||life!=actions[seat].Life)return false;
            var a=actions[seat];
            if(a.Stunned||a.QuickItemVersion!=version||a.QuickEquipped==equip||a.QuickItem<2||DuelInk.Busy(a.Ink,now)&&!a.Ink.Armed)return false;
            if(!equip){HolsterQuickItem(seat);Sequence++;return true;}
            if(a.QuickSpent||a.QuickItem==2&&(!a.HasTaser||!NoCooldownFor(seat)&&iteration<a.TaserReadyIteration)||a.QuickItem==3&&(!a.Ink.Owned||inkFlights[seat]!=null))return false;
            if(a.QuickItem==2)
            {
                holsteredGuns[seat]=a;a.Weapon=DuelWeapon.Taser;a.Action=DuelAction.Aim;a.Code=a.Progress=0;a.LoadedMask=1;a.SpentMask=a.NextChamber=0;
                a.StartedAt=a.ShootingStartedAt=now;a.BlockedUntil=now;a.Version++;
            }
            else {a.Ink.Armed=true;a.Ink.Version++;}
            a.QuickEquipped=true;a.QuickItemVersion++;a.Aiming=false;a.Target=-1;actions[seat]=a;Sequence++;return true;
        }
        private void HolsterQuickItem(int seat)
        {
            var a=actions[seat];if(!a.QuickEquipped)return;
            if(a.QuickItem==2)
            {
                var gun=holsteredGuns[seat];a.Weapon=gun.Weapon;a.Action=gun.Action;a.Code=gun.Code;a.Progress=gun.Progress;
                a.LoadedMask=gun.LoadedMask;a.SpentMask=gun.SpentMask;a.NextChamber=gun.NextChamber;
                a.StartedAt=gun.StartedAt;a.BlockedUntil=gun.BlockedUntil;a.ShootingStartedAt=gun.ShootingStartedAt;a.Version++;
            }
            else if(a.QuickItem==3){a.Ink.Armed=false;a.Ink.Version++;}
            a.QuickEquipped=false;a.QuickItemVersion++;a.Aiming=false;a.Target=-1;actions[seat]=a;
        }
        private void FinishQuickItem(int seat)
        {
            HolsterQuickItem(seat);var a=actions[seat];if(!IsBot(seat)||a.QuickItem<2||a.QuickSpent)return;
            if(a.QuickItem==2)a.TaserReadyIteration=System.Math.Max(a.TaserReadyIteration,clock.State.Iteration+3);
            else if(!UnlimitedFor(seat))a.Ink.Owned=false;
            a.QuickSpent=true;a.QuickItemVersion++;actions[seat]=a;
        }
        private void ClearQuickItems(){for(int i=0;i<actions.Length;i++)FinishQuickItem(i);}
        private void ResetQuickItems()
        {for(int i=0;i<actions.Length;i++){actions[i].PreparedItem=0;actions[i].QuickItem=0;actions[i].QuickEquipped=actions[i].QuickSpent=false;actions[i].QuickItemVersion++;}}
        private bool HasQuickEffects()
        {foreach(var a in actions)if(a.QuickEquipped||a.QuickItem>=2&&!a.QuickSpent)return true;return false;}
        private bool TickQuickItemAi(double now)
        {
            if(BotMask==0||!QuickHandsActive)return false;bool changed=false;
            for(int seat=1;seat<actions.Length;seat++)
            {
                var a=actions[seat];if(!IsBot(seat)||a.QuickItem<2||!RouletteRules.Has(Game.AliveMask,seat)||a.Stunned)continue;
                if(a.QuickSpent||aiQuickUsedIteration[seat]==clock.State.Iteration||a.QuickEquipped&&a.QuickItem==2&&a.Ammo==0)
                {if(a.QuickEquipped&&!(a.QuickItem==2&&a.FiredWeapon==DuelWeapon.Taser&&a.FiredAt>=0&&now<a.FiredAt+TaserHolsterDelay)){HolsterQuickItem(seat);Sequence++;changed=true;}continue;}
                if(!a.QuickEquipped){changed|=SwitchQuickItem(seat,true,clock.State.Iteration,a.Life,a.QuickItemVersion,now);continue;}
                if(a.QuickItem!=2||now-a.StartedAt<.5)continue;int target=RandomTarget(seat,true);
                if(target<0){HolsterQuickItem(seat);aiQuickUsedIteration[seat]=clock.State.Iteration;Sequence++;changed=true;continue;}
                AimAiWeapon(seat,target,now,out float yaw,out float pitch);
                if(Fire(seat,yaw,pitch,false,a.Version,now,now)){aiQuickUsedIteration[seat]=clock.State.Iteration;changed=true;}
            }
            return changed;
        }
    }
}
