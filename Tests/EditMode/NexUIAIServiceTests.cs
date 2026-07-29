using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.AI;
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
    }
}
