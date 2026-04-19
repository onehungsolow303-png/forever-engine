using ForeverEngine.Core.World;

namespace ForeverEngine.World.Voxel
{
    public enum LodBand : byte { L0 = 0, L1 = 1, L2 = 2, Hidden = 3 }

    public static class VoxelLodBucketer
    {
        public const int L0Radius = 2;
        public const int L1Radius = 5;
        public const int L2Radius = 10;
        public const float SpeedThreshold = 20f;

        public static LodBand Bucket(ChunkCoord3D center, ChunkCoord3D chunk, float speed)
        {
            int dx = System.Math.Abs(chunk.X - center.X);
            int dz = System.Math.Abs(chunk.Z - center.Z);
            int horiz = System.Math.Max(dx, dz);
            if (horiz > L2Radius) return LodBand.Hidden;
            if (speed >= SpeedThreshold) return LodBand.L2;
            if (horiz <= L0Radius) return LodBand.L0;
            if (horiz <= L1Radius) return LodBand.L1;
            return LodBand.L2;
        }
    }
}
