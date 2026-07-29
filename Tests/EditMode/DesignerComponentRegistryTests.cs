using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.UI.Panels;
using emiteat.NexUI.Designer.Editor.Viewport;
using emiteat.NexUI.MotionClip;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Registry-completeness tests (spec §34): every runtime component type has a valid descriptor,
    /// ids are unique, defaults are sane, and unknown types resolve to a safe Generic descriptor.
    /// </summary>
    public sealed class DesignerComponentRegistryTests
    {
        [Test]
        public void EveryElementTypeHasADescriptor()
        {
            foreach (DesignerElementType type in Enum.GetValues(typeof(DesignerElementType)))
            {
                var d = DesignerComponentRegistry.Get(type);
                Assert.IsNotNull(d, $"No descriptor for {type}");
                Assert.AreEqual(type.ToString(), d.TypeId, $"Descriptor TypeId mismatch for {type}");
                Assert.IsFalse(string.IsNullOrEmpty(d.DisplayName), $"{type} has no DisplayName");
                Assert.IsFalse(string.IsNullOrEmpty(d.LocalizationKey), $"{type} has no LocalizationKey");
            }
        }

        [Test]
        public void NoDuplicateTypeIds()
        {
            var seen = new HashSet<string>();
            foreach (var d in DesignerComponentRegistry.All)
                Assert.IsTrue(seen.Add(d.TypeId), $"Duplicate descriptor TypeId '{d.TypeId}'");
        }

        [Test]
        public void DefaultSizesAreValidAndAtLeastMinimum()
        {
            foreach (var d in DesignerComponentRegistry.All)
            {
                Assert.Greater(d.DefaultSize.x, 0f, $"{d.TypeId} default width");
                Assert.Greater(d.DefaultSize.y, 0f, $"{d.TypeId} default height");
                Assert.GreaterOrEqual(d.DefaultSize.x, d.MinimumSize.x, $"{d.TypeId} width < min");
                Assert.GreaterOrEqual(d.DefaultSize.y, d.MinimumSize.y, $"{d.TypeId} height < min");
            }
        }

        [Test]
        public void ContainersDeclareAtLeastOneSlot()
        {
            foreach (var d in DesignerComponentRegistry.All)
                if (d.CanHaveChildren)
                    Assert.Greater(d.Slots.Count, 0, $"{d.TypeId} can have children but declares no slots");
        }

        [Test]
        public void UnknownTypeResolvesToGenericKeepingItsId()
        {
            var d = DesignerComponentRegistry.Get("MyCustomWidget");
            Assert.IsTrue(d.IsGeneric);
            Assert.AreEqual("MyCustomWidget", d.TypeId);
            Assert.IsTrue(d.CanHaveChildren, "Generic must be permissive so custom screens aren't blocked");
        }

        [Test]
        public void LeafTypesCannotHaveChildren()
        {
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("Label"));
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("Image"));
            Assert.IsFalse(DesignerComponentRegistry.CanHaveChildren("ProgressBar"));
            Assert.IsTrue(DesignerComponentRegistry.CanHaveChildren("Panel"));
            Assert.IsTrue(DesignerComponentRegistry.CanHaveChildren("Button"), "Button has icon/content slots");
        }

        [Test]
        public void ContainerFlagMatchesExpectations()
        {
            Assert.IsTrue(DesignerComponentRegistry.IsContainer("Panel"));
            Assert.IsTrue(DesignerComponentRegistry.IsContainer("Modal"));
            Assert.IsFalse(DesignerComponentRegistry.IsContainer("Label"));
            Assert.IsFalse(DesignerComponentRegistry.IsContainer("Button"), "Button holds slot children but is not a layout container");
        }

        [Test]
        public void SupportedBindingsAreDeclaredPerType()
        {
            var button = DesignerComponentRegistry.Get("Button");
            Assert.IsTrue(button.SupportsBinding(DesignerBindingChannel.Command));
            Assert.IsTrue(button.SupportsBinding(DesignerBindingChannel.Text));

            var progress = DesignerComponentRegistry.Get("ProgressBar");
            Assert.IsTrue(progress.SupportsBinding(DesignerBindingChannel.Value));
            Assert.IsFalse(progress.SupportsBinding(DesignerBindingChannel.Command), "ProgressBar is not command-driven");
        }

        [Test]
        public void TemplateSlotsAreMarkedOnCollectionTypes()
        {
            foreach (var typeId in new[] { "List", "Grid", "ChoiceList", "Hotbar" })
            {
                var d = DesignerComponentRegistry.Get(typeId);
                var hasTemplate = false;
                foreach (var s in d.Slots) if (s.IsTemplateSlot) hasTemplate = true;
                Assert.IsTrue(hasTemplate, $"{typeId} should declare a template slot");
            }
        }

        [Test]
        public void ChannelForKeyName_MapsSerializedKeys()
        {
            Assert.AreEqual(DesignerBindingChannel.Command, DesignerComponentDescriptor.ChannelForKeyName("commandKey"));
            Assert.AreEqual(DesignerBindingChannel.Value, DesignerComponentDescriptor.ChannelForKeyName("valueKey"));
            Assert.AreEqual(DesignerBindingChannel.None, DesignerComponentDescriptor.ChannelForKeyName("nope"));
        }

        [Test]
        public void DefaultSlotIdPrefersContent()
        {
            Assert.AreEqual("content", DesignerComponentRegistry.Get("Panel").DefaultSlotId);
            Assert.AreEqual("content", DesignerComponentRegistry.Get("Modal").DefaultSlotId);
        }

        [Test]
        public void StockCatalogsDeclareNativeBackendFactories()
        {
            var uguiCount = 0;
            var toolkitCount = 0;
            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (descriptor.Family == DesignerComponentFamily.UGUI)
                {
                    uguiCount++;
                    Assert.IsFalse(string.IsNullOrEmpty(descriptor.UGUIControl), descriptor.TypeId);
                    Assert.AreEqual(DesignerBackendSupport.Full, descriptor.UGUISupport, descriptor.TypeId);
                }
                else if (descriptor.Family == DesignerComponentFamily.UIToolkit)
                {
                    toolkitCount++;
                    Assert.IsFalse(string.IsNullOrEmpty(descriptor.UxmlTag), descriptor.TypeId);
                }
            }
            Assert.GreaterOrEqual(uguiCount, 22, "uGUI catalog should cover the stock GameObject/UI controls and layouts");
            Assert.GreaterOrEqual(toolkitCount, 50, "UI Toolkit catalog should cover Unity 6's creatable runtime controls");
        }

        [Test]
        public void PaletteContainsAllThreeFamilies()
        {
            var families = new HashSet<DesignerComponentFamily>();
            foreach (var group in DesignerComponentPalette.BuildGroups())
            {
                families.Add(group.Family);
                Assert.Greater(group.Items.Count, 0, group.GroupId);
            }
            CollectionAssert.AreEquivalent(new[]
            {
                DesignerComponentFamily.NexUI,
                DesignerComponentFamily.UGUI,
                DesignerComponentFamily.UIToolkit
            }, families);
        }

        [Test]
        public void ComponentLibraryCardsExposePreviewAndDetailedTooltip()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var panel = new NexUIComponentsPanel(context);
                var cards = panel.Query<Button>(className: "nexui-component-card").ToList();
                Assert.GreaterOrEqual(cards.Count, 150, "the library should visualize the full registered catalog");
                foreach (var card in cards)
                {
                    Assert.IsNotNull(card.Q<VisualElement>(className: "nexui-component-card-preview"), card.userData as string);
                    StringAssert.Contains("uGUI", card.tooltip, card.userData as string);
                    StringAssert.Contains("UI Toolkit", card.tooltip, card.userData as string);
                }
                Assert.IsNotNull(panel.Q<VisualElement>(className: "nexui-component-details"));
                Assert.IsNotNull(panel.Q<VisualElement>(className: "nexui-component-detail-preview"));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void LocalizationLoadsCommaFirstUtf8SectionsAndKeepsLanguageParity()
        {
            var previous = DesignerLocalization.CurrentLanguage;
            try
            {
                DesignerLocalization.SetLanguage(DesignerLanguage.Korean);
                Assert.AreEqual("라이브러리", DesignerLocalization.T("palette.tooltip.family"));
                Assert.AreEqual("새 스크린", DesignerLocalization.T("productivity.newScreen"));
                Assert.AreEqual("재생", DesignerLocalization.T("motionClip.toolbar.play"));

                DesignerLocalization.SetLanguage(DesignerLanguage.English);
                Assert.AreEqual("Library", DesignerLocalization.T("palette.tooltip.family"));
                Assert.AreEqual("New Screen", DesignerLocalization.T("productivity.newScreen"));
                Assert.AreEqual("Play", DesignerLocalization.T("motionClip.toolbar.play"));
            }
            finally
            {
                DesignerLocalization.SetLanguage(previous);
            }
        }

        [Test]
        public void MotionPreviewMovesVisibleElementAndKeepsStartGhost()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "motion-stable", elementId = "moving", elementType = "Button",
                rect = new Rect(10, 20, 100, 40), text = "Move"
            });
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.duration = 1f;
            clip.tracks = new[]
            {
                new UIMotionClipTrack
                {
                    targetElementId = "moving",
                    propertyTracks = new[]
                    {
                        new UIMotionClipPropertyTrack
                        {
                            propertyType = UIMotionClipPropertyType.AnchoredPosition,
                            keyframes = new[]
                            {
                                new UIMotionClipKeyframe(0f, UIMotionClipValue.FromVector2(new Vector2(10, 20))),
                                new UIMotionClipKeyframe(1f, UIMotionClipValue.FromVector2(new Vector2(210, 20)))
                            }
                        }
                    }
                }
            };
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            try
            {
                var viewport = new NexUIDesignerViewport(context);
                context.SetActiveMotionClip(clip, 0.5f);

                var views = viewport.Query<VisualElement>(className: "nexui-design-element").ToList();
                var actual = views.Find(view => !view.ClassListContains("nexui-motion-start-ghost"));
                var ghost = views.Find(view => view.ClassListContains("nexui-motion-start-ghost"));
                Assert.IsNotNull(actual);
                Assert.IsNotNull(ghost);
                Assert.That(actual.style.left.value.value, Is.EqualTo(110f).Within(0.01f));
                Assert.That(ghost.style.left.value.value, Is.EqualTo(10f).Within(0.01f));
                Assert.That(ghost.style.opacity.value, Is.EqualTo(0.28f).Within(0.01f));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }
    }
}
