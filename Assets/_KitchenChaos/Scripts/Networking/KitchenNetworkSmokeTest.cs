using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace KitchenChaos.Networking
{
    // Only explicit development command-line flags run these tests; normal play does not.
    public sealed class KitchenNetworkSmokeTest : MonoBehaviour
    {
        public KitchenNetworkSession session;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string role, scenario, report;
        private float deadline;
        private int failures;
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string key, string fallback = "") { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
            role = Arg("-kcRole");
            if (string.IsNullOrEmpty(role))
            {
                string screenshot = Arg("-kcScreenshot");
                if (!string.IsNullOrEmpty(screenshot))
                {
                    yield return new WaitForSecondsRealtime(3);
                    ScreenCapture.CaptureScreenshot(screenshot);
                    yield return new WaitForSecondsRealtime(2);
                    Application.Quit();
                }
                yield break;
            }
            scenario = Arg("-kcScenario", "normal");
            report = Arg("-kcReport");
            deadline = Time.realtimeSinceStartup + 55;
            File.WriteAllText(report, "Scenario=" + scenario + " Role=" + role + "\n");
            bool host = role == "host";
            Check("Transport starts", session.StartLoopbackTest(host, ushort.Parse(Arg("-kcPort", "17777"))));
            if (role == "third")
            {
                yield return new WaitForSecondsRealtime(5);
                Check("Third player rejected", !session.Connected);
                Check("Capacity reason received", session.Status.Contains("hai người"));
                Finish(); yield break;
            }
            yield return Until(() => session.Connected && session.lobby.IsSpawned);
            if (!session.Connected) { Check("Connected", false); Finish(); yield break; }
            Check("Lobby starts clean", session.lobby.Phase.Value == KitchenPhase.Lobby);
            if (host)
            {
                Check("Cannot start with one player", !session.lobby.BeginCountdown());
                yield return Until(() => session.lobby.PlayerCount.Value == 2);
                Check("Exactly two clients connected", session.manager.ConnectedClientsIds.Count == 2);
                Check("Cannot start without both ready", !session.lobby.BeginCountdown());
                session.lobby.SetLocalReady(true);
                if (scenario == "retry")
                {
                    yield return Until(() => session.lobby.PlayerCount.Value == 1);
                    Check("Client leaving lobby keeps Host in lobby", session.Connected && session.lobby.Phase.Value == KitchenPhase.Lobby);
                    Check("Readiness clears when client leaves", !session.lobby.HostReady.Value && !session.lobby.GuestReady.Value);
                    yield return Until(() => session.lobby.PlayerCount.Value == 2);
                    session.lobby.SetLocalReady(true);
                }
                yield return Until(() => session.lobby.CanStart);
                // Window for a third process to attempt joining a full, still-open lobby.
                yield return new WaitForSecondsRealtime(scenario == "normal" ? 8 : 1);
                Check("Two ready enables start", session.lobby.CanStart);
                double before = session.manager.ServerTime.Time;
                Check("Host begins countdown", session.lobby.BeginCountdown());
                Check("Countdown duration exactly three seconds", Math.Abs(session.lobby.StartsAt.Value - before - 3) < .001);
            }
            else
            {
                Check("Client cannot start game", !session.lobby.BeginCountdown());
                if (scenario == "retry")
                {
                    yield return new WaitForSecondsRealtime(3);
                    _ = session.LeaveAsync("Test leave lobby");
                    yield return Until(() => !session.Leaving && !session.manager.IsListening);
                    yield return new WaitForSecondsRealtime(2);
                    Check("Same process reconnect starts", session.StartLoopbackTest(false, ushort.Parse(Arg("-kcPort", "17777"))));
                    yield return Until(() => session.Connected && session.lobby.IsSpawned);
                }
                yield return new WaitForSecondsRealtime(1);
                session.lobby.SetLocalReady(true);
                yield return Until(() => session.lobby.GuestReady.Value);
                session.lobby.SetLocalReady(false);
                yield return Until(() => !session.lobby.GuestReady.Value);
                Check("Client can cancel own ready", !session.lobby.GuestReady.Value);
                session.lobby.SetLocalReady(true);
            }
            yield return Until(() => session.lobby.Phase.Value == KitchenPhase.Countdown);
            Check("Both peers observe countdown", session.lobby.Phase.Value == KitchenPhase.Countdown);
            if (scenario == "countdownlost")
            {
                if (!host) _ = session.LeaveAsync("Test disconnect during countdown");
                yield return Until(() => !session.Connected && !session.Leaving);
                Check("Disconnect cancels countdown and ends session", !session.manager.IsListening);
                Finish(); yield break;
            }
            double observedCountdown = Time.realtimeSinceStartupAsDouble;
            double startsAt = session.lobby.StartsAt.Value;
            while (session.lobby.Phase.Value == KitchenPhase.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            // Client ServerTime is buffered; phase packets can arrive ahead of that estimate.
            // Assert the Host deadline on Host and measure the visible countdown independently.
            double observedDuration = Time.realtimeSinceStartupAsDouble - observedCountdown;
            File.AppendAllText(report, "COUNTDOWN_SECONDS=" + observedDuration.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\n");
            Check("Enter kitchen after countdown", session.lobby.Phase.Value == KitchenPhase.Kitchen &&
                (host ? session.manager.ServerTime.Time >= startsAt : observedDuration >= 2.8 && observedDuration <= 3.5));
            Check("Two network chefs spawned", FindObjectsByType<KitchenNetworkChef>(FindObjectsSortMode.None).Length == 2);
            Check("Own player assigned", session.manager.LocalClient.PlayerObject != null && session.manager.LocalClient.PlayerObject.IsOwner);
            foreach (var chef in FindObjectsByType<KitchenNetworkChef>(FindObjectsSortMode.None))
            {
                bool isHostChef = chef.OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId;
                Check("Chef spawn matches owner " + chef.OwnerClientId, Vector3.Distance(chef.transform.position, (isHostChef ? session.hostSpawn : session.guestSpawn).position) < .01f);
            }
            if (scenario == "hostlost")
            {
                if (host) { yield return new WaitForSecondsRealtime(2); Finish(); yield break; }
                yield return Until(() => !session.Connected && !session.Leaving);
                Check("Host exit returns client to menu without migration", !session.Connected && !session.manager.IsHost);
            }
            else if (!host)
            {
                yield return new WaitForSecondsRealtime(2);
                _ = session.LeaveAsync("Test client leaves kitchen");
                yield return Until(() => !session.Leaving && !session.Connected);
                Check("Client leaves cleanly", !session.manager.IsListening);
            }
            else
            {
                yield return Until(() => !session.Connected && !session.Leaving);
                Check("Client loss ends game, no solo", !session.manager.IsListening);
                Check("Host can start a new room", session.StartLoopbackTest(true, ushort.Parse(Arg("-kcPort", "17777"))));
                yield return Until(() => session.lobby.IsSpawned);
                Check("New room resets lobby and readiness", session.lobby.Phase.Value == KitchenPhase.Lobby && !session.lobby.HostReady.Value && !session.lobby.GuestReady.Value);
            }
            Finish();
        }

        private IEnumerator Until(Func<bool> predicate)
        {
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            if (!predicate()) Check("Timeout waiting for network state", false);
        }
        private void Check(string name, bool pass)
        {
            if (!pass) failures++;
            File.AppendAllText(report, (pass ? "PASS " : "FAIL ") + name + "\n");
        }
        private void Finish()
        {
            File.AppendAllText(report, "COMPLETE failures=" + failures + "\n");
            Application.Quit(failures == 0 ? 0 : 1);
        }
        private void Update()
        {
            if (!string.IsNullOrEmpty(role) && Time.realtimeSinceStartup > deadline + 2)
            { Check("Watchdog expired", false); Finish(); }
        }
#endif
    }
}
