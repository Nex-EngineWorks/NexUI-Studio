using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Viewport;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Ruler tick spacing, guide list behaviour, snapping and persistence - the parts of canvas
    /// guides that decide whether laying a screen out feels precise. All pure; no window required.
    /// </summary>
    public sealed class DesignerCanvasGuidesTests
    {
        // ---- Ruler ticks ------------------------------------------------------------------

        [Test]
        public void TickStep_KeepsTicksAtLeastTheRequestedDistanceApartOnScreen()
        {
            foreach (var zoom in new[] { 0.15f, 0.5f, 1f, 1.75f, 2f })
            {
                var step = DesignerCanvasGuides.TickStep(zoom, 64f);
                Assert.GreaterOrEqual(step * zoom, 64f - 0.001f,
                    $"At zoom {zoom} ticks would be closer than the minimum spacing.");
            }
        }

        [Test]
        public void TickStep_ReturnsRoundNumbersSoLabelsStayReadable()
        {
            var allowed = new List<float> { 1f, 2f, 5f, 10f, 20f, 25f, 50f, 100f, 200f, 250f, 500f, 1000f, 2000f, 5000f };
            foreach (var zoom in new[] { 0.15f, 0.37f, 0.5f, 1f, 1.3f, 2f })
                Assert.Contains(DesignerCanvasGuides.TickStep(zoom), allowed);
        }

        [Test]
        public void TickStep_SurvivesDegenerateZoom()
        {
            Assert.Greater(DesignerCanvasGuides.TickStep(0f), 0f);
            Assert.Greater(DesignerCanvasGuides.TickStep(-1f), 0f);
        }

        [Test]
        public void Ticks_StartAtOrBeforeTheRangeAndCoverIt()
        {
            var ticks = DesignerCanvasGuides.Ticks(105f, 320f, 50f);
            Assert.AreEqual(100f, ticks[0], "The first tick must be at or before the visible range.");
            Assert.GreaterOrEqual(ticks[ticks.Count - 1], 320f - 50f);
            CollectionAssert.AllItemsAreUnique(ticks);
        }

        [Test]
        public void Ticks_RejectsDegenerateRanges()
        {
            Assert.IsEmpty(DesignerCanvasGuides.Ticks(0f, 100f, 0f));
            Assert.IsEmpty(DesignerCanvasGuides.Ticks(100f, 0f, 10f));
            Assert.IsEmpty(DesignerCanvasGuides.Ticks(0f, 1000000f, 1f), "A pathological range must not build a huge list.");
        }

        [TestCase(100f, "100")]
        [TestCase(-50f, "-50")]
        [TestCase(12.5f, "12.5")]
        public void TickLabel_KeepsIntegersInteger(float value, string expected)
            => Assert.AreEqual(expected, DesignerCanvasGuides.TickLabel(value));

        // ---- Guide list -------------------------------------------------------------------

        [Test]
        public void Add_IgnoresAGuideDroppedOnTopOfAnExistingOne()
        {
            var guides = new List<DesignerGuide>();
            Assert.IsTrue(DesignerCanvasGuides.Add(guides, new DesignerGuide(DesignerGuideAxis.Vertical, 100f)));
            Assert.IsFalse(DesignerCanvasGuides.Add(guides, new DesignerGuide(DesignerGuideAxis.Vertical, 100.2f)),
                "Dropping a guide onto an existing one is a slip, not a request for two guides.");
            Assert.AreEqual(1, guides.Count);
        }

        [Test]
        public void Add_AllowsTheSamePositionOnTheOtherAxis()
        {
            var guides = new List<DesignerGuide>();
            DesignerCanvasGuides.Add(guides, new DesignerGuide(DesignerGuideAxis.Vertical, 100f));
            Assert.IsTrue(DesignerCanvasGuides.Add(guides, new DesignerGuide(DesignerGuideAxis.Horizontal, 100f)));
            Assert.AreEqual(2, guides.Count);
        }

        [Test]
        public void IndexAt_GrabAreaScalesWithZoomSoItFeelsTheSame()
        {
            var guides = new List<DesignerGuide> { new DesignerGuide(DesignerGuideAxis.Vertical, 100f) };

            // At 100% a 4px miss is inside the 5px grab area.
            Assert.AreEqual(0, DesignerCanvasGuides.IndexAt(guides, DesignerGuideAxis.Vertical, 104f, 1f));
            // Zoomed out to 25%, the same 4 canvas units are only 1px away - still a hit.
            Assert.AreEqual(0, DesignerCanvasGuides.IndexAt(guides, DesignerGuideAxis.Vertical, 104f, 0.25f));
            // Zoomed in to 400%, 4 canvas units are 16px away - a miss.
            Assert.AreEqual(-1, DesignerCanvasGuides.IndexAt(guides, DesignerGuideAxis.Vertical, 104f, 4f));
        }

        [Test]
        public void IndexAt_IgnoresTheOtherAxis()
        {
            var guides = new List<DesignerGuide> { new DesignerGuide(DesignerGuideAxis.Vertical, 100f) };
            Assert.AreEqual(-1, DesignerCanvasGuides.IndexAt(guides, DesignerGuideAxis.Horizontal, 100f, 1f));
        }

        [Test]
        public void IndexAt_PicksTheNearestWhenGuidesOverlap()
        {
            var guides = new List<DesignerGuide>
            {
                new DesignerGuide(DesignerGuideAxis.Vertical, 100f),
                new DesignerGuide(DesignerGuideAxis.Vertical, 103f)
            };
            Assert.AreEqual(1, DesignerCanvasGuides.IndexAt(guides, DesignerGuideAxis.Vertical, 102.5f, 1f));
        }

        // ---- Snapping ---------------------------------------------------------------------

        [Test]
        public void Snap_PullsTheNearEdgeOntoAGuideWithoutResizing()
        {
            var guides = new List<DesignerGuide> { new DesignerGuide(DesignerGuideAxis.Vertical, 100f) };
            var moving = new Rect(96f, 40f, 50f, 20f);

            var snapped = DesignerCanvasGuides.Snap(moving, guides, 8f, out var vertical, out var horizontal);

            Assert.AreEqual(100f, snapped.x, 0.001f);
            Assert.AreEqual(50f, snapped.width, 0.001f, "Snapping moves a rect; it must never resize it.");
            Assert.AreEqual(100f, vertical);
            Assert.IsNull(horizontal);
        }

        [Test]
        public void Snap_MatchesTheRightEdgeAndTheCentreToo()
        {
            var guides = new List<DesignerGuide> { new DesignerGuide(DesignerGuideAxis.Vertical, 100f) };

            var byRightEdge = DesignerCanvasGuides.Snap(new Rect(48f, 0f, 50f, 10f), guides, 8f, out _, out _);
            Assert.AreEqual(50f, byRightEdge.x, 0.001f, "xMax 98 should snap to the guide at 100.");

            var byCentre = DesignerCanvasGuides.Snap(new Rect(72f, 0f, 50f, 10f), guides, 8f, out _, out _);
            Assert.AreEqual(75f, byCentre.x, 0.001f, "centre 97 should snap to the guide at 100.");
        }

        [Test]
        public void Snap_LeavesTheRectAloneBeyondTheThreshold()
        {
            var guides = new List<DesignerGuide> { new DesignerGuide(DesignerGuideAxis.Vertical, 100f) };
            var moving = new Rect(0f, 0f, 10f, 10f);

            var snapped = DesignerCanvasGuides.Snap(moving, guides, 8f, out var vertical, out _);

            Assert.AreEqual(moving, snapped);
            Assert.IsNull(vertical);
        }

        [Test]
        public void Snap_HandlesBothAxesAtOnce()
        {
            var guides = new List<DesignerGuide>
            {
                new DesignerGuide(DesignerGuideAxis.Vertical, 100f),
                new DesignerGuide(DesignerGuideAxis.Horizontal, 200f)
            };

            var snapped = DesignerCanvasGuides.Snap(new Rect(97f, 196f, 20f, 20f), guides, 8f, out var v, out var h);

            Assert.AreEqual(100f, snapped.x, 0.001f);
            Assert.AreEqual(200f, snapped.y, 0.001f);
            Assert.AreEqual(100f, v);
            Assert.AreEqual(200f, h);
        }

        [Test]
        public void Snap_WithNoGuidesIsANoOp()
        {
            var moving = new Rect(3f, 7f, 11f, 13f);
            Assert.AreEqual(moving, DesignerCanvasGuides.Snap(moving, null, 8f, out _, out _));
            Assert.AreEqual(moving, DesignerCanvasGuides.Snap(moving, new List<DesignerGuide>(), 8f, out _, out _));
        }

        // ---- Persistence ------------------------------------------------------------------

        [Test]
        public void SerializeDeserialize_RoundTrips()
        {
            var guides = new List<DesignerGuide>
            {
                new DesignerGuide(DesignerGuideAxis.Vertical, 120f),
                new DesignerGuide(DesignerGuideAxis.Horizontal, -64.5f)
            };

            var restored = DesignerCanvasGuides.Deserialize(DesignerCanvasGuides.Serialize(guides));

            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual(DesignerGuideAxis.Vertical, restored[0].Axis);
            Assert.AreEqual(120f, restored[0].Position, 0.01f);
            Assert.AreEqual(DesignerGuideAxis.Horizontal, restored[1].Axis);
            Assert.AreEqual(-64.5f, restored[1].Position, 0.01f);
        }

        [Test]
        public void Deserialize_SkipsGarbageInsteadOfThrowing()
        {
            var restored = DesignerCanvasGuides.Deserialize("V:10|nonsense|H:|X:5|H:20|");
            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual(10f, restored[0].Position, 0.01f);
            Assert.AreEqual(20f, restored[1].Position, 0.01f);
        }

        [Test]
        public void Serialize_EmptyListProducesEmptyString()
        {
            Assert.AreEqual(string.Empty, DesignerCanvasGuides.Serialize(new List<DesignerGuide>()));
            Assert.AreEqual(string.Empty, DesignerCanvasGuides.Serialize(null));
            Assert.IsEmpty(DesignerCanvasGuides.Deserialize(string.Empty));
            Assert.IsEmpty(DesignerCanvasGuides.Deserialize(null));
        }
    }
}
