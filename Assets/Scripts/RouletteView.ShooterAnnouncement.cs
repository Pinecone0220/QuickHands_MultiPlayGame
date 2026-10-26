using UnityEngine;
using UnityEngine.UIElements;

namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement shooterAnnouncementLayer;
        private Label shooterAnnouncement;
        private int shooterAnnouncementRound=-1,shooterAnnouncementIteration=-1,announcedShooter=-1;
        private double shooterAnnouncementAt;
        private void UpdateShooterAnnouncement(HelloWorldPlayer player)
        {
            var state=player!=null?player.State.Value:default;var clock=state.Clock;
            bool visible=player!=null&&state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Racing&&
                clock.MiniGame!=PartyMiniGame.QuickHands&&clock.First>=0&&state.Alive(clock.First)&&
                (clock.Phase==ClockPhase.Ranking||clock.Phase==ClockPhase.ItemPreparation||clock.Phase==ClockPhase.Shooting);
            if(!visible)
            {
                if(shooterAnnouncementLayer!=null)Show(shooterAnnouncementLayer,false);
                shooterAnnouncementRound=shooterAnnouncementIteration=announcedShooter=-1;return;
            }
            if(shooterAnnouncementLayer==null)
            {
                shooterAnnouncementLayer=new VisualElement{name="shooter-announcement-layer",pickingMode=PickingMode.Ignore};
                shooterAnnouncementLayer.AddToClassList("shooter-announcement-layer");root.Insert(root.IndexOf(resultsLayer),shooterAnnouncementLayer);
                shooterAnnouncement=Text(shooterAnnouncementLayer,"","shooter-announcement");shooterAnnouncement.name="shooter-announcement";
                shooterAnnouncement.pickingMode=PickingMode.Ignore;shooterAnnouncement.enableRichText=false;
            }
            if(shooterAnnouncementRound!=state.Round||shooterAnnouncementIteration!=clock.Iteration||announcedShooter!=clock.First)
            {
                shooterAnnouncementRound=state.Round;shooterAnnouncementIteration=clock.Iteration;announcedShooter=clock.First;
                // A client arriving after results should see the already-raised banner, not replay it.
                shooterAnnouncementAt=clock.PhaseAt-(clock.Phase==ClockPhase.ItemPreparation?DuelClockContest.MiniResultsDuration:
                    clock.Phase==ClockPhase.Shooting?DuelClockContest.MiniResultsDuration+DuelClockContest.ItemPreparationDuration:0);
            }
            double now=player.NetworkManager.ServerTime.Time;
            double elapsed=now-shooterAnnouncementAt;
            float rise=Mathf.SmoothStep(0,1,(float)((elapsed-.8)/.75));
            float scale=Mathf.Lerp(1,.65f,rise);
            shooterAnnouncement.text=state.NameForSeat(clock.First)+(rise>=1?"가 사격합니다!":"가 사격을 준비합니다!");
            float height=float.IsNaN(root.resolvedStyle.height)?Screen.height:root.resolvedStyle.height;
            float labelHeight=float.IsNaN(shooterAnnouncement.resolvedStyle.height)?76:shooterAnnouncement.resolvedStyle.height;
            shooterAnnouncement.style.scale=new Scale(new Vector3(scale,scale,1));
            shooterAnnouncement.style.translate=new Translate(0,(24+labelHeight*scale*.5f-height*.5f)*rise,0);
            var own=state.ForSeat(player.Seat.Value);
            bool lifeCounterInCentre=own.DiedAt>=0&&now-own.DiedAt<2.2&&rise<1;
            shooterAnnouncementLayer.style.opacity=lifeCounterInCentre?0:Mathf.Clamp01((float)(elapsed/.18));Show(shooterAnnouncementLayer,true);
        }
    }
}
