using Unity.Netcode;
using UnityEngine;

namespace KitchenChaos.Networking
{
    // Step 5: owned network placeholder; movement and carrying belong to step 6.
    public sealed class KitchenNetworkChef : NetworkBehaviour
    {
        public GameObject hostVisual;
        public GameObject guestVisual;
        public override void OnNetworkSpawn()
        {
            bool host = OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
            hostVisual.SetActive(host);
            guestVisual.SetActive(!host);
            name = host ? "Player_Host_P1" : "Player_Client_P2";
        }
    }
}
