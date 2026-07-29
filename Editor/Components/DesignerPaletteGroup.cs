using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;

namespace emiteat.NexUI.Designer.Editor.Components
{
    /// <summary>
    /// Palette folder ids. Each id is also the localization key of the folder title, so the palette
    /// reads in the editor language instead of mixing English folder names into a Korean UI.
    /// Descriptors reference these through <see cref="DesignerComponentDescriptor.PaletteGroup"/>;
    /// <see cref="DesignerComponentPalette"/> turns them into the ordered folder list every palette
    /// surface renders - there is no second hardcoded component list anywhere.
    /// </summary>
    public static class DesignerPaletteGroup
    {
        // NexUI's own component library.
        public const string Containers = "palette.group.containers";
        public const string Layout = "palette.group.layout";
        public const string TextMedia = "palette.group.textMedia";
        public const string Media = "palette.group.media";
        public const string Controls = "palette.group.controls";
        public const string Selection = "palette.group.selection";
        public const string Navigation = "palette.group.navigation";
        public const string Feedback = "palette.group.feedback";
        public const string Overlay = "palette.group.overlay";
        public const string Data = "palette.group.data";
        public const string Charts = "palette.group.charts";
        public const string Social = "palette.group.social";
        public const string Commerce = "palette.group.commerce";
        public const string Settings = "palette.group.settings";
        // Game UI is large enough to need its own shelves: a HUD readout, a loot table row and a
        // match-result screen are different jobs and would be unfindable in one folder.
        public const string Game = "palette.group.game";
        public const string GameWorld = "palette.group.game.world";
        public const string GameItems = "palette.group.game.items";
        public const string GameProgression = "palette.group.game.progression";
        public const string GameMenu = "palette.group.game.menu";
        public const string GameMultiplayer = "palette.group.game.multiplayer";

        // Unity uGUI stock controls (GameObject > UI menu).
        public const string UGUIBasic = "palette.group.ugui.basic";
        public const string UGUIControls = "palette.group.ugui.controls";
        public const string UGUIContainers = "palette.group.ugui.containers";

        // Unity UI Toolkit stock controls (UI Builder Library).
        public const string UITKBasic = "palette.group.uitk.basic";
        public const string UITKControls = "palette.group.uitk.controls";
        public const string UITKFields = "palette.group.uitk.fields";
        public const string UITKContainers = "palette.group.uitk.containers";

        /// <summary>Folder order in the palette. Groups not listed here are appended alphabetically.</summary>
        public static readonly string[] Order =
        {
            Containers, Layout, TextMedia, Media, Controls, Selection, Navigation, Feedback, Overlay,
            Data, Charts, Social, Commerce, Settings,
            Game, GameWorld, GameItems, GameProgression, GameMenu, GameMultiplayer,
            UGUIBasic, UGUIControls, UGUIContainers,
            UITKBasic, UITKControls, UITKFields, UITKContainers
        };

        public static string Title(string groupId) => DesignerLocalization.T(groupId);

        public static DesignerComponentFamily FamilyOf(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return DesignerComponentFamily.NexUI;
            if (groupId.StartsWith("palette.group.ugui.", System.StringComparison.Ordinal)) return DesignerComponentFamily.UGUI;
            if (groupId.StartsWith("palette.group.uitk.", System.StringComparison.Ordinal)) return DesignerComponentFamily.UIToolkit;
            return DesignerComponentFamily.NexUI;
        }
    }

    /// <summary>One palette folder: its id/title plus the descriptors that declared it, in order.</summary>
    public sealed class DesignerPaletteGroupView
    {
        public string GroupId;
        public DesignerComponentFamily Family;
        public readonly List<DesignerComponentDescriptor> Items = new List<DesignerComponentDescriptor>();
        public string Title => DesignerPaletteGroup.Title(GroupId);
    }

    /// <summary>
    /// Builds the palette's folder list straight from <see cref="DesignerComponentRegistry"/>. Adding
    /// a component type therefore means adding one descriptor - never editing a panel.
    /// </summary>
    public static class DesignerComponentPalette
    {
        public static List<DesignerPaletteGroupView> BuildGroups()
        {
            var byId = new Dictionary<string, DesignerPaletteGroupView>();
            var order = new List<string>();

            foreach (var descriptor in DesignerComponentRegistry.All)
            {
                if (descriptor == null || string.IsNullOrEmpty(descriptor.PaletteGroup)) continue; // not palette-creatable
                if (!byId.TryGetValue(descriptor.PaletteGroup, out var group))
                {
                    group = new DesignerPaletteGroupView
                    {
                        GroupId = descriptor.PaletteGroup,
                        Family = descriptor.Family
                    };
                    byId.Add(descriptor.PaletteGroup, group);
                    order.Add(descriptor.PaletteGroup);
                }
                group.Items.Add(descriptor);
            }

            foreach (var group in byId.Values)
                group.Items.Sort((a, b) =>
                {
                    var byOrder = a.PaletteOrder.CompareTo(b.PaletteOrder);
                    return byOrder != 0 ? byOrder : string.CompareOrdinal(a.TypeId, b.TypeId);
                });

            var result = new List<DesignerPaletteGroupView>();
            foreach (var groupId in DesignerPaletteGroup.Order)
                if (byId.TryGetValue(groupId, out var group)) { result.Add(group); byId.Remove(groupId); }

            // Groups introduced by a descriptor that predates the Order table still show up, in
            // registration order, rather than silently disappearing from the palette.
            foreach (var groupId in order)
                if (byId.TryGetValue(groupId, out var group)) { result.Add(group); byId.Remove(groupId); }

            return result;
        }

        /// <summary>Localized display name for a descriptor (falls back to its English DisplayName).</summary>
        public static string DisplayName(DesignerComponentDescriptor descriptor)
        {
            if (descriptor == null) return string.Empty;
            if (string.IsNullOrEmpty(descriptor.LocalizationKey)) return descriptor.DisplayName;
            var localized = DesignerLocalization.T(descriptor.LocalizationKey);
            return localized == descriptor.LocalizationKey ? descriptor.DisplayName : localized;
        }
    }
}
