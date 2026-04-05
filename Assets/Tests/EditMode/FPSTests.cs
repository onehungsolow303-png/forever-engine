using NUnit.Framework;
using ForeverEngine.Genres.FPS;

namespace ForeverEngine.Tests
{
    public class FPSTests
    {
        [Test] public void HitDetection_HeadDamage() { Assert.AreEqual(50f, HitDetection.CalculateDamage(25f, HitZone.Head)); }
        [Test] public void HitDetection_BodyDamage() { Assert.AreEqual(25f, HitDetection.CalculateDamage(25f, HitZone.Body)); }
        [Test] public void HitDetection_LimbDamage() { Assert.AreEqual(17.5f, HitDetection.CalculateDamage(25f, HitZone.Limb), 0.01f); }
    }
}
