# Tier 2 AI Brains — Q-Learning Combat + Unity Sentis Integration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace dumb "chase player" enemy AI with Q-learning-driven tactical decisions that adapt across encounters, then layer Unity Sentis neural network inference on top for future ML-driven behaviors.

**Architecture:** Two-phase approach. Phase A wires the existing QLearner into BattleManager via a new CombatBrain component that encodes battle state (distance, HP ratios, ally count, behavior archetype) into a discrete state index and maps Q-actions to tactical moves (advance, retreat, flank, attack, guard). Rewards flow from combat outcomes. Q-tables persist in LongTermMemory across encounters. Phase B adds the `com.unity.sentis` package, implements real ONNX model loading in InferenceEngine, and creates a CombatIntelligence subclass of IntelligentBehavior that uses neural inference with Q-learning fallback.

**Tech Stack:** Unity 6, C#, QLearner (existing), LongTermMemory (existing), Unity Sentis 2.x, ONNX

---

## File Map

### Phase A — Q-Learning Combat AI
| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Demo/Battle/CombatBrain.cs` | Encodes battle state → discrete index, maps Q-actions → tactical moves, feeds rewards |
| Modify | `Assets/Scripts/Demo/Battle/BattleManager.cs:162-190` | Replace `ProcessAITurn()` to use CombatBrain instead of hardcoded chase |
| Modify | `Assets/Scripts/Demo/AI/DemoAIIntegration.cs` | Add Q-table persistence (save/load via LTM), per-encounter reward tracking |
| Modify | `Assets/Scripts/AI/Learning/NPCLearning.cs` | Add `SaveQTable()`/`LoadQTable()` methods exposing QLearner serialization |

### Phase B — Unity Sentis Integration
| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `Packages/manifest.json` | Add `com.unity.sentis` package |
| Rewrite | `Assets/Scripts/AI/Inference/InferenceEngine.cs` | Replace stub with real Sentis ModelAsset loading, IWorker execution, tensor I/O |
| Create | `Assets/Scripts/Demo/Battle/CombatIntelligence.cs` | Concrete IntelligentBehavior subclass: neural combat decisions with Q-learning fallback |
| Modify | `Assets/Scripts/Demo/Battle/BattleManager.cs` | Wire CombatIntelligence as optional upgrade path over CombatBrain |
| Create | `Assets/StreamingAssets/Models/combat_decision.onnx` | Placeholder ONNX model (simple feed-forward: 8 inputs → 5 outputs) |

---

## Task 1: CombatBrain — State Encoding and Action Mapping

**Files:**
- Create: `Assets/Scripts/Demo/Battle/CombatBrain.cs`

This is the core component that translates battle state into Q-learning state indices and maps Q-actions back to tactical moves.

**State space (8 discrete features, quantized to bins):**
- Distance to player: 0=adjacent, 1=near(2-3), 2=far(4+)  → 3 bins
- Own HP ratio: 0=critical(<25%), 1=wounded(25-60%), 2=healthy(>60%)  → 3 bins
- Player HP ratio: same 3 bins
- Ally count alive: 0=alone, 1=some(1-2), 2=many(3+)  → 3 bins
- Behavior archetype: 0=chase, 1=guard  → 2 bins

Total state space: 3×3×3×3×2 = **162 states**

**Action space (5 actions):**
- 0: Advance (move toward player)
- 1: Retreat (move away from player)
- 2: Flank (move perpendicular to player)
- 3: Attack (if adjacent)
- 4: Hold (end turn, guard position)

- [ ] **Step 1: Create CombatBrain.cs**

```csharp
// Assets/Scripts/Demo/Battle/CombatBrain.cs
using ForeverEngine.AI.Learning;

namespace ForeverEngine.Demo.Battle
{
    public class CombatBrain
    {
        public const int StateSize = 162; // 3*3*3*3*2
        public const int ActionSize = 5;

        public enum Action { Advance, Retreat, Flank, Attack, Hold }

        private QLearner _learner;
        private int _lastState = -1;
        private int _lastAction = -1;
        private float _pendingReward;

        public CombatBrain(float[] savedQTable = null, int seed = 42)
        {
            _learner = new QLearner(StateSize, ActionSize, learningRate: 0.15f,
                discountFactor: 0.85f, explorationRate: 0.25f, seed: seed);
            if (savedQTable != null) _learner.LoadTable(savedQTable);
        }

        public int EncodeState(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior)
        {
            int dist = System.Math.Abs(self.X - player.X) + System.Math.Abs(self.Y - player.Y);
            int distBin = dist <= 1 ? 0 : dist <= 3 ? 1 : 2;

            float selfHpRatio = (float)self.HP / self.MaxHP;
            int selfHpBin = selfHpRatio < 0.25f ? 0 : selfHpRatio < 0.6f ? 1 : 2;

            float playerHpRatio = (float)player.HP / player.MaxHP;
            int playerHpBin = playerHpRatio < 0.25f ? 0 : playerHpRatio < 0.6f ? 1 : 2;

            int allyBin = aliveAllies <= 0 ? 0 : aliveAllies <= 2 ? 1 : 2;
            int behaviorBin = behavior == "guard" ? 1 : 0;

            return distBin + 3 * (selfHpBin + 3 * (playerHpBin + 3 * (allyBin + 3 * behaviorBin)));
        }

        public Action Decide(BattleCombatant self, BattleCombatant player,
            int aliveAllies, string behavior)
        {
            // Deliver pending reward from last action
            int state = EncodeState(self, player, aliveAllies, behavior);
            if (_lastState >= 0)
                _learner.Update(_lastState, _lastAction, _pendingReward, state);

            _pendingReward = 0f;
            _lastState = state;
            _lastAction = _learner.ChooseAction(state);
            return (Action)_lastAction;
        }

        public void AddReward(float reward) => _pendingReward += reward;

        public void OnEpisodeEnd(float finalReward)
        {
            if (_lastState >= 0)
            {
                _learner.Update(_lastState, _lastAction, finalReward + _pendingReward, _lastState);
                _lastState = -1;
                _pendingReward = 0f;
            }
        }

        public float[] SaveQTable() => _learner.SaveTable();

        public void SetExploration(float rate) => _learner.SetExplorationRate(rate);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/combat-brain-compile.log -quit 2>/dev/null; echo "Exit: $?"
```
Expected: Exit 0, no compile errors in log.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/CombatBrain.cs
git commit -m "feat: CombatBrain — Q-learning state encoder and action mapper for enemy AI"
```

---

## Task 2: Wire CombatBrain into BattleManager

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs:162-190`

Replace the hardcoded `ProcessAITurn()` with CombatBrain-driven decisions. Each enemy gets its own CombatBrain instance. The BattleManager translates CombatBrain actions into grid movement and attacks.

- [ ] **Step 1: Add CombatBrain storage to BattleManager**

Add after the existing fields at the top of BattleManager class:

```csharp
private Dictionary<BattleCombatant, CombatBrain> _brains = new();
```

Add using at top:
```csharp
using System.Collections.Generic;
```

- [ ] **Step 2: Initialize brains in Start() after combatant setup**

After the existing combatant initialization and initiative rolls in `Start()`, add:

```csharp
// Initialize Q-learning brains for each enemy
var savedTable = Demo.AI.DemoAIIntegration.Instance?.LoadCombatQTable();
foreach (var c in Combatants)
{
    if (!c.IsPlayer && c.IsAlive)
        _brains[c] = new CombatBrain(savedTable, seed: (int)_rngSeed + c.X * 100 + c.Y);
}
```

- [ ] **Step 3: Replace ProcessAITurn with Q-learning logic**

Replace the existing `ProcessAITurn()` method (lines 162-190) with:

```csharp
private void ProcessAITurn()
{
    var ai = CurrentTurn;
    var player = Combatants.FirstOrDefault(c => c.IsPlayer && c.IsAlive);
    if (player == null) { NextTurn(); return; }

    if (!_brains.TryGetValue(ai, out var brain))
    {
        // Fallback: old chase behavior for any combatant without a brain
        FallbackAI(ai, player);
        Invoke(nameof(NextTurn), 0.5f);
        return;
    }

    int aliveAllies = Combatants.Count(c => !c.IsPlayer && c.IsAlive && c != ai);
    var action = brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");

    switch (action)
    {
        case CombatBrain.Action.Advance:
            MoveToward(ai, player.X, player.Y);
            if (IsAdjacent(ai, player) && ai.HasAction)
            {
                ResolveAttack(ai, player);
                ai.HasAction = false;
                brain.AddReward(0.1f); // Reward closing + attacking
            }
            break;

        case CombatBrain.Action.Retreat:
            MoveAway(ai, player.X, player.Y);
            if (ai.HP < ai.MaxHP * 0.3f) brain.AddReward(0.2f); // Reward retreating when wounded
            break;

        case CombatBrain.Action.Flank:
            MoveFlank(ai, player.X, player.Y);
            break;

        case CombatBrain.Action.Attack:
            if (IsAdjacent(ai, player) && ai.HasAction)
            {
                ResolveAttack(ai, player);
                ai.HasAction = false;
                brain.AddReward(0.3f); // Reward successful attack opportunity
            }
            else
            {
                MoveToward(ai, player.X, player.Y); // Can't attack, advance instead
            }
            break;

        case CombatBrain.Action.Hold:
            brain.AddReward(ai.Behavior == "guard" ? 0.1f : -0.05f); // Guards rewarded for holding
            break;
    }

    Invoke(nameof(NextTurn), 0.5f);
}
```

- [ ] **Step 4: Add movement helper methods**

Add these after ProcessAITurn:

```csharp
private void MoveToward(BattleCombatant ai, int targetX, int targetY)
{
    while (ai.MovementRemaining > 0)
    {
        int dx = System.Math.Sign(targetX - ai.X);
        int dy = System.Math.Sign(targetY - ai.Y);
        if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
    }
}

private void MoveAway(BattleCombatant ai, int targetX, int targetY)
{
    while (ai.MovementRemaining > 0)
    {
        int dx = -System.Math.Sign(targetX - ai.X);
        int dy = -System.Math.Sign(targetY - ai.Y);
        if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
    }
}

private void MoveFlank(BattleCombatant ai, int targetX, int targetY)
{
    // Move perpendicular to player — try both directions
    int dx = System.Math.Sign(targetX - ai.X);
    int dy = System.Math.Sign(targetY - ai.Y);
    // Swap and try perpendicular movement
    int perpX = ai.X + dy, perpY = ai.Y - dx;
    if (!TryMove(ai, perpX, perpY))
    {
        perpX = ai.X - dy; perpY = ai.Y + dx;
        TryMove(ai, perpX, perpY);
    }
    // Then close distance
    MoveToward(ai, targetX, targetY);
}

private bool TryMove(BattleCombatant ai, int nx, int ny)
{
    if (nx < 0 || ny < 0 || nx >= Grid.Width || ny >= Grid.Height) return false;
    if (!Grid.IsWalkable(nx, ny)) return false;
    if (Combatants.Any(c => c.IsAlive && c != ai && c.X == nx && c.Y == ny)) return false;
    ai.X = nx; ai.Y = ny; ai.MovementRemaining--;
    return true;
}

private void FallbackAI(BattleCombatant ai, BattleCombatant player)
{
    int dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
    while (ai.MovementRemaining > 0 && dist > 1)
    {
        int dx = System.Math.Sign(player.X - ai.X);
        int dy = System.Math.Sign(player.Y - ai.Y);
        if (!TryMove(ai, ai.X + dx, ai.Y + dy)) break;
        dist = System.Math.Abs(ai.X - player.X) + System.Math.Abs(ai.Y - player.Y);
    }
    if (IsAdjacent(ai, player) && ai.HasAction)
    {
        ResolveAttack(ai, player);
        ai.HasAction = false;
    }
}
```

- [ ] **Step 5: Add reward hooks into ResolveAttack**

In `ResolveAttack()`, after the existing AI event hooks, add brain rewards. After `if (!target.IsAlive)` block (around line 213):

```csharp
// Q-learning rewards for enemy combatants
if (!attacker.IsPlayer && _brains.TryGetValue(attacker, out var atkBrain))
    atkBrain.AddReward(hit ? 0.5f : -0.1f); // Reward hitting, penalize missing
if (!target.IsPlayer && hit && _brains.TryGetValue(target, out var tgtBrain))
    tgtBrain.AddReward(-0.3f); // Penalize taking damage
```

After `Log.Add($"{target.Name} is defeated!");` add:

```csharp
if (target.IsPlayer)
    foreach (var b in _brains.Values) b.AddReward(1.0f); // All enemies rewarded for killing player
```

- [ ] **Step 6: End episodes in CheckBattleEnd**

In `CheckBattleEnd()`, after the player death block, add:

```csharp
if (BattleOver)
{
    float endReward = PlayerWon ? -0.5f : 0.5f; // Enemies penalized if player wins
    foreach (var b in _brains.Values) b.OnEpisodeEnd(endReward);
    Demo.AI.DemoAIIntegration.Instance?.SaveCombatQTable(_brains.Values.FirstOrDefault()?.SaveQTable());
}
```

Add `using System.Linq;` if not already present.

- [ ] **Step 7: Verify compilation**

Run:
```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/combat-brain-wire.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleManager.cs
git commit -m "feat: Replace hardcoded enemy AI with Q-learning CombatBrain decisions"
```

---

## Task 3: Q-Table Persistence via LongTermMemory

**Files:**
- Modify: `Assets/Scripts/Demo/AI/DemoAIIntegration.cs`
- Modify: `Assets/Scripts/AI/Learning/NPCLearning.cs`

Save Q-tables to LongTermMemory so enemies learn across encounters. Each encounter's Q-table gets merged back into the shared table.

- [ ] **Step 1: Add Q-table persistence methods to DemoAIIntegration**

Add these methods to DemoAIIntegration class:

```csharp
public float[] LoadCombatQTable()
{
    var mem = MemoryManager.Instance?.LongTerm;
    if (mem == null) return null;
    string json = mem.Get("combat_qtable", null);
    if (string.IsNullOrEmpty(json)) return null;
    var wrapper = UnityEngine.JsonUtility.FromJson<QTableWrapper>(json);
    return wrapper?.values;
}

public void SaveCombatQTable(float[] table)
{
    if (table == null) return;
    var mem = MemoryManager.Instance?.LongTerm;
    if (mem == null) return;
    var wrapper = new QTableWrapper { values = table };
    mem.Set("combat_qtable", UnityEngine.JsonUtility.ToJson(wrapper));
    _encountersSinceLastSave++;
    if (_encountersSinceLastSave >= 3) // Save to disk every 3 encounters
    {
        MemoryManager.Instance.SaveLongTerm();
        _encountersSinceLastSave = 0;
    }
}

private int _encountersSinceLastSave;

[System.Serializable]
private class QTableWrapper { public float[] values; }
```

- [ ] **Step 2: Update GetAIStatusText to show learning info**

In `GetAIStatusText()`, add after the existing K/D line:

```csharp
string hasQTable = LoadCombatQTable() != null ? "Active" : "New";
text += $"\nQ-Learning: {hasQTable}";
```

- [ ] **Step 3: Verify compilation**

Run:
```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/qtable-persist.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Demo/AI/DemoAIIntegration.cs
git commit -m "feat: Q-table persistence — enemies learn across encounters via LongTermMemory"
```

---

## Task 4: Rebuild Scenes and Verify Gameplay

**Files:**
- Modify: `Assets/Scenes/BattleMap.unity` (rebuilt via editor script)

- [ ] **Step 1: Rebuild all demo scenes**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod ForeverEngine.Editor.DemoSceneBuilder.BuildAll -logFile tests/scene-rebuild.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 2: Launch demo and capture screenshot**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod ForeverEngine.Editor.DemoSceneBuilder.PlaytestCapture -logFile tests/playtest.log -quit 2>/dev/null; echo "Exit: $?"
```

Review screenshot to verify battle still renders correctly.

- [ ] **Step 3: Commit scenes**

```bash
git add Assets/Scenes/
git commit -m "chore: Rebuild demo scenes with Q-learning combat AI"
```

---

## Task 5: Unity Sentis Package Integration

**Files:**
- Modify: `Packages/manifest.json`

- [ ] **Step 1: Add Sentis to package manifest**

Add to the dependencies object in `Packages/manifest.json`:

```json
"com.unity.sentis": "2.6.0"
```

- [ ] **Step 2: Resolve packages**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/sentis-resolve.log -quit 2>/dev/null; echo "Exit: $?"
```

Check log for successful Sentis import. If the exact version fails, check available versions and use the latest compatible one.

- [ ] **Step 3: Commit**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "deps: Add Unity Sentis 2.6.0 for neural network inference"
```

---

## Task 6: Real InferenceEngine with Sentis

**Files:**
- Rewrite: `Assets/Scripts/AI/Inference/InferenceEngine.cs`

Replace the stub with actual Sentis model loading and inference.

- [ ] **Step 1: Rewrite InferenceEngine.cs**

```csharp
// Assets/Scripts/AI/Inference/InferenceEngine.cs
using UnityEngine;
using Unity.Sentis;

namespace ForeverEngine.AI.Inference
{
    public class InferenceEngine : MonoBehaviour
    {
        public static InferenceEngine Instance { get; private set; }

        private Model _model;
        private Worker _worker;
        private bool _modelLoaded;

        [SerializeField] private BackendType _backend = BackendType.GPUCompute;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public bool IsAvailable => _modelLoaded && _worker != null;

        public void LoadModel(string path)
        {
            UnloadModel();
            try
            {
                var modelAsset = Resources.Load<ModelAsset>(path);
                if (modelAsset == null)
                {
                    // Try StreamingAssets path
                    string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, path);
                    if (System.IO.File.Exists(fullPath))
                    {
                        _model = ModelLoader.Load(fullPath);
                    }
                    else
                    {
                        Debug.LogWarning($"[InferenceEngine] Model not found: {path}");
                        return;
                    }
                }
                else
                {
                    _model = ModelLoader.Load(modelAsset);
                }

                _worker = new Worker(_model, _backend);
                _modelLoaded = true;
                Debug.Log($"[InferenceEngine] Model loaded: {path} (backend: {_backend})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[InferenceEngine] Failed to load model: {e.Message}");
                _modelLoaded = false;
            }
        }

        public float[] Infer(float[] input)
        {
            if (!IsAvailable) return input; // Pass-through fallback

            using var inputTensor = new Tensor<float>(new TensorShape(1, input.Length), input);
            _worker.Schedule(inputTensor);

            var outputTensor = _worker.PeekOutput() as Tensor<float>;
            if (outputTensor == null) return input;

            var result = outputTensor.DownloadToArray();
            return result;
        }

        public void UnloadModel()
        {
            _worker?.Dispose();
            _worker = null;
            _model = null;
            _modelLoaded = false;
        }

        private void OnDestroy()
        {
            UnloadModel();
            if (Instance == this) Instance = null;
        }
    }
}
```

- [ ] **Step 2: Verify compilation with Sentis**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/sentis-compile.log -quit 2>/dev/null; echo "Exit: $?"
```

If Sentis API has changed (version differences), check the compilation log and adapt the API calls to match the installed version.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/AI/Inference/InferenceEngine.cs
git commit -m "feat: Real Sentis-powered InferenceEngine — ONNX model loading and GPU inference"
```

---

## Task 7: Generate Placeholder ONNX Model

**Files:**
- Create: `Assets/StreamingAssets/Models/combat_decision.onnx`
- Create: `tools/generate_combat_model.py`

Create a simple feed-forward network that maps combat state (8 floats) to action probabilities (5 floats). This is a placeholder — the real model would be trained from Q-table data.

- [ ] **Step 1: Create model generation script**

```python
# tools/generate_combat_model.py
"""Generate a placeholder ONNX model for combat decisions.
Input: 8 floats (distance, selfHP, playerHP, allies, behavior, round, hasMoved, hasAction)
Output: 5 floats (action logits: advance, retreat, flank, attack, hold)
"""
import numpy as np

try:
    import onnx
    from onnx import helper, TensorProto, numpy_helper
except ImportError:
    print("Installing onnx...")
    import subprocess, sys
    subprocess.check_call([sys.executable, "-m", "pip", "install", "onnx"])
    import onnx
    from onnx import helper, TensorProto, numpy_helper

np.random.seed(42)

# Simple 2-layer network: 8 -> 16 -> 5
W1 = numpy_helper.from_array(np.random.randn(8, 16).astype(np.float32) * 0.5, "W1")
B1 = numpy_helper.from_array(np.zeros(16, dtype=np.float32), "B1")
W2 = numpy_helper.from_array(np.random.randn(16, 5).astype(np.float32) * 0.5, "W2")
B2 = numpy_helper.from_array(np.zeros(5, dtype=np.float32), "B2")

X = helper.make_tensor_value_info("input", TensorProto.FLOAT, [1, 8])
Y = helper.make_tensor_value_info("output", TensorProto.FLOAT, [1, 5])

# Layer 1: matmul + bias + relu
mm1 = helper.make_node("MatMul", ["input", "W1"], ["mm1"])
add1 = helper.make_node("Add", ["mm1", "B1"], ["add1"])
relu1 = helper.make_node("Relu", ["add1"], ["relu1"])
# Layer 2: matmul + bias
mm2 = helper.make_node("MatMul", ["relu1", "W2"], ["mm2"])
add2 = helper.make_node("Add", ["mm2", "B2"], ["output"])

graph = helper.make_graph([mm1, add1, relu1, mm2, add2], "CombatDecision",
                          [X], [Y], [W1, B1, W2, B2])
model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", 17)])
model.ir_version = 8

import os
os.makedirs("Assets/StreamingAssets/Models", exist_ok=True)
onnx.save(model, "Assets/StreamingAssets/Models/combat_decision.onnx")
print(f"Saved combat_decision.onnx ({os.path.getsize('Assets/StreamingAssets/Models/combat_decision.onnx')} bytes)")
```

- [ ] **Step 2: Run the generator**

```bash
cd "C:/Dev/Forever engin"
python tools/generate_combat_model.py
```

Expected: `Saved combat_decision.onnx (XXXX bytes)`

- [ ] **Step 3: Commit**

```bash
git add tools/generate_combat_model.py Assets/StreamingAssets/Models/combat_decision.onnx
git commit -m "feat: Placeholder ONNX combat decision model (8→16→5 feed-forward)"
```

---

## Task 8: CombatIntelligence — Neural Inference with Q-Learning Fallback

**Files:**
- Create: `Assets/Scripts/Demo/Battle/CombatIntelligence.cs`

Concrete subclass of IntelligentBehavior that uses Sentis for combat decisions with automatic fallback to Q-learning CombatBrain.

- [ ] **Step 1: Create CombatIntelligence.cs**

```csharp
// Assets/Scripts/Demo/Battle/CombatIntelligence.cs
using UnityEngine;
using ForeverEngine.AI.Inference;

namespace ForeverEngine.Demo.Battle
{
    public class CombatIntelligence : IntelligentBehavior
    {
        private CombatBrain _fallbackBrain;
        private BattleCombatant _self;
        private BattleCombatant _player;
        private int _aliveAllies;
        private string _behavior;
        private CombatBrain.Action _lastDecision;

        public CombatBrain.Action LastDecision => _lastDecision;
        public bool UsingNeural { get; private set; }

        public void Configure(BattleCombatant self, CombatBrain fallback)
        {
            _self = self;
            _fallbackBrain = fallback;
        }

        public void SetBattleContext(BattleCombatant player, int aliveAllies, string behavior)
        {
            _player = player;
            _aliveAllies = aliveAllies;
            _behavior = behavior;
        }

        public CombatBrain.Action DecideAction()
        {
            if (InferenceEngine.Instance != null && InferenceEngine.Instance.IsAvailable)
            {
                float[] input = GetModelInput();
                float[] output = InferenceEngine.Instance.Infer(input);
                ApplyModelOutput(output);
                UsingNeural = true;
            }
            else
            {
                FallbackBehavior();
                UsingNeural = false;
            }
            return _lastDecision;
        }

        protected override float[] GetModelInput()
        {
            if (_self == null || _player == null) return new float[8];

            int dist = System.Math.Abs(_self.X - _player.X) + System.Math.Abs(_self.Y - _player.Y);
            return new float[]
            {
                dist / 10f,                              // normalized distance
                (float)_self.HP / _self.MaxHP,           // self HP ratio
                (float)_player.HP / _player.MaxHP,       // player HP ratio
                _aliveAllies / 5f,                       // normalized ally count
                _behavior == "guard" ? 1f : 0f,          // behavior flag
                0f,                                      // round (placeholder)
                _self.MovementRemaining > 0 ? 1f : 0f,   // can move
                _self.HasAction ? 1f : 0f                 // can act
            };
        }

        protected override void ApplyModelOutput(float[] output)
        {
            if (output == null || output.Length < CombatBrain.ActionSize)
            {
                FallbackBehavior();
                return;
            }

            // Pick highest-scoring action
            int best = 0;
            for (int i = 1; i < CombatBrain.ActionSize; i++)
                if (output[i] > output[best]) best = i;

            _lastDecision = (CombatBrain.Action)best;
        }

        protected override void FallbackBehavior()
        {
            if (_fallbackBrain != null && _self != null && _player != null)
                _lastDecision = _fallbackBrain.Decide(_self, _player, _aliveAllies, _behavior);
            else
                _lastDecision = CombatBrain.Action.Advance;
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/combat-intel.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Demo/Battle/CombatIntelligence.cs
git commit -m "feat: CombatIntelligence — neural combat decisions with Q-learning fallback"
```

---

## Task 9: Wire Sentis into Battle Loop

**Files:**
- Modify: `Assets/Scripts/Demo/Battle/BattleManager.cs`
- Modify: `Assets/Scripts/Demo/AI/DemoAIIntegration.cs`

Load the ONNX model at battle start and optionally use CombatIntelligence when available.

- [ ] **Step 1: Add model loading to DemoAIIntegration.OnCombatStarted**

In `DemoAIIntegration.OnCombatStarted()`, add at the end:

```csharp
// Attempt to load neural combat model
var engine = ForeverEngine.AI.Inference.InferenceEngine.Instance;
if (engine != null && !engine.IsAvailable)
    engine.LoadModel("Models/combat_decision.onnx");
```

- [ ] **Step 2: Add neural decision path to BattleManager**

Update the `_brains` dictionary and ProcessAITurn to optionally use CombatIntelligence. Add field:

```csharp
private CombatIntelligence _neuralBrain; // Shared neural intelligence (optional)
```

In `Start()`, after brain initialization, add:

```csharp
// Try to create neural intelligence if InferenceEngine exists
if (ForeverEngine.AI.Inference.InferenceEngine.Instance != null)
{
    var go = new GameObject("CombatIntelligence");
    go.transform.SetParent(transform);
    _neuralBrain = go.AddComponent<CombatIntelligence>();
}
```

In `ProcessAITurn`, before the `brain.Decide()` call, add the neural path:

```csharp
CombatBrain.Action action;
if (_neuralBrain != null && ForeverEngine.AI.Inference.InferenceEngine.Instance?.IsAvailable == true)
{
    _neuralBrain.Configure(ai, brain);
    _neuralBrain.SetBattleContext(player, aliveAllies, ai.Behavior ?? "chase");
    action = _neuralBrain.DecideAction();
    // Feed neural decision back to Q-learner for hybrid learning
    if (_neuralBrain.UsingNeural)
        brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase"); // Keep Q-table updated
}
else
{
    action = brain.Decide(ai, player, aliveAllies, ai.Behavior ?? "chase");
}
```

- [ ] **Step 3: Update HUD status to show neural vs Q-learning**

In `DemoAIIntegration.GetAIStatusText()`, update the Q-Learning line:

```csharp
var engine = ForeverEngine.AI.Inference.InferenceEngine.Instance;
string aiMode = engine != null && engine.IsAvailable ? "Neural+QL" : (LoadCombatQTable() != null ? "Q-Learning" : "New");
text += $"\nAI Brain: {aiMode}";
```

- [ ] **Step 4: Verify compilation**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/sentis-wire.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 5: Rebuild scenes**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod ForeverEngine.Editor.DemoSceneBuilder.BuildAll -logFile tests/final-rebuild.log -quit 2>/dev/null; echo "Exit: $?"
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Demo/Battle/BattleManager.cs Assets/Scripts/Demo/AI/DemoAIIntegration.cs Assets/Scenes/
git commit -m "feat: Wire Sentis neural inference into battle loop with Q-learning hybrid fallback"
```

---

## Task 10: Final Verification

- [ ] **Step 1: Full compile check**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -logFile tests/final-compile.log -quit 2>/dev/null; echo "Exit: $?"
grep -i "error\|exception" tests/final-compile.log | head -20
```

- [ ] **Step 2: Playtest capture**

```bash
cd "C:/Dev/Forever engin"
"C:/Program Files/Unity/Hub/Editor/6000.1.3f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod ForeverEngine.Editor.DemoSceneBuilder.PlaytestCapture -logFile tests/final-playtest.log -quit 2>/dev/null; echo "Exit: $?"
```

Review screenshot — HUD should show AI Brain status (Q-Learning or Neural+QL).

- [ ] **Step 3: Verify git is clean**

```bash
cd "C:/Dev/Forever engin"
git status
git log --oneline -5
```
