using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Backend;
using emiteat.NexUI.Designer.Editor.Commands;
using emiteat.NexUI.Designer.Editor.Components;
using emiteat.NexUI.Designer.Editor.Components.Definitions;
using emiteat.NexUI.Designer.Editor.Components.Preview;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Properties;
using emiteat.NexUI.Designer.Editor.MotionClipEditor;
using emiteat.NexUI.Designer.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace emiteat.NexUI.Designer.Editor.Viewport
{
    public sealed class NexUIDesignerViewport : VisualElement
    {
        private const string ScrollXPref = "NexUI.Designer.UI.CanvasScrollX";
        private const string ScrollYPref = "NexUI.Designer.UI.CanvasScrollY";
        private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("NexUI.Designer.Preview.Apply");
        private readonly NexUIDesignerContext _context;
        private readonly Label _label;
        private readonly Label _hint;
        private readonly Label _zoomBadge;
        private readonly ScrollView _previewFrame;
        private readonly VisualElement _previewCanvas;
        private readonly VisualElement _gridLayer;
        private readonly VisualElement _motionGhostLayer;
        private readonly VisualElement _elementLayer;
        private readonly VisualElement _guideLayer;
        private readonly VisualElement _selectionRectOverlay;
        private readonly VisualElement _floatingToolbar;
        private readonly PopupField<string> _statePopup;
        private readonly Button _interactiveToggle;
        private readonly Button _motionPathToggle;
        private readonly Button _onionSkinToggle;
        private readonly Button _focusNavToggle;
        private readonly MotionPathOverlay _motionPathOverlay;
        private readonly OnionSkinOverlay _onionSkinOverlay;
        private readonly FocusNavigationOverlay _focusNavOverlay;

        private static readonly List<string> PreviewStateChoices = new List<string>
        {
            "Normal", "Hover", "Pressed", "Focused", "Selected", "Disabled",
            "Loading", "Empty", "Error", "Success", "Warning", "Indeterminate"
        };
        private readonly Label _emptyState;
        private readonly Label _distanceLabel;
        private readonly Label _dropHint;
        private readonly DesignerRulerOverlay _horizontalRuler;
        private readonly DesignerRulerOverlay _verticalRuler;
        private readonly DesignerGuideLayer _guideOverlay;
        private readonly List<DesignerGuide> _userGuides;

        // Canvas navigation: space-drag and middle-drag pan without leaving the current tool, the way
        // every layout tool works. Selecting the Hand tool still pans, this just removes the round trip.
        private bool _spaceHeld;
        private bool _panning;
        private Vector2 _panPointerStart;
        private Vector2 _panScrollStart;

        // Drag-to-reparent: the container the current element drag would drop into.
        private DesignerElementMetadata _dropTarget;
        private bool _dropTargetResolved;
        private readonly Dictionary<DesignerElementMetadata, VisualElement> _views = new Dictionary<DesignerElementMetadata, VisualElement>();
        private readonly Dictionary<DesignerElementMetadata, List<VisualElement>> _ownedPreviewViews =
            new Dictionary<DesignerElementMetadata, List<VisualElement>>();
        private readonly Dictionary<VisualElement, Rect> _previewViewRects = new Dictionary<VisualElement, Rect>();
        private readonly Dictionary<string, VisualElement> _motionGhostViews = new Dictionary<string, VisualElement>();
        private emiteat.NexUI.MotionClip.UIMotionClip _motionGhostClip;
        private int _motionGhostTrackSignature;

        // Single element drag/resize state (also used as the "grabbed" element during a group move).
        private DesignerElementMetadata _dragElement;
        private Vector2 _dragStart;
        private Rect _dragStartRect;
        private Rect _pendingDragRect;
        private bool _resizing;
        private Vector2 _lastDragDelta;
        private Dictionary<DesignerElementMetadata, Rect> _groupDragStartRects;
        private Dictionary<VisualElement, Rect> _dragPreviewStartRects;

        // Drag-box (rectangle) selection state, in unscaled canvas coordinates.
        private Vector2? _boxSelectStart;
        private bool _boxSelectShift;
        private bool _boxSelectCtrl;

        private TextField _renameField;

        public NexUIDesignerViewport(NexUIDesignerContext context)
        {
            _context = context;
            var subscriptions = new ContextBoundSubscriptions(this);
            name = "NexUIDesignerViewport";
            focusable = true;
            AddToClassList("nexui-viewport");
            style.flexGrow = 1;

            var header = new VisualElement();
            header.AddToClassList("nexui-viewport-header");
            Add(header);

            var titleBlock = new VisualElement();
            titleBlock.style.flexGrow = 1;
            header.Add(titleBlock);

            _label = new Label();
            _label.AddToClassList("nexui-viewport-title");
            titleBlock.Add(_label);

            _hint = new Label();
            _hint.AddToClassList("nexui-viewport-hint");
            titleBlock.Add(_hint);

            _zoomBadge = new Label();
            _zoomBadge.AddToClassList("nexui-zoom-badge");
            header.Add(_zoomBadge);

            // Preview-state dropdown: forces a state on the canvas (preview-only).
            _statePopup = new PopupField<string>(PreviewStateChoices, 0)
            {
                tooltip = "Force a preview state on the canvas. Preview-only; never saved on elements."
            };
            _statePopup.AddToClassList("nexui-toolbar-button");
            _statePopup.RegisterValueChangedCallback(evt => _context.SetForcedPreviewState(ParseState(evt.newValue)));
            header.Add(_statePopup);

            // Design ⇄ Interactive mode toggle.
            _interactiveToggle = new Button(() => _context.ToggleInteractionMode()) { tooltip = "Toggle Interactive Preview: hover/press/activate simulates commands (logged, never runs real game logic)." };
            _interactiveToggle.AddToClassList("nexui-toolbar-button");
            header.Add(_interactiveToggle);

            // Motion Clip Editor helpers: only meaningful while a clip is open there for the selected element.
            _motionPathToggle = new Button(() => _context.SetShowMotionPath(!_context.ShowMotionPath))
            { text = DesignerLocalization.T("viewport.motionPath"), tooltip = DesignerLocalization.T("tooltip.viewport.motionPath") };
            _motionPathToggle.AddToClassList("nexui-toolbar-button");
            header.Add(_motionPathToggle);

            _onionSkinToggle = new Button(() => _context.SetShowOnionSkin(!_context.ShowOnionSkin))
            { text = DesignerLocalization.T("viewport.onionSkin"), tooltip = DesignerLocalization.T("tooltip.viewport.onionSkin") };
            _onionSkinToggle.AddToClassList("nexui-toolbar-button");
            header.Add(_onionSkinToggle);

            _focusNavToggle = new Button(() => _context.SetShowFocusNav(!_context.ShowFocusNav))
            { text = DesignerLocalization.T("viewport.focusNav"), tooltip = DesignerLocalization.T("tooltip.viewport.focusNav") };
            _focusNavToggle.AddToClassList("nexui-toolbar-button");
            header.Add(_focusNavToggle);

            subscriptions.Add(h => _context.PreviewSettingsChanged += h, h => _context.PreviewSettingsChanged -= h, RefreshPreviewControls);

            _previewFrame = new ScrollView();
            _previewFrame.schedule.Execute(() => _previewFrame.scrollOffset = new Vector2(
                UnityEditor.EditorPrefs.GetFloat(ScrollXPref, 0f), UnityEditor.EditorPrefs.GetFloat(ScrollYPref, 0f)));
            _previewFrame.horizontalScroller.valueChanged += value => UnityEditor.EditorPrefs.SetFloat(ScrollXPref, value);
            _previewFrame.verticalScroller.valueChanged += value => UnityEditor.EditorPrefs.SetFloat(ScrollYPref, value);
            _previewFrame.AddToClassList("nexui-preview-frame");
            _previewCanvas = new VisualElement();
            _previewCanvas.AddToClassList("nexui-preview-canvas");
            _previewCanvas.focusable = true;

            _gridLayer = new VisualElement();
            _gridLayer.AddToClassList("nexui-grid-layer");
            _motionGhostLayer = new VisualElement();
            _motionGhostLayer.AddToClassList("nexui-motion-ghost-layer");
            _motionGhostLayer.pickingMode = PickingMode.Ignore;
            _elementLayer = new VisualElement();
            _elementLayer.AddToClassList("nexui-element-layer");
            _guideLayer = new VisualElement();
            _guideLayer.AddToClassList("nexui-guide-layer");
            _guideLayer.pickingMode = PickingMode.Ignore;
            _motionPathOverlay = new MotionPathOverlay(_context);
            _onionSkinOverlay = new OnionSkinOverlay(_context);
            _focusNavOverlay = new FocusNavigationOverlay(_context);
            _selectionRectOverlay = new VisualElement();
            _selectionRectOverlay.AddToClassList("nexui-selection-rect");
            _selectionRectOverlay.style.position = Position.Absolute;
            _selectionRectOverlay.style.display = DisplayStyle.None;
            _selectionRectOverlay.pickingMode = PickingMode.Ignore;
            _floatingToolbar = new VisualElement();
            _floatingToolbar.AddToClassList("nexui-floating-toolbar");
            _floatingToolbar.style.display = DisplayStyle.None;
            _distanceLabel = new Label();
            _distanceLabel.AddToClassList("nexui-distance-label");
            _distanceLabel.style.display = DisplayStyle.None;
            _distanceLabel.pickingMode = PickingMode.Ignore;
            _emptyState = new Label();
            _emptyState.AddToClassList("nexui-canvas-empty-state");
            _emptyState.pickingMode = PickingMode.Ignore;
            _dropHint = new Label();
            _dropHint.AddToClassList("nexui-drop-hint");
            _dropHint.style.display = DisplayStyle.None;
            _dropHint.pickingMode = PickingMode.Ignore;

            _previewCanvas.Add(_gridLayer);
            _previewCanvas.Add(_motionGhostLayer);
            _previewCanvas.Add(_elementLayer);
            _previewCanvas.Add(_onionSkinOverlay);
            _previewCanvas.Add(_motionPathOverlay);
            _previewCanvas.Add(_focusNavOverlay);
            _previewCanvas.Add(_guideLayer);
            _previewCanvas.Add(_selectionRectOverlay);
            _previewCanvas.Add(_floatingToolbar);
            _previewCanvas.Add(_distanceLabel);
            _previewCanvas.Add(_emptyState);
            _previewCanvas.Add(_dropHint);

            // Rulers frame the canvas the way every layout tool does: a corner box, a horizontal strip
            // above and a vertical strip beside the scrolling area. They read zoom/scroll live rather
            // than caching, so they stay correct without extra bookkeeping.
            _userGuides = LoadGuides();
            _guideOverlay = new DesignerGuideLayer(_userGuides, () => _context.Zoom)
            {
                // Dragging a guide honours the same grid snap as dragging an element, so a guide can
                // be parked exactly on the grid rather than one pixel off it.
                SnapPosition = position => _context.SnapEnabled
                    ? Mathf.Round(position / Mathf.Max(1f, _context.GridSize)) * Mathf.Max(1f, _context.GridSize)
                    : position
            };
            _guideOverlay.Changed += SaveGuides;
            _previewCanvas.Add(_guideOverlay);
            _previewFrame.Add(_previewCanvas);

            _horizontalRuler = new DesignerRulerOverlay(DesignerGuideAxis.Vertical,
                () => _context.Zoom, () => _previewFrame.scrollOffset.x);
            _verticalRuler = new DesignerRulerOverlay(DesignerGuideAxis.Horizontal,
                () => _context.Zoom, () => _previewFrame.scrollOffset.y);
            _horizontalRuler.GuideCreated += AddGuide;
            _verticalRuler.GuideCreated += AddGuide;
            _horizontalRuler.GuidePreview += SetGuidePreview;
            _verticalRuler.GuidePreview += SetGuidePreview;

            var rulerCorner = new VisualElement();
            rulerCorner.AddToClassList("nexui-ruler-corner");
            rulerCorner.tooltip = "Clear all guides.";
            rulerCorner.RegisterCallback<PointerDownEvent>(_ => ClearGuides());

            var topRow = new VisualElement();
            topRow.AddToClassList("nexui-ruler-row");
            topRow.Add(rulerCorner);
            topRow.Add(_horizontalRuler);

            var canvasRow = new VisualElement();
            canvasRow.AddToClassList("nexui-canvas-row");
            canvasRow.Add(_verticalRuler);
            canvasRow.Add(_previewFrame);

            var canvasArea = new VisualElement();
            canvasArea.AddToClassList("nexui-canvas-area");
            canvasArea.Add(topRow);
            canvasArea.Add(canvasRow);
            Add(canvasArea);

            _previewFrame.RegisterCallback<WheelEvent>(OnWheel);
            _previewCanvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            _previewCanvas.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
            // PointerUpEvent does not bubble from child elements on every supported UI Toolkit
            // version. Capture on the canvas so box-select cleanup still runs and, critically,
            // right-clicking an already-selected element can reach the context-menu path.
            _previewCanvas.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp, TrickleDown.TrickleDown);
            _previewCanvas.RegisterCallback<ContextClickEvent>(OnCanvasContextClick, TrickleDown.TrickleDown);
            _previewCanvas.RegisterCallback<DragUpdatedEvent>(OnCanvasDragUpdated);
            _previewCanvas.RegisterCallback<DragPerformEvent>(OnCanvasDragPerform);
            _previewCanvas.RegisterCallback<DragLeaveEvent>(_ => HideDropHint());
            _previewCanvas.RegisterCallback<DragExitedEvent>(_ => HideDropHint());
            _previewCanvas.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                _horizontalRuler?.SetCursor(null);
                _verticalRuler?.SetCursor(null);
            });
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
            // Losing focus mid-pan would otherwise leave space "stuck down".
            RegisterCallback<BlurEvent>(_ => SetSpaceHeld(false));

            subscriptions.Add(h => context.PreviewRebuilt += h, h => context.PreviewRebuilt -= h, RefreshAll);
            subscriptions.Add<DesignerMetadataAsset>(h => context.MetadataChanged += h, h => context.MetadataChanged -= h, _ => RefreshAll());
            subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => RefreshSelection());
            subscriptions.Add<System.Collections.Generic.IReadOnlyList<DesignerElementMetadata>>(h => context.MultiSelectionChanged += h, h => context.MultiSelectionChanged -= h, _ => RefreshSelection());
            subscriptions.Add(h => context.ActiveMotionClipChanged += h, h => context.ActiveMotionClipChanged -= h, RefreshMotionPreview);
            subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, RefreshAll);
            subscriptions.Add<DesignerElementMetadata>(h => context.ElementChanged += h, h => context.ElementChanged -= h, FlashElement);
            RefreshAll();
            RefreshPreviewControls();
        }

        public NexUIDesignerContext Context => _context;

        public void FitToView()
        {
            if (_previewFrame.resolvedStyle.width <= 1f || _previewFrame.resolvedStyle.height <= 1f) return;
            var x = (_previewFrame.resolvedStyle.width - 48f) / Mathf.Max(1f, _context.Resolution.x);
            var y = (_previewFrame.resolvedStyle.height - 48f) / Mathf.Max(1f, _context.Resolution.y);
            _context.SetZoom(Mathf.Clamp(Mathf.Min(x, y), 0.15f, 2f));
        }

        /// <summary>
        /// C1: briefly highlights the element's viewport view so a property/style/theme change
        /// is visibly confirmed instead of relying on the user to notice a value changed in an
        /// inspector field. <see cref="MarkMetadataDirty"/> already rebuilt <see cref="_views"/>
        /// synchronously before this fires (via CanvasChanged), so the lookup below always sees
        /// the current view.
        /// </summary>
        private void FlashElement(DesignerElementMetadata element)
        {
            if (element == null || !_views.TryGetValue(element, out var view) || view == null) return;
            view.AddToClassList("nexui-element-flash");
            view.schedule.Execute(() => view.RemoveFromClassList("nexui-element-flash")).ExecuteLater(300);
        }

        private void RefreshAll()
        {
            using var markerScope = RebuildMarker.Auto();
            RefreshHeaderAndCanvas();
            RebuildElements();
            RefreshSelection();
            RefreshRulers();
        }

        private void RefreshHeaderAndCanvas()
        {
            _label.text = _context.CurrentScreen == null
                ? DesignerLocalization.T("message.noScreenSelected")
                : _context.CurrentScreen.ScreenId;
            _hint.text = BuildHint();
            _zoomBadge.text = Mathf.RoundToInt(_context.Zoom * 100f) + "%";

            var width = Mathf.Max(320, _context.Resolution.x * _context.Zoom);
            var height = Mathf.Max(220, _context.Resolution.y * _context.Zoom);
            _previewCanvas.style.width = width;
            _previewCanvas.style.height = height;
            _gridLayer.style.opacity = _context.SnapEnabled ? 1f : 0.25f;
            BuildGrid(width, height);
            HideSmartGuides();
        }

        private void RebuildElements()
        {
            _elementLayer.Clear();
            _views.Clear();
            _ownedPreviewViews.Clear();
            _previewViewRects.Clear();
            ClearMotionGhosts();

            if (_context.Metadata == null || _context.Metadata.elements.Count == 0)
            {
                _emptyState.text = _context.Metadata == null
                    ? "Select a Metadata asset to edit design elements."
                    : "Add elements from the component palette.";
                _emptyState.style.display = DisplayStyle.Flex;
                return;
            }

            _emptyState.style.display = DisplayStyle.None;

            // Draw the *expanded* tree so component instances show their real content, but key the
            // view map by the *authored* element so selection, drag and the Inspector keep operating
            // on data the user actually owns. Generated children have no authored counterpart and are
            // therefore drawn but never selected (HitTest* only walks Metadata.elements).
            foreach (var element in _context.PreviewElements)
            {
                if (element == null || element.hiddenInDesigner) continue;
                var authored = _context.ResolveAuthoredElement(element);
                var owner = authored ?? _context.ResolveAuthoredOwner(element);
                var view = CreateElementView(element, authored);
                if (authored != null && !_views.ContainsKey(authored))
                    _views[authored] = view;
                if (owner != null)
                {
                    if (!_ownedPreviewViews.TryGetValue(owner, out var ownedViews))
                    {
                        ownedViews = new List<VisualElement>();
                        _ownedPreviewViews[owner] = ownedViews;
                    }
                    ownedViews.Add(view);
                }
                _previewViewRects[view] = element.rect;
                _elementLayer.Add(view);
            }

            RefreshMotionPreview();
        }

        private void BuildGrid(float width, float height)
        {
            _gridLayer.Clear();
            var spacing = Mathf.Max(4f, _context.GridSize * _context.Zoom);
            var verticalCount = Mathf.Min(220, Mathf.CeilToInt(width / spacing));
            var horizontalCount = Mathf.Min(160, Mathf.CeilToInt(height / spacing));

            for (int i = 0; i <= verticalCount; i++)
            {
                var line = new VisualElement();
                line.AddToClassList(i % 8 == 0 ? "nexui-grid-line-major" : "nexui-grid-line");
                line.style.left = i * spacing;
                line.style.top = 0;
                line.style.width = 1;
                line.style.height = height;
                _gridLayer.Add(line);
            }

            for (int i = 0; i <= horizontalCount; i++)
            {
                var line = new VisualElement();
                line.AddToClassList(i % 8 == 0 ? "nexui-grid-line-major" : "nexui-grid-line");
                line.style.left = 0;
                line.style.top = i * spacing;
                line.style.width = width;
                line.style.height = 1;
                _gridLayer.Add(line);
            }
        }

        private string BuildHint()
        {
            if (_context.CurrentScreen == null)
                return "Select a Screen and Metadata, then add elements from the palette.";
            return _context.Backend + " / " + _context.Resolution.x + "x" + _context.Resolution.y + " / " + _context.PreviewState + " / " + _context.InputMode;
        }

        private VisualElement CreateElementView(DesignerElementMetadata element,
            DesignerElementMetadata editableElement, bool motionGhost = false)
        {
            var view = new VisualElement();
            view.AddToClassList("nexui-design-element");
            if (motionGhost) view.AddToClassList("nexui-motion-start-ghost");
            view.AddToClassList("type-" + element.elementType.ToLowerInvariant());
            view.AddToClassList("shape-" + element.shape.ToString().ToLowerInvariant());
            var hasPreviewImage = IsImagePreviewElement(element);
            view.EnableInClassList("has-preview-image", hasPreviewImage);
            view.EnableInClassList("is-locked", element.locked);
            view.style.position = Position.Absolute;
            ApplyRect(view, element.rect);

            // Forced preview state modulates the base box (hover lighten, pressed darken, disabled
            // desaturate/dim, error/success/warning border, selected/focused accent) so the State
            // dropdown visibly changes the canvas.
            var previewState = EffectivePreviewState(element);
            var backgroundColor = DesignerPropertyAdapter.BackgroundColor(element);
            var stateTint = DesignerPreviewColors.Modulate(backgroundColor, previewState);
            if (hasPreviewImage)
            {
                var outline = ImageOutlineColor(backgroundColor);
                view.style.backgroundColor = new StyleColor(Color.clear);
                view.style.borderTopColor = new StyleColor(outline);
                view.style.borderRightColor = new StyleColor(outline);
                view.style.borderBottomColor = new StyleColor(outline);
                view.style.borderLeftColor = new StyleColor(outline);
            }
            else
            {
                view.style.backgroundColor = new StyleColor(stateTint);
                view.style.borderTopColor = new StyleColor(Lighten(stateTint, 0.18f));
                view.style.borderRightColor = new StyleColor(Lighten(stateTint, 0.18f));
                view.style.borderBottomColor = new StyleColor(Darken(stateTint, 0.18f));
                view.style.borderLeftColor = new StyleColor(Lighten(stateTint, 0.18f));
            }

            var stateBorder = DesignerPreviewColors.StateBorder(previewState);
            if (stateBorder.HasValue)
            {
                var c = new StyleColor(stateBorder.Value);
                view.style.borderTopColor = c; view.style.borderRightColor = c;
                view.style.borderBottomColor = c; view.style.borderLeftColor = c;
            }
            var stateOpacity = DesignerPreviewColors.StateOpacity(previewState);
            if (DesignerComponentPropertyAccess.Find(element, "interactable") != null &&
                !DesignerComponentPropertyAccess.GetBool(element, "interactable", true))
                stateOpacity *= 0.62f;
            view.style.opacity = stateOpacity * DesignerPropertyAdapter.Opacity(element);

            var visual = DesignerPropertyAdapter.Visual(element);
            var borderWidth = Mathf.Max(0f, visual.borderWidth);
            if (borderWidth > 0f)
            {
                view.style.borderTopWidth = borderWidth;
                view.style.borderRightWidth = borderWidth;
                view.style.borderBottomWidth = borderWidth;
                view.style.borderLeftWidth = borderWidth;
                var border = new StyleColor(visual.borderColor);
                view.style.borderTopColor = border; view.style.borderRightColor = border;
                view.style.borderBottomColor = border; view.style.borderLeftColor = border;
            }
            var radius = DesignerPropertyAdapter.CornerRadius(element);
            view.style.borderTopLeftRadius = radius;
            view.style.borderTopRightRadius = radius;
            view.style.borderBottomLeftRadius = radius;
            view.style.borderBottomRightRadius = radius;
            var layout = DesignerPropertyAdapter.Layout(element);
            view.style.scale = new Scale(layout.scale);
            view.style.rotate = new Rotate(new Angle(layout.rotation, AngleUnit.Degree));
            view.style.overflow = DesignerPropertyAdapter.Clip(element) ||
                                  DesignerComponentPropertyAccess.GetBool(element, "clipContent")
                ? Overflow.Hidden
                : Overflow.Visible;

            AddTypeSpecificPreview(view, element);

            if (!hasPreviewImage && !motionGhost)
            {
                var name = new Label(string.IsNullOrEmpty(element.displayName) ? element.elementId : element.displayName);
                name.AddToClassList("nexui-element-name");
                view.Add(name);
            }

            if (!hasPreviewImage && !string.IsNullOrEmpty(element.text))
            {
                var text = new Label(element.text);
                text.AddToClassList("nexui-element-text");
                text.style.color = new StyleColor(DesignerPropertyAdapter.TextColor(element));
                text.style.fontSize = Mathf.Max(9f, DesignerPropertyAdapter.FontSize(element)) * _context.Zoom;
                var typography = DesignerPropertyAdapter.Typography(element);
                if (typography.hasOverrides)
                {
                    text.style.unityTextAlign = (TextAnchor)typography.alignment;
                    text.style.whiteSpace = typography.wrapping ? WhiteSpace.Normal : WhiteSpace.NoWrap;
                    text.style.overflow = typography.overflow == DesignerTextOverflow.Overflow ? Overflow.Visible : Overflow.Hidden;
                    text.style.textOverflow = typography.ellipsis || typography.overflow == DesignerTextOverflow.Ellipsis
                        ? TextOverflow.Ellipsis : TextOverflow.Clip;
                    text.style.letterSpacing = typography.letterSpacing;
                    text.style.unityFontStyleAndWeight = PreviewFontStyle(typography);
                    text.style.unityTextOutlineWidth = typography.outlineWidth;
                    text.style.unityTextOutlineColor = typography.outlineColor;
                    if (typography.fontAsset is Font font) text.style.unityFont = font;
                }
                if (DesignerComponentRegistry.Get(element.elementType).GetPart("label") != null &&
                    view.Q<VisualElement>("nexui-part-label") == null)
                    DesignerPreviewPartUtility.Register(text, BuildPreviewContext(element), "label");
                view.Add(text);
            }

            if (!hasPreviewImage && !motionGhost)
            {
                var meta = new Label(element.elementId);
                meta.AddToClassList("nexui-element-meta");
                view.Add(meta);
            }

            if (motionGhost || editableElement == null)
            {
                // Expanded component children are visual detail only. If they take pointer input,
                // the user drags a throw-away expansion clone and the authored instance snaps back
                // on rebuild. Ignoring the whole subtree lets the instance root underneath receive
                // selection, move, resize and context-menu input.
                SetPickingModeRecursive(view, PickingMode.Ignore);
            }
            else
            {
                var handle = new VisualElement();
                handle.AddToClassList("nexui-resize-handle");
                view.Add(handle);

                AddSelectionHandles(view);

                view.RegisterCallback<PointerDownEvent>(evt => BeginDrag(evt, editableElement, view));
                view.RegisterCallback<PointerMoveEvent>(evt => ContinueDrag(evt, view));
                view.RegisterCallback<PointerUpEvent>(evt => EndDrag(evt, view));
                view.RegisterCallback<PointerCancelEvent>(evt => CancelDrag(evt, view));
            }
            return view;
        }

        private static void SetPickingModeRecursive(VisualElement view, PickingMode mode)
        {
            if (view == null) return;
            view.pickingMode = mode;
            for (var i = 0; i < view.childCount; i++)
                SetPickingModeRecursive(view[i], mode);
        }

        /// <summary>
        /// Mirrors the Motion Clip Editor's current time onto the visible Designer elements. The
        /// backend preview surface is still evaluated by UIMotionClipPlayer; this second, read-only
        /// application is what makes the element the user is actually looking at travel along the
        /// path. A persistent low-opacity copy stays at time zero as the start pose.
        /// </summary>
        private void RefreshMotionPreview()
        {
            // Always restore the authored base first so changing/closing clips cannot leave a stale
            // rotation, scale, alpha or position on the metadata canvas.
            foreach (var pair in _views)
            {
                var element = pair.Key;
                if (element == null || pair.Value == null) continue;
                var layout = DesignerPropertyAdapter.Layout(element);
                ApplyMotionPose(pair.Value, new MotionPreviewPose(element.rect, layout.scale, layout.rotation,
                    DesignerPropertyAdapter.Opacity(element)), ghost: false);
            }

            var clip = _context.ActiveMotionClip;
            if (clip?.tracks == null)
            {
                ClearMotionGhosts();
                return;
            }

            var signature = MotionTrackSignature(clip);
            if (_motionGhostClip != clip || _motionGhostTrackSignature != signature)
            {
                ClearMotionGhosts();
                _motionGhostClip = clip;
                _motionGhostTrackSignature = signature;
            }

            foreach (var track in clip.tracks)
            {
                if (track == null || string.IsNullOrEmpty(track.targetElementId)) continue;
                var element = _context.Metadata?.Find(track.targetElementId);
                if (element == null || !_views.TryGetValue(element, out var view) || view == null) continue;

                var currentPose = MotionPreviewPoseUtility.Evaluate(element, track, _context.ActiveMotionClipTime);
                ApplyMotionPose(view, currentPose, ghost: false);

                if (!_motionGhostViews.TryGetValue(track.targetElementId, out var ghost) || ghost == null)
                {
                    ghost = CreateElementView(element, null, motionGhost: true);
                    _motionGhostViews[track.targetElementId] = ghost;
                    _motionGhostLayer.Add(ghost);
                }
                var startPose = MotionPreviewPoseUtility.Evaluate(element, track, 0f);
                ApplyMotionPose(ghost, startPose, ghost: true);
            }
        }

        private void ApplyMotionPose(VisualElement view, MotionPreviewPose pose, bool ghost)
        {
            ApplyRect(view, pose.Rect);
            view.style.scale = new Scale(pose.Scale);
            view.style.rotate = new Rotate(new Angle(pose.Rotation, AngleUnit.Degree));
            view.style.opacity = Mathf.Clamp01(pose.Opacity) * (ghost ? 0.28f : 1f);
            if (ghost)
            {
                var start = new StyleColor(new Color(0.37f, 0.87f, 0.74f, 0.9f));
                view.style.borderTopColor = start;
                view.style.borderRightColor = start;
                view.style.borderBottomColor = start;
                view.style.borderLeftColor = start;
                view.style.borderTopWidth = 1f;
                view.style.borderRightWidth = 1f;
                view.style.borderBottomWidth = 1f;
                view.style.borderLeftWidth = 1f;
            }
        }

        private void ClearMotionGhosts()
        {
            _motionGhostLayer.Clear();
            _motionGhostViews.Clear();
            _motionGhostClip = null;
            _motionGhostTrackSignature = 0;
        }

        private static int MotionTrackSignature(emiteat.NexUI.MotionClip.UIMotionClip clip)
        {
            unchecked
            {
                var hash = 17;
                foreach (var track in clip.tracks ?? System.Array.Empty<emiteat.NexUI.MotionClip.UIMotionClipTrack>())
                {
                    hash = hash * 31 + (track?.targetElementId?.GetHashCode() ?? 0);
                    hash = hash * 31 + (track?.propertyTracks?.Length ?? 0);
                }
                return hash;
            }
        }

        private static void AddSelectionHandles(VisualElement view)
        {
            foreach (var name in new[] { "nw", "n", "ne", "e", "se", "s", "sw", "w" })
            {
                var handle = new VisualElement();
                handle.AddToClassList("nexui-selection-handle");
                handle.AddToClassList("handle-" + name);
                handle.pickingMode = PickingMode.Ignore;
                view.Add(handle);
            }

            var rotate = new VisualElement();
            rotate.AddToClassList("nexui-rotation-handle");
            rotate.pickingMode = PickingMode.Ignore;
            view.Add(rotate);
        }

        /// <summary>
        /// Draws the element from the components attached to it, layer by layer, so the canvas shows a
        /// filled bar / radial ring / list rows rather than the same bare tinted box - and so removing
        /// a component removes what it drew. Elements with no visual components fall back to the
        /// palette preset's renderer. All fills use percentage sizing so they stay correct as the
        /// element is resized, without any per-drag recomputation.
        /// </summary>
        private void AddTypeSpecificPreview(VisualElement view, DesignerElementMetadata element)
        {
            var ctx = BuildPreviewContext(element);
            DesignerElementPreviewComposer.Build(view, ctx);
        }

        private DesignerPreviewContext BuildPreviewContext(DesignerElementMetadata element)
            => new DesignerPreviewContext(element, EffectivePreviewState(element), _context.Zoom, _context.IsInteractive,
                _context.SelectedMetadata == element ? _context.SelectedComponentPartId : null,
                partId => _context.SelectComponentPart(element, partId),
                partId => _context.BeginComponentPartDrag(element, partId),
                delta => _context.DragComponentPart(delta),
                () => _context.EndComponentPartDrag());

        private static bool IsImagePreviewElement(DesignerElementMetadata element)
            => element.previewImage != null && (element.elementType == "Image" || element.elementType == "IconButton");

        private static Color ImageOutlineColor(Color tint)
        {
            if (tint.a > 0.05f)
                return new Color(tint.r, tint.g, tint.b, 0.8f);
            return new Color(0.35f, 0.66f, 1f, 0.75f);
        }

        private static FontStyle PreviewFontStyle(DesignerTypographyMetadata typography)
        {
            var bold = typography.fontWeight >= DesignerFontWeight.SemiBold ||
                       (typography.fontStyle & DesignerFontStyle.Bold) != 0;
            var italic = (typography.fontStyle & DesignerFontStyle.Italic) != 0;
            return bold && italic ? FontStyle.BoldAndItalic : bold ? FontStyle.Bold : italic ? FontStyle.Italic : FontStyle.Normal;
        }

        /// <summary>
        /// The forced preview state to render <paramref name="element"/> in. In Design mode with no
        /// forced state ⇒ Normal. Otherwise the context's forced state applies, but only for states
        /// the element's component descriptor actually supports (unsupported ⇒ Normal), so e.g.
        /// forcing "Loading" doesn't visibly change a plain Panel.
        /// </summary>
        private DesignerComponentState EffectivePreviewState(DesignerElementMetadata element)
        {
            var forced = _context.ForcedPreviewState;
            if (forced == DesignerComponentState.Normal) return DesignerComponentState.Normal;
            var d = DesignerComponentRegistry.Get(element.elementType);
            return d.SupportsState(forced) ? forced : DesignerComponentState.Normal;
        }

        private static DesignerComponentState ParseState(string name)
            => System.Enum.TryParse<DesignerComponentState>(name, out var s) ? s : DesignerComponentState.Normal;

        /// <summary>
        /// Interactive-mode press handler: value components nudge their preview value (undoable),
        /// interactive components simulate their primary command (logged, never real game logic),
        /// everything else records a plain interaction. A brief opacity dip gives press feedback.
        /// </summary>
        private void HandleInteractivePress(DesignerElementMetadata element, VisualElement view)
        {
            var d = DesignerComponentRegistry.Get(element.elementType);
            view.style.opacity = 0.7f;
            view.schedule.Execute(() => view.style.opacity = 1f).StartingIn(120);

            if (d.IsValueComponent)
            {
                var span = Mathf.Max(1f, element.fill.maxValue - element.fill.minValue);
                var next = element.previewValue + span * 0.2f;
                if (next > element.fill.maxValue) next = element.fill.minValue;
                _context.UpdateElement(element, e => e.previewValue = next, "Preview Value Change");
                _context.LogPreviewInteraction(element, $"value → {next:0.#}");
            }
            else if (d.IsInteractive)
            {
                _context.SimulatePrimaryInteraction(element);
            }
            else
            {
                _context.LogPreviewInteraction(element, "clicked (no interactive behavior)");
            }
        }

        private void RefreshPreviewControls()
        {
            if (_interactiveToggle != null)
            {
                _interactiveToggle.text = _context.IsInteractive ? "● Interactive" : "○ Design";
                _interactiveToggle.EnableInClassList("is-active", _context.IsInteractive);
            }
            if (_statePopup != null)
            {
                var name = _context.ForcedPreviewState.ToString();
                if (PreviewStateChoices.Contains(name) && _statePopup.value != name)
                    _statePopup.SetValueWithoutNotify(name);
            }
            if (_motionPathToggle != null) _motionPathToggle.EnableInClassList("is-active", _context.ShowMotionPath);
            if (_onionSkinToggle != null) _onionSkinToggle.EnableInClassList("is-active", _context.ShowOnionSkin);
            if (_focusNavToggle != null) _focusNavToggle.EnableInClassList("is-active", _context.ShowFocusNav);
        }

        private void ApplyRect(VisualElement view, Rect rect)
        {
            view.style.left = rect.x * _context.Zoom;
            view.style.top = rect.y * _context.Zoom;
            view.style.width = Mathf.Max(16, rect.width * _context.Zoom);
            view.style.height = Mathf.Max(16, rect.height * _context.Zoom);
        }

        // ---- Element drag/resize (also drives group move when the dragged element is part of a
        // multi-selection) ------------------------------------------------------------------

        private void BeginDrag(PointerDownEvent evt, DesignerElementMetadata element, VisualElement view)
        {
            if (evt.button != 0) return;
            Focus();

            // Interactive Preview mode: a click exercises the component (simulated command, logged)
            // instead of selecting/moving it. Value components get a click-to-nudge value change.
            if (_context.IsInteractive)
            {
                HandleInteractivePress(element, view);
                evt.StopPropagation();
                return;
            }

            if (evt.shiftKey)
                _context.AddToSelection(element);
            else if (evt.ctrlKey || evt.commandKey)
                _context.ToggleSelection(element);
            else if (!_context.IsSelected(element))
                _context.SelectMetadata(element);
            else if (_context.SelectedElements.Count > 1 && !evt.shiftKey && !evt.ctrlKey && !evt.commandKey)
                _context.SetKeyObject(element);

            if (!_context.IsSelected(element)) return; // e.g. a ctrl-click just removed it
            if (element.locked) return;

            if (evt.altKey)
            {
                var copies = _context.DuplicateSelectionAtDragStart();
                if (copies.Count > 0)
                {
                    element = copies[copies.Count - 1];
                    if (_views.TryGetValue(element, out var duplicateView))
                        view = duplicateView;
                }
            }

            _dragElement = element;
            _dragStart = new Vector2(evt.position.x, evt.position.y);
            _dragStartRect = element.rect;
            _pendingDragRect = element.rect;
            _lastDragDelta = Vector2.zero;

            var local = view.WorldToLocal(evt.position);
            _resizing = local.x >= view.resolvedStyle.width - 16f && local.y >= view.resolvedStyle.height - 16f;

            // For a move (not a resize), drag the whole subtree: the dragged element (or the full
            // multi-selection when it is part of one) plus every descendant, so children visually
            // follow their parent. Rects are absolute canvas space, so each node translates by the
            // same delta. Resizing only affects the single dragged element.
            _groupDragStartRects = null;
            HashSet<DesignerElementMetadata> closure = null;
            if (!_resizing)
            {
                System.Collections.Generic.IEnumerable<DesignerElementMetadata> roots =
                    (_context.SelectedElements.Count > 1 && _context.IsSelected(element))
                        ? _context.SelectedElements
                        : new[] { element };
                closure = _context.MoveClosure(roots);
                if (closure.Count > 1)
                {
                    _groupDragStartRects = new Dictionary<DesignerElementMetadata, Rect>();
                    foreach (var node in closure)
                        _groupDragStartRects[node] = node.rect;
                }
                CaptureDragPreviewRects(closure);
            }

            view.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void ContinueDrag(PointerMoveEvent evt, VisualElement view)
        {
            if (_dragElement == null || !view.HasPointerCapture(evt.pointerId)) return;

            var current = new Vector2(evt.position.x, evt.position.y);
            var delta = (current - _dragStart) / Mathf.Max(0.01f, _context.Zoom);

            if (_resizing)
            {
                var rect = _dragStartRect;
                rect.width = Mathf.Max(24f, rect.width + delta.x);
                rect.height = Mathf.Max(24f, rect.height + delta.y);
                _pendingDragRect = _context.SnapRect(rect);
                ApplyRect(view, _pendingDragRect);
            }
            else
            {
                // Shift held while moving locks the drag to whichever axis has the larger delta.
                if (evt.shiftKey)
                {
                    if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) delta.y = 0f;
                    else delta.x = 0f;
                }
                _lastDragDelta = delta;

                if (_groupDragStartRects != null)
                {
                    ApplyDragPreviewDelta(delta);
                }
                else
                {
                    var rect = _dragStartRect;
                    rect.position += delta;
                    _pendingDragRect = SnapWithSmartGuides(rect, _dragElement);
                    ApplyDragPreviewDelta(_pendingDragRect.position - _dragStartRect.position);
                }

                UpdateDropTarget(evt.position, evt.ctrlKey || evt.commandKey);
            }

            evt.StopPropagation();
        }

        // ---- Drag-to-reparent ------------------------------------------------------------------
        // Dragging an element over a container makes it a child of that container, the same gesture
        // as dropping a row onto another row in Unity's Hierarchy. Holding Ctrl/Cmd suppresses it for
        // the times you only want to move something across a panel without re-parenting.

        /// <summary>Highlights the container the current drag would drop into, and remembers it for commit.</summary>
        private void UpdateDropTarget(Vector2 worldPosition, bool suppressed)
        {
            ClearDropTargetHighlight();

            if (suppressed || _context.Metadata == null)
            {
                _dropTarget = null;
                _dropTargetResolved = false;
                HideDropHint();
                return;
            }

            var movers = _groupDragStartRects != null
                ? new List<DesignerElementMetadata>(_groupDragStartRects.Keys)
                : new List<DesignerElementMetadata> { _dragElement };

            var canvasPoint = CanvasPoint(worldPosition);
            _dropTarget = DesignerDropTargetResolver.Resolve(_context.Metadata, canvasPoint, movers);
            _dropTargetResolved = true;

            if (!DesignerDropTargetResolver.WouldChangeParent(movers, _dropTarget))
            {
                _dropTarget = null;
                _dropTargetResolved = false;
                HideDropHint();
                return;
            }

            if (_dropTarget != null && _views.TryGetValue(_dropTarget, out var targetView))
                targetView.AddToClassList("is-drop-target");

            ShowDropHint(canvasPoint, DesignerDropTargetResolver.Describe(_dropTarget));
        }

        private void ClearDropTargetHighlight()
        {
            foreach (var pair in _views)
                pair.Value.RemoveFromClassList("is-drop-target");
        }

        private void EndDrag(PointerUpEvent evt, VisualElement view)
        {
            // Right-click must keep bubbling so UI Toolkit can synthesize ContextClickEvent for
            // the canvas menu. BeginDrag only accepts the primary button, so ending a drag must
            // follow the same contract instead of consuming every PointerUp over an element.
            if (!ShouldConsumeElementPointerUp(evt.button)) return;
            if (view.HasPointerCapture(evt.pointerId))
                view.ReleasePointer(evt.pointerId);
            CommitDrag();
            evt.StopPropagation();
        }

        private static bool ShouldConsumeElementPointerUp(int button) => button == 0;

        private void CancelDrag(PointerCancelEvent evt, VisualElement view)
        {
            if (view.HasPointerCapture(evt.pointerId))
                view.ReleasePointer(evt.pointerId);
            RestoreDragPreviewRects();
            ClearDropTargetHighlight();
            _dropTarget = null;
            _dropTargetResolved = false;
            _dragElement = null;
            _resizing = false;
            _groupDragStartRects = null;
            _dragPreviewStartRects = null;
            HideDropHint();
            evt.StopPropagation();
        }

        private void CommitDrag()
        {
            if (_dragElement != null)
            {
                var movers = _groupDragStartRects != null
                    ? new List<DesignerElementMetadata>(_groupDragStartRects.Keys)
                    : new List<DesignerElementMetadata> { _dragElement };

                if (_groupDragStartRects != null)
                {
                    var rects = new Dictionary<DesignerElementMetadata, Rect>();
                    foreach (var pair in _groupDragStartRects)
                    {
                        var r = pair.Value;
                        r.position += _lastDragDelta;
                        rects[pair.Key] = r;
                    }
                    _context.SetElementsRects(rects, "Move NexUI Elements");
                }
                else
                {
                    _context.UpdateElementRect(_dragElement, _pendingDragRect);
                }

                // Re-parent after the rects are committed so the element keeps exactly the position it
                // was dropped at (ReparentElements preserves canvas position by default).
                if (_dropTargetResolved && DesignerDropTargetResolver.WouldChangeParent(movers, _dropTarget))
                    _context.ReparentElements(movers, _dropTarget);
            }

            ClearDropTargetHighlight();
            _dropTarget = null;
            _dropTargetResolved = false;
            _dragElement = null;
            _resizing = false;
            _groupDragStartRects = null;
            _dragPreviewStartRects = null;
            _lastDragDelta = Vector2.zero;
            HideSmartGuides();
            HideDropHint();
        }

        private void CaptureDragPreviewRects(IEnumerable<DesignerElementMetadata> owners)
        {
            _dragPreviewStartRects = new Dictionary<VisualElement, Rect>();
            if (owners == null) return;
            foreach (var owner in owners)
            {
                if (owner == null || !_ownedPreviewViews.TryGetValue(owner, out var views)) continue;
                foreach (var ownedView in views)
                    if (ownedView != null && _previewViewRects.TryGetValue(ownedView, out var rect))
                        _dragPreviewStartRects[ownedView] = rect;
            }
        }

        private void ApplyDragPreviewDelta(Vector2 delta)
        {
            if (_dragPreviewStartRects == null) return;
            foreach (var pair in _dragPreviewStartRects)
            {
                var rect = pair.Value;
                rect.position += delta;
                ApplyRect(pair.Key, rect);
            }
        }

        private void RestoreDragPreviewRects()
        {
            if (_dragPreviewStartRects == null) return;
            foreach (var pair in _dragPreviewStartRects)
                ApplyRect(pair.Key, pair.Value);
        }

        private Rect SnapWithSmartGuides(Rect rect, DesignerElementMetadata moving)
        {
            var snapped = _context.SnapRect(rect);
            if (_context.Metadata == null) return snapped;

            var threshold = Mathf.Max(4f, 8f / Mathf.Max(0.01f, _context.Zoom));

            // User guides win over element edges: a guide is an explicit decision, an element edge is
            // incidental, so a deliberately placed guide should not be overruled by whatever happens
            // to be nearby.
            snapped = DesignerCanvasGuides.Snap(snapped, _userGuides, threshold, out var guideX, out var guideY);

            var guide = NexUISmartGuideUtility.Snap(snapped, _context.Metadata.elements, moving, threshold);
            var result = guide.Rect;
            if (guideX.HasValue) result.x = snapped.x;
            if (guideY.HasValue) result.y = snapped.y;

            ShowSmartGuides(new NexUISmartGuideResult(result,
                guideX ?? guide.VerticalGuide, guideY ?? guide.HorizontalGuide, guide.DistanceLabel), snapped);
            return result;
        }

        private void ShowSmartGuides(NexUISmartGuideResult guide, Rect moving)
        {
            _guideLayer.Clear();
            if (guide.VerticalGuide.HasValue)
            {
                var line = new VisualElement();
                line.AddToClassList("nexui-smart-guide-line");
                line.AddToClassList("is-vertical");
                line.pickingMode = PickingMode.Ignore;
                line.style.left = guide.VerticalGuide.Value * _context.Zoom;
                line.style.height = _previewCanvas.resolvedStyle.height;
                _guideLayer.Add(line);
            }

            if (guide.HorizontalGuide.HasValue)
            {
                var line = new VisualElement();
                line.AddToClassList("nexui-smart-guide-line");
                line.AddToClassList("is-horizontal");
                line.pickingMode = PickingMode.Ignore;
                line.style.top = guide.HorizontalGuide.Value * _context.Zoom;
                line.style.width = _previewCanvas.resolvedStyle.width;
                _guideLayer.Add(line);
            }

            if (string.IsNullOrEmpty(guide.DistanceLabel))
            {
                _distanceLabel.style.display = DisplayStyle.None;
                return;
            }

            _distanceLabel.text = guide.DistanceLabel;
            _distanceLabel.style.display = DisplayStyle.Flex;
            _distanceLabel.style.left = moving.center.x * _context.Zoom + 8f;
            _distanceLabel.style.top = moving.center.y * _context.Zoom + 8f;
        }

        private void HideSmartGuides()
        {
            _guideLayer?.Clear();
            if (_distanceLabel != null)
                _distanceLabel.style.display = DisplayStyle.None;
        }

        // ---- Drag-box (rectangle) selection --------------------------------------------------

        private void OnCanvasPointerDown(PointerDownEvent evt)
        {
            Focus();
            if (TryBeginPan(evt))
            {
                evt.StopPropagation();
                return;
            }
            if (evt.button != 0) return;
            if (evt.target != _previewCanvas && evt.target != _gridLayer && evt.target != _elementLayer) return;

            // Alt-clicking a guide removes it - the cheapest gesture that cannot be confused with
            // selecting, since Alt-drag on an element already means duplicate.
            if (evt.altKey && TryRemoveGuideAt(CanvasPoint(evt.position)))
            {
                evt.StopPropagation();
                return;
            }

            _boxSelectStart = _previewCanvas.WorldToLocal(evt.position);
            _boxSelectShift = evt.shiftKey;
            _boxSelectCtrl = evt.ctrlKey || evt.commandKey;
            _previewCanvas.CapturePointer(evt.pointerId);
            ShowSelectionRect(_boxSelectStart.Value, _boxSelectStart.Value);
            evt.StopPropagation();
        }

        private void OnCanvasPointerMove(PointerMoveEvent evt)
        {
            // Track the pointer on both rulers so the current X/Y is always readable while placing.
            var canvasPoint = CanvasPoint(evt.position);
            _horizontalRuler?.SetCursor(canvasPoint.x);
            _verticalRuler?.SetCursor(canvasPoint.y);

            if (UpdatePan(evt))
            {
                evt.StopPropagation();
                return;
            }
            if (!_boxSelectStart.HasValue || !_previewCanvas.HasPointerCapture(evt.pointerId)) return;
            ShowSelectionRect(_boxSelectStart.Value, _previewCanvas.WorldToLocal(evt.position));
            evt.StopPropagation();
        }

        private Vector2 CanvasPoint(Vector2 worldPosition)
            => _previewCanvas.WorldToLocal(worldPosition) / Mathf.Max(0.01f, _context.Zoom);

        private void OnCanvasPointerUp(PointerUpEvent evt)
        {
            if (EndPan(evt))
            {
                evt.StopPropagation();
                return;
            }
            if (!_boxSelectStart.HasValue) return;
            if (_previewCanvas.HasPointerCapture(evt.pointerId))
                _previewCanvas.ReleasePointer(evt.pointerId);

            var start = _boxSelectStart.Value;
            var current = _previewCanvas.WorldToLocal(evt.position);
            _boxSelectStart = null;
            HideSelectionRect();

            if (Vector2.Distance(start, current) < 3f)
            {
                // A plain click (no drag) on empty canvas clears the selection unless a modifier is held.
                if (!_boxSelectShift && !_boxSelectCtrl)
                    _context.ClearSelection();
                evt.StopPropagation();
                return;
            }

            var selectionRect = Rect.MinMaxRect(
                Mathf.Min(start.x, current.x), Mathf.Min(start.y, current.y),
                Mathf.Max(start.x, current.x), Mathf.Max(start.y, current.y));
            var matches = HitTestRect(selectionRect);

            if (_boxSelectShift)
                foreach (var element in matches) _context.AddToSelection(element);
            else if (_boxSelectCtrl)
                foreach (var element in matches) _context.ToggleSelection(element);
            else
                _context.SelectMany(matches);

            evt.StopPropagation();
        }

        private List<DesignerElementMetadata> HitTestRect(Rect selectionRectScaled)
        {
            var result = new List<DesignerElementMetadata>();
            if (_context.Metadata == null) return result;
            foreach (var element in _context.Metadata.elements)
            {
                if (element == null || element.hiddenInDesigner) continue;
                var scaled = new Rect(
                    element.rect.x * _context.Zoom, element.rect.y * _context.Zoom,
                    element.rect.width * _context.Zoom, element.rect.height * _context.Zoom);
                if (scaled.Overlaps(selectionRectScaled))
                    result.Add(element);
            }
            return result;
        }

        private void ShowSelectionRect(Vector2 a, Vector2 b)
        {
            _selectionRectOverlay.style.display = DisplayStyle.Flex;
            _selectionRectOverlay.style.left = Mathf.Min(a.x, b.x);
            _selectionRectOverlay.style.top = Mathf.Min(a.y, b.y);
            _selectionRectOverlay.style.width = Mathf.Abs(a.x - b.x);
            _selectionRectOverlay.style.height = Mathf.Abs(a.y - b.y);
        }

        private void HideSelectionRect() => _selectionRectOverlay.style.display = DisplayStyle.None;

        // ---- Context menu + rename ------------------------------------------------------------

        private void OnCanvasContextClick(ContextClickEvent evt)
        {
            var local = _previewCanvas.WorldToLocal(evt.mousePosition);
            var canvasPoint = local / Mathf.Max(0.01f, _context.Zoom);
            NexUIDesignerContextMenu.ShowForCanvas(_context, canvasPoint, BeginRename, FitToView);
            evt.StopPropagation();
        }

        private List<DesignerElementMetadata> HitTestPoint(Vector2 point)
        {
            var result = new List<DesignerElementMetadata>();
            if (_context.Metadata == null) return result;
            for (int i = _context.Metadata.elements.Count - 1; i >= 0; i--)
            {
                var element = _context.Metadata.elements[i];
                if (element == null || element.hiddenInDesigner) continue;
                if (element.rect.Contains(point))
                    result.Add(element);
            }
            return result;
        }

        private static string Label(DesignerElementMetadata element)
            => string.IsNullOrEmpty(element.displayName) ? element.elementId : element.displayName;

        // ---- Asset drag & drop -----------------------------------------------------------------
        // Accepts payloads from the Designer's own Assets panel and from Unity's Project window
        // alike, because both use UnityEditor.DragAndDrop. What each payload does is decided by
        // DesignerAssetDropResolver so the rule lives in one testable place; anything it does not
        // recognise is rejected outright rather than guessed at.

        private void OnCanvasDragUpdated(DragUpdatedEvent evt)
        {
            // Drag events expose mousePosition (panel space) rather than the pointer events' position.
            var canvasPoint = _previewCanvas.WorldToLocal(evt.mousePosition) / Mathf.Max(0.01f, _context.Zoom);
            var payload = FirstDraggedObject();
            var target = TopHit(canvasPoint);
            var action = DesignerAssetDropResolver.Resolve(payload, target);

            if (action == DesignerAssetDropAction.None)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                HideDropHint();
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            ShowDropHint(canvasPoint, DesignerAssetDropResolver.Describe(action, payload, target));
            evt.StopPropagation();
        }

        private void OnCanvasDragPerform(DragPerformEvent evt)
        {
            HideDropHint();
            if (_context.Metadata == null) return;

            var canvasPoint = _previewCanvas.WorldToLocal(evt.mousePosition) / Mathf.Max(0.01f, _context.Zoom);
            var payload = FirstDraggedObject();
            var target = TopHit(canvasPoint);
            var action = DesignerAssetDropResolver.Resolve(payload, target);
            if (action == DesignerAssetDropAction.None) return;

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();

            switch (action)
            {
                case DesignerAssetDropAction.SetSprite:
                {
                    var sprite = ResolveSprite(payload);
                    if (sprite == null)
                    {
                        _context.PreviewLog.Log(DesignerPreviewLogKind.Info, target?.elementId,
                            $"'{payload.name}' has no Sprite sub-asset; set its Texture Type to Sprite to use it here.");
                        return;
                    }
                    _context.UpdateElement(target, e => e.previewImage = sprite, "Drop NexUI Sprite");
                    break;
                }
                case DesignerAssetDropAction.SetFont:
                    _context.UpdateElement(target, e =>
                    {
                        var typography = DesignerPropertyAdapter.Typography(e);
                        typography.hasOverrides = true;
                        typography.fontAsset = payload;
                    }, "Drop NexUI Font");
                    break;
                case DesignerAssetDropAction.SetMaterial:
                    _context.UpdateElement(target, e =>
                    {
                        var visual = DesignerPropertyAdapter.Visual(e);
                        visual.hasOverrides = true;
                        visual.material = payload as Material;
                    }, "Drop NexUI Material");
                    break;
                case DesignerAssetDropAction.CreateImage:
                {
                    var sprite = ResolveSprite(payload);
                    if (sprite == null) return;
                    var created = _context.CreateMetadataElement(DesignerElementType.Image);
                    if (created == null) return;
                    var rect = created.rect;
                    rect.position = canvasPoint;
                    if (sprite.rect.width > 0f && sprite.rect.height > 0f)
                        rect.size = new Vector2(sprite.rect.width, sprite.rect.height);
                    _context.UpdateElement(created, e =>
                    {
                        e.previewImage = sprite;
                        e.rect = rect;
                    }, "Drop NexUI Image");
                    break;
                }
                case DesignerAssetDropAction.PlaceComponent:
                {
                    var definition = payload as DesignerComponentDefinitionAsset;
                    var result = DesignerComponentService.Instantiate(_context.Metadata, definition, canvasPoint);
                    if (!result.Success)
                    {
                        Debug.LogError("[NexUI Designer] " + result.Message);
                        return;
                    }
                    _context.InvalidateComponentExpansion();
                    _context.Validate();
                    _context.Select(result.Element);
                    break;
                }
            }
        }

        private static Object FirstDraggedObject()
        {
            var references = DragAndDrop.objectReferences;
            return references != null && references.Length > 0 ? references[0] : null;
        }

        /// <summary>The Sprite a payload represents: itself, or the main sprite sub-asset of a texture.</summary>
        private static Sprite ResolveSprite(Object payload)
        {
            if (payload is Sprite sprite) return sprite;
            if (payload is not Texture2D) return null;

            var path = UnityEditor.AssetDatabase.GetAssetPath(payload);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var sub in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is Sprite found) return found;
            return null;
        }

        /// <summary>Top-most authored element under a canvas point, or null over empty canvas.</summary>
        private DesignerElementMetadata TopHit(Vector2 canvasPoint)
        {
            var hits = HitTestPoint(canvasPoint);
            return hits.Count > 0 ? hits[0] : null;
        }

        private void ShowDropHint(Vector2 canvasPoint, string text)
        {
            if (string.IsNullOrEmpty(text)) { HideDropHint(); return; }
            _dropHint.text = text;
            _dropHint.style.display = DisplayStyle.Flex;
            _dropHint.style.left = canvasPoint.x * _context.Zoom + 12f;
            _dropHint.style.top = canvasPoint.y * _context.Zoom + 12f;
        }

        private void HideDropHint() => _dropHint.style.display = DisplayStyle.None;
        private void BeginRename(DesignerElementMetadata element)
        {
            if (element == null || !_views.TryGetValue(element, out var view)) return;

            _renameField?.RemoveFromHierarchy();
            var field = new TextField { value = string.IsNullOrEmpty(element.displayName) ? element.elementId : element.displayName };
            field.AddToClassList("nexui-rename-field");
            field.style.position = Position.Absolute;
            field.style.left = view.style.left;
            field.style.top = view.style.top;
            field.style.width = view.style.width;

            void Commit()
            {
                var newName = field.value;
                field.RemoveFromHierarchy();
                if (_renameField == field) _renameField = null;
                if (!string.IsNullOrEmpty(newName) && newName != element.displayName)
                    _context.UpdateElement(element, m => m.displayName = newName, "Rename NexUI Element");
            }

            field.RegisterCallback<FocusOutEvent>(_ => Commit());
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape)
                {
                    Commit();
                    e.StopPropagation();
                }
            });

            _renameField = field;
            _elementLayer.Add(field);
            field.Focus();
            field.SelectAll();
        }

        // ---- Selection styling / zoom / shortcuts ---------------------------------------------

        private void RefreshSelection()
        {
            foreach (var pair in _views)
            {
                pair.Value.EnableInClassList("is-selected", _context.IsSelected(pair.Key));
                pair.Value.EnableInClassList("is-key-object", pair.Key == _context.KeyObject);
            }
            RefreshFloatingToolbar();
        }

        private void RefreshFloatingToolbar()
        {
            _floatingToolbar.Clear();
            if (_context.SelectedElements.Count == 0)
            {
                _floatingToolbar.style.display = DisplayStyle.None;
                return;
            }

            _floatingToolbar.style.display = DisplayStyle.Flex;
            var bounds = UIAlignmentUtility.GetBounds(_context.SelectedElements);
            _floatingToolbar.style.left = Mathf.Max(8f, bounds.xMin * _context.Zoom);
            _floatingToolbar.style.top = Mathf.Max(8f, bounds.yMin * _context.Zoom - 38f);

            var label = new Label(_context.SelectedElements.Count == 1
                ? Label(_context.SelectedMetadata)
                : _context.SelectedElements.Count + " selected");
            label.AddToClassList("nexui-floating-label");
            _floatingToolbar.Add(label);

            if (_context.SelectedElements.Count > 1)
            {
                AddFloatingButton("Left", () => _context.AlignSelection("left"), "Align left.");
                AddFloatingButton("Center", () => _context.AlignSelection("centerX"), "Align center.");
                AddFloatingButton("Dist", _context.DistributeSelectionHorizontal, "Distribute horizontally.");
                AddFloatingButton("Group", () => _context.GroupSelection(), "Group selected elements.");
            }
            else
            {
                AddFloatingButton("Copy", () => _context.CopySelection(), "Copy selection.");
                AddFloatingButton("Dup", () => _context.DuplicateSelection(), "Duplicate selection.");
                AddFloatingButton("Lock", () => _context.UpdateSelectedElement(e => e.locked = !e.locked, "Toggle NexUI Element Lock"), "Toggle lock.");
                AddFloatingButton("Hide", () => _context.UpdateSelectedElement(e => e.hiddenInDesigner = !e.hiddenInDesigner, "Toggle NexUI Element Hidden"), "Toggle visibility.");
                AddFloatingButton("Motion", () => MotionClipEditorWindow.Open(_context.PreviewSurface, _context.SelectedMetadata.elementId), "Open Motion Clip Editor.");
            }
        }

        private void AddFloatingButton(string text, System.Action action, string tooltip)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("nexui-floating-button");
            _floatingToolbar.Add(button);
        }

        /// <summary>
        /// Ctrl/Cmd + wheel zooms <b>around the pointer</b> rather than the canvas origin, so the thing
        /// under the cursor stays put. Zooming to origin is the single most disorienting thing a canvas
        /// can do while you are placing elements.
        /// </summary>
        private void OnWheel(WheelEvent evt)
        {
            if (!evt.ctrlKey && !evt.commandKey) return;

            var before = _context.Zoom;
            var localPoint = _previewFrame.WorldToLocal(evt.mousePosition);
            var canvasPoint = (localPoint + _previewFrame.scrollOffset) / Mathf.Max(0.01f, before);

            _context.ZoomBy(evt.delta.y > 0 ? -0.08f : 0.08f);

            var after = _context.Zoom;
            if (!Mathf.Approximately(before, after))
                _previewFrame.scrollOffset = canvasPoint * after - localPoint;

            RefreshRulers();
            evt.StopPropagation();
        }

        // ---- Guides ----------------------------------------------------------------------------

        private string GuidePrefKey
            => "NexUI.Designer.Guides." + (_context.Metadata != null
                ? UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(_context.Metadata))
                : "none");

        /// <summary>
        /// Guides are editor-local working state (like zoom and scroll), so they live in EditorPrefs
        /// keyed by the metadata asset rather than in the asset itself. That keeps them out of Git
        /// diffs and avoids a schema migration for scratch data.
        /// </summary>
        private List<DesignerGuide> LoadGuides()
            => DesignerCanvasGuides.Deserialize(UnityEditor.EditorPrefs.GetString(GuidePrefKey, string.Empty));

        private void SaveGuides()
            => UnityEditor.EditorPrefs.SetString(GuidePrefKey, DesignerCanvasGuides.Serialize(_userGuides));

        private void AddGuide(DesignerGuide guide)
        {
            if (!DesignerCanvasGuides.Add(_userGuides, guide)) return;
            SaveGuides();
            _guideOverlay.Rebuild();
        }

        private void SetGuidePreview(DesignerGuideAxis axis, float? position)
        {
            _guideOverlay.Preview = position.HasValue ? new DesignerGuide(axis, position.Value) : (DesignerGuide?)null;
            _guideOverlay.Rebuild();
        }

        private void ClearGuides()
        {
            if (_userGuides.Count == 0) return;
            _userGuides.Clear();
            SaveGuides();
            _guideOverlay.Rebuild();
        }

        /// <summary>Removes the guide under a canvas point, if any. Returns true when one was removed.</summary>
        private bool TryRemoveGuideAt(Vector2 canvasPoint)
        {
            var vertical = DesignerCanvasGuides.IndexAt(_userGuides, DesignerGuideAxis.Vertical, canvasPoint.x, _context.Zoom);
            var horizontal = DesignerCanvasGuides.IndexAt(_userGuides, DesignerGuideAxis.Horizontal, canvasPoint.y, _context.Zoom);
            var index = vertical >= 0 ? vertical : horizontal;
            if (index < 0) return false;

            _userGuides.RemoveAt(index);
            SaveGuides();
            _guideOverlay.Rebuild();
            return true;
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode != KeyCode.Space) return;
            SetSpaceHeld(false);
            evt.StopPropagation();
        }

        private void SetSpaceHeld(bool held)
        {
            if (_spaceHeld == held) return;
            _spaceHeld = held;
            _previewCanvas.EnableInClassList("is-pan-ready", held);
        }

        private void RefreshRulers()
        {
            _horizontalRuler?.Rebuild();
            _verticalRuler?.Rebuild();
            _guideOverlay?.Rebuild();
        }

        // ---- Pan --------------------------------------------------------------------------------

        private bool TryBeginPan(PointerDownEvent evt)
        {
            // Middle mouse always pans; space-drag pans with the left button so the current tool and
            // selection are untouched.
            if (evt.button != 2 && !(evt.button == 0 && _spaceHeld)) return false;

            _panning = true;
            _panPointerStart = evt.position;
            _panScrollStart = _previewFrame.scrollOffset;
            _previewCanvas.CapturePointer(evt.pointerId);
            return true;
        }

        private bool UpdatePan(PointerMoveEvent evt)
        {
            if (!_panning) return false;
            _previewFrame.scrollOffset = _panScrollStart - ((Vector2)evt.position - _panPointerStart);
            RefreshRulers();
            return true;
        }

        private bool EndPan(PointerUpEvent evt)
        {
            if (!_panning) return false;
            _panning = false;
            if (_previewCanvas.HasPointerCapture(evt.pointerId)) _previewCanvas.ReleasePointer(evt.pointerId);
            return true;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Space is a modifier here, not a command: holding it turns a left-drag into a pan so the
            // hand tool is not a mode you have to enter and leave.
            if (evt.keyCode == KeyCode.Space)
            {
                SetSpaceHeld(true);
                evt.StopPropagation();
                return;
            }

            if (UIDesignerCommandDispatcher.TryDispatch(evt, _context))
            {
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F && _context.SelectedMetadata != null)
            {
                var r = _context.SelectedMetadata.rect;
                _previewFrame.scrollOffset = new Vector2(
                    Mathf.Max(0f, r.center.x * _context.Zoom - _previewFrame.resolvedStyle.width * 0.5f),
                    Mathf.Max(0f, r.center.y * _context.Zoom - _previewFrame.resolvedStyle.height * 0.5f));
                evt.StopPropagation();
            }
        }

        private static Color Lighten(Color color, float amount)
            => Color.Lerp(color, Color.white, amount);

        private static Color Darken(Color color, float amount)
            => Color.Lerp(color, Color.black, amount);
    }
}
