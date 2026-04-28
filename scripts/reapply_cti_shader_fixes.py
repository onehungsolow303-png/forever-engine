#!/usr/bin/env python3
"""
Reapply Bug #44 CTI shader fixes after a TFP/Forst reimport.

Bug #44 (gaia-architecture skill): Unity 6 + URP auto-injects
LOD_FADE_CROSSFADE via the surface shader's `dithercrossfade` token AND
via `multi_compile_shadowcaster` in any explicit ShadowCaster Pass. CTI
shaders also hand-author `#pragma multi_compile_fragment __
LOD_FADE_CROSSFADE` in their ShadowCaster Pass, so the keyword ends up
declared 2-3 times → "Keyword 'LOD_FADE_CROSSFADE' is duplicated in
several directives" compile error.

Every TFP reimport overwrites these patches (the `.unitypackage` ships
the broken sources). Run this script after every TFP/Forst reimport.

Idempotent: detects already-fixed lines and leaves them alone. Safe to
run on a clean install (no-op).

Usage:
    python C:/Dev/Forever\\ engine/scripts/reapply_cti_shader_fixes.py [--dry-run]

Exit code 0 always (so CI / build hooks don't fail on no-op).
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(r"C:/Dev/Forever engine")

# Bug #44 fix targets. Each entry: (path relative to project root, list of
# edit specs). Each edit spec is (description, search_regex, replace_fn).
# Idempotency check: if `already_fixed_marker` is in the file, skip.

REWRITE_NOTE = "// dithercrossfade removed (Bug #44 reapply)"


def patch_surface_pragma(line: str) -> tuple[str, bool]:
    """Strip ` dithercrossfade` from `#pragma surface ... dithercrossfade ...`.

    Returns (new_line, changed). Idempotent — second call no-ops.
    """
    m = re.match(r"^(\s*)#pragma\s+surface\s+(.+)$", line)
    if not m:
        return line, False
    indent, rest = m.group(1), m.group(2)
    if "dithercrossfade" not in rest:
        return line, False
    new_rest = re.sub(r"\s*\bdithercrossfade\b", "", rest)
    return f"{indent}#pragma surface {new_rest}\t{REWRITE_NOTE}\n", True


def comment_explicit_lod_fade(line: str) -> tuple[str, bool]:
    """Comment out an uncommented `#pragma multi_compile_fragment __ LOD_FADE_CROSSFADE`."""
    if "LOD_FADE_CROSSFADE" not in line:
        return line, False
    if "multi_compile_fragment" not in line:
        return line, False
    stripped = line.lstrip()
    if stripped.startswith("//"):
        return line, False  # already commented
    indent = line[: len(line) - len(stripped)]
    return f"{indent}// {stripped.rstrip()}\t{REWRITE_NOTE}\n", True


def comment_explicit_multi_compile_lod(line: str) -> tuple[str, bool]:
    """Comment out the older `#pragma multi_compile LOD_FADE_PERCENTAGE LOD_FADE_CROSSFADE`."""
    if "LOD_FADE_CROSSFADE" not in line or "LOD_FADE_PERCENTAGE" not in line:
        return line, False
    stripped = line.lstrip()
    if stripped.startswith("//"):
        return line, False
    indent = line[: len(line) - len(stripped)]
    return f"{indent}// {stripped.rstrip()}\t{REWRITE_NOTE}\n", True


# Per-file patch chain. Each chain is a list of per-line patchers; for each
# line, every patcher gets a chance and the first match wins.
FILE_PATCHERS = [
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_LOD_Bark.shader",
        [patch_surface_pragma, comment_explicit_lod_fade, comment_explicit_multi_compile_lod],
    ),
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_LOD_Leaves.shader",
        [patch_surface_pragma, comment_explicit_lod_fade, comment_explicit_multi_compile_lod],
    ),
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_Debug.shader",
        [patch_surface_pragma],  # Debug has no explicit shadowcaster pass
    ),
    # Pre-emptive patches for variants that haven't fired yet (per Bug #44):
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_LOD_Bark_Array.shader",
        [patch_surface_pragma, comment_explicit_lod_fade, comment_explicit_multi_compile_lod],
    ),
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_LOD_Bark_Tess_DX11.shader",
        [patch_surface_pragma, comment_explicit_lod_fade, comment_explicit_multi_compile_lod],
    ),
    (
        "Assets/TFP/0_Extra/CTI Runtime Components/Shaders/CTI_LOD_Billboard.shader",
        [patch_surface_pragma, comment_explicit_lod_fade, comment_explicit_multi_compile_lod],
    ),
]


def patch_file(path: Path, patchers, dry_run: bool) -> tuple[int, int]:
    """Returns (lines_changed, lines_total)."""
    if not path.exists():
        print(f"  SKIP (missing): {path.relative_to(PROJECT_ROOT)}")
        return 0, 0
    text = path.read_text(encoding="utf-8", errors="replace").splitlines(keepends=True)
    out = []
    changes = 0
    for line in text:
        new_line, changed = line, False
        for fn in patchers:
            new_line, changed = fn(line)
            if changed:
                break
        if changed:
            changes += 1
        out.append(new_line)
    if changes and not dry_run:
        path.write_text("".join(out), encoding="utf-8", newline="")
    flag = "[dry-run] " if dry_run else ""
    rel = path.relative_to(PROJECT_ROOT)
    if changes:
        print(f"  {flag}PATCHED {changes} line(s): {rel}")
    else:
        print(f"  CLEAN: {rel}")
    return changes, len(text)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dry-run", action="store_true", help="report changes without writing")
    args = ap.parse_args()

    if not PROJECT_ROOT.exists():
        print(f"ERROR: project root not found: {PROJECT_ROOT}", file=sys.stderr)
        return 0  # don't fail downstream consumers

    print(f"Reapplying Bug #44 CTI shader fixes (root={PROJECT_ROOT}; dry-run={args.dry_run})")
    total_changes = 0
    for rel_path, patchers in FILE_PATCHERS:
        changes, _ = patch_file(PROJECT_ROOT / rel_path, patchers, args.dry_run)
        total_changes += changes
    print(f"\nTotal lines patched: {total_changes}")
    print("(0 = bake is clean; >0 = reimport had reverted the fix and it's now restored)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
