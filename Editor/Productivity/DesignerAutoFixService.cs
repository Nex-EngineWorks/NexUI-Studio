using System;
using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    public sealed class DesignerIssueFix
    {
        public string Label;
        public bool IsSafe;
        public Action Apply;
    }

    /// <summary>Maps stable validation codes to explicit, Undo-aware repairs.</summary>
    public static class DesignerAutoFixService
    {
        public static DesignerIssueFix GetFix(NexUIDesignerContext context, DesignerValidationIssue issue)
        {
            if (context?.Metadata == null || issue == null) return null;
            switch (issue.Code)
            {
                case "empty-element-id": return Fix("Element ID 생성", true, () => Rename(context, FindEmpty(context), "element"));
                case "duplicate-element-id": return Fix("중복 ID 이름 변경", true, () => Rename(context, FindDuplicate(context, issue.ElementId), issue.ElementId));
                case "missing-parent":
                case "self-parent":
                case "circular-parent": return Fix("잘못된 부모 연결 제거", true, () => Update(context, issue.ElementId, e => e.parentId = string.Empty, "Fix Invalid Parent"));
                case "zero-size-element": return Fix("기본 크기로 복원", true, () => Update(context, issue.ElementId, e =>
                    e.rect = new Rect(e.rect.position, new Vector2(Mathf.Max(100, e.rect.width), Mathf.Max(40, e.rect.height))), "Fix Element Size"));
                case "small-touch-target": return Fix("터치 영역을 32×32 이상으로 확대", true, () => Update(context, issue.ElementId, e =>
                    e.rect = new Rect(e.rect.position, new Vector2(Mathf.Max(32, e.rect.width), Mathf.Max(32, e.rect.height))), "Fix Touch Target"));
                case "outside-canvas": return Fix("Canvas 안으로 이동", true, () => Update(context, issue.ElementId, e =>
                {
                    var r = e.rect;
                    r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, context.Resolution.x - r.width));
                    r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, context.Resolution.y - r.height));
                    e.rect = r;
                }, "Move Element Inside Canvas"));
                case "ugui-decorative-raycast": return Fix("Raycast Target 끄기", true,
                    () => FixPrefabComponent<Graphic>(context, issue.ElementId, x => x.raycastTarget = false, "Disable Decorative Raycast"));
                case "ugui-invisible-canvasgroup-blocks-input": return Fix("CanvasGroup 입력 차단 해제", true,
                    () => FixPrefabComponent<CanvasGroup>(context, issue.ElementId, x => { x.interactable = false; x.blocksRaycasts = false; }, "Fix Invisible CanvasGroup"));
                case "ugui-button-target-graphic-missing": return Fix("Button Target Graphic 연결", false,
                    () => FixPrefabComponent<Button>(context, issue.ElementId, x => x.targetGraphic = x.GetComponent<Graphic>(), "Assign Button Target Graphic"));
                case "motion-close-missing": return Fix("Open을 뒤집어 Close 생성", false, () => GenerateClose(context));
                case "motion-target-missing": return Fix("끊어진 Motion 바인딩 제거", false,
                    () => RemoveDanglingMotionBindings(context));
                default: return null;
            }
        }

        public static int FixAllSafe(NexUIDesignerContext context, IEnumerable<DesignerValidationIssue> issues)
        {
            var fixes = issues?.Select(x => GetFix(context, x)).Where(x => x != null && x.IsSafe).ToList()
                ?? new List<DesignerIssueFix>();
            if (fixes.Count == 0) return 0;
            NexUIDesignerUndo.Group("Fix All Safe Validation Issues", () => fixes.ForEach(x => x.Apply()));
            context.Validate();
            return fixes.Count;
        }

        private static DesignerIssueFix Fix(string label, bool safe, Action apply)
            => new DesignerIssueFix { Label = label, IsSafe = safe, Apply = apply };

        private static DesignerElementMetadata FindEmpty(NexUIDesignerContext c)
            => c.Metadata.elements.FirstOrDefault(x => x != null && string.IsNullOrEmpty(x.elementId));

        private static DesignerElementMetadata FindDuplicate(NexUIDesignerContext c, string id)
            => c.Metadata.elements.Where(x => x != null && x.elementId == id).Skip(1).FirstOrDefault();

        private static void Rename(NexUIDesignerContext context, DesignerElementMetadata element, string baseId)
        {
            if (element == null) return;
            var stem = string.IsNullOrEmpty(baseId) ? "element" : baseId;
            var candidate = stem;
            var i = 1;
            while (context.Metadata.elements.Any(x => x != element && x != null && x.elementId == candidate)) candidate = stem + i++;
            context.RenameElementId(element, candidate);
        }

        /// <summary>
        /// Removes motion bindings that point at an element the screen no longer has.
        /// </summary>
        /// <remarks>
        /// Marked unsafe, so it never runs as part of Fix All Safe - deleting authored motion is
        /// not something to do behind the user's back, even when the binding is provably dead. It
        /// goes through <c>RemoveMotionBinding</c>, so the whole removal is one undo step.
        ///
        /// Only the dangling-target case is offered. A binding with a real target but no clip is
        /// an unfinished edit, not a broken one, and removing it would throw away work in progress.
        /// Screen-level triggers are skipped because they are not supposed to have a target at all.
        /// </remarks>
        private static void RemoveDanglingMotionBindings(NexUIDesignerContext context)
        {
            var metadata = context?.Metadata;
            var motion = metadata?.screenMotion;
            if (motion?.bindings == null) return;

            var ids = new HashSet<string>(metadata.elements
                .Where(x => x != null && !string.IsNullOrEmpty(x.elementId))
                .Select(x => x.elementId));

            var dangling = motion.bindings
                .Where(b => b != null
                            && b.trigger != DesignerMotionTrigger.ScreenEnter
                            && b.trigger != DesignerMotionTrigger.ScreenExit
                            && (string.IsNullOrEmpty(b.targetElementId) || !ids.Contains(b.targetElementId)))
                .ToList();

            foreach (var binding in dangling)
                context.RemoveMotionBinding(binding);
        }

        private static void Update(NexUIDesignerContext context, string id, Action<DesignerElementMetadata> action, string undo)
        {
            var element = context.Metadata.Find(id);
            if (element != null) context.UpdateElement(element, action, undo);
        }

        private static void FixPrefabComponent<T>(NexUIDesignerContext context, string id, Action<T> action, string undo) where T : Component
        {
            if (context.CurrentScreen == null || context.CurrentScreen.backendAsset.backend != UIRenderBackend.UGUI) return;
            if (!(context.CurrentScreen.backendAsset.asset is GameObject prefab)) return;
            var target = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == id);
            var component = target != null ? target.GetComponent<T>() : null;
            if (component == null) return;
            Undo.RecordObject(component, undo);
            action(component);
            EditorUtility.SetDirty(component);
            PrefabUtility.SavePrefabAsset(prefab);
            context.Validate();
        }

        private static void GenerateClose(NexUIDesignerContext context)
        {
            DesignerTransitionPresetService.RegenerateClose(context);
        }
    }
}
