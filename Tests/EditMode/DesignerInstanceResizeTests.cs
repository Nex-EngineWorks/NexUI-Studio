using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Resizing a component instance has to carry its contents. Before this, expansion translated the
    /// definition sub-tree and stopped, so a stretched instance kept a background the old width and
    /// left the rest of itself empty.
    /// </summary>
    /// <remarks>
    /// The rule under test is that each element's authored <see cref="DesignerAnchorPreset"/> is read
    /// as a resize constraint, so the answer matches what uGUI does with the same anchors rather than
    /// being a second, Designer-only heuristic. All pure: rects in, rects out.
    /// </remarks>
    public sealed class DesignerInstanceResizeTests
    {
        private static readonly Rect ParentOld = new Rect(0f, 0f, 200f, 100f);
        private static readonly Rect ParentWider = new Rect(0f, 0f, 400f, 100f);

        // ---- One element against one parent ------------------------------------------------

        [Test]
        public void TopLeftKeepsItsOffsetAndSize()
        {
            var child = new Rect(10f, 10f, 50f, 20f);

            var resized = DesignerInstanceResize.Resize(child, ParentOld, ParentWider, DesignerAnchorPreset.TopLeft);

            Assert.AreEqual(new Rect(10f, 10f, 50f, 20f), resized);
        }

        [Test]
        public void ARightAnchorKeepsItsDistanceToTheRightEdge()
        {
            // 200 - (140 + 50) = 10px from the right edge.
            var child = new Rect(140f, 10f, 50f, 20f);

            var resized = DesignerInstanceResize.Resize(child, ParentOld, ParentWider, DesignerAnchorPreset.TopRight);

            Assert.AreEqual(340f, resized.x, "the gap to the right edge is what stays constant");
            Assert.AreEqual(50f, resized.width);
            Assert.AreEqual(10f, resized.y, "a top anchor must not move vertically");
        }

        [Test]
        public void ACentreAnchorKeepsItsOffsetFromTheCentre()
        {
            var child = new Rect(75f, 10f, 50f, 20f);   // centred in 200

            var resized = DesignerInstanceResize.Resize(child, ParentOld, ParentWider, DesignerAnchorPreset.Top);

            Assert.AreEqual(175f, resized.x, "still centred in 400");
            Assert.AreEqual(50f, resized.width);
        }

        [Test]
        public void StretchKeepsBothMarginsAndGrows()
        {
            var child = new Rect(10f, 10f, 180f, 20f);   // 10px margin each side

            var resized = DesignerInstanceResize.Resize(child, ParentOld, ParentWider, DesignerAnchorPreset.Stretch);

            Assert.AreEqual(10f, resized.x);
            Assert.AreEqual(380f, resized.width);
            Assert.AreEqual(10f, resized.y, "Stretch stretches both axes; the height follows too");
            Assert.AreEqual(20f, resized.height, "the parent's height did not change");
        }

        /// <summary>
        /// Designer rects grow downward from a top-left origin. Reading the vertical fractions the
        /// other way round would move every bottom-anchored element the wrong way, and only on resize.
        /// </summary>
        [Test]
        public void ABottomAnchorTracksTheBottomEdgeNotTheTop()
        {
            var taller = new Rect(0f, 0f, 200f, 300f);
            var child = new Rect(10f, 70f, 50f, 20f);   // 100 - (70 + 20) = 10px from the bottom

            var resized = DesignerInstanceResize.Resize(child, ParentOld, taller, DesignerAnchorPreset.BottomLeft);

            Assert.AreEqual(270f, resized.y);
            Assert.AreEqual(10f, resized.x, "a left anchor must not move horizontally");
        }

        [Test]
        public void ShrinkingPastZeroClampsInsteadOfProducingANegativeRect()
        {
            var child = new Rect(10f, 10f, 180f, 20f);
            var tiny = new Rect(0f, 0f, 5f, 100f);

            var resized = DesignerInstanceResize.Resize(child, ParentOld, tiny, DesignerAnchorPreset.Stretch);

            Assert.AreEqual(0f, resized.width);
        }

        [Test]
        public void AChildOffsetIsRelativeToTheParentsOwnPosition()
        {
            var parentOld = new Rect(50f, 60f, 200f, 100f);
            var parentNew = new Rect(50f, 60f, 400f, 100f);
            var child = new Rect(60f, 70f, 50f, 20f);   // 10,10 inside the parent

            var resized = DesignerInstanceResize.Resize(child, parentOld, parentNew, DesignerAnchorPreset.TopLeft);

            Assert.AreEqual(new Rect(60f, 70f, 50f, 20f), resized);
        }

        // ---- A whole sub-tree ---------------------------------------------------------------

        private static List<DesignerElementMetadata> Card(Rect rootRect)
        {
            return new List<DesignerElementMetadata>
            {
                new DesignerElementMetadata { elementId = "root", rect = rootRect },
                new DesignerElementMetadata
                {
                    elementId = "bg", parentId = "root", rect = new Rect(0f, 0f, 200f, 120f),
                    anchorPreset = DesignerAnchorPreset.Stretch
                },
                new DesignerElementMetadata
                {
                    elementId = "icon", parentId = "root", rect = new Rect(8f, 8f, 24f, 24f),
                    anchorPreset = DesignerAnchorPreset.TopLeft
                },
                new DesignerElementMetadata
                {
                    elementId = "close", parentId = "root", rect = new Rect(168f, 8f, 24f, 24f),
                    anchorPreset = DesignerAnchorPreset.TopRight
                },
                new DesignerElementMetadata
                {
                    elementId = "body", parentId = "root", rect = new Rect(0f, 40f, 200f, 80f),
                    anchorPreset = DesignerAnchorPreset.Stretch
                },
                new DesignerElementMetadata
                {
                    elementId = "bodyText", parentId = "body", rect = new Rect(8f, 48f, 184f, 64f),
                    anchorPreset = DesignerAnchorPreset.Stretch
                }
            };
        }

        private static DesignerElementMetadata Get(List<DesignerElementMetadata> elements, string id)
            => elements.First(e => e.elementId == id);

        [Test]
        public void ResizingTheRootCascadesThroughEveryLevel()
        {
            var elements = Card(new Rect(0f, 0f, 400f, 120f));   // root already widened to 400

            DesignerInstanceResize.Apply(elements, "root", new Vector2(200f, 120f));

            Assert.AreEqual(400f, Get(elements, "bg").rect.width);
            Assert.AreEqual(8f, Get(elements, "icon").rect.x);
            Assert.AreEqual(368f, Get(elements, "close").rect.x);
            Assert.AreEqual(400f, Get(elements, "body").rect.width);
            Assert.AreEqual(384f, Get(elements, "bodyText").rect.width,
                "a grandchild follows its own parent's new size, not the root's");
        }

        [Test]
        public void AnUnchangedSizeRewritesNothing()
        {
            var elements = Card(new Rect(0f, 0f, 200f, 120f));
            var before = elements.Select(e => e.rect).ToList();

            DesignerInstanceResize.Apply(elements, "root", new Vector2(200f, 120f));

            CollectionAssert.AreEqual(before, elements.Select(e => e.rect).ToList());
        }

        [Test]
        public void AZeroSizedDefinitionRootIsLeftAlone()
        {
            var elements = Card(new Rect(0f, 0f, 400f, 120f));
            var before = elements.Select(e => e.rect).ToList();

            DesignerInstanceResize.Apply(elements, "root", Vector2.zero);

            CollectionAssert.AreEqual(before, elements.Select(e => e.rect).ToList());
        }

        /// <summary>
        /// An Auto Layout parent places its own children. Computing a placement here that the layout
        /// then overwrites would only make the canvas disagree with the saved result.
        /// </summary>
        [Test]
        public void AutoLayoutChildrenAreLeftToTheLayout()
        {
            var elements = Card(new Rect(0f, 0f, 400f, 120f));
            Get(elements, "body").autoLayout = new DesignerAutoLayoutMetadata { enabled = true };
            var bodyTextBefore = Get(elements, "bodyText").rect;

            DesignerInstanceResize.Apply(elements, "root", new Vector2(200f, 120f));

            Assert.AreEqual(400f, Get(elements, "body").rect.width, "the layout host itself still resizes");
            Assert.AreEqual(bodyTextBefore, Get(elements, "bodyText").rect);
        }

        // ---- Through the expander ------------------------------------------------------------

        private sealed class StubResolver : IDesignerComponentDefinitionResolver
        {
            public DesignerComponentDefinitionAsset Definition;

            public DesignerComponentDefinitionAsset Resolve(string definitionGuid, string definitionId)
                => Definition;
        }

        private static DesignerComponentDefinitionAsset CardDefinition()
        {
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.componentId = "card";
            definition.displayName = "Card";
            definition.version = 1;
            definition.rootElementId = "root";
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "root", stableId = "def-root", elementType = "Panel",
                rect = new Rect(0f, 0f, 200f, 120f)
            });
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "bg", stableId = "def-bg", parentId = "root", elementType = "Image",
                rect = new Rect(0f, 0f, 200f, 120f), anchorPreset = DesignerAnchorPreset.Stretch
            });
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "close", stableId = "def-close", parentId = "root", elementType = "Button",
                rect = new Rect(168f, 8f, 24f, 24f), anchorPreset = DesignerAnchorPreset.TopRight
            });
            return definition;
        }

        private static DesignerComponentExpansion ExpandWithInstanceRect(Rect instanceRect,
            out DesignerMetadataAsset screen)
        {
            var definition = CardDefinition();
            screen = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            screen.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            screen.elements.Add(new DesignerElementMetadata
            {
                elementId = "card1",
                stableId = "stable-card1",
                rect = instanceRect,
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = "guid-card", definitionId = "card", definitionVersion = 1
                }
            });
            return DesignerComponentExpander.Expand(screen, new StubResolver { Definition = definition });
        }

        [Test]
        public void ExpandingAResizedInstanceCarriesItsContents()
        {
            var expansion = ExpandWithInstanceRect(new Rect(50f, 60f, 400f, 120f), out _);
            try
            {
                var bg = expansion.Expanded.elements.First(e => e.elementId.EndsWith("bg"));
                var close = expansion.Expanded.elements.First(e => e.elementId.EndsWith("close"));

                Assert.AreEqual(400f, bg.rect.width, "the stretched background fills the resized instance");
                Assert.AreEqual(50f, bg.rect.x);
                Assert.AreEqual(50f + 368f, close.rect.x, "the right-anchored button tracks the right edge");
            }
            finally
            {
                expansion.Dispose();
            }
        }

        /// <summary>
        /// An instance placed at the definition's own size must expand byte-identically to how earlier
        /// builds expanded it, or every existing screen shifts on the next save.
        /// </summary>
        [Test]
        public void AnInstanceAtTheDefinitionSizeExpandsExactlyAsBefore()
        {
            var expansion = ExpandWithInstanceRect(new Rect(50f, 60f, 200f, 120f), out _);
            try
            {
                var bg = expansion.Expanded.elements.First(e => e.elementId.EndsWith("bg"));
                var close = expansion.Expanded.elements.First(e => e.elementId.EndsWith("close"));

                Assert.AreEqual(new Rect(50f, 60f, 200f, 120f), bg.rect);
                Assert.AreEqual(new Rect(50f + 168f, 60f + 8f, 24f, 24f), close.rect);
            }
            finally
            {
                expansion.Dispose();
            }
        }
    }
}
