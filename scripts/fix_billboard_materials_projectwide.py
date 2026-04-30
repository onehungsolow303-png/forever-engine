"""Project-wide billboard material fixer (Bug #52 generalization).

White-square / white-foliage at distance = billboard materials with
Surface=Opaque + (null OR missing) _BaseMap. URP renders them as plain
white (default fragment color when no texture is bound and shader treats
the surface as opaque).

Fix: detect billboard/leaves/foliage materials with Surface=Opaque, swap
to Surface=Transparent with AlphaClip enabled. URP's transparent path
+ AlphaClip handles tree-billboard alpha properly. Materials with valid
textures stay visible; materials with null textures become invisible.

Idempotent — skips already-Transparent materials.

Usage:
    python fix_billboard_materials_projectwide.py [--dry-run]
"""
from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(r"C:/Dev/Forever engine/Assets")

# Filename keywords that mean "this is meant to be a billboard / foliage card"
BILLBOARD_KEYWORDS = (
    "billboard", "leaves", "leaf", "foliage", "needles",
    "branch", "branches", "fronds", "frond", "canopy",
    "card", "_lod3", "_lod2",
)


def patch_mat(text: str) -> tuple[str, list[str]]:
    """Apply transparent + alphaclip fingerprint. Return (new_text, changes)."""
    changes: list[str] = []
    new = text

    def replace(pattern: str, replacement: str, label: str):
        nonlocal new
        before = new
        new = re.sub(pattern, replacement, new, count=1)
        if new != before:
            changes.append(label)

    replace(r"(- _Surface:\s*)0\b",        r"\g<1>1",  "Surface=Transparent")
    replace(r"(- _SrcBlend:\s*)1\b",       r"\g<1>5",  "SrcBlend=SrcAlpha")
    replace(r"(- _DstBlend:\s*)0\b",       r"\g<1>10", "DstBlend=OneMinusSrcAlpha")
    replace(r"(- _DstBlendAlpha:\s*)0\b",  r"\g<1>10", "DstBlendAlpha=OneMinusSrcAlpha")
    replace(r"(- _ZWrite:\s*)1\b",         r"\g<1>0",  "ZWrite=Off")
    replace(r"(- _AlphaClip:\s*)0\b",      r"\g<1>1",  "AlphaClip=On")

    return new, changes


def looks_like_billboard(filename: str) -> bool:
    n = filename.lower()
    return any(kw in n for kw in BILLBOARD_KEYWORDS)


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    fixed = 0
    skipped_already = 0
    skipped_not_billboard = 0
    pack_counts: Counter[str] = Counter()
    sample: list[str] = []

    for mat in ROOT.rglob("*.mat"):
        if not looks_like_billboard(mat.stem):
            skipped_not_billboard += 1
            continue
        try:
            text = mat.read_text(errors="replace")
        except Exception:
            continue
        # Already-transparent fingerprint?
        if re.search(r"- _Surface:\s*1\b", text):
            skipped_already += 1
            continue
        # Has Surface=0 (opaque)? Only act if so.
        if not re.search(r"- _Surface:\s*0\b", text):
            skipped_not_billboard += 1
            continue

        new_text, changes = patch_mat(text)
        if not changes:
            continue
        if not args.dry_run:
            mat.write_text(new_text, encoding="utf-8")
        fixed += 1

        # Pack name = first folder under Assets/
        try:
            rel = mat.relative_to(ROOT)
            pack = rel.parts[0] if rel.parts else "?"
        except Exception:
            pack = "?"
        pack_counts[pack] += 1

        if len(sample) < 5:
            sample.append(f"{rel}  ({', '.join(changes)})")

    print(f"Fixed: {fixed} billboard materials")
    print(f"Skipped (already transparent): {skipped_already}")
    print(f"Skipped (not billboard / not Surface=0): {skipped_not_billboard}")
    print()
    print("By pack:")
    for pack, n in pack_counts.most_common(15):
        print(f"  {n:5d}  {pack}")
    print()
    if sample:
        print("Sample fixes:")
        for s in sample:
            print(f"  {s}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
