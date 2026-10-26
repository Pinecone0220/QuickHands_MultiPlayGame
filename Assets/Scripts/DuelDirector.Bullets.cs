namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        // Shared minigame shooting opportunities, independent of the physical chamber mask.
        private int clockShotsRemaining=1;
        private bool UseBullet(int seat,int iteration,double now)
        {
            if(seat<0||seat>=actions.Length||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||
                clock.State.Phase!=ClockPhase.ItemPreparation||clock.State.MiniGame==PartyMiniGame.QuickHands||
                iteration!=clock.State.Iteration||now>=clock.State.StartedAt||clock.State.First!=seat||
                !RouletteRules.Has(Game.AliveMask,seat)||actions[seat].DiedAt>=0||actions[seat].Stunned)return false;
            var a=actions[seat];if(a.BulletItems<=0||a.BulletUsed)return false;
            // Pay on activation; misses, unused extra shots and turn expiry never refund it.
            if(!Game.UnlimitedItems)a.BulletItems--;
            a.BulletUsed=true;actions[seat]=a;Sequence++;return true;
        }
        private void ResetBulletTurn()
        {
            clockShotsRemaining=1;
            for(int i=0;i<actions.Length;i++)actions[i].BulletUsed=false;
        }
        private void PrepareBonusShot(int seat,double now)
        {
            // Other minigames have no numeric reload actions: ready the extra single shot after contact.
            var a=actions[seat];a.Action=DuelAction.Aim;a.Progress=0;a.LoadedMask=1;a.SpentMask=0;a.NextChamber=0;
            a.Version++;a.StartedAt=now;a.ShootingStartedAt=now;a.Aiming=false;actions[seat]=a;
            nextAi[seat]=now+.45;
        }
    }
}
