using emiteat.NexUI.Designer.Editor.Localization;
using emiteat.NexUI.Designer.Editor.Validation;
using emiteat.NexUI.Designer.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using emiteat.NexUI.Designer.Editor.Productivity;
using System.Linq;

namespace emiteat.NexUI.Designer.Editor.Panels
{
    public sealed class NexUIDesignerValidationPanel : VisualElement
    {
        private readonly NexUIDesignerContext _context;
        private readonly Label _summary;
        private readonly ScrollView _list;
        private readonly Button _fixAll;

        public NexUIDesignerValidationPanel(NexUIDesignerContext context)
        {
            _context = context;
            AddToClassList("nexui-panel");
            AddToClassList("nexui-validation-panel");
            Add(new Label(DesignerLocalization.T("panel.validation")) { name = "PanelTitle", tooltip = DesignerLocalization.T("tooltip.validationPanel.title") });

            _summary = new Label();
            _summary.AddToClassList("nexui-validation-summary");
            Add(_summary);

            _fixAll = new Button(() =>
            {
                var count = DesignerAutoFixService.FixAllSafe(_context, _context.ValidationIssues.ToList());
                if (count == 0) EditorUtility.DisplayDialog("NexUI", "자동으로 안전하게 수정할 항목이 없습니다.", "확인");
            }) { text = DesignerLocalization.T("productivity.fixAllSafe"), tooltip = "데이터 손실 가능성이 없는 문제만 한 번의 Undo 단계로 수정합니다." };
            _fixAll.AddToClassList("nexui-button-secondary");
            Add(_fixAll);

            _list = new ScrollView { name = "ValidationList" };
            _list.AddToClassList("nexui-validation-content");
            Add(_list);

            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add(h => context.ValidationChanged += h, h => context.ValidationChanged -= h, Refresh);
            Refresh();
        }

        private void Refresh()
        {
            _list.Clear();
            var issues = _context.ValidationIssues;

            if (issues.Count == 0)
            {
                _summary.text = DesignerLocalization.T("message.validationPassed");
                EnableInClassList("is-valid", true);
                EnableInClassList("has-issues", false);
                _fixAll.SetEnabled(false);
                return;
            }

            _summary.text = $"{_context.ErrorCount} error(s), {_context.WarningCount} warning(s)";
            EnableInClassList("is-valid", false);
            EnableInClassList("has-issues", true);
            _fixAll.SetEnabled(issues.Any(x => DesignerAutoFixService.GetFix(_context, x)?.IsSafe == true));

            foreach (var issue in issues)
            {
                var row = new VisualElement();
                row.AddToClassList("nexui-validation-item");
                row.AddToClassList("severity-" + issue.Severity.ToString().ToLowerInvariant());
                row.style.flexDirection = FlexDirection.Row;
                var text = new Label(issue.ToString());
                text.style.flexGrow = 1;
                text.style.whiteSpace = WhiteSpace.Normal;
                row.Add(text);
                if (!string.IsNullOrEmpty(issue.ElementId))
                {
                    var id = issue.ElementId;
                    text.tooltip = string.Format(DesignerLocalization.T("tooltip.validationPanel.item"), id);
                    text.RegisterCallback<ClickEvent>(_ => _context.SelectMetadata(id));
                }
                else if (issue.Asset != null)
                {
                    var asset = issue.Asset;
                    text.RegisterCallback<ClickEvent>(_ => EditorGUIUtility.PingObject(asset));
                }
                var captured = issue;
                row.RegisterCallback<ContextClickEvent>(evt =>
                {
                    ShowIssueMenu(captured);
                    evt.StopPropagation();
                });

                var fix = DesignerAutoFixService.GetFix(_context, issue);
                if (fix != null)
                {
                    var button = new Button(() =>
                    {
                        if (!fix.IsSafe && !EditorUtility.DisplayDialog("수정 확인", $"'{fix.Label}' 작업은 에셋을 생성하거나 참조를 변경합니다. 계속할까요?", "수정", "취소")) return;
                        fix.Apply();
                        _context.Validate();
                    }) { text = "Fix", tooltip = fix.Label + (fix.IsSafe ? " (안전한 수정)" : " (확인 필요)") };
                    button.AddToClassList("nexui-button-secondary");
                    row.Add(button);
                }
                _list.Add(row);
            }
        }

        /// <summary>
        /// Right-click menu for a Console entry, mirroring what Unity's Console offers on a log
        /// line: copy the message, jump to what it is about, run the check again.
        /// </summary>
        private void ShowIssueMenu(DesignerValidationIssue issue)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.console.copy")), false,
                () => EditorGUIUtility.systemCopyBuffer = issue.ToString());
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.console.copyAll")), false,
                () => EditorGUIUtility.systemCopyBuffer =
                    string.Join(System.Environment.NewLine, _context.ValidationIssues.Select(x => x.ToString())));
            menu.AddSeparator("");

            var elementId = issue.ElementId;
            if (!string.IsNullOrEmpty(elementId))
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.console.selectElement")), false,
                    () => _context.SelectMetadata(elementId));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.console.selectElement")));

            var asset = issue.Asset;
            if (asset != null)
                menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.console.pingAsset")), false,
                    () => EditorGUIUtility.PingObject(asset));
            else
                menu.AddDisabledItem(new GUIContent(DesignerLocalization.T("ctx.console.pingAsset")));

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(DesignerLocalization.T("ctx.console.revalidate")), false, _context.Validate);

            menu.ShowAsContext();
        }
    }
}
