using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForeverEngine.AI.SelfHealing;

namespace ForeverEngine.Tests
{
    public class SelfHealingTests
    {
        [Test] public void FaultBoundary_SuccessResets() { var fb = new FaultBoundary("test"); Assert.IsTrue(fb.TryExecute(() => { })); Assert.AreEqual(0, fb.Failures); }

        [Test] public void FaultBoundary_FailureDisables()
        {
            var fb = new FaultBoundary("test", 2);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*test failed.*"));
            fb.TryExecute(() => throw new System.Exception());
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*test failed.*"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*test disabled.*"));
            fb.TryExecute(() => throw new System.Exception());
            Assert.IsTrue(fb.IsDisabled);
        }

        [Test] public void FaultBoundary_Recovery()
        {
            var fb = new FaultBoundary("test", 2);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*test failed.*"));
            fb.TryExecute(() => throw new System.Exception());
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*test failed.*"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*test disabled.*"));
            fb.TryExecute(() => throw new System.Exception());
            Assert.IsTrue(fb.IsDisabled);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(".*Recovering.*"));
            fb.AttemptRecovery();
            Assert.IsFalse(fb.IsDisabled);
        }

        [Test] public void FaultBoundary_DisabledSkips()
        {
            var fb = new FaultBoundary("test", 1);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*test failed.*"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*test disabled.*"));
            fb.TryExecute(() => throw new System.Exception());
            bool ran = false;
            fb.TryExecute(() => ran = true);
            Assert.IsFalse(ran);
        }
    }
}
