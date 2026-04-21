using System;
using System.IO;
using NUnit.Framework;
using ForeverEngine.Procedural.Editor;

namespace ForeverEngine.Tests.World.Baked
{
    public class AssetPackScannerTests
    {
        [Test]
        public void Scan_ReturnsNonEmpty_ForAssetsRoot()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "pack_scan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                Directory.CreateDirectory(Path.Combine(tmp, "NatureManufacture_BorealForest"));
                Directory.CreateDirectory(Path.Combine(tmp, "AridDesertPack"));
                Directory.CreateDirectory(Path.Combine(tmp, "Plugins"));  // should be skipped

                var packs = AssetPackScanner.ScanRoot(tmp);

                Assert.AreEqual(2, packs.Length, "Plugins dir should be skipped");
                Assert.IsTrue(System.Array.Exists(packs, p => p.Name == "NatureManufacture_BorealForest"));
            }
            finally { Directory.Delete(tmp, recursive: true); }
        }
    }
}
