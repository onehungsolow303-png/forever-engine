#!/usr/bin/env python3
"""
Fix gaia Bug #52 (TRUE ROOT CAUSE — corrected 2026-04-29).

The original Bug #52 entry blamed PW Tree recovery billboard materials,
but the actual source of in-game white squares is GAIA PRO WEATHER VFX
particles (Butterfly, Bird, Pollen) spawned per-chunk via the Props pool.
The custom `PWS/VFX/Buterfly` shader saturates particle color × atlas
sample, producing solid white quads regardless of `_TintColor` material
property (which the shader never reads).

This script applies TWO fixes:

PART A — Replace 3 Gaia Pro Weather VFX particle materials with invisible
URP/Particles/Unlit (Surface=Transparent + alpha=0). This is the actual
white-square fix:
  - Butterfly.mat
  - Bird.mat
  - Pollen.mat

PART B — Apply the URP transparent surface mode pattern to 6 secondary
atmospheric/particle materials that share the same opaque-white fingerprint
(all use URP/Particles/Unlit + Surface=Opaque + null TexEnvs):
  - Clouds Particles (Gaia Pro Weather VFX) — sky cloud billboards
  - PW_VFX_ProceduralSky (Procedural Worlds Sky) — atmospheric sky particles
  - M_Fire_Pit / M_Fire_Stones / M_Fire_Torch (NM Fire and Smoke Particles)
  - VFXFogCurtain 1 (WaltWW Cave Dungeon Toolkit)

Idempotent — detects already-fixed mats and no-ops. Run after every Gaia /
NM / WaltWW pack reimport (which overwrites these vendor-shipped mats).

Usage:
    python "C:/Dev/Forever engine/scripts/fix_white_square_particles.py" [--dry-run]
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

PROJECT_ROOT = Path("C:/Dev/Forever engine/Assets")

URP_PARTICLES_UNLIT_GUID = "0406db5a14f94604a8c57ccfbc9f3b46"

# PART A — true root cause. Particle systems spawned per-chunk by Gaia Pro
# Weather VFX. Custom `PWS/VFX/Buterfly` shader saturates particle color
# regardless of material `_TintColor`. Replaced wholesale with invisible
# URP/Particles/Unlit.
INVISIBLE_PARTICLE_MATS = [
    ("Procedural Worlds/Packages - Install/Gaia/Gaia Pro/Weather/VFX/Materials/Butterfly.mat", "Butterfly"),
    ("Procedural Worlds/Packages - Install/Gaia/Gaia Pro/Weather/VFX/Materials/Bird.mat", "Bird"),
    ("Procedural Worlds/Packages - Install/Gaia/Gaia Pro/Weather/VFX/Materials/Pollen.mat", "Pollen"),
]

INVISIBLE_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _SURFACE_TYPE_TRANSPARENT
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap:
    RenderType: Transparent
  disabledShaderPasses:
  - MOTIONVECTORS
  - DepthOnly
  - ShadowCaster
  - DepthNormals
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _Blend: 0
    - _BlendOp: 0
    - _Cull: 0
    - _DstBlend: 10
    - _DstBlendAlpha: 10
    - _Mode: 0
    - _Surface: 1
    - _SrcBlend: 5
    - _ZWrite: 0
    - _GlossyReflections: 0
    - _SpecularHighlights: 0
    m_Colors:
    - _BaseColor: {{r: 0, g: 0, b: 0, a: 0}}
    - _Color: {{r: 0, g: 0, b: 0, a: 0}}
    - _EmissionColor: {{r: 0, g: 0, b: 0, a: 0}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

# PART B — secondary atmospheric/particle materials with the same opaque-white
# fingerprint. These use URP/Particles/Unlit but with Surface=Opaque + null
# TexEnvs. Patch in-place (preserve textures + colors, just fix surface mode).
TARGET_MATS = [
    "NatureManufacture Assets/Fire and Smoke Particles/Models/M_Fire_Pit.mat",
    "NatureManufacture Assets/Fire and Smoke Particles/Models/M_Fire_Stones.mat",
    "NatureManufacture Assets/Fire and Smoke Particles/Models/M_Fire_Torch.mat",
    "Procedural Worlds/Packages - Install/Gaia/Gaia Pro/Weather/VFX/Materials/Clouds Particles.mat",
    "Procedural Worlds/Packages - Install/Procedural Worlds Sky/Content Resources/Materials/PW_VFX_ProceduralSky.mat",
    "WaltWW/CaveDungeonToolkit/SourceFiles/Materials/VFXFogCurtain 1.mat",
]

# Of the secondary mats, these two are pure-white atmospheric particles that
# need _BaseColor.a=0 in addition to the surface-mode patch (otherwise they
# render as 40%/100% white quads even with transparent surface mode).
ALPHA_ZERO_MATS = [
    "Procedural Worlds/Packages - Install/Gaia/Gaia Pro/Weather/VFX/Materials/Clouds Particles.mat",
    "Procedural Worlds/Packages - Install/Procedural Worlds Sky/Content Resources/Materials/PW_VFX_ProceduralSky.mat",
]


def patch_material(text: str) -> tuple[str, list[str]]:
    """Apply the Bug #52 transparent-surface pattern. Returns (new_text, changes)."""
    changes: list[str] = []
    out = text

    # Floats — substitute or note no-op
    float_targets = {
        "_Surface": "1",
        "_SrcBlend": "5",
        "_DstBlend": "10",
        "_DstBlendAlpha": "10",
        "_ZWrite": "0",
        "_Blend": "0",
    }
    for key, target_value in float_targets.items():
        # Match `- _Surface: 0` style entries inside m_Floats
        pattern = re.compile(rf"(- {re.escape(key)}: )([\-0-9.eE]+)")
        m = pattern.search(out)
        if m:
            current = m.group(2)
            if current != target_value:
                out = pattern.sub(rf"\g<1>{target_value}", out, count=1)
                changes.append(f"{key}: {current} -> {target_value}")

    # m_CustomRenderQueue: -1 -> 3000 (only if currently -1, don't override custom)
    out, n = re.subn(r"(m_CustomRenderQueue: )(-1)", r"\g<1>3000", out, count=1)
    if n:
        changes.append("m_CustomRenderQueue: -1 -> 3000")

    # stringTagMap RenderType: anything -> Transparent
    out_new, n = re.subn(
        r"(stringTagMap:\s*\n\s+RenderType: )([^\n]+)",
        lambda m_: m_.group(1) + "Transparent",
        out,
        count=1,
    )
    if n and "RenderType: Transparent" not in text:
        changes.append("RenderType -> Transparent")
        out = out_new
    elif "RenderType:" not in out:
        # No stringTagMap section — insert a minimal one before m_LightmapFlags
        if "m_LightmapFlags:" in out:
            out = out.replace(
                "m_LightmapFlags:",
                "stringTagMap:\n    RenderType: Transparent\n  m_LightmapFlags:",
                1,
            )
            changes.append("RenderType -> Transparent (inserted)")

    # m_ValidKeywords: add _SURFACE_TYPE_TRANSPARENT if not present
    if "_SURFACE_TYPE_TRANSPARENT" not in out:
        # Find m_ValidKeywords: ... and add the keyword
        # Two cases: (a) m_ValidKeywords: [], (b) m_ValidKeywords: with entries
        if re.search(r"m_ValidKeywords: \[\]", out):
            out = re.sub(
                r"m_ValidKeywords: \[\]",
                "m_ValidKeywords:\n  - _SURFACE_TYPE_TRANSPARENT",
                out,
                count=1,
            )
            changes.append("m_ValidKeywords: [] -> [_SURFACE_TYPE_TRANSPARENT]")
        else:
            out, n = re.subn(
                r"(m_ValidKeywords:)(\n)",
                r"\1\n  - _SURFACE_TYPE_TRANSPARENT\2",
                out,
                count=1,
            )
            if n:
                changes.append("m_ValidKeywords: + _SURFACE_TYPE_TRANSPARENT")

    # disabledShaderPasses: add MOTIONVECTORS / DepthOnly / ShadowCaster / DepthNormals
    desired_passes = ["MOTIONVECTORS", "DepthOnly", "ShadowCaster", "DepthNormals"]
    missing = [p for p in desired_passes if p not in out]
    if missing:
        if re.search(r"disabledShaderPasses: \[\]", out):
            block = "disabledShaderPasses:\n" + "\n".join(f"  - {p}" for p in desired_passes)
            out = re.sub(r"disabledShaderPasses: \[\]", block, out, count=1)
            changes.append(f"disabledShaderPasses: [] -> {desired_passes}")
        else:
            # Insert missing passes
            for p in missing:
                out, n = re.subn(
                    r"(disabledShaderPasses:)(\n)",
                    rf"\1\n  - {p}\2",
                    out,
                    count=1,
                )
                if n:
                    changes.append(f"disabledShaderPasses: + {p}")

    return out, changes


def is_already_invisible(text: str) -> bool:
    """Check if material is already in invisible-particle state (Part A)."""
    return (
        URP_PARTICLES_UNLIT_GUID in text
        and "_Surface: 1" in text
        and "RenderType: Transparent" in text
        and re.search(r"_BaseColor: \{r: 0, g: 0, b: 0, a: 0\}", text) is not None
    )


def force_alpha_zero(text: str) -> tuple[str, bool]:
    """Set _BaseColor alpha to 0. Returns (new_text, changed)."""
    new_text = re.sub(
        r"(- _BaseColor: \{r: [0-9.eE\-]+, g: [0-9.eE\-]+, b: [0-9.eE\-]+, a: )[0-9.eE\-]+(\})",
        r"\g<1>0\g<2>",
        text,
        count=1,
    )
    return new_text, new_text != text


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    print("=== PART A: Rewrite Butterfly/Bird/Pollen as invisible URP/Particles/Unlit ===")
    part_a_changes = 0
    for rel, name in INVISIBLE_PARTICLE_MATS:
        path = PROJECT_ROOT / rel
        if not path.exists():
            print(f"  SKIP missing: {rel}")
            continue
        text = path.read_text(encoding="utf-8")
        if is_already_invisible(text):
            print(f"  CLEAN  {name}.mat (already invisible)")
            continue
        print(f"  REWRITE  {name}.mat -> URP/Particles/Unlit + Surface=Transparent + alpha=0")
        part_a_changes += 1
        if not args.dry_run:
            path.write_text(
                INVISIBLE_TEMPLATE.format(name=name, shader_guid=URP_PARTICLES_UNLIT_GUID),
                encoding="utf-8",
            )

    print()
    print("=== PART B: Patch atmospheric/particle mats to URP transparent surface mode ===")
    part_b_changes = 0
    for rel in TARGET_MATS:
        path = PROJECT_ROOT / rel
        if not path.exists():
            print(f"  SKIP missing: {rel}")
            continue

        text = path.read_text(encoding="utf-8")
        new_text, changes = patch_material(text)

        # Force alpha=0 on the two pure-white atmospheric mats
        if rel in ALPHA_ZERO_MATS:
            new_text, alpha_changed = force_alpha_zero(new_text)
            if alpha_changed:
                changes.append("_BaseColor.a -> 0 (force invisible)")

        if not changes:
            print(f"  CLEAN  {path.name}  (already fixed)")
            continue

        print(f"  PATCH  {path.name}")
        for c in changes:
            print(f"      - {c}")
        part_b_changes += len(changes)

        if not args.dry_run:
            path.write_text(new_text, encoding="utf-8")

    print()
    if args.dry_run:
        print(f"DRY RUN: Part A would rewrite {part_a_changes} materials; Part B would apply {part_b_changes} edits.")
    else:
        print(f"Part A: rewrote {part_a_changes} materials. Part B: applied {part_b_changes} edits across {len(TARGET_MATS)} materials.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
