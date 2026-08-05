using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Locks the behaviour of the element-scoped validation rules.
    /// </summary>
    /// <remarks>
    /// These rules were extracted out of the element loop so validation can eventually be narrowed
    /// to the elements that actually changed. The extraction is only safe if this method really is
    /// a function of one element alone - if it ever starts depending on the rest of the document,
    /// narrowing would silently drop issues. These tests are the net under that property, and the
    /// regression net for the incremental work that comes next.
    /// </remarks>
    public sealed class DesignerValidationElementScopeTests
    {
        private static DesignerElementMetadata Element(string id, string stableId = null)
            => new DesignerElementMetadata
            {
                elementId = id,
                stableId = stableId ?? ("stable-" + id),
                elementType = "Panel",
                rect = new Rect(0f, 0f, 100f, 40f)
            };

        private static List<DesignerValidationIssue> Run(DesignerElementMetadata element,
            HashSet<string> backendNames = null, UIRenderBackend backend = UIRenderBackend.UGUI)
        {
            var issues = new List<DesignerValidationIssue>();
            DesignerValidationService.ValidateElement(element, backend, "TestScreen", backendNames, issues);
            return issues;
        }

        private static bool Has(List<DesignerValidationIssue> issues, string code)
            => issues.Any(i => i.Code == code);

        [Test]
        public void ValidateElement_IgnoresNull()
        {
            Assert.DoesNotThrow(() => Run(null));
            Assert.IsEmpty(Run(null));
        }

        [Test]
        public void ValidateElement_LeavesTheEmptyIdCaseToTheCaller()
        {
            // The loop reports empty ids once per element and then skips it; reporting it here too
            // would double every such issue.
            Assert.IsEmpty(Run(Element(string.Empty)));
        }

        [Test]
        public void ValidateElement_FlagsAnUnsafeIdentifier()
        {
            var issues = Run(Element("has spaces!"), backendNames: null);

            Assert.IsTrue(Has(issues, "invalid-element-id"));
            Assert.AreEqual("has spaces!", issues.First(i => i.Code == "invalid-element-id").ElementId);
        }

        [Test]
        public void ValidateElement_AcceptsASafeIdentifier()
        {
            Assert.IsFalse(Has(Run(Element("login_button-1")), "invalid-element-id"));
        }

        [Test]
        public void ValidateElement_FlagsAnElementMissingFromTheBackendAsset()
        {
            var issues = Run(Element("Start"), new HashSet<string> { "SomethingElse" });

            Assert.IsTrue(Has(issues, "missing-backend-element"));
        }

        [Test]
        public void ValidateElement_AcceptsAnElementPresentByName()
        {
            var issues = Run(Element("Start"), new HashSet<string> { "Start" });

            Assert.IsFalse(Has(issues, "missing-backend-element"));
        }

        [Test]
        public void ValidateElement_AcceptsAnElementMatchedByStableId()
        {
            // A renamed element still matches through its stable identity, which is what stops a
            // rename from reporting the whole screen as missing from the backend.
            var issues = Run(Element("RenamedStart", "stable-1"), new HashSet<string> { "$stable:stable-1" });

            Assert.IsFalse(Has(issues, "missing-backend-element"));
        }

        [Test]
        public void ValidateElement_SkipsTheBackendCheckWhenThereIsNoBackendAsset()
        {
            Assert.IsFalse(Has(Run(Element("Start"), backendNames: null), "missing-backend-element"));
        }

        [Test]
        public void ValidateElement_ReportsNothingCrossElement()
        {
            // Two elements sharing an id must not be detectable from here - duplicate detection is
            // a whole-document rule and has to stay in the loop, or narrowing would lose it.
            var a = Element("Same");
            var b = Element("Same");

            Assert.IsFalse(Has(Run(a), "duplicate-element-id"));
            Assert.IsFalse(Has(Run(b), "duplicate-element-id"));
        }

        [Test]
        public void ValidateElement_IsIndependentOfCallOrder()
        {
            var first = Run(Element("bad id"), new HashSet<string>());
            var second = Run(Element("bad id"), new HashSet<string>());

            CollectionAssert.AreEqual(
                first.Select(i => i.Code).ToArray(),
                second.Select(i => i.Code).ToArray(),
                "Validating the same element twice must produce the same issues in the same order.");
        }
    }
}
