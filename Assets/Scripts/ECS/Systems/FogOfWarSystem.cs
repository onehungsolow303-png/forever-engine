using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Jobs;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Fog of war system — rewritten from pygame fog_of_war.py.
    /// Schedules FogDimJob then FogRaycastJob each frame the player moves.
    /// The fog grid is a NativeArray<byte> managed as a singleton resource.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FogOfWarSystem : ISystem
    {
        private NativeArray<byte> _fogGrid;
        private int _mapWidth;
        private int _mapHeight;
        private int2 _lastPlayerPos;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FogVisionComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Find player entity with FogVision
            foreach (var (vision, position) in
                SystemAPI.Query<RefRO<FogVisionComponent>, RefRO<PositionComponent>>())
            {
                var playerPos = new int2(position.ValueRO.X, position.ValueRO.Y);

                // Only re-raycast if player moved
                if (playerPos.Equals(_lastPlayerPos) && _fogGrid.IsCreated)
                    continue;

                _lastPlayerPos = playerPos;

                if (!_fogGrid.IsCreated)
                    break; // Map not loaded yet

                // Step 1: Dim all VISIBLE → EXPLORED
                var dimJob = new FogDimJob { FogGrid = _fogGrid };
                var dimHandle = dimJob.Schedule(_fogGrid.Length, 256, state.Dependency);

                // Step 2: Raycast 360 rays from player
                var rayJob = new FogRaycastJob
                {
                    PlayerX = playerPos.x,
                    PlayerY = playerPos.y,
                    SightRadius = vision.ValueRO.SightRadius,
                    MapWidth = _mapWidth,
                    MapHeight = _mapHeight,
                    Walkability = SystemAPI.GetSingleton<MapDataSingleton>().Walkability,
                    FogGrid = _fogGrid
                };
                var rayHandle = rayJob.Schedule(360, 36, dimHandle); // 360 rays, batch of 36

                state.Dependency = rayHandle;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_fogGrid.IsCreated)
                _fogGrid.Dispose();
        }

        public void InitializeGrid(int width, int height)
        {
            _mapWidth = width;
            _mapHeight = height;
            if (_fogGrid.IsCreated) _fogGrid.Dispose();
            _fogGrid = new NativeArray<byte>(width * height, Allocator.Persistent);
        }

        public NativeArray<byte> GetFogGrid() => _fogGrid;
    }

    /// <summary>
    /// Singleton component holding map data accessible to all ECS systems.
    /// Created by MapImporter when a map is loaded.
    /// </summary>
    public struct MapDataSingleton : IComponentData
    {
        public int Width;
        public int Height;
        public int CurrentZ;
        // NativeArrays stored separately as managed references via SystemAPI
    }
}
