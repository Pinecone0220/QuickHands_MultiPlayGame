using System;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace HelloWorld
{
    public partial class HelloWorldManager
    {
        public string RelayCode { get; private set; } = "";
        private int relayRequest;

        public void HostRoom(string unused) => StartRelay(false, "");
        public void JoinRoom(string code, string unused) => StartRelay(true, code);

        private async void StartRelay(bool join, string code)
        {
            if (network == null || Busy || network.IsListening || network.ShutdownInProgress) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (join && code.Length == 0) { Status = "방 코드를 입력하세요."; return; }
            int request = ++relayRequest;
            RelayCode = "";
            Busy = true;
            connectDeadline = Time.realtimeSinceStartupAsDouble + 30;
            Status = join ? "방에 연결하는 중…" : "방을 만드는 중…";
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (request != relayRequest || this == null) return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (request != relayRequest || this == null) return;
                var transport = network.NetworkConfig.NetworkTransport as UnityTransport;
                if (transport == null) throw new InvalidOperationException("Unity Transport가 없습니다.");
                if (join)
                {
                    var allocation = await RelayService.Instance.JoinAllocationAsync(code);
                    if (request != relayRequest || this == null) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                }
                else
                {
                    var allocation = await RelayService.Instance.CreateAllocationAsync(RouletteRules.MaxPlayers - 1);
                    if (request != relayRequest || this == null) return;
                    code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if (request != relayRequest || this == null) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                }
                Busy = false;
                RelayCode = join ? "" : code;
                Begin(DuelMode.PvP, join, "", "7777", true);
                if (!Busy) RelayCode = "";
            }
            catch (Exception exception)
            {
                if (request != relayRequest || this == null) return;
                Debug.LogWarning("Relay 연결 실패: " + exception);
                RelayCode = "";
                Busy = false;
                Status = join ? "방에 연결하지 못했습니다. 방 코드와 네트워크를 확인하세요." :
                    "방을 만들지 못했습니다. 네트워크와 Unity 서비스 설정을 확인하세요.";
            }
        }

        private void CancelRelayRequest()
        {
            relayRequest++;
            RelayCode = "";
        }
    }
}
