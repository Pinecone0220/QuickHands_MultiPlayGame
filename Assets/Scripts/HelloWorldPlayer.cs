using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

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

        public bool SendChat(string message)
        {
            if(!IsSpawned||!IsOwner)return false;
            string clean=DuelChatLog.Normalize(message);if(clean.Length==0)return false;
            SendChatRpc(new FixedString512Bytes(clean));return true;
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void SendChatRpc(FixedString512Bytes message,RpcParams rpcParams=default)
        {
            if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;
            HelloWorldManager.Instance?.RequestChat(OwnerClientId,message);
        }
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        public void ReceiveChatRpc(int sequence,ulong sender,FixedString64Bytes name,FixedString512Bytes message)
        {
            if(IsOwner)HelloWorldManager.Instance?.GetComponent<DuelChat>()?.Receive(
                new DuelChatLine{Sequence=sequence,Sender=sender,Name=name,Message=message});
        }
        [Rpc(SendTo.Owner,InvokePermission=RpcInvokePermission.Server)]
        public void RejectChatRpc()
        {if(IsOwner)HelloWorldManager.Instance?.GetComponent<DuelChat>()?.Rejected();}

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
        public void StopDice()
        {if(IsSpawned&&IsOwner)StopDiceRpc(State.Value.Round,State.Value.Clock.Iteration,State.Value.Clock.Dice.Round);}
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void StopDiceRpc(int round,int iteration,int diceRound,RpcParams rpcParams=default)
        {
            if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;
            HelloWorldManager.Instance?.RequestStopDice(OwnerClientId,round,iteration,diceRound);
        }
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
        public void SelectLoadoutItem(DuelItem item)
        {if(IsSpawned&&IsOwner)SelectLoadoutItemRpc(item,State.Value.Round);}
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void SelectLoadoutItemRpc(DuelItem item,int round,RpcParams rpcParams=default)
        {
            if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;
            HelloWorldManager.Instance?.RequestLoadoutItem(OwnerClientId,item,round);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void UseItemRpc(DuelItem item,int round,int iteration,RpcParams rpcParams=default)
        {if(rpcParams.Receive.SenderClientId!=OwnerClientId)return;HelloWorldManager.Instance?.RequestItem(OwnerClientId,item,round,iteration);}
        public void Look(float yaw,float pitch,bool ads)
        { if (IsSpawned && IsOwner) LookRpc(yaw,pitch,ads,State.Value.Round); }
        public void ThrowInk(float yaw,float pitch,double sample)
        {
            if(!IsSpawned||!IsOwner||Seat.Value<0)return;
            var state=State.Value;var own=state.ForSeat(Seat.Value);
            ThrowInkRpc(yaw,pitch,sample,state.Round,state.Clock.Iteration,own.Life,own.Ink.Version);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void ThrowInkRpc(float yaw,float pitch,double sample,int round,int iteration,int life,int version,RpcParams rpcParams=default)
        {if(rpcParams.Receive.SenderClientId==OwnerClientId)HelloWorldManager.Instance?.RequestInk(OwnerClientId,yaw,pitch,sample,round,iteration,life,version);}
        public void SwitchQuickItem(bool equip)
        {
            if(!IsSpawned||!IsOwner||Seat.Value<0)return;var s=State.Value;var a=s.ForSeat(Seat.Value);
            SwitchQuickItemRpc(equip,s.Round,s.Clock.Iteration,a.Life,a.QuickItemVersion);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void SwitchQuickItemRpc(bool equip,int round,int iteration,int life,int version,RpcParams rpcParams=default)
        {if(rpcParams.Receive.SenderClientId==OwnerClientId)HelloWorldManager.Instance?.RequestQuickItem(OwnerClientId,equip,round,iteration,life,version);}
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
        public void ReportHitRpc(int round,int life,int shot,bool headshot)
        {
            if(IsOwner)HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportHit(round,life,shot,headshot);
        }
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void ReportPracticeHitRpc(int round,int session,int shot,bool headshot)
        {
            if(IsOwner)HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportPracticeHit(round,session,shot,headshot);
        }
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        public void ReportKillRpc(int round, int shot, int victimSeat, DuelWeapon weapon, Unity.Collections.FixedString64Bytes attacker, Unity.Collections.FixedString64Bytes victim,int victimLife,int remainingLives,DuelHitPart part,bool fatal=true)
        {
            if (IsOwner) HelloWorldManager.Instance?.GetComponent<RouletteView>()?.ReportKill(round, shot, victimSeat, weapon, attacker.ToString(), victim.ToString(),victimLife,remainingLives,part,fatal);
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



