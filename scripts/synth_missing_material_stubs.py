"""Synthesize URP/Lit stub .mat files for every material GUID referenced by
prefabs that doesn't resolve in the project.

Same family as gaia-architecture Bug #1 Stage 1 (which only patched 15 known
PW Pine/Spruce GUIDs). This is the project-wide generalization: scan every
.prefab and FBX-meta external-objects map, collect all distinct material
GUIDs referenced, diff against on-disk .mat.meta GUIDs, and synthesize a
URP/Lit stub for each missing one. Output goes to
`Assets/_RecoveryMaterials/Synth/`.

Idempotent — skips GUIDs already covered by an existing stub.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(r"C:/Dev/Forever engine/Assets")
STUB_DIR = ROOT / "_RecoveryMaterials" / "Synth"
URP_LIT_GUID = "42dfd86f1908f1347af0e70aaf9971f8"
URP_LIT_FILEID = "-6465566751694194690"

MAT_REF_PATTERN = re.compile(r"\{fileID:\s*2100000,\s*guid:\s*([a-f0-9]{32}),\s*type:\s*2\}")


def collect_referenced_material_guids() -> set[str]:
    """Materials in Unity are referenced as {fileID: 2100000, guid: ..., type: 2}."""
    refs: set[str] = set()
    for ext in ("*.prefab", "*.unity", "*.fbx.meta"):
        for f in ROOT.rglob(ext):
            try:
                text = f.read_text(errors="replace")
            except Exception:
                continue
            for g in MAT_REF_PATTERN.findall(text):
                refs.add(g)
    return refs


def collect_material_guids_on_disk() -> set[str]:
    """Materials are .mat with a .mat.meta. Return set of GUIDs from .mat.meta files."""
    on_disk: set[str] = set()
    for meta in ROOT.rglob("*.mat.meta"):
        try:
            for line in meta.read_text(errors="replace").splitlines():
                m = re.match(r"^guid:\s*([a-f0-9]{32})", line)
                if m:
                    on_disk.add(m.group(1))
                    break
        except Exception:
            pass
    return on_disk


STUB_MAT_BODY = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: SynthStub_{guid_short}
  m_Shader: {{fileID: {fid}, guid: {urp_guid}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors: []
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

STUB_META_BODY = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def main() -> int:
    print(f"Scanning {ROOT} ...")
    referenced = collect_referenced_material_guids()
    on_disk = collect_material_guids_on_disk()
    missing = referenced - on_disk
    print(f"  {len(referenced)} material GUIDs referenced by prefabs/scenes/FBXs")
    print(f"  {len(on_disk)} material GUIDs present on disk")
    print(f"  {len(missing)} missing -> need synth stubs")

    if not missing:
        print("Nothing to synthesize.")
        return 0

    STUB_DIR.mkdir(parents=True, exist_ok=True)
    written = 0
    for guid in sorted(missing):
        short = guid[:8]
        mat_path = STUB_DIR / f"SynthStub_{short}.mat"
        meta_path = mat_path.with_suffix(".mat.meta")
        # Don't overwrite existing
        if mat_path.exists():
            continue
        mat_path.write_text(STUB_MAT_BODY.format(guid_short=short, fid=URP_LIT_FILEID, urp_guid=URP_LIT_GUID))
        meta_path.write_text(STUB_META_BODY.format(guid=guid))
        written += 1
        if written <= 5:
            print(f"  wrote {mat_path.name} (guid={guid})")

    print(f"\nSynthesized {written} stubs at {STUB_DIR}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
