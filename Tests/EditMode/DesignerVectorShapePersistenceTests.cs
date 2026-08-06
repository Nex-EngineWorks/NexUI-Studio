using emiteat.NexUI.Designer;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Vector;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Tests.EditMode
{
    /// <summary>
    /// Locks the paths a drawn shape has to survive.
    /// </summary>
    /// <remarks>
    /// <c>vectorShape</c> was originally <c>[SerializeReference]</c>, which
    /// <see cref="JsonUtility"/> does not serialize - so a path lived in the <c>.asset</c> but was
    /// silently dropped by duplicate, paste and every companion-JSON round trip. Nothing errored;
    /// the artwork was just gone. These tests exist so that cannot come back quietly.
    /// </remarks>
    public sealed class DesignerVectorShapePersistenceTests
    {
        private static DesignerElementMetadata ElementWithTriangle()
        {
            var element = new DesignerElementMetadata
            {
                elementId = "drawn",
                rect = new Rect(10f, 20f, 60f, 40f),
                hasShape = true
            };

            element.vectorShape = new NexVectorShape { Filled = true, FillColor = Color.red };
            element.vectorShape.Contours.Add(new NexVectorContour(new[]
            {
                new NexVectorAnchor(new Vector2(0f, 0f), Vector2.zero, new Vector2(3f, 0f)),
                new NexVectorAnchor(new Vector2(10f, 0f)),
                new NexVectorAnchor(new Vector2(5f, 8f))
            }));

            return element;
        }

        private static void AssertTriangleSurvived(DesignerElementMetadata element, string because)
        {
            Assert.IsTrue(element.hasShape, because + ": the element must still report a shape");
            Assert.IsNotNull(element.vectorShape, because + ": the shape must not be null");
            Assert.AreEqual(1, element.vectorShape.Contours.Count, because + ": contour count");

            var anchors = element.vectorShape.Contours[0].Anchors;
            Assert.AreEqual(3, anchors.Count, because + ": anchor count");
            Assert.AreEqual(new Vector2(5f, 8f), anchors[2].Position, because + ": the last anchor's position");
            Assert.AreEqual(new Vector2(3f, 0f), anchors[0].OutHandle, because + ": handles must survive too");
            Assert.AreEqual(Color.red, element.vectorShape.FillColor, because + ": fill colour");
        }

        [Test]
        public void CloningAnElementKeepsItsPath()
        {
            var clone = DesignerMetadataUtility.Clone(ElementWithTriangle());
            AssertTriangleSurvived(clone, "duplicating a drawn element");
        }

        [Test]
        public void CloningGivesThePathItsOwnCopy()
        {
            // A shared reference would make editing the duplicate silently change the original,
            // which is worse than losing the shape because it looks like it worked.
            var source = ElementWithTriangle();
            var clone = DesignerMetadataUtility.Clone(source);

            clone.vectorShape.Contours[0].Anchors.Clear();

            Assert.AreEqual(3, source.vectorShape.Contours[0].Anchors.Count,
                "editing the duplicate's path must not touch the original's");
        }

        [Test]
        public void AnElementWithNoPathStaysThatWay()
        {
            var element = new DesignerElementMetadata { elementId = "plain" };
            var clone = DesignerMetadataUtility.Clone(element);

            Assert.IsFalse(clone.hasShape,
                "an element nobody drew on must not come back from a clone claiming a shape");
        }

        [Test]
        public void ThePathSurvivesTheCompanionJson()
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                asset.screenId = "vector-round-trip";
                asset.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
                asset.elements.Add(ElementWithTriangle());

                var json = DesignerMetadataJsonSerializer.ToJson(asset);

                var restored = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
                try
                {
                    Assert.IsTrue(DesignerMetadataJsonSerializer.FromJson(json, restored),
                        "the exported JSON must parse back");
                    Assert.AreEqual(1, restored.elements.Count, "the element itself must survive");
                    AssertTriangleSurvived(restored.elements[0], "a companion-JSON round trip");
                }
                finally
                {
                    Object.DestroyImmediate(restored);
                }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
