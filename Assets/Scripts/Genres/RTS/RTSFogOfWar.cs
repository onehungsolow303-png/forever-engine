using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

namespace ForeverEngine.Genres.RTS
{
    public class RTSFogOfWar : MonoBehaviour
    {
        [SerializeField] private int _gridWidth = 128, _gridHeight = 128;
        private NativeArray<byte> _visibility; // 0=unexplored, 1=explored, 2=visible
        private int _teamId;

        public void Initialize(int teamId, int width, int height)
        {
            _teamId = teamId; _gridWidth = width; _gridHeight = height;
            _visibility = new NativeArray<byte>(width * height, Allocator.Persistent);
        }

        public void UpdateVisibility(NativeArray<Vector2> unitPositions, NativeArray<float> sightRanges)
        {
            // Dim all visible to explored
            var dimJob = new DimJob { Grid = _visibility };
            var dimHandle = dimJob.Schedule(_visibility.Length, 256);

            var revealJob = new RevealJob
            {
                Grid = _visibility, Width = _gridWidth, Height = _gridHeight,
                Positions = unitPositions, Ranges = sightRanges
            };
            revealJob.Schedule(unitPositions.Length, 4, dimHandle).Complete();
        }

        public byte GetState(int x, int y) => x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight ? _visibility[y * _gridWidth + x] : (byte)0;
        public bool IsVisible(int x, int y) => GetState(x, y) == 2;
        public bool IsExplored(int x, int y) => GetState(x, y) >= 1;

        private void OnDestroy() { if (_visibility.IsCreated) _visibility.Dispose(); }

        [BurstCompile]
        private struct DimJob : IJobParallelFor
        {
            public NativeArray<byte> Grid;
            public void Execute(int i) { if (Grid[i] == 2) Grid[i] = 1; }
        }

        [BurstCompile]
        private struct RevealJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<byte> Grid;
            [ReadOnly] public NativeArray<Vector2> Positions;
            [ReadOnly] public NativeArray<float> Ranges;
            public int Width, Height;

            public void Execute(int unitIdx)
            {
                var pos = Positions[unitIdx];
                int range = (int)Ranges[unitIdx];
                int cx = (int)pos.x, cy = (int)pos.y;
                for (int dy = -range; dy <= range; dy++)
                    for (int dx = -range; dx <= range; dx++)
                    {
                        if (dx * dx + dy * dy > range * range) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x >= 0 && x < Width && y >= 0 && y < Height)
                            Grid[y * Width + x] = 2;
                    }
            }
        }
    }
}
