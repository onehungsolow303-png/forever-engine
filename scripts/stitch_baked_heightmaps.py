#!/usr/bin/env python3
"""
Post-bake heightmap stitcher for Forever Engine's baked planet tiles.

Why this exists
---------------
Gaia's headless bake (GaiaHeadlessPipeline.cs) produces N×N adjacent terrain
tiles whose shared boundary cells DO NOT have matching heightmap values —
each tile's TerrainData was authored independently by Gaia's spawn coroutines
and the bake pipeline (UnityTerrainSampler) reads them as-is. Result: 20-60m
vertical cliffs along world X=0 and Z=0 lines (the tile boundaries), visible
as trenches/canyons running across the world.

This script post-processes the baked tiles in place to fix that:
  1. Heal any sub-1m anomaly cells (8-neighbor average)
  2. Stitch each pair of adjacent tile edges (average shared-line cells)
  3. Force the 4-tile junction cell to a single shared value

Until/unless we find the proper bake-time Gaia setting (gaia skill Bug #34
documents the investigation), run this AFTER GaiaHeadlessPipeline.BuildXxx
completes and BEFORE deploy_props_bin.ps1 pushes to the server.

File format awareness
---------------------
- heightmap.bin       = float32[grid*grid] meters (no header). Typically 16x16.
- heightmap_hires.bin = int32 grid + float32[grid*grid] meters. Typically 513x513.
The int32 header MUST be preserved on write — earlier versions of this fix
broke the format by treating all bytes as floats.

Usage
-----
  python scripts/stitch_baked_heightmaps.py [--layer-dir PATH] [--dry-run]

Hardcoded defaults match Forever Engine's bake at C:/Dev/.shared/baked/planet/layer_0.
Assumes 2x2 tile layout: tiles (0,0), (0,1), (1,0), (1,1).
"""
import argparse
import math
import os
import struct
import sys


def load_hires(path):
    """heightmap_hires.bin = int32 grid + grid*grid floats."""
    with open(path, 'rb') as f:
        data = f.read()
    g = struct.unpack('<i', data[:4])[0]
    flat = list(struct.unpack(f'<{g*g}f', data[4:4 + g*g*4]))
    return [flat[z*g:(z+1)*g] for z in range(g)], g


def save_hires(grid, g, path):
    flat = [grid[z][x] for z in range(g) for x in range(g)]
    with open(path, 'wb') as f:
        f.write(struct.pack('<i', g))
        f.write(struct.pack(f'<{g*g}f', *flat))


def load_lores(path):
    """heightmap.bin = grid*grid floats, no header."""
    n = os.path.getsize(path) // 4
    g = int(math.isqrt(n))
    with open(path, 'rb') as f:
        flat = list(struct.unpack(f'<{n}f', f.read()))
    return [flat[z*g:(z+1)*g] for z in range(g)], g


def save_lores(grid, g, path):
    flat = [grid[z][x] for z in range(g) for x in range(g)]
    with open(path, 'wb') as f:
        f.write(struct.pack(f'<{g*g}f', *flat))


def heal_zeros(grid, g, threshold=1.0):
    """Replace cells with Y < threshold by average of 8 valid neighbors."""
    healed = 0
    for z in range(g):
        for x in range(g):
            if grid[z][x] < threshold:
                vals = []
                for dz in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if dz == 0 and dx == 0:
                            continue
                        nz, nx = z + dz, x + dx
                        if 0 <= nz < g and 0 <= nx < g and grid[nz][nx] >= threshold:
                            vals.append(grid[nz][nx])
                if vals:
                    grid[z][x] = sum(vals) / len(vals)
                    healed += 1
    return healed


def stitch_x_edge(west, east, g):
    """West tile's east edge (x=g-1) shares world X with east tile's west edge (x=0)."""
    for z in range(g):
        avg = (west[z][g-1] + east[z][0]) / 2
        west[z][g-1] = avg
        east[z][0] = avg


def stitch_z_edge(south, north, g):
    """South tile's north edge (z=g-1) shares world Z with north tile's south edge (z=0)."""
    for x in range(g):
        avg = (south[g-1][x] + north[0][x]) / 2
        south[g-1][x] = avg
        north[0][x] = avg


def four_corner_junction(t00, t01, t10, t11, g):
    """Force all 4 cells at the world(0,0) junction to a single shared value."""
    vals = [t00[g-1][g-1], t01[0][g-1], t10[g-1][0], t11[0][0]]
    avg = sum(vals) / 4
    t00[g-1][g-1] = avg
    t01[0][g-1] = avg
    t10[g-1][0] = avg
    t11[0][0] = avg
    return vals, avg


def process_layer(layer_dir, dry_run=False):
    tile_keys = [(0, 0), (0, 1), (1, 0), (1, 1)]
    for fname, loader, saver in [("heightmap_hires.bin", load_hires, save_hires),
                                  ("heightmap.bin",       load_lores, save_lores)]:
        print(f"\n=== {fname} ===")
        tiles = {}
        paths = {}
        g = None
        for t in tile_keys:
            p = f"{layer_dir}/tile_{t[0]}_{t[1]}/macro/{fname}"
            if not os.path.exists(p):
                print(f"  SKIP (missing): {p}")
                return 1
            grid, gg = loader(p)
            tiles[t] = grid
            paths[t] = p
            g = gg
        print(f"  loaded 4 tiles at {g}x{g}")

        total_healed = sum(heal_zeros(grid, g) for grid in tiles.values())
        print(f"  healed {total_healed} sub-1m anomaly cells")

        stitch_x_edge(tiles[(0, 0)], tiles[(1, 0)], g)
        stitch_x_edge(tiles[(0, 1)], tiles[(1, 1)], g)
        stitch_z_edge(tiles[(0, 0)], tiles[(0, 1)], g)
        stitch_z_edge(tiles[(1, 0)], tiles[(1, 1)], g)
        print("  stitched 4 internal tile boundaries")

        vals, avg = four_corner_junction(tiles[(0, 0)], tiles[(0, 1)],
                                         tiles[(1, 0)], tiles[(1, 1)], g)
        print(f"  4-corner junction: {[f'{v:.2f}' for v in vals]} -> {avg:.2f}m")

        if dry_run:
            print("  DRY RUN: would save 4 files")
            continue
        for t in tile_keys:
            saver(tiles[t], g, paths[t])
        print("  saved 4 files")

    print("\n=== Verification ===")
    tiles = {t: load_hires(f"{layer_dir}/tile_{t[0]}_{t[1]}/macro/heightmap_hires.bin")[0]
             for t in tile_keys}
    g = len(tiles[(0, 0)])
    pairs = [
        ("(0,0).E vs (1,0).W", [tiles[(0,0)][z][g-1] for z in range(g)],
                                [tiles[(1,0)][z][0]   for z in range(g)]),
        ("(0,1).E vs (1,1).W", [tiles[(0,1)][z][g-1] for z in range(g)],
                                [tiles[(1,1)][z][0]   for z in range(g)]),
        ("(0,0).N vs (0,1).S", [tiles[(0,0)][g-1][x] for x in range(g)],
                                [tiles[(0,1)][0][x]   for x in range(g)]),
        ("(1,0).N vs (1,1).S", [tiles[(1,0)][g-1][x] for x in range(g)],
                                [tiles[(1,1)][0][x]   for x in range(g)]),
    ]
    for label, a, b in pairs:
        diffs = [abs(a[i] - b[i]) for i in range(g)]
        ok = "OK" if max(diffs) < 0.001 else "MISMATCH"
        print(f"  {label}: max|diff|={max(diffs):.6f}m  {ok}")
    return 0


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--layer-dir", default="C:/Dev/.shared/baked/planet/layer_0",
                   help="Path to baked layer directory containing tile_*/macro/*")
    p.add_argument("--dry-run", action="store_true",
                   help="Compute stitch but don't write files")
    args = p.parse_args()
    if not os.path.isdir(args.layer_dir):
        print(f"ERROR: layer dir not found: {args.layer_dir}", file=sys.stderr)
        return 2
    return process_layer(args.layer_dir, args.dry_run)


if __name__ == "__main__":
    sys.exit(main())
