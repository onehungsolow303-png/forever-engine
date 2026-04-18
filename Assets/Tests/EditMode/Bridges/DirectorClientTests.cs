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

        // RetryCountDefaultsToThree test removed — DirectorClient no longer exposes
        // RetryCount after the Spec 3B thin-client refactor (the bridge was gutted
        // to a stub since the server drives the Director Hub flow now).
    }
}
