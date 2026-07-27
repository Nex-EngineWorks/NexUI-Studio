using System;
using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.MotionClipEditor;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Designer.Editor.Productivity;
using emiteat.NexUI.Designer.Editor.Scenario;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    /// <summary>
    /// The Designer's right-click menus, built with <see cref="GenericMenu"/> so they behave
    /// exactly like Unity's own context menus: nested submenus, checkmarks for toggles, greyed-out
    /// (rather than missing) entries when an action does not apply, and separators between groups.
    ///
    /// <para>
    /// The canvas and the Hierarchy panel share <see cref="BuildElementSections"/>, so the same
    /// element always offers the same actions wherever you right-click it - one of the things that
    /// makes Unity's own menus predictable. Menus are rebuilt on every click and never cached, so
    /// enabled/checked state always reflects the live selection.
    /// </para>
    ///
    /// <para>
    /// Item labels are localization keys joined with '/' to form submenu paths; translations must
    /// therefore never contain a '/' of their own.
    /// </para>
    /// </summary>
    public static class NexUIDesignerContextMenu
    {
        /// <summary>The Library, grouped as it is in the Library panel, for the Create submenu.</summary>
        private static readonly (string categoryKey, (DesignerElementType type, string labelKey)[] items)[] CreateCategories =
        {
            ("shell.library.category.containers", new[] { (DesignerElementType.Panel, "component.panel"), (DesignerElementType.Card, "component.card"), (DesignerElementType.Container, "component.container"), (DesignerElementType.Modal, "component.modal") }),
            ("shell.library.category.textMedia", new[] { (DesignerElementType.Label, "component.label"), (DesignerElementType.Image, "component.image") }),
            ("shell.library.category.controls", new[] { (DesignerElementType.Button, "component.button"), (DesignerElementType.IconButton, "component.iconButton"), (DesignerElementType.ChoiceList, "component.choiceList") }),
            ("shell.library.category.feedback", new[] { (DesignerElementType.Toast, "component.toast"), (DesignerElementType.Tooltip, "component.tooltip"), (DesignerElementType.ProgressBar, "component.progressBar"), (DesignerElementType.Spinner, "component.spinner") }),
            ("shell.library.category.data", new[] { (DesignerElementType.List, "component.list"), (DesignerElementType.Grid, "component.grid"), (DesignerElementType.Slot, "component.slot"), (DesignerElementType.Skeleton, "component.skeleton") }),
        };

        private static readonly (DesignerAnchorPreset preset, string labelKey)[] AnchorPresets =
        {
            (DesignerAnchorPreset.TopLeft, "ctx.anchor.topLeft"),
            (DesignerAnchorPreset.Top, "ctx.anchor.top"),
            (DesignerAnchorPreset.TopRight, "ctx.anchor.topRight"),
            (DesignerAnchorPreset.Left, "ctx.anchor.left"),
            (DesignerAnchorPreset.Center, "ctx.anchor.center"),
            (DesignerAnchorPreset.Right, "ctx.anchor.right"),
            (DesignerAnchorPreset.BottomLeft, "ctx.anchor.bottomLeft"),
            (DesignerAnchorPreset.Bottom, "ctx.anchor.bottom"),
            (DesignerAnchorPreset.BottomRight, "ctx.anchor.bottomRight"),
            (DesignerAnchorPreset.Stretch, "ctx.anchor.stretch"),
        };

        // ---- entry points ---------------------------------------------------------------------

        /// <summary>
        /// Canvas right-click. With nothing under the cursor this is the "create / paste / view"
        /// menu; over one or more elements it is the full element menu, prefixed by a picker when
        /// elements overlap so the user can say which one they meant.
        /// </summary>
        public static void ShowForCanvas(NexUIDesignerContext context, Vector2 canvasPoint,
            Action<DesignerElementMetadata> requestRename, Action frameAll)
        {
            if (context == null) return;

            var hits = HitTest(context, canvasPoint);
            var menu = new GenericMenu();

            if (hits.Count == 0)
            {
                BuildCanvasSections(menu, context, canvasPoint, frameAll);
                menu.ShowAsContext();
                return;
            }

            if (hits.Count > 1)
            {
                foreach (var hit in hits)
                {
                    var captured = hit;
                    Item(menu, Path("ctx.selectElement") + "/" + Leaf(Label(captured)), true, () => context.SelectMetadata(captured));
                }
                menu.AddSeparator("");
            }

            var primary = hits[0];
            if (!context.IsSelected(primary)) context.SelectMetadata(primary);

            BuildElementSections(menu, context, primary, requestRename);
            menu.AddSeparator("");
            Item(menu, T("ctx.frameAll"), frameAll != null, () => frameAll?.Invoke());
            AddCreateSubmenu(menu, context, canvasPoint);
            menu.ShowAsContext();
        }

        /// <summary>
        /// Hierarchy row right-click. Same element actions as the canvas, plus the tree-only
        /// expand/collapse commands.
        /// </summary>
        public static void ShowForElement(NexUIDesignerContext context, DesignerElementMetadata element,
            Action<DesignerElementMetadata> requestRename, Action expandAll, Action collapseAll)
        {
            if (context == null || element == null) return;
            if (!context.IsSelected(element)) context.SelectMetadata(element);

            var menu = new GenericMenu();
            BuildElementSections(menu, context, element, requestRename);

            // Optional commands are omitted rather than shown greyed out - a permanently disabled
            // entry in every menu is noise, which is exactly what this pass is removing.
            menu.AddSeparator("");
            if (expandAll != null) Item(menu, T("ctx.expandAll"), true, expandAll);
            if (collapseAll != null) Item(menu, T("ctx.collapseAll"), true, collapseAll);
            AddCreateSubmenu(menu, context, null);
            menu.ShowAsContext();
        }

        /// <summary>Right-click on empty space in the Hierarchy panel.</summary>
        public static void ShowForHierarchyBackground(NexUIDesignerContext context, Action expandAll, Action collapseAll)
        {
            if (context == null) return;
            var menu = new GenericMenu();

            AddCreateSubmenu(menu, context, null);
            menu.AddSeparator("");
            Item(menu, T("ctx.paste"), context.HasClipboard, () => context.PasteSelection());
            menu.AddSeparator("");
            Item(menu, T("ctx.select.all"), HasElements(context), context.SelectAll);
            Item(menu, T("ctx.select.none"), context.SelectedElements.Count > 0, context.ClearSelection);
            menu.AddSeparator("");
            Item(menu, T("ctx.expandAll"), expandAll != null, () => expandAll?.Invoke());
            Item(menu, T("ctx.collapseAll"), collapseAll != null, () => collapseAll?.Invoke());
            menu.AddSeparator("");
            AddVisibilityResetItems(menu, context);

            menu.ShowAsContext();
        }

        // ---- sections -------------------------------------------------------------------------

        private static void BuildCanvasSections(GenericMenu menu, NexUIDesignerContext context,
            Vector2 canvasPoint, Action frameAll)
        {
            AddCreateSubmenu(menu, context, canvasPoint);
            menu.AddSeparator("");

            Item(menu, T("ctx.paste"), context.HasClipboard, () => context.PasteSelection());
            menu.AddSeparator("");

            Item(menu, T("ctx.select.all"), HasElements(context), context.SelectAll);
            Item(menu, T("ctx.select.none"), context.SelectedElements.Count > 0, context.ClearSelection);
            menu.AddSeparator("");

            Item(menu, T("ctx.frameAll"), frameAll != null, () => frameAll?.Invoke());
            Toggle(menu, Path("shell.canvas.snapping", "shell.canvas.gridSnap"), context.SnapEnabled, true,
                () => context.SetSnap(!context.SnapEnabled));
            foreach (var size in new[] { 1f, 2f, 4f, 8f, 16f, 32f })
            {
                var captured = size;
                Toggle(menu, Path("shell.canvas.snapping", "shell.canvas.gridSize") + "/" + size.ToString("0"),
                    Mathf.Approximately(context.GridSize, captured), true, () => context.SetGridSize(captured));
            }

            menu.AddSeparator("");
            AddVisibilityResetItems(menu, context);
        }

        /// <summary>
        /// Everything that applies to a concrete element. Shared verbatim by the canvas and the
        /// Hierarchy so an element never offers different actions depending on where it was clicked.
        /// </summary>
        private static void BuildElementSections(GenericMenu menu, NexUIDesignerContext context,
            DesignerElementMetadata primary, Action<DesignerElementMetadata> requestRename)
        {
            var hasSelection = context.SelectedElements.Count > 0;
            var children = context.GetChildren(primary);

            // Clipboard + lifecycle, in Unity's Hierarchy order.
            Item(menu, T("ctx.cut"), hasSelection, () => NexUIDesignerUndo.Group("Cut NexUI Elements", () =>
            {
                context.CopySelection();
                context.DeleteSelection();
            }));
            Item(menu, T("ctx.copy"), hasSelection, context.CopySelection);
            Item(menu, T("ctx.paste"), context.HasClipboard, () => context.PasteSelection());
            Item(menu, T("ctx.pasteAsChild"), context.HasClipboard, () => PasteAsChild(context, primary));
            Item(menu, T("ctx.duplicate"), hasSelection, () => context.DuplicateSelection());
            menu.AddSeparator("");

            Item(menu, T("ctx.rename"), requestRename != null, () => requestRename?.Invoke(primary));
            Item(menu, T("ctx.delete"), hasSelection, () => context.DeleteSelectedMetadata(true));
            Item(menu, T("ctx.deleteKeepChildren"), children.Count > 0, () => context.DeleteSelectedMetadata(false));
            menu.AddSeparator("");

            AddSelectSubmenu(menu, context, primary, children);
            AddHierarchySubmenu(menu, context, primary, children);
            AddArrangeSubmenus(menu, context);
            AddVisibilitySubmenu(menu, context, primary);
            menu.AddSeparator("");

            Item(menu, T("ctx.copyId"), true, () => EditorGUIUtility.systemCopyBuffer = primary.elementId);
            Item(menu, T("ctx.copyPath"), true, () => EditorGUIUtility.systemCopyBuffer = HierarchyPath(context, primary));
            menu.AddSeparator("");

            AddMotionSubmenu(menu, context, primary);
            AddLayoutSubmenu(menu, context);
            AddMockDataSubmenu(menu, context);
        }

        private static void AddSelectSubmenu(GenericMenu menu, NexUIDesignerContext context,
            DesignerElementMetadata primary, IReadOnlyList<DesignerElementMetadata> children)
        {
            Item(menu, Path("ctx.select", "ctx.select.parent"), !string.IsNullOrEmpty(primary.parentId),
                () => context.SelectParent(primary));
            Item(menu, Path("ctx.select", "ctx.select.children"), children.Count > 0,
                () => context.SelectChildren(primary));
            Item(menu, Path("ctx.select", "ctx.select.descendants"), children.Count > 0,
                () => context.SelectMany(context.GetDescendants(primary)));
            Item(menu, Path("ctx.select", "ctx.select.sameType"), HasElements(context), () =>
                context.SelectMany(Elements(context).Where(e => e.elementType == primary.elementType)));
            Item(menu, Path("ctx.select", "ctx.select.invert"), HasElements(context), () =>
                context.SelectMany(Elements(context).Where(e => !context.IsSelected(e)).ToList()));
            Item(menu, Path("ctx.select", "ctx.select.all"), HasElements(context), context.SelectAll);
            Item(menu, Path("ctx.select", "ctx.select.none"), context.SelectedElements.Count > 0, context.ClearSelection);
            Toggle(menu, Path("ctx.select", "ctx.select.keyObject"), context.KeyObject == primary,
                context.SelectedElements.Count > 1, () => context.SetKeyObject(primary));
        }

        private static void AddHierarchySubmenu(GenericMenu menu, NexUIDesignerContext context,
            DesignerElementMetadata primary, IReadOnlyList<DesignerElementMetadata> children)
        {
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.createEmptyParent"), context.SelectedElements.Count > 0,
                () => context.WrapSelectionInContainer());
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.group"), context.SelectedElements.Count >= 2,
                () => context.GroupSelection());
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.ungroup"), children.Count > 0,
                () => context.UngroupSelection());
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.moveToRoot"), !string.IsNullOrEmpty(primary.parentId),
                () => context.MoveSelectionToRoot());
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.firstSibling"), true,
                () => context.SetSiblingIndex(primary, 0));
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.lastSibling"), true,
                () => context.SetSiblingIndex(primary, int.MaxValue));
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.moveUp"), true, () => context.MoveSiblingBy(primary, -1));
            Item(menu, Path("ctx.hierarchy", "ctx.hierarchy.moveDown"), true, () => context.MoveSiblingBy(primary, 1));
        }

        private static void AddArrangeSubmenus(GenericMenu menu, NexUIDesignerContext context)
        {
            var hasSelection = context.SelectedElements.Count > 0;
            Item(menu, Path("ctx.order", "ctx.order.forward"), hasSelection, context.BringSelectionForward);
            Item(menu, Path("ctx.order", "ctx.order.backward"), hasSelection, context.SendSelectionBackward);
            Item(menu, Path("ctx.order", "ctx.order.front"), hasSelection, context.BringSelectionToFront);
            Item(menu, Path("ctx.order", "ctx.order.back"), hasSelection, context.SendSelectionToBack);

            // Aligning one element aligns it to the canvas, so these stay available at any count.
            Item(menu, Path("ctx.align", "ctx.align.left"), hasSelection, () => context.AlignSelection("left"));
            Item(menu, Path("ctx.align", "ctx.align.centerX"), hasSelection, () => context.AlignSelection("centerX"));
            Item(menu, Path("ctx.align", "ctx.align.right"), hasSelection, () => context.AlignSelection("right"));
            Item(menu, Path("ctx.align", "ctx.align.top"), hasSelection, () => context.AlignSelection("top"));
            Item(menu, Path("ctx.align", "ctx.align.centerY"), hasSelection, () => context.AlignSelection("centerY"));
            Item(menu, Path("ctx.align", "ctx.align.bottom"), hasSelection, () => context.AlignSelection("bottom"));

            // Distributing needs at least three elements for the spacing to mean anything.
            var canDistribute = context.SelectedElements.Count >= 3;
            Item(menu, Path("ctx.distribute", "ctx.distribute.horizontal"), canDistribute, context.DistributeSelectionHorizontal);
            Item(menu, Path("ctx.distribute", "ctx.distribute.vertical"), canDistribute, context.DistributeSelectionVertical);

            foreach (var (preset, labelKey) in AnchorPresets)
            {
                var captured = preset;
                Item(menu, Path("ctx.anchor", labelKey), hasSelection, () => context.SetSelectedAnchor(captured));
            }
        }

        private static void AddVisibilitySubmenu(GenericMenu menu, NexUIDesignerContext context, DesignerElementMetadata primary)
        {
            Toggle(menu, Path("ctx.visibility", "ctx.visibility.showInCanvas"), !primary.hiddenInDesigner, true,
                () => context.UpdateElement(primary, e => e.hiddenInDesigner = !e.hiddenInDesigner, "Toggle NexUI Element Hidden"));
            Toggle(menu, Path("ctx.visibility", "ctx.visibility.lock"), primary.locked, true,
                () => context.UpdateElement(primary, e => e.locked = !e.locked, "Toggle NexUI Element Lock"));
            Item(menu, Path("ctx.visibility", "ctx.visibility.showAll"), AnyHidden(context), () => ShowAll(context));
            Item(menu, Path("ctx.visibility", "ctx.visibility.unlockAll"), AnyLocked(context), () => UnlockAll(context));
        }

        private static void AddMotionSubmenu(GenericMenu menu, NexUIDesignerContext context, DesignerElementMetadata primary)
        {
            Item(menu, Path("ctx.motion", "ctx.motion.clipEditor"), true,
                () => MotionClipEditorWindow.Open(context.PreviewSurface, primary.elementId));
            Item(menu, Path("ctx.motion", "ctx.motion.presets"), true,
                () => DesignerTransitionPresetWindow.Open(context));

            var entry = context.Metadata?.screenMotion?.entryClip;
            var exit = context.Metadata?.screenMotion?.exitClip;
            Item(menu, Path("ctx.motion", "ctx.motion.previewOpen"), entry != null,
                () => DesignerTransitionPresetService.Preview(context, entry));
            Item(menu, Path("ctx.motion", "ctx.motion.previewClose"), exit != null,
                () => DesignerTransitionPresetService.Preview(context, exit));
            Item(menu, Path("ctx.motion", "ctx.motion.regenerateClose"), entry != null,
                () => DesignerTransitionPresetService.RegenerateClose(context));
            Item(menu, Path("ctx.motion", "ctx.motion.clear"), entry != null || exit != null,
                () => context.UpdateScreenMotion(x => { x.entryClip = null; x.exitClip = null; }, "Remove Screen Transitions"));
        }

        private static void AddLayoutSubmenu(GenericMenu menu, NexUIDesignerContext context)
        {
            Item(menu, Path("ctx.layout", "ctx.layout.autoLayout"), context.SelectedElements.Count >= 2,
                () => DesignerLayoutConversionWindow.Open(context));
            Item(menu, Path("ctx.layout", "ctx.layout.recommendedAnchors"), context.SelectedElements.Count > 0,
                () => DesignerAnchorRecommendationService.Apply(context, context.Resolution));
        }

        private static void AddMockDataSubmenu(GenericMenu menu, NexUIDesignerContext context)
        {
            foreach (DesignerTextPreset preset in Enum.GetValues(typeof(DesignerTextPreset)))
            {
                var captured = preset;
                Item(menu, Path("ctx.mock", "ctx.mock.text") + "/" + preset, true,
                    () => DesignerMockDataPresetService.ApplyText(context, captured));
            }
            foreach (DesignerValuePreset preset in Enum.GetValues(typeof(DesignerValuePreset)))
            {
                var captured = preset;
                Item(menu, Path("ctx.mock", "ctx.mock.value") + "/" + preset, true,
                    () => DesignerMockDataPresetService.ApplyValue(context, captured));
            }
            foreach (var count in new[] { 0, 1, 5, 20, 100 })
            {
                var captured = count;
                Item(menu, Path("ctx.mock", "ctx.mock.collection") + "/" + T("ctx.mock.collectionItems", count), true,
                    () => DesignerMockDataPresetService.ApplyCollection(context, captured));
            }
            Item(menu, Path("ctx.mock", "ctx.mock.captureScenario"), context.Metadata != null, () =>
            {
                var scenario = ScenarioService.CreateAsset();
                ScenarioService.Capture(scenario, context);
                AssetDatabase.SaveAssetIfDirty(scenario);
            });
        }

        /// <summary>
        /// The Create submenu, grouped exactly like the Library panel. A null
        /// <paramref name="canvasPoint"/> means "wherever the element normally lands" (used from
        /// the Hierarchy, where there is no click position on the canvas).
        /// </summary>
        private static void AddCreateSubmenu(GenericMenu menu, NexUIDesignerContext context, Vector2? canvasPoint)
        {
            var enabled = context.Metadata != null;
            foreach (var (categoryKey, items) in CreateCategories)
            {
                foreach (var (type, labelKey) in items)
                {
                    var capturedType = type;
                    Item(menu, Path("ctx.create", categoryKey, labelKey), enabled,
                        () => CreateAt(context, capturedType, canvasPoint));
                }
            }
        }

        private static void AddVisibilityResetItems(GenericMenu menu, NexUIDesignerContext context)
        {
            Item(menu, T("ctx.visibility.showAll"), AnyHidden(context), () => ShowAll(context));
            Item(menu, T("ctx.visibility.unlockAll"), AnyLocked(context), () => UnlockAll(context));
        }

        // ---- actions --------------------------------------------------------------------------

        private static void CreateAt(NexUIDesignerContext context, DesignerElementType type, Vector2? canvasPoint)
        {
            var element = context.CreateMetadataElement(type);
            if (element == null || !canvasPoint.HasValue) return;
            var rect = element.rect;
            rect.position = canvasPoint.Value;
            context.UpdateSelectedRect(rect);
        }

        private static void PasteAsChild(NexUIDesignerContext context, DesignerElementMetadata parent)
        {
            NexUIDesignerUndo.Group("Paste NexUI Elements As Child", () =>
            {
                var pasted = context.PasteSelection();
                if (pasted != null && pasted.Count > 0)
                    context.ReparentElements(pasted, parent);
            });
        }

        private static void ShowAll(NexUIDesignerContext context)
        {
            NexUIDesignerUndo.Group("Show All NexUI Elements", () =>
            {
                foreach (var element in Elements(context).Where(e => e.hiddenInDesigner).ToList())
                    context.UpdateElement(element, e => e.hiddenInDesigner = false, "Show NexUI Element");
            });
        }

        private static void UnlockAll(NexUIDesignerContext context)
        {
            NexUIDesignerUndo.Group("Unlock All NexUI Elements", () =>
            {
                foreach (var element in Elements(context).Where(e => e.locked).ToList())
                    context.UpdateElement(element, e => e.locked = false, "Unlock NexUI Element");
            });
        }

        /// <summary>Root-to-leaf display path, matching what "Copy Path" gives you in Unity.</summary>
        private static string HierarchyPath(NexUIDesignerContext context, DesignerElementMetadata element)
        {
            var parts = new List<string>();
            var current = element;
            // The guard stops a corrupt parentId cycle from hanging the editor.
            for (var guard = 0; current != null && guard < 128; guard++)
            {
                parts.Add(Label(current));
                if (string.IsNullOrEmpty(current.parentId)) break;
                var parentId = current.parentId;
                current = Elements(context).FirstOrDefault(e => e.elementId == parentId);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---- helpers --------------------------------------------------------------------------

        private static List<DesignerElementMetadata> HitTest(NexUIDesignerContext context, Vector2 point)
        {
            var result = new List<DesignerElementMetadata>();
            if (context.Metadata == null) return result;
            // Later-in-list elements render on top, so walk back-to-front for a front-to-back hit order.
            for (int i = context.Metadata.elements.Count - 1; i >= 0; i--)
            {
                var element = context.Metadata.elements[i];
                if (element == null || element.hiddenInDesigner) continue;
                if (element.rect.Contains(point)) result.Add(element);
            }
            return result;
        }

        private static IEnumerable<DesignerElementMetadata> Elements(NexUIDesignerContext context)
            => context.Metadata == null ? Enumerable.Empty<DesignerElementMetadata>() : context.Metadata.elements.Where(e => e != null);

        private static bool HasElements(NexUIDesignerContext context)
            => context.Metadata != null && context.Metadata.elements.Count > 0;

        private static bool AnyHidden(NexUIDesignerContext context) => Elements(context).Any(e => e.hiddenInDesigner);
        private static bool AnyLocked(NexUIDesignerContext context) => Elements(context).Any(e => e.locked);

        internal static string Label(DesignerElementMetadata element)
            => string.IsNullOrEmpty(element.displayName) ? element.elementId : element.displayName;

        private static string T(string key) => DesignerLocalization.T(key);
        private static string T(string key, params object[] args) => DesignerLocalization.T(key, args);

        /// <summary>Translates each key and joins them into a GenericMenu submenu path.</summary>
        private static string Path(params string[] keys)
        {
            var parts = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                parts[i] = Leaf(T(keys[i]));
            return string.Join("/", parts);
        }

        /// <summary>
        /// Makes user-authored text safe as a single menu entry. GenericMenu treats '/' as a
        /// submenu separator, so an element named "HUD/Top" would otherwise silently become a
        /// nested submenu instead of one item.
        /// </summary>
        private static string Leaf(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace('/', '∕');

        private static void Item(GenericMenu menu, string path, bool enabled, Action action)
        {
            if (enabled && action != null) menu.AddItem(new GUIContent(path), false, () => action());
            else menu.AddDisabledItem(new GUIContent(path));
        }

        private static void Toggle(GenericMenu menu, string path, bool on, bool enabled, Action action)
        {
            if (enabled && action != null) menu.AddItem(new GUIContent(path), on, () => action());
            else menu.AddDisabledItem(new GUIContent(path), on);
        }
    }
}
