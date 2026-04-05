# Phase 2: Game Feature Systems — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build all gameplay systems needed to make a complete game — inventory, dialogue, quests, save/load, menus, animation, audio, and particles.

**Architecture:** ECS components + systems for game logic (inventory, quests). MonoBehaviour for Unity-native features (audio, animation, particles, UI). Each system is independent — they communicate through ECS components and events, not direct references.

**Tech Stack:** Unity 6+ DOTS, UI Toolkit, Unity Audio, Unity Animator, Unity VFX Graph, Newtonsoft JSON (for save/load)

**Spec:** `docs/superpowers/specs/2026-04-05-forever-engine-design.md` Section 7, items 13-20

**Existing Phase 1 code:** 30 C# files in `Assets/Scripts/` — ECS components, jobs, systems, MonoBehaviour renderers, UI, input, bootstrap.

---

## File Map

### Task 1 — Inventory System (ECS)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/ECS/Components/InventoryComponent.cs` | Per-entity inventory: slot count, gold |
| `Assets/Scripts/ECS/Data/ItemDatabase.cs` | ScriptableObject item definitions |
| `Assets/Scripts/ECS/Data/ItemInstance.cs` | Runtime item struct (id, count, equipped) |
| `Assets/Scripts/ECS/Systems/InventorySystem.cs` | Equip, use, drop, pickup logic |
| `Assets/Tests/EditMode/InventoryTests.cs` | Item add/remove/equip/stack tests |

### Task 2 — Dialogue System (MonoBehaviour)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/ECS/Components/DialogueComponent.cs` | Tag for NPCs with dialogue |
| `Assets/Scripts/MonoBehaviour/Dialogue/DialogueManager.cs` | Conversation state machine |
| `Assets/Scripts/MonoBehaviour/Dialogue/DialogueData.cs` | Dialogue tree data structure |
| `Assets/Scripts/MonoBehaviour/UI/DialogueUI.cs` | UI Toolkit dialogue panel |
| `Assets/Tests/EditMode/DialogueTests.cs` | Dialogue tree traversal tests |

### Task 3 — Quest System (ECS)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/ECS/Components/QuestComponent.cs` | Per-quest state tracking |
| `Assets/Scripts/ECS/Data/QuestDefinition.cs` | Quest template: objectives, rewards |
| `Assets/Scripts/ECS/Systems/QuestSystem.cs` | Objective tracking, completion |
| `Assets/Scripts/MonoBehaviour/UI/QuestLogUI.cs` | UI Toolkit quest journal |
| `Assets/Tests/EditMode/QuestTests.cs` | Quest state progression tests |

### Task 4 — Save/Load System

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/MonoBehaviour/SaveLoad/SaveManager.cs` | Serialize ECS world to JSON |
| `Assets/Scripts/MonoBehaviour/SaveLoad/SaveData.cs` | Save file data structures |
| `Assets/Tests/EditMode/SaveLoadTests.cs` | Round-trip serialize/deserialize tests |

### Task 5 — Menu System (MonoBehaviour)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/MonoBehaviour/UI/MainMenuUI.cs` | Title screen: new game, load, options |
| `Assets/Scripts/MonoBehaviour/UI/PauseMenuUI.cs` | Pause overlay: resume, save, options, quit |
| `Assets/Scripts/MonoBehaviour/UI/OptionsUI.cs` | Settings: audio volume, keybinds |
| `Assets/Scripts/MonoBehaviour/UI/InventoryUI.cs` | Inventory grid display |

### Task 6 — Animation System (MonoBehaviour)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/MonoBehaviour/Animation/SpriteAnimator.cs` | Frame-based sprite animation |
| `Assets/Scripts/MonoBehaviour/Animation/AnimationData.cs` | Animation clip definitions |
| `Assets/Tests/EditMode/AnimationTests.cs` | Frame cycling, speed, loop tests |

### Task 7 — Audio Manager (MonoBehaviour)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/MonoBehaviour/Audio/AudioManager.cs` | Singleton: play SFX, music, ambient |
| `Assets/Scripts/MonoBehaviour/Audio/AudioConfig.cs` | ScriptableObject: volume settings, clip refs |

### Task 8 — Particle/VFX System (MonoBehaviour)

| File | Responsibility |
|------|---------------|
| `Assets/Scripts/MonoBehaviour/VFX/VFXManager.cs` | Spawn/pool particle effects |
| `Assets/Scripts/MonoBehaviour/VFX/VFXConfig.cs` | ScriptableObject: effect definitions |

---

## Task 1: Inventory System

**Files:**
- Create: `Assets/Scripts/ECS/Data/ItemInstance.cs`
- Create: `Assets/Scripts/ECS/Data/ItemDatabase.cs`
- Create: `Assets/Scripts/ECS/Components/InventoryComponent.cs`
- Create: `Assets/Scripts/ECS/Systems/InventorySystem.cs`
- Create: `Assets/Tests/EditMode/InventoryTests.cs`

- [ ] **Step 1: Write inventory tests**

```csharp
// Assets/Tests/EditMode/InventoryTests.cs
using NUnit.Framework;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Tests
{
    public class InventoryTests
    {
        [Test]
        public void NewInventory_IsEmpty()
        {
            var inv = new Inventory(20);
            Assert.AreEqual(0, inv.Count);
            Assert.AreEqual(20, inv.MaxSlots);
            Assert.AreEqual(0, inv.Gold);
        }

        [Test]
        public void AddItem_IncreasesCount()
        {
            var inv = new Inventory(20);
            bool added = inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 });
            Assert.IsTrue(added);
            Assert.AreEqual(1, inv.Count);
        }

        [Test]
        public void AddItem_FullInventory_Fails()
        {
            var inv = new Inventory(2);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 });
            inv.Add(new ItemInstance { ItemId = 2, StackCount = 1 });
            bool added = inv.Add(new ItemInstance { ItemId = 3, StackCount = 1 });
            Assert.IsFalse(added);
            Assert.AreEqual(2, inv.Count);
        }

        [Test]
        public void AddItem_Stackable_MergesStack()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 3, MaxStack = 10 });
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 5, MaxStack = 10 });
            Assert.AreEqual(1, inv.Count); // One slot
            Assert.AreEqual(8, inv.GetSlot(0).StackCount); // Combined
        }

        [Test]
        public void RemoveItem_DecreasesCount()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 5, MaxStack = 10 });
            bool removed = inv.Remove(1, 3);
            Assert.IsTrue(removed);
            Assert.AreEqual(2, inv.GetSlot(0).StackCount);
        }

        [Test]
        public void RemoveItem_EntireStack_RemovesSlot()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 });
            inv.Remove(1, 1);
            Assert.AreEqual(0, inv.Count);
        }

        [Test]
        public void RemoveItem_NotPresent_Fails()
        {
            var inv = new Inventory(20);
            bool removed = inv.Remove(999, 1);
            Assert.IsFalse(removed);
        }

        [Test]
        public void Equip_SetsEquipped()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 });
            inv.Equip(0);
            Assert.IsTrue(inv.GetSlot(0).Equipped);
        }

        [Test]
        public void Unequip_ClearsEquipped()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 1, StackCount = 1 });
            inv.Equip(0);
            inv.Unequip(0);
            Assert.IsFalse(inv.GetSlot(0).Equipped);
        }

        [Test]
        public void Gold_AddAndRemove()
        {
            var inv = new Inventory(20);
            inv.AddGold(100);
            Assert.AreEqual(100, inv.Gold);
            bool spent = inv.SpendGold(60);
            Assert.IsTrue(spent);
            Assert.AreEqual(40, inv.Gold);
        }

        [Test]
        public void Gold_CantOverspend()
        {
            var inv = new Inventory(20);
            inv.AddGold(50);
            bool spent = inv.SpendGold(100);
            Assert.IsFalse(spent);
            Assert.AreEqual(50, inv.Gold); // Unchanged
        }

        [Test]
        public void HasItem_ChecksPresence()
        {
            var inv = new Inventory(20);
            inv.Add(new ItemInstance { ItemId = 42, StackCount = 3, MaxStack = 10 });
            Assert.IsTrue(inv.HasItem(42, 1));
            Assert.IsTrue(inv.HasItem(42, 3));
            Assert.IsFalse(inv.HasItem(42, 4));
            Assert.IsFalse(inv.HasItem(99));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: Unity Test Runner EditMode
Expected: FAIL — `Inventory`, `ItemInstance` types not found

- [ ] **Step 3: Implement ItemInstance**

```csharp
// Assets/Scripts/ECS/Data/ItemInstance.cs
namespace ForeverEngine.ECS.Data
{
    /// <summary>
    /// Single item stack in an inventory slot.
    /// Value type — no heap allocation, safe for ECS buffers.
    /// </summary>
    public struct ItemInstance
    {
        public int ItemId;       // References ItemDatabase entry
        public int StackCount;   // How many in this stack
        public int MaxStack;     // Max per slot (1 = unstackable)
        public bool Equipped;    // Currently worn/held

        public bool IsEmpty => ItemId == 0 || StackCount <= 0;

        public static ItemInstance Empty => new ItemInstance();
    }
}
```

- [ ] **Step 4: Implement Inventory**

```csharp
// Assets/Scripts/ECS/Data/ItemDatabase.cs
using System.Collections.Generic;

namespace ForeverEngine.ECS.Data
{
    /// <summary>
    /// Runtime inventory container — manages item slots, stacking, gold.
    /// Used by InventoryComponent as managed data.
    /// </summary>
    public class Inventory
    {
        private readonly List<ItemInstance> _slots;
        public int MaxSlots { get; }
        public int Count => _slots.Count;
        public int Gold { get; private set; }

        public Inventory(int maxSlots)
        {
            MaxSlots = maxSlots;
            _slots = new List<ItemInstance>(maxSlots);
        }

        public ItemInstance GetSlot(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index] : ItemInstance.Empty;
        }

        public bool Add(ItemInstance item)
        {
            // Try to stack with existing
            if (item.MaxStack > 1)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ItemId == item.ItemId && _slots[i].StackCount < _slots[i].MaxStack)
                    {
                        var slot = _slots[i];
                        int space = slot.MaxStack - slot.StackCount;
                        int toAdd = item.StackCount <= space ? item.StackCount : space;
                        slot.StackCount += toAdd;
                        _slots[i] = slot;
                        item.StackCount -= toAdd;
                        if (item.StackCount <= 0) return true;
                    }
                }
            }

            // Add to new slot
            if (_slots.Count >= MaxSlots) return false;
            _slots.Add(item);
            return true;
        }

        public bool Remove(int itemId, int count = 1)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId)
                {
                    var slot = _slots[i];
                    if (slot.StackCount < count) return false;
                    slot.StackCount -= count;
                    if (slot.StackCount <= 0)
                        _slots.RemoveAt(i);
                    else
                        _slots[i] = slot;
                    return true;
                }
            }
            return false;
        }

        public bool HasItem(int itemId, int count = 1)
        {
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemId == itemId)
                    total += _slots[i].StackCount;
            }
            return total >= count;
        }

        public void Equip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            var slot = _slots[slotIndex];
            slot.Equipped = true;
            _slots[slotIndex] = slot;
        }

        public void Unequip(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            var slot = _slots[slotIndex];
            slot.Equipped = false;
            _slots[slotIndex] = slot;
        }

        public void AddGold(int amount) => Gold += amount;

        public bool SpendGold(int amount)
        {
            if (Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        public List<ItemInstance> GetAllSlots() => new List<ItemInstance>(_slots);
    }
}
```

- [ ] **Step 5: Implement InventoryComponent**

```csharp
// Assets/Scripts/ECS/Components/InventoryComponent.cs
using Unity.Entities;

namespace ForeverEngine.ECS.Components
{
    /// <summary>
    /// Tag component marking an entity as having an inventory.
    /// The actual Inventory data is managed (class) and stored in InventorySystem's registry.
    /// ECS components must be unmanaged — we use Entity as a key to look up the managed Inventory.
    /// </summary>
    public struct InventoryComponent : IComponentData
    {
        public int MaxSlots;
    }
}
```

- [ ] **Step 6: Implement InventorySystem**

```csharp
// Assets/Scripts/ECS/Systems/InventorySystem.cs
using Unity.Entities;
using System.Collections.Generic;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.ECS.Systems
{
    /// <summary>
    /// Manages inventory data for entities.
    /// ECS components are unmanaged, but Inventory uses managed List.
    /// This system bridges: Entity → managed Inventory lookup.
    /// </summary>
    public partial class InventorySystem : SystemBase
    {
        private Dictionary<Entity, Inventory> _inventories = new();

        public Inventory GetInventory(Entity entity)
        {
            if (_inventories.TryGetValue(entity, out var inv)) return inv;

            if (EntityManager.HasComponent<InventoryComponent>(entity))
            {
                var comp = EntityManager.GetComponentData<InventoryComponent>(entity);
                inv = new Inventory(comp.MaxSlots);
                _inventories[entity] = inv;
                return inv;
            }
            return null;
        }

        public static InventorySystem Instance { get; private set; }

        protected override void OnCreate()
        {
            Instance = this;
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            _inventories.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Expected: 11/11 PASS

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/ECS/Data/ItemInstance.cs Assets/Scripts/ECS/Data/ItemDatabase.cs Assets/Scripts/ECS/Components/InventoryComponent.cs Assets/Scripts/ECS/Systems/InventorySystem.cs Assets/Tests/EditMode/InventoryTests.cs
git commit -m "feat: Inventory system with stacking, equip, gold — 11 tests"
```

---

## Task 2: Dialogue System

**Files:**
- Create: `Assets/Scripts/ECS/Components/DialogueComponent.cs`
- Create: `Assets/Scripts/MonoBehaviour/Dialogue/DialogueData.cs`
- Create: `Assets/Scripts/MonoBehaviour/Dialogue/DialogueManager.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/DialogueUI.cs`
- Create: `Assets/Tests/EditMode/DialogueTests.cs`

- [ ] **Step 1: Write dialogue tests**

```csharp
// Assets/Tests/EditMode/DialogueTests.cs
using NUnit.Framework;
using ForeverEngine.MonoBehaviour.Dialogue;

namespace ForeverEngine.Tests
{
    public class DialogueTests
    {
        [Test]
        public void NewConversation_StartsAtRoot()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Hello traveler!", Choices = new[] {
                new DialogueChoice { Text = "Hello", NextNodeId = "greet" },
                new DialogueChoice { Text = "Goodbye", NextNodeId = "" }
            }});
            tree.AddNode(new DialogueNode { Id = "greet", Text = "Welcome to the dungeon." });

            var state = new DialogueState(tree);
            Assert.AreEqual("root", state.CurrentNode.Id);
            Assert.AreEqual("Hello traveler!", state.CurrentNode.Text);
            Assert.AreEqual(2, state.CurrentNode.Choices.Length);
        }

        [Test]
        public void ChooseOption_AdvancesToNextNode()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Hello!", Choices = new[] {
                new DialogueChoice { Text = "Hi", NextNodeId = "response" }
            }});
            tree.AddNode(new DialogueNode { Id = "response", Text = "How can I help?" });

            var state = new DialogueState(tree);
            state.Choose(0);
            Assert.AreEqual("response", state.CurrentNode.Id);
        }

        [Test]
        public void EmptyNextNodeId_EndsConversation()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Bye!", Choices = new[] {
                new DialogueChoice { Text = "Leave", NextNodeId = "" }
            }});

            var state = new DialogueState(tree);
            state.Choose(0);
            Assert.IsTrue(state.IsFinished);
        }

        [Test]
        public void NoChoices_IsTerminal()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "The end." });

            var state = new DialogueState(tree);
            Assert.IsTrue(state.CurrentNode.IsTerminal);
        }

        [Test]
        public void ConditionalChoice_HiddenWhenConditionFalse()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Shop", Choices = new[] {
                new DialogueChoice { Text = "Buy sword (100g)", NextNodeId = "buy", ConditionTag = "has_gold_100" },
                new DialogueChoice { Text = "Leave", NextNodeId = "" }
            }});

            var state = new DialogueState(tree);
            var available = state.GetAvailableChoices(new System.Collections.Generic.HashSet<string>());
            Assert.AreEqual(1, available.Count); // Only "Leave" — no gold tag

            var withGold = state.GetAvailableChoices(new System.Collections.Generic.HashSet<string> { "has_gold_100" });
            Assert.AreEqual(2, withGold.Count); // Both options
        }
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

- [ ] **Step 3: Implement DialogueData**

```csharp
// Assets/Scripts/MonoBehaviour/Dialogue/DialogueData.cs
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.Dialogue
{
    public struct DialogueChoice
    {
        public string Text;
        public string NextNodeId;    // Empty = end conversation
        public string ConditionTag;  // Empty = always available
        public string ActionTag;     // Trigger on selection (e.g., "give_item_42")
    }

    public class DialogueNode
    {
        public string Id;
        public string Text;
        public string Speaker;
        public DialogueChoice[] Choices;

        public bool IsTerminal => Choices == null || Choices.Length == 0;
    }

    public class DialogueTree
    {
        private Dictionary<string, DialogueNode> _nodes = new();
        public string RootId { get; private set; }

        public void AddNode(DialogueNode node)
        {
            if (_nodes.Count == 0) RootId = node.Id;
            _nodes[node.Id] = node;
        }

        public DialogueNode GetNode(string id)
        {
            return _nodes.TryGetValue(id, out var node) ? node : null;
        }

        public DialogueNode Root => GetNode(RootId);
    }

    public class DialogueState
    {
        private readonly DialogueTree _tree;
        public DialogueNode CurrentNode { get; private set; }
        public bool IsFinished { get; private set; }

        public DialogueState(DialogueTree tree)
        {
            _tree = tree;
            CurrentNode = tree.Root;
        }

        public void Choose(int choiceIndex)
        {
            if (CurrentNode == null || CurrentNode.Choices == null) { IsFinished = true; return; }
            if (choiceIndex < 0 || choiceIndex >= CurrentNode.Choices.Length) return;

            var choice = CurrentNode.Choices[choiceIndex];
            if (string.IsNullOrEmpty(choice.NextNodeId))
            {
                IsFinished = true;
                return;
            }

            CurrentNode = _tree.GetNode(choice.NextNodeId);
            if (CurrentNode == null) IsFinished = true;
        }

        public List<DialogueChoice> GetAvailableChoices(HashSet<string> activeTags)
        {
            var result = new List<DialogueChoice>();
            if (CurrentNode?.Choices == null) return result;

            foreach (var choice in CurrentNode.Choices)
            {
                if (string.IsNullOrEmpty(choice.ConditionTag) || activeTags.Contains(choice.ConditionTag))
                    result.Add(choice);
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Implement DialogueComponent**

```csharp
// Assets/Scripts/ECS/Components/DialogueComponent.cs
using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    public struct DialogueComponent : IComponentData
    {
        public FixedString64Bytes DialogueTreeId;
    }
}
```

- [ ] **Step 5: Implement DialogueManager**

```csharp
// Assets/Scripts/MonoBehaviour/Dialogue/DialogueManager.cs
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.Dialogue
{
    public class DialogueManager : UnityEngine.MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        private Dictionary<string, DialogueTree> _trees = new();
        private DialogueState _activeState;

        public bool InDialogue => _activeState != null && !_activeState.IsFinished;
        public DialogueState ActiveState => _activeState;

        private void Awake() => Instance = this;

        public void RegisterTree(string id, DialogueTree tree) => _trees[id] = tree;

        public void StartDialogue(string treeId)
        {
            if (!_trees.TryGetValue(treeId, out var tree)) return;
            _activeState = new DialogueState(tree);
            Debug.Log($"[Dialogue] Started: {treeId}");
        }

        public void Choose(int index)
        {
            _activeState?.Choose(index);
            if (_activeState?.IsFinished == true)
            {
                Debug.Log("[Dialogue] Conversation ended");
                _activeState = null;
            }
        }

        public void EndDialogue()
        {
            _activeState = null;
        }
    }
}
```

- [ ] **Step 6: Implement DialogueUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/DialogueUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.MonoBehaviour.Dialogue;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class DialogueUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _dialoguePanel;
        private Label _speakerLabel;
        private Label _textLabel;
        private VisualElement _choiceContainer;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _dialoguePanel = root.Q<VisualElement>("dialogue-panel");
            _speakerLabel = root.Q<Label>("dialogue-speaker");
            _textLabel = root.Q<Label>("dialogue-text");
            _choiceContainer = root.Q<VisualElement>("dialogue-choices");
        }

        private void Update()
        {
            var mgr = DialogueManager.Instance;
            if (mgr == null) return;

            if (_dialoguePanel != null)
                _dialoguePanel.style.display = mgr.InDialogue
                    ? DisplayStyle.Flex : DisplayStyle.None;

            if (!mgr.InDialogue) return;

            var node = mgr.ActiveState.CurrentNode;
            if (node == null) return;

            if (_speakerLabel != null) _speakerLabel.text = node.Speaker ?? "";
            if (_textLabel != null) _textLabel.text = node.Text ?? "";

            if (_choiceContainer != null)
            {
                _choiceContainer.Clear();
                if (node.IsTerminal)
                {
                    var btn = new Button(() => mgr.EndDialogue()) { text = "[Continue]" };
                    _choiceContainer.Add(btn);
                }
                else
                {
                    var choices = mgr.ActiveState.GetAvailableChoices(new HashSet<string>());
                    for (int i = 0; i < choices.Count; i++)
                    {
                        int idx = i;
                        var btn = new Button(() => mgr.Choose(idx)) { text = choices[idx].Text };
                        _choiceContainer.Add(btn);
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 7: Run tests**

Expected: 5/5 PASS

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/ECS/Components/DialogueComponent.cs Assets/Scripts/MonoBehaviour/Dialogue/ Assets/Scripts/MonoBehaviour/UI/DialogueUI.cs Assets/Tests/EditMode/DialogueTests.cs
git commit -m "feat: Dialogue system with branching trees, conditions, and UI — 5 tests"
```

---

## Task 3: Quest System

**Files:**
- Create: `Assets/Scripts/ECS/Data/QuestDefinition.cs`
- Create: `Assets/Scripts/ECS/Components/QuestComponent.cs`
- Create: `Assets/Scripts/ECS/Systems/QuestSystem.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/QuestLogUI.cs`
- Create: `Assets/Tests/EditMode/QuestTests.cs`

- [ ] **Step 1: Write quest tests**

```csharp
// Assets/Tests/EditMode/QuestTests.cs
using NUnit.Framework;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.Tests
{
    public class QuestTests
    {
        [Test]
        public void NewQuest_IsNotStarted()
        {
            var quest = new QuestInstance("kill_goblins", new QuestDefinition
            {
                Id = "kill_goblins",
                Title = "Goblin Slayer",
                Objectives = new[] {
                    new QuestObjective { Id = "kill", Description = "Kill 5 goblins", RequiredCount = 5 }
                }
            });
            Assert.AreEqual(QuestStatus.NotStarted, quest.Status);
        }

        [Test]
        public void StartQuest_ChangesStatus()
        {
            var quest = CreateTestQuest();
            quest.Start();
            Assert.AreEqual(QuestStatus.Active, quest.Status);
        }

        [Test]
        public void ProgressObjective_TracksCount()
        {
            var quest = CreateTestQuest();
            quest.Start();
            quest.Progress("kill", 3);
            Assert.AreEqual(3, quest.GetObjectiveProgress("kill"));
            Assert.IsFalse(quest.IsComplete);
        }

        [Test]
        public void CompleteAllObjectives_CompletesQuest()
        {
            var quest = CreateTestQuest();
            quest.Start();
            quest.Progress("kill", 5);
            Assert.IsTrue(quest.IsComplete);
            Assert.AreEqual(QuestStatus.Completed, quest.Status);
        }

        [Test]
        public void MultipleObjectives_AllMustComplete()
        {
            var quest = new QuestInstance("dungeon", new QuestDefinition
            {
                Id = "dungeon",
                Title = "Clear the Dungeon",
                Objectives = new[] {
                    new QuestObjective { Id = "kill", Description = "Kill boss", RequiredCount = 1 },
                    new QuestObjective { Id = "loot", Description = "Find treasure", RequiredCount = 1 }
                }
            });
            quest.Start();
            quest.Progress("kill", 1);
            Assert.IsFalse(quest.IsComplete);
            quest.Progress("loot", 1);
            Assert.IsTrue(quest.IsComplete);
        }

        [Test]
        public void ProgressBeforeStart_DoesNothing()
        {
            var quest = CreateTestQuest();
            quest.Progress("kill", 5);
            Assert.AreEqual(QuestStatus.NotStarted, quest.Status);
            Assert.AreEqual(0, quest.GetObjectiveProgress("kill"));
        }

        private QuestInstance CreateTestQuest()
        {
            return new QuestInstance("test", new QuestDefinition
            {
                Id = "test",
                Title = "Test Quest",
                Objectives = new[] {
                    new QuestObjective { Id = "kill", Description = "Kill 5 goblins", RequiredCount = 5 }
                }
            });
        }
    }
}
```

- [ ] **Step 2: Run tests — expect failure**

- [ ] **Step 3: Implement QuestDefinition and QuestInstance**

```csharp
// Assets/Scripts/ECS/Data/QuestDefinition.cs
using System.Collections.Generic;

namespace ForeverEngine.ECS.Data
{
    public enum QuestStatus { NotStarted, Active, Completed, Failed }

    public struct QuestObjective
    {
        public string Id;
        public string Description;
        public int RequiredCount;
    }

    public class QuestDefinition
    {
        public string Id;
        public string Title;
        public string Description;
        public QuestObjective[] Objectives;
        public int GoldReward;
        public int XPReward;
    }

    public class QuestInstance
    {
        public string QuestId { get; }
        public QuestDefinition Definition { get; }
        public QuestStatus Status { get; private set; }
        public bool IsComplete => Status == QuestStatus.Completed;

        private Dictionary<string, int> _progress = new();

        public QuestInstance(string questId, QuestDefinition definition)
        {
            QuestId = questId;
            Definition = definition;
            Status = QuestStatus.NotStarted;
        }

        public void Start()
        {
            if (Status == QuestStatus.NotStarted)
                Status = QuestStatus.Active;
        }

        public void Progress(string objectiveId, int amount)
        {
            if (Status != QuestStatus.Active) return;

            _progress.TryGetValue(objectiveId, out int current);
            _progress[objectiveId] = current + amount;

            CheckCompletion();
        }

        public int GetObjectiveProgress(string objectiveId)
        {
            return _progress.TryGetValue(objectiveId, out int val) ? val : 0;
        }

        private void CheckCompletion()
        {
            if (Definition.Objectives == null) return;

            foreach (var obj in Definition.Objectives)
            {
                if (GetObjectiveProgress(obj.Id) < obj.RequiredCount)
                    return;
            }
            Status = QuestStatus.Completed;
        }

        public void Fail() => Status = QuestStatus.Failed;
    }
}
```

- [ ] **Step 4: Implement QuestComponent**

```csharp
// Assets/Scripts/ECS/Components/QuestComponent.cs
using Unity.Entities;
using Unity.Collections;

namespace ForeverEngine.ECS.Components
{
    public struct QuestComponent : IComponentData
    {
        public FixedString64Bytes ActiveQuestId;
    }
}
```

- [ ] **Step 5: Implement QuestSystem**

```csharp
// Assets/Scripts/ECS/Systems/QuestSystem.cs
using Unity.Entities;
using System.Collections.Generic;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.ECS.Systems
{
    public partial class QuestSystem : SystemBase
    {
        private Dictionary<string, QuestDefinition> _definitions = new();
        private Dictionary<string, QuestInstance> _activeQuests = new();
        private List<QuestInstance> _completedQuests = new();

        public static QuestSystem Instance { get; private set; }

        protected override void OnCreate() => Instance = this;

        public void RegisterQuest(QuestDefinition def) => _definitions[def.Id] = def;

        public QuestInstance StartQuest(string questId)
        {
            if (!_definitions.TryGetValue(questId, out var def)) return null;
            var instance = new QuestInstance(questId, def);
            instance.Start();
            _activeQuests[questId] = instance;
            return instance;
        }

        public void ProgressQuest(string questId, string objectiveId, int amount = 1)
        {
            if (!_activeQuests.TryGetValue(questId, out var quest)) return;
            quest.Progress(objectiveId, amount);
            if (quest.IsComplete)
            {
                _activeQuests.Remove(questId);
                _completedQuests.Add(quest);
            }
        }

        public QuestInstance GetQuest(string questId)
        {
            return _activeQuests.TryGetValue(questId, out var q) ? q : null;
        }

        public List<QuestInstance> GetActiveQuests() => new(_activeQuests.Values);
        public List<QuestInstance> GetCompletedQuests() => new(_completedQuests);

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            _activeQuests.Clear();
            _completedQuests.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
```

- [ ] **Step 6: Implement QuestLogUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/QuestLogUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class QuestLogUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _questPanel;
        private ScrollView _questList;
        private bool _visible;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _questPanel = root.Q<VisualElement>("quest-panel");
            _questList = root.Q<ScrollView>("quest-list");
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_questPanel != null)
                _questPanel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) Refresh();
        }

        public void Refresh()
        {
            if (_questList == null) return;
            _questList.Clear();

            var qs = QuestSystem.Instance;
            if (qs == null) return;

            foreach (var quest in qs.GetActiveQuests())
            {
                var header = new Label($"[Active] {quest.Definition.Title}");
                header.style.color = new Color(1f, 0.84f, 0f);
                header.style.fontSize = 14;
                _questList.Add(header);

                foreach (var obj in quest.Definition.Objectives)
                {
                    int progress = quest.GetObjectiveProgress(obj.Id);
                    var line = new Label($"  - {obj.Description}: {progress}/{obj.RequiredCount}");
                    line.style.fontSize = 12;
                    line.style.color = progress >= obj.RequiredCount
                        ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
                    _questList.Add(line);
                }
            }

            foreach (var quest in qs.GetCompletedQuests())
            {
                var header = new Label($"[Done] {quest.Definition.Title}");
                header.style.color = new Color(0.5f, 0.5f, 0.5f);
                header.style.fontSize = 14;
                _questList.Add(header);
            }
        }
    }
}
```

- [ ] **Step 7: Run tests**

Expected: 6/6 PASS

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/ECS/Data/QuestDefinition.cs Assets/Scripts/ECS/Components/QuestComponent.cs Assets/Scripts/ECS/Systems/QuestSystem.cs Assets/Scripts/MonoBehaviour/UI/QuestLogUI.cs Assets/Tests/EditMode/QuestTests.cs
git commit -m "feat: Quest system with multi-objective tracking and quest log UI — 6 tests"
```

---

## Task 4: Save/Load System

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/SaveLoad/SaveData.cs`
- Create: `Assets/Scripts/MonoBehaviour/SaveLoad/SaveManager.cs`
- Create: `Assets/Tests/EditMode/SaveLoadTests.cs`

- [ ] **Step 1: Write save/load tests**

```csharp
// Assets/Tests/EditMode/SaveLoadTests.cs
using NUnit.Framework;
using ForeverEngine.MonoBehaviour.SaveLoad;

namespace ForeverEngine.Tests
{
    public class SaveLoadTests
    {
        [Test]
        public void SaveData_SerializesToJson()
        {
            var data = new SaveData();
            data.MapPath = "test/map_data.json";
            data.PlayerX = 5;
            data.PlayerY = 3;
            data.PlayerZ = 0;
            data.PlayerHP = 15;
            data.PlayerMaxHP = 20;
            data.Gold = 100;

            string json = SaveData.ToJson(data);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("\"PlayerX\":5"));
        }

        [Test]
        public void SaveData_DeserializesFromJson()
        {
            var original = new SaveData { MapPath = "test.json", PlayerX = 7, PlayerY = 2, PlayerHP = 10, PlayerMaxHP = 20, Gold = 50 };
            string json = SaveData.ToJson(original);
            var loaded = SaveData.FromJson(json);

            Assert.AreEqual(7, loaded.PlayerX);
            Assert.AreEqual(2, loaded.PlayerY);
            Assert.AreEqual(10, loaded.PlayerHP);
            Assert.AreEqual(50, loaded.Gold);
            Assert.AreEqual("test.json", loaded.MapPath);
        }

        [Test]
        public void SaveData_RoundTrip_Preserves()
        {
            var data = new SaveData
            {
                MapPath = "dungeon/map.json",
                PlayerX = 12, PlayerY = 8, PlayerZ = -1,
                PlayerHP = 5, PlayerMaxHP = 30,
                Gold = 999,
                ActiveQuests = new[] { "kill_goblins", "find_artifact" },
                CompletedQuests = new[] { "tutorial" }
            };

            string json = SaveData.ToJson(data);
            var restored = SaveData.FromJson(json);

            Assert.AreEqual(data.MapPath, restored.MapPath);
            Assert.AreEqual(data.PlayerX, restored.PlayerX);
            Assert.AreEqual(data.PlayerZ, restored.PlayerZ);
            Assert.AreEqual(data.Gold, restored.Gold);
            Assert.AreEqual(2, restored.ActiveQuests.Length);
            Assert.AreEqual("find_artifact", restored.ActiveQuests[1]);
        }
    }
}
```

- [ ] **Step 2: Implement SaveData**

```csharp
// Assets/Scripts/MonoBehaviour/SaveLoad/SaveData.cs
using UnityEngine;

namespace ForeverEngine.MonoBehaviour.SaveLoad
{
    [System.Serializable]
    public class SaveData
    {
        public string MapPath;
        public int PlayerX, PlayerY, PlayerZ;
        public int PlayerHP, PlayerMaxHP;
        public int Gold;
        public string[] ActiveQuests;
        public string[] CompletedQuests;
        public string SaveTimestamp;

        public static string ToJson(SaveData data)
        {
            data.SaveTimestamp = System.DateTime.UtcNow.ToString("o");
            return JsonUtility.ToJson(data, true);
        }

        public static SaveData FromJson(string json)
        {
            return JsonUtility.FromJson<SaveData>(json);
        }
    }
}
```

- [ ] **Step 3: Implement SaveManager**

```csharp
// Assets/Scripts/MonoBehaviour/SaveLoad/SaveManager.cs
using UnityEngine;
using Unity.Entities;
using System.IO;
using ForeverEngine.ECS.Components;
using ForeverEngine.ECS.Data;

namespace ForeverEngine.MonoBehaviour.SaveLoad
{
    public class SaveManager : UnityEngine.MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");

        private void Awake()
        {
            Instance = this;
            Directory.CreateDirectory(SaveDirectory);
        }

        public void Save(string slotName = "quicksave")
        {
            var data = CaptureState();
            string json = SaveData.ToJson(data);
            string path = Path.Combine(SaveDirectory, $"{slotName}.json");
            File.WriteAllText(path, json);
            Debug.Log($"[SaveManager] Saved to {path}");
        }

        public bool Load(string slotName = "quicksave")
        {
            string path = Path.Combine(SaveDirectory, $"{slotName}.json");
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            var data = SaveData.FromJson(json);
            RestoreState(data);
            Debug.Log($"[SaveManager] Loaded from {path}");
            return true;
        }

        public string[] GetSaveSlots()
        {
            var files = Directory.GetFiles(SaveDirectory, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                names[i] = Path.GetFileNameWithoutExtension(files[i]);
            return names;
        }

        private SaveData CaptureState()
        {
            var data = new SaveData();
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return data;

            var playerQuery = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent), typeof(StatsComponent));
            if (playerQuery.CalculateEntityCount() > 0)
            {
                var pos = playerQuery.GetSingleton<PositionComponent>();
                var stats = playerQuery.GetSingleton<StatsComponent>();
                data.PlayerX = pos.X;
                data.PlayerY = pos.Y;
                data.PlayerZ = pos.Z;
                data.PlayerHP = stats.HP;
                data.PlayerMaxHP = stats.MaxHP;
            }

            var store = MapDataStore.Instance;
            if (store != null) data.Gold = 0; // TODO: wire gold from InventorySystem

            return data;
        }

        private void RestoreState(SaveData data)
        {
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return;

            var playerQuery = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(PositionComponent), typeof(StatsComponent));
            if (playerQuery.CalculateEntityCount() > 0)
            {
                var entities = playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                var entity = entities[0];
                entities.Dispose();

                em.Value.SetComponentData(entity, new PositionComponent { X = data.PlayerX, Y = data.PlayerY, Z = data.PlayerZ });
                var stats = em.Value.GetComponentData<StatsComponent>(entity);
                stats.HP = data.PlayerHP;
                stats.MaxHP = data.PlayerMaxHP;
                em.Value.SetComponentData(entity, stats);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests**

Expected: 3/3 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/SaveLoad/ Assets/Tests/EditMode/SaveLoadTests.cs
git commit -m "feat: Save/Load system with JSON serialization and quicksave — 3 tests"
```

---

## Task 5: Menu System

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/UI/MainMenuUI.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/PauseMenuUI.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/OptionsUI.cs`
- Create: `Assets/Scripts/MonoBehaviour/UI/InventoryUI.cs`

No tests — these are pure UI wiring (UI Toolkit panels calling existing systems).

- [ ] **Step 1: Implement MainMenuUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/MainMenuUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.MonoBehaviour.SaveLoad;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class MainMenuUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _menuPanel;
        private Button _newGameBtn;
        private Button _loadBtn;
        private Button _optionsBtn;
        private Button _quitBtn;

        public System.Action OnNewGame;
        public System.Action OnLoadGame;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _menuPanel = root.Q<VisualElement>("main-menu-panel");
            _newGameBtn = root.Q<Button>("btn-new-game");
            _loadBtn = root.Q<Button>("btn-load");
            _optionsBtn = root.Q<Button>("btn-options");
            _quitBtn = root.Q<Button>("btn-quit");

            _newGameBtn?.RegisterCallback<ClickEvent>(_ => OnNewGame?.Invoke());
            _loadBtn?.RegisterCallback<ClickEvent>(_ => OnLoadGame?.Invoke());
            _quitBtn?.RegisterCallback<ClickEvent>(_ => Application.Quit());
        }

        public void Show() { if (_menuPanel != null) _menuPanel.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_menuPanel != null) _menuPanel.style.display = DisplayStyle.None; }
    }
}
```

- [ ] **Step 2: Implement PauseMenuUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/PauseMenuUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.MonoBehaviour.SaveLoad;
using ForeverEngine.ECS.Systems;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class PauseMenuUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _pausePanel;
        private bool _paused;

        public bool IsPaused => _paused;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _pausePanel = root.Q<VisualElement>("pause-panel");

            root.Q<Button>("btn-resume")?.RegisterCallback<ClickEvent>(_ => Resume());
            root.Q<Button>("btn-save")?.RegisterCallback<ClickEvent>(_ =>
            {
                SaveManager.Instance?.Save();
            });
            root.Q<Button>("btn-quit-menu")?.RegisterCallback<ClickEvent>(_ =>
            {
                Resume();
                // Return to main menu would go here
            });
        }

        public void TogglePause()
        {
            if (_paused) Resume(); else Pause();
        }

        private void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;
            if (_pausePanel != null) _pausePanel.style.display = DisplayStyle.Flex;
        }

        private void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;
            if (_pausePanel != null) _pausePanel.style.display = DisplayStyle.None;
        }
    }
}
```

- [ ] **Step 3: Implement OptionsUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/OptionsUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class OptionsUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _optionsPanel;
        private Slider _masterVolume;
        private Slider _sfxVolume;
        private Slider _musicVolume;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _optionsPanel = root.Q<VisualElement>("options-panel");
            _masterVolume = root.Q<Slider>("slider-master-vol");
            _sfxVolume = root.Q<Slider>("slider-sfx-vol");
            _musicVolume = root.Q<Slider>("slider-music-vol");

            _masterVolume?.RegisterValueChangedCallback(e => AudioListener.volume = e.newValue);

            root.Q<Button>("btn-options-back")?.RegisterCallback<ClickEvent>(_ => Hide());
        }

        public void Show() { if (_optionsPanel != null) _optionsPanel.style.display = DisplayStyle.Flex; }
        public void Hide() { if (_optionsPanel != null) _optionsPanel.style.display = DisplayStyle.None; }
    }
}
```

- [ ] **Step 4: Implement InventoryUI**

```csharp
// Assets/Scripts/MonoBehaviour/UI/InventoryUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.ECS.Systems;
using ForeverEngine.ECS.Components;
using Unity.Entities;

namespace ForeverEngine.MonoBehaviour.UI
{
    public class InventoryUI : UnityEngine.MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _inventoryPanel;
        private VisualElement _slotGrid;
        private Label _goldLabel;
        private bool _visible;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            _inventoryPanel = root.Q<VisualElement>("inventory-panel");
            _slotGrid = root.Q<VisualElement>("inventory-grid");
            _goldLabel = root.Q<Label>("gold-label");
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_inventoryPanel != null)
                _inventoryPanel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) Refresh();
        }

        public void Refresh()
        {
            if (_slotGrid == null) return;
            _slotGrid.Clear();

            var invSys = InventorySystem.Instance;
            if (invSys == null) return;

            // Find player entity
            var em = World.DefaultGameObjectInjectionWorld?.EntityManager;
            if (em == null) return;

            var query = em.Value.CreateEntityQuery(typeof(PlayerTag), typeof(InventoryComponent));
            if (query.CalculateEntityCount() == 0) return;

            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            var inv = invSys.GetInventory(entities[0]);
            entities.Dispose();

            if (inv == null) return;

            if (_goldLabel != null) _goldLabel.text = $"Gold: {inv.Gold}";

            foreach (var slot in inv.GetAllSlots())
            {
                var slotElement = new VisualElement();
                slotElement.style.width = 48;
                slotElement.style.height = 48;
                slotElement.style.borderBottomWidth = slotElement.style.borderTopWidth =
                    slotElement.style.borderLeftWidth = slotElement.style.borderRightWidth = 1;
                slotElement.style.borderBottomColor = slotElement.style.borderTopColor =
                    slotElement.style.borderLeftColor = slotElement.style.borderRightColor =
                    slot.Equipped ? new Color(1f, 0.84f, 0f) : new Color(0.4f, 0.4f, 0.4f);
                slotElement.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);

                var label = new Label($"#{slot.ItemId}\nx{slot.StackCount}");
                label.style.fontSize = 10;
                label.style.color = Color.white;
                slotElement.Add(label);

                _slotGrid.Add(slotElement);
            }
        }
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/UI/MainMenuUI.cs Assets/Scripts/MonoBehaviour/UI/PauseMenuUI.cs Assets/Scripts/MonoBehaviour/UI/OptionsUI.cs Assets/Scripts/MonoBehaviour/UI/InventoryUI.cs
git commit -m "feat: Menu system — main menu, pause, options, inventory UI panels"
```

---

## Task 6: Animation System

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/Animation/AnimationData.cs`
- Create: `Assets/Scripts/MonoBehaviour/Animation/SpriteAnimator.cs`
- Create: `Assets/Tests/EditMode/AnimationTests.cs`

- [ ] **Step 1: Write animation tests**

```csharp
// Assets/Tests/EditMode/AnimationTests.cs
using NUnit.Framework;
using ForeverEngine.MonoBehaviour.Animation;

namespace ForeverEngine.Tests
{
    public class AnimationTests
    {
        [Test]
        public void NewAnimation_StartsAtFrame0()
        {
            var clip = new AnimClip("idle", 4, 0.25f, true);
            var state = new AnimState(clip);
            Assert.AreEqual(0, state.CurrentFrame);
        }

        [Test]
        public void Advance_ProgressesFrames()
        {
            var clip = new AnimClip("walk", 4, 0.25f, true);
            var state = new AnimState(clip);
            state.Advance(0.25f);
            Assert.AreEqual(1, state.CurrentFrame);
            state.Advance(0.25f);
            Assert.AreEqual(2, state.CurrentFrame);
        }

        [Test]
        public void Loop_WrapsAround()
        {
            var clip = new AnimClip("walk", 3, 0.1f, true);
            var state = new AnimState(clip);
            state.Advance(0.1f); // frame 1
            state.Advance(0.1f); // frame 2
            state.Advance(0.1f); // frame 0 (wrapped)
            Assert.AreEqual(0, state.CurrentFrame);
            Assert.IsFalse(state.Finished);
        }

        [Test]
        public void NoLoop_StopsAtEnd()
        {
            var clip = new AnimClip("attack", 3, 0.1f, false);
            var state = new AnimState(clip);
            state.Advance(0.1f); // 1
            state.Advance(0.1f); // 2
            state.Advance(0.1f); // stays 2
            Assert.AreEqual(2, state.CurrentFrame);
            Assert.IsTrue(state.Finished);
        }

        [Test]
        public void SpeedMultiplier_AffectsRate()
        {
            var clip = new AnimClip("fast", 4, 0.5f, true);
            var state = new AnimState(clip) { SpeedMultiplier = 2f };
            state.Advance(0.25f); // 0.25 * 2 = 0.5 → next frame
            Assert.AreEqual(1, state.CurrentFrame);
        }
    }
}
```

- [ ] **Step 2: Implement AnimationData**

```csharp
// Assets/Scripts/MonoBehaviour/Animation/AnimationData.cs
namespace ForeverEngine.MonoBehaviour.Animation
{
    public class AnimClip
    {
        public string Name;
        public int FrameCount;
        public float FrameDuration; // Seconds per frame
        public bool Loop;

        public AnimClip(string name, int frameCount, float frameDuration, bool loop)
        {
            Name = name;
            FrameCount = frameCount;
            FrameDuration = frameDuration;
            Loop = loop;
        }
    }

    public class AnimState
    {
        public AnimClip Clip { get; }
        public int CurrentFrame { get; private set; }
        public bool Finished { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;

        private float _timer;

        public AnimState(AnimClip clip)
        {
            Clip = clip;
        }

        public void Advance(float deltaTime)
        {
            if (Finished) return;

            _timer += deltaTime * SpeedMultiplier;

            while (_timer >= Clip.FrameDuration)
            {
                _timer -= Clip.FrameDuration;
                CurrentFrame++;

                if (CurrentFrame >= Clip.FrameCount)
                {
                    if (Clip.Loop)
                        CurrentFrame = 0;
                    else
                    {
                        CurrentFrame = Clip.FrameCount - 1;
                        Finished = true;
                        return;
                    }
                }
            }
        }

        public void Reset()
        {
            CurrentFrame = 0;
            _timer = 0f;
            Finished = false;
        }
    }
}
```

- [ ] **Step 3: Implement SpriteAnimator**

```csharp
// Assets/Scripts/MonoBehaviour/Animation/SpriteAnimator.cs
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.Animation
{
    public class SpriteAnimator : UnityEngine.MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite[] _spriteSheet;
        [SerializeField] private int _framesPerRow = 4;

        private Dictionary<string, AnimClip> _clips = new();
        private AnimState _current;

        public void RegisterClip(AnimClip clip) => _clips[clip.Name] = clip;

        public void Play(string clipName)
        {
            if (!_clips.TryGetValue(clipName, out var clip)) return;
            if (_current?.Clip.Name == clipName && !_current.Finished) return;
            _current = new AnimState(clip);
        }

        private void Update()
        {
            if (_current == null) return;
            _current.Advance(Time.deltaTime);

            // Update sprite from sheet
            if (_spriteSheet != null && _current.CurrentFrame < _spriteSheet.Length)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.sprite = _spriteSheet[_current.CurrentFrame];
            }
        }

        public string CurrentClipName => _current?.Clip.Name;
        public int CurrentFrame => _current?.CurrentFrame ?? 0;
        public bool IsFinished => _current?.Finished ?? true;
    }
}
```

- [ ] **Step 4: Run tests**

Expected: 5/5 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/Animation/ Assets/Tests/EditMode/AnimationTests.cs
git commit -m "feat: Animation system with frame clips, looping, speed control — 5 tests"
```

---

## Task 7: Audio Manager

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/Audio/AudioConfig.cs`
- Create: `Assets/Scripts/MonoBehaviour/Audio/AudioManager.cs`

No tests — audio is Unity-native and requires runtime AudioSource.

- [ ] **Step 1: Implement AudioConfig**

```csharp
// Assets/Scripts/MonoBehaviour/Audio/AudioConfig.cs
using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Audio
{
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Forever Engine/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        [Header("Volume")]
        [Range(0, 1)] public float MasterVolume = 1f;
        [Range(0, 1)] public float SFXVolume = 0.8f;
        [Range(0, 1)] public float MusicVolume = 0.5f;
        [Range(0, 1)] public float AmbientVolume = 0.3f;

        [Header("Clips")]
        public AudioClip[] HitSounds;
        public AudioClip[] MissSounds;
        public AudioClip[] FootstepSounds;
        public AudioClip[] DeathSounds;
        public AudioClip[] UIClickSounds;
        public AudioClip DoorOpenSound;
        public AudioClip LevelUpSound;
        public AudioClip QuestCompleteSound;

        [Header("Music")]
        public AudioClip ExplorationMusic;
        public AudioClip CombatMusic;
        public AudioClip MenuMusic;

        [Header("Ambient")]
        public AudioClip DungeonAmbient;
        public AudioClip CaveAmbient;
        public AudioClip ForestAmbient;
    }
}
```

- [ ] **Step 2: Implement AudioManager**

```csharp
// Assets/Scripts/MonoBehaviour/Audio/AudioManager.cs
using UnityEngine;

namespace ForeverEngine.MonoBehaviour.Audio
{
    public class AudioManager : UnityEngine.MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioConfig _config;
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _ambientSource;

        private void Awake()
        {
            Instance = this;

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
            }
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
            }
            if (_ambientSource == null)
            {
                _ambientSource = gameObject.AddComponent<AudioSource>();
                _ambientSource.loop = true;
                _ambientSource.playOnAwake = false;
            }
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null || _config == null) return;
            _sfxSource.volume = _config.MasterVolume * _config.SFXVolume;
            _sfxSource.PlayOneShot(clip);
        }

        public void PlayRandomSFX(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySFX(clips[Random.Range(0, clips.Length)]);
        }

        public void PlayHit() => PlayRandomSFX(_config?.HitSounds);
        public void PlayMiss() => PlayRandomSFX(_config?.MissSounds);
        public void PlayFootstep() => PlayRandomSFX(_config?.FootstepSounds);
        public void PlayDeath() => PlayRandomSFX(_config?.DeathSounds);
        public void PlayUIClick() => PlayRandomSFX(_config?.UIClickSounds);

        public void PlayMusic(AudioClip clip)
        {
            if (_musicSource == null || _config == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            _musicSource.clip = clip;
            _musicSource.volume = _config.MasterVolume * _config.MusicVolume;
            _musicSource.Play();
        }

        public void PlayExplorationMusic() => PlayMusic(_config?.ExplorationMusic);
        public void PlayCombatMusic() => PlayMusic(_config?.CombatMusic);

        public void PlayAmbient(AudioClip clip)
        {
            if (_ambientSource == null || _config == null) return;
            if (_ambientSource.clip == clip && _ambientSource.isPlaying) return;
            _ambientSource.clip = clip;
            _ambientSource.volume = _config.MasterVolume * _config.AmbientVolume;
            _ambientSource.Play();
        }

        public void StopMusic() => _musicSource?.Stop();
        public void StopAmbient() => _ambientSource?.Stop();

        public void SetMasterVolume(float vol)
        {
            if (_config != null) _config.MasterVolume = Mathf.Clamp01(vol);
            AudioListener.volume = vol;
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/Audio/
git commit -m "feat: AudioManager with SFX, music, ambient channels and AudioConfig"
```

---

## Task 8: VFX Manager

**Files:**
- Create: `Assets/Scripts/MonoBehaviour/VFX/VFXConfig.cs`
- Create: `Assets/Scripts/MonoBehaviour/VFX/VFXManager.cs`

- [ ] **Step 1: Implement VFXConfig**

```csharp
// Assets/Scripts/MonoBehaviour/VFX/VFXConfig.cs
using UnityEngine;

namespace ForeverEngine.MonoBehaviour.VFX
{
    [CreateAssetMenu(fileName = "VFXConfig", menuName = "Forever Engine/VFX Config")]
    public class VFXConfig : ScriptableObject
    {
        [Header("Combat")]
        public GameObject HitEffect;
        public GameObject CriticalHitEffect;
        public GameObject MissEffect;
        public GameObject DeathEffect;
        public GameObject HealEffect;

        [Header("Environment")]
        public GameObject TorchFlame;
        public GameObject MagicGlow;
        public GameObject DustCloud;
        public GameObject WaterSplash;

        [Header("UI")]
        public GameObject DamageNumberPrefab;
        public GameObject LevelUpEffect;
    }
}
```

- [ ] **Step 2: Implement VFXManager**

```csharp
// Assets/Scripts/MonoBehaviour/VFX/VFXManager.cs
using UnityEngine;
using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.VFX
{
    public class VFXManager : UnityEngine.MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [SerializeField] private VFXConfig _config;
        [SerializeField] private int _poolSize = 20;

        private Dictionary<GameObject, Queue<GameObject>> _pools = new();

        private void Awake() => Instance = this;

        public void SpawnAt(GameObject prefab, Vector3 position, float duration = 2f)
        {
            if (prefab == null) return;

            var instance = GetFromPool(prefab);
            instance.transform.position = position;
            instance.SetActive(true);

            StartCoroutine(ReturnAfter(prefab, instance, duration));
        }

        public void PlayHit(Vector3 pos) => SpawnAt(_config?.HitEffect, pos);
        public void PlayCritHit(Vector3 pos) => SpawnAt(_config?.CriticalHitEffect, pos);
        public void PlayMiss(Vector3 pos) => SpawnAt(_config?.MissEffect, pos, 1f);
        public void PlayDeath(Vector3 pos) => SpawnAt(_config?.DeathEffect, pos, 3f);
        public void PlayHeal(Vector3 pos) => SpawnAt(_config?.HealEffect, pos);

        public void ShowDamageNumber(Vector3 pos, int damage, bool critical = false)
        {
            if (_config?.DamageNumberPrefab == null) return;
            var instance = GetFromPool(_config.DamageNumberPrefab);
            instance.transform.position = pos + Vector3.up * 0.5f;
            instance.SetActive(true);

            var label = instance.GetComponentInChildren<TextMesh>();
            if (label != null)
            {
                label.text = damage.ToString();
                label.color = critical ? Color.yellow : Color.white;
            }

            StartCoroutine(FloatAndReturn(_config.DamageNumberPrefab, instance));
        }

        private GameObject GetFromPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<GameObject>();
                _pools[prefab] = pool;
            }

            if (pool.Count > 0)
            {
                var pooled = pool.Dequeue();
                if (pooled != null) return pooled;
            }

            return Instantiate(prefab, transform);
        }

        private void ReturnToPool(GameObject prefab, GameObject instance)
        {
            instance.SetActive(false);
            if (_pools.TryGetValue(prefab, out var pool))
                pool.Enqueue(instance);
        }

        private System.Collections.IEnumerator ReturnAfter(GameObject prefab, GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(prefab, instance);
        }

        private System.Collections.IEnumerator FloatAndReturn(GameObject prefab, GameObject instance)
        {
            float elapsed = 0f;
            Vector3 start = instance.transform.position;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                instance.transform.position = start + Vector3.up * elapsed * 0.5f;
                yield return null;
            }
            ReturnToPool(prefab, instance);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/MonoBehaviour/VFX/
git commit -m "feat: VFX Manager with object pooling and damage numbers"
```

---

## Summary

| Task | System | New Files | Tests |
|------|--------|-----------|-------|
| 1 | Inventory | 5 (ItemInstance, Inventory, InventoryComponent, InventorySystem, tests) | 11 |
| 2 | Dialogue | 5 (DialogueData, DialogueManager, DialogueComponent, DialogueUI, tests) | 5 |
| 3 | Quest | 5 (QuestDefinition, QuestComponent, QuestSystem, QuestLogUI, tests) | 6 |
| 4 | Save/Load | 3 (SaveData, SaveManager, tests) | 3 |
| 5 | Menus | 4 (MainMenu, Pause, Options, InventoryUI) | 0 |
| 6 | Animation | 3 (AnimationData, SpriteAnimator, tests) | 5 |
| 7 | Audio | 2 (AudioConfig, AudioManager) | 0 |
| 8 | VFX | 2 (VFXConfig, VFXManager) | 0 |
| **Total** | **8 systems** | **29 new files** | **30 tests** |

After all 8 tasks: Inventory with stacking/equip/gold, branching dialogue with conditions, multi-objective quests with journal, quicksave/load, main menu + pause + options + inventory UI, frame-based sprite animation, 3-channel audio (SFX/music/ambient), pooled VFX with damage numbers.
