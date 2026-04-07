using NUnit.Framework;
using ForeverEngine.Bridges;

namespace ForeverEngine.Tests.Bridges
{
    public class AssetClientTests
    {
        [Test]
        public void DefaultBaseUrlIsAssetManagerPort()
        {
            var c = new AssetClient();
            Assert.AreEqual("http://127.0.0.1:7801", c.BaseUrl);
        }

        [Test]
        public void TrailingSlashIsTrimmed()
        {
            var c = new AssetClient("http://example.com:9000/");
            Assert.AreEqual("http://example.com:9000", c.BaseUrl);
        }
    }
}
