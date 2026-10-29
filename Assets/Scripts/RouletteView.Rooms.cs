using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private TextField hostAddressInput;
        private Button hostRoomButton, joinRoomButton;
        private Button relayCodeButton;
        private void BuildRoomMenu(VisualElement panel)
        {
            hostAddressInput = new TextField("방 코드") { maxLength = 12 };
            panel.Add(hostAddressInput);
            var buttons = Box(panel, "button-row");
            hostRoomButton = Button(buttons, "방 만들기", () => manager.HostRoom(""), "secondary");
            joinRoomButton = Button(buttons, "참가하기", () => manager.JoinRoom(hostAddressInput.value, ""), "secondary");
            Button(panel, "운을 시험하기", () => SetGachaOpen(true), "secondary").AddToClassList("menu-extra-action");
            Button(panel, "스킨 보관함", OpenSkinInventory, "secondary").AddToClassList("menu-extra-action");
        }
        private void BuildRelayCodeButton(VisualElement panel)
        {
            relayCodeButton = Button(panel, "", () => GUIUtility.systemCopyBuffer = manager.RelayCode, "secondary");
            relayCodeButton.AddToClassList("relay-code-button");
            Show(relayCodeButton, false);
        }
        private void RefreshConnectionMenu()
        {
            bool idle = !manager.Busy;
            hostAddressInput.SetEnabled(idle);
            hostRoomButton.SetEnabled(idle);
            joinRoomButton.SetEnabled(idle);
            if (relayCodeButton != null)
            {
                var player = HelloWorldPlayer.Local;
                bool visible = manager.Busy && !string.IsNullOrEmpty(manager.RelayCode) &&
                    player != null && player.IsSpawned && player.Seat.Value >= 0 &&
                    player.State.Value.Stage == DuelStage.Idle && player.State.Value.Phase == DuelPhase.Waiting &&
                    !RouletteRules.Has(player.State.Value.ReadyMask, player.Seat.Value);
                Show(relayCodeButton, visible);
                if (visible)
                {
                    relayCodeButton.text = "방 코드 " + manager.RelayCode + " · 복사";
                    relayCodeButton.BringToFront();
                }
            }
        }
    }
}
