using System.Collections.Generic;
using ForeverEngine.MonoBehaviour.Dialogue;

namespace ForeverEngine.Demo.NPCs
{
    public static class NPCManager
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Merchant in Ashwick Ruins
            var merchantTree = new DialogueTree();
            merchantTree.AddNode(new DialogueNode { Id = "root", Speaker = "Merchant Vara", Text = "Welcome to what's left of Ashwick. I trade in supplies... if you have gold.",
                Choices = new[] {
                    new DialogueChoice { Text = "What happened here?", NextNodeId = "lore" },
                    new DialogueChoice { Text = "I need supplies. [Shop]", NextNodeId = "shop" },
                    new DialogueChoice { Text = "Any work available?", NextNodeId = "quest", ConditionTag = "quest_1_complete" },
                    new DialogueChoice { Text = "Goodbye.", NextNodeId = "" }
                }});
            merchantTree.AddNode(new DialogueNode { Id = "lore", Speaker = "Merchant Vara", Text = "The Rot came from the castle to the northeast. Undead, mutants... everything twisted. Most fled. I stayed — someone has to keep trade alive.",
                Choices = new[] { new DialogueChoice { Text = "I see...", NextNodeId = "root" } }});
            merchantTree.AddNode(new DialogueNode { Id = "shop", Speaker = "Merchant Vara", Text = "Take a look at what I've got." });
            merchantTree.AddNode(new DialogueNode { Id = "quest", Speaker = "Merchant Vara", Text = "The Hollow — a dungeon to the southeast. Something stirs down there. Clear it out and I'll make it worth your while. I'll also unlock my better stock.",
                Choices = new[] {
                    new DialogueChoice { Text = "I'll handle it.", NextNodeId = "quest_accept" },
                    new DialogueChoice { Text = "Not yet.", NextNodeId = "root" }
                }});
            merchantTree.AddNode(new DialogueNode { Id = "quest_accept", Speaker = "Merchant Vara", Text = "Be careful down there. The dead don't stay dead in this kingdom." });

            // Dwarf Chief in Ironhold
            var dwarfTree = new DialogueTree();
            dwarfTree.AddNode(new DialogueNode { Id = "root", Speaker = "Chief Borin", Text = "Hmm. A surface wanderer. Ironhold doesn't see many visitors these days.",
                Choices = new[] {
                    new DialogueChoice { Text = "I bring a letter from Ashwick.", NextNodeId = "letter", ConditionTag = "has_letter" },
                    new DialogueChoice { Text = "Can I rest here?", NextNodeId = "rest" },
                    new DialogueChoice { Text = "Tell me about the curse.", NextNodeId = "curse" },
                    new DialogueChoice { Text = "Farewell.", NextNodeId = "" }
                }});
            dwarfTree.AddNode(new DialogueNode { Id = "letter", Speaker = "Chief Borin", Text = "Vara sent you? She's bold, that one. The letter speaks of an alliance against the Rot. Very well — take this dwarven steel armor. You'll need it where you're going.",
                Choices = new[] { new DialogueChoice { Text = "Thank you, Chief.", NextNodeId = "alliance" } }});
            dwarfTree.AddNode(new DialogueNode { Id = "alliance", Speaker = "Chief Borin", Text = "The source of the Rot is the Throne of Rot — the old castle far northeast. The king himself became the vessel. End him, and perhaps the land can heal." });
            dwarfTree.AddNode(new DialogueNode { Id = "rest", Speaker = "Chief Borin", Text = "Aye, rest here. Ironhold is safe — the deep stone keeps the Rot out." });
            dwarfTree.AddNode(new DialogueNode { Id = "curse", Speaker = "Chief Borin", Text = "It started with the old king's obsession with immortality. He found something in the deep places — something that should have stayed buried. Now the whole kingdom rots from the inside." });

            var mgr = DialogueManager.Instance;
            if (mgr != null)
            {
                mgr.RegisterTree("merchant", merchantTree);
                mgr.RegisterTree("dwarf_chief", dwarfTree);
            }
        }
    }
}
