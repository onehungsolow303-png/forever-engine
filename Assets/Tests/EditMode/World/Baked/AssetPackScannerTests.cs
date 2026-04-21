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

        [Test]
        public void Scan_OrderIsOrdinalSorted_RegardlessOfInsertionOrder()
        {
            // Windows insertion order can differ from lexical order; verify the scanner
            // sorts ordinally so bakes are reproducible across machines.
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "pack_scan_order_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tmp);
            try
            {
                // Create in reverse alpha order to catch any "insertion-order" reliance.
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tmp, "zzz_Last"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tmp, "mmm_Mid"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tmp, "aaa_First"));

                var packs = AssetPackScanner.ScanRoot(tmp);

                Assert.AreEqual(3, packs.Length);
                Assert.AreEqual("aaa_First", packs[0].Name);
                Assert.AreEqual("mmm_Mid",   packs[1].Name);
                Assert.AreEqual("zzz_Last",  packs[2].Name);
            }
            finally { System.IO.Directory.Delete(tmp, recursive: true); }
        }
    }
}
