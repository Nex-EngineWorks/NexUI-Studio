using System.Collections;
using emiteat.NexUI.Integrations.UGUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace emiteat.NexUI.Designer.Tests.PlayMode
{
    public sealed class DesignerRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator UguiSurface_ResolvesStableTaggedElement_AndControlsVisibility()
        {
            var root = new GameObject("RuntimeScreen", typeof(RectTransform));
            var button = new GameObject("RenamedButton", typeof(RectTransform));
            button.transform.SetParent(root.transform, false);
            var tag = button.AddComponent<NxUGuiBindingTag>();
            tag.stableId = "stable-button";
            tag.elementId = "confirmButton";
            tag.ownership = NexUIElementOwnership.DesignerOwned;

            var surface = new UGUISurface("runtime-smoke", root);
            var handle = surface.TryFind("confirmButton");

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.Native, Is.SameAs(button),
                "stable tag identity must resolve the intended GameObject after a rename");
            surface.SetActive(false);
            Assert.That(root.activeSelf, Is.False);
            surface.SetActive(true);
            Assert.That(root.activeSelf, Is.True);

            surface.Destroy();
            yield return null;
            Assert.That(root == null, Is.True);
        }
    }
}
