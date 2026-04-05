using NUnit.Framework;
using ForeverEngine.MonoBehaviour.Animation;

namespace ForeverEngine.Tests
{
    public class AnimationTests
    {
        [Test] public void NewAnimation_StartsAtFrame0() { var state = new AnimState(new AnimClip("idle", 4, 0.25f, true)); Assert.AreEqual(0, state.CurrentFrame); }
        [Test] public void Advance_ProgressesFrames() { var state = new AnimState(new AnimClip("walk", 4, 0.25f, true)); state.Advance(0.25f); Assert.AreEqual(1, state.CurrentFrame); state.Advance(0.25f); Assert.AreEqual(2, state.CurrentFrame); }
        [Test] public void Loop_WrapsAround() { var state = new AnimState(new AnimClip("walk", 3, 0.1f, true)); state.Advance(0.1f); state.Advance(0.1f); state.Advance(0.1f); Assert.AreEqual(0, state.CurrentFrame); Assert.IsFalse(state.Finished); }
        [Test] public void NoLoop_StopsAtEnd() { var state = new AnimState(new AnimClip("attack", 3, 0.1f, false)); state.Advance(0.1f); state.Advance(0.1f); state.Advance(0.1f); Assert.AreEqual(2, state.CurrentFrame); Assert.IsTrue(state.Finished); }
        [Test] public void SpeedMultiplier_AffectsRate() { var state = new AnimState(new AnimClip("fast", 4, 0.5f, true)) { SpeedMultiplier = 2f }; state.Advance(0.25f); Assert.AreEqual(1, state.CurrentFrame); }
    }
}
