using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        VisualElement diceLayer;
        Label diceTitle,diceStatus,diceTimer;
        readonly Label[] diceRows=new Label[RouletteRules.MaxPlayers];
        Image diceImage;
        DuelDiceVisual diceVisual;
        readonly VisualElement[] seatDice=new VisualElement[RouletteRules.MaxPlayers];
        readonly Image[] seatDiceImages=new Image[RouletteRules.MaxPlayers];
        readonly Label[] seatDiceResults=new Label[RouletteRules.MaxPlayers];
        readonly DuelDiceVisual[] seatDiceVisuals=new DuelDiceVisual[RouletteRules.MaxPlayers];
        void BuildDiceUI()
        {
            diceVisual=gameObject.AddComponent<DuelDiceVisual>();
            for(int i=0;i<seatDice.Length;i++)
            {
                var badge=Box(nameplateLayer,"seat-dice");badge.pickingMode=PickingMode.Ignore;seatDice[i]=badge;
                var picture=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};picture.AddToClassList("seat-dice-image");badge.Add(picture);seatDiceImages[i]=picture;
                seatDiceResults[i]=Text(badge,"","seat-dice-result");seatDiceResults[i].pickingMode=PickingMode.Ignore;Show(badge,false);
            }
            diceLayer=Box(root,"dice-layer");diceLayer.pickingMode=PickingMode.Ignore;
            var panel=Box(diceLayer,"dice-panel");panel.pickingMode=PickingMode.Ignore;
            diceTitle=Text(panel,"주사위 겨루기","dice-title");diceTitle.pickingMode=PickingMode.Ignore;
            diceImage=new Image{scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};diceImage.AddToClassList("dice-image");panel.Add(diceImage);
            diceStatus=Text(panel,"","dice-status");diceStatus.pickingMode=PickingMode.Ignore;
            diceTimer=Text(panel,"","dice-timer");diceTimer.pickingMode=PickingMode.Ignore;
            for(int i=0;i<diceRows.Length;i++){diceRows[i]=Text(panel,"","dice-row");diceRows[i].pickingMode=PickingMode.Ignore;}
            Show(diceLayer,false);
        }
        bool UpdateDice(HelloWorldPlayer player)
        {
            bool visible=player!=null&&player.State.Value.Phase==DuelPhase.Playing&&player.State.Value.Clock.Phase==ClockPhase.Dice;
            Show(diceLayer,visible);
            if(!visible)
            {
                diceVisual.Show(false,false,0,0);
                if(player==null)ClearSeatDice();return false;
            }
            var state=player.State.Value;var d=state.Clock.Dice;int seat=player.Seat.Value;double now=player.NetworkManager.ServerTime.Time;
            bool eligible=RouletteRules.Has(d.ParticipantsMask,seat),rolling=d.CanStop(seat);
            var own=d.ForSeat(seat);diceVisual.Show(eligible,rolling,own.Face,seat,now-d.StartedAt);diceImage.image=diceVisual.Texture;Show(diceImage,eligible);
            diceTitle.text="주사위 겨루기";
            diceStatus.text=rolling?(d.Round>1?"무승부입니다. SPACE · 다시 멈추기":"SPACE · 멈추기"):eligible&&own.Face>0?$"{own.Face} 확정"+(d.Phase==DuelDicePhase.Comparing?"":" · 다른 플레이어를 기다리는 중"):"무승부 참가자들이 주사위를 굴리고 있습니다.";
            diceTimer.text=d.Phase==DuelDicePhase.Rolling?$"자동 확정까지 {System.Math.Max(0,d.EndsAt-now):F1}초":state.Clock.MiniGame==PartyMiniGame.Memory||state.Clock.MiniGame==PartyMiniGame.Greed?"가장 높은 숫자가 1등입니다.":"같은 기록끼리 높은 숫자가 앞섭니다.";
            for(int i=0;i<diceRows.Length;i++)
            {
                bool show=RouletteRules.Has(d.ParticipantsMask,i);Show(diceRows[i],show);if(!show)continue;
                var e=d.ForSeat(i);bool pending=RouletteRules.Has(d.PendingMask,i)&&!RouletteRules.Has(d.LockedMask,i);
                diceRows[i].text=state.NameForSeat(i)+(i==seat?" · 나":"")+" · "+DiceRecord(state.Clock,i)+"    "+(pending?"굴리는 중":e.Face>0?e.Face.ToString():"대기");
                diceRows[i].EnableInClassList("dice-own",i==seat);
            }
            return true;
        }
        void ClearSeatDice()
        {
            for(int i=0;i<seatDice.Length;i++){Show(seatDice[i],false);if(seatDiceVisuals[i]!=null)seatDiceVisuals[i].Show(false,false,0,i);}
        }
        public float DiceNameplateHeight(int seat)=>seat>=0&&seat<seatDice.Length&&seatDice[seat]!=null&&seatDice[seat].style.display.value==DisplayStyle.Flex?120:0;
        void UpdateSeatDice(DuelSnapshot state,int seat,bool anchorVisible,Vector3 anchor,double now,int localSeat)
        {
            var c=state.Clock;var d=c.Dice;
            bool result=(c.Phase==ClockPhase.Ranking||c.Phase==ClockPhase.QuickResults)&&d.Phase==DuelDicePhase.Finished&&now-c.PhaseAt<DuelClockContest.MiniResultsDuration;
            bool visible=anchorVisible&&state.Connected(seat)&&RouletteRules.Has(d.ParticipantsMask,seat)&&(c.Phase==ClockPhase.Dice||result);
            Show(seatDice[seat],visible);
            if(!visible){if(seatDiceVisuals[seat]!=null)seatDiceVisuals[seat].Show(false,false,0,seat);return;}
            DuelDiceVisual visual;
            if(seat==localSeat)visual=diceVisual;
            else
            {
                if(seatDiceVisuals[seat]==null){seatDiceVisuals[seat]=gameObject.AddComponent<DuelDiceVisual>();seatDiceVisuals[seat].Initialize(seat+1,160);}
                visual=seatDiceVisuals[seat];
            }
            var entry=d.ForSeat(seat);bool rolling=d.CanStop(seat);
            visual.Show(true,rolling,entry.Face,seat,now-d.StartedAt);seatDiceImages[seat].image=visual.Texture;
            seatDiceResults[seat].text=rolling?"굴리는 중":entry.Face>0?$"{entry.Face} 확정":"대기";
            seatDice[seat].EnableInClassList("seat-dice-confirmed",!rolling&&entry.Face>0);
            seatDice[seat].style.left=anchor.x*root.resolvedStyle.width;
            seatDice[seat].style.top=(1-anchor.y)*root.resolvedStyle.height-188-NameplateHealthHeight;
        }
        static string DiceRecord(ClockState state,int seat)
        {
            var e=state.ForSeat(seat);
            if(state.MiniGame==PartyMiniGame.Memory)return $"오류 {state.Memory.ForSeat(seat).Errors}개";
            if(state.MiniGame==PartyMiniGame.QuickHands)return $"공동 {e.Rank}위";
            if(e.Failure!=MiniFailure.None)return "실패";
            return state.MiniGame==PartyMiniGame.Greed?$"{e.Points:N0}점":$"{e.Error/100.0:F2}초";
        }
        bool StopOwnDice()
        {
            var player=HelloWorldPlayer.Local;
            if(player==null||!player.IsSpawned||player.State.Value.Clock.Phase!=ClockPhase.Dice)return false;
            if(player.State.Value.Clock.Dice.CanStop(player.Seat.Value)){feedback?.KeyInput(false);player.StopDice();}
            return true;
        }
    }
}
