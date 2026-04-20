using UnityEngine;
using ForeverEngine.Demo;
using ForeverEngine.Network;

namespace ForeverEngine.Demo.Boot
{
    /// <summary>
    /// Client entry point. Always auto-connects to the game server and starts
    /// the game after login. The game is multiplayer-only — there is no offline
    /// mode and no main menu.
    ///
    /// Replaces DemoMainMenu.cs (deleted 2026-04-20 multiplayer-only cleanup).
    /// When real character creation lands, swap the StartGameWithSheet call
    /// for a CharacterCreationUI hand-off.
    /// </summary>
    public class ClientBoot : UnityEngine.MonoBehaviour
    {
        private void Awake()
        {
            // Keep the player loop running when the window loses focus. Multi-client
            // playtests otherwise deadlock: the unfocused client never gets focus,
            // its main-thread queue freezes, and its LoginRequest never fires.
            Application.runInBackground = true;

            // GameManager singleton — constructs bridge clients, watchdog, state server.
            if (GameManager.Instance == null)
            {
                var gmGO = new GameObject("GameManager");
                gmGO.AddComponent<GameManager>();
            }

            // ConnectionManager singleton — owns connect / login / reconnect UI.
            if (ConnectionManager.Instance == null)
            {
                var cmGO = new GameObject("ConnectionManager");
                cmGO.AddComponent<ConnectionManager>();
            }

            StartCoroutine(BootSequence());
        }

        private System.Collections.IEnumerator BootSequence()
        {
            // Wait for login. ConnectionManager shows its own connect/login overlay
            // via ConnectionUI.uxml for the full duration.
            while (ConnectionManager.Instance == null || !ConnectionManager.Instance.IsLoggedIn)
                yield return null;

            Debug.Log("[ClientBoot] Logged in — auto-starting Human Warrior (placeholder).");

            // TODO: Replace with CharacterCreationUI hand-off when real character
            // creation is implemented. This single line is the entire char-creation
            // insertion point.
            GameManager.Instance.StartGameWithSheet(RPGBridge.CreateHumanWarrior());
        }
    }
}
