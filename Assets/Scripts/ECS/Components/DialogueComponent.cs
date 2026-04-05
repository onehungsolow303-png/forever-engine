using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    public struct DialogueComponent : IComponentData
    {
        public FixedString64Bytes DialogueTreeId;
    }
}
