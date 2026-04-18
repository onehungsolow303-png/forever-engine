using UnityEngine;
using ForeverEngine.Network;
using ForeverEngine.Core.Messages;

namespace ForeverEngine.Demo.UI
{
    /// <summary>
    /// IMGUI party panel. Toggled by P key. Shows current party members,
    /// pending invite prompt, and invite/leave controls.
    ///
    /// Spawned as DontDestroyOnLoad by WorldBootstrap so it survives scene changes.
    /// Part of Spec 7 Phase 3 Task 6.
    /// </summary>
    public class PartyPanel : UnityEngine.MonoBehaviour
    {
        public static PartyPanel Instance { get; private set; }

        public bool IsOpen { get; private set; }
        private string _inviteInput = "";

        private void Awake() => Instance = this;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
                IsOpen = !IsOpen;
        }

        private void OnGUI()
        {
            var cache = ServerStateCache.Instance;
            if (cache == null) return;

            // Pending-invite prompt is visible even when panel is closed.
            if (cache.PendingInvite is var inv && inv.HasValue)
            {
                var r = new Rect(Screen.width / 2f - 150f, 50f, 300f, 80f);
                GUI.Box(r, "");
                GUILayout.BeginArea(r);
                GUILayout.Label($"Party invite from {inv.Value.fromPlayerId}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Accept"))
                {
                    var cm = ConnectionManager.Instance;
                    cm?.Send(new PartyAcceptRequest { PartyId = inv.Value.partyId });
                    cache.PendingInvite = null;
                }
                if (GUILayout.Button("Decline"))
                    cache.PendingInvite = null;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            if (!IsOpen) return;

            var panelRect = new Rect(10f, 10f, 260f, 260f);
            GUI.Box(panelRect, "Party");
            GUILayout.BeginArea(new Rect(20f, 30f, 240f, 240f));

            if (cache.CurrentParty != null)
            {
                GUILayout.Label($"PartyId: {cache.CurrentParty.PartyId[..8]}\u2026");
                GUILayout.Label($"Leader: {cache.CurrentParty.LeaderId}");
                GUILayout.Label("Members:");
                foreach (var id in cache.CurrentParty.MemberIds)
                    GUILayout.Label($"  \u2022 {id}");
            }
            else
            {
                GUILayout.Label("(Not in a party yet)");
            }

            GUILayout.Space(10f);
            GUILayout.Label("Invite player (enter id):");
            _inviteInput = GUILayout.TextField(_inviteInput, 40);
            if (GUILayout.Button("Send Invite") && !string.IsNullOrWhiteSpace(_inviteInput))
            {
                var cm = ConnectionManager.Instance;
                cm?.Send(new PartyInviteRequest { TargetPlayerId = _inviteInput.Trim() });
                _inviteInput = "";
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("Leave Party"))
            {
                var cm = ConnectionManager.Instance;
                cm?.Send(new PartyLeaveRequest());
            }

            GUILayout.EndArea();
        }
    }
}
