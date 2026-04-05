using NUnit.Framework;
using ForeverEngine.MonoBehaviour.Dialogue;

namespace ForeverEngine.Tests
{
    public class DialogueTests
    {
        [Test] public void NewConversation_StartsAtRoot()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Hello traveler!", Choices = new[] { new DialogueChoice { Text = "Hello", NextNodeId = "greet" }, new DialogueChoice { Text = "Goodbye", NextNodeId = "" } } });
            tree.AddNode(new DialogueNode { Id = "greet", Text = "Welcome to the dungeon." });
            var state = new DialogueState(tree);
            Assert.AreEqual("root", state.CurrentNode.Id);
            Assert.AreEqual(2, state.CurrentNode.Choices.Length);
        }

        [Test] public void ChooseOption_AdvancesToNextNode()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Hello!", Choices = new[] { new DialogueChoice { Text = "Hi", NextNodeId = "response" } } });
            tree.AddNode(new DialogueNode { Id = "response", Text = "How can I help?" });
            var state = new DialogueState(tree);
            state.Choose(0);
            Assert.AreEqual("response", state.CurrentNode.Id);
        }

        [Test] public void EmptyNextNodeId_EndsConversation()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Bye!", Choices = new[] { new DialogueChoice { Text = "Leave", NextNodeId = "" } } });
            var state = new DialogueState(tree);
            state.Choose(0);
            Assert.IsTrue(state.IsFinished);
        }

        [Test] public void NoChoices_IsTerminal()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "The end." });
            var state = new DialogueState(tree);
            Assert.IsTrue(state.CurrentNode.IsTerminal);
        }

        [Test] public void ConditionalChoice_HiddenWhenConditionFalse()
        {
            var tree = new DialogueTree();
            tree.AddNode(new DialogueNode { Id = "root", Text = "Shop", Choices = new[] { new DialogueChoice { Text = "Buy sword", NextNodeId = "buy", ConditionTag = "has_gold_100" }, new DialogueChoice { Text = "Leave", NextNodeId = "" } } });
            var state = new DialogueState(tree);
            Assert.AreEqual(1, state.GetAvailableChoices(new System.Collections.Generic.HashSet<string>()).Count);
            Assert.AreEqual(2, state.GetAvailableChoices(new System.Collections.Generic.HashSet<string> { "has_gold_100" }).Count);
        }
    }
}
