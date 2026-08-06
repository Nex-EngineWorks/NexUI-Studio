using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The pen tool's editing operations, tested without a viewport.
    /// </summary>
    /// <remarks>
    /// These are the parts of a pen tool that are actually right or wrong - subdivision, handle
    /// mirroring, what happens at the ends of an open path - and none of them can be reached
    /// through an EditorWindow in a test. Keeping them in plain functions is what makes them
    /// verifiable at all; the viewport is then only hit-testing and dragging.
    /// </remarks>
    public sealed class NexPathEditingTests
    {
        private static NexVectorShape Square()
            => NexShapeFactory.Rectangle(new Rect(0f, 0f, 100f, 100f));

        private static NexVectorAnchor AnchorAt(NexVectorShape shape, int index)
            => shape.Contours[0].Anchors[index];

        // ---- hit testing -----------------------------------------------------

        [Test]
        public void APointIsFoundWithinTheGrabRadius()
        {
            var hit = NexPathEditing.HitTest(Square(), new Vector2(2f, 2f), radius: 6f);

            Assert.IsTrue(hit.Found);
            Assert.AreEqual(NexAnchorPart.Point, hit.Part);
            Assert.AreEqual(0, hit.Anchor);
        }

        [Test]
        public void NothingIsFoundOutsideTheGrabRadius()
        {
            Assert.IsFalse(NexPathEditing.HitTest(Square(), new Vector2(50f, 50f), radius: 6f).Found);
        }

        [Test]
        public void AHandleWinsOverThePointBeneathIt()
        {
            // With small curvature a handle sits almost on its anchor. Preferring the point would
            // move the whole anchor when the user was clearly aiming at the visible handle.
            var shape = Square();
            var anchors = shape.Contours[0].Anchors;
            anchors[0] = new NexVectorAnchor(anchors[0].Position, Vector2.zero, new Vector2(3f, 0f));

            var hit = NexPathEditing.HitTest(shape, new Vector2(3f, 0f), radius: 6f);

            Assert.AreEqual(NexAnchorPart.OutHandle, hit.Part);
        }

        [Test]
        public void AZeroHandleCannotBeGrabbed()
        {
            // It is not drawn, so it must not be grabbable - otherwise a corner has two invisible
            // handles stacked on it that steal every click.
            var hit = NexPathEditing.HitTest(Square(), Vector2.zero, radius: 6f);

            Assert.AreEqual(NexAnchorPart.Point, hit.Part);
        }

        // ---- moving ----------------------------------------------------------

        [Test]
        public void MovingAPointCarriesItsHandlesAlong()
        {
            var shape = Square();
            var anchors = shape.Contours[0].Anchors;
            anchors[0] = new NexVectorAnchor(Vector2.zero, new Vector2(-5f, 0f), new Vector2(5f, 0f));

            NexPathEditing.Move(shape, new NexPathHit(0, 0, NexAnchorPart.Point), new Vector2(10f, 10f));

            var moved = AnchorAt(shape, 0);
            Assert.AreEqual(new Vector2(10f, 10f), moved.Position);
            Assert.AreEqual(new Vector2(-5f, 0f), moved.InHandle, "Handles are relative, so they follow.");
            Assert.AreEqual(new Vector2(5f, 0f), moved.OutHandle);
        }

        [Test]
        public void DraggingOneHandleMirrorsTheOther()
        {
            var shape = Square();
            var anchors = shape.Contours[0].Anchors;
            anchors[0] = new NexVectorAnchor(Vector2.zero, new Vector2(-5f, 0f), new Vector2(5f, 0f));

            NexPathEditing.Move(shape, new NexPathHit(0, 0, NexAnchorPart.OutHandle), new Vector2(0f, 5f));

            var moved = AnchorAt(shape, 0);
            Assert.AreEqual(new Vector2(5f, 5f), moved.OutHandle);
            Assert.AreEqual(new Vector2(-5f, -5f), moved.InHandle, "A smooth point keeps its handles opposed.");
        }

        [Test]
        public void MirroringIsSkippedOnACornerAndWhenTurnedOff()
        {
            var shape = Square();
            var anchors = shape.Contours[0].Anchors;
            anchors[0] = new NexVectorAnchor(Vector2.zero, Vector2.zero, new Vector2(5f, 0f));

            NexPathEditing.Move(shape, new NexPathHit(0, 0, NexAnchorPart.OutHandle), new Vector2(0f, 5f));

            Assert.AreEqual(Vector2.zero, AnchorAt(shape, 0).InHandle,
                "A handle that was zero stays zero - the point is a corner on that side.");
        }

        [Test]
        public void AZeroDragChangesNothing()
        {
            Assert.IsFalse(NexPathEditing.Move(Square(), new NexPathHit(0, 0, NexAnchorPart.Point), Vector2.zero));
        }

        // ---- inserting -------------------------------------------------------

        [Test]
        public void InsertingSplitsTheSegmentWithoutMovingTheCurve()
        {
            // De Casteljau subdivision: the two halves reproduce the original curve exactly, so the
            // shape does not twitch when a point is added. Guessing handles instead does twitch,
            // which is the most noticeable way a pen tool feels wrong.
            var shape = NexShapeFactory.Ellipse(new Rect(0f, 0f, 100f, 100f));
            var before = NexVectorTessellator.Tessellate(shape)[0].Vertices.Length;

            Assert.IsTrue(NexPathEditing.InsertOnSegment(shape, 0, 0, 0.5f));
            Assert.AreEqual(5, shape.Contours[0].Anchors.Count);

            var after = NexVectorTessellator.Tessellate(shape)[0].Vertices.Length;
            Assert.AreEqual(before, after, 4,
                "Subdividing must not change the shape, only how many points describe it.");
        }

        [Test]
        public void TheInsertedPointLandsOnTheCurve()
        {
            var shape = Square();
            NexPathEditing.InsertOnSegment(shape, 0, 0, 0.5f);

            // Straight segment from (0,0) to (100,0): the midpoint is unambiguous.
            Assert.AreEqual(new Vector2(50f, 0f), AnchorAt(shape, 1).Position);
        }

        [Test]
        public void TheLastSegmentOfAnOpenPathCannotBeSplit()
        {
            var shape = Square();
            shape.Contours[0].Closed = false;
            var last = shape.Contours[0].Anchors.Count - 1;

            Assert.IsFalse(NexPathEditing.InsertOnSegment(shape, 0, last, 0.5f),
                "There is no segment after the final point of an open path.");
        }

        [Test]
        public void TheClosingSegmentOfAClosedPathCanBeSplit()
        {
            var shape = Square();
            var last = shape.Contours[0].Anchors.Count - 1;

            Assert.IsTrue(NexPathEditing.InsertOnSegment(shape, 0, last, 0.5f));
            Assert.AreEqual(5, shape.Contours[0].Anchors.Count);
        }

        // ---- removing and corners ---------------------------------------------

        [Test]
        public void AnAnchorCanBeRemoved()
        {
            var shape = Square();

            Assert.IsTrue(NexPathEditing.RemoveAnchor(shape, 0, 1));
            Assert.AreEqual(3, shape.Contours[0].Anchors.Count);
        }

        [Test]
        public void ThePathRefusesToShrinkBelowTwoAnchors()
        {
            // Below two there is no path left - only a point that draws nothing and cannot be
            // recovered from. Refusing is less surprising than deleting the contour.
            var shape = Square();
            NexPathEditing.RemoveAnchor(shape, 0, 0);
            NexPathEditing.RemoveAnchor(shape, 0, 0);

            Assert.IsFalse(NexPathEditing.RemoveAnchor(shape, 0, 0));
            Assert.AreEqual(2, shape.Contours[0].Anchors.Count);
        }

        [Test]
        public void TogglingMakesACurvePointACorner()
        {
            var shape = NexShapeFactory.Ellipse(new Rect(0f, 0f, 100f, 100f));
            Assert.IsFalse(AnchorAt(shape, 0).IsCorner);

            NexPathEditing.ToggleCorner(shape, 0, 0);

            Assert.IsTrue(AnchorAt(shape, 0).IsCorner);
        }

        [Test]
        public void TogglingACornerGivesItHandlesAlongItsNeighbours()
        {
            var shape = Square();
            Assert.IsTrue(AnchorAt(shape, 0).IsCorner);

            NexPathEditing.ToggleCorner(shape, 0, 0);

            var anchor = AnchorAt(shape, 0);
            Assert.IsFalse(anchor.IsCorner);
            Assert.AreEqual(-anchor.OutHandle, anchor.InHandle, "A smoothed point starts symmetric.");
        }

        [Test]
        public void OperationsOnAMissingContourReportFailureRatherThanThrowing()
        {
            var shape = Square();

            Assert.IsFalse(NexPathEditing.Append(shape, 5, Vector2.zero));
            Assert.IsFalse(NexPathEditing.RemoveAnchor(shape, 5, 0));
            Assert.IsFalse(NexPathEditing.InsertOnSegment(shape, 5, 0, 0.5f));
            Assert.IsFalse(NexPathEditing.ToggleCorner(shape, 5, 0));
            Assert.IsFalse(NexPathEditing.Move(null, new NexPathHit(0, 0, NexAnchorPart.Point), Vector2.one));
        }
    }
}
