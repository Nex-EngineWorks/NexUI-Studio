using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// The universal component data model: one component list, values that can hold more than seven
    /// shapes, references stored by identity, and a migration that loses nothing.
    /// </summary>
    public sealed class DesignerUniversalComponentModelTests
    {
        private static DesignerMetadataAsset NewAsset(int schemaVersion, params DesignerElementMetadata[] elements)
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            asset.schemaVersion = schemaVersion;
            asset.screenId = "screen";
            foreach (var element in elements) asset.elements.Add(element);
            return asset;
        }

        private static DesignerElementMetadata Element(string id = "panel")
            => new DesignerElementMetadata { stableId = id + "-stable", elementId = id, elementType = "Panel" };

        // ---- Project component ids -------------------------------------------------------------

        [Test]
        public void ProjectIdsShareTheRegistryIdSpaceWithoutColliding()
        {
            var id = DesignerProjectComponentIds.FromQualifiedName(
                "Health.HealthBarController, Assembly-CSharp, Version=0.0.0.0, Culture=neutral");

            Assert.AreEqual("Project:Health.HealthBarController", id);
            Assert.IsTrue(DesignerProjectComponentIds.IsProjectId(id));
            Assert.IsFalse(DesignerProjectComponentIds.IsProjectId("UGUI.Image"));
            Assert.AreEqual("Health.HealthBarController", DesignerProjectComponentIds.ToFullName(id));
            Assert.AreEqual("HealthBarController", DesignerProjectComponentIds.ShortName(id));
        }

        [Test]
        public void AQualifiedNameWithoutAnAssemblyStillProducesAnId()
        {
            Assert.AreEqual("Project:MyBehaviour",
                DesignerProjectComponentIds.FromQualifiedName("MyBehaviour"));
        }

        // ---- Migration -------------------------------------------------------------------------

        [Test]
        public void MigrationFoldsAttachedComponentsIntoTheComponentStack()
        {
            var element = Element();
            element.attachedComponents.Add(new DesignerAttachedComponentMetadata
            {
                typeName = "Health.HealthBarController, Assembly-CSharp"
            });
            var asset = NewAsset(5, element);
            try
            {
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);

                var migrated = element.components.Find(c => c.source == DesignerComponentSource.Project);
                Assert.IsNotNull(migrated, "The project script should now be a real stack entry.");
                Assert.AreEqual("Project:Health.HealthBarController", migrated.typeId);
                Assert.AreEqual("Health.HealthBarController, Assembly-CSharp", migrated.assemblyQualifiedTypeName);
                Assert.IsTrue(migrated.enabled);
                Assert.IsFalse(string.IsNullOrEmpty(migrated.instanceId), "Every component needs a stable identity.");
                Assert.AreEqual(6, asset.schemaVersion);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MigrationKeepsTheOldListSoAnOlderBuildStillSeesTheComponent()
        {
            var element = Element();
            element.attachedComponents.Add(new DesignerAttachedComponentMetadata { typeName = "MyBehaviour" });
            var asset = NewAsset(5, element);
            try
            {
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);

                Assert.AreEqual(1, element.attachedComponents.Count,
                    "Clearing the old list would make the component vanish in a pre-v6 Designer.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MigrationIsIdempotent()
        {
            var element = Element();
            element.attachedComponents.Add(new DesignerAttachedComponentMetadata { typeName = "MyBehaviour" });
            var asset = NewAsset(5, element);
            try
            {
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);
                var afterFirst = element.components.Count;

                asset.schemaVersion = 5;
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);

                Assert.AreEqual(afterFirst, element.components.Count,
                    "Running the migration twice must not duplicate the component.");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MigrationInfersTheSourceOfExistingStackEntries()
        {
            var element = Element();
            element.components.Add(new DesignerElementComponent("UGUI.Image"));
            element.components.Add(new DesignerElementComponent("UITK.Button"));
            element.components.Add(new DesignerElementComponent("Core.Element"));
            var asset = NewAsset(5, element);
            try
            {
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);

                Assert.AreEqual(DesignerComponentSource.UGUI, element.components[0].source);
                Assert.AreEqual(DesignerComponentSource.UIToolkit, element.components[1].source);
                Assert.AreEqual(DesignerComponentSource.NexUI, element.components[2].source);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MigrationKeepsAnUnresolvableTypeInsteadOfDroppingIt()
        {
            var element = Element();
            element.attachedComponents.Add(new DesignerAttachedComponentMetadata
            {
                typeName = "Deleted.Namespace.GoneScript, SomeAssemblyThatIsNotLoaded"
            });
            var asset = NewAsset(5, element);
            try
            {
                DesignerHierarchyMigration.Migrate(asset, recordUndo: false);

                var migrated = element.components.Find(c => c.source == DesignerComponentSource.Project);
                Assert.IsNotNull(migrated, "A missing script must be reported, not silently deleted.");
                Assert.AreEqual("Deleted.Namespace.GoneScript, SomeAssemblyThatIsNotLoaded",
                    migrated.assemblyQualifiedTypeName);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---- Property values -------------------------------------------------------------------

        [Test]
        public void ComplexValuesRoundTripThroughTheSerializedField()
        {
            var value = new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.Serialized,
                json = "{\"x\":1.0,\"y\":2.0,\"z\":3.0}"
            };

            var clone = value.Clone();

            Assert.AreEqual(DesignerPropertyValueType.Serialized, clone.type);
            Assert.AreEqual(value.json, clone.json);
        }

        [Test]
        public void AnUnknownValueSurvivesACloneSoOlderBuildsDoNotDropIt()
        {
            var value = new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.Serialized,
                json = "{\"somethingThisBuildDoesNotKnow\":42}"
            };

            Assert.AreEqual(value.json, value.Clone().json);
            Assert.IsFalse(value.IsEmpty);
        }

        [Test]
        public void AFreshValueReportsItselfEmpty()
        {
            Assert.IsTrue(new DesignerPropertyValue().IsEmpty);
        }

        [Test]
        public void ExistingTypedFieldsAreUntouchedByTheExtension()
        {
            // The seven original fields keep their meaning, which is why v5 assets need no rewrite.
            var value = new DesignerPropertyValue
            {
                type = DesignerPropertyValueType.Float, floatValue = 1.5f
            };

            var clone = value.Clone();

            Assert.AreEqual(DesignerPropertyValueType.Float, clone.type);
            Assert.AreEqual(1.5f, clone.floatValue);

            // Null-or-empty, not null: Clone round-trips through JsonUtility, which turns a null
            // string into "" and never back. DesignerPropertyValue.IsEmpty already treats the two
            // as the same thing, so "carries no json payload" is what this is actually asserting.
            Assert.IsTrue(string.IsNullOrEmpty(clone.json), "a float value carries no json payload");
        }

        // ---- References ------------------------------------------------------------------------

        [Test]
        public void ElementReferencesAreRemappedOnDuplication()
        {
            var reference = new DesignerObjectReference
            {
                kind = DesignerReferenceKind.Element,
                stableElementId = "fill-stable",
                componentTypeName = "UnityEngine.UI.Image, UnityEngine.UI"
            };

            reference.Remap(new Dictionary<string, string> { { "fill-stable", "fill-copy-stable" } });

            Assert.AreEqual("fill-copy-stable", reference.stableElementId,
                "A duplicate must point at its own child, not at the original's.");
            Assert.AreEqual("UnityEngine.UI.Image, UnityEngine.UI", reference.componentTypeName);
        }

        [Test]
        public void AReferenceOutsideTheCopiedSetIsLeftAlone()
        {
            var reference = new DesignerObjectReference
            {
                kind = DesignerReferenceKind.Element, stableElementId = "external-stable"
            };

            reference.Remap(new Dictionary<string, string> { { "fill-stable", "fill-copy-stable" } });

            Assert.AreEqual("external-stable", reference.stableElementId,
                "Pointing at the original is correct when the target was never copied.");
        }

        [Test]
        public void AssetReferencesAreNotRemapped()
        {
            var reference = new DesignerObjectReference
            {
                kind = DesignerReferenceKind.Asset, assetGuid = "abc123", localFileId = 21300000
            };

            reference.Remap(new Dictionary<string, string> { { "abc123", "should-not-apply" } });

            Assert.AreEqual("abc123", reference.assetGuid);
        }

        [Test]
        public void AnUnassignedReferenceReportsItself()
        {
            Assert.IsFalse(new DesignerObjectReference().IsAssigned);
            Assert.IsTrue(new DesignerObjectReference { kind = DesignerReferenceKind.Element }.IsAssigned);
        }

        [Test]
        public void ComponentCloneCarriesSourceAndQualifiedName()
        {
            var component = new DesignerElementComponent("Project:My.Script")
            {
                source = DesignerComponentSource.Project,
                assemblyQualifiedTypeName = "My.Script, Assembly-CSharp"
            };

            var clone = component.Clone();

            Assert.AreEqual(DesignerComponentSource.Project, clone.source);
            Assert.AreEqual("My.Script, Assembly-CSharp", clone.assemblyQualifiedTypeName);
            Assert.AreNotEqual(component.instanceId, clone.instanceId, "A clone is a different attachment.");
        }
    }
}
