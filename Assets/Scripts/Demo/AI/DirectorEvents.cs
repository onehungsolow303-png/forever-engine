namespace ForeverEngine.Demo.AI
{
    // DirectorEvents is no longer used by the Unity client.
    // All Director Hub communication routes through ForeverEngine.Server.
    // These stubs remain for compile compatibility until Phase 2/3 cleanup.
    public static class DirectorEvents
    {
        public static void Send(
            string playerInput,
            object actorStats = null,
            string targetId = null,
            object targetStats = null)
        {
            // No-op: Director Hub routing removed in Spec 3B.
        }

        public static void SendDialogue(
            string text,
            string npcId,
            System.Action<string> onResponse,
            string locationId = null,
            string[] recentHistory = null)
        {
            // No-op: Director Hub routing removed in Spec 3B.
            onResponse?.Invoke("");
        }

        public static void SendDialogueDecision(
            string text,
            string npcId,
            System.Action<object> onResponse,
            string locationId = null,
            string[] recentHistory = null)
        {
            // No-op: Director Hub routing removed in Spec 3B.
            onResponse?.Invoke(null);
        }
    }
}
