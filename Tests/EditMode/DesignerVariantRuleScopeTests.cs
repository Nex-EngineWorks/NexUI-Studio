using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Properties;
using NUnit.Framework;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// What a component variant rule is allowed to change, and when it applies.
    /// </summary>
    /// <remarks>
    /// Two gaps are covered. Motion and Theme had no <see cref="DesignerPropertyId"/> at all, so a
    /// variant could change an element's colour but not the animation it plays or the theme class it
    /// carries. And a rule could only key off a variant axis, so "use the compact arrangement on a
    /// narrow canvas" had to be a variant the user selected by hand on every instance.
    /// </remarks>
    public sealed class DesignerVariantRuleScopeTests
    {
        /// <summary>
        /// "No override was applied" is asserted as null-or-empty, not as null.
        /// </summary>
        /// <remarks>
        /// Expansion clones elements through <c>DesignerMetadataUtility.Clone</c>, which round-trips
        /// them via <c>JsonUtility</c> - and Unity's serializer turns a null string into <c>""</c>
        /// and never back. So a cloned element can never have a null <c>text</c>, and asserting on
        /// null was asserting something the serializer makes impossible rather than anything about
        /// variant rules.
        /// </remarks>
        private const string NoOverride = "no variant rule applied, so the text should be unset";

        private sealed class StubResolver : IDesignerComponentDefinitionResolver
        {
            public DesignerComponentDefinitionAsset Definition;
            public DesignerComponentDefinitionAsset Resolve(string guid, string id) => Definition;
        }

        private static DesignerComponentDefinitionAsset Definition()
        {
            var definition = ScriptableObject.CreateInstance<DesignerComponentDefinitionAsset>();
            definition.componentId = "card";
            definition.displayName = "Card";
            definition.version = 1;
            definition.rootElementId = "root";
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "root", stableId = "def-root", elementType = "Panel",
                rect = new Rect(0f, 0f, 200f, 120f)
            });
            definition.elements.Add(new DesignerElementMetadata
            {
                elementId = "title", stableId = "def-title", parentId = "root", elementType = "Label",
                rect = new Rect(8f, 8f, 180f, 24f)
            });
            return definition;
        }

        private static DesignerMetadataAsset Screen()
        {
            var screen = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            screen.schemaVersion = DesignerMetadataAsset.CurrentSchemaVersion;
            screen.elements.Add(new DesignerElementMetadata
            {
                elementId = "card1", stableId = "stable-card1", rect = new Rect(0f, 0f, 200f, 120f),
                componentInstance = new DesignerComponentInstanceMetadata
                {
                    definitionGuid = "guid-card", definitionId = "card", definitionVersion = 1
                }
            });
            return screen;
        }

        private static DesignerComponentPropertyOverride Override(string target, DesignerPropertyId id, string text)
            => new DesignerComponentPropertyOverride
            {
                targetElementId = target,
                targetStableId = "def-" + target,
                propertyId = id,
                value = new DesignerPropertyValue { type = DesignerPropertyValueType.String, stringValue = text }
            };

        private static DesignerElementMetadata Expanded(DesignerComponentDefinitionAsset definition,
            DesignerMetadataAsset screen, DesignerComponentVariantContext context, string idSuffix)
        {
            var expansion = DesignerComponentExpander.Expand(screen, new StubResolver { Definition = definition }, context);
            try
            {
                return expansion.Expanded.elements.First(e => e.elementId.EndsWith(idSuffix));
            }
            finally
            {
                expansion.Dispose();
            }
        }

        // ---- Motion and Theme are overridable ------------------------------------------------

        [Test]
        public void AVariantRuleCanChangeAnElementsMotion()
        {
            var definition = Definition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
                { propertyName = "emphasis", options = { "calm", "loud" }, defaultValue = "loud" });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "emphasis", equalsValue = "loud",
                overrides = { Override("title", DesignerPropertyId.MotionHoverVariant, "pulse") }
            });

            var title = Expanded(definition, Screen(), DesignerComponentVariantContext.Unknown, "title");

            Assert.AreEqual("pulse", title.motion.hoverVariant);
        }

        [Test]
        public void AVariantRuleCanChangeThemeClasses()
        {
            var definition = Definition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
                { propertyName = "tone", options = { "default", "danger" }, defaultValue = "danger" });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "tone", equalsValue = "danger",
                overrides = { Override("title", DesignerPropertyId.ThemeClasses, "text danger-fg") }
            });

            var title = Expanded(definition, Screen(), DesignerComponentVariantContext.Unknown, "title");

            CollectionAssert.AreEqual(new[] { "text", "danger-fg" }, title.theme.classes);
        }

        [Test]
        public void AVariantRuleCanChangeThemeTokens()
        {
            var definition = Definition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
                { propertyName = "tone", options = { "default", "danger" }, defaultValue = "danger" });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "tone", equalsValue = "danger",
                overrides = { Override("title", DesignerPropertyId.ThemeTokens, "fg=#FF0000;bg=#220000") }
            });

            var title = Expanded(definition, Screen(), DesignerComponentVariantContext.Unknown, "title");

            Assert.AreEqual(2, title.theme.tokenOverrides.Count);
            Assert.AreEqual("fg", title.theme.tokenOverrides[0].key);
            Assert.AreEqual("#FF0000", title.theme.tokenOverrides[0].value);
        }

        // ---- The text form of the list-valued theme fields -------------------------------------

        [Test]
        public void ClassListsRoundTrip()
        {
            var parsed = DesignerThemeValueCodec.ParseClasses("  a   b\tc ");

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, parsed);
            Assert.AreEqual("a b c", DesignerThemeValueCodec.FormatClasses(parsed));
        }

        [Test]
        public void TokenListsRoundTrip()
        {
            var parsed = DesignerThemeValueCodec.ParseTokens("fg=#fff;bg=#000");

            Assert.AreEqual("fg=#fff;bg=#000", DesignerThemeValueCodec.FormatTokens(parsed));
        }

        [Test]
        public void AMalformedTokenPairIsDroppedRatherThanStoredAsAnEmptyValue()
        {
            var parsed = DesignerThemeValueCodec.ParseTokens("fg=#fff;garbage;=novalue");

            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual("fg", parsed[0].key);
            Assert.IsNotNull(DesignerThemeValueCodec.ValidateTokens("fg=#fff;garbage"));
        }

        [Test]
        public void AnEmptyValueIsAnEmptyListNotANullOne()
        {
            CollectionAssert.IsEmpty(DesignerThemeValueCodec.ParseClasses(null));
            CollectionAssert.IsEmpty(DesignerThemeValueCodec.ParseTokens(string.Empty));
            Assert.AreEqual(string.Empty, DesignerThemeValueCodec.FormatClasses(null));
            Assert.AreEqual(string.Empty, DesignerThemeValueCodec.FormatTokens(new List<DesignerTokenOverride>()));
        }

        // ---- Environment-conditioned rules ------------------------------------------------------

        private static DesignerComponentDefinitionAsset WithCompactRule()
        {
            var definition = Definition();
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                constrainResolution = true,
                minResolution = new Vector2Int(0, 0),
                maxResolution = new Vector2Int(899, 9999),
                overrides = { Override("title", DesignerPropertyId.Text, "Compact") }
            });
            return definition;
        }

        [Test]
        public void ARuleWithNoVariantAxisAppliesFromItsEnvironmentConditionAlone()
        {
            var title = Expanded(WithCompactRule(), Screen(),
                new DesignerComponentVariantContext(new Vector2Int(800, 600)), "title");

            Assert.AreEqual("Compact", title.text);
        }

        [Test]
        public void TheSameRuleDoesNotApplyOutsideItsRange()
        {
            var title = Expanded(WithCompactRule(), Screen(),
                new DesignerComponentVariantContext(new Vector2Int(1920, 1080)), "title");

            Assert.IsTrue(string.IsNullOrEmpty(title.text), NoOverride);
        }

        [Test]
        public void AnInputModeConditionIsHonoured()
        {
            var definition = Definition();
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                constrainInputMode = true, inputMode = UIInputMode.Gamepad,
                overrides = { Override("title", DesignerPropertyId.Text, "Press A") }
            });

            var gamepad = Expanded(definition, Screen(),
                new DesignerComponentVariantContext(new Vector2Int(1920, 1080), UIInputMode.Gamepad), "title");
            Assert.AreEqual("Press A", gamepad.text);

            var mouse = Expanded(definition, Screen(),
                new DesignerComponentVariantContext(new Vector2Int(1920, 1080), UIInputMode.KeyboardMouse), "title");
            Assert.IsTrue(string.IsNullOrEmpty(mouse.text), NoOverride);
        }

        /// <summary>
        /// Guessing a resolution would let a headless expansion produce a different tree than the
        /// canvas showed, and nothing would say so.
        /// </summary>
        [Test]
        public void WithoutACanvasTheRuleIsSkippedAndSaidSo()
        {
            var screen = Screen();
            var expansion = DesignerComponentExpander.Expand(screen,
                new StubResolver { Definition = WithCompactRule() }, DesignerComponentVariantContext.Unknown);
            try
            {
                var title = expansion.Expanded.elements.First(e => e.elementId.EndsWith("title"));
                Assert.IsTrue(string.IsNullOrEmpty(title.text), NoOverride);
                Assert.AreEqual(1, expansion.Issues.Count(i =>
                    i.Kind == DesignerComponentExpansionIssueKind.MissingVariantContext));
            }
            finally
            {
                expansion.Dispose();
            }
        }

        /// <summary>An unconditioned rule must not start reporting a missing context.</summary>
        [Test]
        public void AnOrdinaryVariantRuleNeedsNoEnvironment()
        {
            var definition = Definition();
            definition.variantProperties.Add(new DesignerComponentVariantProperty
                { propertyName = "size", options = { "small", "large" }, defaultValue = "large" });
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                propertyName = "size", equalsValue = "large",
                overrides = { Override("title", DesignerPropertyId.Text, "Large") }
            });

            var screen = Screen();
            var expansion = DesignerComponentExpander.Expand(screen,
                new StubResolver { Definition = definition }, DesignerComponentVariantContext.Unknown);
            try
            {
                Assert.AreEqual("Large",
                    expansion.Expanded.elements.First(e => e.elementId.EndsWith("title")).text);
                CollectionAssert.IsEmpty(expansion.Issues.Where(i =>
                    i.Kind == DesignerComponentExpansionIssueKind.MissingVariantContext));
            }
            finally
            {
                expansion.Dispose();
            }
        }

        /// <summary>Both conditions on one rule means both must hold.</summary>
        [Test]
        public void ResolutionAndInputModeConditionsAreAnded()
        {
            var definition = Definition();
            definition.variantRules.Add(new DesignerComponentVariantRule
            {
                constrainResolution = true,
                minResolution = new Vector2Int(0, 0), maxResolution = new Vector2Int(899, 9999),
                constrainInputMode = true, inputMode = UIInputMode.Touch,
                overrides = { Override("title", DesignerPropertyId.Text, "Tap") }
            });

            Assert.AreEqual("Tap", Expanded(definition, Screen(),
                new DesignerComponentVariantContext(new Vector2Int(800, 600), UIInputMode.Touch), "title").text);
            Assert.IsTrue(string.IsNullOrEmpty(Expanded(definition, Screen(),
                new DesignerComponentVariantContext(new Vector2Int(800, 600), UIInputMode.Gamepad), "title").text), NoOverride);
            Assert.IsTrue(string.IsNullOrEmpty(Expanded(definition, Screen(),
                new DesignerComponentVariantContext(new Vector2Int(1920, 1080), UIInputMode.Touch), "title").text), NoOverride);
        }
    }
}
