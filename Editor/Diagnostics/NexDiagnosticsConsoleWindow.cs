using System.Collections.Generic;
using System.IO;
using System.Linq;
using emiteat.NexUI.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Diagnostics
{
    /// <summary>
    /// One place to see every diagnostic NexUI has reported this session.
    /// </summary>
    /// <remarks>
    /// The console exists because a diagnostic that only ever appeared in the Unity console is
    /// gone the moment something else logs. Compile results, publish failures and runtime problems
    /// all reach here, keep their occurrence counts, and can be filtered down to the one screen
    /// somebody is actually working on.
    ///
    /// It shows the new <c>NEX-*</c> diagnostics only. Studio's authoring validation is a separate
    /// system with its own panel, and merging the two is a bigger job than putting a window on
    /// this one - doing it halfway would leave two consoles that each show some of the problems.
    /// </remarks>
    public sealed class NexDiagnosticsConsoleWindow : EditorWindow
    {
        private const string AllSubsystems = "All";
        private const string AllFeatures = "All features";

        private NexDiagnosticQuery _query = new NexDiagnosticQuery
        {
            MinSeverity = NexSeverity.Information,
            IncludeResolved = false
        };

        private ScrollView _list;
        private Label _summary;
        private string _subsystem = AllSubsystems;
        private string _screen = AllSubsystems;

        [MenuItem("Tools/NexUI/Diagnostics Console")]
        public static void Open()
        {
            var window = GetWindow<NexDiagnosticsConsoleWindow>();
            window.titleContent = new GUIContent("NexUI Diagnostics");
            window.minSize = new Vector2(520f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            BuildUI();
            NexDiagnosticSession.Log.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => NexDiagnosticSession.Log.Changed -= Refresh;

        // ---- layout ---------------------------------------------------------

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            rootVisualElement.Add(toolbar);

            var severity = new EnumField("Min severity", NexSeverity.Information);
            severity.RegisterValueChangedCallback(evt =>
            {
                _query.MinSeverity = (NexSeverity)evt.newValue;
                Refresh();
            });
            toolbar.Add(severity);

            var subsystems = new[] { AllSubsystems }.Concat(NexDiagnosticCodes.Subsystems()).ToList();
            var subsystem = new DropdownField("Subsystem", subsystems, 0);
            subsystem.RegisterValueChangedCallback(evt =>
            {
                _subsystem = evt.newValue;
                _query.Subsystem = evt.newValue == AllSubsystems ? null : evt.newValue;
                Refresh();
            });
            toolbar.Add(subsystem);

            // Feature is the first thing people filter by - "the save is broken" is a statement
            // about a feature, not about a subsystem - so it sits next to the severity.
            var features = new List<string> { AllFeatures };
            features.AddRange(NexDiagnosticSession.Log.Features());
            var feature = new DropdownField("Feature", features, 0);
            feature.RegisterValueChangedCallback(evt =>
            {
                _query.Feature = evt.newValue == AllFeatures ? null : evt.newValue;
                Refresh();
            });
            toolbar.Add(feature);

            var search = new TextField("Search")
            {
                tooltip = "Matches the code, the message, the location, or the origin -> handler route."
            };
            search.RegisterValueChangedCallback(evt =>
            {
                _query.Text = evt.newValue;
                Refresh();
            });
            toolbar.Add(search);

            var resolved = new Toggle("Show resolved") { value = false };
            resolved.RegisterValueChangedCallback(evt =>
            {
                _query.IncludeResolved = evt.newValue;
                Refresh();
            });
            toolbar.Add(resolved);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            buttons.Add(new Button(ExportJson) { text = "Export JSON" });
            buttons.Add(new Button(() => { NexDiagnosticSession.Log.Clear(); }) { text = "Clear" });
            rootVisualElement.Add(buttons);

            _summary = new Label();
            rootVisualElement.Add(_summary);

            _list = new ScrollView { style = { flexGrow = 1f } };
            rootVisualElement.Add(_list);
        }

        private void Refresh()
        {
            if (_list == null) return;

            _list.Clear();

            var log = NexDiagnosticSession.Log;
            var entries = log.Query(_query).ToList();

            _summary.text = entries.Count + " shown  ·  " +
                            log.CountAtLeast(NexSeverity.Error) + " unresolved error(s)  ·  " +
                            log.Count + " total";

            if (entries.Count == 0)
            {
                _list.Add(new Label("Nothing to show. Compile a screen, or widen the filters."));
                return;
            }

            // Grouped by feature rather than listed flat. Twelve rows from three features read as
            // twelve unrelated problems; under three headings they read as "the save worked, the
            // import did not", which is the first thing anyone wants to know.
            foreach (var group in entries.GroupBy(e => e.Feature))
            {
                var name = string.IsNullOrEmpty(group.Key) ? "Uncategorized" : group.Key;
                var worst = group.Max(e => e.Diagnostic.Severity);

                var header = new Foldout
                {
                    text = name + "  ·  " + group.Count() + " item(s)  ·  worst: " + worst,
                    value = worst >= NexSeverity.Error
                };
                header.AddToClassList("nexui-diagnostic-feature-folder");

                foreach (var entry in group) header.Add(BuildRow(entry));
                _list.Add(header);
            }
        }

        /// <summary>
        /// One diagnostic, collapsed. The header alone has to be enough to decide whether to open
        /// it, so it carries the severity, the code, the location and the repeat count.
        /// </summary>
        private VisualElement BuildRow(NexDiagnosticEntry entry)
        {
            var d = entry.Diagnostic;

            var title = d.Severity + "  " + d.Code;

            // The route goes in the collapsed header, not only in the detail. "Which thing raised
            // this?" is part of deciding whether to open the row at all.
            var route = entry.Route;
            if (!string.IsNullOrEmpty(route)) title += "  ·  " + route;
            if (!d.Location.IsNone) title += "  ·  " + d.Location;
            if (entry.Occurrences > 1) title += "  (×" + entry.Occurrences + ")";
            if (entry.Resolved) title += "  ✓";

            var foldout = new Foldout { text = title, value = false };
            foldout.style.marginBottom = 2f;

            foldout.Add(new Label(d.Message) { style = { whiteSpace = WhiteSpace.Normal } });

            var catalog = NexDiagnosticCodes.Find(d.Code);
            if (catalog != null && !string.IsNullOrEmpty(catalog.Resolution))
                foldout.Add(new Label("Fix: " + catalog.Resolution) { style = { whiteSpace = WhiteSpace.Normal } });

            var root = d.RootCause();
            if (!ReferenceEquals(root, d))
                foldout.Add(new Label("Root cause: " + root.Code + " — " + root.Message)
                {
                    style = { whiteSpace = WhiteSpace.Normal }
                });

            foldout.Add(new Label("First seen " + entry.FirstSeen.ToString("HH:mm:ss") +
                                  ", last " + entry.LastSeen.ToString("HH:mm:ss")));

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            actions.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = d.ToDetailedString())
            {
                text = "Copy details"
            });
            actions.Add(new Button(() =>
            {
                NexDiagnosticSession.Log.SetResolved(entry, !entry.Resolved);
            })
            {
                text = entry.Resolved ? "Mark unresolved" : "Mark resolved"
            });
            foldout.Add(actions);

            return foldout;
        }

        // ---- export ---------------------------------------------------------

        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("Export NexUI Diagnostics", "", "nexui-diagnostics.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                File.WriteAllText(path, NexDiagnosticSession.Log.ToJson(_query));
                Debug.Log("[NexUI] Diagnostics exported to " + path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NexUI] Could not write " + path + ": " + ex.Message);
            }
        }
    }
}
