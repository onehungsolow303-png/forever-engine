"""Force URP/Lit shader reassignment on materials whose current shader GUID
doesn't resolve in the project.

Magenta-of-death is caused by materials referencing custom vendor shaders
that aren't in the URP project (e.g., SeedMesh's separate Vegetation Shaders
Asset Store pkg, G-Star's QualitySwamp shader). This script rebinds those
materials to URP/Lit so they at least RENDER (textured/colored) instead of
showing magenta. Visual fidelity is degraded — vendor-specific features
(SSS, custom wind, etc.) lost — but it unblocks asset-variety showcase.

Idempotent: skip materials whose current shader GUID resolves.

Usage:
    python force_urp_lit_for_missing_shaders.py [--dry-run]
"""
from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(r"C:/Dev/Forever engine/Assets")
URP_LIT_GUID = "42dfd86f1908f1347af0e70aaf9971f8"
URP_LIT_FILEID = "-6465566751694194690"

# Pack roots to scan (broken-pack candidates)
PACK_ROOTS = [
    "_SwampBundle",
    "G-Star",
    "SeedMesh",
    "TFP/3_Materials",
    "TFP/2_Prefabs",
]


def collect_resolvable_guids() -> set[str]:
    guids: set[str] = set()
    for meta in ROOT.rglob("*.meta"):
        try:
            for line in meta.read_text(errors="replace").splitlines():
                m = re.match(r"^guid:\s*([a-f0-9]{32})", line)
                if m:
                    guids.add(m.group(1))
                    break
        except Exception:
            pass
    return guids


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    print(f"Scanning {ROOT} for resolvable shader GUIDs...")
    resolvable = collect_resolvable_guids()
    print(f"  {len(resolvable)} resolvable GUIDs in project")

    pattern = re.compile(
        r"(m_Shader:\s*\{fileID:\s*)([-0-9]+)(,\s*guid:\s*)([a-f0-9]{32}|0+)(,\s*type:\s*)(\d+)(\})"
    )

    fixed = 0
    skipped_resolved = 0
    skipped_builtin = 0
    skipped_already_lit = 0
    pack_counts: Counter[str] = Counter()

    for pack in PACK_ROOTS:
        pack_root = ROOT / pack
        if not pack_root.exists():
            continue
        for mat in pack_root.rglob("*.mat"):
            try:
                text = mat.read_text(errors="replace")
            except Exception:
                continue
            m = pattern.search(text)
            if not m:
                continue
            guid = m.group(4)
            if guid == URP_LIT_GUID:
                skipped_already_lit += 1
                continue
            if guid == "0" * 32 or all(c in "0f" for c in guid[:24]):
                # Built-in shader (handled by URP converter elsewhere)
                skipped_builtin += 1
                continue
            if guid in resolvable:
                skipped_resolved += 1
                continue

            # Force URP/Lit
            new_line = f"{m.group(1)}{URP_LIT_FILEID}{m.group(3)}{URP_LIT_GUID}{m.group(5)}3{m.group(7)}"
            new_text = text[: m.start()] + new_line + text[m.end() :]

            if not args.dry_run:
                mat.write_text(new_text, encoding="utf-8")
            fixed += 1
            pack_counts[pack] += 1
            if fixed <= 5:
                rel = mat.relative_to(ROOT)
                print(f"  {'[DRY] ' if args.dry_run else ''}fixed {rel} (was guid={guid[:8]}...)")

    print()
    print(f"Reassigned to URP/Lit: {fixed} materials")
    print(f"Already URP/Lit:       {skipped_already_lit}")
    print(f"Built-in (Standard):   {skipped_builtin} (URP converter handles)")
    print(f"Custom-but-resolved:   {skipped_resolved} (left alone)")
    print()
    print("By pack:")
    for pack, n in pack_counts.most_common():
        print(f"  {n:5d}  {pack}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
