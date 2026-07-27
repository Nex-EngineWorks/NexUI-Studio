using emiteat.NexUI.Designer.Editor.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Tests for the pure UXML/USS generator (brief §31/§39.2). Verifies element tag mapping, nesting,
    /// name/class/text attributes, absolute-position USS rules, and XML escaping — all without touching
    /// any file, which is the whole point of the separate-generated-file strategy.
    /// </summary>
    public sealed class UIToolkitCodeGeneratorTests
    {
        private static DesignerMetadataAsset Metadata(params DesignerElementMetadata[] elements)
        {
            var asset = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            asset.screenId = "TestScreen";
            asset.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            foreach (var e in elements) asset.elements.Add(e);
            return asset;
        }

        private static DesignerElementMetadata El(string id, string type, Rect rect, string parentId = null)
        {
            return new DesignerElementMetadata { elementId = id, elementType = type, rect = rect, parentId = parentId };
        }

        [Test]
        public void Uxml_HasUxmlRootAndGeneratedBanner()
        {
            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(El("root", "Panel", new Rect(0, 0, 100, 100))));
            StringAssert.Contains("<ui:UXML", uxml);
            StringAssert.Contains("NEXUI:GENERATED", uxml);
            StringAssert.Contains("</ui:UXML>", uxml);
        }

        [Test]
        public void Uxml_MapsTypesToTagsAndNames()
        {
            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(
                El("panel", "Panel", new Rect(0, 0, 200, 200)),
                El("label", "Label", new Rect(10, 10, 80, 20), parentId: "panel")));

            StringAssert.Contains("<ui:VisualElement name=\"panel\"", uxml);
            StringAssert.Contains("<ui:Label name=\"label\"", uxml);
        }

        [Test]
        public void Uxml_NestsChildrenUnderParent()
        {
            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(
                El("panel", "Panel", new Rect(0, 0, 200, 200)),
                El("label", "Label", new Rect(10, 10, 80, 20), parentId: "panel")));

            var panelOpen = uxml.IndexOf("name=\"panel\"", System.StringComparison.Ordinal);
            var labelPos = uxml.IndexOf("name=\"label\"", System.StringComparison.Ordinal);
            var panelClose = uxml.IndexOf("</ui:VisualElement>", System.StringComparison.Ordinal);

            Assert.Less(panelOpen, labelPos, "label should come after panel open");
            Assert.Less(labelPos, panelClose, "label should be nested before panel close");
        }

        [Test]
        public void Uxml_EmitsTextForLabelAndButtonOnly()
        {
            var button = El("btn", "Button", new Rect(0, 0, 100, 30));
            button.text = "Play";
            var panel = El("p", "Panel", new Rect(0, 0, 100, 30));
            panel.text = "ignored";

            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(button, panel));

            StringAssert.Contains("text=\"Play\"", uxml);
            StringAssert.DoesNotContain("text=\"ignored\"", uxml);
        }

        [Test]
        public void Uxml_EmitsClassAttribute()
        {
            var el = El("card", "Panel", new Rect(0, 0, 100, 100));
            el.classes.Add("inventory-slot");
            el.classes.Add("selected");

            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(el));

            StringAssert.Contains("class=\"inventory-slot selected\"", uxml);
        }

        [Test]
        public void Uxml_EscapesSpecialCharactersInText()
        {
            var label = El("l", "Label", new Rect(0, 0, 100, 20));
            label.text = "<b>A</b> & \"B\"";

            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(label));

            StringAssert.Contains("&lt;b&gt;A&lt;/b&gt; &amp; &quot;B&quot;", uxml);
            StringAssert.DoesNotContain("<b>A</b>", uxml);
        }

        [Test]
        public void Uss_EmitsAbsolutePositionRule()
        {
            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(El("root", "Panel", new Rect(10, 20, 120, 40))));

            StringAssert.Contains("#root {", uss);
            StringAssert.Contains("position: absolute;", uss);
            StringAssert.Contains("left: 10px;", uss);
            StringAssert.Contains("top: 20px;", uss);
            StringAssert.Contains("width: 120px;", uss);
            StringAssert.Contains("height: 40px;", uss);
        }

        [Test]
        public void Uss_TextElementGetsColorAndFontSize_ContainerGetsBackground()
        {
            var label = El("label", "Label", new Rect(0, 0, 80, 20));
            label.textColor = Color.red;
            label.fontSize = 18;
            var panel = El("panel", "Panel", new Rect(0, 0, 80, 20));

            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(label, panel));

            StringAssert.Contains("color: rgba(255, 0, 0, 1);", uss);
            StringAssert.Contains("font-size: 18px;", uss);
            StringAssert.Contains("background-color:", uss);
        }

        [Test]
        public void NullMetadata_ProducesWellFormedEmptyDocuments()
        {
            StringAssert.Contains("</ui:UXML>", UIToolkitCodeGenerator.GenerateUxml(null));
            StringAssert.Contains("NEXUI:GENERATED", UIToolkitCodeGenerator.GenerateUss(null));
        }

        [Test]
        public void Uss_RuntimeVisibilityIsIndependentFromEditorHidden()
        {
            var editorHidden = El("editorHidden", "Panel", new Rect(0, 0, 10, 10));
            editorHidden.hiddenInDesigner = true;
            editorHidden.runtimeVisible = true;
            var runtimeHidden = El("runtimeHidden", "Panel", new Rect(0, 0, 10, 10));
            runtimeHidden.runtimeVisible = false;

            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(editorHidden, runtimeHidden));

            StringAssert.DoesNotContain("display: none;", RuleFor(uss, "editorHidden"));
            StringAssert.Contains("display: none;", RuleFor(uss, "runtimeHidden"));
        }

        [Test]
        public void Uss_AutoLayoutContainer_EmitsFlexDirectionAndPadding()
        {
            var panel = El("panel", "Panel", new Rect(0, 0, 200, 300));
            panel.autoLayout.enabled = true;
            panel.autoLayout.direction = DesignerAutoLayoutDirection.Row;
            panel.autoLayout.paddingLeft = 5;
            panel.autoLayout.paddingTop = 6;

            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(panel));

            StringAssert.Contains("flex-direction: row;", uss);
            StringAssert.Contains("padding: 6px 0px 0px 5px;", uss);
        }

        [Test]
        public void Uss_ChildOfAutoLayout_UsesRelativeNotAbsolute()
        {
            var panel = El("panel", "Panel", new Rect(0, 0, 200, 300));
            panel.autoLayout.enabled = true;
            panel.autoLayout.direction = DesignerAutoLayoutDirection.Column;
            panel.autoLayout.spacing = 10;
            var a = El("a", "Panel", new Rect(0, 0, 100, 40), parentId: "panel");
            var b = El("b", "Panel", new Rect(0, 0, 100, 40), parentId: "panel");
            b.siblingIndex = 1;

            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(panel, a, b));

            // Children flow relatively, not absolutely.
            var aRule = RuleFor(uss, "a");
            StringAssert.Contains("position: relative;", aRule);
            StringAssert.DoesNotContain("position: absolute;", aRule);
            // First child has no leading margin; second does.
            StringAssert.DoesNotContain("margin-top", aRule);
            StringAssert.Contains("margin-top: 10px;", RuleFor(uss, "b"));
        }

        [Test]
        public void Uss_FillSizing_EmitsFlexGrowOnMainAxis()
        {
            var panel = El("panel", "Panel", new Rect(0, 0, 200, 300));
            panel.autoLayout.enabled = true;
            panel.autoLayout.direction = DesignerAutoLayoutDirection.Column;
            var child = El("child", "Panel", new Rect(0, 0, 100, 40), parentId: "panel");
            child.autoLayout.heightSizing = DesignerAutoLayoutSizing.Fill;

            var uss = RuleFor(UIToolkitCodeGenerator.GenerateUss(Metadata(panel, child)), "child");

            StringAssert.Contains("flex-grow: 1;", uss);
        }

        [Test]
        public void GridLayout_EmitsWrappingContainerAndFixedCellSize()
        {
            var grid = El("grid", "Grid", new Rect(0, 0, 240, 240));
            grid.autoLayout.enabled = true;
            grid.autoLayout.direction = DesignerAutoLayoutDirection.Grid;
            grid.autoLayout.gridColumns = 3;
            grid.autoLayout.gridCellWidth = 64;
            grid.autoLayout.gridCellHeight = 48;
            var child = El("slot", "Slot", new Rect(0, 0, 10, 10), parentId: "grid");

            var uss = UIToolkitCodeGenerator.GenerateUss(Metadata(grid, child));

            StringAssert.Contains("flex-direction: row;", RuleFor(uss, "grid"));
            StringAssert.Contains("flex-wrap: wrap;", RuleFor(uss, "grid"));
            StringAssert.Contains("width: 64px;", RuleFor(uss, "slot"));
            StringAssert.Contains("height: 48px;", RuleFor(uss, "slot"));
        }

        [Test]
        public void Uxml_WithStylesheet_ReferencesGeneratedUss()
        {
            var uxml = UIToolkitCodeGenerator.GenerateUxml(Metadata(), "Inventory.uss");
            StringAssert.Contains("<Style src=\"Inventory.uss\" />", uxml);
        }

        [Test]
        public void Uss_TypedLayoutVisualAndTypography_AreEmitted()
        {
            var label = El("typed", "Label", new Rect(0, 0, 120, 40));
            label.layoutStyle = new DesignerLayoutStyleMetadata
            {
                hasOverrides = true, minSize = new Vector2(40, 20), maxSize = new Vector2(240, 80),
                marginLeft = 3, marginTop = 4, rotation = 12, scale = new Vector2(1.1f, .9f),
                overflow = DesignerOverflowMode.Hidden
            };
            label.visualStyle = new DesignerVisualStyleMetadata
            {
                hasOverrides = true, backgroundColor = Color.blue, opacity = .65f,
                borderWidth = 2f, borderColor = Color.yellow, cornerRadius = 7f,
                outlineWidth = 1f, outlineColor = Color.black
            };
            label.typography = new DesignerTypographyMetadata
            {
                hasOverrides = true, fontSize = 22f, color = Color.cyan,
                fontStyle = DesignerFontStyle.Bold | DesignerFontStyle.Italic,
                alignment = DesignerTextAlignment.UpperRight, wrapping = false,
                ellipsis = true, lineHeight = 1.4f, letterSpacing = 2f,
                textShadow = true, outlineWidth = 1f
            };

            var rule = RuleFor(UIToolkitCodeGenerator.GenerateUss(Metadata(label)), "typed");
            StringAssert.Contains("min-width: 40px;", rule);
            StringAssert.Contains("max-height: 80px;", rule);
            StringAssert.Contains("rotate: 12deg;", rule);
            StringAssert.Contains("opacity: 0.65;", rule);
            StringAssert.Contains("border-left-width: 2px;", rule);
            StringAssert.Contains("border-top-left-radius: 7px;", rule);
            StringAssert.Contains("font-size: 22px;", rule);
            StringAssert.Contains("-unity-font-style: bold-and-italic;", rule);
            StringAssert.Contains("text-overflow: ellipsis;", rule);
        }

        private static string RuleFor(string uss, string id)
        {
            var start = uss.IndexOf("#" + id + " {", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"rule for #{id} not found");
            var end = uss.IndexOf('}', start);
            return uss.Substring(start, end - start);
        }
    }
}
