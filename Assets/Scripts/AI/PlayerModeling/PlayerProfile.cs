using System.Collections.Generic;

namespace ForeverEngine.AI.PlayerModeling
{
    [System.Serializable]
    public struct PlaystyleVector
    {
        public float AggressiveVsCautious;
        public float MeleeVsRanged;
        public float StealthVsLoud;
        public float CombatPreference;
        public float ExplorationPreference;
        public float LootPreference;
        public float SocialPreference;
    }

    [System.Serializable]
    public class PlayerProfile
    {
        public PlaystyleVector Playstyle;
        public float ReactionTimeMs = 300f;
        public float AimAccuracy = 0.5f;
        public float ResourceEfficiency = 0.5f;
        public float PuzzleSolvingSpeed = 0.5f;
        public float ExplorationVsRushing = 0.5f;
        public float MenuTimeRatio = 0.1f;
        public float AvgSessionMinutes = 30f;
        public int SessionsPerWeek = 3;
        public bool RageQuitDetected;
        public List<string> ArchetypeTags = new();

        public bool MatchesTag(string tag) => ArchetypeTags.Contains(tag);

        public string GetPrimaryArchetype()
        {
            var parts = new List<string>();
            parts.Add(Playstyle.AggressiveVsCautious > 0.6f ? "aggressive" : Playstyle.AggressiveVsCautious < 0.4f ? "cautious" : "balanced");
            parts.Add(Playstyle.MeleeVsRanged > 0.6f ? "melee" : Playstyle.MeleeVsRanged < 0.4f ? "ranged" : "hybrid");
            if (Playstyle.ExplorationPreference > 0.6f) parts.Add("explorer");
            else if (Playstyle.CombatPreference > 0.6f) parts.Add("fighter");
            else if (Playstyle.LootPreference > 0.6f) parts.Add("collector");
            return string.Join("-", parts);
        }
    }
}
