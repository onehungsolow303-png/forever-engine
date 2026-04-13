# Smooth Hex Movement & Camera Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace instant hex teleportation with smooth 0.3s lerp movement (with input blocking and character rotation), and switch camera orbit from Q/E keys to right-click drag.

**Architecture:** Three files changed. The renderer gains a coroutine-driven movement animation system that blocks input in the manager during transit. The camera controller replaces keyboard/middle-mouse orbit with right-click drag. No new files created.

**Tech Stack:** Unity 6 / C# / MonoBehaviour coroutines

---

### Task 1: Add smooth movement animation to Overworld3DRenderer

**Files:**
- Modify: `Assets/Scripts/Demo/Overworld/Overworld3DRenderer.cs`

This task adds a coroutine-based movement animation system. When `StartMoveAnimation` is called, the player capsule lerps from its current position to the target hex over 0.3s and rotates to face the movement direction. `IsMoving` blocks further input.

- [ ] **Step 1: Add movement state fields and IsMoving property (line 20, after `_directionalLight`)**

Add these fields to the class:

```csharp
private Coroutine _moveCoroutine;
private const float MOVE_DURATION = 0.3f;

/// <summary>True while the player model is animating between hexes.</summary>
public bool IsMoving => _moveCoroutine != null;
```

- [ ] **Step 2: Add the StartMoveAnimation method (after UpdateVisuals, around line 171)**

```csharp
/// <summary>
/// Kick off a smooth lerp from the player's current visual position to the target hex.
/// Called by OverworldManager after a successful TryMove.
/// </summary>
public void StartMoveAnimation(int targetQ, int targetR)
{
    if (_playerModel == null) return;
    float hexSize = _prefabMap != null ? _prefabMap.HexWorldSize : 4f;
    Vector3 targetPos = HexToWorld3D(targetQ, targetR, 0, hexSize, 0f) + Vector3.up * 0.1f;

    if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
    _moveCoroutine = StartCoroutine(AnimateMove(targetPos));
}

private System.Collections.IEnumerator AnimateMove(Vector3 targetPos)
{
    Vector3 startPos = _playerModel.transform.position;

    // Rotate to face movement direction (XZ only)
    Vector3 direction = targetPos - startPos;
    direction.y = 0f;
    if (direction.sqrMagnitude > 0.001f)
    {
        Quaternion targetRot = Quaternion.LookRotation(direction);
        _playerModel.transform.rotation = targetRot;
    }

    // Lerp position over MOVE_DURATION using smooth step
    float elapsed = 0f;
    while (elapsed < MOVE_DURATION)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / MOVE_DURATION));
        _playerModel.transform.position = Vector3.Lerp(startPos, targetPos, t);
        yield return null;
    }

    _playerModel.transform.position = targetPos;
    _moveCoroutine = null;
}
```

- [ ] **Step 3: Guard the player position update in UpdateVisuals to skip during animation**

Replace lines 129-142 (the existing player lerp block in `UpdateVisuals`):

```csharp
// Lerp player model to target hex position
if (_playerModel != null)
{
    int height = 0;
    if (OverworldManager.Instance != null &&
        OverworldManager.Instance.Tiles.TryGetValue((playerQ, playerR), out var playerTile))
    {
        height = playerTile.Height;
    }

    Vector3 targetPos = HexToWorld3D(playerQ, playerR, 0, hexSize, 0f) + Vector3.up * 0.1f;
    _playerModel.transform.position = Vector3.Lerp(
        _playerModel.transform.position, targetPos, Time.deltaTime * 12f);
}
```

With:

```csharp
// Snap player to target hex when not animating (handles teleport, scene load, etc.)
if (_playerModel != null && !IsMoving)
{
    Vector3 targetPos = HexToWorld3D(playerQ, playerR, 0, hexSize, 0f) + Vector3.up * 0.1f;
    _playerModel.transform.position = targetPos;
}
```

- [ ] **Step 4: Add System.Collections to the using directives if not present**

The file already has `System.Collections.Generic` — verify `System.Collections` is available for `IEnumerator`. Unity MonoBehaviours can use `IEnumerator` from `System.Collections` which is implicitly available, but the coroutine return type uses it. The qualified name `System.Collections.IEnumerator` in the method signature avoids needing an extra using.

- [ ] **Step 5: Compile check**

```bash
cd "C:/Dev/Forever engine"
CSC="C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll"
RSP=$(find "Library/Bee" -name "ForeverEngine.rsp" 2>/dev/null | head -1)
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Expected: exit code 0 (clean compile).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Demo/Overworld/Overworld3DRenderer.cs
git commit -m "feat: add smooth 0.3s hex movement animation with rotation"
```

---

### Task 2: Block input during movement in OverworldManager

**Files:**
- Modify: `Assets/Scripts/Demo/Overworld/OverworldManager.cs`

The manager must check `IsMoving` before accepting input, and call `StartMoveAnimation` after a successful `TryMove`.

- [ ] **Step 1: Add IsMoving guard at the top of the input block (line 122)**

Replace lines 122-131 (the input block):

```csharp
// Input: hex movement (WASD mapped to 3D camera-relative hex directions)
// In 3D view: +R = +Z (forward/up on screen), +Q = +X (right on screen)
if      (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    Player.TryMove(0, -1);
else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  Player.TryMove(0, 1);
else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  Player.TryMove(-1, 0);
else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Player.TryMove(1, 0);
else if (Input.GetKeyDown(KeyCode.Z)) Player.TryMove(-1, -1); // hex NW
else if (Input.GetKeyDown(KeyCode.C)) Player.TryMove(1, 1);   // hex SE
else if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryEnterLocation();
```

With:

```csharp
// Block all input while the player model is animating between hexes
if (_renderer3D != null && _renderer3D.IsMoving) return;

// Input: hex movement (WASD mapped to 3D camera-relative hex directions)
// In 3D view: +R = +Z (forward/up on screen), +Q = +X (right on screen)
bool moved = false;
if      (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    moved = Player.TryMove(0, -1);
else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  moved = Player.TryMove(0, 1);
else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  moved = Player.TryMove(-1, 0);
else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) moved = Player.TryMove(1, 0);
else if (Input.GetKeyDown(KeyCode.Z)) moved = Player.TryMove(-1, -1); // hex NW
else if (Input.GetKeyDown(KeyCode.C)) moved = Player.TryMove(1, 1);   // hex SE
else if (Input.GetKeyDown(KeyCode.F)) Player.Forage();
else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TryEnterLocation();

// Trigger smooth animation for 3D renderer
if (moved && _renderer3D != null)
    _renderer3D.StartMoveAnimation(Player.Q, Player.R);
```

- [ ] **Step 2: Compile check**

```bash
cd "C:/Dev/Forever engine"
CSC="C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll"
RSP=$(find "Library/Bee" -name "ForeverEngine.rsp" 2>/dev/null | head -1)
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Expected: exit code 0.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Overworld/OverworldManager.cs
git commit -m "feat: block input during hex movement animation"
```

---

### Task 3: Switch camera orbit to right-click drag

**Files:**
- Modify: `Assets/Scripts/MonoBehaviour/Camera/PerspectiveCameraController.cs`

Replace Q/E keyboard orbit and middle-mouse orbit with right-click drag. Remove the `SuppressKeyboardOrbit` property (no longer needed). Keep scroll wheel zoom unchanged.

- [ ] **Step 1: Update the class docstring (lines 20-23)**

Replace:

```csharp
/// Controls:
///   Q/E or Middle-mouse drag  — orbit (rotate around target)
///   Scroll wheel              — zoom in/out
///   Camera auto-follows FollowTarget with damping
```

With:

```csharp
/// Controls:
///   Right-click drag  — orbit (rotate around target)
///   Scroll wheel      — zoom in/out
///   Camera auto-follows FollowTarget with damping
```

- [ ] **Step 2: Remove Q/E orbit speed field, rename mouse orbit speed (lines 45-49)**

Replace:

```csharp
[Tooltip("Speed of orbit rotation when pressing Q/E (degrees/sec).")]
[SerializeField] private float _orbitSpeed = 120f;

[Tooltip("Speed of orbit rotation when dragging middle mouse (degrees/pixel).")]
[SerializeField] private float _mouseOrbitSpeed = 0.5f;
```

With:

```csharp
[Tooltip("Speed of orbit rotation when right-click dragging (degrees/pixel).")]
[SerializeField] private float _orbitSpeed = 0.5f;
```

- [ ] **Step 3: Remove SuppressKeyboardOrbit property (lines 120-125)**

Delete:

```csharp
/// <summary>
/// When true, Q/E orbit is suppressed to avoid conflicts with
/// OverworldManager hex movement (Q=NW, E=SE). Middle-mouse
/// orbit still works. Set by Overworld3DSetup.
/// </summary>
public bool SuppressKeyboardOrbit { get; set; }
```

- [ ] **Step 4: Replace HandleInput orbit logic (lines 127-157)**

Replace the entire `HandleInput()` method:

```csharp
private void HandleInput()
{
    // Orbit: right-click drag
    if (UnityEngine.Input.GetMouseButton(1))
    {
        float dx = UnityEngine.Input.GetAxis("Mouse X");
        _orbitAngle += dx * _orbitSpeed * 10f;
    }

    // Keep orbit angle in 0-360 range
    _orbitAngle = Mathf.Repeat(_orbitAngle, 360f);

    // Zoom: scroll wheel
    float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
    if (Mathf.Abs(scroll) > 0.001f)
    {
        _targetDistance -= scroll * _zoomSpeed;
        _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
    }

    // Smooth zoom
    _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * _zoomSmoothing);
}
```

- [ ] **Step 5: Remove SuppressKeyboardOrbit reference from Overworld3DSetup**

In `Assets/Scripts/Demo/Overworld/Overworld3DSetup.cs`, the comment at line 47 (`// Q/E now available for camera orbit (hex NW/SE moved to Z/C)`) is stale. No code references `SuppressKeyboardOrbit` from this file, so just update the comment.

Replace:

```csharp
// Q/E now available for camera orbit (hex NW/SE moved to Z/C)
```

With nothing (delete the comment).

- [ ] **Step 6: Compile check**

```bash
cd "C:/Dev/Forever engine"
CSC="C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Data/DotNetSdkRoslyn/csc.dll"
RSP=$(find "Library/Bee" -name "ForeverEngine.rsp" 2>/dev/null | head -1)
dotnet "$CSC" "@$RSP" -out:Temp/check_ForeverEngine.dll
```

Expected: exit code 0. If `SuppressKeyboardOrbit` is referenced elsewhere, the compiler will catch it here.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/Camera/PerspectiveCameraController.cs Assets/Scripts/Demo/Overworld/Overworld3DSetup.cs
git commit -m "feat: switch camera orbit to right-click drag, remove Q/E orbit"
```

---

### Task 4: Manual verification in Unity

- [ ] **Step 1: Switch to Unity and let it recompile**

The editor auto-detects file changes and recompiles. Wait for the console to show no errors.

- [ ] **Step 2: Enter Play mode and verify**

Test checklist:
- Press WASD — player capsule smoothly slides to the next hex over ~0.3s
- During movement, pressing another key does nothing (input blocked)
- Player capsule rotates to face movement direction before sliding
- Right-click drag rotates camera orbit around the player
- Scroll wheel zooms in/out
- Q and E keys do NOT orbit the camera
- Middle mouse does NOT orbit the camera
- Foraging (F) and location entry (Enter) still work
- Day/night cycle and fog of war still function

- [ ] **Step 3: Final commit with all files**

```bash
git add -A
git commit -m "feat: smooth hex movement (0.3s lerp) and right-click camera orbit"
```
