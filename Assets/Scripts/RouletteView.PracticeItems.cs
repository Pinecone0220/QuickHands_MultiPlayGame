using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private void UpdatePracticeItems(HelloWorldPlayer player)
        {
            var state=player.State.Value;var a=state.ForSeat(player.Seat.Value);
            if(itemRow.parent!=root){itemRow.RemoveFromHierarchy();root.Add(itemRow);}
            itemRow.EnableInClassList("ink-live-items",false);itemRow.EnableInClassList("practice-items",true);
            Show(itemRow,true);
            double now=player.NetworkManager.ServerTime.Time;
            for(int i=0;i<itemButtons.Length;i++)
            {
                var item=(DuelItem)i;bool selected=DuelLoadout.Has(a.SelectedItems,item);
                Show(itemButtons[i],selected);Show(itemStatuses[i],false);
                bool equipped=a.QuickEquipped&&a.QuickItem==i+1;
                bool active=equipped||item==DuelItem.NoruFace&&a.NoruFaceActive||item==DuelItem.Bullet&&a.BulletUsed;
                bool throwing=DuelInk.Busy(a.Ink,now)&&!a.Ink.Armed;
                bool flying=a.Ink.ThrownAt>=0&&a.Ink.ImpactAt<a.Ink.ThrownAt&&now<a.Ink.ThrownAt+DuelInk.ReleaseDelay+DuelInk.Lifetime;
                itemButtons[i].EnableInClassList("item-used",active);
                itemButtons[i].EnableInClassList("item-spent",false);itemButtons[i].EnableInClassList("item-cooldown",false);
                itemButtons[i].SetEnabled(selected&&!a.IsStunned(now)&&!((item==DuelItem.Taser||item==DuelItem.Ink)&&throwing)&&!(item==DuelItem.Ink&&flying&&!equipped));
            }
            Show(recoveryUsedMark,false);
        }
    }
}
