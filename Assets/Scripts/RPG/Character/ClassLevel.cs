namespace ForeverEngine.RPG.Character
{
    /// <summary>
    /// Tracks a single class and its level within a multiclass character.
    /// </summary>
    [System.Serializable]
    public struct ClassLevel
    {
        public ClassData ClassRef;
        public int Level;

        public ClassLevel(ClassData classData, int level)
        {
            ClassRef = classData;
            Level = level;
        }
    }
}
