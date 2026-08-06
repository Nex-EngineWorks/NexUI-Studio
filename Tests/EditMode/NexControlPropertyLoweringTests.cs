using System.Linq;
using emiteat.NexUI.Compiled;
using emiteat.NexUI.Designer.Editor.Compiler;
using emiteat.NexUI.Designer.Editor.Components;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Authored control settings reaching the compiled program, and the content hash noticing.
    /// </summary>
    /// <remarks>
    /// Two failures are guarded here, and the second is the subtle one.
    ///
    /// The first is that the compiled path used to carry no control settings at all - a screen
    /// could save with a character limit and run without one, because only the prefab writer
    /// applied them.
    ///
    /// The second is that <c>ToCanonicalString</c> feeds the content hash, and the publisher skips
    /// writing when the hash is unchanged. A field the canonical form omits is a field the author
    /// can edit with the change never reaching the asset - which looks exactly like the edit not
    /// working, with nothing in any log.
    /// </remarks>
    public sealed class NexControlPropertyLoweringTests
    {
        private DesignerMetadataAsset _metadata;

        [SetUp]
        public void SetUp()
        {
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "PropertyScreen";
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_metadata);

        private DesignerElementMetadata Add(string id, string type)
        {
            var element = new DesignerElementMetadata
            {
                elementId = id,
                stableId = id + "-stable",
                displayName = id,
                elementType = type,
                rect = new Rect(0, 0, 200, 40),
                runtimeVisible = true,
                binding = new DesignerBindingMetadata()
            };
            _metadata.elements.Add(element);
            return element;
        }

        /// <summary>Sets a property the way the Inspector does, so it counts as overridden.</summary>
        private static void Override(DesignerElementMetadata element, string key, DesignerPropertyValue value)
            => DesignerComponentPropertyAccess.Set(element, key, value);

        private static DesignerPropertyValue Number(float value)
            => new DesignerPropertyValue { type = DesignerPropertyValueType.Float, floatValue = value };

        private static DesignerPropertyValue Flag(bool value)
            => new DesignerPropertyValue { type = DesignerPropertyValueType.Boolean, boolValue = value };

        private NexNodeProgram Node(string name)
            => NexScreenCompiler.Compile(_metadata).Program.Nodes.First(n => n.Name == name);

        private string Hash() => NexScreenCompiler.Compile(_metadata).Program.ContentHash;

        // ---- collection -----------------------------------------------------

        [Test]
        public void UntouchedPropertiesAreNotEmitted()
        {
            // A schema default belongs to the control. Writing it out would make the compiled asset
            // churn whenever a default changes, and let the compiler override the backend.
            Add("Plain", "Panel");

            var node = Node("Plain");

            Assert.IsTrue(node.ControlProperties == null || node.ControlProperties.Length == 0);
        }

        [Test]
        public void AnOverriddenPropertyReachesTheProgram()
        {
            var element = Add("Field", "Panel");
            Override(element, "media.raycastTarget", Flag(false));

            var node = Node("Field");

            Assert.IsTrue(node.TryGetProperty("media.raycastTarget", out var property),
                "An overridden property must survive the compile.");
            Assert.AreEqual(NexPropertyKind.Flag, property.Kind);
            Assert.IsFalse(property.Flag);
        }

        [Test]
        public void LookupOfAnAbsentKeyReportsMissingRatherThanADefault()
        {
            Add("Plain", "Panel");

            Assert.IsFalse(Node("Plain").TryGetProperty("media.raycastTarget", out _));
        }

        // ---- content hash ----------------------------------------------------

        [Test]
        public void ChangingAPropertyChangesTheContentHash()
        {
            var element = Add("Field", "Panel");
            Override(element, "media.raycastTarget", Flag(true));
            var before = Hash();

            Override(element, "media.raycastTarget", Flag(false));

            Assert.AreNotEqual(before, Hash(),
                "The publisher skips writing on an unchanged hash, so an unhashed field never ships.");
        }

        [Test]
        public void ChangingABindingKeyChangesTheContentHash()
        {
            var element = Add("Bar", "Panel");
            element.binding.valueKey = "player.health";
            var before = Hash();

            element.binding.valueKey = "player.stamina";

            Assert.AreNotEqual(before, Hash());
        }

        [Test]
        public void ChangingABindingModeChangesTheContentHash()
        {
            var element = Add("Field", "Button");
            element.binding.textKey = "form.name";
            var before = Hash();

            element.binding.textMode = emiteat.NexUI.State.UIBindingMode.TwoWay;

            Assert.AreNotEqual(before, Hash(),
                "Direction is part of what the screen does, so it is part of its identity.");
        }

        [Test]
        public void ChangingAConverterChangesTheContentHash()
        {
            var element = Add("Label", "Label");
            element.binding.textKey = "score.value";
            var before = Hash();

            element.binding.textConverterKey = "thousands";

            Assert.AreNotEqual(before, Hash());
        }

        [Test]
        public void ChangingAnAccessibilityLabelChangesTheContentHash()
        {
            var element = Add("Close", "Button");
            element.text = "X";
            var before = Hash();

            element.accessibilityLabel = "Close the inventory";

            Assert.AreNotEqual(before, Hash(),
                "What a screen announces is part of the screen, not a note beside it.");
        }

        [Test]
        public void AnUnchangedScreenKeepsItsHash()
        {
            // The other half of the contract: if this drifted, every save would rewrite the asset
            // and every screen would show up in source control on every build.
            var element = Add("Field", "Panel");
            Override(element, "media.raycastTarget", Flag(true));

            Assert.AreEqual(Hash(), Hash());
        }

        [Test]
        public void PropertyOrderIsStableAcrossCompiles()
        {
            var element = Add("Field", "Panel");
            Override(element, "media.raycastTarget", Flag(false));
            Override(element, "media.maskable", Flag(true));

            var first = Node("Field").ControlProperties.Select(p => p.Key).ToArray();
            var second = Node("Field").ControlProperties.Select(p => p.Key).ToArray();

            CollectionAssert.AreEqual(first, second,
                "A data-dependent order would make the hash change without the screen changing.");
        }
    }
}
