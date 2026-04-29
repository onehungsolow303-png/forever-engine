"""Extract a .unitypackage directly into a Unity project.

Falls back path when Unity batchmode -importPackage fails (license handshake
errors in CI/headless environments). The .unitypackage format is gzipped
tar; each asset is a directory whose name is a GUID, containing:
  asset       — the file content
  asset.meta  — Unity import metadata (carries the GUID into the project)
  pathname    — UTF-8 text: destination relative to project root

Unity picks the files up on the next AssetDatabase refresh and processes
them via its normal import pipeline (textures get re-encoded, FBXs get
imported, etc.). The .meta file preserves the GUID so cross-pack refs
keep working.

Usage:
    python extract_unitypackage.py <pkg.unitypackage> <project_root> [--skip <regex>]

Recommended skip regex for Hivemind packs:
    --skip 'HDRP|HDRPDefaultResources|HDR Performant|TutorialInfo'

(TutorialInfo collides with the existing ReadmeEditor.cs in
Hivemind/Medieval&FantasyGigaBundle/TutorialInfo — CS0101 on next compile.
HDRP brings back the render pipeline we removed.)
"""
from __future__ import annotations

import re
import sys
import tarfile
import time
from pathlib import Path


def extract(pkg_path: Path, project_root: Path, skip: re.Pattern | None = None) -> tuple[int, int, int]:
    """Returns (assets_written, metas_written, skipped)."""
    written = metas = skipped = 0
    t0 = time.time()
    with tarfile.open(pkg_path, "r:gz") as tar:
        groups: dict[str, dict[str, tarfile.TarInfo]] = {}
        for member in tar.getmembers():
            if "/" not in member.name:
                continue
            guid, fname = member.name.split("/", 1)
            groups.setdefault(guid, {})[fname] = member

        total = len(groups)
        for i, (guid, files) in enumerate(groups.items(), 1):
            if i % 500 == 0:
                print(f"  ... {i}/{total} entries processed (written={written} skipped={skipped})")
            pn_member = files.get("pathname")
            if pn_member is None:
                skipped += 1
                continue
            pn_bytes = tar.extractfile(pn_member).read()
            pathname = pn_bytes.decode("utf-8", errors="ignore").strip().splitlines()[0].strip()
            if not pathname:
                skipped += 1
                continue
            if skip and skip.search(pathname):
                skipped += 1
                continue
            target = project_root / pathname
            target.parent.mkdir(parents=True, exist_ok=True)

            asset_member = files.get("asset")
            if asset_member is not None and asset_member.isfile():
                with tar.extractfile(asset_member) as src, target.open("wb") as dst:
                    dst.write(src.read())
                written += 1

            meta_member = files.get("asset.meta")
            if meta_member is not None and meta_member.isfile():
                with tar.extractfile(meta_member) as src, (target.with_suffix(target.suffix + ".meta")).open("wb") as dst:
                    dst.write(src.read())
                metas += 1

    elapsed = time.time() - t0
    print(f"  done in {elapsed:.1f}s — assets={written} metas={metas} skipped={skipped}")
    return written, metas, skipped


def main() -> int:
    args = sys.argv[1:]
    skip = None
    if "--skip" in args:
        i = args.index("--skip")
        skip = re.compile(args[i + 1])
        del args[i:i + 2]
    if len(args) != 2:
        print("usage: extract_unitypackage.py <pkg.unitypackage> <project_root> [--skip <regex>]")
        return 2
    pkg = Path(args[0])
    proj = Path(args[1])
    if not pkg.is_file():
        print(f"pkg not found: {pkg}")
        return 1
    if not proj.is_dir():
        print(f"project not found: {proj}")
        return 1
    print(f"extracting {pkg.name} -> {proj}" + (f" (skip={skip.pattern})" if skip else ""))
    extract(pkg, proj, skip)
    return 0


if __name__ == "__main__":
    sys.exit(main())
