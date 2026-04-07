using NUnit.Framework;
using ForeverEngine.AI.Learning;

namespace ForeverEngine.Tests
{
    public class LearningTests
    {
        [Test] public void QLearner_ChooseAction_InRange() { var q = new QLearner(4, 3, seed: 42); int a = q.ChooseAction(0); Assert.GreaterOrEqual(a, 0); Assert.Less(a, 3); }
        [Test] public void QLearner_Update_ChangesQValue() { var q = new QLearner(4, 3, 0.5f, 0.9f, 0f, 42); q.Update(0, 1, 1.0f, 1); Assert.Greater(q.GetQValue(0, 1), 0f); }
        [Test] public void QLearner_BestAction_AfterTraining() { var q = new QLearner(2, 2, 0.5f, 0.9f, 0f, 42); for (int i = 0; i < 100; i++) q.Update(0, 1, 1.0f, 0); Assert.AreEqual(1, q.GetBestAction(0)); }
        [Test] public void QLearner_SaveLoadTable_Roundtrips() { var q = new QLearner(4, 3, seed: 42); q.Update(0, 1, 5f, 1); var saved = q.SaveTable(); var q2 = new QLearner(4, 3); q2.LoadTable(saved); Assert.AreEqual(q.GetQValue(0, 1), q2.GetQValue(0, 1), 0.001f); }
        // ReinforcementLearner test removed: the wrapper class was archived in
        // the orphan-resolution sweep (superseded by direct QLearner usage in
        // CombatBrain). The QLearner tests above cover the same algorithmic surface.
    }
}
