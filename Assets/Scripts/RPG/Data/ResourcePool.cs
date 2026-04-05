namespace ForeverEngine.RPG.Data
{
    [System.Serializable]
    public struct ResourcePool
    {
        public int Current;
        public int Max;

        public ResourcePool(int max)
        {
            Max = max;
            Current = max;
        }

        public ResourcePool(int current, int max)
        {
            Current = current;
            Max = max;
        }

        public bool IsFull => Current >= Max;
        public bool IsEmpty => Current <= 0;

        public bool Spend(int amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }

        public void Restore(int amount)
        {
            Current += amount;
            if (Current > Max) Current = Max;
        }

        public void RestoreAll()
        {
            Current = Max;
        }

        public override string ToString()
        {
            return $"{Current}/{Max}";
        }
    }
}
