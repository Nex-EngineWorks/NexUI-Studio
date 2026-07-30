using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Preview;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Tests for the component model: an element is a container of components, exactly like a
    /// GameObject. These cover the rules that make that model safe to edit - single-instance types,
    /// required and conflicting components, and the fact that a preset produces removable components
    /// rather than an untouchable built-in.
    /// </summary>
    public sealed class DesignerElementComponentTests
    {
        private static DesignerElementMetadata Element(string presetType = "Panel")
            => new DesignerElementMetadata { elementId = "e0", elementType = presetType, rect = new Rect(0, 0, 100, 40) };

        [Test]
        public void PresetsProduceRealRemovableComponents()
        {
            var element = Element("Button");
            DesignerComponentPresetComposer.Stamp(element, "Button", DesignerUIComponentFamily.UGUI);

            CollectionAssert.Contains(TypeIds(element), "UGUI.Image");
            CollectionAssert.Contains(TypeIds(element), "UGUI.Button");
            Assert.IsTrue(DesignerElementComponentAccess.Has(element, DesignerElementComponentAccess.CoreElement));

            var button = Find(element, "UGUI.Button");
            Assert.IsTrue(button.fromPreset, "preset-authored components are labelled as such");
            Assert.IsTrue(DesignerElementComponentAccess.Detach(element, button.instanceId, out _),
                "everything a preset adds must be removable - that is the point of the component model");
            Assert.IsFalse(DesignerElementComponentAccess.Has(element, "UGUI.Button"));
        }

        /// <summary>
        /// The Inspector composes an element that has no components yet, and that write raises
        /// ElementChanged, which can bring it straight back here. Stamping must therefore be
        /// idempotent - otherwise the panel grows a duplicate set of components every rebuild.
        /// </summary>
        [Test]
        public void StampingTwiceProducesTheSameComponents()
        {
            var element = Element("Button");
            DesignerComponentPresetComposer.Stamp(element, "Button", DesignerUIComponentFamily.UGUI);
            var first = TypeIds(element);

            DesignerComponentPresetComposer.Stamp(element, "Button", DesignerUIComponentFamily.UGUI);
            CollectionAssert.AreEqual(first, TypeIds(element));
        }

        [Test]
        public void TheCoreElementComponentCannotBeRemoved()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            var core = Find(element, DesignerElementComponentAccess.CoreElement);

            Assert.IsFalse(DesignerElementComponentAccess.Detach(element, core.instanceId, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void SingleInstanceTypesCannotBeAddedTwice()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);

            Assert.IsNotNull(DesignerElementComponentAccess.Attach(element, "UGUI.Image", DesignerUIComponentFamily.UGUI));
            Assert.IsNull(DesignerElementComponentAccess.Attach(element, "UGUI.Image", DesignerUIComponentFamily.UGUI),
                "Image is not marked AllowMultiple, so a second one must be refused");
        }

        [Test]
        public void ConflictingRenderersAreRefusedWithAReason()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            DesignerElementComponentAccess.Attach(element, "UGUI.Image", DesignerUIComponentFamily.UGUI);

            var reason = DesignerElementComponentAccess.AttachBlockedReason(element, "NX.RoundedRect", DesignerUIComponentFamily.UGUI);
            Assert.IsNotNull(reason, "two graphics on one element is exactly what Unity refuses too");
            StringAssert.Contains("Image", reason);
        }

        [Test]
        public void RequiredComponentsComeAlong()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            DesignerElementComponentAccess.Attach(element, "UGUI.Button", DesignerUIComponentFamily.UGUI);

            CollectionAssert.Contains(TypeIds(element), "UGUI.Image");

            var image = Find(element, "UGUI.Image");
            Assert.IsFalse(DesignerElementComponentAccess.Detach(element, image.instanceId, out var reason),
                "the Button still requires it");
            StringAssert.Contains("Button", reason);
        }

        [Test]
        public void BackendFilteringKeepsToolkitControlsOffUGUIScreens()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);

            Assert.IsFalse(DesignerElementComponentAccess.CanAttach(element, "UITK.Slider", DesignerUIComponentFamily.UGUI));
            Assert.IsFalse(DesignerElementComponentAccess.CanAttach(element, "UGUI.Slider", DesignerUIComponentFamily.UIToolkit));

            // NexUI base components exist on both backends, which is the reason they exist at all.
            Assert.IsTrue(DesignerElementComponentAccess.CanAttach(element, "NX.SafeArea", DesignerUIComponentFamily.UGUI));
            Assert.IsTrue(DesignerElementComponentAccess.CanAttach(element, "NX.SafeArea", DesignerUIComponentFamily.UIToolkit));
        }

        /// <summary>
        /// The schema is reflected from the real Unity type, which is what lets the Designer show the
        /// same fields Unity does without a hand-written table per component.
        /// </summary>
        [Test]
        public void UnityComponentSchemasComeFromTheRealType()
        {
            var image = DesignerUIComponentRegistry.Get("UGUI.Image");
            Assert.AreEqual(typeof(UnityEngine.UI.Image), image.BackingType);

            var keys = new List<string>();
            foreach (var property in image.Properties) keys.Add(property.Key);

            CollectionAssert.Contains(keys, "sprite");
            CollectionAssert.Contains(keys, "preserveAspect");
            CollectionAssert.Contains(keys, "fillAmount");
            CollectionAssert.Contains(keys, "color", "inherited Graphic fields must be included too");
        }

        [Test]
        public void NexUIBaseComponentsAreBackedByRealRuntimeTypes()
        {
            foreach (var type in DesignerUIComponentRegistry.All)
            {
                if (type.Family != DesignerUIComponentFamily.NexUIBase) continue;
                Assert.IsNotNull(type.BackingType, $"{type.TypeId} has no runtime implementation");
                Assert.Greater(type.Properties.Count, 0, $"{type.TypeId} exposes no properties");
            }
        }

        [Test]
        public void ComponentValuesFallBackToTheSchemaDefault()
        {
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            var component = DesignerElementComponentAccess.Attach(element, "NX.SegmentedBar", DesignerUIComponentFamily.UGUI);

            Assert.IsFalse(DesignerElementComponentAccess.IsOverridden(component, "segments"));
            DesignerElementComponentAccess.Set(component, "segments",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Integer, intValue = 8 });

            Assert.IsTrue(DesignerElementComponentAccess.IsOverridden(component, "segments"));
            Assert.AreEqual(8, DesignerElementComponentAccess.GetInt(component, "segments"));

            DesignerElementComponentAccess.Reset(component, "segments");
            Assert.IsFalse(DesignerElementComponentAccess.IsOverridden(component, "segments"));
        }

        /// <summary>
        /// The canvas draws from the components, so removing one must remove what it drew. Without
        /// this the model and the picture disagree, which is the whole reason the preview moved off
        /// the palette type.
        /// </summary>
        [Test]
        public void CanvasDrawsTheComponentsThatAreAttached()
        {
            Assert.IsTrue(DesignerElementPreviewComposer.Draws("UGUI.Image"));
            Assert.IsTrue(DesignerElementPreviewComposer.Draws("NX.SegmentedBar"));
            Assert.IsTrue(DesignerElementPreviewComposer.Draws("UGUI.Slider"));

            // Behaviour-only components deliberately draw nothing.
            Assert.IsFalse(DesignerElementPreviewComposer.Draws("UGUI.CanvasGroup"));
            Assert.IsFalse(DesignerElementPreviewComposer.Draws("NX.SwipeArea"));
        }

        /// <summary>
        /// Switching a screen's backend is the case attach-time rules cannot catch, so validation has
        /// to, and the fix has to swap rather than delete.
        /// </summary>
        [Test]
        public void BackendMismatchIsReportedAndRepairable()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "Screen";
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            DesignerElementComponentAccess.Attach(element, "UGUI.Slider", DesignerUIComponentFamily.UGUI);
            metadata.elements.Add(element);

            var issues = new List<DesignerValidationIssue>();
            DesignerElementComponentValidation.Validate(metadata, "Screen",
                DesignerUIComponentFamily.UIToolkit, issues);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("NEXUI-COMPONENT-BACKEND", issues[0].Code);

            var replaced = DesignerElementComponentValidation.ReplaceUnsupported(
                metadata, DesignerUIComponentFamily.UIToolkit, out var unresolved);

            Assert.AreEqual(1, replaced);
            Assert.AreEqual(0, unresolved);
            CollectionAssert.Contains(TypeIds(element), "UITK.Slider");
            CollectionAssert.DoesNotContain(TypeIds(element), "UGUI.Slider");
        }

        [Test]
        public void MissingRequiredComponentsAreReported()
        {
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var element = Element();
            DesignerElementComponentAccess.EnsureCore(element);
            // Built by hand rather than through Attach, which would have pulled the Image in.
            element.components.Add(new DesignerElementComponent("UGUI.Button"));
            metadata.elements.Add(element);

            var issues = new List<DesignerValidationIssue>();
            DesignerElementComponentValidation.Validate(metadata, "Screen",
                DesignerUIComponentFamily.UGUI, issues);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("NEXUI-COMPONENT-REQUIRED", issues[0].Code);
        }

        private static List<string> TypeIds(DesignerElementMetadata element)
        {
            var ids = new List<string>();
            foreach (var component in element.components) ids.Add(component.typeId);
            return ids;
        }

        private static DesignerElementComponent Find(DesignerElementMetadata element, string typeId)
        {
            foreach (var component in element.components)
                if (component.typeId == typeId) return component;
            Assert.Fail($"{typeId} was not attached");
            return null;
        }
    }
}
