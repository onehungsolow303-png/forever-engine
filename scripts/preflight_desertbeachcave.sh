#!/bin/bash
# Pre-flight checks before BuildDesertBeachCave bake.
# Per gaia-architecture skill Section 9 + 1b orphan-process rule.

set -uo pipefail
PASS=0; FAIL=0; WARN=0
ok()   { echo "  OK  $*"; PASS=$((PASS+1)); }
warn() { echo "  WARN $*"; WARN=$((WARN+1)); }
fail() { echo "  FAIL $*"; FAIL=$((FAIL+1)); }

echo "=== Pre-flight: Desert Beach Cave bake ==="

# 1. Unity closed
if [ -f "/c/Dev/Forever engine/Temp/UnityLockfile" ]; then
  fail "Unity is open (UnityLockfile present) — close Unity before batchmode"
else
  ok "Unity closed"
fi

# 1b. No orphan Unity child processes (gaia skill Pre-bake checklist 1b)
ORPHANS=$(powershell -NoProfile -Command "(Get-Process | Where-Object { \$_.Name -in @('Unity','Unity.ILPP.Runner','UnityShaderCompiler','UnityPackageManager','UnityAutoQuitter') } | Measure-Object).Count" | tr -d '[:space:]')
if [ "$ORPHANS" = "0" ]; then
  ok "No orphan Unity child processes"
else
  fail "$ORPHANS orphan Unity child processes — kill via Stop-Process -Force then delete Temp/UnityLockfile"
fi

# 2. Roslyn check artifacts not in Temp/
if ls "/c/Dev/Forever engine/Temp/check_"*.dll >/dev/null 2>&1; then
  fail "Roslyn check_*.dll leftovers in Temp/ — remove with: rm -f 'C:/Dev/Forever engine/Temp/check_*.dll'"
else
  ok "No Roslyn dll leftovers"
fi

# 3. Required packs present
PACKS=(
  "Procedural Worlds/Packages - Install/Gaia/Scripts/Core"
  "Procedural Worlds/Packages - Install/Stamps"
  "TFP/2_Prefabs/Trees"
  "Hivemind/Art"
  "3DForge/Cave Adventure kit"
  "../Packages/com.waveharmonic.crest"
)
for pack in "${PACKS[@]}"; do
  if [ -e "/c/Dev/Forever engine/Assets/$pack" ] || [ -e "/c/Dev/Forever engine/$pack" ]; then
    ok "$pack present"
  else
    fail "$pack MISSING"
  fi
done

# 4. Recovery materials (15 PW Tree mats — per gaia Bug #1)
RECOVERY=$(ls "/c/Dev/Forever engine/Assets/Procedural Worlds/_RecoveryMaterials/Trees/"*.mat 2>/dev/null | wc -l)
if [ "$RECOVERY" -ge 15 ]; then
  ok "$RECOVERY recovery mats present (>= 15)"
else
  warn "Only $RECOVERY recovery mats — Bug #1 risk if any Pine/Spruce ends up in scene (this bake doesn't use them, so warn-not-fail)"
fi

# 5. PackBiomeHeuristics 3DForge classification (belt+suspenders for future runtime renderer reuse)
HEURISTICS="/c/Dev/Forever engine/Assets/Editor/Baked/PackBiomeHeuristics.cs"
if grep -qE '"3dforge".*PackRole\.OutdoorBiomeContent' "$HEURISTICS"; then
  ok "3DForge classified OutdoorBiomeContent"
else
  warn "3DForge currently NOT OutdoorBiomeContent (cave prefabs won't enter PrefabRegistry — fine for this test build, may matter for future runtime reuse — see spec note)"
fi

echo
echo "=== SUMMARY: pass=$PASS warn=$WARN fail=$FAIL ==="
[ "$FAIL" = "0" ] || exit 1
