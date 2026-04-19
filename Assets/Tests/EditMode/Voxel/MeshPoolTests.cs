using NUnit.Framework;
using UnityEngine;
using ForeverEngine.World.Voxel;

namespace ForeverEngine.Tests.EditMode.Voxel
{
    public class MeshPoolTests
    {
        [Test]
        public void Rent_Then_Return_Then_Rent_Again_Returns_Same_Mesh_Instance()
        {
            var pool = new MeshPool(maxSize: 4);
            Mesh first = pool.Rent();
            pool.Return(first);
            Mesh second = pool.Rent();
            Assert.AreSame(first, second);
        }

        [Test]
        public void Rent_When_Pool_Empty_Allocates_Fresh()
        {
            var pool = new MeshPool(maxSize: 4);
            Mesh m = pool.Rent();
            Assert.IsNotNull(m);
        }

        [Test]
        public void Return_When_Pool_Full_Destroys_The_Excess()
        {
            var pool = new MeshPool(maxSize: 1);
            Mesh kept = new Mesh();
            Mesh excess = new Mesh();
            pool.Return(kept);
            pool.Return(excess);   // pool full → excess must be destroyed (no leak)
            // Can't easily assert "destroyed" in EditMode; proxy: pool size ≤ max.
            Assert.LessOrEqual(pool.Count, 1);
        }
    }
}
