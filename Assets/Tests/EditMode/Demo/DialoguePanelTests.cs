using NUnit.Framework;
using UnityEngine;
using ForeverEngine.Demo.UI;

namespace ForeverEngine.Tests.Demo
{
    public class DialoguePanelTests
    {
        [Test]
        public void NewPanelStartsClosed()
        {
            var go = new GameObject("dlg-test");
            var panel = go.AddComponent<DialoguePanel>();
            Assert.IsFalse(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowOpensPanel()
        {
            var go = new GameObject("dlg-test");
            var panel = go.AddComponent<DialoguePanel>();
            panel.Show("camp_01", "innkeeper");
            Assert.IsTrue(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void CloseHidesPanel()
        {
            var go = new GameObject("dlg-test");
            var panel = go.AddComponent<DialoguePanel>();
            panel.Show("camp_01", "innkeeper");
            panel.Close();
            Assert.IsFalse(panel.IsOpen);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShowAcceptsNullNpcId()
        {
            var go = new GameObject("dlg-test");
            var panel = go.AddComponent<DialoguePanel>();
            Assert.DoesNotThrow(() => panel.Show("camp_01", null));
            Object.DestroyImmediate(go);
        }
    }
}
