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
        public void StartDialogue(string treeId) { if (_trees.TryGetValue(treeId, out var tree)) _activeState = new DialogueState(tree); }
        public void Choose(int index) { _activeState?.Choose(index); if (_activeState?.IsFinished == true) _activeState = null; }
        public void EndDialogue() => _activeState = null;
    }
}
