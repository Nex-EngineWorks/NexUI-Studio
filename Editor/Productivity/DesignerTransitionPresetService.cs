using System;
using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEngine;

namespace emiteat.NexUI.Designer.Editor.Productivity
{
    public enum DesignerTransitionPreset { Fade, SlideLeft, SlideRight, SlideUp, SlideDown, ScalePop, Modal, Dropdown, Tooltip, Toast, StaggerList }
    public enum DesignerStaggerOrder { Hierarchy, TopToBottom, LeftToRight, Selection, Random }

    [Serializable]
    public sealed class DesignerTransitionSettings
    {
        public float Duration = .25f;
        public float Delay;
        public UIMotionEasing Easing = UIMotionEasing.EaseOutCubic;
        public float Distance = 48f;
        public float StartScale = .9f;
        public float StartAlpha;
        public float Overshoot = 1.04f;
        public bool IncludeChildren;
        public float StaggerInterval = .05f;
        public DesignerStaggerOrder Order = DesignerStaggerOrder.Hierarchy;
        public bool ReverseOrder;
        public int RandomSeed = 12345;
    }

    public sealed class DesignerTransitionPair
    {
        public UIMotionClip Open;
        public UIMotionClip Close;
        public readonly List<string> CreatedPaths = new List<string>();
    }

    /// <summary>Builds reusable Motion Clip assets from productivity presets; no parallel motion runtime is introduced.</summary>
    public static class DesignerTransitionPresetService
    {
        private static readonly UIMotionClipPlayer PreviewPlayer = new UIMotionClipPlayer();

        public static UIMotionClip Build(DesignerMetadataAsset metadata, IReadOnlyList<DesignerElementMetadata> selection,
            DesignerTransitionPreset preset, DesignerTransitionSettings settings, bool close)
        {
            settings ??= new DesignerTransitionSettings();
            var clip = ScriptableObject.CreateInstance<UIMotionClip>();
            clip.clipName = preset + (close ? " Close" : " Open");
            var targets = OrderTargets(metadata, selection, settings);
            var tracks = new List<UIMotionClipTrack>();
            for (var i = 0; i < targets.Count; i++)
            {
                var delayIndex = settings.ReverseOrder ? targets.Count - i - 1 : i;
                var delay = settings.Delay + (targets.Count > 1 || preset == DesignerTransitionPreset.StaggerList ? delayIndex * settings.StaggerInterval : 0f);
                tracks.Add(BuildTrack(targets[i], preset, settings, close, delay));
            }
            clip.duration = Mathf.Max(.01f, settings.Delay + settings.Duration + Mathf.Max(0, targets.Count - 1) * settings.StaggerInterval);
            clip.tracks = tracks.ToArray();
            return clip;
        }

        public static UIMotionClip Reverse(UIMotionClip source)
        {
            if (source == null) return null;
            var result = UnityEngine.Object.Instantiate(source);
            result.name = source.name + " Close";
            result.clipName = source.clipName + " Close";
            foreach (var track in result.tracks ?? Array.Empty<UIMotionClipTrack>())
                foreach (var property in track.propertyTracks ?? Array.Empty<UIMotionClipPropertyTrack>())
                {
                    var sourceKeys = property.keyframes ?? Array.Empty<UIMotionClipKeyframe>();
                    property.keyframes = sourceKeys.Select(k => { k.time = result.duration - k.time; return k; })
                        .OrderBy(k => k.time).ToArray();
                }
            return result;
        }

        public static DesignerTransitionPair CreateAssetPair(DesignerMetadataAsset metadata, string targetId,
            DesignerTransitionPreset preset, string pathWithoutSuffix, DesignerTransitionSettings settings)
        {
            var target = metadata?.Find(targetId);
            var selection = target != null ? new[] { target } : Array.Empty<DesignerElementMetadata>();
            var pair = new DesignerTransitionPair();
            pair.Open = Build(metadata, selection, preset, settings, false);
            pair.Close = Reverse(pair.Open);
            var openPath = AssetDatabase.GenerateUniqueAssetPath(pathWithoutSuffix + ".Open.asset");
            var closePath = AssetDatabase.GenerateUniqueAssetPath(pathWithoutSuffix + ".Close.asset");
            AssetDatabase.CreateAsset(pair.Open, openPath);
            AssetDatabase.CreateAsset(pair.Close, closePath);
            Undo.RegisterCreatedObjectUndo(pair.Open, "Create Open Transition");
            Undo.RegisterCreatedObjectUndo(pair.Close, "Create Close Transition");
            pair.CreatedPaths.Add(openPath); pair.CreatedPaths.Add(closePath);
            return pair;
        }

        public static DesignerTransitionPair CreateAssetPair(DesignerMetadataAsset metadata,
            IReadOnlyList<DesignerElementMetadata> selection, DesignerTransitionPreset preset,
            string pathWithoutSuffix, DesignerTransitionSettings settings)
        {
            var pair = new DesignerTransitionPair { Open = Build(metadata, selection, preset, settings, false) };
            pair.Close = Reverse(pair.Open);
            var openPath = AssetDatabase.GenerateUniqueAssetPath(pathWithoutSuffix + ".Open.asset");
            var closePath = AssetDatabase.GenerateUniqueAssetPath(pathWithoutSuffix + ".Close.asset");
            AssetDatabase.CreateAsset(pair.Open, openPath);
            AssetDatabase.CreateAsset(pair.Close, closePath);
            Undo.RegisterCreatedObjectUndo(pair.Open, "Create Open Transition");
            Undo.RegisterCreatedObjectUndo(pair.Close, "Create Close Transition");
            pair.CreatedPaths.Add(openPath);
            pair.CreatedPaths.Add(closePath);
            return pair;
        }

        public static void Preview(NexUIDesignerContext context, UIMotionClip clip)
        {
            if (context?.PreviewSurface == null || clip == null) return;
            context.SetActiveMotionClip(clip, 0f);
            PreviewPlayer.PlayAsync(context.PreviewSurface, clip);
        }

        public static UIMotionClip RegenerateClose(NexUIDesignerContext context)
        {
            var open = context?.Metadata?.screenMotion?.entryClip;
            if (open == null) return null;
            var close = Reverse(open);
            var sourcePath = AssetDatabase.GetAssetPath(open);
            var folder = string.IsNullOrEmpty(sourcePath) ? "Assets" : System.IO.Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{open.name}.Close.asset");
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Regenerate Close Transition");
            AssetDatabase.CreateAsset(close, path);
            Undo.RegisterCreatedObjectUndo(close, "Create Close Transition");
            context.UpdateScreenMotion(x => x.exitClip = close, "Assign Regenerated Close Transition");
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssetIfDirty(close);
            return close;
        }

        private static UIMotionClipTrack BuildTrack(DesignerElementMetadata e, DesignerTransitionPreset preset, DesignerTransitionSettings s, bool close, float delay)
        {
            var properties = new List<UIMotionClipPropertyTrack>();
            var fromAlpha = close ? 1f : s.StartAlpha; var toAlpha = close ? s.StartAlpha : 1f;
            properties.Add(Property(UIMotionClipPropertyType.CanvasGroupAlpha, delay, s.Duration, UIMotionClipValue.Float(fromAlpha), UIMotionClipValue.Float(toAlpha), s.Easing));
            var offset = Offset(preset, s.Distance);
            if (offset != Vector2.zero)
            {
                var at = e?.rect.position ?? Vector2.zero;
                properties.Add(Property(UIMotionClipPropertyType.AnchoredPosition, delay, s.Duration,
                    UIMotionClipValue.FromVector2(close ? at : at + offset), UIMotionClipValue.FromVector2(close ? at + offset : at), s.Easing));
            }
            if (preset == DesignerTransitionPreset.ScalePop || preset == DesignerTransitionPreset.Modal || preset == DesignerTransitionPreset.Tooltip)
            {
                var start = Vector3.one * (close ? 1f : s.StartScale);
                var end = Vector3.one * (close ? s.StartScale : 1f);
                var scale = Property(UIMotionClipPropertyType.LocalScale, delay, s.Duration,
                    UIMotionClipValue.FromVector3(start), UIMotionClipValue.FromVector3(end), s.Easing);
                if (!close && s.Overshoot > 1f)
                    scale.keyframes = new[]
                    {
                        new UIMotionClipKeyframe(delay, UIMotionClipValue.FromVector3(start), s.Easing),
                        new UIMotionClipKeyframe(delay + s.Duration * .75f, UIMotionClipValue.FromVector3(Vector3.one * s.Overshoot), s.Easing),
                        new UIMotionClipKeyframe(delay + s.Duration, UIMotionClipValue.FromVector3(end), s.Easing)
                    };
                properties.Add(scale);
            }
            return new UIMotionClipTrack { targetElementId = e?.elementId ?? string.Empty, propertyTracks = properties.ToArray() };
        }

        private static UIMotionClipPropertyTrack Property(UIMotionClipPropertyType type, float delay, float duration, UIMotionClipValue a, UIMotionClipValue b, UIMotionEasing easing)
            => new UIMotionClipPropertyTrack { propertyType = type, keyframes = new[] { new UIMotionClipKeyframe(delay, a, easing), new UIMotionClipKeyframe(delay + duration, b, easing) } };

        private static Vector2 Offset(DesignerTransitionPreset p, float d)
        {
            switch (p) { case DesignerTransitionPreset.SlideLeft: return Vector2.right * d; case DesignerTransitionPreset.SlideRight: return Vector2.left * d; case DesignerTransitionPreset.SlideUp: case DesignerTransitionPreset.Dropdown: case DesignerTransitionPreset.Toast: return Vector2.down * d; case DesignerTransitionPreset.SlideDown: return Vector2.up * d; default: return Vector2.zero; }
        }

        private static List<DesignerElementMetadata> OrderTargets(DesignerMetadataAsset metadata, IReadOnlyList<DesignerElementMetadata> source, DesignerTransitionSettings s)
        {
            var list = source?.Where(x => x != null).Distinct().ToList() ?? new List<DesignerElementMetadata>();
            if (s.IncludeChildren && metadata != null) foreach (var item in list.ToArray()) list.AddRange(DesignerHierarchyUtility.GetDescendants(metadata, item));
            switch (s.Order) { case DesignerStaggerOrder.TopToBottom: list = list.OrderBy(x => x.rect.y).ThenBy(x => x.rect.x).ToList(); break; case DesignerStaggerOrder.LeftToRight: list = list.OrderBy(x => x.rect.x).ThenBy(x => x.rect.y).ToList(); break; case DesignerStaggerOrder.Random: var random = new System.Random(s.RandomSeed); list = list.OrderBy(_ => random.Next()).ToList(); break; case DesignerStaggerOrder.Hierarchy: if (metadata != null) list = list.OrderBy(x => metadata.elements.IndexOf(x)).ToList(); break; }
            return list;
        }
    }
}
