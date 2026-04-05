namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Per-level features struct — what a class gains at each level.
    /// </summary>
    [System.Serializable]
    public struct ClassLevelData
    {
        public int Level;
        public string[] FeaturesGained;
        public bool IsASILevel;
        public string SubclassFeature;

        public ClassLevelData(int level, string[] features, bool isASI = false, string subclassFeature = null)
        {
            Level = level;
            FeaturesGained = features ?? System.Array.Empty<string>();
            IsASILevel = isASI;
            SubclassFeature = subclassFeature;
        }
    }
}
