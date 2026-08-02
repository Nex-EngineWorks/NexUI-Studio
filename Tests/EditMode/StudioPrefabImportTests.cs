using System;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Integrations.UGUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Prefab Import and the semantic round trip: a prefab the Studio never created can be read in,
    /// edited and written back without losing components, values, references or hierarchy.
    /// </summary>
    public sealed class StudioPrefabImportTests
    {
        private const string TempFolder = "Assets/NexUIStudioImportTests";
        private const string PrefabPath = TempFolder + "/Imported.prefab";
        private const string NestedPrefabPath = TempFolder + "/NestedPanel.prefab";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "NexUIStudioImportTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
        }

        /// <summary>
        /// A hand-built prefab of the kind a user would already have: no Studio tags anywhere, a
        /// wired-up controller, and a child whose values are not at their defaults.
        /// </summary>
        private static GameObject BuildHandAuthoredPrefab()
        {
            var root = new GameObject("HUD", typeof(RectTransform));

            var bar = new GameObject("HealthBar", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.sizeDelta = new Vector2(200f, 40f);
            barRect.anchoredPosition = new Vector2(30f, -20f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 1f);
            fillRect.sizeDelta = new Vector2(180f, 20f);
            fillRect.anchoredPosition = new Vector2(10f, -10f);
            var image = fill.GetComponent<Image>();
            image.color = new Color(0.2f, 0.8f, 0.3f, 1f);
            image.type = Image.Type.Filled;
            image.fillAmount = 0.75f;

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(bar.transform, false);
            label.GetComponent<TextMeshProUGUI>().text = "100 / 100";

            var controller = bar.AddComponent<SampleHealthBarController>();
            var so = new SerializedObject(controller);
            so.FindProperty("fill").objectReferenceValue = image;
            so.FindProperty("label").objectReferenceValue = label.GetComponent<TextMeshProUGUI>();
            so.FindProperty("smoothTime").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static DesignerMetadataAsset NewMetadata()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "hud";
            return metadata;
        }

        private static UIScreenDefinition NewScreen(GameObject prefab)
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "hud" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            return screen;
        }

        // ---- Structure ------------------------------------------------------------------------

        [Test]
        public void ImportBringsInTheHierarchyWithoutTheRoot()
        {
            var prefab = BuildHandAuthoredPrefab();

            var result = StudioPrefabImporter.Import(prefab);

            Assert.IsFalse(result.Report.HasErrors, result.Report.Details());
            Assert.AreEqual(3, result.Elements.Count, "HealthBar, Fill and Label - the root maps to the screen.");

            var bar = result.Elements.Find(e => e.displayName == "HealthBar");
            var fill = result.Elements.Find(e => e.displayName == "Fill");
            Assert.IsNotNull(bar);
            Assert.IsNotNull(fill);
            Assert.IsNull(bar.parentId, "A child of the prefab root is a root element.");
            Assert.AreEqual(bar.elementId, fill.parentId);
        }

        [Test]
        public void ImportPlacesElementsInAbsoluteCanvasSpace()
        {
            var prefab = BuildHandAuthoredPrefab();

            var result = StudioPrefabImporter.Import(prefab);
            var bar = result.Elements.Find(e => e.displayName == "HealthBar");
            var fill = result.Elements.Find(e => e.displayName == "Fill");

            Assert.AreEqual(30f, bar.rect.x, 0.01f);
            Assert.AreEqual(20f, bar.rect.y, 0.01f);
            Assert.AreEqual(new Vector2(200f, 40f), bar.rect.size);

            // Child rects are absolute, so Fill sits at the bar's origin plus its own offset.
            Assert.AreEqual(40f, fill.rect.x, 0.01f);
            Assert.AreEqual(30f, fill.rect.y, 0.01f);
            Assert.AreEqual(new Vector2(180f, 20f), fill.rect.size);
        }

        [Test]
        public void ImportCapturesEveryComponentIncludingUserScripts()
        {
            var prefab = BuildHandAuthoredPrefab();

            var result = StudioPrefabImporter.Import(prefab);
            var bar = result.Elements.Find(e => e.displayName == "HealthBar");

            var controller = bar.components.Find(
                c => c.assemblyQualifiedTypeName != null &&
                     c.assemblyQualifiedTypeName.StartsWith(typeof(SampleHealthBarController).FullName, StringComparison.Ordinal));
            Assert.IsNotNull(controller, "A project script on the prefab must come across.");
            Assert.AreEqual(DesignerComponentValueFormat.PropertyPath, controller.valueFormat);
            Assert.AreEqual(0.35f,
                DesignerComponentPropertyBag.Find(controller.properties, "smoothTime").floatValue, 1e-4f);
        }

        [Test]
        public void ImportTurnsInternalObjectReferencesIntoElementReferences()
        {
            var prefab = BuildHandAuthoredPrefab();

            var result = StudioPrefabImporter.Import(prefab);
            var bar = result.Elements.Find(e => e.displayName == "HealthBar");
            var fill = result.Elements.Find(e => e.displayName == "Fill");
            var controller = bar.components.Find(c => c.source == DesignerComponentSource.Project);

            var reference = DesignerComponentPropertyBag.Find(controller.properties, "fill").reference;
            Assert.AreEqual(DesignerReferenceKind.Element, reference.kind,
                "A reference inside the prefab must become an element reference, not an asset one.");
            Assert.AreEqual(fill.stableId, reference.stableElementId);
            StringAssert.Contains("Image", reference.componentTypeName);
        }

        [Test]
        public void ImportCapturesNonDefaultValuesOnRegistryComponentsToo()
        {
            var prefab = BuildHandAuthoredPrefab();

            var result = StudioPrefabImporter.Import(prefab);
            var fill = result.Elements.Find(e => e.displayName == "Fill");
            var image = fill.components.Find(c => c.assemblyQualifiedTypeName.StartsWith("UnityEngine.UI.Image", StringComparison.Ordinal));

            Assert.IsNotNull(image);
            Assert.AreEqual(DesignerComponentValueFormat.PropertyPath, image.valueFormat,
                "Imported values are keyed by property path so nothing outside the curated schema is lost.");
            Assert.AreEqual(0.75f,
                DesignerComponentPropertyBag.Find(image.properties, "m_FillAmount").floatValue, 1e-4f);
        }

        [Test]
        public void ImportNeverModifiesThePrefab()
        {
            var prefab = BuildHandAuthoredPrefab();
            var before = System.IO.File.ReadAllText(PrefabPath);

            StudioPrefabImporter.Import(prefab);
            AssetDatabase.SaveAssets();

            Assert.AreEqual(before, System.IO.File.ReadAllText(PrefabPath),
                "Reading a prefab must not write to it.");
        }

        // ---- Round trip ---------------------------------------------------------------------------

        [Test]
        public void ImportThenSaveThenImportKeepsTheSameValuesAndReferences()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            var screen = NewScreen(prefab);

            StudioPrefabImporter.ImportInto(metadata, prefab);
            var saveReport = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(saveReport.HasErrors, saveReport.Details());

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var second = StudioPrefabImporter.Import(reloaded);
            Assert.IsFalse(second.Report.HasErrors, second.Report.Details());

            var bar = second.Elements.Find(e => e.displayName == "HealthBar");
            var fill = second.Elements.Find(e => e.displayName == "Fill");
            Assert.IsNotNull(bar, "The hierarchy must survive the round trip.");
            Assert.IsNotNull(fill);

            var controller = bar.components.Find(c => c.source == DesignerComponentSource.Project);
            Assert.IsNotNull(controller, "The user script must still be there.");
            Assert.AreEqual(0.35f,
                DesignerComponentPropertyBag.Find(controller.properties, "smoothTime").floatValue, 1e-4f);

            var reference = DesignerComponentPropertyBag.Find(controller.properties, "fill").reference;
            Assert.AreEqual(DesignerReferenceKind.Element, reference.kind);
            Assert.AreEqual(fill.stableId, reference.stableElementId,
                "The reference must still point at the same element after a save/import cycle.");

            var image = fill.components.Find(c => c.assemblyQualifiedTypeName.StartsWith("UnityEngine.UI.Image", StringComparison.Ordinal));
            Assert.AreEqual(0.75f,
                DesignerComponentPropertyBag.Find(image.properties, "m_FillAmount").floatValue, 1e-4f);
        }

        [Test]
        public void SavingAnImportedScreenDoesNotDuplicateOrRenameTheUsersObjects()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            var screen = NewScreen(prefab);

            StudioPrefabImporter.ImportInto(metadata, prefab);
            new UGUIAssetSerializer().Save(screen, metadata);

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.AreEqual(3, contents.GetComponentsInChildren<Transform>(true).Length - 1,
                    "Saving an imported screen must reuse the existing objects, not add copies.");
                Assert.IsNotNull(contents.transform.Find("HealthBar"), "User object names must be preserved.");
                Assert.IsNotNull(contents.transform.Find("HealthBar/Fill"));
                Assert.IsNotNull(contents.transform.Find("HealthBar/Label"));

                var bar = contents.transform.Find("HealthBar").gameObject;
                Assert.AreEqual(1, bar.GetComponents<SampleHealthBarController>().Length,
                    "The imported controller must be associated with the existing component, not duplicated.");
                var fill = contents.transform.Find("HealthBar/Fill").gameObject;
                Assert.AreEqual(1, fill.GetComponents<Image>().Length,
                    "Registry components imported by property path must not be duplicated either.");

                foreach (var tag in contents.GetComponentsInChildren<NxUGuiBindingTag>(true))
                    Assert.AreEqual(NexUIElementOwnership.UserOwned, tag.ownership,
                        "Objects that existed before the Studio touched them stay user-owned.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void RemovingAnImportedEntryNeverDeletesTheOriginalComponent()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            var screen = NewScreen(prefab);

            StudioPrefabImporter.ImportInto(metadata, prefab);
            new UGUIAssetSerializer().Save(screen, metadata);
            var bar = metadata.elements.Find(e => e.displayName == "HealthBar");
            bar.components.RemoveAll(c => c.source == DesignerComponentSource.Project);

            new UGUIAssetSerializer().Save(screen, metadata);

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.IsNotNull(contents.transform.Find("HealthBar").GetComponent<SampleHealthBarController>(),
                    "An imported component belongs to the user's prefab and must survive entry removal.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void ReimportingUpdatesTheSameElementsInsteadOfDuplicatingThem()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            var screen = NewScreen(prefab);

            StudioPrefabImporter.ImportInto(metadata, prefab);
            new UGUIAssetSerializer().Save(screen, metadata);
            var countAfterFirst = metadata.elements.Count;

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            StudioPrefabImporter.ImportInto(metadata, reloaded);

            Assert.AreEqual(countAfterFirst, metadata.elements.Count,
                "Re-import matches on stable id, so nothing is duplicated.");
        }

        [Test]
        public void ImportPreservesStudioOnlyDataOnReimport()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            var screen = NewScreen(prefab);

            StudioPrefabImporter.ImportInto(metadata, prefab);
            new UGUIAssetSerializer().Save(screen, metadata);

            var bar = metadata.elements.Find(e => e.displayName == "HealthBar");
            bar.binding.textKey = "player.hp";
            bar.classes.Add("hud-bar");

            var reloaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            StudioPrefabImporter.ImportInto(metadata, reloaded);

            var after = metadata.elements.Find(e => e.displayName == "HealthBar");
            Assert.AreEqual("player.hp", after.binding.textKey,
                "A prefab has no opinion about bindings, so re-import must not clear them.");
            CollectionAssert.Contains(after.classes, "hud-bar");
        }

        [Test]
        public void AnElementWithNoObjectInThePrefabIsReportedNotDeleted()
        {
            var prefab = BuildHandAuthoredPrefab();
            var metadata = NewMetadata();
            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "planned-stable", elementId = "NotYetBuilt", elementType = "Panel"
            });

            var result = StudioPrefabImporter.ImportInto(metadata, prefab);

            Assert.IsNotNull(metadata.elements.Find(e => e.elementId == "NotYetBuilt"),
                "An element the prefab does not have yet must survive the import.");
            Assert.AreEqual(1, result.Report.Count(DesignerSaveImpactKind.Orphan));
        }

        [Test]
        public void DuplicateSiblingNamesGetUniqueElementIdsWithoutRenamingTheObjects()
        {
            var root = new GameObject("HUD", typeof(RectTransform));
            for (var i = 0; i < 2; i++)
            {
                var slot = new GameObject("Slot", typeof(RectTransform));
                slot.transform.SetParent(root.transform, false);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            var result = StudioPrefabImporter.Import(prefab);

            Assert.AreEqual(2, result.Elements.Count);
            Assert.AreNotEqual(result.Elements[0].elementId, result.Elements[1].elementId,
                "Element ids must be unique even when GameObject names are not.");
            Assert.AreEqual("Slot", result.Elements[0].displayName);
            Assert.AreEqual("Slot", result.Elements[1].displayName,
                "The GameObject's own name is never changed to make an id unique.");
        }

        [Test]
        public void NestedPrefabRelationshipSurvivesImportAndSave()
        {
            var panelRoot = new GameObject("NestedPanel", typeof(RectTransform));
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(panelRoot.transform, false);
            var nestedAsset = PrefabUtility.SaveAsPrefabAsset(panelRoot, NestedPrefabPath);
            UnityEngine.Object.DestroyImmediate(panelRoot);

            var screenRoot = new GameObject("HUD", typeof(RectTransform));
            PrefabUtility.InstantiatePrefab(nestedAsset, screenRoot.transform);
            var screenPrefab = PrefabUtility.SaveAsPrefabAsset(screenRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(screenRoot);

            var metadata = NewMetadata();
            StudioPrefabImporter.ImportInto(metadata, screenPrefab);
            var report = new UGUIAssetSerializer().Save(NewScreen(screenPrefab), metadata);
            Assert.IsFalse(report.HasErrors, report.Details());

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var nestedInstance = contents.transform.Find("NestedPanel").gameObject;
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(nestedInstance));
                Assert.AreEqual(nestedAsset,
                    PrefabUtility.GetCorrespondingObjectFromOriginalSource(nestedInstance),
                    "Studio tags and property overrides must not unpack a nested prefab instance.");
                Assert.IsNotNull(nestedInstance.transform.Find("Icon").GetComponent<Image>());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
