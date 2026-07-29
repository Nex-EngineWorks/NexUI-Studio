using emiteat.NexUI.Designer.Editor.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.UI.Shell
{
    /// <summary>
    /// The always-visible X / Y / W / H strip under the canvas toolbar.
    ///
    /// Laying a screen out is mostly "nudge this, match that number", and doing it through the
    /// Inspector means a round trip across the window for every value. Keeping the four numbers that
    /// matter directly under the canvas - where the eye already is - removes that trip. Unity's
    /// FloatField supports label-drag scrubbing, so the same strip also gives coarse dragging.
    ///
    /// It edits the authored element only. When the selection is a component instance, the fields
    /// still target the instance element (the thing the user owns), never generated children.
    /// </summary>
    public sealed class NexUITransformBar : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly FloatField _x;
        private readonly FloatField _y;
        private readonly FloatField _width;
        private readonly FloatField _height;
        private readonly Label _summary;
        private bool _refreshing;

        public NexUITransformBar(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-transform-bar");

            _x = MakeField("X", "shell.transform.x", value => Apply(rect => new Rect(value, rect.y, rect.width, rect.height)));
            _y = MakeField("Y", "shell.transform.y", value => Apply(rect => new Rect(rect.x, value, rect.width, rect.height)));
            _width = MakeField("W", "shell.transform.w", value => Apply(rect => new Rect(rect.x, rect.y, Mathf.Max(1f, value), rect.height)));
            _height = MakeField("H", "shell.transform.h", value => Apply(rect => new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, value))));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            _summary = new Label();
            _summary.AddToClassList("nexui-transform-summary");
            Add(_summary);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h,
                h => context.MetadataSelectionChanged -= h, _ => Refresh());
            subscriptions.Add<System.Collections.Generic.IReadOnlyList<DesignerElementMetadata>>(
                h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => Refresh());
            subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private FloatField MakeField(string label, string tooltipKey, System.Action<float> apply)
        {
            var field = new FloatField(label) { tooltip = DesignerLocalization.T(tooltipKey) };
            field.AddToClassList("nexui-transform-field");
            field.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                apply(evt.newValue);
            });
            Add(field);
            return field;
        }

        private void Apply(System.Func<Rect, Rect> transform)
        {
            var element = _context.SelectedMetadata;
            if (element == null || element.locked) return;
            _context.UpdateElementRect(element, transform(element.rect));
        }

        private void Refresh()
        {
            _refreshing = true;

            var count = _context.SelectedElements.Count;
            var element = _context.SelectedMetadata;
            var single = count == 1 && element != null;

            SetEnabled(single && !element.locked);
            _x.style.display = _y.style.display = _width.style.display = _height.style.display =
                count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            if (single)
            {
                _x.SetValueWithoutNotify(Mathf.Round(element.rect.x));
                _y.SetValueWithoutNotify(Mathf.Round(element.rect.y));
                _width.SetValueWithoutNotify(Mathf.Round(element.rect.width));
                _height.SetValueWithoutNotify(Mathf.Round(element.rect.height));
                _summary.text = element.locked
                    ? DesignerLocalization.T("shell.transform.locked")
                    : element.elementId;
            }
            else if (count > 1)
            {
                // Multi-selection shows the union so the user can see what they grabbed, but editing
                // several rects through four fields would be ambiguous, so the fields stay disabled.
                var bounds = UIAlignmentUtility.GetBounds(_context.SelectedElements);
                _x.SetValueWithoutNotify(Mathf.Round(bounds.x));
                _y.SetValueWithoutNotify(Mathf.Round(bounds.y));
                _width.SetValueWithoutNotify(Mathf.Round(bounds.width));
                _height.SetValueWithoutNotify(Mathf.Round(bounds.height));
                _summary.text = DesignerLocalization.T("shell.transform.multi", count);
            }
            else
            {
                _summary.text = DesignerLocalization.T("shell.transform.none");
            }

            _refreshing = false;
        }
    }
}
