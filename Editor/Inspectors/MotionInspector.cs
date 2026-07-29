using System;
using System.Collections.Generic;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Graph;
using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.MotionClipEditor;
using emiteat.NexUI.Designer.Editor.Productivity;
using emiteat.NexUI.Motion;
using emiteat.NexUI.MotionClip;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Inspectors
{
    /// <summary>
    /// Element motion authoring with a live, runtime-math preview. The compact stage evaluates the
    /// same compiled <see cref="UIMotionTimeline"/> and easing function used by playback, while the
    /// example gallery routes into the existing transition-preset workflow instead of inventing a
    /// second animation asset format.
    /// </summary>
    public sealed class MotionInspector : DesignerInspectorBase
    {
        private const string MotionSectionPrefKey = "NexUI.Designer.MotionInspector.ActiveSection";

        private enum MotionSection
        {
            Settings,
            Examples,
            Triggers
        }

        private readonly ObjectField _preset;
        private readonly TextField _motionId;
        private readonly TextField _initial;
        private readonly TextField _animate;
        private readonly TextField _exit;
        private readonly TextField _hover;
        private readonly TextField _pressed;
        private readonly TextField _focus;
        private readonly Button _openGraph;
        private readonly Button _openClipEditor;
        private readonly Label _noPresetHelp;
        private readonly ObjectField _entryClip;
        private readonly ObjectField _exitClip;
        private readonly Foldout _bindings;
        private readonly Button _addBinding;
        private readonly MotionLiveExamplePreview _preview;
        private readonly Label _overviewTitle;
        private readonly Label _overviewSubtitle;
        private readonly Label _overviewBadge;
        private readonly Label _previewTitle;
        private readonly Label _previewDescription;
        private readonly Button _useExample;
        private readonly Dictionary<MotionSection, Button> _sectionButtons = new Dictionary<MotionSection, Button>();
        private readonly Dictionary<MotionSection, VisualElement> _sectionPages = new Dictionary<MotionSection, VisualElement>();
        private bool _refreshing;
        private bool _showingExample;
        private DesignerTransitionPreset _selectedExample = DesignerTransitionPreset.Fade;

        public MotionInspector(NexUIDesignerContext context) : base(context, "inspector.motion")
        {
            AddToClassList("nexui-motion-inspector");

            _preset = new ObjectField(DesignerLocalization.T("motionInspector.preset"))
            {
                objectType = typeof(UIMotionPreset), allowSceneObjects = false,
                tooltip = DesignerLocalization.T("tooltip.motion.preset")
            };
            _motionId = new TextField(DesignerLocalization.T("motionInspector.motionId")) { tooltip = DesignerLocalization.T("tooltip.motion.motionId") };
            _initial = VariantField("motionInspector.initial", "tooltip.motion.initial");
            _animate = VariantField("motionInspector.animate", "tooltip.motion.animate");
            _exit = VariantField("motionInspector.exit", "tooltip.motion.exit");
            _hover = VariantField("motionInspector.hover", "tooltip.motion.hover");
            _pressed = VariantField("motionInspector.pressed", "tooltip.motion.pressed");
            _focus = VariantField("motionInspector.focus", "tooltip.motion.focus");
            _openGraph = new Button(OpenGraph)
            {
                text = DesignerLocalization.T("button.openMotionGraph"),
                tooltip = DesignerLocalization.T("tooltip.motion.openGraph")
            };
            _openClipEditor = new Button(OpenClipEditor)
            {
                text = DesignerLocalization.T("button.openMotionClipEditor"),
                tooltip = DesignerLocalization.T("tooltip.motion.openClipEditor")
            };
            _noPresetHelp = new Label(DesignerLocalization.T("motionInspector.noPreset"))
            {
                tooltip = DesignerLocalization.T("tooltip.motion.preset")
            };
            _noPresetHelp.AddToClassList("nexui-motion-empty-help");
            _entryClip = new ObjectField(DesignerLocalization.T("motion.entryClip"))
            {
                objectType = typeof(UIMotionClip), allowSceneObjects = false,
                tooltip = DesignerLocalization.T("motionInspector.entryClipTooltip")
            };
            _exitClip = new ObjectField(DesignerLocalization.T("motion.exitClip"))
            {
                objectType = typeof(UIMotionClip), allowSceneObjects = false,
                tooltip = DesignerLocalization.T("motionInspector.exitClipTooltip")
            };
            _bindings = new Foldout { text = DesignerLocalization.T("motion.bindings"), value = true };
            _bindings.AddToClassList("nexui-motion-bindings");
            _addBinding = new Button(() =>
            {
                Context.AddMotionBinding(DesignerMotionTrigger.Click);
                RefreshBindings();
            })
            {
                text = "+  " + DesignerLocalization.T("motion.addBinding"),
                tooltip = DesignerLocalization.T("motionInspector.addBindingTooltip")
            };
            _addBinding.AddToClassList("nexui-motion-add-binding");

            var overview = BuildOverview(out _overviewTitle, out _overviewSubtitle, out _overviewBadge);
            Add(overview);

            var previewCard = new VisualElement();
            previewCard.AddToClassList("nexui-motion-preview-card");
            var previewHeader = new VisualElement();
            previewHeader.AddToClassList("nexui-motion-preview-header");
            var previewCopy = new VisualElement { pickingMode = PickingMode.Ignore };
            previewCopy.AddToClassList("nexui-motion-preview-copy");
            _previewTitle = new Label(DesignerLocalization.T("motionInspector.previewTitle"));
            _previewTitle.AddToClassList("nexui-motion-preview-title");
            previewCopy.Add(_previewTitle);
            _previewDescription = new Label(DesignerLocalization.T("motionInspector.previewDescription"));
            _previewDescription.AddToClassList("nexui-motion-preview-description");
            previewCopy.Add(_previewDescription);
            previewHeader.Add(previewCopy);
            var replay = new Button(() => _preview.Restart())
            {
                text = "▶",
                tooltip = DesignerLocalization.T("motionInspector.replayTooltip")
            };
            replay.AddToClassList("nexui-motion-replay");
            previewHeader.Add(replay);
            previewCard.Add(previewHeader);
            _preview = new MotionLiveExamplePreview();
            previewCard.Add(_preview);
            Add(previewCard);

            var sectionTabs = new VisualElement
            {
                tooltip = DesignerLocalization.T("motionInspector.sectionTabsTooltip")
            };
            sectionTabs.AddToClassList("nexui-motion-section-tabs");
            sectionTabs.Add(MotionSectionButton(MotionSection.Settings, "⚙", "motionInspector.tabSettings"));
            sectionTabs.Add(MotionSectionButton(MotionSection.Examples, "◇", "motionInspector.tabExamples"));
            sectionTabs.Add(MotionSectionButton(MotionSection.Triggers, "⌁", "motionInspector.tabTriggers"));
            Add(sectionTabs);

            var settingsPage = MotionSectionPage(MotionSection.Settings);

            var presetCard = new VisualElement();
            presetCard.AddToClassList("nexui-motion-authoring-card");
            presetCard.Add(SectionHeading("motionInspector.elementMotion", "motionInspector.elementMotionDescription"));
            presetCard.Add(_preset);
            presetCard.Add(_motionId);
            var graphActions = new VisualElement();
            graphActions.AddToClassList("nexui-motion-action-row");
            _openGraph.AddToClassList("nexui-motion-action-primary");
            graphActions.Add(_openGraph);
            var previewAssigned = new Button(PreviewAssigned)
            {
                text = DesignerLocalization.T("motionInspector.previewAssigned"),
                tooltip = DesignerLocalization.T("motionInspector.previewAssignedTooltip")
            };
            previewAssigned.AddToClassList("nexui-motion-action-secondary");
            graphActions.Add(previewAssigned);
            presetCard.Add(graphActions);
            presetCard.Add(_noPresetHelp);

            var variants = new Foldout { text = DesignerLocalization.T("motionInspector.variants"), value = false };
            variants.AddToClassList("nexui-motion-variants");
            variants.tooltip = DesignerLocalization.T("motionInspector.variantsTooltip");
            variants.Add(_initial);
            variants.Add(_animate);
            variants.Add(_exit);
            variants.Add(_hover);
            variants.Add(_pressed);
            variants.Add(_focus);
            presetCard.Add(variants);
            settingsPage.Add(presetCard);

            var clipCard = new VisualElement();
            clipCard.AddToClassList("nexui-motion-authoring-card");
            clipCard.Add(SectionHeading("motionInspector.screenMotion", "motionInspector.screenMotionDescription"));
            clipCard.Add(BuildClipRow(_entryClip, false));
            clipCard.Add(BuildClipRow(_exitClip, true));
            _openClipEditor.AddToClassList("nexui-motion-action-secondary");
            clipCard.Add(_openClipEditor);
            settingsPage.Add(clipCard);
            Add(settingsPage);

            var examplesPage = MotionSectionPage(MotionSection.Examples);
            var exampleCard = new VisualElement();
            exampleCard.AddToClassList("nexui-motion-examples");
            exampleCard.Add(SectionHeading("motionInspector.examples", "motionInspector.examplesDescription"));
            var examples = new VisualElement();
            examples.AddToClassList("nexui-motion-example-grid");
            examples.Add(BuildExampleCard(DesignerTransitionPreset.Fade, "motionExample.fade", "motionExample.fadeDescription"));
            examples.Add(BuildExampleCard(DesignerTransitionPreset.SlideLeft, "motionExample.slide", "motionExample.slideDescription"));
            examples.Add(BuildExampleCard(DesignerTransitionPreset.ScalePop, "motionExample.scale", "motionExample.scaleDescription"));
            examples.Add(BuildExampleCard(DesignerTransitionPreset.Toast, "motionExample.toast", "motionExample.toastDescription"));
            exampleCard.Add(examples);
            _useExample = new Button(() => DesignerTransitionPresetWindow.Open(Context, _selectedExample))
            {
                text = DesignerLocalization.T("motionInspector.useExample"),
                tooltip = DesignerLocalization.T("motionInspector.useExampleTooltip")
            };
            _useExample.AddToClassList("nexui-motion-use-example");
            exampleCard.Add(_useExample);
            examplesPage.Add(exampleCard);
            Add(examplesPage);

            var triggersPage = MotionSectionPage(MotionSection.Triggers);
            var bindingCard = new VisualElement();
            bindingCard.AddToClassList("nexui-motion-authoring-card");
            bindingCard.Add(SectionHeading("motionInspector.triggerMotion", "motionInspector.triggerMotionDescription"));
            bindingCard.Add(_bindings);
            bindingCard.Add(_addBinding);
            triggersPage.Add(bindingCard);
            Add(triggersPage);
            SelectMotionSection(ReadMotionSectionPreference(), false);

            RegisterCallbacks();
            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add<DesignerElementMetadata>(h => context.MetadataSelectionChanged += h, h => context.MetadataSelectionChanged -= h, _ => Refresh());
            subscriptions.Add(h => context.CanvasChanged += h, h => context.CanvasChanged -= h, Refresh);
            Refresh();
        }

        private Button MotionSectionButton(MotionSection section, string icon, string localizationKey)
        {
            var captured = section;
            var button = new Button(() => SelectMotionSection(captured))
            {
                text = icon + "  " + DesignerLocalization.T(localizationKey),
                userData = captured,
                tooltip = DesignerLocalization.T(localizationKey + "Tooltip")
            };
            button.AddToClassList("nexui-motion-section-tab");
            _sectionButtons.Add(section, button);
            return button;
        }

        private VisualElement MotionSectionPage(MotionSection section)
        {
            var page = new VisualElement();
            page.AddToClassList("nexui-motion-section-page");
            page.AddToClassList("section-" + section.ToString().ToLowerInvariant());
            _sectionPages.Add(section, page);
            return page;
        }

        private void SelectMotionSection(MotionSection section, bool persist = true)
        {
            foreach (var pair in _sectionButtons)
                pair.Value.EnableInClassList("is-selected", pair.Key == section);
            foreach (var pair in _sectionPages)
                pair.Value.style.display = pair.Key == section ? DisplayStyle.Flex : DisplayStyle.None;
            if (persist) EditorPrefs.SetInt(MotionSectionPrefKey, (int)section);
        }

        private static MotionSection ReadMotionSectionPreference()
        {
            var value = EditorPrefs.GetInt(MotionSectionPrefKey, (int)MotionSection.Settings);
            return Enum.IsDefined(typeof(MotionSection), value) ? (MotionSection)value : MotionSection.Settings;
        }

        private static TextField VariantField(string labelKey, string tooltipKey)
            => new TextField(DesignerLocalization.T(labelKey)) { tooltip = DesignerLocalization.T(tooltipKey) };

        private static VisualElement BuildOverview(out Label title, out Label subtitle, out Label badge)
        {
            var overview = new VisualElement();
            overview.AddToClassList("nexui-motion-overview");
            var icon = new Label("◆") { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("nexui-motion-overview-icon");
            overview.Add(icon);
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("nexui-motion-overview-copy");
            title = new Label();
            title.AddToClassList("nexui-motion-overview-title");
            copy.Add(title);
            subtitle = new Label();
            subtitle.AddToClassList("nexui-motion-overview-subtitle");
            copy.Add(subtitle);
            overview.Add(copy);
            badge = new Label();
            badge.AddToClassList("nexui-motion-status-badge");
            overview.Add(badge);
            return overview;
        }

        private static VisualElement SectionHeading(string titleKey, string descriptionKey)
        {
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("nexui-motion-section-heading");
            var title = new Label(DesignerLocalization.T(titleKey));
            title.AddToClassList("nexui-motion-section-title");
            header.Add(title);
            var description = new Label(DesignerLocalization.T(descriptionKey));
            description.AddToClassList("nexui-motion-section-description");
            header.Add(description);
            return header;
        }

        private VisualElement BuildClipRow(ObjectField field, bool close)
        {
            var row = new VisualElement();
            row.AddToClassList("nexui-motion-clip-row");
            row.Add(field);
            var preview = new Button(() =>
            {
                var clip = field.value as UIMotionClip;
                if (clip != null) DesignerTransitionPresetService.Preview(Context, clip);
            })
            {
                text = "▶",
                tooltip = DesignerLocalization.T(close ? "motionInspector.previewExitClip" : "motionInspector.previewEntryClip")
            };
            preview.AddToClassList("nexui-motion-clip-preview");
            row.Add(preview);
            return row;
        }

        private Button BuildExampleCard(DesignerTransitionPreset preset, string titleKey, string descriptionKey)
        {
            var captured = preset;
            var button = new Button(() => SelectExample(captured, titleKey, descriptionKey))
            {
                text = string.Empty,
                tooltip = DesignerLocalization.T(descriptionKey) + "\n\n" + DesignerLocalization.T("motionInspector.exampleTooltip")
            };
            button.AddToClassList("nexui-motion-example-card");
            button.userData = captured;
            button.EnableInClassList("is-selected", captured == _selectedExample);
            var thumbnail = new MotionExampleThumbnail(preset) { pickingMode = PickingMode.Ignore };
            button.Add(thumbnail);
            var title = new Label(DesignerLocalization.T(titleKey)) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("nexui-motion-example-title");
            button.Add(title);
            var description = new Label(DesignerLocalization.T(descriptionKey)) { pickingMode = PickingMode.Ignore };
            description.AddToClassList("nexui-motion-example-description");
            button.Add(description);
            return button;
        }

        private void RegisterCallbacks()
        {
            _preset.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                var preset = evt.newValue as UIMotionPreset;
                Context.UpdateSelectedElement(e =>
                {
                    e.motion.motionPreset = preset;
                    if (preset != null && !string.IsNullOrEmpty(preset.motionId)) e.motion.motionId = preset.motionId;
                }, "Assign NexUI Element Motion Preset");
                _showingExample = false;
                Refresh();
            });
            _motionId.RegisterValueChangedCallback(evt => Change(e => e.motion.motionId = evt.newValue, "Edit NexUI Motion Id"));
            _initial.RegisterValueChangedCallback(evt => ChangeAndPreview(e => e.motion.initialVariant = evt.newValue, "Edit NexUI Initial Variant"));
            _animate.RegisterValueChangedCallback(evt => ChangeAndPreview(e => e.motion.animateVariant = evt.newValue, "Edit NexUI Animate Variant"));
            _exit.RegisterValueChangedCallback(evt => Change(e => e.motion.exitVariant = evt.newValue, "Edit NexUI Exit Variant"));
            _hover.RegisterValueChangedCallback(evt => Change(e => e.motion.hoverVariant = evt.newValue, "Edit NexUI Hover Variant"));
            _pressed.RegisterValueChangedCallback(evt => Change(e => e.motion.pressedVariant = evt.newValue, "Edit NexUI Pressed Variant"));
            _focus.RegisterValueChangedCallback(evt => Change(e => e.motion.focusVariant = evt.newValue, "Edit NexUI Focus Variant"));
            _entryClip.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.UpdateScreenMotion(m => m.entryClip = evt.newValue as UIMotionClip, "Assign NexUI Screen Enter Clip");
            });
            _exitClip.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing) return;
                Context.UpdateScreenMotion(m => m.exitClip = evt.newValue as UIMotionClip, "Assign NexUI Screen Exit Clip");
            });
        }

        private void Change(Action<DesignerElementMetadata> change, string undoName)
        {
            if (_refreshing) return;
            Context.UpdateSelectedElement(change, undoName);
        }

        private void ChangeAndPreview(Action<DesignerElementMetadata> change, string undoName)
        {
            Change(change, undoName);
            _showingExample = false;
            RefreshAssignedPreview();
        }

        private void OpenGraph()
        {
            var preset = Context.SelectedMetadata?.motion.motionPreset;
            if (preset != null) MotionGraphWindow.Open(preset);
        }

        private void OpenClipEditor()
        {
            var elementId = Context.SelectedMetadata?.elementId;
            if (!string.IsNullOrEmpty(elementId)) MotionClipEditorWindow.Open(Context.PreviewSurface, elementId);
        }

        private void PreviewAssigned()
        {
            _showingExample = false;
            RefreshAssignedPreview();
            _preview.Restart();
        }

        private void SelectExample(DesignerTransitionPreset preset, string titleKey, string descriptionKey)
        {
            _selectedExample = preset;
            _showingExample = true;
            foreach (var card in this.Query<Button>(className: "nexui-motion-example-card").ToList())
                card.EnableInClassList("is-selected", card.userData is DesignerTransitionPreset candidate && candidate == preset);
            _previewTitle.text = DesignerLocalization.T(titleKey);
            _previewDescription.text = DesignerLocalization.T(descriptionKey);
            _preview.SetTimeline(MotionLiveExamplePreview.ExampleTimeline(preset));
            _preview.Restart();
            _useExample.text = string.Format(DesignerLocalization.T("motionInspector.useNamedExample"), DesignerLocalization.T(titleKey));
        }

        private void Refresh()
        {
            _refreshing = true;
            var selected = Context.SelectedMetadata;
            SetEnabled(selected != null);
            if (selected != null)
            {
                var motion = selected.motion ?? new DesignerMotionMetadata();
                _preset.SetValueWithoutNotify(motion.motionPreset);
                _motionId.SetValueWithoutNotify(motion.motionId);
                _initial.SetValueWithoutNotify(motion.initialVariant);
                _animate.SetValueWithoutNotify(motion.animateVariant);
                _exit.SetValueWithoutNotify(motion.exitVariant);
                _hover.SetValueWithoutNotify(motion.hoverVariant);
                _pressed.SetValueWithoutNotify(motion.pressedVariant);
                _focus.SetValueWithoutNotify(motion.focusVariant);
            }
            var screenMotion = Context.Metadata?.screenMotion;
            _entryClip.SetValueWithoutNotify(screenMotion?.entryClip);
            _exitClip.SetValueWithoutNotify(screenMotion?.exitClip);
            _refreshing = false;

            RefreshOverview(selected);
            RefreshBindings();
            RefreshGraphButton();
            if (!_showingExample) RefreshAssignedPreview();
        }

        private void RefreshOverview(DesignerElementMetadata selected)
        {
            if (selected == null)
            {
                _overviewTitle.text = DesignerLocalization.T("motionInspector.noSelection");
                _overviewSubtitle.text = DesignerLocalization.T("motionInspector.noSelectionDescription");
                _overviewBadge.text = "—";
                return;
            }
            var preset = selected.motion?.motionPreset;
            _overviewTitle.text = string.Format(DesignerLocalization.T("motionInspector.target"),
                string.IsNullOrWhiteSpace(selected.displayName) ? selected.elementId : selected.displayName);
            if (preset == null)
            {
                _overviewSubtitle.text = DesignerLocalization.T("motionInspector.unassignedDescription");
                _overviewBadge.text = DesignerLocalization.T("motionInspector.unassigned");
                _overviewBadge.EnableInClassList("is-ready", false);
                return;
            }
            var timeline = CompileSelected(preset, selected.motion);
            _overviewSubtitle.text = string.Format(DesignerLocalization.T("motionInspector.motionSummary"),
                timeline.Tracks?.Length ?? 0, timeline.TotalDuration);
            _overviewBadge.text = DesignerLocalization.T("motionInspector.ready");
            _overviewBadge.EnableInClassList("is-ready", true);
        }

        private void RefreshAssignedPreview()
        {
            var selected = Context.SelectedMetadata;
            var preset = selected?.motion?.motionPreset;
            if (preset == null)
            {
                _previewTitle.text = DesignerLocalization.T("motionExample.fade");
                _previewDescription.text = DesignerLocalization.T("motionInspector.noPresetPreview");
                _preview.SetTimeline(MotionLiveExamplePreview.ExampleTimeline(DesignerTransitionPreset.Fade));
                return;
            }
            var variant = PreviewVariant(selected.motion, preset);
            _previewTitle.text = string.IsNullOrWhiteSpace(preset.motionId) ? preset.name : preset.motionId;
            _previewDescription.text = string.Format(DesignerLocalization.T("motionInspector.assignedPreviewDescription"),
                string.IsNullOrEmpty(variant) ? preset.defaultVariant : variant);
            _preview.SetTimeline(MotionCompiler.Compile(preset, variant));
        }

        private static UIMotionTimeline CompileSelected(UIMotionPreset preset, DesignerMotionMetadata motion)
            => MotionCompiler.Compile(preset, PreviewVariant(motion, preset));

        private static string PreviewVariant(DesignerMotionMetadata motion, UIMotionPreset preset)
        {
            if (!string.IsNullOrWhiteSpace(motion?.animateVariant)) return motion.animateVariant;
            if (!string.IsNullOrWhiteSpace(motion?.initialVariant)) return motion.initialVariant;
            return preset?.defaultVariant;
        }

        private void RefreshBindings()
        {
            _bindings.Clear();
            var motion = Context.Metadata?.screenMotion;
            var selectedId = Context.SelectedMetadata?.elementId;
            if (motion?.bindings == null || motion.bindings.Count == 0)
            {
                var empty = new Label(DesignerLocalization.T("motionInspector.noBindings"));
                empty.AddToClassList("nexui-motion-binding-empty");
                _bindings.Add(empty);
                return;
            }

            var count = 0;
            foreach (var binding in motion.bindings)
            {
                if (binding == null || (!string.IsNullOrEmpty(binding.targetElementId) && binding.targetElementId != selectedId)) continue;
                count++;
                var captured = binding;
                var card = new VisualElement
                {
                    tooltip = DesignerLocalization.T("motionInspector.bindingTooltip")
                };
                card.AddToClassList("nexui-motion-binding");
                var trigger = new EnumField(DesignerLocalization.T("motionInspector.trigger"), captured.trigger);
                var clip = new ObjectField(DesignerLocalization.T("motion.clip")) { objectType = typeof(UIMotionClip), allowSceneObjects = false, value = captured.clip };
                var reduced = new ObjectField(DesignerLocalization.T("motion.reducedClip")) { objectType = typeof(UIMotionClip), allowSceneObjects = false, value = captured.reducedMotionClip };
                var state = new TextField(DesignerLocalization.T("motion.stateId")) { value = captured.stateId };
                var command = new TextField(DesignerLocalization.T("motion.commandId")) { value = captured.commandId };
                var remove = new Button(() => { Context.RemoveMotionBinding(captured); RefreshBindings(); })
                {
                    text = "×", tooltip = DesignerLocalization.T("motion.removeBinding")
                };
                remove.AddToClassList("nexui-motion-binding-remove");
                trigger.RegisterValueChangedCallback(evt => Context.UpdateMotionBinding(captured, b => b.trigger = (DesignerMotionTrigger)evt.newValue));
                clip.RegisterValueChangedCallback(evt => Context.UpdateMotionBinding(captured, b => b.clip = evt.newValue as UIMotionClip));
                reduced.RegisterValueChangedCallback(evt => Context.UpdateMotionBinding(captured, b => b.reducedMotionClip = evt.newValue as UIMotionClip));
                state.RegisterValueChangedCallback(evt => Context.UpdateMotionBinding(captured, b => b.stateId = evt.newValue));
                command.RegisterValueChangedCallback(evt => Context.UpdateMotionBinding(captured, b => b.commandId = evt.newValue));
                card.Add(trigger);
                card.Add(clip);
                card.Add(reduced);
                card.Add(state);
                card.Add(command);
                card.Add(remove);
                _bindings.Add(card);
            }
            if (count == 0)
            {
                var empty = new Label(DesignerLocalization.T("motionInspector.noBindingsForTarget"));
                empty.AddToClassList("nexui-motion-binding-empty");
                _bindings.Add(empty);
            }
        }

        private void RefreshGraphButton()
        {
            var hasPreset = Context.SelectedMetadata?.motion?.motionPreset != null;
            _openGraph.SetEnabled(hasPreset);
            _openGraph.style.display = hasPreset ? DisplayStyle.Flex : DisplayStyle.None;
            _noPresetHelp.style.display = hasPreset ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>Animated inspector sample driven by a compiled motion timeline.</summary>
    internal sealed class MotionLiveExamplePreview : VisualElement
    {
        private readonly VisualElement _stage;
        private readonly VisualElement _ghost;
        private readonly VisualElement _target;
        private readonly VisualElement _sample;
        private readonly MotionExamplePathVisual _path;
        private UIMotionTimeline _timeline = UIMotionTimeline.Empty;
        private double _startedAt;

        public MotionLiveExamplePreview()
        {
            AddToClassList("nexui-motion-live-preview");
            _stage = new VisualElement();
            _stage.AddToClassList("nexui-motion-example-stage");
            _path = new MotionExamplePathVisual { pickingMode = PickingMode.Ignore };
            _stage.Add(_path);
            _ghost = Sample("START", "nexui-motion-example-ghost");
            _target = Sample(string.Empty, "nexui-motion-example-target");
            _sample = Sample("UI", "nexui-motion-example-object");
            _stage.Add(_ghost);
            _stage.Add(_target);
            _stage.Add(_sample);
            Add(_stage);
            _stage.RegisterCallback<GeometryChangedEvent>(_ => ApplyAt(0f));
            schedule.Execute(Tick).Every(16);
            SetTimeline(ExampleTimeline(DesignerTransitionPreset.Fade));
        }

        private static VisualElement Sample(string text, string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            if (!string.IsNullOrEmpty(text))
            {
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                element.Add(label);
            }
            return element;
        }

        public void SetTimeline(UIMotionTimeline timeline)
        {
            _timeline = timeline ?? UIMotionTimeline.Empty;
            _path.SetEndpoints(PositionAt(0f), PositionAt(Mathf.Max(_timeline.TotalDuration, 0.001f)));
            Restart();
        }

        public void Restart()
        {
            _startedAt = EditorApplication.timeSinceStartup;
            ApplyAt(0f);
        }

        private void Tick()
        {
            if (panel == null) return;
            var duration = Mathf.Max(0.15f, _timeline.TotalDuration);
            var cycle = duration + 0.65f;
            var elapsed = (float)(EditorApplication.timeSinceStartup - _startedAt);
            var within = Mathf.Repeat(elapsed, cycle);
            ApplyAt(Mathf.Min(within, duration));
        }

        private void ApplyAt(float time)
        {
            if (_stage.contentRect.width <= 1f) return;
            var duration = Mathf.Max(0.15f, _timeline.TotalDuration);
            var start = PositionAt(0f);
            var end = PositionAt(duration);
            var current = PositionAt(time);
            ApplyTransform(_ghost, start, 0f, true);
            ApplyTransform(_target, end, duration, true);
            ApplyTransform(_sample, current, time, false);
            _path.Progress = Mathf.Clamp01(time / duration);
        }

        private Vector2 PositionAt(float time)
            => new Vector2(ValueAt(UIMotionProperty.PositionX, time, 0f), ValueAt(UIMotionProperty.PositionY, time, 0f));

        private void ApplyTransform(VisualElement element, Vector2 offset, float time, bool marker)
        {
            var width = _stage.contentRect.width;
            var height = _stage.contentRect.height;
            var baseX = width * 0.5f - 24f;
            var baseY = height * 0.48f - 18f;
            element.style.left = baseX + Mathf.Clamp(offset.x, -width * 0.36f, width * 0.36f);
            element.style.top = baseY + Mathf.Clamp(offset.y, -height * 0.28f, height * 0.28f);
            var scaleX = ValueAt(UIMotionProperty.ScaleX, time, 1f);
            var scaleY = ValueAt(UIMotionProperty.ScaleY, time, 1f);
            element.style.scale = new Scale(new Vector3(scaleX, scaleY, 1f));
            element.style.rotate = new Rotate(new Angle(ValueAt(UIMotionProperty.Rotation, time, 0f), AngleUnit.Degree));
            if (!marker) element.style.opacity = Mathf.Clamp01(ValueAt(UIMotionProperty.Opacity, time, 1f));
        }

        private float ValueAt(UIMotionProperty property, float time, float fallback)
        {
            var found = false;
            var value = fallback;
            foreach (var track in _timeline.Tracks ?? Array.Empty<UIMotionTrack>())
            {
                if (track == null || track.Property != property) continue;
                if (!found || time >= track.Delay)
                {
                    value = Evaluate(track, time);
                    found = true;
                }
            }
            return value;
        }

        internal static float Evaluate(UIMotionTrack track, float time)
        {
            if (track?.Keyframes == null || track.Keyframes.Length == 0) return 0f;
            var from = track.Keyframes[0].Value;
            var to = track.Keyframes[track.Keyframes.Length - 1].Value;
            var normalized = Mathf.Clamp01(Mathf.InverseLerp(track.Delay, track.Delay + Mathf.Max(track.Duration, 0.0001f), time));
            var eased = UIMotionClipEvaluator.Ease(track.Easing, normalized);
            return Mathf.LerpUnclamped(from, to, eased);
        }

        public static UIMotionTimeline ExampleTimeline(DesignerTransitionPreset preset)
        {
            var tracks = new List<UIMotionTrack>();
            switch (preset)
            {
                case DesignerTransitionPreset.SlideLeft:
                    tracks.Add(Track(UIMotionProperty.PositionX, -58f, 0f, 0.65f, UIMotionEasing.EaseOutCubic));
                    tracks.Add(Track(UIMotionProperty.Opacity, 0.15f, 1f, 0.45f, UIMotionEasing.EaseOutCubic));
                    break;
                case DesignerTransitionPreset.ScalePop:
                    tracks.Add(Track(UIMotionProperty.ScaleX, 0.68f, 1f, 0.62f, UIMotionEasing.EaseOutBack));
                    tracks.Add(Track(UIMotionProperty.ScaleY, 0.68f, 1f, 0.62f, UIMotionEasing.EaseOutBack));
                    tracks.Add(Track(UIMotionProperty.Opacity, 0f, 1f, 0.34f, UIMotionEasing.EaseOutQuad));
                    break;
                case DesignerTransitionPreset.Toast:
                    tracks.Add(Track(UIMotionProperty.PositionY, 38f, 0f, 0.68f, UIMotionEasing.EaseOutBack));
                    tracks.Add(Track(UIMotionProperty.Opacity, 0.1f, 1f, 0.42f, UIMotionEasing.EaseOutCubic));
                    break;
                default:
                    tracks.Add(Track(UIMotionProperty.Opacity, 0.08f, 1f, 0.72f, UIMotionEasing.EaseInOutSine));
                    break;
            }
            return new UIMotionTimeline { MotionId = preset.ToString(), Tracks = tracks.ToArray() };
        }

        private static UIMotionTrack Track(UIMotionProperty property, float from, float to, float duration, UIMotionEasing easing)
            => new UIMotionTrack
            {
                Property = property,
                Easing = easing,
                Duration = duration,
                Keyframes = new[] { new UIMotionKeyframe(0f, from), new UIMotionKeyframe(1f, to) }
            };
    }

    internal sealed class MotionExamplePathVisual : VisualElement
    {
        private Vector2 _start;
        private Vector2 _end;
        private float _progress;

        public float Progress
        {
            set { _progress = value; MarkDirtyRepaint(); }
        }

        public MotionExamplePathVisual()
        {
            AddToClassList("nexui-motion-example-path");
            generateVisualContent += Draw;
        }

        public void SetEndpoints(Vector2 start, Vector2 end)
        {
            _start = start;
            _end = end;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f) return;
            var center = new Vector2(rect.width * 0.5f, rect.height * 0.48f);
            var a = center + new Vector2(Mathf.Clamp(_start.x, -rect.width * 0.36f, rect.width * 0.36f),
                Mathf.Clamp(_start.y, -rect.height * 0.28f, rect.height * 0.28f));
            var b = center + new Vector2(Mathf.Clamp(_end.x, -rect.width * 0.36f, rect.width * 0.36f),
                Mathf.Clamp(_end.y, -rect.height * 0.28f, rect.height * 0.28f));
            if ((b - a).sqrMagnitude < 9f)
            {
                a = new Vector2(22f, rect.height - 16f);
                b = new Vector2(rect.width - 22f, rect.height - 16f);
            }

            var painter = context.painter2D;
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.43f, 0.67f, 1f, 0.55f);
            painter.BeginPath();
            painter.MoveTo(a);
            painter.BezierCurveTo(Vector2.Lerp(a, b, 0.33f) + Vector2.up * 10f,
                Vector2.Lerp(a, b, 0.66f) + Vector2.up * 10f, b);
            painter.Stroke();

            var point = Bezier(a, Vector2.Lerp(a, b, 0.33f) + Vector2.up * 10f,
                Vector2.Lerp(a, b, 0.66f) + Vector2.up * 10f, b, _progress);
            painter.fillColor = new Color(0.55f, 0.76f, 1f, 0.96f);
            painter.BeginPath();
            painter.Arc(point, 3.5f, 0f, 360f);
            painter.Fill();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            var u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }

    internal sealed class MotionExampleThumbnail : VisualElement
    {
        private readonly DesignerTransitionPreset _preset;

        public MotionExampleThumbnail(DesignerTransitionPreset preset)
        {
            _preset = preset;
            AddToClassList("nexui-motion-example-thumbnail");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f) return;
            var painter = context.painter2D;
            var start = new Vector2(12f, rect.height * 0.55f);
            var end = new Vector2(rect.width - 16f, rect.height * 0.55f);
            if (_preset == DesignerTransitionPreset.Toast)
            {
                start = new Vector2(rect.width * 0.5f, rect.height - 8f);
                end = new Vector2(rect.width * 0.5f, 10f);
            }
            else if (_preset == DesignerTransitionPreset.Fade || _preset == DesignerTransitionPreset.ScalePop)
            {
                start = new Vector2(rect.width * 0.5f - 12f, rect.height * 0.55f);
                end = new Vector2(rect.width * 0.5f + 12f, rect.height * 0.55f);
            }
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.46f, 0.68f, 1f, 0.70f);
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
            painter.fillColor = new Color(0.35f, 0.57f, 0.92f, 0.35f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(end.x - 7f, end.y - 5f));
            painter.LineTo(new Vector2(end.x + 7f, end.y - 5f));
            painter.LineTo(new Vector2(end.x + 7f, end.y + 5f));
            painter.LineTo(new Vector2(end.x - 7f, end.y + 5f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
