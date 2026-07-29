using System.Collections.Generic;
using emiteat.NexUI.Accessibility;
using UnityEngine;
using G = emiteat.NexUI.Designer.Editor.Components.DesignerPaletteGroup;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Shared archetype factories for NexUI's own component catalogs. An archetype fixes the states,
    /// binding channels and accessibility role every component of that shape must declare, which is
    /// what keeps hundreds of descriptors consistent: a catalog entry is one line, and it cannot
    /// forget that an input needs Error/Focused states or that a collection needs an Empty state.
    ///
    /// Used through <c>using static</c> by <see cref="NexUILibraryCatalog"/> and
    /// <see cref="NexUIGameCatalog"/>.
    /// </summary>
    internal static class NexUIComponentArchetypes
    {
        internal const DesignerComponentState Interactive =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Pressed |
            DesignerComponentState.Focused | DesignerComponentState.Disabled;
        internal const DesignerComponentState FieldStates =
            DesignerComponentState.Normal | DesignerComponentState.Hover | DesignerComponentState.Focused |
            DesignerComponentState.Disabled | DesignerComponentState.Error | DesignerComponentState.Success;
        internal const DesignerComponentState CollectionStates =
            DesignerComponentState.Normal | DesignerComponentState.Loading |
            DesignerComponentState.Empty | DesignerComponentState.Error;
        internal const DesignerComponentState SeverityStates =
            DesignerComponentState.Normal | DesignerComponentState.Success |
            DesignerComponentState.Warning | DesignerComponentState.Error;

        internal const DesignerBindingChannel B_Text = DesignerBindingChannel.Text;
        internal const DesignerBindingChannel B_Value = DesignerBindingChannel.Value;
        internal const DesignerBindingChannel B_Vis = DesignerBindingChannel.Visibility;
        internal const DesignerBindingChannel B_Class = DesignerBindingChannel.Class;
        internal const DesignerBindingChannel B_Cmd = DesignerBindingChannel.Command;
        internal const DesignerBindingChannel B_Inter = DesignerBindingChannel.Interactable;

        internal static readonly Color Surface = new Color(0.13f, 0.18f, 0.26f, 1f);
        internal static readonly Color Field2 = new Color(0.10f, 0.14f, 0.20f, 1f);
        internal static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);

        /// <summary>Static text.</summary>
        internal static DesignerComponentDescriptor Text(string id, string name, float w, float h, string description,
            string text = null, bool value = false, bool children = false,
            AccessibilityRole role = AccessibilityRole.Label, string group = null)
            => Make(id, name, group ?? G.TextMedia, DesignerComponentCategory.Text, w, h, description,
                text: text, color: Transparent, role: role,
                bindings: B_Text | B_Vis | B_Class | (value ? B_Value : 0),
                valueComponent: value, canHaveChildren: children,
                uxml: "ui:Label", uxmlText: true);

        /// <summary>Image-shaped leaf.</summary>
        internal static DesignerComponentDescriptor Media(string id, string name, float w, float h, string description,
            bool interactive = false, bool children = false, DesignerElementShape shape = DesignerElementShape.Rounded)
            => Make(id, name, G.Media, DesignerComponentCategory.Media, w, h, description,
                role: AccessibilityRole.Image, shape: shape, color: Surface,
                bindings: B_Value | B_Vis | B_Class | (interactive ? B_Cmd | B_Inter : 0),
                states: interactive ? Interactive : DesignerComponentState.Normal,
                interactive: interactive, canHaveChildren: children,
                uxml: "ui:Image");

        /// <summary>Command-driven control.</summary>
        internal static DesignerComponentDescriptor Control(string id, string name, float w, float h, string description,
            string text = null, bool value = false, bool selectable = false, bool children = false,
            DesignerElementShape shape = DesignerElementShape.Rounded,
            AccessibilityRole role = AccessibilityRole.Button, string uxml = null, string group = null)
            => Make(id, name, group ?? G.Controls, DesignerComponentCategory.Input, w, h, description,
                text: text, color: Field2, shape: shape, role: role,
                states: Interactive | (selectable ? DesignerComponentState.Selected : 0),
                bindings: B_Vis | B_Class | B_Cmd | B_Inter | (text != null ? B_Text : 0) | (value ? B_Value : 0),
                interactive: true, valueComponent: value, canHaveChildren: children,
                events: value ? new[] { "ValueChanged" } : new[] { "Click" },
                uxml: uxml, uxmlText: uxml != null && text != null);

        /// <summary>Text/number input.</summary>
        internal static DesignerComponentDescriptor Field(string id, string name, float w, float h, string description,
            string uxml = "ui:TextField", string group = null)
            => Make(id, name, group ?? G.Controls, DesignerComponentCategory.Input, w, h, description,
                color: Field2, role: AccessibilityRole.TextField,
                states: FieldStates, bindings: B_Text | B_Value | B_Vis | B_Class | B_Inter,
                interactive: true,
                events: new[] { "ValueChanged", "Submitted", "Focus", "Blur" },
                slots: new[] { Slot("leading", "Leading", 0, 1), Slot("trailing", "Trailing", 0, 1), Slot("helper", "Helper Text", 0, 1) },
                uxml: uxml);

        /// <summary>Value indicator (bar, gauge, meter).</summary>
        internal static DesignerComponentDescriptor Meter(string id, string name, float w, float h, string description,
            DesignerElementShape shape = DesignerElementShape.Rounded, string group = null)
            => Make(id, name, group ?? G.Feedback, DesignerComponentCategory.Feedback, w, h, description,
                color: Surface, shape: shape, role: AccessibilityRole.ProgressIndicator,
                states: DesignerComponentState.Normal | DesignerComponentState.Indeterminate |
                        DesignerComponentState.Warning | DesignerComponentState.Error | DesignerComponentState.Success,
                bindings: B_Value | B_Vis | B_Class, valueComponent: true,
                events: new[] { "ValueChanged" },
                uxml: "ui:ProgressBar");

        /// <summary>Status/notification badge or readout.</summary>
        internal static DesignerComponentDescriptor Status(string id, string name, float w, float h, string description,
            string text = null, bool value = false, bool children = false, bool overlay = false,
            DesignerElementShape shape = DesignerElementShape.Rounded, string group = null)
            => Make(id, name, group ?? G.Feedback, DesignerComponentCategory.Feedback, w, h, description,
                text: text, color: Surface, shape: shape, role: AccessibilityRole.Label,
                states: SeverityStates, bindings: B_Text | B_Vis | B_Class | (value ? B_Value : 0),
                valueComponent: value, canHaveChildren: children, overlay: overlay,
                minSize: new Vector2(8, 8),
                uxml: "ui:Label", uxmlText: text != null);

        /// <summary>Layout/grouping container.</summary>
        internal static DesignerComponentDescriptor Container(string id, string name, string group, float w, float h,
            string description, string text = null, DesignerComponentSlot[] slots = null, bool children = true,
            bool interactive = false, bool selectable = false, bool value = false, bool overlay = false,
            DesignerComponentState states = DesignerComponentState.Normal,
            AccessibilityRole role = AccessibilityRole.Container,
            DesignerElementShape shape = DesignerElementShape.Rounded,
            Color? color = null, string uxml = null, bool uxmlText = false)
            => Make(id, name, group, DesignerComponentCategory.Container, w, h, description,
                text: text, color: color ?? Surface, shape: shape,
                // An overlay container is a surface the player is meant to answer, so it reads as a
                // dialog to assistive tech unless the caller asked for something else.
                role: overlay && role == AccessibilityRole.Container ? AccessibilityRole.Dialog : role,
                states: interactive ? Interactive | (selectable ? DesignerComponentState.Selected : 0) : states,
                bindings: B_Vis | B_Class | (text != null ? B_Text : 0) | (value ? B_Value : 0) |
                          (interactive ? B_Cmd | B_Inter : 0),
                container: children, canHaveChildren: children, interactive: interactive, valueComponent: value,
                overlay: overlay, slots: slots, uxml: uxml, uxmlText: uxmlText);

        /// <summary>Collection bound to an items source, with an item template slot.</summary>
        internal static DesignerComponentDescriptor Collection(string id, string name, string group, float w, float h,
            string description, string templateSlotId, string templateSlotName, bool overlay = false,
            DesignerElementShape shape = DesignerElementShape.Rounded)
            => Make(id, name, group, DesignerComponentCategory.Data, w, h, description,
                color: Surface, shape: shape, role: AccessibilityRole.List,
                states: CollectionStates, bindings: B_Value | B_Vis | B_Class,
                container: true, canHaveChildren: true, collection: true, overlay: overlay,
                events: new[] { "SelectionChanged", "ItemActivated" },
                slots: new[]
                {
                    Slot(templateSlotId, templateSlotName, 0, 1, template: true),
                    Slot("empty", "Empty State", 0, 1)
                });

        /// <summary>Modal/overlay surface.</summary>
        internal static DesignerComponentDescriptor Dialog(string id, string name, float w, float h, string description,
            string text)
            => Make(id, name, G.Overlay, DesignerComponentCategory.Overlay, w, h, description,
                text: text, color: new Color(0.08f, 0.10f, 0.14f, 0.96f), role: AccessibilityRole.Dialog,
                bindings: B_Vis | B_Cmd | (text != null ? B_Text : 0),
                container: true, canHaveChildren: true, overlay: true,
                events: new[] { "Opened", "Confirmed", "Dismissed", "Closed" },
                slots: new[]
                {
                    Slot("header", "Header", 0, 1), Slot("content", "Content"),
                    Slot("actions", "Actions", 0, 1)
                });

        /// <summary>Data visualization. Preview-only on stock backends - the drawing is the runtime's job.</summary>
        internal static DesignerComponentDescriptor Chart(string id, string name, float w, float h, string description,
            DesignerElementShape shape = DesignerElementShape.Rounded)
        {
            var descriptor = Make(id, name, G.Charts, DesignerComponentCategory.Data, w, h, description,
                color: Surface, shape: shape, role: AccessibilityRole.Image,
                states: CollectionStates, bindings: B_Value | B_Vis | B_Class,
                canHaveChildren: true, collection: true,
                events: new[] { "PointSelected" },
                slots: new[] { Slot("legend", "Legend", 0, 1), Slot("empty", "Empty State", 0, 1) });
            // No stock uGUI/UI Toolkit control draws a chart: the Designer places and styles the box,
            // and the Save Report says the series rendering is not written.
            descriptor.UGUISupport = DesignerBackendSupport.PreviewOnly;
            descriptor.UIToolkitSupport = DesignerBackendSupport.PreviewOnly;
            return descriptor;
        }

        internal static DesignerComponentSlot Slot(string id, string name, int min = 0, int max = int.MaxValue,
            bool template = false)
            => DesignerComponentRegistry.MakeSlot(id, name, min, max, template);

        // Palette order is assigned per group in declaration order, starting after the foundational
        // catalog's explicit orders, so entries land below the components they extend.
        internal static readonly Dictionary<string, int> Orders = new Dictionary<string, int>();

        internal static int NextOrder(string group)
        {
            Orders.TryGetValue(group, out var index);
            Orders[group] = index + 1;
            return 100 + index;
        }

        internal static DesignerComponentDescriptor Make(string id, string name, string group,
            DesignerComponentCategory category, float w, float h, string description,
            string text = null, Color? color = null,
            DesignerElementShape shape = DesignerElementShape.Rounded,
            AccessibilityRole role = AccessibilityRole.None,
            DesignerComponentState states = DesignerComponentState.Normal,
            DesignerBindingChannel bindings = DesignerBindingChannel.Visibility,
            bool container = false, bool canHaveChildren = false, bool interactive = false,
            bool valueComponent = false, bool collection = false, bool overlay = false,
            string[] events = null, DesignerComponentSlot[] slots = null,
            Vector2? minSize = null, string uxml = null, bool uxmlText = false)
        {
            var descriptor = new DesignerComponentDescriptor
            {
                TypeId = id,
                DisplayName = name,
                LocalizationKey = "component." + char.ToLowerInvariant(id[0]) + id.Substring(1),
                Category = category,
                Family = DesignerComponentFamily.NexUI,
                Description = description,
                PaletteGroup = group,
                PaletteOrder = NextOrder(group),
                DefaultSize = new Vector2(w, h),
                MinimumSize = minSize ?? new Vector2(Mathf.Min(24f, w), Mathf.Min(24f, h)),
                DefaultShape = shape,
                DefaultColor = color ?? Surface,
                DefaultText = text,
                CanHaveChildren = container || canHaveChildren || (slots != null && slots.Length > 0),
                IsContainer = container,
                IsInteractive = interactive,
                IsValueComponent = valueComponent,
                IsCollectionComponent = collection,
                IsOverlayComponent = overlay,
                DefaultAccessibilityRole = role,
                SupportedStates = states,
                SupportedBindings = bindings,
                UxmlTag = uxml,
                UxmlHasText = uxmlText,
                // Structure, text and style are written to both backends; the behaviour behind them
                // (a chart's series, a joystick's vector) belongs to the runtime.
                UGUISupport = DesignerBackendSupport.Partial,
                UIToolkitSupport = DesignerBackendSupport.Partial
            };

            if (events != null) descriptor.SupportedEvents.AddRange(events);
            if (slots != null) descriptor.Slots.AddRange(slots);
            else if (descriptor.CanHaveChildren)
                descriptor.Slots.Add(DesignerComponentRegistry.MakeSlot(DesignerComponentSlot.Content, "Content"));

            return descriptor;
        }
    }
}
