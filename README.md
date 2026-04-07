# Forever Engine

The Unity 6 game runtime for the Forever engine RPG. Hybrid DOTS/MonoBehaviour
client that hosts the playable game; the AI brain and asset library live
out-of-process in Python services.

**Spec:** `C:\Dev\.shared\docs\superpowers\specs\2026-04-06-three-module-consolidation-design.md`

## Architecture

After the 2026-04-06 three-module pivot, the Forever engine is one of four repos:

| Repo | Role | Port |
|---|---|---|
| `Forever engine` (this repo) | Unity 6 game client | — |
| `Director Hub` | Agentic AI brain (LLM, memory, tools, loop) | 7802 |
| `Asset Manager` | Asset library + selectors + generators + AI gateway | 7801 |
| `.shared` | JSON schemas, codegen, project state, docs | — |

The engine talks to both Python services over plain HTTP via `Bridges/`:

- `AssetClient.cs` — coroutine HTTP client to Asset Manager
- `DirectorClient.cs` — coroutine HTTP client to Director Hub with retry + backoff
- `ServiceWatchdog.cs` — boot-time `/health` check on both services
- `GameStateServer.cs` — HttpListener on `127.0.0.1:7803` exposing live engine state to Director Hub's `game_state_tool`
- `SharedSchemaTypes.cs` — auto-generated POCOs from `.shared/codegen/csharp_gen.py`
- `SmokeTestRunner.cs` — batchmode-friendly cross-module integration test
- `DialogueSmokeTest.cs` — batchmode verification of the dialogue UI binding

## Rules

1. **Forever engine is the source of truth for rules.** Stats, HP, dice resolution, item/spell mechanics — these never leave C#. Director Hub interprets player intent; the engine resolves the math.
2. **Asset Manager is the only writer of the asset library.** The engine reads asset IDs over HTTP via AssetClient and never generates or modifies asset files at runtime.
3. **Director Hub never touches the engine directly.** It returns structured `DecisionPayload` JSON the engine applies. No direct ECS writes.
4. **`.shared/` is the contract layer.** Schemas in `.shared/schemas/` are the single source of truth. After editing one, regenerate `Bridges/SharedSchemaTypes.cs` via:

   ```bash
   python C:/Dev/.shared/codegen/csharp_gen.py \
     --out "C:/Dev/Forever engine/Assets/Scripts/Bridges/SharedSchemaTypes.cs"
   ```

5. **ECS for per-frame logic, MonoBehaviour for visuals + IO.** Per-frame AI (`AI/Inference`, `AI/Learning`, `AI/PlayerModeling`, `AI/SelfHealing`) stays in C#; LLM-driven planning and narrative live out-of-process in Director Hub.
6. **Burst-compile all Jobs.** Every `IJob` must have `[BurstCompile]`.
7. **NativeArray for map data.** Walkability, fog, elevation stored as `NativeArray`s for job access.
8. **Validate imports.** Always run `SchemaValidator` before loading external map/asset data.
9. **Fault boundaries on bridge calls.** Use `SystemMonitor.GetOrCreate("name").TryExecute(...)` around any out-of-process call so repeated failures auto-disable rather than spam logs.

## Quick start

Boot the two Python services in the background:

```bash
cd "C:/Dev/Director Hub"  && uvicorn director_hub.bridge.server:app --port 7802 &
cd "C:/Dev/Asset Manager" && uvicorn asset_manager.bridge.server:app --port 7801 &
```

Or use the docker-compose convenience in `.shared`:

```bash
cd C:/Dev/.shared && docker compose up
```

Open the Forever engine project in Unity 6 (6000.4.1f1 or newer) and play the Demo scene.

## Headless tests

Run the dialogue UI smoke test in batchmode:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
  -executeMethod ForeverEngine.Tests.DialogueSmokeTest.Run -quit -logFile -
```

Run the cross-module HTTP integration smoke test:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
  -executeMethod ForeverEngine.Tests.SmokeTestRunner.Run -quit -logFile -
```

Both exit `0` on pass, `1` on failure.

## Key files

- `Assets/Scripts/Bridges/` — out-of-process HTTP clients + state server
- `Assets/Scripts/Demo/GameManager.cs` — singleton wiring the bridge clients into the demo
- `Assets/Scripts/Demo/AI/DirectorEvents.cs` — fire-and-forget gameplay → Director event bridge
- `Assets/Scripts/Demo/UI/DialoguePanel.cs` — UI Toolkit dialogue overlay routing player text through Director Hub
- `Assets/Scripts/AI/Inference/InferenceEngine.cs` — Sentis ONNX wrapper (per-frame, in-engine)
- `Assets/Scripts/AI/Learning/QLearner.cs` — pure Q-learning (per-decision, in-engine)
- `Assets/Scripts/AI/SelfHealing/{SystemMonitor,FaultBoundary,AssetFaultHandler,PerformanceRegulator}.cs` — runtime fault graph

## Pre-pivot archive

The pre-pivot AI brain (CEO/agent system, ClaudeAPIClient, MemoryManager, AIDirector, DemoAIIntegration) lives at `C:\Dev\_archive\forever-engine-pre-pivot\` for reference. Anything still needed has been salvaged into Director Hub. Do not depend on the archive at runtime.
