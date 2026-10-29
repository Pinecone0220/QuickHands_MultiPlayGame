using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private void ToggleQuickInventory()
        {
            var p=HelloWorldPlayer.Local;
            if(p==null||!p.IsSpawned||p.Seat.Value<0)return;
            var s=p.State.Value;var a=s.ForSeat(p.Seat.Value);
            if(s.IsPracticing(p.Seat.Value)&&a.QuickEquipped){p.SwitchQuickItem(false);return;}
            if(s.Phase==DuelPhase.Playing&&s.Stage==DuelStage.Racing&&s.Clock.Phase==ClockPhase.QuickHands&&s.Alive(p.Seat.Value)&&a.QuickItem>=2)
                p.SwitchQuickItem(!a.QuickEquipped);
        }
        private HoverInfo LocalEquipmentHover()
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||p.Seat.Value<0)return default;
            var s=p.State.Value;var a=s.ForSeat(spectator!=null&&spectator.Watching?spectator.TargetSeat:p.Seat.Value);
            int seat=spectator!=null&&spectator.Watching?spectator.TargetSeat:p.Seat.Value;
            return a.Ink.Armed||s.CanPreviewEquipment(seat)&&a.PreviewItem==(int)DuelWheelSlot.Ink?ItemHoverInfo(DuelItem.Ink):a.QuickEquipped&&a.QuickItem==2||a.TaserArmed&&a.Weapon==DuelWeapon.Taser||s.CanPreviewEquipment(seat)&&a.PreviewItem==(int)DuelWheelSlot.Taser?ItemHoverInfo(DuelItem.Taser):WeaponHoverInfo(a.SelectedWeapon);
        }
    }
}
