using emiteat.NexUI.Integrations.Figma;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class FigmaDocumentImporterTests
    {
        [Test]
        public void CredentialProjectKeyIsStableAcrossSeparatorStyles()
        {
            var windowsStyle = FigmaCredentials.ProjectKeyFor(@"C:\Work\NexUI\Assets", true);
            var unityStyle = FigmaCredentials.ProjectKeyFor("c:/work/nexui/assets/", true);
            Assert.AreEqual(windowsStyle, unityStyle);
            Assert.AreEqual(32, windowsStyle.Length);

            Assert.AreNotEqual(
                FigmaCredentials.ProjectKeyFor("/work/NexUI/Assets", false),
                FigmaCredentials.ProjectKeyFor("/work/nexui/Assets", false),
                "Linux and case-sensitive macOS volumes must keep distinct project identities.");
        }

        [Test]
        public void ImportFirstFrame_MapsHierarchyTextFillAndAutoLayout()
        {
            const string json = "{\"document\":{\"type\":\"DOCUMENT\",\"children\":[{\"type\":\"CANVAS\",\"children\":[{" +
                "\"id\":\"1\",\"name\":\"Inventory\",\"type\":\"FRAME\",\"layoutMode\":\"HORIZONTAL\",\"itemSpacing\":12," +
                "\"paddingLeft\":8,\"absoluteBoundingBox\":{\"x\":100,\"y\":200,\"width\":400,\"height\":300},\"children\":[{" +
                "\"id\":\"2\",\"name\":\"Title\",\"type\":\"TEXT\",\"characters\":\"Items\"," +
                "\"style\":{\"fontSize\":22},\"fills\":[{\"type\":\"SOLID\",\"visible\":true,\"opacity\":1,\"color\":{\"r\":1,\"g\":0.5,\"b\":0,\"a\":1}}]," +
                "\"absoluteBoundingBox\":{\"x\":120,\"y\":230,\"width\":80,\"height\":30}}]}]}]}}";
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                Assert.AreEqual(2, FigmaDocumentImporter.ImportFirstFrame(json, metadata));
                var frame = metadata.Find("Inventory");
                var title = metadata.Find("Title");
                Assert.NotNull(frame);
                Assert.NotNull(title);
                Assert.AreEqual("Inventory", title.parentId);
                Assert.AreEqual(new Rect(20, 30, 80, 30), title.rect);
                Assert.AreEqual("Label", title.elementType);
                Assert.AreEqual("Items", title.text);
                Assert.AreEqual(22, title.fontSize);
                Assert.AreEqual(new Color(1f, .5f, 0f, 1f), title.textColor);
                Assert.IsTrue(frame.autoLayout.enabled);
                Assert.AreEqual(DesignerAutoLayoutDirection.Row, frame.autoLayout.direction);
                Assert.AreEqual(12f, frame.autoLayout.spacing);
                Assert.AreEqual(8f, frame.autoLayout.paddingLeft);
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }

        // ---- JSON shape detection -----------------------------------------------------
        // The reader is a hand-written scanner rather than a JSON parser, so the cases that matter
        // are the ones where naive substring matching would be wrong: a key that only appears
        // nested, and a brace or quote inside a string value.

        [Test]
        public void Read_DevModeSingleNode_IsRecognisedWithoutAWrapper()
        {
            const string json = "{\"id\":\"1:2\",\"name\":\"Card\",\"type\":\"FRAME\"," +
                                "\"absoluteBoundingBox\":{\"x\":0,\"y\":0,\"width\":10,\"height\":10}}";

            var source = FigmaJsonReader.Read(json);

            Assert.IsTrue(source.IsValid);
            Assert.AreEqual(FigmaJsonShape.SingleNode, source.Shape);
            Assert.AreEqual(1, source.AvailableRoots);
        }

        [Test]
        public void Read_NodeArray_UsesFirstAndReportsHowManyWereOffered()
        {
            const string json = "[{\"name\":\"A\",\"type\":\"FRAME\"},{\"name\":\"B\",\"type\":\"FRAME\"}]";

            var source = FigmaJsonReader.Read(json);

            Assert.AreEqual(FigmaJsonShape.NodeArray, source.Shape);
            Assert.AreEqual(2, source.AvailableRoots);
            StringAssert.Contains("\"A\"", source.RootNodeJson);
            StringAssert.DoesNotContain("\"B\"", source.RootNodeJson);
        }

        [Test]
        public void Read_NodesResponse_UnwrapsTheDocumentOfTheFirstEntry()
        {
            const string json = "{\"nodes\":{\"1:2\":{\"document\":{\"name\":\"Picked\",\"type\":\"FRAME\"}}," +
                                "\"3:4\":{\"document\":{\"name\":\"Other\",\"type\":\"FRAME\"}}}}";

            var source = FigmaJsonReader.Read(json);

            Assert.AreEqual(FigmaJsonShape.NodesResponse, source.Shape);
            Assert.AreEqual(2, source.AvailableRoots);
            StringAssert.Contains("Picked", source.RootNodeJson);
            StringAssert.DoesNotContain("Other", source.RootNodeJson);
        }

        [Test]
        public void Read_DocumentKeyNestedInsideAChild_IsNotMistakenForAFileResponse()
        {
            // A substring search for "document" would match the child and import the wrong node.
            const string json = "{\"name\":\"Root\",\"type\":\"FRAME\"," +
                                "\"children\":[{\"name\":\"document\",\"type\":\"TEXT\"}]}";

            var source = FigmaJsonReader.Read(json);

            Assert.AreEqual(FigmaJsonShape.SingleNode, source.Shape);
            StringAssert.Contains("Root", source.RootNodeJson);
        }

        [Test]
        public void Read_BracesAndQuotesInsideStringValues_DoNotBreakScanning()
        {
            const string json = "{\"name\":\"a\\\"}{b\",\"type\":\"FRAME\"," +
                                "\"absoluteBoundingBox\":{\"x\":0,\"y\":0,\"width\":4,\"height\":4}}";

            var source = FigmaJsonReader.Read(json);

            Assert.IsTrue(source.IsValid, "An escaped quote inside a value must not end the object early.");
            Assert.AreEqual(FigmaJsonShape.SingleNode, source.Shape);
        }

        [Test]
        public void Read_JsonThatIsNotFigma_IsRejectedRatherThanImportedEmpty()
        {
            Assert.AreEqual(FigmaJsonShape.Unknown, FigmaJsonReader.Read("{\"hello\":\"world\"}").Shape);
            Assert.AreEqual(FigmaJsonShape.Unknown, FigmaJsonReader.Read("not json at all").Shape);
            Assert.AreEqual(FigmaJsonShape.Unknown, FigmaJsonReader.Read("").Shape);
            Assert.AreEqual(FigmaJsonShape.Unknown, FigmaJsonReader.Read("[]").Shape);
        }

        // ---- import through the shared mapper ------------------------------------------

        [Test]
        public void Import_DevModeNodeWithNodeLevelGeometry_MapsWithoutAbsoluteBoundingBox()
        {
            // Dev Mode and plugin exports often carry x/y/width/height on the node itself.
            const string json = "{\"name\":\"Panel\",\"type\":\"FRAME\",\"x\":50,\"y\":60,\"width\":200,\"height\":100," +
                                "\"children\":[{\"name\":\"Body\",\"type\":\"TEXT\",\"characters\":\"Hi\"," +
                                "\"x\":60,\"y\":80,\"width\":40,\"height\":20}]}";
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                var result = FigmaDocumentImporter.Import(json, metadata);

                Assert.AreEqual(2, result.ElementCount);
                Assert.AreEqual("Panel", result.FrameName);
                Assert.AreEqual(FigmaJsonShape.SingleNode, result.Shape);

                var body = metadata.Find("Body");
                Assert.NotNull(body);
                Assert.AreEqual(new Rect(10, 20, 40, 20), body.rect,
                    "Children are placed relative to the frame origin.");
                Assert.AreEqual("Hi", body.text);
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void Import_PastedFrameItself_IsUsedRatherThanADescendant()
        {
            // Searching for a FRAME before checking the root would import the inner frame and
            // silently drop everything the user actually selected.
            const string json = "{\"name\":\"Outer\",\"type\":\"FRAME\",\"x\":0,\"y\":0,\"width\":100,\"height\":100," +
                                "\"children\":[{\"name\":\"Inner\",\"type\":\"FRAME\",\"x\":10,\"y\":10,\"width\":20,\"height\":20}]}";
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                var result = FigmaDocumentImporter.Import(json, metadata);
                Assert.AreEqual("Outer", result.FrameName);
                Assert.AreEqual(2, result.ElementCount);
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void Import_NonFigmaJson_ThrowsInsteadOfClearingTheScreen()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                metadata.elements.Add(new DesignerElementMetadata { elementId = "Existing" });

                Assert.Throws<System.InvalidOperationException>(
                    () => FigmaDocumentImporter.Import("{\"hello\":\"world\"}", metadata));
                Assert.NotNull(metadata.Find("Existing"), "A rejected import must not have emptied the screen.");
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }

        [Test]
        public void ImportFirstFrame_MakesDuplicateNamesUnique()
        {
            const string json = "{\"document\":{\"type\":\"DOCUMENT\",\"children\":[{\"name\":\"Root\",\"type\":\"FRAME\",\"absoluteBoundingBox\":{\"x\":0,\"y\":0,\"width\":10,\"height\":10},\"children\":[{\"name\":\"Item\",\"type\":\"GROUP\",\"absoluteBoundingBox\":{\"x\":0,\"y\":0,\"width\":1,\"height\":1}},{\"name\":\"Item\",\"type\":\"GROUP\",\"absoluteBoundingBox\":{\"x\":1,\"y\":1,\"width\":1,\"height\":1}}]}]}}";
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            try
            {
                FigmaDocumentImporter.ImportFirstFrame(json, metadata);
                Assert.NotNull(metadata.Find("Item"));
                Assert.NotNull(metadata.Find("Item_2"));
            }
            finally
            {
                Object.DestroyImmediate(metadata);
            }
        }
    }
}
