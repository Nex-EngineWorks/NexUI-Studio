# Changelog

## [Unreleased]

### Commercial readiness
- Added Setup Doctor for dependency, project asset, scene backend and writable-path checks.
- Consolidated screen creation under `Tools/NexUI/Designer` and grouped beta graph tools as experimental utilities.
- Added explicit Loaded/Unsaved/Saved and validation state to the Designer toolbar.
- Removed placeholder Assets and Timeline tabs; Unity's Project window and the Motion Clip Editor are now the single entry points.
- Added package manifest documentation links, install-order guidance and a release readiness checklist.

### Productivity
- Added a Korean Screen Creation Wizard that creates connected Screen, Metadata, uGUI Prefab or UXML/USS assets with overwrite protection and rollback.
- Added Motion Clip-based Open/Close transition presets, direct preview, reverse generation and stagger ordering.
- Extended Preview Scenario values with Sprite/List data, scenario navigation/duplicate/reset/delete, and quick Text/Value/Collection edge-case presets.
- Added layout inference, Auto Layout conversion, anchor recommendations, nested-layout warnings and grouped Undo.
- Added actionable Validation fixes for metadata geometry/hierarchy and common uGUI raycast, CanvasGroup and Button issues.
- Completed AnimationClip import/export for supported RectTransform, Transform and CanvasGroup curves, including Editor and Assets menu actions.
- Added Grid Auto Layout serialization for uGUI and UI Toolkit, including column/cell metadata and generated USS wrapping.
- Completed Sprite/List Scenario Timeline editing and preview context capture for resolution, input device and theme.
- Added live Preview Snapshot capture/diff and a configurable Designer shortcut settings window.
- Added first-frame Figma import for hierarchy, coordinates, text, solid fills and Auto Layout with Undo.
- Added `DesignerMotionTriggerRuntime` for backend-neutral Click/Pointer/Focus subscription, lifecycle dispatch, Reduced Motion selection and deterministic disposal.

### Documentation
- Expanded Korean onboarding, workflow, Scenario, Motion and troubleshooting guides.
- Added Backend support, asset ownership, validation catalog, compatibility and metadata schema references.
- Fixed outdated documentation links, menu paths and installation guidance.
- Reorganized Korean documentation into Getting Started, User Guide, Motion, Advanced, Tutorials, Reference, and Developer sections.
- Separated current implementation status from the long-term feature specification.
- Added verified Backend, Figma, Migration, Runtime Debug, shortcut, limitation, troubleshooting, extension, and serialization guidance.
- Kept short redirect documents at externally referenced legacy paths.

### Stabilized
- Added a focus-aware `DesignerSessionRegistry` and removed satellite-window context discovery through `Resources.FindObjectsOfTypeAll`.
- Added panel-lifetime event subscriptions so rebuilt/closed VisualElements do not accumulate Context callbacks.
- Persisted screen and element Motion Clip bindings, Reduced Motion alternatives, Motion State Machine and Motion Graph references in Designer metadata.
- Added Motion binding Undo/Redo, element-id reference migration, save synchronization and validation for missing targets/clips and invalid keyframes.
- Added dirty-state handling, Ctrl+S and Undo/Redo preview refresh.
- Restored recent screen, metadata, valid selection and canvas scroll state by asset GUID after reload.
- Avoided constructing `RectOffset` while Unity is serializing metadata during domain reload.
- Added a transactional generated-asset writer for UXML/USS with validation, marker protection, dry run, VCS checkout, rollback and targeted imports.
- Added Session, lifecycle, Motion persistence, Undo consistency, generated-writer and sample smoke EditMode tests plus GitHub Actions EditMode/PlayMode workflow.
- Updated Korean documentation, architecture, implementation status, installation and testing guides.

### Added
- **Motion Clip Editor**: new standalone `Tools/NexUI/Designer/Motion Clip Editor` window for
  authoring multi-element, multi-property, keyframe-based `UIMotionClip` assets, with a Designer
  selection-linked entry point ("Open Motion Clip Editor") from the Motion inspector. Includes a
  minimal timeline view (draggable keyframes), live preview against the Designer's preview
  surface, and Play/Stop. See `Documentation~/motion/motion-clip-editor.md`.
- `UnityAnimationClipAdapter` (preview an existing `AnimationClip` via `SampleAnimation`) and
  implemented `UIMotionClipImporter`/`UIMotionClipExporter` conversion services.
- Motion Graph Editor: `Tools/NexUI/Designer/Motion Graph` menu entry so it can be opened
  standalone (with its own Preset picker) instead of only from the Motion inspector; new
  documentation (`Documentation~/motion/motion-graph-editor.md`, previously undocumented); "Auto
  Layout" and "Duplicate Node" context menu actions; brand-new (empty) graphs are now seeded
  with a connected `start`/`end` node pair.
- Shared IMGUI chrome for all `NexUIToolWindow`-based satellite tool windows (header band,
  accent section headers, status badges) driven by an expanded `DesignerColors` token set, so
  their look now tracks the main Designer's dark UI Toolkit theme instead of default Editor
  styling.

### Fixed
- Main Designer window's bottom panel (`State`/`Command`/`Screen Graph` cards) was clipped at a
  fixed 34px/28px height that didn't fit its own content; increased to 64px/56px.
- `MotionGraphWindow` (Motion Graph popout) now applies the shared `NexUIDesigner.uss`
  stylesheet and button classes, matching the rest of the Designer.

### Known limitations
- Motion Clip `AnchoredPosition`/`LocalPosition` currently resolve to the same underlying value.
- Capability-backed Motion triggers subscribe automatically; screen/state/command/enable lifecycle owners call the explicit Binder API.
- AnimationClip conversion skips unsupported curves and exports GameObject paths for direct uGUI playback.
- Figma import does not provide asset image download, component variants or bidirectional sync.

## 0.1.0

- Initial NexUI Designer extension package.
- Added metadata assets, localized Editor window shell, backend abstraction, tools, inspectors, graph panels, serializers, and documentation.
