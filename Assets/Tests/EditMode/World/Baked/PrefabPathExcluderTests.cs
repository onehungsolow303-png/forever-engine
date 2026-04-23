using NUnit.Framework;
using ForeverEngine.Procedural.Editor;

namespace ForeverEngine.Tests.World.Baked
{
    [TestFixture]
    public class PrefabPathExcluderTests
    {
        [TestCase("Assets/Magic Pig Games/Medieval/Dungeon/Chest.prefab", true)]
        [TestCase("Assets/3DForge/Cave Adventure kit/Prefabs/Crystals/fire_crystal4.prefab", true)]
        [TestCase("Assets/Lordenfel/Prefabs/Interior/Furniture/Table.prefab", true)]
        [TestCase("Assets/NAKED_SINGULARITY/DRAGON_CATACOMB/PREFABS/Structure/Arch.prefab", true)]
        [TestCase("Assets/Multistory Dungeons 2/Rooms/Corridor_01.prefab", true)]
        [TestCase("Assets/WaltWW/CaveDungeonToolkit/Tunnel_03.prefab", true)]
        [TestCase("Assets/NatureManufacture Assets/Mountain Environment/Rocks/Prefabs/Big_rock_01.prefab", false)]
        [TestCase("Assets/Eternal Temple/Prefabs/Flora/Tree_01.prefab", false)]
        [TestCase("Assets/Procedural Worlds/Packages - Install/Gaia/Sample/Grass_01.prefab", false)]
        public void ShouldExclude_MatchesIndoorKeywords(string path, bool expected)
        {
            Assert.That(PrefabPathExcluder.ShouldExclude(path), Is.EqualTo(expected));
        }

        [Test]
        public void ShouldExclude_NullOrEmpty_Falsy()
        {
            Assert.That(PrefabPathExcluder.ShouldExclude(null), Is.False);
            Assert.That(PrefabPathExcluder.ShouldExclude(""), Is.False);
        }

        [Test]
        public void ShouldExclude_CaseInsensitive()
        {
            Assert.That(PrefabPathExcluder.ShouldExclude("Assets/pack/DUNGEON/Thing.prefab"), Is.True);
            Assert.That(PrefabPathExcluder.ShouldExclude("Assets/pack/dungeon/Thing.prefab"), Is.True);
            Assert.That(PrefabPathExcluder.ShouldExclude("Assets/pack/Dungeon_variant/Thing.prefab"), Is.True);
        }
    }
}
