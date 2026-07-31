using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Inspectors;
using emiteat.NexUI.Designer.Editor.UI.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerInspectorRegistryTests
    {
        [Test]
        public void BuiltInSectionsHaveUniqueStableIdsAndFactories()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var section in DesignerInspectorRegistry.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Id));
                Assert.IsFalse(string.IsNullOrWhiteSpace(section.Title));
                Assert.IsNotNull(section.Create);
                Assert.IsTrue(ids.Add(section.Id), $"Duplicate Inspector section id '{section.Id}'.");
            }

            CollectionAssert.IsSubsetOf(
                new[] { "screen", "component", "layout", "style", "binding", "motion", "validation", "capabilities" },
                ids);
        }

        [TestCase("position", "layout")]
        [TestCase("command", "binding")]
        [TestCase("backend", "capabilities")]
        public void SearchKeywordsDiscoverExpectedSection(string query, string expectedId)
        {
            var match = DesignerInspectorRegistry.Get(expectedId);

            Assert.IsNotNull(match);
            Assert.IsTrue(match.Matches(query));
        }

        [Test]
        public void DescriptorRejectsMissingIdentityAndFactory()
        {
            Assert.Throws<ArgumentException>(() => CreateDescriptor("", "Title", _ => new VisualElement()));
            Assert.Throws<ArgumentException>(() => CreateDescriptor("id", "", _ => new VisualElement()));
            Assert.Throws<ArgumentNullException>(() => CreateDescriptor("id", "Title", null));
        }

        [Test]
        public void RegistryRejectsCaseInsensitiveDuplicateIds()
        {
            Assert.Throws<InvalidOperationException>(() => DesignerInspectorRegistry.Register(
                CreateDescriptor("LAYOUT", "Duplicate", _ => new VisualElement())));
        }

        /// <summary>
        /// The always-present slots have to stay the Unity ones - transform, the element's own look,
        /// and the component stack - or the Inspector goes back to listing every feature at once.
        /// </summary>
        [Test]
        public void OnlyTransformCoreAndComponentSlotsAreAlwaysPresent()
        {
            foreach (var section in DesignerInspectorRegistry.All)
            {
                if (section.Target == DesignerInspectorTarget.Screen) continue;
                if (section.Slot == DesignerInspectorSlot.Feature) continue;
                Assert.That(section.Slot, Is.EqualTo(DesignerInspectorSlot.Transform)
                        .Or.EqualTo(DesignerInspectorSlot.Core)
                        .Or.EqualTo(DesignerInspectorSlot.Components),
                    $"Section '{section.Id}' is always shown; only transform, core and the component stack may be.");
            }
        }

        [Test]
        public void UntouchedElementShowsNoFeatureSections()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata
            {
                stableId = "unity-inspector-stable", elementId = "card", displayName = "Card",
                elementType = "Panel", rect = new Rect(0, 0, 240, 120)
            };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                foreach (var section in DesignerInspectorRegistry.All)
                {
                    if (section.Slot != DesignerInspectorSlot.Feature) continue;
                    if (!section.AppliesTo(context)) continue;
                    if (section.IsInUse == null) continue; // Pro-only sections, gated by exposure.
                    Assert.IsFalse(section.IsInUseBy(context),
                        $"Feature section '{section.Id}' claims to be in use on an untouched element.");
                }
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void FeatureSectionAppearsOnceTheElementUsesIt()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata { elementId = "hp", elementType = "Label" };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                var binding = DesignerInspectorRegistry.Get("binding");
                Assert.IsNotNull(binding);
                Assert.IsFalse(binding.IsInUseBy(context));

                element.binding.textKey = "player.hp";
                Assert.IsTrue(binding.IsInUseBy(context));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void InspectorBuildsUnityLikeStackWithAddComponentLast()
        {
            var preferenceKeys = new List<string>();
            var previousValues = new Dictionary<string, bool>();
            foreach (var descriptor in DesignerInspectorRegistry.All)
            {
                var key = "NexUI.Designer.Inspector.Block." + descriptor.Id;
                preferenceKeys.Add(key);
                if (EditorPrefs.HasKey(key)) previousValues[key] = EditorPrefs.GetBool(key);
                EditorPrefs.DeleteKey(key);
            }

            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata
            {
                stableId = "unity-inspector-stack", elementId = "card", displayName = "Card",
                elementType = "Panel", rect = new Rect(0, 0, 240, 120)
            };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                var inspector = new NexUIRightInspector(context);

                Assert.IsNotNull(inspector.Q<TextField>(className: "nexui-inspector-identity-name"),
                    "The header should rename the element in place, like Unity's GameObject header.");
                Assert.IsNotNull(inspector.Q<Toggle>(className: "nexui-inspector-identity-active"));
                Assert.IsEmpty(inspector.Query<Button>(className: "nexui-inspector-workflow-tab").ToList(),
                    "Workflow tabs are gone: the stack is ordered by slot instead.");

                var blocks = inspector.Query<NexUIInspectorBlock>().ToList();
                Assert.IsNotEmpty(blocks);
                Assert.AreEqual("layout", blocks[0].Id, "The transform block is pinned first, as in Unity.");
                CollectionAssert.DoesNotContain(blocks.ConvertAll(block => block.Id), "motion",
                    "An element with no motion should not carry a Motion section.");

                Assert.IsNotNull(inspector.Q<VisualElement>(className: "nexui-inspector-component-stack"),
                    "The component stack is a top-level stack of cards, not a section inside a foldout.");

                var add = inspector.Q<Button>(className: "nexui-inspector-add-component");
                Assert.IsNotNull(add, "Add Component belongs under the stack.");
                Assert.Greater(add.parent.IndexOf(add), add.parent.IndexOf(blocks[0]));
            }
            finally
            {
                context.Dispose();
                UnityEngine.Object.DestroyImmediate(metadata);
                foreach (var key in preferenceKeys)
                {
                    if (previousValues.TryGetValue(key, out var value)) EditorPrefs.SetBool(key, value);
                    else EditorPrefs.DeleteKey(key);
                }
            }
        }

        private static DesignerInspectorSectionDescriptor CreateDescriptor(
            string id,
            string title,
            Func<NexUIDesignerContext, VisualElement> factory)
            => new DesignerInspectorSectionDescriptor(
                id,
                title,
                "keywords",
                DesignerInspectorSlot.Feature,
                DesignerInspectorExposure.Common,
                DesignerInspectorTarget.Element,
                factory);
    }
}
