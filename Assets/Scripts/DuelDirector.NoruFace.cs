namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        private bool UseNoruFace(int seat,int iteration,double now)
        {
            if(seat<0||seat>=actions.Length||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||
                clock.State.Phase!=ClockPhase.ItemPreparation||iteration!=clock.State.Iteration||now>=clock.State.StartedAt||
                !RouletteRules.Has(Game.AliveMask,seat)||actions[seat].DiedAt>=0||actions[seat].Stunned)return false;
            var a=actions[seat];bool quick=clock.State.MiniGame==PartyMiniGame.QuickHands;
            if(!a.HasNoruFace||a.NoruFaceActive||!NoCooldownFor(seat)&&iteration<a.NoruFaceReadyIteration||
                quick&&(a.QuickItem!=0||a.QuickSpent))return false;
            a.NoruFaceActive=true;a.NoruFaceReadyIteration=iteration+3;
            // Instant-use equipment occupies the same one-item slot as healing, not the held weapon slot.
            if(quick){a.QuickItem=1;a.QuickSpent=true;a.QuickItemVersion++;}
            actions[seat]=a;Sequence++;return true;
        }
    }
}
