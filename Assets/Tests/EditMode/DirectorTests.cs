using NUnit.Framework;
using ForeverEngine.AI.Director;

namespace ForeverEngine.Tests
{
    public class DirectorTests
    {
        [Test] public void Pacing_DecaysOverTime() { var p = new PacingController(); p.SetIntensity(0.8f); p.Update(5f); Assert.Less(p.CurrentIntensity, 0.8f); }
        [Test] public void Pacing_NeverNegative() { var p = new PacingController(); p.Update(1000f); Assert.GreaterOrEqual(p.CurrentIntensity, 0f); }
        [Test] public void Drama_BuildsOverTime() { var d = new DramaManager(); d.Update(60f); Assert.Greater(d.DramaNeed, 0f); }
        [Test] public void Drama_TriggerReducesNeed() { var d = new DramaManager(); d.Update(60f); float before = d.DramaNeed; d.TriggerDrama(0.5f); Assert.Less(d.DramaNeed, before); }
        [Test] public void DirectorAction_CooldownWorks() { var a = new DirectorAction { Cooldown = 10f, LastUsedTime = 5f }; Assert.IsFalse(a.IsReady(10f)); Assert.IsTrue(a.IsReady(16f)); }
    }
}
