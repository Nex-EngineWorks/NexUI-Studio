using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Inspectors;
using NUnit.Framework;
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
