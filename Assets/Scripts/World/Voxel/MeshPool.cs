using System.Collections.Generic;
using UnityEngine;

namespace ForeverEngine.World.Voxel
{
    /// <summary>
    /// Tiny FIFO pool of Unity Mesh instances. Per-chunk Mesh allocation is
    /// the dominant cost on the streaming hot path — each new Mesh triggers
    /// a native handle alloc and a GPU upload. Reusing instances lets the
    /// VBO/IBO be re-uploaded into the same handle, which Unity batches more
    /// efficiently and keeps the C# GC quiet.
    /// </summary>
    public sealed class MeshPool
    {
        private readonly Stack<Mesh> _stash = new();
        private readonly int _max;

        public int Count => _stash.Count;

        public MeshPool(int maxSize) { _max = maxSize; }

        public Mesh Rent() => _stash.Count > 0 ? _stash.Pop() : new Mesh();

        public void Return(Mesh mesh)
        {
            if (mesh == null) return;
            if (_stash.Count >= _max)
            {
                if (Application.isPlaying) Object.Destroy(mesh);
                else                       Object.DestroyImmediate(mesh);
                return;
            }
            mesh.Clear();
            _stash.Push(mesh);
        }
    }
}
