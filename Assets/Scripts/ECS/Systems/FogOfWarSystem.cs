using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FogOfWarSystem : ISystem
    {
        private int2 _lastPlayerPos;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FogVisionComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var store = MapDataStore.Instance;
            if (store == null || !store.FogGrid.IsCreated) return;

            foreach (var (vision, position) in
                SystemAPI.Query<RefRO<FogVisionComponent>, RefRO<PositionComponent>>())
            {
                var playerPos = new int2(position.ValueRO.X, position.ValueRO.Y);

                if (playerPos.Equals(_lastPlayerPos) && _initialized)
                    continue;

                _lastPlayerPos = playerPos;
                _initialized = true;

                var dimJob = new FogDimJob { FogGrid = store.FogGrid };
                var dimHandle = dimJob.Schedule(store.FogGrid.Length, 256, state.Dependency);

                var rayJob = new FogRaycastJob
                {
                    PlayerX = playerPos.x,
                    PlayerY = playerPos.y,
                    SightRadius = vision.ValueRO.SightRadius,
                    MapWidth = store.Width,
                    MapHeight = store.Height,
                    Walkability = store.Walkability,
                    FogGrid = store.FogGrid
                };
                var rayHandle = rayJob.Schedule(360, 36, dimHandle);

                state.Dependency = rayHandle;
            }
        }
    }

    public struct MapDataSingleton : IComponentData
    {
        public int Width;
        public int Height;
        public int CurrentZ;
    }
}
