using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace KitchenChaos.Networking
{
    public sealed class KitchenNetworkSession : MonoBehaviour
    {
        public NetworkManager manager;
        public KitchenLobbyState lobby;
        public Transform hostSpawn;
        public Transform guestSpawn;
        public bool Busy { get; private set; }
        public bool Leaving { get; private set; }
        public string Status { get; private set; } = "Tạo phòng hoặc nhập mã để cùng vào bếp.";
        public string RoomCode => session == null ? "" : session.Code;
        public bool Connected => manager != null && manager.IsConnectedClient;
        private ISession session;
        private bool wasSessionHost;
        private bool quitting;
        private bool hadConnection;
        private CancellationTokenSource operation;
        private readonly HashSet<ulong> participants = new HashSet<ulong>();
        private readonly Dictionary<ulong, float> reserved = new Dictionary<ulong, float>();
        private static readonly byte[] Protocol = Encoding.UTF8.GetBytes("KitchenChaos-A02-Step5-v1");

        private void Awake()
        {
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = Protocol;
            manager.ConnectionApprovalCallback = Approve;
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
            manager.OnTransportFailure += OnTransportFailure;
            manager.OnClientStopped += OnClientStopped;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            foreach (var id in reserved.Where(p => Time.realtimeSinceStartup - p.Value > 15).Select(p => p.Key).ToArray()) reserved.Remove(id);
            bool host = request.ClientNetworkId == NetworkManager.ServerClientId;
            bool compatible = request.Payload != null && request.Payload.SequenceEqual(Protocol);
            int count = manager.ConnectedClientsIds.Count + reserved.Keys.Count(id => !manager.ConnectedClients.ContainsKey(id));
            bool lobbyOpen = !lobby.IsSpawned || lobby.Phase.Value == KitchenPhase.Lobby;
            response.Approved = compatible && lobbyOpen && (host || count < 2);
            Debug.Log($"KC_APPROVAL id={request.ClientNetworkId} count={count} compatible={compatible} approved={response.Approved}");
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            response.Reason = !compatible ? "Phiên bản trò chơi không khớp." : !lobbyOpen ? "Ca bếp đã bắt đầu." : "Phòng đã đủ hai người.";
            if (!response.Approved) return;
            response.Position = (host ? hostSpawn : guestSpawn).position;
            response.Rotation = Quaternion.identity;
            reserved[request.ClientNetworkId] = Time.realtimeSinceStartup;
        }

        public Task CreateRoomAsync() => ConnectOnlineAsync(null);
        public Task JoinRoomAsync(string code) => ConnectOnlineAsync((code ?? "").Trim().ToUpperInvariant());

        private async Task ConnectOnlineAsync(string code)
        {
            if (Busy || Leaving || manager.IsListening) return;
            if (code != null && (code.Length == 0 || code.Length > 16 || !code.All(char.IsLetterOrDigit)))
            { Status = "Nhập mã phòng hợp lệ do Host cung cấp."; return; }
            if (string.IsNullOrWhiteSpace(Application.cloudProjectId))
            { Status = "Dự án chưa liên kết Unity Gaming Services. Xem Docs/Step5_Network_Guide.md để thiết lập."; return; }
            Busy = true;
            operation = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            var token = operation.Token;
            Status = code == null ? "Đang tạo phòng…" : "Đang tham gia phòng…";
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                token.ThrowIfCancellationRequested();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    // Separate profiles allow two Windows instances to test with different identities.
                    AuthenticationService.Instance.SwitchProfile("kc" + System.Diagnostics.Process.GetCurrentProcess().Id);
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                token.ThrowIfCancellationRequested();
                wasSessionHost = code == null;
                var joined = code == null
                    ? await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { Name = "Kitchen Chaos", MaxPlayers = 2, IsPrivate = true }.WithRelayNetwork(), token)
                    : await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions(), token);
                if (quitting || token.IsCancellationRequested)
                {
                    if (wasSessionHost) await joined.AsHost().DeleteAsync(); else await joined.LeaveAsync();
                    return;
                }
                session = joined;
                session.RemovedFromSession += OnSessionRemoved;
                session.Deleted += OnSessionRemoved;
                session.SessionHostChanged += OnSessionHostChanged;
                Status = "Đã vào phòng. Cả hai người cần bấm Sẵn sàng.";
            }
            catch (OperationCanceledException) { await LeaveAsync("Kết nối quá thời gian. Hãy thử lại."); }
            catch (Exception ex)
            {
                Debug.LogWarning("Kitchen online connection failed: " + ex.GetType().Name);
                await LeaveAsync("Không thể vào phòng. Kiểm tra mã, mạng và cấu hình Unity Gaming Services rồi thử lại.");
            }
            finally { Busy = false; operation?.Dispose(); operation = null; }
        }

        private void OnConnected(ulong id)
        {
            reserved.Remove(id);
            participants.Add(id);
            Debug.Log($"KC_CONNECTED id={id} local={manager.LocalClientId} server={manager.IsServer} count={manager.ConnectedClientsIds.Count} lobbySpawned={lobby.IsSpawned}");
            if (id == manager.LocalClientId) hadConnection = true;
            if (manager.IsServer) lobby.ResetReadiness();
        }

        private void OnDisconnected(ulong id)
        {
            reserved.Remove(id);
            if (Leaving || quitting) return;
            Debug.Log($"KC_DISCONNECTED id={id} local={manager.LocalClientId} server={manager.IsServer}");
            bool wasParticipant = participants.Remove(id);
            if (manager.IsServer && id != NetworkManager.ServerClientId && !wasParticipant) return;
            if (manager.IsServer && id != NetworkManager.ServerClientId)
            {
                if (lobby.IsSpawned && lobby.Phase.Value != KitchenPhase.Lobby)
                    _ = LeaveAsync("Người còn lại mất kết nối. Ca bếp kết thúc; hãy tạo phòng mới.");
                else
                {
                    lobby.ResetReadiness();
                    Status = "Client đã rời phòng. Đang chờ người thứ hai; cần chọn Sẵn sàng lại.";
                }
            }
            else if (id == manager.LocalClientId)
                _ = LeaveAsync(hadConnection ? "Đã mất kết nối với Host. Phiên chơi kết thúc." :
                    string.IsNullOrEmpty(manager.DisconnectReason) ? "Không kết nối được với phòng." : manager.DisconnectReason);
        }

        private void OnTransportFailure() { if (!quitting && !Leaving) _ = LeaveAsync("Kết nối mạng bị gián đoạn. Hãy tạo hoặc tham gia lại phòng."); }
        private void OnClientStopped(bool wasHost) { if (hadConnection && !Leaving && !quitting) _ = LeaveAsync("Phiên chơi đã kết thúc."); }
        private void OnSessionRemoved() { if (!quitting && !Leaving) _ = LeaveAsync("Phòng đã đóng hoặc kết nối đã hết hạn."); }
        private void OnSessionHostChanged(string hostId) { if (!quitting && !Leaving) _ = LeaveAsync("Host đã rời phòng. Phiên chơi kết thúc."); }

        public async Task LeaveAsync(string message = "Đã rời phòng.")
        {
            if (Leaving) return;
            Leaving = true;
            operation?.Cancel();
            Status = message;
            var old = session;
            session = null;
            if (old != null)
            {
                old.RemovedFromSession -= OnSessionRemoved;
                old.Deleted -= OnSessionRemoved;
                old.SessionHostChanged -= OnSessionHostChanged;
            }
            // Stop gameplay immediately; service cleanup may take longer if Internet is lost.
            if (manager != null && manager.IsListening) manager.Shutdown();
            reserved.Clear(); participants.Clear();
            hadConnection = false;
            try
            {
                if (old != null)
                {
                    var cleanup = wasSessionHost ? old.AsHost().DeleteAsync() : old.LeaveAsync();
                    // Observe late failures without blocking the menu indefinitely.
                    var observed = ObserveCleanupAsync(cleanup);
                    await Task.WhenAny(observed, Task.Delay(8000));
                }
                while (manager != null && manager.ShutdownInProgress && !quitting) await Task.Yield();
            }
            finally { Leaving = false; }
        }

        private static async Task ObserveCleanupAsync(Task task)
        {
            try { await task; }
            catch (Exception ex) { Debug.LogWarning("Session cleanup pending/failed: " + ex.GetType().Name); }
        }

        private void OnApplicationQuit() { quitting = true; operation?.Cancel(); }
        private void OnDestroy()
        {
            quitting = true; operation?.Cancel();
            if (manager == null) return;
            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnTransportFailure -= OnTransportFailure;
            manager.OnClientStopped -= OnClientStopped;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit diagnostic route only, absent from release builds and normal UI.
        public bool StartLoopbackTest(bool host, ushort port)
        {
            if (Busy || Leaving || manager.IsListening) return false;
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port, "127.0.0.1");
            Status = "Kiểm thử nội bộ loopback — không phải phòng online.";
            return host ? manager.StartHost() : manager.StartClient();
        }
#endif
    }
}
