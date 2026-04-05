using NUnit.Framework;
using ForeverEngine.Genres.Strategy;
using ForeverEngine.Genres.Adventure;

namespace ForeverEngine.Tests
{
    public class GenreTests
    {
        [Test] public void HexGrid_Distance() { Assert.AreEqual(0, HexGrid.Distance(0, 0, 0, 0)); Assert.AreEqual(1, HexGrid.Distance(0, 0, 1, 0)); Assert.AreEqual(2, HexGrid.Distance(0, 0, 2, 0)); }
        [Test] public void HexGrid_MovementCost() { Assert.AreEqual(1f, HexGrid.GetMovementCost(TileType.Plains)); Assert.AreEqual(2f, HexGrid.GetMovementCost(TileType.Forest)); Assert.AreEqual(99f, HexGrid.GetMovementCost(TileType.Water)); }
        [Test] public void HexGrid_DefenseBonus() { Assert.AreEqual(0.5f, HexGrid.GetDefenseBonus(TileType.Mountain)); Assert.AreEqual(0f, HexGrid.GetDefenseBonus(TileType.Plains)); }
        [Test] public void PuzzleState_Values() { Assert.AreEqual(0, (int)PuzzleState.Locked); Assert.AreEqual(2, (int)PuzzleState.Solved); }
    }
}
