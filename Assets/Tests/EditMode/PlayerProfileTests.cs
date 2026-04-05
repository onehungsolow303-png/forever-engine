using NUnit.Framework;
using ForeverEngine.AI.PlayerModeling;

namespace ForeverEngine.Tests
{
    public class PlayerProfileTests
    {
        [Test] public void NewProfile_IsBalanced() { var p = new PlayerProfile(); Assert.AreEqual(0f, p.Playstyle.AggressiveVsCautious); }
        [Test] public void GetPrimaryArchetype_Aggressive() { var p = new PlayerProfile(); p.Playstyle.AggressiveVsCautious = 0.8f; p.Playstyle.MeleeVsRanged = 0.9f; p.Playstyle.CombatPreference = 0.8f; Assert.IsTrue(p.GetPrimaryArchetype().Contains("aggressive")); Assert.IsTrue(p.GetPrimaryArchetype().Contains("melee")); }
        [Test] public void MatchesTag() { var p = new PlayerProfile(); p.ArchetypeTags.Add("fighter"); Assert.IsTrue(p.MatchesTag("fighter")); Assert.IsFalse(p.MatchesTag("explorer")); }
    }
}
