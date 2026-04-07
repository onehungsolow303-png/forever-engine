using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Tests.Demo
{
    /// <summary>
    /// EditMode tests for DialoguePanel. Two failure modes these tests must catch:
    ///
    /// 1. UXML asset binding regression — if the .uxml is moved out of Resources/
    ///    or a named element is renamed, the panel renders dead at runtime.
    /// 2. Show/RefreshHistory crash — if any code path in Show or its callees
    ///    throws an exception, the panel never appears in real gameplay.
    ///
    /// Critical: in EditMode, AddComponent does NOT auto-fire Awake (only PlayMode
    /// does), so we invoke it manually via reflection. Without that, Awake never
    /// runs, _history stays null, and RefreshHistory's null guard early-returns
    /// before reaching the buggy ScrollTo path. The original tests passed while
    /// the production bug was live for exactly this reason.
    /// </summary>
    public class DialoguePanelTests
    {
        private static (GameObject, DialoguePanel) MakeInitializedPanel()
        {
            var go = new GameObject("dlg-test");
            var panel = go.AddComponent<DialoguePanel>();
            // Force Awake — EditMode does not invoke it automatically.
            var awake = typeof(DialoguePanel).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(awake, "DialoguePanel must have an Awake method");
            awake.Invoke(panel, null);
            return (go, panel);
        }

        [Test]
        public void NewPanelStartsClosed()
        {
            var (go, panel) = MakeInitializedPanel();
            Assert.IsFalse(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowOpensPanel()
        {
            var (go, panel) = MakeInitializedPanel();
            panel.Show("camp_01", "innkeeper");
            Assert.IsTrue(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CloseHidesPanel()
        {
            var (go, panel) = MakeInitializedPanel();
            panel.Show("camp_01", "innkeeper");
            panel.Close();
            Assert.IsFalse(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowAcceptsNullNpcId()
        {
            var (go, panel) = MakeInitializedPanel();
            Assert.DoesNotThrow(() => panel.Show("camp_01", null));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowDoesNotThrowWithEmptyHistory()
        {
            // Regression guard for the ScrollTo(null) bug: Show clears history
            // and then calls RefreshHistory, which pre-bugfix passed null to
            // ScrollView.ScrollTo when the container had no children.
            var (go, panel) = MakeInitializedPanel();
            Assert.DoesNotThrow(() => panel.Show("camp_01", "innkeeper"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void UxmlAssetLoadsAndBindsAllNamedElements()
        {
            // Regression guard: if the .uxml is moved out of Resources/, or any
            // named element is renamed/retyped, the panel renders dead at runtime.
            var asset = Resources.Load<VisualTreeAsset>("DialoguePanel");
            Assert.IsNotNull(asset, "DialoguePanel.uxml must be loadable from Resources/");
            var tree = asset.Instantiate();
            Assert.IsNotNull(tree.Q<Label>("npc-name"), "missing Label 'npc-name'");
            Assert.IsNotNull(tree.Q<Label>("offline-banner"), "missing Label 'offline-banner'");
            Assert.IsNotNull(tree.Q<ScrollView>("history"), "missing ScrollView 'history'");
            Assert.IsNotNull(tree.Q<TextField>("input"), "missing TextField 'input'");
            Assert.IsNotNull(tree.Q<Button>("send"), "missing Button 'send'");
            Assert.IsNotNull(tree.Q<Button>("close"), "missing Button 'close'");
        }
    }
}
