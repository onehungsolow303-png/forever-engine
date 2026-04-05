using System.Collections.Generic;

namespace ForeverEngine.MonoBehaviour.Dialogue
{
    public struct DialogueChoice
    {
        public string Text;
        public string NextNodeId;
        public string ConditionTag;
        public string ActionTag;
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
        public void AddNode(DialogueNode node) { if (_nodes.Count == 0) RootId = node.Id; _nodes[node.Id] = node; }
        public DialogueNode GetNode(string id) => _nodes.TryGetValue(id, out var node) ? node : null;
        public DialogueNode Root => GetNode(RootId);
    }

    public class DialogueState
    {
        private readonly DialogueTree _tree;
        public DialogueNode CurrentNode { get; private set; }
        public bool IsFinished { get; private set; }

        public DialogueState(DialogueTree tree) { _tree = tree; CurrentNode = tree.Root; }

        public void Choose(int choiceIndex)
        {
            if (CurrentNode == null || CurrentNode.Choices == null) { IsFinished = true; return; }
            if (choiceIndex < 0 || choiceIndex >= CurrentNode.Choices.Length) return;
            var choice = CurrentNode.Choices[choiceIndex];
            if (string.IsNullOrEmpty(choice.NextNodeId)) { IsFinished = true; return; }
            CurrentNode = _tree.GetNode(choice.NextNodeId);
            if (CurrentNode == null) IsFinished = true;
        }

        public List<DialogueChoice> GetAvailableChoices(HashSet<string> activeTags)
        {
            var result = new List<DialogueChoice>();
            if (CurrentNode?.Choices == null) return result;
            foreach (var choice in CurrentNode.Choices)
                if (string.IsNullOrEmpty(choice.ConditionTag) || activeTags.Contains(choice.ConditionTag))
                    result.Add(choice);
            return result;
        }
    }
}
