using System.Linq;
using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The vector shape model, the presets built on it, and the SVG round trip.
    /// </summary>
    /// <remarks>
    /// The two conversions - NexUI anchors to Unity segments and back - are inverses of each other,
    /// and both are easy to get subtly wrong: a segment's two control points belong to two
    /// different anchors, and a closed contour repeats its first point. Round-tripping is what
    /// catches an off-by-one that would otherwise show up as a shape that drifts slightly every
    /// time it is imported and re-exported.
    /// </remarks>
    public sealed class NexVectorShapeTests
    {
        private static readonly Rect Box = new Rect(0f, 0f, 100f, 100f);

        // ---- presets --------------------------------------------------------

        [Test]
        public void APolygonHasOneAnchorPerSide()
        {
            var shape = NexShapeFactory.Polygon(Box, 6);

            Assert.AreEqual(1, shape.Contours.Count);
            Assert.AreEqual(6, shape.Contours[0].Anchors.Count);
            Assert.IsTrue(shape.Contours[0].Closed);
        }

        [Test]
        public void APolygonCannotHaveFewerThanThreeSides()
        {
            Assert.AreEqual(3, NexShapeFactory.Polygon(Box, 1).Contours[0].Anchors.Count);
        }

        [Test]
        public void AStarAlternatesOuterAndInnerRadius()
        {
            var anchors = NexShapeFactory.Star(Box, 5, innerRatio: 0.4f).Contours[0].Anchors;
            Assert.AreEqual(10, anchors.Count, "Five points means five tips and five valleys.");

            var centre = Box.center;
            var outer = Vector2.Distance(anchors[0].Position, centre);
            var inner = Vector2.Distance(anchors[1].Position, centre);

            Assert.Less(inner, outer);
            Assert.AreEqual(0.4f, inner / outer, 0.01f);
        }

        [Test]
        public void PresetsAreCornersUntilAPenToolTouchesThem()
        {
            // Every preset is an ordinary path, so it can be edited afterwards. A star with a
            // "points" slider and no way in would be a dead end.
            Assert.IsTrue(NexShapeFactory.Star(Box, 5).Contours[0].Anchors.All(a => a.IsCorner));
            Assert.IsTrue(NexShapeFactory.Polygon(Box, 5).Contours[0].Anchors.All(a => a.IsCorner));
        }

        [Test]
        public void AnEllipseIsCurvedRatherThanFaceted()
        {
            var anchors = NexShapeFactory.Ellipse(Box).Contours[0].Anchors;

            Assert.AreEqual(4, anchors.Count, "A circle needs four cubic segments, not a polygon.");
            Assert.IsFalse(anchors.Any(a => a.IsCorner));
        }

        [Test]
        public void ARingIsOneContourSoItStrokesAsOneOutline()
        {
            // Two contours would fill correctly and stroke wrongly - the stroke would trace each
            // circle separately instead of following the ring's actual outline.
            var shape = NexShapeFactory.Ring(Box, thickness: 20f);

            Assert.AreEqual(1, shape.Contours.Count);
        }

        [Test]
        public void AnArcIsStrokedNotFilled()
        {
            var shape = NexShapeFactory.Arc(Box, 0f, 90f, strokeWidth: 4f);

            Assert.IsFalse(shape.Filled);
            Assert.IsTrue(shape.HasStroke);
            Assert.IsFalse(shape.Contours[0].Closed);
        }

        [Test]
        public void ARoundedRectangleWithNoRadiusIsARectangle()
        {
            var rounded = NexShapeFactory.RoundedRectangle(Box, 0f);

            Assert.AreEqual(4, rounded.Contours[0].Anchors.Count);
        }

        [Test]
        public void PresetsStayInsideTheRectTheyWereGiven()
        {
            // Fitting inside, not centred on. A five-pointed star reaches its box at the top tip
            // and shares the bottom between two, so its bounds sit high - which is what a star
            // looks like in every drawing tool. Asserting a centred bounding box would be
            // asserting the wrong shape.
            var rect = new Rect(10f, 20f, 80f, 40f);

            foreach (var shape in new[]
                     {
                         NexShapeFactory.Rectangle(rect),
                         NexShapeFactory.Ellipse(rect),
                         NexShapeFactory.Polygon(rect, 6),
                         NexShapeFactory.Star(rect, 5)
                     })
            {
                var bounds = shape.Bounds();
                Assert.GreaterOrEqual(bounds.xMin, rect.xMin - 0.01f);
                Assert.LessOrEqual(bounds.xMax, rect.xMax + 0.01f);
                Assert.GreaterOrEqual(bounds.yMin, rect.yMin - 0.01f);
                Assert.LessOrEqual(bounds.yMax, rect.yMax + 0.01f);
            }
        }

        [Test]
        public void SymmetricPresetsAreCentredOnTheirRect()
        {
            // The ones that genuinely should be: a rectangle, an ellipse, and a polygon with an
            // even number of sides all have opposing points.
            var rect = new Rect(10f, 20f, 80f, 40f);

            foreach (var shape in new[]
                     {
                         NexShapeFactory.Rectangle(rect),
                         NexShapeFactory.Ellipse(rect),
                         NexShapeFactory.Polygon(rect, 6)
                     })
            {
                var bounds = shape.Bounds();
                Assert.AreEqual(rect.center.x, bounds.center.x, 0.01f);
                Assert.AreEqual(rect.center.y, bounds.center.y, 0.01f);
            }
        }

        // ---- tessellation ---------------------------------------------------

        [Test]
        public void AFilledShapeTessellatesIntoTriangles()
        {
            var meshes = NexVectorTessellator.Tessellate(NexShapeFactory.Polygon(Box, 5));

            Assert.IsNotEmpty(meshes);
            var mesh = meshes[0];
            Assert.GreaterOrEqual(mesh.Vertices.Length, 3);
            Assert.AreEqual(0, mesh.Indices.Length % 3, "Indices must come in whole triangles.");
        }

        [Test]
        public void AnEmptyShapeTessellatesToNothingRatherThanThrowing()
        {
            Assert.IsEmpty(NexVectorTessellator.Tessellate(new NexVectorShape()));
            Assert.IsEmpty(NexVectorTessellator.Tessellate(null));
        }

        [Test]
        public void ASmootherSettingProducesMoreGeometry()
        {
            var shape = NexShapeFactory.Ellipse(Box);

            var coarse = NexVectorTessellator.DefaultOptions;
            coarse.MaxCordDeviation = 5f;
            var fine = NexVectorTessellator.DefaultOptions;
            fine.MaxCordDeviation = 0.05f;

            var coarseCount = NexVectorTessellator.Tessellate(shape, coarse).Sum(m => m.Vertices.Length);
            var fineCount = NexVectorTessellator.Tessellate(shape, fine).Sum(m => m.Vertices.Length);

            Assert.Greater(fineCount, coarseCount);
        }

        // ---- SVG round trip --------------------------------------------------

        [Test]
        public void ImportingASquareGivesBackFourAnchors()
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'>"
                               + "<path d='M 0 0 L 100 0 L 100 100 L 0 100 Z' fill='#ff0000'/></svg>";

            var result = NexSvgImporter.Import(svg);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.AreEqual(1, result.Shapes.Count);

            var contour = result.Shapes[0].Contours[0];
            Assert.IsTrue(contour.Closed);
            Assert.AreEqual(4, contour.Anchors.Count,
                "The repeated closing point must not survive as a fifth anchor.");
        }

        [Test]
        public void ImportedFillColourIsPreserved()
        {
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'>"
                               + "<rect width='10' height='10' fill='#ff0000'/></svg>";

            var shape = NexSvgImporter.Import(svg).Shapes.First();

            Assert.IsTrue(shape.Filled);
            Assert.AreEqual(1f, shape.FillColor.r, 0.01f);
            Assert.AreEqual(0f, shape.FillColor.g, 0.01f);
        }

        [Test]
        public void AnImportedShapeSurvivesTessellation()
        {
            // The point of importing into the same model: an icon behaves like a drawn shape.
            const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'>"
                               + "<circle cx='10' cy='10' r='8' fill='#00ff00'/></svg>";

            var shape = NexSvgImporter.Import(svg).Shapes.First();

            Assert.IsNotEmpty(NexVectorTessellator.Tessellate(shape));
        }

        [Test]
        public void MalformedSvgIsReportedRatherThanThrown()
        {
            // Being handed a broken file is normal, not exceptional.
            var result = NexSvgImporter.Import("<svg><path d='M nonsense'></svg>");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotEmpty(result.Error);
            Assert.IsEmpty(result.Shapes);
        }

        [Test]
        public void EmptyInputIsReportedRatherThanThrown()
        {
            Assert.IsFalse(NexSvgImporter.Import("").Succeeded);
            Assert.IsFalse(NexSvgImporter.Import(null).Succeeded);
        }

        [Test]
        public void AnSvgWithNoShapesIsReported()
        {
            var result = NexSvgImporter.Import("<svg xmlns='http://www.w3.org/2000/svg'></svg>");

            Assert.IsFalse(result.Succeeded);
        }
    }
}
