using Unity.Netcode;
using UnityEngine;

namespace HelloWorld
{
    // Retains the existing script GUID and registered Player prefab.
    public class HelloWorldPlayer : NetworkBehaviour
    {
        public NetworkVariable<DuelSnapshot> State = new NetworkVariable<DuelSnapshot>();
        public NetworkVariable<int> Seat = new NetworkVariable<int>(-1);
        public NetworkVariable<DuelDamageReport> DeathReport = new NetworkVariable<DuelDamageReport>(default,
            NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        public static HelloWorldPlayer Local { get; private set; }

        public override void OnNetworkSpawn()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (IsOwner) Local = this;
            if (IsServer) HelloWorldManager.Instance?.RegisterPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
        }

        public void InputDigit(int digit, int version)
        {
            if (IsSpawned && IsOwner) InputDigitRpc(digit, version);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InputDigitRpc(int digit, int version, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HelloWorldManager.Instance?.RequestInput(OwnerClientId, digit, version);
        }

        public void Rematch()
        {
            if (IsSpawned && IsOwner) RematchRpc(State.Value.Round);
        }
        public void StopClock(double sample)
        { if(IsSpawned && IsOwner) StopClockRpc(State.Value.Round,State.Value.Clock.Iteration,sample); }
        public void InputMemory(int direction,int memoryRound,int index)
        { if(IsSpawned && IsOwner) InputMemoryRpc(State.Value.Round,State.Value.Clock.Iteration,memoryRound,index,direction); }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void InputMemoryRpc(int round,int iteration,int memoryRound,int index,int direction,RpcParams rpcParams=default)
        {
            if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;
            HelloWorldManager.Instance?.RequestMemoryInput(OwnerClientId,round,iteration,memoryRound,index,direction);
        }
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        public void ReportMemoryInputRpc(int round,int iteration,int memoryRound,bool accepted,int count)
        { if(IsOwner) HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReceiveMemoryInput(round,iteration,memoryRound,accepted,count); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void StopClockRpc(int round,int iteration,double sample,RpcParams rpcParams=default)
        {
            if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;
            HelloWorldManager.Instance?.RequestStopClock(OwnerClientId,round,iteration,sample);
        }
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void ReportClockStopRpc(int round,int iteration,bool accepted,int record,MiniFailure failure)
        { if(IsOwner) HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportClockStop(round,iteration,accepted,record,failure); }
        public void UseItem(DuelItem item)
        {if(IsSpawned&&IsOwner)UseItemRpc(item,State.Value.Round,State.Value.Clock.Iteration);}
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void UseItemRpc(DuelItem item,int round,int iteration,RpcParams rpcParams=default)
        {if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;HelloWorldManager.Instance?.RequestItem(OwnerClientId,item,round,iteration);}
        public void Look(float yaw,float pitch,bool ads)
        { if (IsSpawned && IsOwner) LookRpc(yaw,pitch,ads,State.Value.Round); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        private void LookRpc(float yaw,float pitch,bool ads,int round,RpcParams rpcParams=default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HelloWorldManager.Instance?.RequestLook(OwnerClientId,yaw,pitch,ads,round);
        }
        public void Fire(float yaw,float pitch,bool ads,double shotTime)
        { if (IsSpawned && IsOwner) FireRpc(yaw,pitch,ads,State.Value.Round,State.Value.ForSeat(Seat.Value).Version,shotTime); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void FireRpc(float yaw,float pitch,bool ads,int round,int version,double shotTime,RpcParams rpcParams=default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HelloWorldManager.Instance?.RequestFire(OwnerClientId,yaw,pitch,ads,round,version,shotTime);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void ReportHitRpc(int round,int life,int shot)
        {
            if(IsOwner)HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportHit(round,life,shot);
        }
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void ReportKillRpc(int round, int shot, int victimSeat, DuelWeapon weapon, Unity.Collections.FixedString64Bytes attacker, Unity.Collections.FixedString64Bytes victim,int victimLife,int remainingLives,DuelHitPart part)
        {
            if (IsOwner) HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportKill(round, shot, victimSeat, weapon, attacker.ToString(), victim.ToString(),victimLife,remainingLives,part);
        }

        public void SelectWeapon(DuelWeapon weapon)
        {
            if (IsSpawned && IsOwner) SelectWeaponRpc(weapon, State.Value.Round);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SelectWeaponRpc(DuelWeapon weapon, int round, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HelloWorldManager.Instance?.RequestWeapon(OwnerClientId, weapon, round);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RematchRpc(int round, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            HelloWorldManager.Instance?.RequestRematch(OwnerClientId, round);
        }
    }
}



