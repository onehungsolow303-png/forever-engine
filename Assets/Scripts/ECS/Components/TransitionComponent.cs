using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    public struct TransitionComponent : IComponentData
    {
        public int FromZ;
        public int ToZ;
        public int TransitionType; // 0=stairs_down, 1=stairs_up, 2=ladder, 3=trapdoor, 4=portal
    }
}
