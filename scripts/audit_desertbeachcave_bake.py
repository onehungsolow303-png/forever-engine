"""
Post-bake audit — DesertBeachCave test bake.

Per gaia-architecture skill Section 5 + Section 22 §20 (Bug #33 alignment).
Reads from .shared/baked/test/desert_beach_cave/ and prints PASS/FAIL counts.
Exit 0 if all pass, exit 1 if any fail.
"""
import json
import os
import struct
import statistics
import sys

BAKE = r"C:/Dev/.shared/baked/test/desert_beach_cave/layer_0/tile_0_0/macro"

def section(title): print(f"\n=== {title} ===")
def ok(msg):   print(f"  OK   {msg}"); return 1
def fail(msg): print(f"  FAIL {msg}"); return 0

passed = 0; failed = 0

# Section 5.A: 9-file artifact completeness
section("5.A artifact completeness")
expected = ["heightmap.bin", "heightmap_hires.bin", "splat.bin", "splat_hires.bin",
            "biome.bin", "features.bin", "props.bin", "tree_instances.bin", "metadata.json"]
for f in expected:
    p = os.path.join(BAKE, f)
    if os.path.exists(p): passed += ok(f"{f} present")
    else: failed += 1; fail(f"{f} MISSING")

# Section 5.B: tree_instances.bin not synthetic stub
# The 264-byte sentinel is the failure pattern from gaia skill Bug #1.
# 30-tree test bake produces ~784 bytes; planet-scale produces 100KB+. Either is fine.
section("5.B tree_instances.bin not synthetic stub")
ti = os.path.join(BAKE, "tree_instances.bin")
if os.path.exists(ti):
    sz = os.path.getsize(ti)
    if sz > 500: passed += ok(f"tree_instances.bin = {sz} B (real instances baked)")
    else: failed += 1; fail(f"tree_instances.bin = {sz} B (synthetic stub or 0 trees)")

# Section 5.C: heightmap_hires.bin > 500 KB
section("5.C heightmap_hires.bin > 500 KB")
hm = os.path.join(BAKE, "heightmap_hires.bin")
if os.path.exists(hm):
    sz = os.path.getsize(hm)
    if sz > 500000: passed += ok(f"heightmap_hires.bin = {sz} B")
    else: failed += 1; fail(f"heightmap_hires.bin = {sz} B (low-res?)")

# Section 22 §20: Heightmap vs props alignment (Bug #33 prevention)
section("22.20 heightmap vs props alignment")
def read_varint(f):
    L = 0; s = 0
    while True:
        b = f.read(1)[0]; L |= (b & 0x7F) << s
        if not (b & 0x80): break
        s += 7
    return L
def read_str(f): return f.read(read_varint(f)).decode("utf-8", errors="replace")

props_path = os.path.join(BAKE, "props.bin")
hm_path = os.path.join(BAKE, "heightmap_hires.bin")
meta_path = os.path.join(BAKE, "metadata.json")
if os.path.exists(props_path) and os.path.exists(hm_path):
    # Read world bounds from metadata so normalization matches any tile size
    world_min_x = -512.0; world_min_z = -512.0; world_size = 1024.0
    if os.path.exists(meta_path):
        meta = json.loads(open(meta_path).read())
        world_min_x = float(meta.get("WorldMinX", -512))
        world_min_z = float(meta.get("WorldMinZ", -512))
        world_size = float(meta.get("WorldMaxX", 512)) - world_min_x

    # Read heightmap (int32 grid + grid*grid float32)
    with open(hm_path, "rb") as f:
        grid = struct.unpack("<i", f.read(4))[0]
        heights = struct.unpack(f"<{grid*grid}f", f.read(grid*grid*4))
    def sample(x, z):
        x_norm = (x - world_min_x) / world_size
        z_norm = (z - world_min_z) / world_size
        gx = max(0, min(grid - 1, int(x_norm * (grid - 1))))
        gz = max(0, min(grid - 1, int(z_norm * (grid - 1))))
        return heights[gz * grid + gx]

    deltas = []
    with open(props_path, "rb") as f:
        n = struct.unpack("<i", f.read(4))[0]
        for _ in range(n):
            read_str(f); read_str(f)  # guid, path
            data = f.read(20)
            if len(data) < 20: break
            x, y, z, _rot1, _rot2 = struct.unpack("<fffff", data)
            deltas.append(y - sample(x, z))
    if deltas:
        med = statistics.median([abs(d) for d in deltas])
        if med < 2.0: passed += ok(f"prop alignment median |delta| = {med:.2f}m (< 2m, good)")
        else: failed += 1; fail(f"prop alignment median |delta| = {med:.2f}m (Bug #33 territory)")
    else:
        passed += ok("no props baked (acceptable for sparse test scene)")

# Section 5.D: bake log signatures
section("5.D bake log signatures")
log = "C:/tmp/gaia-bake-desertbeachcave-final.log"
if os.path.exists(log):
    text = open(log, encoding="utf-8", errors="replace").read()
    if "=== DONE in" in text: passed += ok("bake log shows DONE")
    else: failed += 1; fail("bake log missing DONE")
    bad = sum(text.count(s) for s in ["OUTPUTTING STACK TRACE", "Aborting batchmode",
                                      "Tree prefab at index", "Texture Array Generation Failed"])
    if bad == 0: passed += ok("0 bake log error signatures")
    else: failed += 1; fail(f"{bad} bake log error signatures")
else:
    failed += 1; fail(f"bake log {log} missing")

print(f"\n=== SUMMARY: pass={passed} fail={failed} ===")
sys.exit(0 if failed == 0 else 1)
