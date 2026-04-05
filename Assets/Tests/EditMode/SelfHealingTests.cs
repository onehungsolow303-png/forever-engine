using NUnit.Framework;
using ForeverEngine.AI.SelfHealing;

namespace ForeverEngine.Tests
{
    public class SelfHealingTests
    {
        [Test] public void FaultBoundary_SuccessResets() { var fb = new FaultBoundary("test"); Assert.IsTrue(fb.TryExecute(() => { })); Assert.AreEqual(0, fb.Failures); }
        [Test] public void FaultBoundary_FailureDisables() { var fb = new FaultBoundary("test", 2); fb.TryExecute(() => throw new System.Exception()); fb.TryExecute(() => throw new System.Exception()); Assert.IsTrue(fb.IsDisabled); }
        [Test] public void FaultBoundary_Recovery() { var fb = new FaultBoundary("test", 2); fb.TryExecute(() => throw new System.Exception()); fb.TryExecute(() => throw new System.Exception()); Assert.IsTrue(fb.IsDisabled); fb.AttemptRecovery(); Assert.IsFalse(fb.IsDisabled); }
        [Test] public void FaultBoundary_DisabledSkips() { var fb = new FaultBoundary("test", 1); fb.TryExecute(() => throw new System.Exception()); bool ran = false; fb.TryExecute(() => ran = true); Assert.IsFalse(ran); }
    }
}
