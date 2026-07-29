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
            DesignerInspectorSectionDescriptor match = null;
            foreach (var section in DesignerInspectorRegistry.All)
                if (string.Equals(section.Id, expectedId, StringComparison.Ordinal))
                    match = section;

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

        [Test]
        public void UnifiedInspectorStartsInCompactBuildWorkspaceAndLazilyBuildsOneSection()
        {
            const string workflowPreferenceKey = "NexUI.Designer.Inspector.ActiveWorkflow";
            var hadWorkflowPreference = EditorPrefs.HasKey(workflowPreferenceKey);
            var previousWorkflow = EditorPrefs.GetInt(workflowPreferenceKey, (int)DesignerInspectorWorkflow.Build);
            EditorPrefs.SetInt(workflowPreferenceKey, (int)DesignerInspectorWorkflow.Build);
            var preferenceKeys = new List<string>();
            var previousValues = new Dictionary<string, bool>();
            foreach (var descriptor in DesignerInspectorRegistry.All)
            {
                var key = "NexUI.Designer.Inspector.V2.Section." + descriptor.Id;
                preferenceKeys.Add(key);
                if (EditorPrefs.HasKey(key)) previousValues[key] = EditorPrefs.GetBool(key);
                EditorPrefs.DeleteKey(key);
            }

            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = new DesignerElementMetadata
            {
                stableId = "compact-inspector-stable", elementId = "card", displayName = "Card",
                elementType = "Panel", rect = new Rect(0, 0, 240, 120)
            };
            metadata.elements.Add(element);
            var context = new NexUIDesignerContext();
            context.SetMetadata(metadata);
            context.Select(element);
            try
            {
                var inspector = new NexUIRightInspector(context);
                var tabs = inspector.Query<Button>(className: "nexui-inspector-workflow-tab").ToList();
                Assert.AreEqual(6, tabs.Count);
                var selected = tabs.Find(tab => tab.ClassListContains("is-selected"));
                Assert.IsNotNull(selected);
                Assert.AreEqual(DesignerInspectorWorkflow.Build, selected.userData);
                Assert.IsTrue(tabs.TrueForAll(tab => !string.IsNullOrWhiteSpace(tab.tooltip)));
                Assert.IsTrue(tabs.TrueForAll(tab => char.IsDigit(tab.text[tab.text.Length - 1])));

                var sections = inspector.Query<Foldout>(className: "nexui-unified-inspector-section").ToList();
                Assert.Greater(sections.Count, 1);
                Assert.IsTrue(sections.TrueForAll(section => section.ClassListContains("workflow-build")));
                Assert.IsTrue(sections.TrueForAll(section =>
                    !string.IsNullOrWhiteSpace(section.tooltip) && section.tooltip.Split('\n').Length >= 4));
                Assert.AreEqual(1,
                    inspector.Query<VisualElement>(className: "nexui-unified-inspector-content").ToList().Count,
                    "Only the default expanded section should instantiate its heavy Inspector UI.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    inspector.Q<Label>(className: "nexui-inspector-workspace-description")?.text));
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
                if (hadWorkflowPreference) EditorPrefs.SetInt(workflowPreferenceKey, previousWorkflow);
                else EditorPrefs.DeleteKey(workflowPreferenceKey);
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
                DesignerInspectorWorkflow.Build,
                DesignerInspectorExposure.Common,
                DesignerInspectorTarget.Element,
                factory);
    }
}
