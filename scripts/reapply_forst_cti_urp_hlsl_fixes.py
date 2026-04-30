"""Reapply manual fixes inside vendor-gitignored asset packs.

These packs (Forst, Procedural Worlds) are excluded from this repo via
.gitignore — Asset Store reimport overwrites them. The session that landed
these fixes patched files inside those gitignored folders, so the edits
will be lost on any fresh clone or vendor reimport.

Idempotent — detects already-fixed state and exits 0 with no-op.

Run after every reimport of:
  * Forst Conifers BOTD URP / CTI Runtime Components URP 14plus
  * Procedural Worlds Gaia Pro Assets and Biomes (specifically the recovery
    materials at _RecoveryMaterials/Trees/)

Fixes applied:

1. Forst CTI URP SG BillboardVertex.hlsl + BillboardVertexSimple.hlsl
   Add `_WindStrength` and `_WindPower` declarations next to existing
   `_CTI_SRP_Wind` / `_CTI_SRP_Turbulence` so the billboard vertex shader
   can resolve them. Without these, Unity 6.4 / URP 17 fails to compile
   `pow(saturate(percent.y), _WindPower)` at line 98:
       Shader error: 'pow': no matching 2 parameter intrinsic function
       Shader error: undeclared identifier '_WindPower'

2. Forst CTI URP SG Wind.hlsl — 10-arg overload of CTI_AnimateVertexSG_float
   Shadergraph nodes generate a 10-arg call (legacy CTI signature) but
   the only definition in Wind.hlsl is the 15-arg version. Adds an
   overload that delegates to the 15-arg with sensible defaults. Without
   this, every Forst conifer + every TFP material that references the
   shadergraph fails:
       Shader error: 'CTI_AnimateVertexSG_float': no matching 10 parameter function

3. PW_Tree_Endcap_Universal.mat (in Procedural Worlds/_RecoveryMaterials/Trees/)
   This single shared material is referenced by every PW Tree's LOD0
   "Endcap" geometry across all PW Pine/Spruce/Sequoia families. Originally
   referenced a missing shader GUID (933532a4...) and rendered as a solid
   white quad at every tree's base, making the whole world look broken.
   Force to URP/Lit shader + transparent surface + alpha=0 so the endcap
   geometry stays invisible. Trees still render their main mesh normally.

Discovered via WhiteQuadDiagnostic.cs runtime-style introspection
2026-04-29 PM. See also gaia-architecture skill Bug #1 (PW Tree material
cascade) and Bug #52 (white-square fingerprint).
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(r"C:/Dev/Forever engine")

# --- Forst HLSL targets ---
FORST_BILLBOARD_VERTEX = PROJECT_ROOT / "Assets/Forst/CTI Runtime Components/CTI Runtime Components URP 14plus/Shaders/Includes/CTI URP SG BillboardVertex.hlsl"
FORST_BILLBOARD_SIMPLE = PROJECT_ROOT / "Assets/Forst/CTI Runtime Components/CTI Runtime Components URP 14plus/Shaders/Includes/CTI URP SG BillboardVertexSimple.hlsl"
FORST_WIND_HLSL = PROJECT_ROOT / "Assets/Forst/CTI Runtime Components/CTI Runtime Components URP 14plus/Shaders/Includes/CTI URP SG Wind.hlsl"

# --- PW Tree Endcap material ---
PW_TREE_ENDCAP_MAT = PROJECT_ROOT / "Assets/Procedural Worlds/_RecoveryMaterials/Trees/PW_Tree_Endcap_Universal.mat"

URP_LIT_GUID = "42dfd86f1908f1347af0e70aaf9971f8"
URP_LIT_FILEID = "-6465566751694194690"

WIND_DECL_INSERT = """\
\tfloat4 _CTI_SRP_Wind;
\tfloat _CTI_SRP_Turbulence;

// 2026-04-29: declarations added to fix Unity 6.4 / URP 17 compile errors.
\tfloat _WindStrength;
\tfloat _WindPower;
"""

WIND_DECL_MARKER_OLD = """\
\tfloat4 _CTI_SRP_Wind;
\tfloat _CTI_SRP_Turbulence;

#if defined(_PARALLAXMAP)"""

WIND_DECL_MARKER_NEW = """\
\tfloat4 _CTI_SRP_Wind;
\tfloat _CTI_SRP_Turbulence;

// 2026-04-29: declarations added to fix Unity 6.4 / URP 17 compile errors.
\tfloat _WindStrength;
\tfloat _WindPower;

#if defined(_PARALLAXMAP)"""


WIND_OVERLOAD_OLD = """\
void CTI_AnimateVertexSG_float(
    float3      PositionOS,
    half3       NormalOS,
    half4       VertexColor,
    float2      UV2,
    float3      UV3,

    float       leafNoise,"""

WIND_OVERLOAD_NEW = """\
// 2026-04-29: 10-arg overload added to fix Unity 6.4 / URP 17 shadergraph
// node-vs-function arity mismatch.
void CTI_AnimateVertexSG_float(
    float3 PositionOS, half3 NormalOS, half4 VertexColor, float2 UV2, float3 UV3,
    float3 baseWindMultipliers, bool IsLeaves,
    out float3 o_positionOS, out half3 o_normalOS, out half2 o_colorVariationAmbient);

void CTI_AnimateVertexSG_float(
    float3 PositionOS, half3 NormalOS, half4 VertexColor, float2 UV2, float3 UV3,
    float3 baseWindMultipliers, bool IsLeaves,
    out float3 o_positionOS, out half3 o_normalOS, out half2 o_colorVariationAmbient)
{
    CTI_AnimateVertexSG_float(
        PositionOS, NormalOS, VertexColor, UV2, UV3,
        0.0, baseWindMultipliers, true, false, float2(0, 0),
        float3(_Time.y, 0, 0), IsLeaves,
        o_positionOS, o_normalOS, o_colorVariationAmbient);
}

void CTI_AnimateVertexSG_float(
    float3      PositionOS,
    half3       NormalOS,
    half4       VertexColor,
    float2      UV2,
    float3      UV3,

    float       leafNoise,"""


def patch_billboard_vertex(path: Path, dry_run: bool) -> str:
    if not path.exists():
        return f"MISSING {path.name}"
    text = path.read_text(encoding="utf-8")
    if "_WindPower" in text and "// 2026-04-29: declarations added" in text:
        return f"CLEAN   {path.name}"
    if WIND_DECL_MARKER_OLD not in text:
        return f"NO-MATCH {path.name} (manual investigation needed)"
    new = text.replace(WIND_DECL_MARKER_OLD, WIND_DECL_MARKER_NEW)
    if not dry_run:
        path.write_text(new, encoding="utf-8")
    return f"PATCHED {path.name}"


def patch_wind_overload(path: Path, dry_run: bool) -> str:
    if not path.exists():
        return f"MISSING {path.name}"
    text = path.read_text(encoding="utf-8")
    if "// 2026-04-29: 10-arg overload added" in text:
        return f"CLEAN   {path.name}"
    if WIND_OVERLOAD_OLD not in text:
        return f"NO-MATCH {path.name} (manual investigation needed)"
    new = text.replace(WIND_OVERLOAD_OLD, WIND_OVERLOAD_NEW)
    if not dry_run:
        path.write_text(new, encoding="utf-8")
    return f"PATCHED {path.name}"


def patch_endcap_mat(path: Path, dry_run: bool) -> str:
    if not path.exists():
        return f"MISSING {path.name}"
    text = path.read_text(encoding="utf-8")
    # Idempotent check: alpha=0 and Surface=1 already?
    if (re.search(r"_BaseColor:\s*\{r:\s*1,\s*g:\s*1,\s*b:\s*1,\s*a:\s*0\}", text) and
            re.search(r"- _Surface:\s*1\b", text)):
        return f"CLEAN   {path.name}"
    new = text
    new = re.sub(r"(- _Surface:\s*)0\b", r"\g<1>1", new, count=1)
    new = re.sub(r"(- _SrcBlend:\s*)1\b", r"\g<1>5", new, count=1)
    new = re.sub(r"(- _DstBlend:\s*)0\b", r"\g<1>10", new, count=1)
    new = re.sub(r"(- _DstBlendAlpha:\s*)0\b", r"\g<1>10", new, count=1)
    new = re.sub(r"(- _ZWrite:\s*)1\b", r"\g<1>0", new, count=1)
    new = re.sub(
        r"_BaseColor:\s*\{r:\s*1,\s*g:\s*1,\s*b:\s*1,\s*a:\s*1\}",
        "_BaseColor: {r: 1, g: 1, b: 1, a: 0}",
        new, count=1,
    )
    if new == text:
        return f"NO-MATCH {path.name}"
    if not dry_run:
        path.write_text(new, encoding="utf-8")
    return f"PATCHED {path.name}"


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args()

    print(f"Reapplying vendor-pack fixes (dry-run={args.dry_run})")
    results = [
        patch_billboard_vertex(FORST_BILLBOARD_VERTEX, args.dry_run),
        patch_billboard_vertex(FORST_BILLBOARD_SIMPLE, args.dry_run),
        patch_wind_overload(FORST_WIND_HLSL, args.dry_run),
        patch_endcap_mat(PW_TREE_ENDCAP_MAT, args.dry_run),
    ]
    for r in results:
        print(f"  {r}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
