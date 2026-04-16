// Assets/Scripts/World/ChunkCoord.cs
using System;

namespace ForeverEngine.World
{
    /// <summary>
    /// Immutable chunk coordinate. Chunks are 256×256 meters.
    /// (0,0) is the player spawn chunk.
    /// </summary>
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public const int ChunkSize = 256; // meters

        public readonly int X;
        public readonly int Z;

        public ChunkCoord(int x, int z) { X = x; Z = z; }

        /// <summary>World-space position of the SW corner of this chunk.</summary>
        public UnityEngine.Vector3 WorldOrigin =>
            new UnityEngine.Vector3(X * ChunkSize, 0f, Z * ChunkSize);

        /// <summary>World-space center of this chunk.</summary>
        public UnityEngine.Vector3 WorldCenter =>
            new UnityEngine.Vector3(X * ChunkSize + ChunkSize * 0.5f, 0f, Z * ChunkSize + ChunkSize * 0.5f);

        /// <summary>Get the chunk coordinate that contains a world position.</summary>
        public static ChunkCoord FromWorldPos(UnityEngine.Vector3 pos) =>
            new ChunkCoord(
                UnityEngine.Mathf.FloorToInt(pos.x / ChunkSize),
                UnityEngine.Mathf.FloorToInt(pos.z / ChunkSize));

        /// <summary>Manhattan distance between two chunks.</summary>
        public int DistanceTo(ChunkCoord other) =>
            Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

        /// <summary>Chebyshev (chessboard) distance — used for load radius.</summary>
        public int ChebyshevTo(ChunkCoord other) =>
            Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));

        public bool Equals(ChunkCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoord c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X},{Z})";

        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }
}
