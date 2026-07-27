using System.Collections.Generic;
using UnityEditor;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEngine;
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.Designer.Editor.Common;

namespace emiteat.NexUI.Designer.Editor.Responsive
{
    /// <summary>Full authoring UI for responsive rules (§5).</summary>
    public sealed class ResponsiveEditorWindow : NexUIToolWindow
    {
        [SerializeField] private DesignerMetadataAsset _asset;
        private readonly Dictionary<DesignerResponsiveMetadata, bool> _expanded = new();
        private List<string> _validation;

        protected override string TitleKey => "panel.responsive";
        protected override string TooltipKey => "tooltip.responsive";

        public static void Open() => GetWindow<ResponsiveEditorWindow>();

        protected override void DrawBody()
        {
            _asset = (DesignerMetadataAsset)EditorGUILayout.ObjectField(
                LC("panel.hierarchy"), _asset, typeof(DesignerMetadataAsset), false);
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(T("message.noScreenSelected"), MessageType.Info);
                return;
            }

            Section("panel.responsive");
            foreach (var r in new List<DesignerResponsiveMetadata>(_asset.responsiveRules))
                DrawRule(r);

            if (GUILayout.Button(LC("button.create", "tooltip.responsive"), GUILayout.Height(22)))
            {
                Undo.RecordObject(_asset, "Create Responsive Rule");
                ResponsiveService.Create(_asset, "Rule" + (_asset.responsiveRules.Count + 1));
                MarkDirty(_asset);
            }

            Section("panel.validation");
            if (GUILayout.Button(LC("toolbar.validate"), GUILayout.Width(120)))
                _validation = ResponsiveService.Validate(_asset);
            if (_validation != null)
                EditorGUILayout.HelpBox(_validation.Count == 0
                    ? T("message.validationPassed") : string.Join("\n", _validation),
                    _validation.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void DrawRule(DesignerResponsiveMetadata r)
        {
            _expanded.TryGetValue(r, out bool open);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _expanded[r] = EditorGUILayout.Foldout(open, r.ruleId, true);
            if (GUILayout.Button(LC("button.duplicate"), GUILayout.Width(70)))
            {
                Undo.RecordObject(_asset, "Duplicate Rule");
                ResponsiveService.Duplicate(_asset, r); MarkDirty(_asset);
            }
            if (GUILayout.Button(LC("button.delete"), GUILayout.Width(60)))
            {
                Undo.RecordObject(_asset, "Delete Rule");
                ResponsiveService.Delete(_asset, r); MarkDirty(_asset);
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return;
            }
            EditorGUILayout.EndHorizontal();

            if (_expanded[r])
            {
                EditorGUI.indentLevel++;
                r.ruleId = EditorGUILayout.TextField("ruleId", r.ruleId);
                r.minResolution = EditorGUILayout.Vector2IntField("min", r.minResolution);
                r.maxResolution = EditorGUILayout.Vector2IntField("max", r.maxResolution);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("presets", GUILayout.Width(60));
                foreach (var p in ResponsiveService.Presets)
                    if (GUILayout.Button(p.label, EditorStyles.miniButton))
                    {
                        r.minResolution = p.min; r.maxResolution = p.max; MarkDirty(_asset);
                    }
                EditorGUILayout.EndHorizontal();

                r.constrainInputMode = EditorGUILayout.Toggle("constrain input", r.constrainInputMode);
                using (new EditorGUI.DisabledScope(!r.constrainInputMode))
                    r.inputMode = (UIInputMode)EditorGUILayout.EnumPopup("inputMode", r.inputMode);

                EditorGUILayout.LabelField("Overrides", EditorStyles.miniBoldLabel);
                for (int j = 0; j < r.overrides.Count; j++)
                {
                    var o = r.overrides[j];
                    EditorGUILayout.BeginHorizontal();
                    o.elementId = EditorGUILayout.TextField(o.elementId);
                    o.propertyId = (DesignerPropertyId)EditorGUILayout.EnumPopup(o.propertyId, GUILayout.Width(140));
                    if (o.propertyId == DesignerPropertyId.None)
                        o.propertyPath = EditorGUILayout.TextField(o.propertyPath);
                    else
                        o.propertyPath = DesignerPropertyRegistry.PathFor(o.propertyId);
                    o.value = EditorGUILayout.TextField(DesignerPropertyRegistry.EffectiveValue(o.propertyId, o.typedValue, o.value));
                    if (o.propertyId != DesignerPropertyId.None)
                        o.typedValue = DesignerPropertyRegistry.Parse(o.propertyId, o.value);
                    if (GUILayout.Button("×", GUILayout.Width(22))) { r.overrides.RemoveAt(j); MarkDirty(_asset); EditorGUILayout.EndHorizontal(); break; }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("+ override", GUILayout.Width(90)))
                {
                    r.overrides.Add(new DesignerResponsiveOverrideMetadata()); MarkDirty(_asset);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }
    }
}
