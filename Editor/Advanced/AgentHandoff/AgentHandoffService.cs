using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using emiteat.NexUI.Core;

namespace emiteat.NexUI.Designer.Editor.AgentHandoff
{
    /// <summary>
    /// Collects the project's NexUI surface area (screens, state/action keys, motions,
    /// themes, contracts) from all DesignerMetadataAssets and UIScreenDefinitions and
    /// renders it as the AI agent handoff manifest (JSON) and brief (Markdown) (§23).
    /// </summary>
    public static class AgentHandoffService
    {
        public static DesignerAgentHandoffMetadata Collect()
        {
            var m = new DesignerAgentHandoffMetadata();
            var screens = new SortedSet<string>();
            var state = new SortedSet<string>();
            var action = new SortedSet<string>();
            var motion = new SortedSet<string>();
            var theme = new SortedSet<string>();
            var contracts = new SortedSet<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:DesignerMetadataAsset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<DesignerMetadataAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a == null) continue;
                Add(screens, a.screenId);
                foreach (var e in a.elements)
                {
                    if (e?.binding != null)
                    {
                        Add(state, e.binding.textKey); Add(state, e.binding.valueKey);
                        Add(state, e.binding.visibilityKey); Add(state, e.binding.interactableKey);
                        Add(action, e.binding.commandKey);
                    }
                    if (e?.motion != null) Add(motion, e.motion.motionId);
                    if (e?.theme != null) Add(theme, e.theme.themeId);
                }
                if (a.contract != null) Add(contracts, a.contract.contractId);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:UIScreenDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<UIScreenDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) Add(screens, d.ScreenId);
            }

            m.screens.AddRange(screens);
            m.stateKeys.AddRange(state);
            m.actionKeys.AddRange(action);
            m.motionIds.AddRange(motion);
            m.themeIds.AddRange(theme);
            m.contracts.AddRange(contracts);
            m.forbiddenRules.Add("Core must not reference UnityEngine.UIElements");
            m.forbiddenRules.Add("Motion must not reference DOTween directly");
            m.forbiddenRules.Add("Do not reintroduce the 'Hyojun' package/namespace (renamed to emiteat)");
            return m;
        }

        public static string ToJson(DesignerAgentHandoffMetadata m) => JsonUtility.ToJson(m, true);

        public static string ToMarkdown(DesignerAgentHandoffMetadata m)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# NexUI Agent Brief").AppendLine();
            sb.AppendLine($"- package: `{m.package}`");
            sb.AppendLine($"- designer package: `{m.designerPackage}`").AppendLine();
            Bullets(sb, "Screens", m.screens);
            Bullets(sb, "State keys", m.stateKeys);
            Bullets(sb, "Action keys", m.actionKeys);
            Bullets(sb, "Motion ids", m.motionIds);
            Bullets(sb, "Theme ids", m.themeIds);
            Bullets(sb, "Contracts", m.contracts);
            Bullets(sb, "Known issues", m.knownIssues);
            Bullets(sb, "Forbidden rules", m.forbiddenRules);
            sb.AppendLine("## Suggested next steps").AppendLine();
            sb.AppendLine("- Run Tools/NexUI/QA/Run Advanced Validation and resolve findings.");
            sb.AppendLine("- Fill missing contracts / focus graphs flagged above.");
            return sb.ToString();
        }

        private static void Bullets(StringBuilder sb, string title, List<string> items)
        {
            sb.AppendLine($"## {title}").AppendLine();
            if (items.Count == 0) sb.AppendLine("_(none)_");
            else foreach (var i in items) sb.AppendLine($"- {i}");
            sb.AppendLine();
        }

        private static void Add(SortedSet<string> set, string v)
        {
            if (!string.IsNullOrEmpty(v)) set.Add(v);
        }
    }
}
