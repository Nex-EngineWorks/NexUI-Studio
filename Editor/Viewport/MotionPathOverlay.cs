using System.Linq;
using emiteat.NexUI.Designer.Editor;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.MotionClip;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    /// <summary>
    /// Draws the AnchoredPosition track (if any) of whichever <see cref="NexUIDesignerContext.ActiveMotionClip"/>
    /// is open in the Motion Clip Editor for the currently selected element: a connected path through
    /// every keyframe, a dot per keyframe, and a marker at the current scrub time. Read-only overlay -
    /// never mutates metadata or the clip, and draws nothing if Motion Path is off, no clip is open,
    /// nothing is selected, or the selection has no AnchoredPosition track.
    /// </summary>
    public sealed class MotionPathOverlay : VisualElement
    {
        private static readonly Color LineColor = new Color(0.26f, 0.9f, 0.76f, 0.85f);
        private static readonly Color KeyframeColor = new Color(0.26f, 0.9f, 0.76f, 0.95f);
        private static readonly Color CurrentColor = new Color(1f, 1f, 1f, 0.95f);

        private readonly NexUIDesignerContext _context;
        private readonly ContextBoundSubscriptions _subscriptions;

        public MotionPathOverlay(NexUIDesignerContext context)
        {
            _context = context;
            name = "MotionPathOverlay";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            generateVisualContent += OnGenerateVisualContent;

            _subscriptions = new ContextBoundSubscriptions(this);
            _subscriptions.Add(h => context.ActiveMotionClipChanged += h, h => context.ActiveMotionClipChanged -= h, MarkDirtyRepaint);
            _subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => MarkDirtyRepaint());
            _subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, MarkDirtyRepaint);
            _subscriptions.Add(h => context.PreviewSettingsChanged += h, h => context.PreviewSettingsChanged -= h, MarkDirtyRepaint);
        }

        private UIMotionClipTrack FindMotionTrack(
            out DesignerElementMetadata element, out UIMotionClipPropertyTrack positionTrack)
        {
            element = _context.SelectedMetadata;
            positionTrack = null;

            if (!_context.ShowMotionPath ||
                _context.ActiveMotionClip == null ||
                element == null)
            {
                return null;
            }

            var elementId = element.elementId;

            var track = _context.ActiveMotionClip.tracks?
                .FirstOrDefault(track => track.targetElementId == elementId);

            positionTrack = track?.propertyTracks?
                .FirstOrDefault(propertyTrack =>
                    propertyTrack.propertyType ==
                    UIMotionClipPropertyType.AnchoredPosition);
            return positionTrack != null ? track : null;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var track = FindMotionTrack(out var element, out var propertyTrack);
            if (propertyTrack?.keyframes == null || propertyTrack.keyframes.Length < 2) return;

            var zoom = _context.Zoom;
            var painter = ctx.painter2D;

            painter.strokeColor = LineColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            var startPose = MotionPreviewPoseUtility.Evaluate(element, track, 0f);
            painter.MoveTo(startPose.Rect.center * zoom);
            for (var i = 0; i < propertyTrack.keyframes.Length; i++)
            {
                var pose = MotionPreviewPoseUtility.Evaluate(element, track, propertyTrack.keyframes[i].time);
                painter.LineTo(pose.Rect.center * zoom);
            }
            painter.Stroke();

            for (var i = 0; i < propertyTrack.keyframes.Length; i++)
            {
                var pose = MotionPreviewPoseUtility.Evaluate(element, track, propertyTrack.keyframes[i].time);
                var point = pose.Rect.center * zoom;
                painter.BeginPath();
                painter.Arc(point, 3f, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.fillColor = KeyframeColor;
                painter.Fill();
            }

            var currentPose = MotionPreviewPoseUtility.Evaluate(element, track, _context.ActiveMotionClipTime);
            painter.BeginPath();
            painter.Arc(currentPose.Rect.center * zoom, 5f, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.fillColor = CurrentColor;
            painter.Fill();
        }
    }

    /// <summary>
    /// Evaluates the same five properties as <c>UIMotionClipPlayer</c>, but into a Designer canvas
    /// pose. This keeps the visible metadata preview synchronized with the real backend preview
    /// surface while scrubbing/playing without mutating authored metadata.
    /// </summary>
    internal readonly struct MotionPreviewPose
    {
        public readonly Rect Rect;
        public readonly Vector2 Scale;
        public readonly float Rotation;
        public readonly float Opacity;

        public MotionPreviewPose(Rect rect, Vector2 scale, float rotation, float opacity)
        {
            Rect = rect;
            Scale = scale;
            Rotation = rotation;
            Opacity = opacity;
        }
    }

    internal static class MotionPreviewPoseUtility
    {
        public static MotionPreviewPose Evaluate(DesignerElementMetadata element, UIMotionClipTrack track, float time)
        {
            var rect = element.rect;
            var layout = DesignerPropertyAdapter.Layout(element);
            var scale = layout.scale;
            var rotation = layout.rotation;
            var opacity = DesignerPropertyAdapter.Opacity(element);
            if (track?.propertyTracks == null)
                return new MotionPreviewPose(rect, scale, rotation, opacity);

            foreach (var propertyTrack in track.propertyTracks)
            {
                if (propertyTrack == null) continue;
                var value = UIMotionClipEvaluator.Evaluate(propertyTrack, time);
                if (!value.HasValue) continue;
                switch (propertyTrack.propertyType)
                {
                    case UIMotionClipPropertyType.AnchoredPosition:
                        rect.position = value.Value.vector2Value;
                        break;
                    case UIMotionClipPropertyType.LocalPosition:
                        rect.position = value.Value.valueType == UIMotionClipValueType.Vector3
                            ? new Vector2(value.Value.vector3Value.x, value.Value.vector3Value.y)
                            : value.Value.vector2Value;
                        break;
                    case UIMotionClipPropertyType.SizeDelta:
                        rect.size = value.Value.vector2Value;
                        break;
                    case UIMotionClipPropertyType.LocalScale:
                        scale = new Vector2(value.Value.vector3Value.x, value.Value.vector3Value.y);
                        break;
                    case UIMotionClipPropertyType.LocalRotationZ:
                        rotation = value.Value.floatValue;
                        break;
                    case UIMotionClipPropertyType.CanvasGroupAlpha:
                        opacity = Mathf.Clamp01(value.Value.floatValue);
                        break;
                }
            }
            return new MotionPreviewPose(rect, scale, rotation, opacity);
        }
    }
}
