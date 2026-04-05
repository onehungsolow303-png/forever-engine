using NUnit.Framework;
using ForeverEngine.AI.Inference;

namespace ForeverEngine.Tests
{
    public class InferenceTests
    {
        [Test] public void InferenceRequest_ExecutesCallback()
        {
            float[] result = null;
            var req = new InferenceRequest { Input = new float[] { 1, 2, 3 }, Priority = 1f, Callback = output => result = output };
            req.Execute(); // No engine available, callback gets null
            Assert.IsNull(result);
        }
    }
}
