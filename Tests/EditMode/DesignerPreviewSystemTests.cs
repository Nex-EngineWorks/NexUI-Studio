using System;
using System.Linq;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Preview;
using NUnit.Framework;

namespace emiteat.NexUI.Designer.Tests.EditMode
{
    /// <summary>
    /// Tests for the preview renderer registry, state color modulation, the preview log, and the
    /// context's Interactive-mode simulation (which must log, never dispatch real commands).
    /// </summary>
    public sealed class DesignerPreviewSystemTests
    {
        [Test]
        public void EveryElementTypeHasAPreviewRenderer()
        {
            foreach (DesignerElementType type in Enum.GetValues(typeof(DesignerElementType)))
                Assert.IsNotNull(DesignerComponentPreviewRegistry.Get(type.ToString()), $"No renderer for {type}");
        }

        [Test]
        public void UnknownTypeGetsGenericRenderer()
        {
            Assert.IsInstanceOf<GenericPreviewRenderer>(DesignerComponentPreviewRegistry.Get("WhoKnows"));
        }

        [Test]
        public void HotbarHasDedicatedRenderer()
        {
            Assert.IsInstanceOf<HotbarPreviewRenderer>(DesignerComponentPreviewRegistry.Get("Hotbar"));
        }

        [Test]
        public void EventPayloads_ProvideExampleForKnownEvents()
        {
            StringAssert.Contains("targetSlotId", emiteat.NexUI.Designer.Editor.Inspectors.DesignerEventPayloads.Example("Slot", "Dropped"));
            Assert.AreEqual("{ }", emiteat.NexUI.Designer.Editor.Inspectors.DesignerEventPayloads.Example("Panel", "Nonexistent"));
        }

        [Test]
        public void ValueAndCollectionTypesGetDedicatedRenderers()
        {
            Assert.IsInstanceOf<LinearFillPreviewRenderer>(DesignerComponentPreviewRegistry.Get("ProgressBar"));
            Assert.IsInstanceOf<CollectionPreviewRenderer>(DesignerComponentPreviewRegistry.Get("List"));
            Assert.IsInstanceOf<SlotPreviewRenderer>(DesignerComponentPreviewRegistry.Get("Slot"));
        }

        [Test]
        public void StateModulation_DisabledDimsAndDesaturates()
        {
            var baseColor = new UnityEngine.Color(0.2f, 0.4f, 0.8f, 1f);
            var disabled = DesignerPreviewColors.Modulate(baseColor, DesignerComponentState.Disabled);
            Assert.Less(disabled.a, baseColor.a, "disabled should reduce alpha");
        }

        [Test]
        public void StateBorder_OnlyForSignalStates()
        {
            Assert.IsTrue(DesignerPreviewColors.StateBorder(DesignerComponentState.Error).HasValue);
            Assert.IsTrue(DesignerPreviewColors.StateBorder(DesignerComponentState.Selected).HasValue);
            Assert.IsFalse(DesignerPreviewColors.StateBorder(DesignerComponentState.Normal).HasValue);
        }

        [Test]
        public void PreviewLog_NewestFirst_AndClears()
        {
            var log = new DesignerPreviewLog();
            log.Log(DesignerPreviewLogKind.Info, null, "first");
            log.Log(DesignerPreviewLogKind.Info, null, "second");
            Assert.AreEqual("second", log.Entries[0].Message);
            log.Clear();
            Assert.AreEqual(0, log.Entries.Count);
        }

        [Test]
        public void PreviewLog_CommandSimulatedEventFiresOnlyForCommands()
        {
            var log = new DesignerPreviewLog();
            var fired = 0;
            log.CommandSimulated += _ => fired++;
            log.Log(DesignerPreviewLogKind.State, null, "state");
            Assert.AreEqual(0, fired);
            log.Log(DesignerPreviewLogKind.Command, "btn", "cmd");
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void SimulatePrimaryInteraction_LogsCommandForInteractiveTypeOnly()
        {
            var context = new NexUIDesignerContext();

            var button = new DesignerElementMetadata { elementId = "btn", elementType = "Button" };
            button.binding.commandKey = "confirm";
            Assert.IsTrue(context.SimulatePrimaryInteraction(button));
            Assert.AreEqual(DesignerPreviewLogKind.Command, context.PreviewLog.Entries[0].Kind);
            StringAssert.Contains("confirm", context.PreviewLog.Entries[0].Message);

            var label = new DesignerElementMetadata { elementId = "lbl", elementType = "Label" };
            Assert.IsFalse(context.SimulatePrimaryInteraction(label), "non-interactive types are not simulated");
        }

        [Test]
        public void SetForcedPreviewState_LogsAndUpdates()
        {
            var context = new NexUIDesignerContext();
            context.SetForcedPreviewState(DesignerComponentState.Hover);
            Assert.AreEqual(DesignerComponentState.Hover, context.ForcedPreviewState);
            Assert.AreEqual(DesignerPreviewLogKind.State, context.PreviewLog.Entries[0].Kind);
        }

        [Test]
        public void InteractionMode_TogglesAndResetsStateOnReturnToDesign()
        {
            var context = new NexUIDesignerContext();
            context.SetInteractionMode(DesignerInteractionMode.Interactive);
            context.SetForcedPreviewState(DesignerComponentState.Pressed);
            Assert.IsTrue(context.IsInteractive);

            context.SetInteractionMode(DesignerInteractionMode.Design);
            Assert.IsFalse(context.IsInteractive);
            Assert.AreEqual(DesignerComponentState.Normal, context.ForcedPreviewState, "returning to Design clears forced state");
        }

        [Test]
        public void RebuildPreview_PreservesMultiSelectionAndKeyObject()
        {
            var context = new NexUIDesignerContext();
            var metadata = UnityEngine.ScriptableObject.CreateInstance<DesignerMetadataAsset>();
            var first = new DesignerElementMetadata { elementId = "first" };
            var second = new DesignerElementMetadata { elementId = "second" };
            metadata.elements.Add(first);
            metadata.elements.Add(second);
            context.SetMetadata(metadata);
            context.SelectMany(new[] { first, second });
            context.SetKeyObject(first);

            context.RebuildPreview();

            CollectionAssert.AreEqual(new[] { "first", "second" }, context.SelectedElements.Select(item => item.elementId));
            Assert.That(context.KeyObject, Is.SameAs(first));
            context.Dispose();
            UnityEngine.Object.DestroyImmediate(metadata);
        }
    }
}
