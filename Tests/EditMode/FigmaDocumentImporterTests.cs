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
