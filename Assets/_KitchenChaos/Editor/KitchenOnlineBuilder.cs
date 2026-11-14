using System;
using System.IO;
using System.Linq;
using KitchenChaos.Layout;
using KitchenChaos.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace KitchenChaos.EditorTools
{
    public static class KitchenOnlineBuilder
    {
        public const string ScenePath = "Assets/_KitchenChaos/Scenes/KitchenOnline.unity";
        const string PlayerPath = "Assets/_KitchenChaos/Prefabs/Network/KitchenNetworkChef.prefab";

        [MenuItem("Kitchen Chaos/Step 5/Open Online Lobby")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void BuildScene()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Online scene exists; do not overwrite user edits.");
            EditorSceneManager.OpenScene("Assets/_KitchenChaos/Scenes/KitchenLayout_Graybox.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
            AssetDatabase.Refresh();
            var markers = Object.FindObjectsByType<KitchenLayoutMarker>(FindObjectsSortMode.None);
            var host = markers.Single(m => m.name == "Spawn_P1");
            var guest = markers.Single(m => m.name == "Spawn_P2");
            var player = new GameObject("KitchenNetworkChef");
            player.AddComponent<NetworkObject>();
            var chef = player.AddComponent<KitchenNetworkChef>();
            chef.hostVisual = Object.Instantiate(host.transform.Find("Placeholder_Mannequin").gameObject, player.transform);
            chef.hostVisual.name = "HostVisual"; chef.hostVisual.transform.localPosition = Vector3.zero;
            chef.guestVisual = Object.Instantiate(guest.transform.Find("Placeholder_Mannequin").gameObject, player.transform);
            chef.guestVisual.name = "GuestVisual"; chef.guestVisual.transform.localPosition = Vector3.zero;
            chef.guestVisual.SetActive(false);
            foreach (var t in player.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            Object.DestroyImmediate(player);
            host.transform.Find("Placeholder_Mannequin").gameObject.SetActive(false);
            guest.transform.Find("Placeholder_Mannequin").gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(host.transform.Find("Placeholder_Mannequin").gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(guest.transform.Find("Placeholder_Mannequin").gameObject);

            var net = new GameObject("NetworkSession");
            var transport = net.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            var manager = net.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = prefab;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.ClientConnectionBufferTimeout = 15;
            var stateObject = new GameObject("HostAuthoritativeLobby");
            stateObject.AddComponent<NetworkObject>();
            var lobby = stateObject.AddComponent<KitchenLobbyState>();
            var session = net.AddComponent<KitchenNetworkSession>();
            session.manager = manager; session.lobby = lobby;
            session.hostSpawn = host.transform; session.guestSpawn = guest.transform;
            var ui = new GameObject("LobbyUI_Step5").AddComponent<KitchenLobbyView>(); ui.session = session;
            var test = net.AddComponent<KitchenNetworkSmokeTest>(); test.session = session;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("KC_STEP5_SCENE_SAVED");
            BuildWindows();
        }

        [MenuItem("Kitchen Chaos/Step 5/Build Windows Network Review")]
        public static void BuildWindows()
        {
            // Programmatic creation must validate NGO identifiers after assets have persistent GUIDs.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            player.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
            PrefabUtility.SavePrefabAsset(player);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            foreach (var obj in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                obj.SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            uint playerHash = (uint)new SerializedObject(player.GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue;
            uint lobbyHash = (uint)new SerializedObject(Object.FindFirstObjectByType<KitchenLobbyState>().GetComponent<NetworkObject>()).FindProperty("GlobalObjectIdHash").longValue;
            if (playerHash == 0 || lobbyHash == 0 || playerHash == lobbyHash) throw new Exception("Invalid/duplicate NGO object hashes.");
            Debug.Log("KC_OBJECT_HASHES_VALID");
            string path = Path.GetFullPath("Builds/Step5Network/KitchenChaos.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + result.summary.result);
            Debug.Log("KC_STEP5_BUILD_PASS " + result.summary.totalErrors + " errors");
        }
    }
}
