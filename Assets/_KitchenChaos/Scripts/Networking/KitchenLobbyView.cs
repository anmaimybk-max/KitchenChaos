using UnityEngine;

namespace KitchenChaos.Networking
{
    // Functional step-5 UI. Final Menu/Settings/Credits/HUD styling is a later milestone.
    public sealed class KitchenLobbyView : MonoBehaviour
    {
        public KitchenNetworkSession session;
        private string code = "";
        private GUIStyle heading, body;

        private void OnGUI()
        {
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            }
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 760f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            var lobby = session.lobby;
            bool connected = session.Connected && lobby != null && lobby.IsSpawned;
            bool kitchen = connected && lobby.Phase.Value == KitchenPhase.Kitchen;
            GUILayout.BeginArea(new Rect((width - 650) / 2, kitchen ? 10 : 65, 650, kitchen ? 170 : 630), GUI.skin.box);
            GUILayout.Label("KITCHEN CHAOS", heading);
            GUILayout.Space(12);
            GUI.enabled = !session.Busy && !session.Leaving;
            if (!connected)
            {
                GUILayout.Label("Cùng một người bạn chuẩn bị ca bếp", body);
                GUILayout.Space(16);
                if (GUILayout.Button("Tạo phòng (Host)", GUILayout.Height(44))) _ = session.CreateRoomAsync();
                GUILayout.Space(14);
                GUILayout.Label("Mã phòng từ Host", body);
                code = GUILayout.TextField(code, 16, GUILayout.Height(36));
                if (GUILayout.Button("Tham gia phòng (Client)", GUILayout.Height(44))) _ = session.JoinRoomAsync(code);
            }
            else if (kitchen)
            {
                GUILayout.Label("Hai người đã vào bếp. Di chuyển và tương tác sẽ có ở bước 6.", body);
            }
            else
            {
                GUILayout.Label("PHÒNG CHỜ · " + lobby.PlayerCount.Value + "/2", body);
                GUILayout.Label("Mã phòng: " + session.RoomCode, body);
                if (!string.IsNullOrEmpty(session.RoomCode) && GUILayout.Button("Sao chép mã", GUILayout.Height(30))) GUIUtility.systemCopyBuffer = session.RoomCode;
                GUILayout.Space(12);
                GUILayout.Label("P1 · Host: " + (lobby.HostReady.Value ? "Sẵn sàng" : "Chưa sẵn sàng"), body);
                GUILayout.Label("P2 · Client: " + (lobby.PlayerCount.Value < 2 ? "Chưa có người" : lobby.GuestReady.Value ? "Sẵn sàng" : "Chưa sẵn sàng"), body);
                GUILayout.Space(12);
                if (lobby.Phase.Value == KitchenPhase.Lobby)
                {
                    bool ready = session.manager.IsHost ? lobby.HostReady.Value : lobby.GuestReady.Value;
                    if (GUILayout.Button(ready ? "Hủy sẵn sàng" : "Sẵn sàng", GUILayout.Height(44))) lobby.SetLocalReady(!ready);
                    if (session.manager.IsHost)
                    {
                        GUI.enabled = lobby.CanStart;
                        if (GUILayout.Button("Bắt đầu ca bếp", GUILayout.Height(44))) lobby.BeginCountdown();
                        GUI.enabled = !session.Busy && !session.Leaving;
                    }
                    GUILayout.Label("Chỉ Host bắt đầu khi đúng hai người đều sẵn sàng.", body);
                }
                else GUILayout.Label("Bắt đầu sau " + lobby.Countdown + "…", heading);
            }
            GUILayout.Space(12);
            if (connected && GUILayout.Button("Rời phòng", GUILayout.Height(36))) _ = session.LeaveAsync();
            GUI.enabled = true;
            GUILayout.Label(session.Status, body);
            GUILayout.EndArea();
        }
    }
}
