using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.AI;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    public sealed class NexUIAIServiceTests
    {
        private DesignerMetadataAsset _metadata;
        private NexUIDesignerContext _context;

        private float _originalGridSize;
        private bool _originalSnap;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            _metadata = ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            _metadata.screenId = "test-screen";
            _context = new NexUIDesignerContext();

            // An applied plan states exact rects, so grid snapping (an EditorPrefs value shared with
            // the Designer window) must not silently round them. Restored in TearDown.
            _originalGridSize = _context.GridSize;
            _originalSnap = _context.SnapEnabled;
            _context.SetSnap(false);

            _context.SetMetadata(_metadata);
        }

        [TearDown]
        public void TearDown()
        {
            _context.SetGridSize(_originalGridSize);
            _context.SetSnap(_originalSnap);
            _context.Dispose();
            Object.DestroyImmediate(_metadata);
            Undo.ClearAll();
        }

        [Test]
        public void ParserAcceptsFencedJson()
        {
            const string response = "```json\n{\"message\":\"Ready\",\"actions\":[{\"type\":\"select\",\"targetId\":\"title\"}]}\n```";

            Assert.IsTrue(NexUIAIPlanParser.TryParse(response, out var plan, out var error), error);
            Assert.That(plan.message, Is.EqualTo("Ready"));
            Assert.That(plan.actions, Has.Count.EqualTo(1));
            Assert.That(plan.actions[0].targetId, Is.EqualTo("title"));
        }

        [Test]
        public void ValidatorAllowsNewElementToBeUsedByLaterActions()
        {
            var plan = new NexUIAIActionPlan
            {
                actions = new List<NexUIAIAction>
                {
                    new NexUIAIAction { type = "create", elementId = "login_card", elementType = "Panel", hasRect = true, x = 100, y = 80, width = 360, height = 260 },
                    new NexUIAIAction { type = "set", targetId = "login_card", property = "displayName", value = "Login Card" },
                    new NexUIAIAction { type = "add_class", targetId = "login_card", value = "login-card" }
                }
            };

            var validation = NexUIAIActionService.Validate(_context, plan);

            CollectionAssert.IsEmpty(validation.Errors);
            Assert.IsTrue(validation.IsValid);
        }

        [Test]
        public void ValidatorRejectsUnknownPropertyAndOversizedPlans()
        {
            _metadata.elements.Add(new DesignerElementMetadata { elementId = "title", elementType = "Label" });
            var unknownProperty = new NexUIAIActionPlan
            {
                actions = new List<NexUIAIAction>
                {
                    new NexUIAIAction { type = "set", targetId = "title", property = "executeCSharp", value = "anything" }
                }
            };
            var oversized = new NexUIAIActionPlan();
            for (var i = 0; i <= NexUIAIActionService.MaxActions; i++)
                oversized.actions.Add(new NexUIAIAction { type = "select", targetId = "title" });

            Assert.IsFalse(NexUIAIActionService.Validate(_context, unknownProperty).IsValid);
            Assert.IsFalse(NexUIAIActionService.Validate(_context, oversized).IsValid);
        }

        [Test]
        public void ValidatorTreatsDeleteAsRemovingTheWholeSubtree()
        {
            _metadata.elements.Add(new DesignerElementMetadata { elementId = "panel", elementType = "Panel" });
            _metadata.elements.Add(new DesignerElementMetadata { elementId = "row", parentId = "panel", elementType = "Container" });
            _metadata.elements.Add(new DesignerElementMetadata { elementId = "label", parentId = "row", elementType = "Label" });
            var plan = new NexUIAIActionPlan
            {
                actions = new List<NexUIAIAction>
                {
                    new NexUIAIAction { type = "delete", targetId = "panel" },
                    new NexUIAIAction { type = "set", targetId = "label", property = "text", value = "Too late" }
                }
            };

            Assert.IsFalse(NexUIAIActionService.Validate(_context, plan).IsValid);
        }

        [Test]
        public void ApplyCreatesAndConfiguresElement()
        {
            var plan = new NexUIAIActionPlan
            {
                actions = new List<NexUIAIAction>
                {
                    new NexUIAIAction { type = "create", elementId = "headline", elementType = "Label", hasRect = true, x = 24, y = 40, width = 280, height = 52 },
                    new NexUIAIAction { type = "set", targetId = "headline", property = "text", value = "Welcome" },
                    new NexUIAIAction { type = "set", targetId = "headline", property = "textColor", value = "#43E6C2" }
                }
            };

            NexUIAIActionService.Apply(_context, plan);

            var element = _metadata.Find("headline");
            Assert.IsNotNull(element);
            Assert.That(element.elementType, Is.EqualTo("Label"));
            Assert.That(element.text, Is.EqualTo("Welcome"));
            Assert.That(element.rect, Is.EqualTo(new Rect(24, 40, 280, 52)));
            Assert.That(ColorUtility.ToHtmlStringRGB(element.textColor), Is.EqualTo("43E6C2"));
        }

        [Test]
        public void ApplyCreatesRegisteredUnityCatalogElement()
        {
            var plan = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.Create, elementId = "unity_button", elementType = "UGUI.Button", hasRect = true, x = 20, y = 30, width = 180, height = 44 }
            } };

            NexUIAIActionService.Apply(_context, plan);

            Assert.AreEqual("UGUI.Button", _metadata.Find("unity_button").elementType);
        }

        [Test]
        public void ContextSnapshotContainsOnlyCurrentScreenByDefault()
        {
            var element = new DesignerElementMetadata { elementId = "cta", elementType = "Button", text = "Continue" };
            _metadata.elements.Add(element);
            _context.SelectMetadata(element);

            var json = NexUIAIContextBuilder.Build(_context, false);

            StringAssert.Contains("\"metadataScreenId\": \"test-screen\"", json);
            StringAssert.Contains("\"elementId\": \"cta\"", json);
            StringAssert.Contains("\"selectedElementIds\"", json);
            StringAssert.DoesNotContain("\"project\":", json);
        }

        [Test]
        public void SelectedScopeFiltersContextAndRejectsOutsideTargets()
        {
            var selected = new DesignerElementMetadata { elementId = "selected", elementType = "Panel" };
            var child = new DesignerElementMetadata { elementId = "child", parentId = "selected", elementType = "Label" };
            var outside = new DesignerElementMetadata { elementId = "outside", elementType = "Button" };
            _metadata.elements.AddRange(new[] { selected, child, outside });
            _context.SelectMetadata(selected);
            var policy = NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);

            var snapshot = NexUIAIContextBuilder.Build(_context, false, policy);
            var plan = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.Set, targetId = "outside", property = "text", value = "blocked" }
            } };

            StringAssert.Contains("\"elementId\": \"selected\"", snapshot);
            StringAssert.Contains("\"elementId\": \"child\"", snapshot);
            StringAssert.DoesNotContain("\"elementId\": \"outside\"", snapshot);
            Assert.IsFalse(NexUIAIActionService.Validate(_context, plan, policy).IsValid);
        }

        [Test]
        public void PermissionPolicyBlocksDeleteAndAssetCreatingMotion()
        {
            var element = new DesignerElementMetadata { elementId = "panel", elementType = "Panel" };
            _metadata.elements.Add(element);
            _context.SelectMetadata(element);
            var policy = NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.SelectedSafe);
            var delete = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.Delete, targetId = "panel" }
            } };
            var transition = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.ApplyTransition, targetId = "panel", preset = "Fade", duration = .25f }
            } };

            Assert.IsFalse(NexUIAIActionService.Validate(_context, delete, policy).IsValid);
            Assert.IsFalse(NexUIAIActionService.Validate(_context, transition, policy).IsValid);
        }

        [Test]
        public void ApplyCanEditMotionAndAdvancedVisualStyle()
        {
            var element = new DesignerElementMetadata { elementId = "cta", elementType = "Button" };
            _metadata.elements.Add(element);
            var plan = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.SetMotion, targetId = "cta", property = "hoverVariant", value = "HoverLift" },
                new NexUIAIAction { type = NexUIAIActionTypes.Set, targetId = "cta", property = "visualStyle.opacity", value = "0.75" },
                new NexUIAIAction { type = NexUIAIActionTypes.Set, targetId = "cta", property = "typography.fontWeight", value = "Bold" }
            } };

            NexUIAIActionService.Apply(_context, plan);

            Assert.AreEqual("HoverLift", element.motion.hoverVariant);
            Assert.AreEqual(.75f, element.visualStyle.opacity, .0001f);
            Assert.AreEqual(DesignerFontWeight.Bold, element.typography.fontWeight);
        }

        [Test]
        public void ProviderRegistryIncludesHostedAndExtensibleProviders()
        {
            Assert.That(NexUIAIProviderRegistry.All, Has.Count.EqualTo(4));
            Assert.AreEqual("OPENAI_API_KEY", NexUIAIProviderRegistry.Get(NexUIAIProviderKind.OpenAI).EnvironmentVariable);
            Assert.AreEqual("ANTHROPIC_API_KEY", NexUIAIProviderRegistry.Get(NexUIAIProviderKind.Anthropic).EnvironmentVariable);
            Assert.AreEqual("GEMINI_API_KEY", NexUIAIProviderRegistry.Get(NexUIAIProviderKind.Gemini).EnvironmentVariable);
            Assert.IsTrue(NexUIAIProviderRegistry.Get(NexUIAIProviderKind.OpenAICompatible).CustomEndpoint);
            Assert.IsFalse(NexUIAIProviderRegistry.Get(NexUIAIProviderKind.OpenAICompatible).RequiresApiKey);
        }

        [Test]
        public void CustomMotionClipValidatesTracksValuesAndScope()
        {
            _metadata.elements.Add(new DesignerElementMetadata { elementId = "panel", elementType = "Panel" });
            var action = new NexUIAIAction
            {
                type = NexUIAIActionTypes.CreateMotionClip,
                clipName = "AIEntrance",
                assignTo = "entry",
                duration = 1f,
                fps = 60,
                motionTracks = new List<NexUIAIMotionTrack>
                {
                    new NexUIAIMotionTrack
                    {
                        targetId = "panel",
                        property = "AnchoredPosition",
                        keyframes = new List<NexUIAIMotionKeyframe>
                        {
                            new NexUIAIMotionKeyframe { time = 0f, value = "-80,0", easing = "EaseOutCubic" },
                            new NexUIAIMotionKeyframe { time = 1f, value = "0,0", easing = "Linear" }
                        }
                    }
                }
            };
            var plan = new NexUIAIActionPlan { actions = new List<NexUIAIAction> { action } };
            var policy = NexUIAIScopePolicy.ForPreset(NexUIAIScopePreset.FullDesigner);

            var validation = NexUIAIActionService.Validate(_context, plan, policy);
            Assert.IsTrue(validation.IsValid, string.Join("\n", validation.Errors));

            action.motionTracks[0].targetId = "outside";
            validation = NexUIAIActionService.Validate(_context, plan, policy);
            Assert.IsFalse(validation.IsValid);
        }

        [Test]
        public void ApplyEditsReusableComponentVariantAndExposedProperty()
        {
            var definition = DesignerBuiltInComponentCatalog.All[0].Definition;
            var plan = new NexUIAIActionPlan { actions = new List<NexUIAIAction>
            {
                new NexUIAIAction { type = NexUIAIActionTypes.InstantiateComponent, componentId = definition.componentId, elementId = "ai_component", hasRect = true, x = 0, y = 0, width = 320, height = 200 },
                new NexUIAIAction { type = NexUIAIActionTypes.SetComponentVariant, targetId = "ai_component", property = "state", value = "Disabled" },
                new NexUIAIAction { type = NexUIAIActionTypes.SetComponentProperty, targetId = "ai_component", property = "title", value = "AI Title" }
            } };

            NexUIAIActionService.Apply(_context, plan);

            var placed = _metadata.Find("ai_component");
            Assert.AreEqual("Disabled", placed.componentInstance.GetVariantSelection("state"));
            Assert.AreEqual("AI Title", placed.componentInstance.FindOverride("exposed:title").value.stringValue);
        }
    }
}
