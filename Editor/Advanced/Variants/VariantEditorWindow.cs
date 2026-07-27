using System.Collections.Generic;
using UnityEditor;
using emiteat.NexUI.Designer.Editor.Properties;
using UnityEngine;
using emiteat.NexUI.Designer.Editor.Common;

namespace emiteat.NexUI.Designer.Editor.Variants
{
    /// <summary>Full authoring UI for screen variants (§4).</summary>
    public sealed class VariantEditorWindow : NexUIToolWindow
    {
        [SerializeField] private DesignerMetadataAsset _asset;
        private readonly Dictionary<DesignerVariantMetadata, bool> _expanded = new();
        private List<string> _validationMessages;
        private List<string> _diffMessages;
        private int _diffA, _diffB;

        protected override string TitleKey => "panel.variants";
        protected override string TooltipKey => "tooltip.variants";

        public static void Open() => GetWindow<VariantEditorWindow>();

        protected override void DrawBody()
        {
            _asset = (DesignerMetadataAsset)EditorGUILayout.ObjectField(
                LC("panel.hierarchy"), _asset, typeof(DesignerMetadataAsset), false);

            if (_asset == null)
            {
                EditorGUILayout.HelpBox(T("message.noScreenSelected"), MessageType.Info);
                return;
            }

            Section("panel.variants");
            for (int i = 0; i < _asset.variants.Count; i++)
                DrawVariant(_asset.variants[i], i);

            EditorGUILayout.Space(4);
            if (GUILayout.Button(LC("button.create", "tooltip.variants"), GUILayout.Height(22)))
            {
                Undo.RecordObject(_asset, "Create Variant");
                VariantService.Create(_asset, "Variant" + (_asset.variants.Count + 1));
                MarkDirty(_asset);
            }

            DrawValidation();
            DrawDiff();
        }

        private void DrawVariant(DesignerVariantMetadata v, int index)
        {
            _expanded.TryGetValue(v, out bool open);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _expanded[v] = EditorGUILayout.Foldout(open, $"{index}. {v.variantId}", true);
            if (GUILayout.Button(LC("button.duplicate"), GUILayout.Width(70)))
            {
                Undo.RecordObject(_asset, "Duplicate Variant");
                VariantService.Duplicate(_asset, v);
                MarkDirty(_asset);
            }
            if (GUILayout.Button(LC("button.delete"), GUILayout.Width(60)))
            {
                Undo.RecordObject(_asset, "Delete Variant");
                VariantService.Delete(_asset, v);
                MarkDirty(_asset);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (_expanded[v])
            {
                EditorGUI.indentLevel++;
                v.variantId = EditorGUILayout.TextField("variantId", v.variantId);
                v.displayName = EditorGUILayout.TextField("displayName", v.displayName);
                v.isDefault = EditorGUILayout.Toggle("isDefault", v.isDefault);

                EditorGUILayout.LabelField("Overrides", EditorStyles.miniBoldLabel);
                for (int j = 0; j < v.overrides.Count; j++)
                {
                    var o = v.overrides[j];
                    EditorGUILayout.BeginHorizontal();
                    o.targetElementId = EditorGUILayout.TextField(o.targetElementId);
                    o.propertyId = (DesignerPropertyId)EditorGUILayout.EnumPopup(o.propertyId, GUILayout.Width(140));
                    if (o.propertyId == DesignerPropertyId.None)
                        o.propertyPath = EditorGUILayout.TextField(o.propertyPath);
                    else
                        o.propertyPath = DesignerPropertyRegistry.PathFor(o.propertyId);
                    o.value = EditorGUILayout.TextField(DesignerPropertyRegistry.EffectiveValue(o.propertyId, o.typedValue, o.value));
                    if (o.propertyId != DesignerPropertyId.None)
                        o.typedValue = DesignerPropertyRegistry.Parse(o.propertyId, o.value);
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                    {
                        v.overrides.RemoveAt(j);
                        MarkDirty(_asset);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("+ override", GUILayout.Width(90)))
                {
                    v.overrides.Add(new DesignerVariantOverrideMetadata());
                    MarkDirty(_asset);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawValidation()
        {
            Section("panel.validation");
            if (GUILayout.Button(LC("toolbar.validate"), GUILayout.Width(120)))
                _validationMessages = VariantService.Validate(_asset);

            if (_validationMessages != null)
            {
                if (_validationMessages.Count == 0)
                    EditorGUILayout.HelpBox(T("message.validationPassed"), MessageType.Info);
                else
                    EditorGUILayout.HelpBox(string.Join("\n", _validationMessages), MessageType.Warning);
            }
        }

        private void DrawDiff()
        {
            if (_asset.variants.Count < 2) return;
            Section("panel.diff");
            var names = new string[_asset.variants.Count];
            for (int i = 0; i < names.Length; i++) names[i] = _asset.variants[i].variantId;

            EditorGUILayout.BeginHorizontal();
            _diffA = EditorGUILayout.Popup(_diffA, names);
            _diffB = EditorGUILayout.Popup(_diffB, names);
            if (GUILayout.Button(LC("button.compare", "tooltip.compare"), GUILayout.Width(80)))
                _diffMessages = VariantService.Diff(
                    _asset.variants[Mathf.Clamp(_diffA, 0, names.Length - 1)],
                    _asset.variants[Mathf.Clamp(_diffB, 0, names.Length - 1)]);
            EditorGUILayout.EndHorizontal();

            if (_diffMessages != null)
                EditorGUILayout.HelpBox(_diffMessages.Count == 0
                    ? "(identical)" : string.Join("\n", _diffMessages), MessageType.None);
        }
    }
}
