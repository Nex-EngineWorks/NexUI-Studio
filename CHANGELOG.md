# Changelog

## [Unreleased]

### Panes can be pulled out and docked anywhere
- Each region (Explorer, Inspector, Output) has a **⧉** button that opens it as its own `EditorWindow`. Rather than building a bespoke docking system, the pane becomes a normal editor window — so Unity's docking, tabbing, floating, multi-monitor placement and layout saving all apply for free, and the arrangement survives restarts and layout switches.
- Closing a detached window docks the pane back into the Designer, and the shell re-lays itself out around whatever is currently detached. The canvas is never detachable, since it is what the Designer window is.
- The canvas column instance is preserved across re-layouts, so rearranging panes does not throw away the viewport and its scroll/zoom/guides.
- Detached panes follow the active Designer window's context, so focusing a different screen re-points them instead of leaving a stale view.
- Added `Tools/NexUI/Panels/…` to open any panel directly, including Hierarchy, Library and Project Assets as extra windows, plus `Dock All Back Into Designer` as the way back from a layout the user has lost track of.

### Every pane is now labelled
- Added a one-line caption to each region of the Designer window (sidebar, canvas, inspector, output drawer). The five regions previously looked alike with nothing naming them, which is what made "where is this feature" a hunt.
- The caption states the pane name *and* what the current view is for, so it adds information rather than repeating the tab label directly underneath it: the sidebar reads `EXPLORER — Structure of the open screen` and changes with the active tab, and the canvas caption carries the open screen's id.
- Kept to a single 18px row, and hidden on the output drawer while it is collapsed so it never eats the one row left visible on purpose.

### Layers panel drag-and-drop actually works now
- **Dropping a row onto another row did nothing.** `CompleteDrag` cleared the dragged-element field *before* calling the legality check, and that check bails out when the field is null — so every into/before/after drop was silently refused and only drops on empty space (which skip the check) ever worked. The check now takes the dragged element as an argument.
- Drag initiation no longer depends on pointer capture. The rows live inside a `ScrollView`, and once it takes the pointer for its own scrolling the row stopped receiving move events, so a drag could fail to start at all. The panel now tracks the press and listens for moves itself.
- Rebuilding the row list is suppressed while a drag is live; it previously destroyed the rows the drop target was measured against.
- The "into" zone now owns the middle half of a row (was ~44%), since re-parenting is the main reason to drag here. Dropping into a collapsed container also expands it, so the moved element does not appear to vanish.

### Fixes found while building the above
- Smart guides never snapped one element's edge against its neighbour's opposite edge — only like-for-like edges and centres. Butting two elements together, the most common layout move, therefore did not snap at all. Added the two adjacency pairs on each axis.
- Several EditMode tests read grid size and snapping straight out of `EditorPrefs`, which are shared with whatever was last set in the Designer window. The suite's pass/fail set changed depending on the machine. `DesignerUIStateTests`, `DesignerUndoConsistencyTests` and `NexUIAIServiceTests` now pin known values and restore the user's settings afterwards.

### Canvas ergonomics (follow-up)
- Guides can now be **grabbed and moved**. Each one carries an 11px transparent grab band around its 1px line — a hair-line is effectively unhittable with a mouse, which is why guides were previously create-and-delete only. Hovering thickens the line so the target is visible, and dragging honours Grid Snap.
- Dragging a guide back onto the ruler removes it, alongside the existing `Alt`-click.
- Added **drag-to-reparent on the canvas**: drop an element onto a container and it becomes its child, the same gesture as dropping a row onto another row in Unity's Hierarchy. The target is outlined and named in a drop hint before release, the element keeps its on-screen position, and `Ctrl/Cmd` suppresses re-parenting for a plain move.
- Drop targets exclude the dragged elements and their descendants (which would detach a branch), hidden and locked elements, and component types that accept no children. The deepest container under the cursor wins, with draw order breaking ties.

### Canvas ergonomics
- Added rulers along the top and left of the canvas. Tick spacing adapts to zoom and always lands on round numbers (1/2/5 × 10ⁿ), and both rulers track the pointer so the current X/Y is readable while placing.
- Added drag-out guides: pull from a ruler to place one, `Alt`-click to delete one, click the ruler corner to clear all. Elements snap their edges and centre to guides, and guides take priority over element edges because a guide is an explicit decision.
- Added `Space`-drag and middle-mouse panning that keep the current tool and selection, so panning no longer means switching to the Hand tool and back.
- `Ctrl/Cmd`+wheel now zooms around the pointer instead of the canvas origin, so the element under the cursor stays put.
- Added a Transform Bar under the canvas toolbar with live, editable X/Y/W/H for the selection (label-drag scrubs). Nudging a rect no longer requires a round trip to the Inspector. Multi-selection shows the union read-only rather than pretending four fields can edit many rects.
- Guides are stored per metadata asset in `EditorPrefs`, like zoom and scroll — they stay out of Git diffs and need no schema migration.

### Documentation accuracy pass
- Corrected `developer/metadata-schema.md`, which still claimed `CurrentSchemaVersion` was 1 (it is 4), and documented what every migration step actually changes.
- Corrected `reference/backend-support-matrix.md`, `developer/serialization.md` and `reference/troubleshooting.md`, which still described uGUI saving as name-based matching. It has been stable-id-first with a name fallback since Phase 0.
- Corrected the claim that UI Toolkit save never rewrites UXML: it does regenerate `.uxml`/`.uss` when the target carries the generated marker.
- Fixed `developer/testing.md`, which showed `-runTests` combined with `-quit` — that can terminate the editor before results are written. Added a junction-based recipe for running tests while the project is open in the editor.
- Documented the 9 validation codes that existed in code but were missing from the catalog; the catalog now covers all 75. `developer/api-reference.md` no longer duplicates a partial list and points at the catalog instead.
- Rewrote `developer/project-structure.md` against the actual folder layout (it was missing 8 directories, including `Editor/Properties` and `Editor/Components/Definitions`).
- Marked `RepositoryArchitectureAudit.md` and `RiskReport.md` as point-in-time Phase 0 records so their stale findings are not read as current state.
- Added reusable components and the Assets panel to the concepts, terminology, feature status, known limitations, parity matrix, workflows, canvas, inspector and architecture docs.

### Assets panel
- Added a Project-window-style **Assets** tab to the Designer sidebar: folder navigation with breadcrumbs, recursive search, kind filtering (Image/Font/Material/Prefab/UXML/USS/Asset), thumbnails, click-to-ping and double-click-to-open. This replaces the placeholder tab that previously only had a "Show Project Assets" button.
- Added asset drag-and-drop onto the canvas: a sprite sets an element's image (or creates an Image element on empty canvas), a font or material assigns to the hovered element, and a component definition places an instance. Payloads with no defined behaviour are rejected rather than guessed at, and every drop is a single Undo step.
- Drops from Unity's own Project window work identically, since both paths use `UnityEditor.DragAndDrop`.
- The panel stays read-only by design — rename/move/delete/create remain the Project window's job, so their reference-fixup and `.meta` safety rules are not duplicated.

### Reusable components (Phase 3)
- Added `DesignerComponentDefinitionAsset`: a user-authored, versioned component with an element sub-tree, exposed properties, slots and variant axes.
- Added a component *instance* reference on every element (`DesignerElementMetadata.componentInstance`). Instances store a reference plus overrides — never a copy — so editing a definition updates every instance with no propagation pass.
- Added `DesignerComponentExpander`, which flattens instances for Preview, both backend serializers, Save Preview and Validation. The expansion is an in-memory throw-away asset and is never written back to authored data.
- Generated element ids are `{instanceId}--{definitionElementId}` and generated stableIds are derived deterministically from the instance and definition, so uGUI prefab objects reconnect across saves instead of being recreated.
- Added `DesignerPropertyApplier`, completing the Phase 1 typed-property model with apply/read against element metadata. Properties with no authored representation are reported, never silently ignored.
- Added `DesignerComponentService` for create-from-selection, instantiate, override set/reset, detach, swap and update-from-definition — all Undo-aware, with destructive operations reporting exactly what they drop.
- Added `DesignerComponentLibrary` (project index with search, categories, tags, favourites and usage lookup) and a Component Library window under `Tools/NexUI/Component Library`.
- Added a Component Instance Inspector section for variant selection, per-property override with Reset, and lifecycle actions.
- Added 19 component validation codes covering missing definitions, cycles, slot contracts, override resolution, variant contracts and version mismatch.
- Bumped metadata schema to v4. The v3 → v4 migration is additive and idempotent; no authored value changes.

### Commercial readiness
- Added an in-editor AI Assistant with session/environment API key handling, current-screen context, bounded action-plan validation, explicit approval, destructive-action confirmation, and single-step Undo.
- Replaced the split Design/Prototype/Motion Inspector with one searchable, foldout-based Inspector using workflow filters and Beginner/Pro progressive disclosure.
- Added a public Inspector section registry and compatibility wrapper so Inspector extensions share one rendering path.
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
- Added a Korean AI Assistant guide covering setup, privacy, supported actions, review/apply workflow, costs, limitations, and troubleshooting.
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
- **Motion Clip Editor**: new standalone `Tools/NexUI/Utilities > Motion Clip Editor` window for
  authoring multi-element, multi-property, keyframe-based `UIMotionClip` assets, with a Designer
  selection-linked entry point ("Open Motion Clip Editor") from the Motion inspector. Includes a
  minimal timeline view (draggable keyframes), live preview against the Designer's preview
  surface, and Play/Stop. See `Documentation~/motion/motion-clip-editor.md`.
- `UnityAnimationClipAdapter` (preview an existing `AnimationClip` via `SampleAnimation`) and
  implemented `UIMotionClipImporter`/`UIMotionClipExporter` conversion services.
- Motion Graph Editor: `Tools/NexUI/Utilities > Motion Graph` menu entry so it can be opened
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
