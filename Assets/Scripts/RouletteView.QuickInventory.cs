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
            if(s.Phase==DuelPhase.Playing&&s.Stage==DuelStage.Racing&&s.Clock.Phase==ClockPhase.QuickHands&&s.Alive(p.Seat.Value)&&a.QuickItem>=2)
                p.SwitchQuickItem(!a.QuickEquipped);
        }
        private HoverInfo LocalEquipmentHover()
        {
            var p=HelloWorldPlayer.Local;if(p==null||!p.IsSpawned||p.Seat.Value<0)return default;
            var a=p.State.Value.ForSeat(p.Seat.Value);return a.QuickEquipped?ItemHoverInfo((DuelItem)(a.QuickItem-1)):WeaponHoverInfo(a.SelectedWeapon);
        }
    }
}
