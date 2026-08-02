using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Serialization;
using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The vertical slice the universal component system is judged by: add a project MonoBehaviour to
    /// an element, edit it, wire it to sibling elements, save the prefab and open it again with
    /// everything intact.
    /// </summary>
    /// <remarks>
    /// These tests deliberately go through the real serializer and a real prefab on disk rather than
    /// asserting on metadata alone. "Stored in metadata" and "works in the prefab" were exactly the
    /// two things that used to be confused for each other.
    /// </remarks>
    public sealed class StudioVerticalSliceTests
    {
        private const string TempFolder = "Assets/NexUIStudioVerticalSlice";
        private const string PrefabPath = TempFolder + "/HealthBar.prefab";

        private static readonly Type ControllerType = typeof(SampleHealthBarController);

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "NexUIStudioVerticalSlice");
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
            StudioScratchComponentHost.Dispose();
        }

        // ---- Fixture ------------------------------------------------------------------------------

        /// <summary>An empty prefab with just a root, the way a new uGUI screen starts.</summary>
        private static GameObject CreatePrefab()
        {
            var root = new GameObject("HealthBar", typeof(RectTransform));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static DesignerMetadataAsset CreateMetadata()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            metadata.screenId = "hud";

            metadata.elements.Add(new DesignerElementMetadata
            {
                stableId = "bar-stable", elementId = "HealthBar", elementType = "Panel"
            });
            var fill = new DesignerElementMetadata
            {
                stableId = "fill-stable", elementId = "Fill", elementType = "Image", parentId = "HealthBar"
            };
            fill.components.Add(new DesignerElementComponent("UGUI.Image")
                { source = DesignerComponentSource.UGUI });
            metadata.elements.Add(fill);

            var label = new DesignerElementMetadata
            {
                stableId = "label-stable", elementId = "Label", elementType = "Text", parentId = "HealthBar"
            };
            label.components.Add(new DesignerElementComponent("UGUI.TextMeshProUGUI")
                { source = DesignerComponentSource.UGUI });
            metadata.elements.Add(label);

            return metadata;
        }

        private static UIScreenDefinition CreateScreen(GameObject prefab)
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = "hud" };
            screen.backendAsset = new UIScreenBackendAsset { backend = UIRenderBackend.UGUI, asset = prefab };
            return screen;
        }

        /// <summary>Adds the controller and wires it exactly as the inspector would.</summary>
        private static DesignerElementComponent Wire(DesignerElementMetadata bar, float smoothTime = 0.25f)
        {
            var component = DesignerElementComponentAccess.AttachProject(bar, ControllerType);

            SetFloat(component, "smoothTime", smoothTime);
            SetElementReference(component, "fill", "fill-stable", typeof(Image));
            SetElementReference(component, "label", "label-stable", typeof(TextMeshProUGUI));
            return component;
        }

        private static void SetFloat(DesignerElementComponent component, string key, float value)
            => DesignerComponentPropertyBag.Set(component.properties, key,
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = value });

        private static void SetElementReference(DesignerElementComponent component, string key,
            string stableId, Type componentType)
            => DesignerComponentPropertyBag.Set(component.properties, key, new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.ElementReference,
                reference = new DesignerObjectReference
                {
                    kind = DesignerReferenceKind.Element,
                    stableElementId = stableId,
                    componentTypeName = StudioComponentTypeIndex.Identity(componentType)
                }
            });

        private static SampleHealthBarController Saved(GameObject prefabContents)
            => prefabContents.GetComponentInChildren<SampleHealthBarController>(includeInactive: true);

        // ---- 1-4: discovery and attachment ---------------------------------------------------------

        [Test]
        public void Condition_1to4_TheControllerIsFoundAndLandsInTheOneComponentStack()
        {
            var found = false;
            foreach (var entry in StudioComponentTypeIndex.Search("SampleHealthBarController"))
                if (entry.Type == ControllerType) { found = true; break; }
            Assert.IsTrue(found, "Add Component must be able to find a plain project MonoBehaviour.");

            var bar = new DesignerElementMetadata { stableId = "bar-stable", elementId = "HealthBar" };
            var component = DesignerElementComponentAccess.AttachProject(bar, ControllerType);

            Assert.IsNotNull(component);
            CollectionAssert.Contains(bar.components, component);
            Assert.IsEmpty(bar.attachedComponents, "Nothing may be written to the legacy list.");
            Assert.AreEqual(DesignerComponentSource.Project, component.source);
        }

        // ---- 5: the three fields are editable ------------------------------------------------------

        [Test]
        public void Condition_5_AllThreeSerializedFieldsAreExposed()
        {
            var bar = new DesignerElementMetadata { stableId = "bar-stable", elementId = "HealthBar" };
            var component = DesignerElementComponentAccess.AttachProject(bar, ControllerType);

            var serializedObject = StudioSerializedComponentBridge.Load(component, ControllerType);
            Assert.IsNotNull(serializedObject, "A scratch instance is required to edit any component.");

            var paths = new List<string>();
            foreach (var property in StudioSerializedComponentBridge.Leaves(serializedObject))
                paths.Add(property.propertyPath);

            CollectionAssert.Contains(paths, "fill");
            CollectionAssert.Contains(paths, "label");
            CollectionAssert.Contains(paths, "smoothTime");
        }

        [Test]
        public void Condition_8_EditingAValueStoresOnlyTheDifferenceFromUnitysDefault()
        {
            var bar = new DesignerElementMetadata { stableId = "bar-stable", elementId = "HealthBar" };
            var component = DesignerElementComponentAccess.AttachProject(bar, ControllerType);

            var serializedObject = StudioSerializedComponentBridge.Load(component, ControllerType);
            serializedObject.FindProperty("smoothTime").floatValue = 0.25f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            StudioSerializedComponentBridge.Capture(serializedObject, ControllerType, component);

            Assert.AreEqual(0.25f,
                DesignerComponentPropertyBag.Find(component.properties, "smoothTime").floatValue, 1e-5f);

            // Back to the script's own default: the entry must disappear rather than be stored as a
            // value the Studio invented.
            serializedObject.FindProperty("smoothTime").floatValue = 0.1f;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            StudioSerializedComponentBridge.Capture(serializedObject, ControllerType, component);

            Assert.IsNull(DesignerComponentPropertyBag.Find(component.properties, "smoothTime"));
        }

        // ---- 6, 7, 10, 11: references reach the prefab ---------------------------------------------

        [Test]
        public void Condition_6_7_10_11_ReferencesAndValuesReachTheRealPrefab()
        {
            var prefab = CreatePrefab();
            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();
            Wire(metadata.Find("HealthBar"));

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(report.HasErrors, report.Details());

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var controller = Saved(contents);
                Assert.IsNotNull(controller, "The MonoBehaviour itself must exist on the prefab.");
                Assert.AreEqual(0.25f, controller.SmoothTime, 1e-5f);
                Assert.IsNotNull(controller.Fill, "fill must resolve to the child element's Image.");
                Assert.AreEqual("Fill", controller.Fill.gameObject.name);
                Assert.IsNotNull(controller.Label, "label must resolve to the child element's TMP_Text.");
                Assert.AreEqual("Label", controller.Label.gameObject.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ---- 13: the values come back ---------------------------------------------------------------

        [Test]
        public void Condition_13_ValuesSurviveAMetadataSaveAndReload()
        {
            var metadata = CreateMetadata();
            Wire(metadata.Find("HealthBar"), smoothTime: 0.42f);

            var path = TempFolder + "/Hud.Metadata.asset";
            AssetDatabase.CreateAsset(metadata, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var loaded = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(path);
            var component = loaded.Find("HealthBar").components
                .Find(c => c.source == DesignerComponentSource.Project);

            Assert.IsNotNull(component);
            Assert.AreEqual(ControllerType.FullName + ", " + ControllerType.Assembly.GetName().Name,
                component.assemblyQualifiedTypeName);
            Assert.AreEqual(0.42f,
                DesignerComponentPropertyBag.Find(component.properties, "smoothTime").floatValue, 1e-5f);

            var fill = DesignerComponentPropertyBag.Find(component.properties, "fill").reference;
            Assert.AreEqual(DesignerReferenceKind.Element, fill.kind);
            Assert.AreEqual("fill-stable", fill.stableElementId);

            // And the restored metadata still drives a real inspector.
            var serializedObject = StudioSerializedComponentBridge.Load(component, ControllerType);
            Assert.AreEqual(0.42f, serializedObject.FindProperty("smoothTime").floatValue, 1e-5f);
        }

        // ---- 14, 15: duplication re-points internal references --------------------------------------

        [Test]
        public void Condition_14_DuplicationRePointsReferencesAtTheCopiesOwnChildren()
        {
            var metadata = CreateMetadata();
            var component = Wire(metadata.Find("HealthBar"));

            var bar = metadata.Find("HealthBar");
            var clone = DesignerMetadataUtility.Clone(bar);
            clone.stableId = "bar-copy-stable";
            DesignerMetadataUtility.RemapComponentReferences(clone,
                new Dictionary<string, string> { { "fill-stable", "fill-copy-stable" } });

            var cloned = clone.components.Find(c => c.source == DesignerComponentSource.Project);
            Assert.AreNotEqual(component.instanceId, cloned.instanceId,
                "A copy is a different attachment and needs its own identity.");
            Assert.AreEqual("fill-copy-stable",
                DesignerComponentPropertyBag.Find(cloned.properties, "fill").reference.stableElementId);
            Assert.AreEqual("label-stable",
                DesignerComponentPropertyBag.Find(cloned.properties, "label").reference.stableElementId,
                "A target outside the copied set must keep pointing at the original.");
        }

        // ---- 16: saving twice must not keep dirtying the prefab --------------------------------------

        [Test]
        public void Condition_16_SavingTheSameMetadataTwiceLeavesThePrefabUnchanged()
        {
            var prefab = CreatePrefab();
            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();
            Wire(metadata.Find("HealthBar"));

            new UGUIAssetSerializer().Save(screen, metadata);
            var firstWrite = System.IO.File.ReadAllText(PrefabPath);

            var second = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(second.HasErrors, second.Details());
            var secondWrite = System.IO.File.ReadAllText(PrefabPath);

            Assert.AreEqual(firstWrite, secondWrite,
                "An unchanged screen must produce an identical prefab, or every save is a source-control diff.");
        }

        // ---- Ownership: the user's own components are never touched -----------------------------------

        [Test]
        public void AUserAddedComponentIsNeverRemovedBySaving()
        {
            // The prefab root already carries the element's identity, so the Studio writes onto the
            // very object the user has been editing by hand - the case where deleting the wrong
            // component would actually hurt.
            var root = new GameObject("HealthBar", typeof(RectTransform));
            var tag = root.AddComponent<emiteat.NexUI.Integrations.UGUI.NxUGuiBindingTag>();
            tag.stableId = "bar-stable";
            tag.elementId = "HealthBar";
            tag.ownership = emiteat.NexUI.Integrations.UGUI.NexUIElementOwnership.UserOwned;
            root.AddComponent<SampleHealthBarController>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();
            Wire(metadata.Find("HealthBar"));

            var report = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(report.HasErrors, report.Details());

            var saved = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var controllers = saved.GetComponents<SampleHealthBarController>();
                Assert.AreEqual(2, controllers.Length,
                    "The user's own component must survive alongside the Studio-owned one.");

                // Only one of the two is ours, and it is the one that got wired.
                var tracker = saved.GetComponent<DesignerAttachedComponentTracker>();
                Assert.IsNotNull(tracker);
                Assert.AreEqual(1, tracker.managedByInstance.Count);
                var owned = (SampleHealthBarController)tracker.managedByInstance[0].component;
                Assert.AreEqual(0.25f, owned.SmoothTime, 1e-5f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
            }
        }

        [Test]
        public void RemovingTheStackEntryRemovesOnlyTheStudioOwnedComponent()
        {
            var prefab = CreatePrefab();
            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();
            var component = Wire(metadata.Find("HealthBar"));

            new UGUIAssetSerializer().Save(screen, metadata);

            metadata.Find("HealthBar").components.RemoveAll(c => c.instanceId == component.instanceId);
            var report = new UGUIAssetSerializer().Save(screen, metadata);
            Assert.IsFalse(report.HasErrors, report.Details());

            var saved = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Assert.IsNull(Saved(saved), "A removed stack entry must remove the component it created.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(saved);
            }
        }

        // ---- Missing data is reported, never deleted ---------------------------------------------------

        [Test]
        public void AnUnresolvableScriptIsReportedAndItsValuesPreserved()
        {
            var prefab = CreatePrefab();
            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();

            var bar = metadata.Find("HealthBar");
            var ghost = new DesignerElementComponent("Project:Gone.Script")
            {
                source = DesignerComponentSource.Project,
                assemblyQualifiedTypeName = "Gone.Script, NotLoadedAssembly"
            };
            ghost.properties.Add(new DesignerComponentPropertyEntry("speed",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 3f }));
            bar.components.Add(ghost);

            var report = new UGUIAssetSerializer().Save(screen, metadata);

            Assert.AreEqual(1, report.Count(DesignerSaveImpactKind.Unsupported),
                "A missing script must be reported.");
            Assert.AreEqual(1, ghost.properties.Count, "Its stored values must survive the save untouched.");
        }

        [Test]
        public void AReferenceToAMissingElementIsAnErrorThatBlocksTheWrite()
        {
            var prefab = CreatePrefab();
            var screen = CreateScreen(prefab);
            var metadata = CreateMetadata();

            var component = DesignerElementComponentAccess.AttachProject(metadata.Find("HealthBar"), ControllerType);
            SetElementReference(component, "fill", "element-that-does-not-exist", typeof(Image));

            var report = new UGUIAssetSerializer().Save(screen, metadata);

            Assert.IsTrue(report.HasErrors, "Silently writing null would surface as a NullReference at runtime.");
            StringAssert.Contains("element-that-does-not-exist", string.Join("\n", report.Errors));
        }

        [Test]
        public void SerializeReferenceRoundTripsThroughTheUniversalPropertyBag()
        {
            var sourceObject = new GameObject("ManagedReferenceSource");
            var targetObject = new GameObject("ManagedReferenceTarget");
            try
            {
                var source = sourceObject.AddComponent<SampleAdvancedSerializationController>();
                var sourceProperty = new SerializedObject(source).FindProperty("rule");
                Assert.AreEqual(SerializedPropertyType.ManagedReference, sourceProperty.propertyType);
                Assert.IsTrue(StudioPropertyValueCodec.TryEncode(sourceProperty, out var stored));

                var target = targetObject.AddComponent<SampleAdvancedSerializationController>();
                var targetSerialized = new SerializedObject(target);
                var targetProperty = targetSerialized.FindProperty("rule");
                targetProperty.managedReferenceValue = null;
                Assert.IsTrue(StudioPropertyValueCodec.TryDecode(stored, targetProperty));
                targetSerialized.ApplyModifiedPropertiesWithoutUndo();

                var restored = target.Rule as SampleThresholdRule;
                Assert.IsNotNull(restored);
                Assert.AreEqual("critical", restored.label);
                Assert.AreEqual(0.25f, restored.threshold, 1e-5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }
    }
}
