using NUnit.Framework;
using ForeverEngine.Genres.Strategy;

namespace ForeverEngine.Tests
{
    public class GenreTests
    {
        // The PuzzleState_Values test was removed in the 2026-04-07 dead code
        // sweep that deleted Genres/Adventure/. The remaining tests cover
        // Strategy/HexGrid which is still load-bearing for the Overworld
        // system. See docs/dead-code-audit-2026-04-07.md.
        [Test] public void HexGrid_Distance() { Assert.AreEqual(0, HexGrid.Distance(0, 0, 0, 0)); Assert.AreEqual(1, HexGrid.Distance(0, 0, 1, 0)); Assert.AreEqual(2, HexGrid.Distance(0, 0, 2, 0)); }
        [Test] public void HexGrid_MovementCost() { Assert.AreEqual(1f, HexGrid.GetMovementCost(TileType.Plains)); Assert.AreEqual(2f, HexGrid.GetMovementCost(TileType.Forest)); Assert.AreEqual(99f, HexGrid.GetMovementCost(TileType.Water)); }
        [Test] public void HexGrid_DefenseBonus() { Assert.AreEqual(0.5f, HexGrid.GetDefenseBonus(TileType.Mountain)); Assert.AreEqual(0f, HexGrid.GetDefenseBonus(TileType.Plains)); }
    }
}
