using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// What the uGUI prefab save is allowed to own, and what it can express while owning it.
    /// </summary>
    /// <remarks>
    /// Three separate holes are covered here, and each one was invisible from the metadata alone:
    /// an <c>Animator</c> could not be attached because the type index only walked MonoBehaviours;
    /// a field shape the curated schema had no name for (a RectOffset, a Vector4) was silently
    /// dropped on registry components while the very same shape round-tripped on an unregistered
    /// script; and the report never said which half of the prefab the save had rewritten.
    /// </remarks>
    public sealed class StudioComponentCoverageTests
    {
        private const string TempFolder = "Assets/NexUIStudioComponentCoverage";
        private const string PrefabPath = TempFolder + "/Coverage.prefab";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "NexUIStudioComponentCoverage");
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
            StudioScratchComponentHost.Dispose();
        }

        // ---- Type index ---------------------------------------------------------------------------

        /// <summary>
        /// Animator is an engine component, not a script. Indexing only MonoBehaviours is what kept it
        /// - and every other built-in - out of Add Component, and therefore out of the writer's reach.
        /// </summary>
        [Test]
        public void BuiltInEngineComponentsAreAttachable()
        {
            Assert.IsNotNull(StudioComponentTypeIndex.Find(typeof(Animator)),
                "Animator must be attachable; it is the component the known-limitations entry named");
            Assert.IsNotNull(StudioComponentTypeIndex.Find(typeof(AudioSource)));
            Assert.AreEqual(StudioComponentOrigin.Unity, StudioComponentTypeIndex.Find(typeof(Animator)).Origin);
        }

        /// <summary>The exclusions exist because something else already owns those components.</summary>
        [Test]
        public void ComponentsOwnedByTheElementItselfAreNotOffered()
        {
            Assert.IsNull(StudioComponentTypeIndex.Find(typeof(Transform)));
            Assert.IsNull(StudioComponentTypeIndex.Find(typeof(RectTransform)));
            Assert.IsNull(StudioComponentTypeIndex.Find(typeof(CanvasRenderer)));
            Assert.IsNull(StudioComponentTypeIndex.Find(typeof(DesignerAttachedComponentTracker)));
        }

        [Test]
        public void MonoBehavioursAreStillIndexed()
        {
            Assert.IsNotNull(StudioComponentTypeIndex.Find(typeof(Image)));
            Assert.IsNotNull(StudioComponentTypeIndex.Find(typeof(VerticalLayoutGroup)));
        }

        // ---- One value path -----------------------------------------------------------------------

        [Test]
        public void RegistryComponentsWithARuntimeTypeUseTheUniversalValuePath()
        {
            var element = Element("panel");
            var image = DesignerElementComponentAccess.Attach(element, "UGUI.Image", DesignerUIComponentFamily.UGUI);

            Assert.AreEqual(DesignerComponentValueFormat.PropertyPath, image.valueFormat);
            Assert.IsTrue(image.adoptExistingComponent,
                "the old writer called GetComponent before AddComponent; adopting is what preserves that");
        }

        /// <summary>A required component is attached by the same rule as the one that pulled it in.</summary>
        [Test]
        public void RequiredComponentsGetTheSameValuePath()
        {
            var element = Element("panel");
            DesignerElementComponentAccess.Attach(element, "UGUI.Button", DesignerUIComponentFamily.UGUI);

            var image = Find(element, "UGUI.Image");
            Assert.AreEqual(DesignerComponentValueFormat.PropertyPath, image.valueFormat);
            Assert.IsTrue(image.adoptExistingComponent);
        }

        /// <summary>
        /// A UI Toolkit control is generated into UXML and has no <c>System.Type</c>, so it stays on the
        /// curated schema. Sending it down the property-path writer would report it as a missing script.
        /// </summary>
        [Test]
        public void ToolkitControlsStayOnTheCuratedSchema()
        {
            var element = Element("panel");
            var slider = DesignerElementComponentAccess.Attach(element, "UITK.Slider", DesignerUIComponentFamily.UIToolkit);

            Assert.AreEqual(DesignerComponentValueFormat.SchemaKeys, slider.valueFormat);
            Assert.IsFalse(slider.adoptExistingComponent);
        }

        [Test]
        public void SwappingBackendsMovesTheEntryToThePathItsNewTypeHas()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = Element("panel");
            DesignerElementComponentAccess.Attach(element, "UGUI.Slider", DesignerUIComponentFamily.UGUI);
            metadata.elements.Add(element);

            Assert.AreEqual(1, DesignerElementComponentValidation.ReplaceUnsupported(
                metadata, DesignerUIComponentFamily.UIToolkit, out _));

            var slider = Find(element, "UITK.Slider");
            Assert.AreEqual(DesignerComponentValueFormat.SchemaKeys, slider.valueFormat,
                "a Toolkit control has no runtime type for the property-path writer to resolve");
            Assert.IsFalse(slider.adoptExistingComponent);
        }

        /// <summary>
        /// The shapes the curated schema had no builder for. <c>DesignerReflectedSchema</c> returned null
        /// for each of these and the field vanished from both the inspector and the save.
        /// </summary>
        [Test]
        public void ShapesTheCuratedSchemaCannotNameSurviveARoundTrip()
        {
            var element = Element("panel");
            var mask = DesignerElementComponentAccess.Attach(element, "UGUI.RectMask2D", DesignerUIComponentFamily.UGUI);

            var serialized = StudioSerializedComponentBridge.Load(mask, typeof(RectMask2D));
            Assert.IsNotNull(serialized);
            serialized.FindProperty("m_Padding").vector4Value = new Vector4(1f, 2f, 3f, 4f);
            serialized.FindProperty("m_Softness").vector2IntValue = new Vector2Int(5, 6);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(StudioSerializedComponentBridge.Capture(serialized, typeof(RectMask2D), mask));

            var reloaded = StudioSerializedComponentBridge.Load(mask, typeof(RectMask2D));
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 4f), reloaded.FindProperty("m_Padding").vector4Value);
            Assert.AreEqual(new Vector2Int(5, 6), reloaded.FindProperty("m_Softness").vector2IntValue);
        }

        /// <summary>
        /// Readers written against the curated key space predate the move to property paths and must
        /// keep working: a schema key is the backing field name minus <c>m_</c>.
        /// </summary>
        [Test]
        public void SchemaKeyReadsResolveAgainstPropertyPathValues()
        {
            var element = Element("bar");
            var component = DesignerElementComponentAccess.Attach(element, "NX.SegmentedBar", DesignerUIComponentFamily.UGUI);
            DesignerComponentPropertyBag.Set(component.properties, "m_Segments",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = 9 });

            Assert.AreEqual(9, DesignerElementComponentAccess.GetInt(component, "segments"));
            Assert.IsTrue(DesignerElementComponentAccess.IsOverridden(component, "segments"));
        }

        [Test]
        public void NestedPathsAreNeverGuessedIntoASchemaKey()
        {
            var element = Element("panel");
            var component = DesignerElementComponentAccess.Attach(element, "UGUI.Button", DesignerUIComponentFamily.UGUI);
            DesignerComponentPropertyBag.Set(component.properties, "m_Colors.m_NormalColor",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = Color.red });

            Assert.IsFalse(DesignerElementComponentAccess.IsOverridden(component, "colors"),
                "a nested path has no curated counterpart, so no counterpart may be invented");
        }

        // ---- The prefab, and what the save says about it -------------------------------------------

        /// <summary>
        /// A RectOffset never reached the prefab before: the schema dropped <c>m_Padding</c>, so a
        /// layout group saved with Unity's defaults no matter what the user set.
        /// </summary>
        [Test]
        public void AShapeTheOldSchemaDroppedNowReachesThePrefab()
        {
            var prefab = CreatePrefab();
            var metadata = CreateMetadata();
            var panel = metadata.Find("Panel");

            var group = DesignerElementComponentAccess.Attach(panel, "UGUI.VerticalLayoutGroup",
                DesignerUIComponentFamily.UGUI);
            var serialized = StudioSerializedComponentBridge.Load(group, typeof(VerticalLayoutGroup));
            serialized.FindProperty("m_Padding.m_Left").intValue = 11;
            serialized.FindProperty("m_Padding.m_Bottom").intValue = 13;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            StudioSerializedComponentBridge.Capture(serialized, typeof(VerticalLayoutGroup), group);

            var report = new UGUIAssetSerializer().Save(CreateScreen(prefab), metadata);
            CollectionAssert.IsEmpty(report.Errors);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var written = saved.transform.Find("Panel").GetComponent<VerticalLayoutGroup>();
            Assert.IsNotNull(written, "the component the element declares must exist on the prefab");
            Assert.AreEqual(11, written.padding.left);
            Assert.AreEqual(13, written.padding.bottom);
        }

        /// <summary>The declared component is bound to the one already there, never duplicated.</summary>
        [Test]
        public void SavingTwiceDoesNotAddASecondComponent()
        {
            var prefab = CreatePrefab();
            var metadata = CreateMetadata();
            DesignerElementComponentAccess.Attach(metadata.Find("Panel"), "UGUI.Image", DesignerUIComponentFamily.UGUI);

            var screen = CreateScreen(prefab);
            new UGUIAssetSerializer().Save(screen, metadata);
            new UGUIAssetSerializer().Save(screen, metadata);

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.AreEqual(1, saved.transform.Find("Panel").GetComponents<Image>().Length);
        }

        /// <summary>
        /// The report has to state the boundary, not only its consequences: without it, "Preserved
        /// user-authored X" reads as a failure rather than as the ownership rule working.
        /// </summary>
        [Test]
        public void TheReportStatesWhatTheSaveOwnedAndWhatItLeftAlone()
        {
            var prefab = CreatePrefab();
            var metadata = CreateMetadata();
            var screen = CreateScreen(prefab);

            // First save materializes the elements so there is something to add a component to.
            new UGUIAssetSerializer().Save(screen, metadata);

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                contents.transform.Find("Panel").gameObject.AddComponent<AudioSource>();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            var report = new UGUIAssetSerializer().Save(screen, metadata);

            CollectionAssert.IsNotEmpty(report.Ownership, "the overwrite scope must always be stated");
            StringAssert.Contains("AudioSource", string.Join("\n", report.Ownership),
                "a component the user added in the Prefab is the manual half of the boundary");
            StringAssert.Contains("Overwrite scope", report.Details());

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(saved.transform.Find("Panel").GetComponent<AudioSource>(),
                "stating that it was left alone is only true if it really was");
        }

        [Test]
        public void ADeclaredComponentIsNotReportedAsManual()
        {
            var prefab = CreatePrefab();
            var metadata = CreateMetadata();
            DesignerElementComponentAccess.Attach(metadata.Find("Panel"), "UGUI.Image", DesignerUIComponentFamily.UGUI);

            var screen = CreateScreen(prefab);
            new UGUIAssetSerializer().Save(screen, metadata);
            var report = new UGUIAssetSerializer().Save(screen, metadata);

            StringAssert.DoesNotContain("Image", string.Join("\n", report.Ownership));
        }

        // ---- Fixture ------------------------------------------------------------------------------

        private static DesignerElementMetadata Element(string id) => new DesignerElementMetadata
        {
            elementId = id,
            stableId = Guid.NewGuid().ToString("N"),
            elementType = "Panel",
            rect = new Rect(0f, 0f, 100f, 40f)
        };

        private static GameObject CreatePrefab()
        {
            var root = new GameObject("Coverage", typeof(RectTransform));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static DesignerMetadataAsset CreateMetadata()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "coverage";
            metadata.elements.Add(Element("Panel"));
            return metadata;
        }

        private static UIScreenDefinition CreateScreen(GameObject prefab)
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "coverage" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            return screen;
        }

        private static DesignerElementComponent Find(DesignerElementMetadata element, string typeId)
        {
            foreach (var component in element.components ?? new List<DesignerElementComponent>())
                if (component != null && component.typeId == typeId) return component;
            Assert.Fail($"{typeId} was not attached");
            return null;
        }
    }
}
