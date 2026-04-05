using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Data
{
    /// <summary>
    /// Dynamic buffer for walkability data per z-level.
    /// Attached to the map singleton entity.
    /// NativeArray<bool> equivalent stored as ECS buffer for Job System access.
    /// </summary>
    [InternalBufferCapacity(0)] // External storage — maps can be large
    public struct WalkabilityBuffer : IBufferElementData
    {
        public bool Value;
    }

    /// <summary>
    /// Dynamic buffer for elevation data per z-level.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ElevationBuffer : IBufferElementData
    {
        public float Value;
    }

    /// <summary>
    /// Combat log entry stored as ECS buffer for the UI to read.
    /// Replaces pygame game_engine.py log list.
    /// </summary>
    [InternalBufferCapacity(50)]
    public struct CombatLogEntry : IBufferElementData
    {
        public FixedString128Bytes Message;
        public byte ColorR;
        public byte ColorG;
        public byte ColorB;
        public float Timestamp;
    }
}
