using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Tests for component properties - the Designer's equivalent of a Unity component's serialized
    /// fields. The schema drives the Inspector, the canvas and both backend writers at once, so a
    /// malformed schema entry (enum with no options, default of the wrong type, duplicate key) breaks
    /// several surfaces at once and is worth catching here rather than in the editor.
    /// </summary>
    public sealed class DesignerComponentPropertyTests
    {
        private static DesignerElementMetadata Element(string type)
            => new DesignerElementMetadata { elementId = "e0", elementType = type, rect = new Rect(0, 0, 100, 30) };

        [Test]
        public void SchemasAreWellFormed()
        {
            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                Assert.Greater(descriptor.Properties.Count, 0,
                    $"{descriptor.TypeId} must expose at least the shared component property schema");
                var keys = new HashSet<string>();
                foreach (var property in descriptor.Properties)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(property.Key), descriptor.TypeId);
                    Assert.IsTrue(keys.Add(property.Key), $"{descriptor.TypeId} declares '{property.Key}' twice");
                    Assert.IsFalse(string.IsNullOrEmpty(property.DisplayName), property.Key);
                    Assert.IsNotNull(property.Default, property.Key);
                    Assert.AreEqual(property.Type, property.Default.type,
                        $"{descriptor.TypeId}.{property.Key} default does not match its declared type");

                    if (property.Type == DesignerPropertyValueType.Enum)
                    {
                        Assert.IsNotNull(property.EnumOptions, property.Key);
                        Assert.Greater(property.EnumOptions.Length, 0, property.Key);
                        Assert.Less(property.Default.intValue, property.EnumOptions.Length,
                            $"{property.Key} default index is out of range");
                        Assert.GreaterOrEqual(property.Default.intValue, 0, property.Key);
                    }

                    if (property.HasRange)
                        Assert.Greater(property.Max, property.Min, property.Key);
                }
            }
        }

        /// <summary>The Inspector groups by these ids; an unknown group would render outside every foldout.</summary>
        [Test]
        public void EveryPropertyBelongsToAKnownGroup()
        {
            var known = new HashSet<string>(DesignerComponentPropertyGroup.Order);
            foreach (var descriptor in DesignerComponentRegistry.All)
                foreach (var property in descriptor.Properties)
                    Assert.IsTrue(known.Contains(property.Group),
                        $"{descriptor.TypeId}.{property.Key} uses unknown group '{property.Group}'");
        }

        [Test]
        public void PropertyLabelsAreTranslatedInBothLanguages()
        {
            const string root = "Packages/com.nexengineworks.nexui.studio/Localization/";
            var korean = System.IO.File.ReadAllText(root + "ko-KR.json");
            var english = System.IO.File.ReadAllText(root + "en-US.json");

            var untranslated = new List<string>();
            foreach (var descriptor in DesignerComponentRegistry.All)
                foreach (var property in descriptor.Properties)
                {
                    var token = "\"" + property.LocalizationKey + "\":";
                    if ((!korean.Contains(token) || !english.Contains(token)) && !untranslated.Contains(property.Key))
                        untranslated.Add(property.Key);
                }

            CollectionAssert.IsEmpty(untranslated, "untranslated property labels: " + string.Join(", ", untranslated));
        }

        [Test]
        public void ReadsFallBackToTheSchemaDefaultUntilOverridden()
        {
            var element = Element("Slider");
            Assert.IsFalse(DesignerComponentPropertyAccess.IsOverridden(element, "value.max"));
            Assert.AreEqual(100f, DesignerComponentPropertyAccess.GetFloat(element, "value.max"));

            DesignerComponentPropertyAccess.Set(element, "value.max",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 42f });

            Assert.IsTrue(DesignerComponentPropertyAccess.IsOverridden(element, "value.max"));
            Assert.AreEqual(42f, DesignerComponentPropertyAccess.GetFloat(element, "value.max"));

            DesignerComponentPropertyAccess.Reset(element, "value.max");
            Assert.IsFalse(DesignerComponentPropertyAccess.IsOverridden(element, "value.max"));
            Assert.AreEqual(100f, DesignerComponentPropertyAccess.GetFloat(element, "value.max"),
                "reset must fall back to the schema default, not to zero");
        }

        [Test]
        public void UnknownKeysSurviveReadsAndArePrunedOnDemand()
        {
            var element = Element("Slider");
            DesignerComponentPropertyAccess.Set(element, "fromANewerDesigner",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });

            // Unknown keys are kept so a screen authored in a newer build round-trips through this one.
            Assert.AreEqual(1, element.componentProperties.Count);
            Assert.IsTrue(DesignerComponentPropertyAccess.GetBool(element, "fromANewerDesigner"));

            Assert.AreEqual(1, DesignerComponentPropertyAccess.PruneUnknown(element));
            Assert.AreEqual(0, element.componentProperties.Count);
        }

        [Test]
        public void EnumReadsResolveToOptionNames()
        {
            var element = Element("ScrollArea");
            Assert.AreEqual("Elastic", DesignerComponentPropertyAccess.GetEnum(element, "scroll.movement"),
                "the default index must resolve to its option name");

            DesignerComponentPropertyAccess.Set(element, "scroll.movement",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Enum, intValue = 2 });
            Assert.AreEqual("Clamped", DesignerComponentPropertyAccess.GetEnum(element, "scroll.movement"));
        }

        /// <summary>
        /// The point of the mapping table: a NexUI Slider must become a real Unity Slider, not a box
        /// that looks like one. A typo in the table would silently fall back to a plain GameObject.
        /// </summary>
        [Test]
        public void NexUIComponentsMapOntoStockUnityControls()
        {
            var expected = new Dictionary<string, string>
            {
                { "Slider", "Slider" }, { "Checkbox", "Toggle" }, { "Dropdown", "DropdownTMP" },
                { "TextField", "InputFieldTMP" }, { "ScrollArea", "ScrollView" },
                { "Button", "ButtonTMP" }, { "Icon", "Image" }, { "Label", "TextTMP" }
            };

            foreach (var pair in expected)
            {
                var descriptor = DesignerComponentRegistry.Get(pair.Key);
                Assert.AreEqual(DesignerComponentFamily.NexUI, descriptor.Family, pair.Key);
                Assert.AreEqual(pair.Value, descriptor.UGUIControl, $"{pair.Key} should write a stock {pair.Value}");
                Assert.AreEqual(DesignerBackendSupport.Full, descriptor.UGUISupport,
                    $"{pair.Key} maps onto a stock control, so uGUI support is Full");
            }

            // A component with no stock equivalent must not claim one.
            Assert.IsTrue(string.IsNullOrEmpty(DesignerComponentRegistry.Get("QuestCard").UGUIControl));
        }

        [Test]
        public void UxmlEmitsOverriddenPropertiesOnly()
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            asset.screenId = "PropertyScreen";
            var toggle = Element("UITK.Toggle");
            asset.elements.Add(toggle);

            var before = UIToolkitCodeGenerator.GenerateUxml(asset);
            StringAssert.DoesNotContain("value=\"", before, "an untouched property must not be restated in the UXML");

            DesignerComponentPropertyAccess.Set(toggle, "toggle.isOn",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = true });

            var after = UIToolkitCodeGenerator.GenerateUxml(asset);
            StringAssert.Contains("value=\"true\"", after);
        }

        [Test]
        public void UxmlDoesNotPutControlAttributesOnCrossBackendFallbacks()
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var slider = Element("UGUI.Slider");
            asset.elements.Add(slider);
            DesignerComponentPropertyAccess.Set(slider, "value.min",
                new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = 5f });

            var uxml = UIToolkitCodeGenerator.GenerateUxml(asset);
            StringAssert.Contains("<ui:VisualElement", uxml);
            StringAssert.DoesNotContain("low-value=", uxml,
                "uGUI controls use a generic VisualElement fallback and must not emit invalid Slider attributes");
        }

        [Test]
        public void BackendSupportIsReportedPerProperty()
        {
            var slider = DesignerComponentRegistry.Get("Slider");
            Assert.AreEqual(DesignerBackendSupport.Full,
                DesignerComponentPropertySupport.UGUI(slider, slider.Properties.Find(p => p.Key == "value.min")));
            Assert.AreEqual(DesignerBackendSupport.PreviewOnly,
                DesignerComponentPropertySupport.UGUI(slider, slider.Properties.Find(p => p.Key == "slider.showTicks")));

            var uitkToggle = DesignerComponentRegistry.Get("UITK.Toggle");
            Assert.AreEqual(DesignerBackendSupport.Full,
                DesignerComponentPropertySupport.UIToolkit(uitkToggle,
                    uitkToggle.Properties.Find(p => p.Key == "toggle.isOn")));
        }
    }
}
