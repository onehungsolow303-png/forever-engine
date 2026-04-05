namespace ForeverEngine.ECS.Data
{
    public struct ItemInstance
    {
        public int ItemId;
        public int StackCount;
        public int MaxStack;
        public bool Equipped;
        public bool IsEmpty => ItemId == 0 || StackCount <= 0;
        public static ItemInstance Empty => new ItemInstance();
    }
}
