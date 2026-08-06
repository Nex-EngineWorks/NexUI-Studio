using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// The pen tool's correctness is almost entirely this transform. Every "the path squirms away
    /// from the cursor" bug is a failure of the round trip or of the rect invariant below.
    /// </summary>
    public sealed class DesignerVectorSpaceTests
    {
        private static NexVectorShape Square(float x, float y, float size)
        {
            var contour = new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(x, y)),
                new NexVectorAnchor(new Vector2(x + size, y)),
                new NexVectorAnchor(new Vector2(x + size, y + size)),
                new NexVectorAnchor(new Vector2(x, y + size))
            });

            var shape = new NexVectorShape();
            shape.Contours.Add(contour);
            return shape;
        }

        private static void AssertClose(Vector2 expected, Vector2 actual, string because)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, because + " (x)");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, because + " (y)");
        }

        [Test]
        public void CanvasAndShapeCoordinatesRoundTrip()
        {
            var shape = Square(0f, 0f, 10f);
            var rect = new Rect(100f, 50f, 200f, 400f);

            var original = new Vector2(3f, 7f);
            var canvas = DesignerVectorSpace.ShapeToCanvas(shape, rect, original);

            AssertClose(original, DesignerVectorSpace.CanvasToShape(shape, rect, canvas),
                "converting to the canvas and back must land on the point that was started from");
        }

        [Test]
        public void ShapeCornersLandOnTheRectCorners()
        {
            var shape = Square(-5f, -5f, 10f);
            var rect = new Rect(20f, 30f, 100f, 60f);

            AssertClose(rect.min, DesignerVectorSpace.ShapeToCanvas(shape, rect, new Vector2(-5f, -5f)),
                "the path's top-left must render at the element's top-left");
            AssertClose(rect.max, DesignerVectorSpace.ShapeToCanvas(shape, rect, new Vector2(5f, 5f)),
                "the path's bottom-right must render at the element's bottom-right");
        }

        [Test]
        public void BakingPutsThePathWhereItWasDrawn()
        {
            var shape = Square(0f, 0f, 10f);
            var rect = new Rect(64f, 32f, 100f, 200f);

            Assert.IsTrue(DesignerVectorSpace.Bake(shape, rect), "a path that is not already fitted must move");

            var bounds = shape.Bounds();
            AssertClose(rect.min, bounds.min, "after baking, the path's bounds must start at the rect");
            AssertClose(rect.size, bounds.size, "after baking, the path's bounds must fill the rect");
        }

        [Test]
        public void BakingIsIdempotent()
        {
            // The pen bakes before every edit, so a second bake happening on an already-fitted path
            // is the common case, not an edge case. If it moved anything, drawing would drift.
            var shape = Square(0f, 0f, 10f);
            var rect = new Rect(64f, 32f, 100f, 200f);

            DesignerVectorSpace.Bake(shape, rect);
            var afterFirst = shape.Bounds();

            Assert.IsFalse(DesignerVectorSpace.Bake(shape, rect), "re-baking a fitted path must be a no-op");
            AssertClose(afterFirst.min, shape.Bounds().min, "re-baking must not move the path");
            AssertClose(afterFirst.size, shape.Bounds().size, "re-baking must not resize the path");
        }

        [Test]
        public void BakingScalesHandlesButDoesNotTranslateThem()
        {
            // Handles are stored relative to their anchor, so carrying the offset into them would
            // move each one twice and the curve would explode away from the path.
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(0f, 0f), new Vector2(-1f, 0f), new Vector2(1f, 0f)),
                new NexVectorAnchor(new Vector2(10f, 10f))
            }));

            DesignerVectorSpace.Bake(shape, new Rect(500f, 500f, 20f, 30f));

            var anchor = shape.Contours[0].Anchors[0];
            AssertClose(new Vector2(-2f, 0f), anchor.InHandle, "the in handle must scale by the fit, not translate");
            AssertClose(new Vector2(2f, 0f), anchor.OutHandle, "the out handle must scale by the fit, not translate");
        }

        [Test]
        public void RectFollowsThePathOnceItHasArea()
        {
            var shape = Square(30f, 40f, 25f);
            var result = DesignerVectorSpace.RectFor(shape, new Rect(0f, 0f, 100f, 100f));

            Assert.AreEqual(new Rect(30f, 40f, 25f, 25f), result,
                "an element with a drawn path takes the path's bounds as its rect");
        }

        [Test]
        public void RectIsLeftAloneWhileNothingHasBeenDrawn()
        {
            // Otherwise starting a path on an existing element would teleport it to the origin
            // before the first point was even placed.
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour());

            var current = new Rect(64f, 64f, 240f, 96f);
            Assert.AreEqual(current, DesignerVectorSpace.RectFor(shape, current),
                "an empty path must not resize the element it is being drawn on");
        }

        [Test]
        public void ASinglePointKeepsTheElementSelectable()
        {
            // A zero-size rect cannot be clicked, so there would be no way back to the element the
            // first click was made on.
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new[] { new NexVectorAnchor(new Vector2(10f, 20f)) }, false));

            var result = DesignerVectorSpace.RectFor(shape, new Rect(0f, 0f, 240f, 96f));

            Assert.AreEqual(240f, result.width, "a one-point path keeps the element's width");
            Assert.AreEqual(96f, result.height, "a one-point path keeps the element's height");
            AssertClose(new Vector2(10f, 20f), result.min, "the rect still follows the point that was placed");
        }

        [Test]
        public void AStraightRunKeepsItsExtentOnTheFlatAxis()
        {
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(10f, 50f)),
                new NexVectorAnchor(new Vector2(90f, 50f))
            }, false));

            var result = DesignerVectorSpace.RectFor(shape, new Rect(0f, 0f, 240f, 96f));

            Assert.AreEqual(80f, result.width, 1e-3f, "the drawn axis takes the path's extent");
            Assert.AreEqual(96f, result.height, 1e-3f, "the flat axis keeps the element's extent");
        }

        [Test]
        public void APointStaysUnderTheCursorAcrossAnEdit()
        {
            // The whole reason the bake exists: append a point, let the rect follow, bake again -
            // and every earlier point must still be exactly where it was clicked.
            var shape = new NexVectorShape();
            shape.Contours.Add(new NexVectorContour(new System.Collections.Generic.List<NexVectorAnchor>(), false));
            var rect = new Rect(64f, 64f, 240f, 96f);

            var clicks = new[]
            {
                new Vector2(100f, 100f), new Vector2(180f, 120f),
                new Vector2(160f, 200f), new Vector2(90f, 170f)
            };

            foreach (var click in clicks)
            {
                DesignerVectorSpace.Bake(shape, rect);
                NexPathEditing.Append(shape, 0, click);
                rect = DesignerVectorSpace.RectFor(shape, rect);
            }

            DesignerVectorSpace.Bake(shape, rect);

            var anchors = shape.Contours[0].Anchors;
            for (var i = 0; i < clicks.Length; i++)
            {
                AssertClose(clicks[i], anchors[i].Position,
                    "point " + i + " must stay where it was clicked as later points are added");
            }
        }

        [Test]
        public void ResizingTheElementScalesThePathWithIt()
        {
            // This is the payoff of storing the path relative to the rect: an element that is made
            // twice as wide draws artwork that is twice as wide, with no path edit at all.
            var shape = Square(0f, 0f, 10f);
            var rect = new Rect(0f, 0f, 100f, 100f);
            DesignerVectorSpace.Bake(shape, rect);

            var widened = new Rect(0f, 0f, 200f, 100f);
            AssertClose(new Vector2(200f, 100f),
                DesignerVectorSpace.ShapeToCanvas(shape, widened, shape.Bounds().max),
                "the path's far corner follows the widened rect");
        }
    }
}
