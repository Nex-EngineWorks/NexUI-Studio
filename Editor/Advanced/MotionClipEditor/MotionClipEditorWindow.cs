using System.Collections.Generic;
using System.Linq;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.MotionClipEditor
{
    /// <summary>
    /// Standalone timeline/keyframe editor for <see cref="UIMotionClip"/> assets. Opened either
    /// via Tools/NexUI/Utilities > Motion Clip Editor (pick any clip from the toolbar) or from the
    /// Motion inspector's "Open Motion Clip Editor" button (preview surface + selected element
    /// pre-filled), matching the standalone-window convention used by <c>MotionGraphWindow</c>.
    /// MVP: buttons-first UI, not a polished drag-and-drop timeline.
    /// </summary>
    public sealed class MotionClipEditorWindow : EditorWindow
    {
        [SerializeField] private UIMotionClip _clip;
        [SerializeField] private string _targetElementId;
        [SerializeField] private float _previewTime;
        [SerializeField] private bool _snap = true;

        private enum PlaybackState { Stopped, Playing, Paused }

        private IUISurface _previewSurface;
        private readonly UIMotionClipPlayer _player = new UIMotionClipPlayer();
        private readonly MotionClipPreviewSnapshot _snapshot = new MotionClipPreviewSnapshot();
        private PlaybackState _playbackState = PlaybackState.Stopped;
        private double _lastEditorTime;

        private MotionClipAutoKeyMode _autoKeyMode = MotionClipAutoKeyMode.Off;
        private NexUIDesignerContext _recordingContext;

        private readonly HashSet<string> _lockedTracks = new HashSet<string>();
        private readonly List<MotionClipTimelineView> _timelineViews = new List<MotionClipTimelineView>();

        private Slider _scrubber;
        private Label _timeLabel;
        private MotionClipMasterRuler _masterRuler;
        private VisualElement _tracksHost;
        private VisualElement _statusRow;
        private Button _playButton;
        private Button _stopButton;
        private Button _addTrackButton;

        private MotionClipTimelineContext TimelineContext => new MotionClipTimelineContext
        {
            GetDuration = () => _clip != null ? _clip.duration : 1f,
            GetFps = () => _clip != null ? Mathf.Max(_clip.fps, 1) : 30,
            GetSnap = () => _snap,
            GetPreviewTime = () => _previewTime,
            GetHostWindow = () => this
        };

        /// <summary>Opens the window. Reached through Tools/NexUI/Utilities, which is the single
        /// searchable home for the advanced tools rather than a second copy of them in the menu.</summary>
        public static void OpenFromMenu()
        {
            var window = GetWindow<MotionClipEditorWindow>();
            window.titleContent = new GUIContent("Motion Clip Editor");
            window.minSize = new Vector2(640f, 420f);
            window.BuildUI();
        }

        /// <summary>Opened from the Designer's Motion inspector with the live preview surface and selected element pre-filled.</summary>
        public static void Open(IUISurface previewSurface, string targetElementId)
        {
            var window = GetWindow<MotionClipEditorWindow>();
            window.titleContent = new GUIContent("Motion Clip Editor");
            window.minSize = new Vector2(640f, 420f);
            window._previewSurface = previewSurface;
            window._targetElementId = targetElementId;
            window.BuildUI();
        }

        private void CreateGUI()
        {
            BuildUI();
            DesignerLocalization.LanguageChanged += BuildUI;
        }

        private void OnDisable()
        {
            DesignerLocalization.LanguageChanged -= BuildUI;
            EditorApplication.update -= OnEditorUpdate;
            _player.Stop();
            _snapshot.Restore(ResolvePreviewSurface());
            SetRecordingContext(null);
            ResolveDesignerContext()?.SetActiveMotionClip(null, 0f);
        }

        private void BuildUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("nexui-designer-root");
            root.AddToClassList("nexui-tool-window-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.emiteat.nexui.designer/Editor/Styles/NexUIDesigner.uss");
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);

            root.Add(BuildToolbar());
            root.Add(BuildTimeRow());

            _masterRuler = new MotionClipMasterRuler(
                getClip: () => _clip,
                getPreviewTime: () => _previewTime,
                onJumpToTime: JumpToTime,
                onMarkersChanged: () => _masterRuler.Rebuild());
            root.Add(_masterRuler);

            _statusRow = new VisualElement { name = "PreviewStatusRow" };
            _statusRow.AddToClassList("nexui-status-row");
            root.Add(_statusRow);
            RefreshStatusRow();

            _tracksHost = new VisualElement { name = "TracksHost" };
            _tracksHost.style.flexGrow = 1;
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.Add(_tracksHost);
            root.Add(scroll);

            RefreshTracks();
            ResolveDesignerContext()?.SetActiveMotionClip(_clip, _previewTime);
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("nexui-tool-window-toolbar");

            var clipField = new ObjectField(DesignerLocalization.T("motionClip.toolbar.clip"))
            { objectType = typeof(UIMotionClip), allowSceneObjects = false, value = _clip };
            clipField.AddToClassList("nexui-metadata-field");
            DesignerTooltip.Set(clipField, "tooltip.motionClip.clipField");
            clipField.RegisterValueChangedCallback(evt =>
            {
                _snapshot.Restore(ResolvePreviewSurface());
                StopPlaybackInternal();
                _clip = evt.newValue as UIMotionClip;
                _previewTime = 0f;
                BuildUI();
            });
            toolbar.Add(clipField);

            var createButton = MakeButton(CreateClip, DesignerLocalization.T("motionClip.toolbar.createClip"), "nexui-button-secondary");
            DesignerTooltip.Set(createButton, "tooltip.motionClip.createClip", "Ctrl+N");
            toolbar.Add(createButton);

            var importButton = MakeButton(() =>
            {
                if (Selection.activeObject is AnimationClip)
                {
                    MotionClipConversionMenu.ImportSelected();
                    _clip = Selection.activeObject as UIMotionClip;
                    BuildUI();
                }
            }, DesignerLocalization.T("motionClip.toolbar.importAnimationClip"), "nexui-button-secondary");
            importButton.SetEnabled(Selection.activeObject is AnimationClip);
            importButton.tooltip = DesignerLocalization.T("tooltip.motionClip.importAnimationClip");
            toolbar.Add(importButton);

            _addTrackButton = MakeButton(AddTrackFromSelection, DesignerLocalization.T("motionClip.toolbar.addTrackFromSelection"), "nexui-button-secondary");
            toolbar.Add(_addTrackButton);
            RefreshAddTrackButtonState();

            if (_clip != null)
            {
                var duration = new FloatField(DesignerLocalization.T("motionClip.toolbar.duration")) { value = _clip.duration };
                duration.AddToClassList("nexui-compact-popup");
                DesignerTooltip.Set(duration, "tooltip.motionClip.duration");
                duration.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_clip, "Edit Motion Clip Duration");
                    _clip.duration = Mathf.Max(0.01f, evt.newValue);
                    EditorUtility.SetDirty(_clip);
                    _scrubber.highValue = _clip.duration;
                    RefreshTracks();
                });
                toolbar.Add(duration);

                var fps = new IntegerField(DesignerLocalization.T("motionClip.toolbar.fps")) { value = _clip.fps };
                fps.AddToClassList("nexui-compact-popup");
                DesignerTooltip.Set(fps, "tooltip.motionClip.fps");
                fps.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_clip, "Edit Motion Clip FPS");
                    _clip.fps = Mathf.Max(1, evt.newValue);
                    EditorUtility.SetDirty(_clip);
                    RefreshTracks();
                });
                toolbar.Add(fps);

                var loop = new Toggle(DesignerLocalization.T("motionClip.toolbar.loop")) { value = _clip.loop };
                loop.AddToClassList("nexui-toolbar-toggle");
                DesignerTooltip.Set(loop, "tooltip.motionClip.loop");
                loop.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_clip, "Edit Motion Clip Loop");
                    _clip.loop = evt.newValue;
                    EditorUtility.SetDirty(_clip);
                });
                toolbar.Add(loop);

                var snap = new Toggle(DesignerLocalization.T("motionClip.toolbar.snap")) { value = _snap };
                snap.AddToClassList("nexui-toolbar-toggle");
                DesignerTooltip.Set(snap, "tooltip.motionClip.snap");
                snap.RegisterValueChangedCallback(evt => _snap = evt.newValue);
                toolbar.Add(snap);

                var hasTracks = _clip.tracks != null && _clip.tracks.Length > 0;
                _playButton = MakeButton(TogglePlayPause, PlayPauseButtonText(), "nexui-button-primary");
                _playButton.SetEnabled(hasTracks);
                DesignerTooltip.Set(_playButton, "tooltip.motionClip.play", "Space",
                    hasTracks ? null : DesignerLocalization.T("tooltip.motionClip.reason.noPropertyTracks"));
                toolbar.Add(_playButton);

                _stopButton = MakeButton(StopPlayback, DesignerLocalization.T("motionClip.toolbar.stop"), "nexui-button-secondary");
                RefreshStopButtonState();
                DesignerTooltip.Set(_stopButton, "tooltip.motionClip.stop");
                toolbar.Add(_stopButton);

                var autoKeyChoices = new List<string>
                {
                    DesignerLocalization.T("motionClip.autoKey.off"),
                    DesignerLocalization.T("motionClip.autoKey.existingTracks"),
                    DesignerLocalization.T("motionClip.autoKey.allChanges")
                };
                var autoKey = new PopupField<string>(DesignerLocalization.T("motionClip.toolbar.autoKey"), autoKeyChoices, (int)_autoKeyMode);
                autoKey.AddToClassList("nexui-compact-popup");
                DesignerTooltip.Set(autoKey, "tooltip.motionClip.autoKey");
                autoKey.RegisterValueChangedCallback(evt =>
                {
                    _autoKeyMode = (MotionClipAutoKeyMode)autoKeyChoices.IndexOf(evt.newValue);
                    UpdateRecordingSubscription();
                    RefreshStatusRow();
                });
                toolbar.Add(autoKey);

                var saveButton = MakeButton(() => AssetDatabase.SaveAssets(), DesignerLocalization.T("motionClip.toolbar.save"), "nexui-button-secondary");
                DesignerTooltip.Set(saveButton, "tooltip.motionClip.save", "Ctrl+S");
                toolbar.Add(saveButton);

                var exportButton = MakeButton(() => MotionClipConversionMenu.Export(_clip), "AnimationClip 내보내기", "nexui-button-secondary");
                exportButton.tooltip = "현재 Motion Clip을 uGUI GameObject용 AnimationClip으로 내보냅니다.";
                toolbar.Add(exportButton);

                toolbar.Add(BuildWorkAreaControls());
            }

            return toolbar;
        }

        private VisualElement BuildWorkAreaControls()
        {
            var row = new VisualElement();
            row.AddToClassList("nexui-inline-row");

            var useWorkArea = new Toggle(DesignerLocalization.T("motionClip.toolbar.useWorkArea")) { value = _clip.useWorkArea };
            useWorkArea.AddToClassList("nexui-toolbar-toggle");
            DesignerTooltip.Set(useWorkArea, "tooltip.motionClip.useWorkArea");
            row.Add(useWorkArea);

            var startField = new FloatField(DesignerLocalization.T("motionClip.toolbar.workAreaStart")) { value = _clip.workAreaStart };
            startField.style.width = 90f;
            var endField = new FloatField(DesignerLocalization.T("motionClip.toolbar.workAreaEnd")) { value = _clip.workAreaEnd };
            endField.style.width = 90f;

            useWorkArea.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_clip, "Edit Motion Clip Work Area");
                _clip.useWorkArea = evt.newValue;
                EditorUtility.SetDirty(_clip);
                _masterRuler?.Rebuild();
            });
            startField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_clip, "Edit Motion Clip Work Area");
                _clip.workAreaStart = Mathf.Clamp(evt.newValue, 0f, _clip.duration);
                EditorUtility.SetDirty(_clip);
                _masterRuler?.Rebuild();
            });
            endField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_clip, "Edit Motion Clip Work Area");
                _clip.workAreaEnd = Mathf.Clamp(evt.newValue, 0f, _clip.duration);
                EditorUtility.SetDirty(_clip);
                _masterRuler?.Rebuild();
            });

            row.Add(startField);
            row.Add(endField);
            return row;
        }

        private string PlayPauseButtonText() => DesignerLocalization.T(
            _playbackState == PlaybackState.Playing ? "motionClip.toolbar.pause" : "motionClip.toolbar.play");

        private void RefreshStopButtonState()
        {
            if (_stopButton == null) return;
            var canStop = _playbackState != PlaybackState.Stopped || _snapshot.IsCaptured;
            _stopButton.SetEnabled(canStop);
            DesignerTooltip.Set(_stopButton, "tooltip.motionClip.stop", null,
                canStop ? null : DesignerLocalization.T("tooltip.motionClip.reason.nothingToStop"));
        }

        private void RefreshAddTrackButtonState()
        {
            if (_addTrackButton == null) return;
            var canAdd = _clip != null;
            _addTrackButton.SetEnabled(canAdd);
            DesignerTooltip.Set(_addTrackButton, "tooltip.motionClip.addTrackFromSelection", "T",
                canAdd ? null : DesignerLocalization.T("tooltip.motionClip.reason.noClip"));
        }

        private VisualElement BuildTimeRow()
        {
            var row = new VisualElement { name = "TimeRow" };
            row.AddToClassList("nexui-toolbar-row");
            row.AddToClassList("nexui-tool-window-time-row");

            _scrubber = new Slider(DesignerLocalization.T("motionClip.toolbar.time"), 0f, _clip != null ? _clip.duration : 1f) { value = _previewTime };
            _scrubber.style.flexGrow = 1;
            DesignerTooltip.Set(_scrubber, "tooltip.motionClip.scrubber");
            _scrubber.RegisterValueChangedCallback(evt =>
            {
                _previewTime = evt.newValue;
                EvaluatePreview();
                RefreshPlayheads();
                RefreshTimeLabel();
            });
            row.Add(_scrubber);

            _timeLabel = new Label();
            _timeLabel.AddToClassList("nexui-toolbar-status");
            _timeLabel.AddToClassList("is-muted");
            row.Add(_timeLabel);
            RefreshTimeLabel();

            if (_clip != null)
            {
                var addMarkerButton = MakeButton(AddMarkerAtCurrentTime, DesignerLocalization.T("motionClip.toolbar.addMarker"), "nexui-button-secondary");
                DesignerTooltip.Set(addMarkerButton, "tooltip.motionClip.addMarker");
                row.Add(addMarkerButton);
            }

            return row;
        }

        private void AddMarkerAtCurrentTime()
        {
            if (_clip == null) return;
            Undo.RecordObject(_clip, "Add Motion Clip Marker");
            var markers = new List<UIMotionClipMarker>(_clip.markers)
            {
                new UIMotionClipMarker($"Marker {_clip.markers.Length + 1}", _previewTime)
            };
            markers.Sort((a, b) => a.time.CompareTo(b.time));
            _clip.markers = markers.ToArray();
            EditorUtility.SetDirty(_clip);
            _masterRuler?.Rebuild();
        }

        private void RefreshTimeLabel()
        {
            if (_timeLabel == null) return;
            var fps = _clip != null ? Mathf.Max(_clip.fps, 1) : 30;
            var frame = Mathf.RoundToInt(_previewTime * fps);
            _timeLabel.text = $"{_previewTime:0.000}s / {frame}f";
        }

        private void RefreshStatusRow()
        {
            if (_statusRow == null) return;
            _statusRow.Clear();

            var surface = ResolvePreviewSurface();
            var targetLabel = new Label($"{DesignerLocalization.T("motionClip.status.target")}: " +
                (surface != null ? surface.ScreenId : DesignerLocalization.T("motionClip.status.none")));
            targetLabel.AddToClassList("nexui-toolbar-status");
            targetLabel.AddToClassList("is-muted");
            _statusRow.Add(targetLabel);

            var connected = surface != null;
            var statusLabel = new Label($"{DesignerLocalization.T("motionClip.status.label")}: " +
                DesignerLocalization.T(connected ? "motionClip.status.connected" : "motionClip.status.disconnected"));
            statusLabel.AddToClassList("nexui-toolbar-status");
            statusLabel.AddToClassList(connected ? "is-ok" : "is-warning");
            _statusRow.Add(statusLabel);

            if (!connected)
            {
                var openButton = MakeButton(() => EditorApplication.ExecuteMenuItem("Tools/NexUI/Designer"),
                    DesignerLocalization.T("motionClip.status.openDesigner"), "nexui-button-secondary");
                DesignerTooltip.Set(openButton, "tooltip.motionClip.openDesigner");
                _statusRow.Add(openButton);
            }

            if (_autoKeyMode != MotionClipAutoKeyMode.Off)
            {
                var recordingLabel = new Label(DesignerLocalization.T("motionClip.status.recording"));
                recordingLabel.AddToClassList("nexui-toolbar-status");
                recordingLabel.AddToClassList("is-warning");
                DesignerTooltip.Set(recordingLabel, "tooltip.motionClip.recording");
                _statusRow.Add(recordingLabel);
            }
        }

        private void CreateClip()
        {
            var asset = ScriptableObject.CreateInstance<UIMotionClip>();
            asset.clipName = "NewMotionClip";
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/NewMotionClip.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _clip = asset;
            EditorGUIUtility.PingObject(asset);
            BuildUI();
        }

        private void AddTrackFromSelection()
        {
            if (_clip == null) return;

            var elementId = _targetElementId;
            if (string.IsNullOrEmpty(elementId))
            {
                var context = DesignerSessions.ActiveContext;
                if (context?.SelectedMetadata != null)
                {
                    elementId = context.SelectedMetadata.elementId;
                    _previewSurface = context.PreviewSurface;
                }
            }

            if (string.IsNullOrEmpty(elementId))
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("motionClip.toolbar.createClip"),
                    DesignerLocalization.T("tooltip.motionClip.reason.noElementSelected"), "OK");
                return;
            }

            var tracks = new List<UIMotionClipTrack>(_clip.tracks ?? System.Array.Empty<UIMotionClipTrack>());
            if (tracks.Any(t => t.targetElementId == elementId))
            {
                RefreshTracks();
                return;
            }

            Undo.RecordObject(_clip, "Add Motion Clip Track");
            tracks.Add(new UIMotionClipTrack { targetElementId = elementId });
            _clip.tracks = tracks.ToArray();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void RefreshTracks()
        {
            if (_tracksHost == null) return;
            _tracksHost.Clear();
            _timelineViews.Clear();
            RefreshAddTrackButtonState();
            RefreshStatusRow();
            _masterRuler?.Rebuild();

            if (_clip == null)
            {
                _tracksHost.Add(new HelpBox(DesignerLocalization.T("motionClip.toolbar.noClip"), HelpBoxMessageType.Info));
                return;
            }

            if (_clip.tracks == null || _clip.tracks.Length == 0)
            {
                _tracksHost.Add(new HelpBox(DesignerLocalization.T("motionClip.toolbar.noTracks"), HelpBoxMessageType.Info));
                return;
            }

            foreach (var track in _clip.tracks)
                _tracksHost.Add(BuildTrackView(track));
        }

        private VisualElement BuildTrackView(UIMotionClipTrack track)
        {
            var section = new VisualElement();
            section.AddToClassList("nexui-inspector-section");

            var header = new Label(track.targetElementId) { name = "SectionTitle" };
            section.Add(header);

            var addPropertyRow = new VisualElement();
            addPropertyRow.AddToClassList("nexui-inline-row");
            var propertyPopup = new EnumField(DesignerLocalization.T("motionClip.toolbar.property"), UIMotionClipPropertyType.AnchoredPosition);
            propertyPopup.style.flexGrow = 1;
            addPropertyRow.Add(propertyPopup);
            var addPropertyButton = MakeButton(() => AddPropertyTrack(track, (UIMotionClipPropertyType)propertyPopup.value),
                DesignerLocalization.T("motionClip.toolbar.addPropertyTrack"), "nexui-button-secondary");
            DesignerTooltip.Set(addPropertyButton, "tooltip.motionClip.addPropertyTrack");
            addPropertyRow.Add(addPropertyButton);
            section.Add(addPropertyRow);

            var propertyTracks = track.propertyTracks ?? System.Array.Empty<UIMotionClipPropertyTrack>();
            foreach (MotionClipPropertyCategory category in System.Enum.GetValues(typeof(MotionClipPropertyCategory)))
            {
                var inCategory = propertyTracks.Where(p => MotionClipPropertyCategoryUtility.CategoryOf(p.propertyType) == category).ToList();
                if (inCategory.Count == 0) continue;
                section.Add(BuildCategoryFoldout(track, category, inCategory));
            }

            return section;
        }

        private VisualElement BuildCategoryFoldout(UIMotionClipTrack track, MotionClipPropertyCategory category, List<UIMotionClipPropertyTrack> propertyTracks)
        {
            var prefsKey = $"NexUI.MotionClipEditor.Foldout.{(_clip != null ? _clip.name : "clip")}.{track.targetElementId}.{category}";
            var foldout = new Foldout
            {
                text = $"{DesignerLocalization.T(MotionClipPropertyCategoryUtility.LocalizationKey(category))} ({propertyTracks.Count})",
                value = EditorPrefs.GetBool(prefsKey, true)
            };
            foldout.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(prefsKey, evt.newValue));

            foreach (var propertyTrack in propertyTracks)
                foldout.Add(BuildPropertyTrackView(track, propertyTrack));

            return foldout;
        }

        private void AddPropertyTrack(UIMotionClipTrack track, UIMotionClipPropertyType propertyType)
        {
            var propertyTracks = new List<UIMotionClipPropertyTrack>(track.propertyTracks ?? System.Array.Empty<UIMotionClipPropertyTrack>());
            if (propertyTracks.Any(p => p.propertyType == propertyType))
            {
                RefreshTracks();
                return;
            }

            Undo.RecordObject(_clip, "Add Motion Clip Property Track");
            propertyTracks.Add(new UIMotionClipPropertyTrack { propertyType = propertyType });
            track.propertyTracks = propertyTracks.ToArray();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private VisualElement BuildPropertyTrackView(UIMotionClipTrack track, UIMotionClipPropertyTrack propertyTrack)
        {
            var box = new VisualElement();
            box.AddToClassList("nexui-panel");

            var trackKey = $"{track.targetElementId}::{propertyTrack.propertyType}";
            var locked = _lockedTracks.Contains(trackKey);
            var keyCount = propertyTrack.keyframes?.Length ?? 0;

            var titleRow = new VisualElement();
            titleRow.AddToClassList("nexui-inline-row");
            var trackTitle = new Label($"{propertyTrack.propertyType} ({keyCount})") { name = "PanelTitle" };
            trackTitle.style.flexGrow = 1f;
            titleRow.Add(trackTitle);

            var lockButton = new Button(() =>
            {
                if (!_lockedTracks.Add(trackKey)) _lockedTracks.Remove(trackKey);
                RefreshTracks();
            })
            { text = DesignerLocalization.T(locked ? "motionClip.toolbar.unlockTrack" : "motionClip.toolbar.lockTrack") };
            lockButton.AddToClassList("nexui-toolbar-button");
            lockButton.AddToClassList("nexui-button-secondary");
            DesignerTooltip.Set(lockButton, "tooltip.motionClip.lockTrack");
            titleRow.Add(lockButton);

            var addKeyButton = MakeButton(() => AddKeyframe(track, propertyTrack), DesignerLocalization.T("motionClip.toolbar.addKeyframe"), "nexui-button-secondary");
            addKeyButton.SetEnabled(!locked);
            DesignerTooltip.Set(addKeyButton, "tooltip.motionClip.addKeyframe", "K", locked ? DesignerLocalization.T("tooltip.motionClip.reason.trackLocked") : null);
            titleRow.Add(addKeyButton);

            var deleteTrackButton = MakeButton(() => DeletePropertyTrack(track, propertyTrack), DesignerLocalization.T("motionClip.toolbar.deleteTrack"), "nexui-button-secondary");
            DesignerTooltip.Set(deleteTrackButton, "tooltip.motionClip.deleteTrack");
            titleRow.Add(deleteTrackButton);

            var scaleFactorField = new FloatField { value = 1f };
            scaleFactorField.style.width = 50f;
            DesignerTooltip.Set(scaleFactorField, "tooltip.motionClip.scaleTimingFactor");
            titleRow.Add(scaleFactorField);

            var moreButton = MakeButton(() => ShowTrackOpsMenu(propertyTrack, scaleFactorField.value), "⋮", "nexui-button-secondary");
            moreButton.SetEnabled(!locked);
            DesignerTooltip.Set(moreButton, "tooltip.motionClip.moreTrackOps");
            titleRow.Add(moreButton);

            box.Add(titleRow);

            var timeline = new MotionClipTimelineView(propertyTrack, TimelineContext,
                onChanged: () => { EditorUtility.SetDirty(_clip); EvaluatePreview(); },
                onDeleteKeyframes: indices => DeleteKeyframes(track, propertyTrack, indices),
                locked: locked);
            _timelineViews.Add(timeline);
            box.Add(timeline);

            return box;
        }

        // ---- Track-level keyframe operations (Copy/Paste/Reverse/Scale Timing) ----------------

        private static UIMotionClipKeyframe[] _keyframeClipboard;
        private static UIMotionClipPropertyType _keyframeClipboardType;

        private void ShowTrackOpsMenu(UIMotionClipPropertyTrack propertyTrack, float scaleFactor)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(DesignerLocalization.T("motionClip.toolbar.copyKeyframes")), false,
                () => CopyKeyframes(propertyTrack));

            if (_keyframeClipboard != null && _keyframeClipboardType == propertyTrack.propertyType)
                menu.AddItem(new GUIContent(DesignerLocalization.T("motionClip.toolbar.pasteKeyframes")), false,
                    () => PasteKeyframes(propertyTrack));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("motionClip.toolbar.pasteKeyframes")));

            menu.AddItem(new GUIContent(DesignerLocalization.T("motionClip.toolbar.reverseKeyframes")), false,
                () => ReverseKeyframes(propertyTrack));
            menu.AddItem(new GUIContent(DesignerLocalization.T("motionClip.toolbar.scaleTiming")), false,
                () => ScaleTiming(propertyTrack, scaleFactor));
            menu.ShowAsContext();
        }

        private void CopyKeyframes(UIMotionClipPropertyTrack propertyTrack)
        {
            _keyframeClipboard = (UIMotionClipKeyframe[])(propertyTrack.keyframes ?? System.Array.Empty<UIMotionClipKeyframe>()).Clone();
            _keyframeClipboardType = propertyTrack.propertyType;
        }

        private void PasteKeyframes(UIMotionClipPropertyTrack propertyTrack)
        {
            if (_keyframeClipboard == null || _keyframeClipboardType != propertyTrack.propertyType) return;
            Undo.RecordObject(_clip, "Paste Motion Clip Keyframes");
            propertyTrack.keyframes = (UIMotionClipKeyframe[])_keyframeClipboard.Clone();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void ReverseKeyframes(UIMotionClipPropertyTrack propertyTrack)
        {
            var keyframes = propertyTrack.keyframes;
            if (keyframes == null || keyframes.Length == 0) return;

            Undo.RecordObject(_clip, "Reverse Motion Clip Keyframes");
            var duration = _clip.duration;
            var reversed = new UIMotionClipKeyframe[keyframes.Length];
            for (var i = 0; i < keyframes.Length; i++)
            {
                var source = keyframes[keyframes.Length - 1 - i];
                source.time = duration - source.time;
                reversed[i] = source;
            }
            propertyTrack.keyframes = reversed;
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void ScaleTiming(UIMotionClipPropertyTrack propertyTrack, float factor)
        {
            var keyframes = propertyTrack.keyframes;
            if (keyframes == null || keyframes.Length == 0 || Mathf.Approximately(factor, 1f)) return;

            Undo.RecordObject(_clip, "Scale Motion Clip Timing");
            var duration = _clip.duration;
            for (var i = 0; i < keyframes.Length; i++)
            {
                var keyframe = keyframes[i];
                keyframe.time = Mathf.Clamp(keyframe.time * factor, 0f, duration);
                keyframes[i] = keyframe;
            }
            System.Array.Sort(keyframes, (a, b) => a.time.CompareTo(b.time));
            propertyTrack.keyframes = keyframes;
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void DeletePropertyTrack(UIMotionClipTrack track, UIMotionClipPropertyTrack propertyTrack)
        {
            var propertyTracks = new List<UIMotionClipPropertyTrack>(track.propertyTracks);
            Undo.RecordObject(_clip, "Delete Motion Clip Property Track");
            propertyTracks.Remove(propertyTrack);
            track.propertyTracks = propertyTracks.ToArray();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void AddKeyframe(UIMotionClipTrack track, UIMotionClipPropertyTrack propertyTrack)
        {
            var value = DefaultValueFor(propertyTrack.propertyType);
            var keyframes = new List<UIMotionClipKeyframe>(propertyTrack.keyframes ?? System.Array.Empty<UIMotionClipKeyframe>())
            {
                new UIMotionClipKeyframe(_previewTime, value)
            };
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));

            Undo.RecordObject(_clip, "Add Motion Clip Keyframe");
            propertyTrack.keyframes = keyframes.ToArray();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private void DeleteKeyframes(UIMotionClipTrack track, UIMotionClipPropertyTrack propertyTrack, IReadOnlyList<int> indices)
        {
            var keyframes = new List<UIMotionClipKeyframe>(propertyTrack.keyframes);
            var sortedDescending = indices.Where(i => i >= 0 && i < keyframes.Count).OrderByDescending(i => i).ToList();
            if (sortedDescending.Count == 0) return;

            Undo.RecordObject(_clip, sortedDescending.Count > 1 ? "Delete Motion Clip Keyframes" : "Delete Motion Clip Keyframe");
            foreach (var index in sortedDescending)
                keyframes.RemoveAt(index);
            propertyTrack.keyframes = keyframes.ToArray();
            EditorUtility.SetDirty(_clip);
            RefreshTracks();
        }

        private static UIMotionClipValue DefaultValueFor(UIMotionClipPropertyType propertyType)
        {
            switch (propertyType)
            {
                case UIMotionClipPropertyType.LocalRotationZ:
                case UIMotionClipPropertyType.CanvasGroupAlpha:
                    return UIMotionClipValue.Float(propertyType == UIMotionClipPropertyType.CanvasGroupAlpha ? 1f : 0f);
                case UIMotionClipPropertyType.LocalScale:
                    return UIMotionClipValue.FromVector3(Vector3.one);
                default:
                    return UIMotionClipValue.FromVector2(Vector2.zero);
            }
        }

        // ---- Preview -------------------------------------------------------

        private IUISurface ResolvePreviewSurface()
        {
            if (_previewSurface != null) return _previewSurface;
            return ResolveDesignerContext()?.PreviewSurface;
        }

        private NexUIDesignerContext ResolveDesignerContext()
        {
            return DesignerSessions.ActiveContext;
        }

        private void EvaluatePreview()
        {
            if (_clip == null) return;
            var surface = ResolvePreviewSurface();
            if (surface == null) return;
            var wasCaptured = _snapshot.IsCaptured;
            _snapshot.CaptureIfNeeded(surface, _clip);
            if (!wasCaptured && _snapshot.IsCaptured) RefreshStopButtonState();
            _player.Evaluate(surface, _clip, _previewTime);
        }

        private void RefreshPlayheads()
        {
            foreach (var view in _timelineViews) view.RefreshPlayhead();
            _masterRuler?.RefreshPlayhead();
            ResolveDesignerContext()?.SetActiveMotionClipTime(_previewTime);
        }

        private void JumpToTime(float time)
        {
            if (_clip == null) return;
            _previewTime = Mathf.Clamp(time, 0f, _clip.duration);
            if (_scrubber != null) _scrubber.SetValueWithoutNotify(_previewTime);
            RefreshTimeLabel();
            RefreshPlayheads();
            EvaluatePreview();
        }

        private void TogglePlayPause()
        {
            if (_playbackState == PlaybackState.Playing) PausePlayback();
            else StartPlayback();
        }

        private void StartPlayback()
        {
            if (_clip == null) return;

            // Pressing Play right after a non-looping clip auto-paused at its end would otherwise
            // immediately re-trigger the end-of-clip pause on the very next tick (time is still at
            // duration) - restart from 0 instead, matching "재생: 처음부터 재생" (brief §6.2).
            if (_previewTime >= _clip.duration)
            {
                _previewTime = 0f;
                if (_scrubber != null) _scrubber.SetValueWithoutNotify(0f);
                RefreshTimeLabel();
                RefreshPlayheads();
                EvaluatePreview();
            }

            _playbackState = PlaybackState.Playing;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (_playButton != null) _playButton.text = PlayPauseButtonText();
            RefreshStopButtonState();
            EditorApplication.update += OnEditorUpdate;
        }

        private void PausePlayback()
        {
            _playbackState = PlaybackState.Paused;
            if (_playButton != null) _playButton.text = PlayPauseButtonText();
            RefreshStopButtonState();
            EditorApplication.update -= OnEditorUpdate;
        }

        /// <summary>Explicit Stop: ends playback, rewinds to 0, and restores the pre-preview snapshot (brief 6.3: "Stop/Close -> Restore Snapshot"). Scrubbing/pausing mid-clip deliberately leaves the pose in place so a specific time can be inspected.</summary>
        private void StopPlayback()
        {
            StopPlaybackInternal();
            _previewTime = 0f;
            if (_scrubber != null) _scrubber.SetValueWithoutNotify(0f);
            RefreshTimeLabel();
            RefreshPlayheads();
            _snapshot.Restore(ResolvePreviewSurface());
            RefreshStopButtonState();
        }

        private void StopPlaybackInternal()
        {
            _playbackState = PlaybackState.Stopped;
            if (_playButton != null) _playButton.text = PlayPauseButtonText();
            RefreshStopButtonState();
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            _previewTime += delta;
            var duration = _clip != null ? _clip.duration : 0f;
            if (_previewTime >= duration)
            {
                if (_clip != null && _clip.loop)
                    _previewTime %= Mathf.Max(duration, 0.0001f);
                else
                {
                    _previewTime = duration;
                    PausePlayback();
                }
            }

            if (_scrubber != null) _scrubber.SetValueWithoutNotify(_previewTime);
            RefreshTimeLabel();
            RefreshPlayheads();
            EvaluatePreview();
        }

        // ---- Auto Key / Record ---------------------------------------------

        private void UpdateRecordingSubscription()
        {
            SetRecordingContext(_autoKeyMode != MotionClipAutoKeyMode.Off ? ResolveDesignerContext() : null);
        }

        private void SetRecordingContext(NexUIDesignerContext context)
        {
            if (_recordingContext == context) return;
            if (_recordingContext != null) _recordingContext.ElementChanged -= OnCanvasElementChanged;
            _recordingContext = context;
            if (_recordingContext != null) _recordingContext.ElementChanged += OnCanvasElementChanged;
        }

        /// <summary>Fired by <see cref="NexUIDesignerContext.UpdateElementRect"/> whenever the Designer canvas commits a move/resize drag; if Auto Key is on, mirrors that rect into a keyframe at the current scrub time instead of (in addition to) the metadata rect.</summary>
        private void OnCanvasElementChanged(DesignerElementMetadata element)
        {
            if (_clip == null || element == null || _autoKeyMode == MotionClipAutoKeyMode.Off) return;

            // One canvas drag can touch the track array (new track/property track) and two separate
            // keyframe upserts - each records its own Undo step. Collapse them so a single Ctrl+Z
            // reverts the whole drag, instead of requiring up to three.
            NexUIDesignerUndo.Group("Auto Key", () =>
            {
                var track = _clip.tracks?.FirstOrDefault(t => t.targetElementId == element.elementId);
                if (track == null)
                {
                    if (_autoKeyMode != MotionClipAutoKeyMode.AllChanges) return;
                    Undo.RecordObject(_clip, "Auto Key");
                    var tracks = new List<UIMotionClipTrack>(_clip.tracks ?? System.Array.Empty<UIMotionClipTrack>());
                    track = new UIMotionClipTrack { targetElementId = element.elementId };
                    tracks.Add(track);
                    _clip.tracks = tracks.ToArray();
                }

                UpsertAutoKey(track, UIMotionClipPropertyType.AnchoredPosition, UIMotionClipValue.FromVector2(element.rect.position));
                UpsertAutoKey(track, UIMotionClipPropertyType.SizeDelta, UIMotionClipValue.FromVector2(element.rect.size));

                EditorUtility.SetDirty(_clip);
            });

            RefreshTracks();
        }

        private void UpsertAutoKey(UIMotionClipTrack track, UIMotionClipPropertyType propertyType, UIMotionClipValue value)
        {
            var propertyTrack = track.propertyTracks?.FirstOrDefault(p => p.propertyType == propertyType);
            if (propertyTrack == null)
            {
                if (_autoKeyMode != MotionClipAutoKeyMode.AllChanges) return;
                Undo.RecordObject(_clip, "Auto Key");
                var propertyTracks = new List<UIMotionClipPropertyTrack>(track.propertyTracks ?? System.Array.Empty<UIMotionClipPropertyTrack>());
                propertyTrack = new UIMotionClipPropertyTrack { propertyType = propertyType };
                propertyTracks.Add(propertyTrack);
                track.propertyTracks = propertyTracks.ToArray();
            }

            var fps = Mathf.Max(_clip.fps, 1);
            var frameEpsilon = 0.5f / fps;
            var keyframes = new List<UIMotionClipKeyframe>(propertyTrack.keyframes ?? System.Array.Empty<UIMotionClipKeyframe>());
            var existingIndex = keyframes.FindIndex(k => Mathf.Abs(k.time - _previewTime) <= frameEpsilon);

            Undo.RecordObject(_clip, "Auto Key");
            if (existingIndex >= 0)
            {
                var existing = keyframes[existingIndex];
                existing.value = value;
                keyframes[existingIndex] = existing;
            }
            else
            {
                keyframes.Add(new UIMotionClipKeyframe(_previewTime, value));
                keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            }
            propertyTrack.keyframes = keyframes.ToArray();
        }

        private static Button MakeButton(System.Action action, string text, string className)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("nexui-toolbar-button");
            button.AddToClassList(className);
            return button;
        }
    }
}
