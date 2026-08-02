using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Core;
using emiteat.NexUI.Designer.Editor.Common;

namespace emiteat.NexUI.Designer.Editor.Contracts
{
    /// <summary>Full authoring UI for UI contracts (§6).</summary>
    public sealed class ContractEditorWindow : NexUIToolWindow
    {
        [SerializeField] private DesignerMetadataAsset _asset;
        private List<string> _checkMessages;
        private List<string> _validation;

        protected override string TitleKey => "panel.contract";
        protected override string TooltipKey => "tooltip.contract";

        public static void Open() => GetWindow<ContractEditorWindow>();

        protected override void DrawBody()
        {
            _asset = (DesignerMetadataAsset)EditorGUILayout.ObjectField(
                LC("panel.hierarchy"), _asset, typeof(DesignerMetadataAsset), false);
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(T("message.noScreenSelected"), MessageType.Info);
                return;
            }

            var contract = _asset.contract;
            Section("panel.contract");
            contract.contractId = EditorGUILayout.TextField("contractId", contract.contractId);
            contract.screenId = EditorGUILayout.TextField("screenId", contract.screenId);

            for (int i = 0; i < contract.requiredElements.Count; i++)
                DrawRequirement(contract, contract.requiredElements[i], i);

            if (GUILayout.Button("+ required element", GUILayout.Width(150)))
            {
                Undo.RecordObject(_asset, "Add Contract Requirement");
                ContractService.AddRequirement(contract, "element" + (contract.requiredElements.Count + 1));
                MarkDirty(_asset);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("toolbar.validate"), GUILayout.Width(120)))
                _validation = ContractService.Validate(_asset);
            if (GUILayout.Button(LC("button.compare", "tooltip.contract"), GUILayout.Width(160)))
                _checkMessages = ContractService.CheckSatisfaction(_asset);
            if (GUILayout.Button("Export UIScreenContract", GUILayout.Width(200)))
                ExportContractAsset(contract);
            if (GUILayout.Button("Generate C# Constants", GUILayout.Width(200)))
            {
                var written = ContractCodeGenerator.Generate(_asset);
                if (!string.IsNullOrEmpty(written))
                    Debug.Log($"[NexUI Studio] Generated constants at {written}");
            }
            EditorGUILayout.EndHorizontal();

            if (_validation != null && _validation.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", _validation), MessageType.Warning);
            if (_checkMessages != null)
                EditorGUILayout.HelpBox(_checkMessages.Count == 0
                    ? T("message.validationPassed")
                    : T("validation.missingContractElement") + "\n" + string.Join("\n", _checkMessages),
                    _checkMessages.Count == 0 ? MessageType.Info : MessageType.Error);
        }

        private void DrawRequirement(DesignerContractMetadata contract, DesignerContractElementMetadata req, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            req.elementId = EditorGUILayout.TextField($"#{index} elementId", req.elementId);
            req.required = GUILayout.Toggle(req.required, "required", GUILayout.Width(80));
            if (GUILayout.Button(LC("button.delete"), GUILayout.Width(60)))
            {
                Undo.RecordObject(_asset, "Remove Requirement");
                ContractService.RemoveRequirement(contract, req); MarkDirty(_asset);
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("required capabilities", EditorStyles.miniBoldLabel);
            for (int j = 0; j < req.requiredCapabilities.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();
                int cur = Mathf.Max(0, System.Array.IndexOf(ContractService.KnownCapabilities, req.requiredCapabilities[j]));
                int sel = EditorGUILayout.Popup(cur, ContractService.KnownCapabilities);
                req.requiredCapabilities[j] = ContractService.KnownCapabilities[sel];
                if (GUILayout.Button("×", GUILayout.Width(22))) { req.requiredCapabilities.RemoveAt(j); MarkDirty(_asset); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ capability", GUILayout.Width(110)))
            {
                req.requiredCapabilities.Add(ContractService.KnownCapabilities[0]); MarkDirty(_asset);
            }
            EditorGUILayout.EndVertical();
        }

        private void ExportContractAsset(DesignerContractMetadata contract)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export UIScreenContract", (contract.contractId ?? "ScreenContract") + ".asset", "asset", "");
            if (string.IsNullOrEmpty(path)) return;

            var so = ScriptableObject.CreateInstance<UIScreenContract>();
            ContractService.Populate(so, contract);
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(so);
        }
    }
}
