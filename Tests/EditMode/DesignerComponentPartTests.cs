using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerComponentPartTests
    {
        [Test]
        public void StockControlsDeclareStableEditableParts()
        {
            var toggle = DesignerComponentRegistry.Get("UGUI.Toggle");
            Assert.AreEqual("Background", toggle.GetPart("background").UGUIPath);
            Assert.AreEqual("Background/Checkmark", toggle.GetPart("checkmark").UGUIPath);
            Assert.AreEqual("Label", toggle.GetPart("label").UGUIPath);

            var slider = DesignerComponentRegistry.Get("UGUI.Slider");
            Assert.AreEqual("Background", slider.GetPart("track").UGUIPath);
            Assert.AreEqual("Fill Area/Fill", slider.GetPart("fill").UGUIPath);
            Assert.AreEqual("Handle Slide Area/Handle", slider.GetPart("handle").UGUIPath);
        }

        [Test]
        public void SparseOverrideBagCreatesFindsAndRemovesEmptyValues()
        {
            var element = new DesignerElementMetadata();
            var value = DesignerComponentPartOverrideBag.GetOrCreate(element.componentPartOverrides, "handle");
            Assert.AreSame(value, DesignerComponentPartOverrideBag.Find(element.componentPartOverrides, "handle"));
            value.hasPosition = true;
            value.position = new Vector2(12f, -4f);
            DesignerComponentPartOverrideBag.RemoveEmpty(element.componentPartOverrides, "handle");
            Assert.AreEqual(1, element.componentPartOverrides.Count);

            value.hasPosition = false;
            DesignerComponentPartOverrideBag.RemoveEmpty(element.componentPartOverrides, "handle");
            Assert.AreEqual(0, element.componentPartOverrides.Count);
        }

        [Test]
        public void GeneratedUssWritesMappedInternalPartTransforms()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var slider = new DesignerElementMetadata
            {
                elementId = "volume",
                elementType = "UITK.Slider",
                rect = new Rect(0, 0, 200, 24)
            };
            slider.componentPartOverrides.Add(new DesignerComponentPartOverrideMetadata
            {
                partId = "handle",
                hasPosition = true,
                position = new Vector2(6, 2),
                hasRotation = true,
                rotation = 15,
                hasScale = true,
                scale = new Vector2(1.2f, .8f)
            });
            metadata.elements.Add(slider);

            var uss = UIToolkitCodeGenerator.GenerateUss(metadata);
            StringAssert.Contains("#volume .unity-base-slider__dragger {", uss);
            StringAssert.Contains("translate: 6px 2px;", uss);
            StringAssert.Contains("rotate: 15deg;", uss);
            StringAssert.Contains("scale: 1.2 0.8;", uss);
            Object.DestroyImmediate(metadata);
        }

        [Test]
        public void ToggleGroupAddsRealEditableToggleChild()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var group = new DesignerElementMetadata
            {
                elementId = "choices",
                elementType = "UGUI.ToggleGroup",
                rect = new Rect(20, 30, 240, 140)
            };
            metadata.elements.Add(group);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);

            var child = context.CreateChildElement(group, "UGUI.Toggle");

            Assert.NotNull(child);
            Assert.AreEqual(group.elementId, child.parentId);
            Assert.AreEqual(DesignerComponentSlot.Content, child.parentSlotId);
            Assert.AreEqual("UGUI.Toggle", child.elementType);
            Assert.AreSame(child, context.SelectedMetadata);
            context.Dispose();
            Object.DestroyImmediate(metadata);
        }

        [Test]
        public void UguiBaselineRestoreIsIdempotent()
        {
            var go = new GameObject("Part", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(7, 8);
            rect.sizeDelta = new Vector2(30, 12);
            var tag = go.AddComponent<DesignerUGUIPartBaselineTag>();
            tag.Capture(rect, "owner", "handle");

            rect.anchoredPosition += new Vector2(100, 50);
            rect.sizeDelta += new Vector2(20, 10);
            tag.Restore(rect);
            tag.Restore(rect);

            Assert.AreEqual(new Vector2(7, 8), rect.anchoredPosition);
            Assert.AreEqual(new Vector2(30, 12), rect.sizeDelta);
            Object.DestroyImmediate(go);
        }
    }
}
