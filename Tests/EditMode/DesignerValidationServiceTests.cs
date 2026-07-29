using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Validation;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerValidationServiceTests
    {
        [SetUp]
        public void SetUp() => DesignerBackendRegistry.RegisterDefaults();

        private static UIScreenDefinition NewScreen(string id, UIRenderBackend backend, Object asset)
        {
            var screen = ScriptableObject.CreateInstance<UIScreenDefinition>();
            screen.identity = new UIScreenIdentity { screenId = id };
            screen.backendAsset = new UIScreenBackendAsset { backend = backend, asset = asset };
            return screen;
        }

        private static bool HasCode(List<DesignerValidationIssue> issues, string code)
            => issues.Exists(i => i.Code == code);

        [Test]
        public void NullScreen_ReportsNoScreen()
        {
            var issues = DesignerValidationService.Validate(null, null);
            Assert.IsTrue(HasCode(issues, "no-screen"));
        }

        [Test]
        public void EmptyScreenId_ReportsError()
        {
            var screen = NewScreen("", UIRenderBackend.UIToolkit, null);
            var issues = DesignerValidationService.Validate(screen, null);
            Assert.IsTrue(HasCode(issues, "empty-screen-id"));
            Assert.IsTrue(HasCode(issues, "backend-asset-missing"));
        }

        [Test]
        public void BackendTypeMismatch_ForUGUIWithNonGameObject()
        {
            // A ScriptableObject is not a GameObject prefab.
            var wrong = ScriptableObject.CreateInstance<UIScreenDefinition>();
            var screen = NewScreen("hud", UIRenderBackend.UGUI, wrong);
            var issues = DesignerValidationService.Validate(screen, null);
            Assert.IsTrue(HasCode(issues, "backend-type-mismatch"));
        }

        [Test]
        public void DuplicateElementIds_ReportError()
        {
            var screen = NewScreen("hud", UIRenderBackend.UIToolkit, null);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "hud";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "dup", rect = new Rect(0, 0, 100, 100) });
            metadata.elements.Add(new DesignerElementMetadata { elementId = "dup", rect = new Rect(0, 0, 100, 100) });

            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.IsTrue(HasCode(issues, "duplicate-element-id"));
            var duplicate = issues.Find(issue => issue.Code == "duplicate-element-id");
            Assert.AreEqual(DesignerValidationCategory.Identity, duplicate.Category);
            Assert.IsTrue(duplicate.CanAutoFix);
            Assert.IsTrue(duplicate.IsSafeAutoFix);
            Assert.IsFalse(duplicate.RequiresUserAction);
        }

        [Test]
        public void ComponentPropertyRangeAndTypeErrorsAreActionable()
        {
            var screen = NewScreen("hud", UIRenderBackend.UGUI, null);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "hud";
            var slider = new DesignerElementMetadata
            {
                elementId = "volume", elementType = "Slider", rect = new Rect(0, 0, 180, 40)
            };
            DesignerComponentPropertyAccess.Set(slider, "value.min",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 20f });
            DesignerComponentPropertyAccess.Set(slider, "value.max",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 10f });
            DesignerComponentPropertyAccess.Set(slider, "value.wholeNumbers",
                new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = "bad" });
            metadata.elements.Add(slider);

            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.IsTrue(HasCode(issues, "component-property-invalid-range"));
            Assert.IsTrue(HasCode(issues, "component-property-type-mismatch"));
        }

        [Test]
        public void ButtonWithoutCommand_Warns()
        {
            var screen = NewScreen("hud", UIRenderBackend.UIToolkit, null);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "hud";
            metadata.elements.Add(new DesignerElementMetadata
            {
                elementId = "okButton",
                elementType = "Button",
                text = "OK",
                rect = new Rect(0, 0, 120, 48)
            });

            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.IsTrue(HasCode(issues, "button-without-command"));
        }

        [Test]
        public void SmallTouchTarget_Warns()
        {
            var screen = NewScreen("hud", UIRenderBackend.UIToolkit, null);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "hud";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "tiny", rect = new Rect(0, 0, 10, 10) });

            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.IsTrue(HasCode(issues, "small-touch-target"));
        }

        [Test]
        public void TypedOverrideWithWrongValueType_ReportsError()
        {
            var screen = NewScreen("hud", UIRenderBackend.UIToolkit, null);
            var metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            metadata.screenId = "hud";
            metadata.elements.Add(new DesignerElementMetadata { elementId = "title", rect = new Rect(0, 0, 100, 30) });
            var variant = new DesignerVariantMetadata { variantId = "Default", isDefault = true };
            variant.overrides.Add(new DesignerVariantOverrideMetadata
            {
                targetElementId = "title", propertyId = DesignerPropertyId.FontSize,
                typedValue = new DesignerPropertyValue { type = DesignerPropertyValueType.Color, colorValue = Color.red }
            });
            metadata.variants.Add(variant);

            var issues = DesignerValidationService.Validate(screen, metadata);
            Assert.IsTrue(HasCode(issues, "property-value-type-mismatch"));
        }
    }
}
