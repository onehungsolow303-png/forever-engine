#if UNITY_EDITOR
using System;

namespace ForeverEngine.Procedural.Editor
{
    public static class PrefabPathExcluder
    {
        // Case-insensitive substring match. Kept tight to avoid false positives on
        // outdoor content that happens to share a word (e.g. a surface "cave entrance"
        // prop). If a false positive surfaces, document it and add an explicit override.
        //
        // "Room" is deliberately omitted — many pack folders use Rooms/ or rootRoom/
        // subdirectories without being indoor-only.
        private static readonly string[] IndoorKeywords =
        {
            "Dungeon",
            "Cave",
            "Interior",
            "Catacomb",
            "Tunnel",
            "Corridor",
            "Indoor",
        };

        public static bool ShouldExclude(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            for (int i = 0; i < IndoorKeywords.Length; i++)
            {
                if (assetPath.IndexOf(IndoorKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
#endif
