using System;
namespace HelloWorld
{
    public sealed partial class DuelDirector
    {
        private readonly DuelInkFlight[] inkFlights=new DuelInkFlight[RouletteRules.MaxPlayers];
        public bool CanUseInk(int seat,double now)
        {
            if(seat<0||seat>=actions.Length||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||!RouletteRules.Has(Game.AliveMask,seat)||actions[seat].Stunned||!DuelInk.Usable(clock.State.Phase))return false;
            if(clock.State.Phase==ClockPhase.ItemPreparation)return now<clock.State.StartedAt;
            if(clock.State.Phase==ClockPhase.Shooting&&Game.ShotTimeLimit>0&&now>=clock.State.PhaseAt+Game.ShotTimeLimit)return false;
            return !shotOpportunitySpent||QuickHandsActive;
        }
        private bool UseInk(int seat,int iteration,double now)
        {
            if(iteration!=clock.State.Iteration||!CanUseInk(seat,now))return false;
            var state=actions[seat].Ink;
            if(!state.Owned||inkFlights[seat]!=null||state.ThrownAt>=0&&now-state.ThrownAt<DuelInk.ThrowDuration)return false;
            state.Armed=!state.Armed;state.Version++;actions[seat].Ink=state;
            actions[seat].Aiming=false;actions[seat].Target=-1;Sequence++;return true;
        }
        public bool ThrowInk(int seat,float yaw,float pitch,int iteration,int life,int version,double sample,double now)
        {
            if(!CanUseInk(seat,now)||!DuelInk.Shooting(clock.State.Phase)||iteration!=clock.State.Iteration||life!=actions[seat].Life||
                !DuelGunplay.ValidSeatAngles(seat,yaw,pitch)||double.IsNaN(sample)||double.IsInfinity(sample)||sample<now-.3||sample>now+.05)return false;
            var ink=actions[seat].Ink;
            if(QuickHandsActive&&(actions[seat].QuickItem!=3||!actions[seat].QuickEquipped||actions[seat].QuickSpent))return false;
            if(!ink.Owned||!ink.Armed||ink.Version!=version||inkFlights[seat]!=null)return false;
            ink.Armed=false;if(!Game.UnlimitedItems)ink.Owned=false;
            ink.Version++;ink.Serial++;ink.ThrownAt=now;ink.ImpactAt=-1;ink.Target=-1;
            ink.Origin=DuelInk.ThrowOrigin(seat,yaw,pitch);ink.Direction=DuelGunplay.Direction(yaw,pitch);
            actions[seat].Ink=ink;actions[seat].AimYaw=yaw;actions[seat].AimPitch=pitch;actions[seat].Aiming=false;actions[seat].Target=-1;
            if(QuickHandsActive){actions[seat].QuickEquipped=false;actions[seat].QuickSpent=!Game.UnlimitedItems;actions[seat].QuickItemVersion++;}
            inkFlights[seat]=new DuelInkFlight(seat,life,iteration,ink);Sequence++;return true;
        }
        private bool AnyInkFlight(){foreach(var flight in inkFlights)if(flight!=null)return true;return false;}
        private void ClearInkTurn()
        {
            Array.Clear(inkFlights,0,inkFlights.Length);
            for(int i=0;i<actions.Length;i++)if(actions[i].Ink.Armed){actions[i].Ink.Armed=false;actions[i].Ink.Version++;}
        }
        private bool TickInk(double now)
        {
            bool changed=false;
            for(int i=0;i<inkFlights.Length;i++)
            {
                var flight=inkFlights[i];if(flight==null)continue;
                if(Game.Phase!=DuelPhase.Playing||!DuelInk.Shooting(clock.State.Phase)||!RouletteRules.Has(Game.ConnectedMask,i)||flight.Life!=actions[i].Life||flight.Iteration!=clock.State.Iteration)
                {inkFlights[i]=null;continue;}
                if(!flight.Tick(Game.AliveMask,now,tabletop.State,out var hit,out var point))continue;
                inkFlights[i]=null;actions[i].Ink.ImpactAt=now;actions[i].Ink.Impact=point;actions[i].Ink.Target=hit.Seat;
                if(RouletteRules.Has(Game.AliveMask,hit.Seat))actions[hit.Seat].Ink.InkedAt=now;
                Sequence++;changed=true;
            }
            if(changed&&clock.State.Phase==ClockPhase.Shooting&&shotOpportunitySpent&&!AnyProjectile()&&!FinishPartyIfReady(now))clock.Return(now);
            return changed;
        }
        private bool TickInkAi(double now)
        {
            if(BotMask==0||!DuelInk.Shooting(clock.State.Phase))return false;
            bool changed=false;
            for(int bot=1;bot<actions.Length;bot++)
            {
                var a=actions[bot];if(!IsBot(bot)||!a.Ink.Owned||aiInkUsedIteration[bot]==clock.State.Iteration||!CanUseInk(bot,now)||now-a.StartedAt<.35||QuickHandsActive&&(!a.QuickEquipped||a.QuickItem!=3))continue;
                if(!a.Ink.Armed){changed|=UseInk(bot,clock.State.Iteration,now);continue;}
                int target=RandomTarget(bot);if(target<0)continue;
                AimAiInk(bot,target,out float yaw,out float pitch);
                if(ThrowInk(bot,yaw,pitch,clock.State.Iteration,a.Life,actions[bot].Ink.Version,now,now))
                {aiInkUsedIteration[bot]=clock.State.Iteration;if(QuickHandsActive)aiQuickUsedIteration[bot]=clock.State.Iteration;changed=true;}
            }
            return changed;
        }
    }
}
