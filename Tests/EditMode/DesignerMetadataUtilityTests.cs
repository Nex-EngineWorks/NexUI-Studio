using emiteat.NexUI.Designer.Editor.Serialization;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Designer.Editor.Responsive;
using emiteat.NexUI.Designer.Editor.Variants;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class DesignerMetadataUtilityTests
    {
        private static DesignerMetadataAsset NewAsset()
            => ScriptableObject.CreateInstance<DesignerMetadataAsset>();

        [Test]
        public void MakeUniqueId_ReturnsBaseWhenFree()
        {
            var asset = NewAsset();
            Assert.AreEqual("panel", DesignerMetadataUtility.MakeUniqueId(asset, "panel"));
        }

        [Test]
        public void MakeUniqueId_AppendsNumberWhenTaken()
        {
            var asset = NewAsset();
            asset.elements.Add(new DesignerElementMetadata { elementId = "panel" });
            Assert.AreEqual("panel1", DesignerMetadataUtility.MakeUniqueId(asset, "panel"));
        }

        [Test]
        public void Rename_RepointsChildParentIds()
        {
            var asset = NewAsset();
            var parent = DesignerMetadataUtility.Create(asset, new DesignerElementMetadata { elementId = "parent" });
            DesignerMetadataUtility.Create(asset, new DesignerElementMetadata { elementId = "child", parentId = "parent" });

            Assert.IsTrue(DesignerMetadataUtility.Rename(asset, parent, "root"));
            Assert.AreEqual("root", asset.Find("child").parentId);
        }

        [Test]
        public void Rename_RejectsCollision()
        {
            var asset = NewAsset();
            var a = DesignerMetadataUtility.Create(asset, new DesignerElementMetadata { elementId = "a" });
            DesignerMetadataUtility.Create(asset, new DesignerElementMetadata { elementId = "b" });
            Assert.IsFalse(DesignerMetadataUtility.Rename(asset, a, "b"));
        }

        [Test]
        public void Duplicate_ProducesUniqueDeepCopy()
        {
            var asset = NewAsset();
            var src = DesignerMetadataUtility.Create(asset, new DesignerElementMetadata { elementId = "btn", text = "Hi" });
            src.classes.Add("primary");

            var copy = DesignerMetadataUtility.Duplicate(asset, src);
            Assert.AreNotEqual(src.elementId, copy.elementId);
            Assert.AreEqual("Hi", copy.text);
            Assert.Contains("primary", copy.classes);
            copy.classes.Add("mutated");
            Assert.AreEqual(1, src.classes.Count, "clone must not share the classes list");
            Assert.AreNotEqual(src.stableId, copy.stableId, "a duplicate must have a new backend identity");
        }

        [Test]
        public void Clone_DeepCopiesNestedMetadataAndKeepsUnityObjectReferences()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            try
            {
                var source = new DesignerElementMetadata
                {
                    elementId = "source",
                    runtimeVisible = false,
                    previewImage = sprite,
                    binding = new DesignerBindingMetadata { textKey = "player.name" },
                    focus = new DesignerFocusMetadata { rightElementId = "next" }
                };
                source.classes.Add("primary");
                source.previewOptions.Add("A");
                source.autoLayout.enabled = true;
                source.autoLayout.spacing = 12f;

                var clone = DesignerMetadataUtility.Clone(source);

                Assert.AreEqual(source.stableId, clone.stableId, "plain clone preserves identity; Duplicate replaces it");
                Assert.AreEqual(sprite, clone.previewImage);
                Assert.IsFalse(clone.runtimeVisible);
                Assert.AreEqual("player.name", clone.binding.textKey);
                Assert.AreEqual("next", clone.focus.rightElementId);
                Assert.AreEqual(12f, clone.autoLayout.spacing);
                Assert.AreNotSame(source.classes, clone.classes);
                Assert.AreNotSame(source.previewOptions, clone.previewOptions);
                Assert.AreNotSame(source.binding, clone.binding);
                Assert.AreNotSame(source.focus, clone.focus);
                Assert.AreNotSame(source.autoLayout, clone.autoLayout);

                clone.classes.Add("mutated");
                clone.binding.textKey = "changed";
                Assert.AreEqual(1, source.classes.Count);
                Assert.AreEqual("player.name", source.binding.textKey);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AnchorPreset_DefaultsToTopLeft()
        {
            var element = new DesignerElementMetadata();
            Assert.AreEqual(DesignerAnchorPreset.TopLeft, element.anchorPreset,
                "Default (0) must be TopLeft so pre-existing metadata deserializes to the historical anchor.");
        }

        [Test]
        public void Duplicate_PreservesAnchorPreset()
        {
            var asset = NewAsset();
            var src = DesignerMetadataUtility.Create(asset,
                new DesignerElementMetadata { elementId = "panel", anchorPreset = DesignerAnchorPreset.BottomRight });

            var copy = DesignerMetadataUtility.Duplicate(asset, src);
            Assert.AreEqual(DesignerAnchorPreset.BottomRight, copy.anchorPreset);
        }

        [Test]
        public void FindDuplicateIds_DetectsRepeats()
        {
            var asset = NewAsset();
            asset.elements.Add(new DesignerElementMetadata { elementId = "dup" });
            asset.elements.Add(new DesignerElementMetadata { elementId = "dup" });
            asset.elements.Add(new DesignerElementMetadata { elementId = "unique" });
            var dupes = DesignerMetadataUtility.FindDuplicateIds(asset);
            Assert.AreEqual(1, dupes.Count);
            Assert.AreEqual("dup", dupes[0]);
        }

        [Test]
        public void IsValidElementId_RejectsBadIds()
        {
            Assert.IsTrue(DesignerMetadataUtility.IsValidElementId("login_button-1"));
            Assert.IsFalse(DesignerMetadataUtility.IsValidElementId("1button"));
            Assert.IsFalse(DesignerMetadataUtility.IsValidElementId("has space"));
            Assert.IsFalse(DesignerMetadataUtility.IsValidElementId(""));
        }

        [Test]
        public void TypedPropertyRegistry_ResolvesLegacyAliasesAndRoundTripsValues()
        {
            Assert.AreEqual(DesignerPropertyId.Width, DesignerPropertyRegistry.ResolveLegacyPath("rect.width"));
            Assert.AreEqual(DesignerPropertyId.RuntimeVisible, DesignerPropertyRegistry.ResolveLegacyPath("visible"));
            var value = DesignerPropertyRegistry.Parse(DesignerPropertyId.Scale, "1.25,0.75");
            Assert.AreEqual(DesignerPropertyValueType.Vector2, value.type);
            Assert.AreEqual(new Vector2(1.25f, .75f), value.vector2Value);
            Assert.AreEqual("1.25,0.75", DesignerPropertyRegistry.Serialize(value));
            Assert.AreEqual(DesignerPropertyBackendSupport.Unsupported,
                DesignerPropertyRegistry.Get(DesignerPropertyId.Blur).UGUI);
            Assert.AreEqual(1f, DesignerPropertyRegistry.Get(DesignerPropertyId.Opacity).DefaultValue.floatValue);
            Assert.IsFalse(DesignerPropertyRegistry.TryParse(DesignerPropertyId.FontSize, "not-a-number", out _, out var error));
            StringAssert.Contains("not a valid", error);
        }

        [Test]
        public void TypedVariantAndResponsiveOverrides_CompileToLegacyRuntimeContract()
        {
            var asset = NewAsset();
            asset.elements.Add(new DesignerElementMetadata { elementId = "title" });
            var typed = new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 20f };
            var variant = new DesignerVariantMetadata { variantId = "compact" };
            variant.overrides.Add(new DesignerVariantOverrideMetadata
            {
                targetElementId = "title", propertyId = DesignerPropertyId.FontSize, typedValue = typed
            });
            asset.variants.Add(variant);
            var responsive = new DesignerResponsiveMetadata { ruleId = "small" };
            responsive.overrides.Add(new DesignerResponsiveOverrideMetadata
            {
                elementId = "title", propertyId = DesignerPropertyId.FontSize, typedValue = typed.Clone()
            });
            asset.responsiveRules.Add(responsive);

            Assert.AreEqual("fontSize", VariantService.Compile(asset)[0].overrides[0].propertyPath);
            Assert.AreEqual("20", VariantService.Compile(asset)[0].overrides[0].value);
            Assert.AreEqual("fontSize", ResponsiveService.Compile(asset)[0].overrides[0].propertyPath);
            Assert.AreEqual("20", ResponsiveService.Compile(asset)[0].overrides[0].value);
        }
    }
}
