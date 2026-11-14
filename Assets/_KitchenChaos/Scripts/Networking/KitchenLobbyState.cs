using Unity.Netcode;
using UnityEngine;

namespace KitchenChaos.Networking
{
    public enum KitchenPhase : byte { Lobby, Countdown, Kitchen }

    // All lobby decisions and countdown timestamps are written by the Host.
    public sealed class KitchenLobbyState : NetworkBehaviour
    {
        public readonly NetworkVariable<int> PlayerCount = new NetworkVariable<int>();
        public readonly NetworkVariable<bool> HostReady = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> GuestReady = new NetworkVariable<bool>();
        public readonly NetworkVariable<KitchenPhase> Phase = new NetworkVariable<KitchenPhase>();
        public readonly NetworkVariable<double> StartsAt = new NetworkVariable<double>();
        public bool CanStart => IsSpawned && IsServer && Phase.Value == KitchenPhase.Lobby &&
            PlayerCount.Value == 2 && HostReady.Value && GuestReady.Value;
        public int Countdown => IsSpawned ? Mathf.Max(0, Mathf.CeilToInt((float)(StartsAt.Value - NetworkManager.ServerTime.Time))) : 0;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Phase.Value = KitchenPhase.Lobby;
            StartsAt.Value = 0;
            ResetReadiness();
        }

        public void ResetReadiness()
        {
            if (!IsServer || !IsSpawned) return;
            PlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
            HostReady.Value = false;
            GuestReady.Value = false;
        }

        public void SetLocalReady(bool ready)
        {
            if (IsSpawned && IsClient) SetReadyRpc(ready);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SetReadyRpc(bool ready, RpcParams rpc = default)
        {
            if (Phase.Value != KitchenPhase.Lobby) return;
            ulong sender = rpc.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.ContainsKey(sender)) return;
            // The caller supplies only its readiness, never another player's identity.
            if (sender == Unity.Netcode.NetworkManager.ServerClientId) HostReady.Value = ready;
            else GuestReady.Value = ready;
        }

        public bool BeginCountdown()
        {
            if (!CanStart || NetworkManager.ConnectedClientsIds.Count != 2) return false;
            StartsAt.Value = NetworkManager.ServerTime.Time + 3.0;
            Phase.Value = KitchenPhase.Countdown;
            return true;
        }

        private void Update()
        {
            if (IsSpawned && IsServer && Phase.Value == KitchenPhase.Countdown &&
                NetworkManager.ConnectedClientsIds.Count == 2 && NetworkManager.ServerTime.Time >= StartsAt.Value)
                Phase.Value = KitchenPhase.Kitchen;
        }
    }
}
