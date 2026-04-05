using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    public struct QuestComponent : IComponentData
    {
        public FixedString64Bytes ActiveQuestId;
    }
}
