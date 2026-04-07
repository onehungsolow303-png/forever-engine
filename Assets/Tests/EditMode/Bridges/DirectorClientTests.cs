using NUnit.Framework;
using ForeverEngine.Bridges;

namespace ForeverEngine.Tests.Bridges
{
    public class DirectorClientTests
    {
        [Test]
        public void DefaultBaseUrlIsDirectorHubPort()
        {
            var c = new DirectorClient();
            Assert.AreEqual("http://127.0.0.1:7802", c.BaseUrl);
        }

        [Test]
        public void RetryCountDefaultsToThree()
        {
            var c = new DirectorClient();
            Assert.AreEqual(3, c.RetryCount);
        }
    }
}
