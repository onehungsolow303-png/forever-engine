using UnityEngine;
using ForeverEngine.Network;
using ForeverEngine.Core.Messages;

namespace ForeverEngine.Procedural
{
    /// <summary>
    /// Spec 7 Phase 3 Task 8 debug shortcut: F key in the World scene sends an
    /// EnterDungeonRequest with the debug_small template to the server. Real
    /// world-POI entry (dungeon door colliders, cave entrances) is deferred.
    /// </summary>
    public class DungeonEntryInput : UnityEngine.MonoBehaviour
    {
        public KeyCode EnterKey = KeyCode.F;
        public string TemplateName = "debug_small";

        private void Update()
        {
            if (!Input.GetKeyDown(EnterKey)) return;
            var cm = ConnectionManager.Instance;
            if (cm == null) return;
            cm.Send(new EnterDungeonRequest { TemplateName = TemplateName });
            Debug.Log($"[DungeonEntryInput] Sent EnterDungeonRequest template={TemplateName}");
        }
    }
}
