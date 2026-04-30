"""Surgical revert of the over-broad billboard fix.

The earlier `fix_billboard_materials_projectwide.py` matched too many filename
keywords (`_lod3`, `_lod2`, `leaves`, `foliage`, `branch`, etc.), catching
architectural meshes in Hivemind / NM that aren't billboard cards. Those got
flipped to Surface=Transparent + AlphaClip and now render blank-white.

This script reverts that flip on materials whose filename does NOT strictly
suggest a billboard (only filename-contains `billboard` or `_card` keeps the
fix). Detection: identifies the fix fingerprint via combined Surface=1 +
AlphaClip=1 + DstBlend=10 + SrcBlend=5 + ZWrite=0.

Idempotent.
"""
from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(r"C:/Dev/Forever engine/Assets")

# Only KEEP the fix on these strict patterns. Everything else gets reverted.
STRICT_BILLBOARD_KEYWORDS = ("billboard", "_card", "impost")


def looks_like_real_billboard(filename: str) -> bool:
    n = filename.lower()
    return any(kw in n for kw in STRICT_BILLBOARD_KEYWORDS)


def has_my_fix_fingerprint(text: str) -> bool:
    """Detect the EXACT fingerprint left by fix_billboard_materials_projectwide.py.

    All five must be present: Surface=1, AlphaClip=1, DstBlend=10, SrcBlend=5, ZWrite=0.
    Materials that were ALREADY transparent (genuine alpha-blended) likely have only
    a subset of these — keep those alone.
    """
    return (
        re.search(r"- _Surface:\s*1\b", text) is not None
        and re.search(r"- _AlphaClip:\s*1\b", text) is not None
        and re.search(r"- _DstBlend:\s*10\b", text) is not None
        and re.search(r"- _SrcBlend:\s*5\b", text) is not None
        and re.search(r"- _ZWrite:\s*0\b", text) is not None
    )


def revert(text: str) -> str:
    text = re.sub(r"(- _Surface:\s*)1\b",       r"\g<1>0", text, count=1)
    text = re.sub(r"(- _SrcBlend:\s*)5\b",      r"\g<1>1", text, count=1)
    text = re.sub(r"(- _DstBlend:\s*)10\b",     r"\g<1>0", text, count=1)
    text = re.sub(r"(- _DstBlendAlpha:\s*)10\b", r"\g<1>0", text, count=1)
    text = re.sub(r"(- _ZWrite:\s*)0\b",        r"\g<1>1", text, count=1)
    text = re.sub(r"(- _AlphaClip:\s*)1\b",     r"\g<1>0", text, count=1)
    return text


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    reverted = 0
    kept_billboard = 0
    pack_counts: Counter[str] = Counter()
    sample: list[str] = []

    # 2026-04-29 PM revision: revert ALL materials with the fix fingerprint
    # except those explicitly named "billboard" / "_card" / "impost". The broad
    # fix caused too many regressions (Kapok leaves rendering white, etc.).
    for mat in ROOT.rglob("*.mat"):
        try:
            rel = mat.relative_to(ROOT)
        except Exception:
            continue
        try:
            text = mat.read_text(errors="replace")
        except Exception:
            continue
        if not has_my_fix_fingerprint(text):
            continue
        if looks_like_real_billboard(mat.stem):
            kept_billboard += 1
            continue

        new_text = revert(text)
        if new_text == text:
            continue
        if not args.dry_run:
            mat.write_text(new_text, encoding="utf-8")
        reverted += 1
        try:
            rel = mat.relative_to(ROOT)
            pack = rel.parts[0] if rel.parts else "?"
        except Exception:
            pack = "?"
        pack_counts[pack] += 1
        if len(sample) < 5:
            sample.append(str(rel))

    print(f"Reverted: {reverted} materials")
    print(f"Kept (real billboard): {kept_billboard}")
    print()
    print("Reverted by pack:")
    for pack, n in pack_counts.most_common(15):
        print(f"  {n:5d}  {pack}")
    print()
    if sample:
        print("Sample reverted:")
        for s in sample:
            print(f"  {s}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
