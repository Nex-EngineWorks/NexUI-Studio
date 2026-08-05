using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Covers the one thing this cache must never do: serve issues that no longer describe the
    /// element.
    /// </summary>
    /// <remarks>
    /// A cache that is too eager loses real validation errors, and the user finds out by shipping
    /// them. Every test here is therefore about invalidation rather than about hit rate - being
    /// slow is recoverable, being wrong is not.
    /// </remarks>
    public sealed class NexValidationCacheTests
    {
        private NexValidationCache _cache;

        [SetUp]
        public void SetUp() => _cache = new NexValidationCache();

        private static DesignerElementMetadata Element(string id)
            => new DesignerElementMetadata { elementId = id, stableId = "stable-" + id, elementType = "Panel" };

        private static List<DesignerValidationIssue> Issues(string code)
            => new List<DesignerValidationIssue>
            {
                new DesignerValidationIssue(DesignerValidationSeverity.Warning, code, "m", "f")
            };

        private void BeginPass(HashSet<string> backendNames = null,
            UIRenderBackend backend = UIRenderBackend.UGUI, string screenId = "TestScreen")
            => _cache.BeginPass(backend, screenId, backendNames ?? new HashSet<string>());

        // ---- reuse ----------------------------------------------------------

        [Test]
        public void TryReuse_ReturnsNothingBeforeAnythingIsStored()
        {
            BeginPass();

            Assert.IsNull(_cache.TryReuse(Element("A")));
        }

        [Test]
        public void TryReuse_ReturnsWhatWasStoredForTheSameElement()
        {
            BeginPass();
            var element = Element("A");
            _cache.Store(element, Issues("some-code"));

            BeginPass();
            var reused = _cache.TryReuse(element);

            Assert.IsNotNull(reused);
            Assert.AreEqual("some-code", reused[0].Code);
        }

        [Test]
        public void TryReuse_MatchesOnStableIdNotElementId()
        {
            BeginPass();
            var before = Element("Original");
            _cache.Store(before, Issues("some-code"));

            // The same element after a rename: elementId moved, stableId did not.
            var afterRename = new DesignerElementMetadata
            {
                elementId = "Renamed", stableId = before.stableId, elementType = "Panel"
            };

            BeginPass();
            Assert.IsNotNull(_cache.TryReuse(afterRename),
                "A rename is an ordinary edit; the cache keys on identity, not on the display id.");
        }

        [Test]
        public void TryReuse_RefusesAnElementWithNoStableIdentity()
        {
            BeginPass();
            var anonymous = new DesignerElementMetadata { elementId = "A", stableId = string.Empty };
            _cache.Store(anonymous, Issues("some-code"));

            BeginPass();
            Assert.IsNull(_cache.TryReuse(anonymous),
                "Without an identity there is nothing to key on, so it must be recomputed.");
        }

        // ---- invalidation ---------------------------------------------------

        [Test]
        public void Invalidate_DropsOnlyTheNamedElements()
        {
            BeginPass();
            var a = Element("A");
            var b = Element("B");
            _cache.Store(a, Issues("a-code"));
            _cache.Store(b, Issues("b-code"));

            _cache.Invalidate(new[] { a.stableId });

            BeginPass();
            Assert.IsNull(_cache.TryReuse(a));
            Assert.IsNotNull(_cache.TryReuse(b));
        }

        [Test]
        public void InvalidateAll_DropsEverything()
        {
            BeginPass();
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));

            _cache.InvalidateAll();

            BeginPass();
            Assert.IsNull(_cache.TryReuse(a));
        }

        [Test]
        public void BeginPass_DropsEverythingWhenTheTargetBackendChanged()
        {
            BeginPass(backend: UIRenderBackend.UGUI);
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));

            BeginPass(backend: UIRenderBackend.UIToolkit);

            Assert.IsNull(_cache.TryReuse(a),
                "Backend-dependent rules would otherwise answer for the wrong backend.");
        }

        [Test]
        public void BeginPass_DropsEverythingWhenTheBackendAssetChanged()
        {
            BeginPass(new HashSet<string> { "A" });
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));

            // Someone edited the prefab / UXML outside the designer and an element disappeared.
            BeginPass(new HashSet<string>());

            Assert.IsNull(_cache.TryReuse(a),
                "missing-backend-element depends on the asset, so its verdict is now unknown.");
        }

        [Test]
        public void BeginPass_DropsEverythingWhenTheScreenChanged()
        {
            BeginPass(screenId: "ScreenA");
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));

            BeginPass(screenId: "ScreenB");

            Assert.IsNull(_cache.TryReuse(a));
        }

        [Test]
        public void BeginPass_KeepsEntriesWhenNothingAboutTheEnvironmentMoved()
        {
            var names = new HashSet<string> { "A", "B" };

            BeginPass(names);
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));

            // A fresh set with the same contents in a different insertion order must still count
            // as unchanged, or the cache would never hit.
            BeginPass(new HashSet<string> { "B", "A" });

            Assert.IsNotNull(_cache.TryReuse(a));
        }

        // ---- reporting ------------------------------------------------------

        [Test]
        public void BeginPass_ResetsTheHitCountersForEachPass()
        {
            BeginPass();
            var a = Element("A");
            _cache.Store(a, Issues("a-code"));
            Assert.AreEqual(1, _cache.RecomputedLastPass);

            BeginPass();
            _cache.TryReuse(a);

            Assert.AreEqual(1, _cache.ReusedLastPass);
            Assert.AreEqual(0, _cache.RecomputedLastPass);
        }
    }
}
