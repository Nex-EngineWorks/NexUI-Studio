using System;
using System.Collections.Generic;
using emiteat.NexUI.Designer.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace emiteat.NexUI.Designer.Editor.Utilities
{
    /// <summary>
    /// A single, searchable home for the small NexUI editor tools. The tools remain
    /// independent windows so their existing workflows and serialized state are preserved.
    /// </summary>
    public sealed class NexUIUtilitiesWindow : EditorWindow
    {
        private const string StylePath = "Packages/com.emiteat.nexui.designer/Editor/Styles/NexUIDesigner.uss";

        private readonly List<ToolEntry> _tools = new()
        {
            Header("utilities.category.workspace", "utilities.category.workspace.description"),
            Window("aiAssistant", "emiteat.NexUI.Designer.Editor.AI.NexUIAIWindow, emiteat.NexUI.Designer.Editor", "ai assistant copilot chat prompt openai design generate edit 인공지능 채팅 생성 편집"),
            Window("setupDoctor", "emiteat.NexUI.Designer.Editor.Productivity.NexUISetupDoctorWindow, emiteat.NexUI.Designer.Editor", "doctor readiness dependency install setup 진단 준비 의존성 설치"),
            Window("projectSetup", "emiteat.NexUI.Editor.ProjectSetup.NexUIProjectSetupWindow, emiteat.NexUI.Editor.ProjectSetup", "setup defaults install 설정 기본 설치"),
            Action("projectSettings", () => SettingsService.OpenProjectSettings("Project/NexUI"), "settings preferences 설정 환경설정"),
            Window("validator", "emiteat.NexUI.Editor.Validator.NexUIValidatorWindow, emiteat.NexUI.Editor.Validator", "validate errors warnings 검사 오류 경고"),
            Window("idGenerator", "emiteat.NexUI.Editor.IDGenerator.NexUIIDGeneratorWindow, emiteat.NexUI.Editor.IDGenerator", "id code generate 식별자 코드 생성"),
            Window("debugSnapshot", "emiteat.NexUI.Editor.DebugTools.NexUIDebugWindow, emiteat.NexUI.Editor.DebugTools", "debug runtime snapshot 디버그 런타임"),
            Window("migration", "emiteat.NexUI.Editor.Migration.NexUIMigrationWindow, emiteat.NexUI.Editor.Migration", "migrate upgrade legacy 마이그레이션 업그레이드"),

            Header("utilities.category.design", "utilities.category.design.description"),
            Window("designTokens", "emiteat.NexUI.Designer.Editor.Tokens.DesignerTokenWindow, emiteat.NexUI.Designer.Editor", "theme style tokens 테마 스타일 토큰"),
            Window("uiRecipes", "emiteat.NexUI.Designer.Editor.Recipes.RecipesWindow, emiteat.NexUI.Designer.Editor", "template create 템플릿 생성"),
            Window("screenVariants", "emiteat.NexUI.Designer.Editor.Variants.VariantEditorWindow, emiteat.NexUI.Designer.Editor", "variant platform state 변형 플랫폼 상태"),
            Window("responsiveRules", "emiteat.NexUI.Designer.Editor.Responsive.ResponsiveEditorWindow, emiteat.NexUI.Designer.Editor", "responsive resolution layout 반응형 해상도 레이아웃"),
            Window("promptGlyphs", "emiteat.NexUI.Designer.Editor.PromptGlyph.PromptGlyphWindow, emiteat.NexUI.Designer.Editor", "input glyph gamepad 입력 글리프 게임패드"),
            Window("gameLocalization", "emiteat.NexUI.Designer.Editor.GameLocalization.GameLocalizationWindow, emiteat.NexUI.Designer.Editor", "language locale text 언어 현지화 텍스트"),
            Window("uiContract", "emiteat.NexUI.Designer.Editor.Contracts.ContractEditorWindow, emiteat.NexUI.Designer.Editor", "contract code binding 계약 코드 바인딩"),
            Window("generateUxml", "emiteat.NexUI.Designer.Editor.Backend.UIToolkitGenerationWindow, emiteat.NexUI.Designer.Editor", "uitoolkit export backend 생성 내보내기"),
            Window("figmaBridge", "emiteat.NexUI.Integrations.Figma.FigmaWindow, emiteat.NexUI.Integrations.Figma", "figma import sync 피그마 가져오기 동기화"),
            Window("agentHandoff", "emiteat.NexUI.Designer.Editor.AgentHandoff.AgentHandoffWindow, emiteat.NexUI.Designer.Editor", "agent ai export manifest 에이전트 내보내기"),

            Header("utilities.category.motion", "utilities.category.motion.description"),
            Window("motionClip", "emiteat.NexUI.Designer.Editor.MotionClipEditor.MotionClipEditorWindow, emiteat.NexUI.Designer.Editor", "animation timeline clip 애니메이션 타임라인 클립"),
            Window("motionBudget", "emiteat.NexUI.Designer.Editor.MotionBudget.MotionBudgetWindow, emiteat.NexUI.Designer.Editor", "animation performance 애니메이션 성능"),
            Window("scenarioEditor", "emiteat.NexUI.Designer.Editor.Scenario.ScenarioEditorWindow, emiteat.NexUI.Designer.Editor", "scenario timeline test 시나리오 테스트"),
            Window("inputPreview", "emiteat.NexUI.Designer.Editor.InputPreview.InputPreviewWindow, emiteat.NexUI.Designer.Editor", "input device preview 입력 장치 미리보기"),
            Window("loadingStrategy", "emiteat.NexUI.Designer.Editor.LoadingStrategy.LoadingStrategyWindow, emiteat.NexUI.Designer.Editor", "load addressables performance 로딩 성능"),
            Window("syncPublish", "emiteat.NexUI.Designer.Editor.Sync.SyncPublishWindow, emiteat.NexUI.Designer.Editor", "sync publish export 동기화 게시"),

            Header("utilities.category.experimental", "utilities.category.experimental.description"),
            Window("motionGraph", "emiteat.NexUI.Designer.Editor.GraphV2.MotionGraphV2Window, emiteat.NexUI.Designer.Editor", "beta experimental animation graph 베타 실험 애니메이션 그래프"),
            Window("motionStateMachine", "emiteat.NexUI.Designer.Editor.StateMachine.MotionStateMachineWindow, emiteat.NexUI.Designer.Editor", "beta experimental animation state transition 베타 실험 상태 전환"),
            Window("screenFlow", "emiteat.NexUI.Designer.Editor.ScreenFlow.ScreenFlowWindow, emiteat.NexUI.Designer.Editor", "beta experimental navigation graph screen 베타 실험 내비게이션 화면"),

            Header("utilities.category.quality", "utilities.category.quality.description"),
            Window("accessibility", "emiteat.NexUI.Designer.Editor.Accessibility.AccessibilityWindow, emiteat.NexUI.Designer.Editor", "accessibility a11y focus 접근성 포커스"),
            Window("contrast", "emiteat.NexUI.Designer.Editor.Contrast.ContrastWindow, emiteat.NexUI.Designer.Editor", "contrast wcag color 대비 색상"),
            Window("fontGlyph", "emiteat.NexUI.Designer.Editor.FontCheck.FontCheckWindow, emiteat.NexUI.Designer.Editor", "font glyph locale 폰트 글리프"),
            Window("uiProfiler", "emiteat.NexUI.Designer.Editor.Profiler.ProfilerWindow, emiteat.NexUI.Designer.Editor", "profile performance 프로파일 성능"),
            Window("bindingProfiler", "emiteat.NexUI.Designer.Editor.BindingProfiler.BindingProfilerWindow, emiteat.NexUI.Designer.Editor", "binding profile performance 바인딩 성능"),
            Window("flowSimulator", "emiteat.NexUI.Designer.Editor.FlowSimulator.FlowSimulatorWindow, emiteat.NexUI.Designer.Editor", "navigation simulate test 흐름 시뮬레이션"),
            Window("metadataDiff", "emiteat.NexUI.Designer.Editor.Diff.DiffEditorWindow, emiteat.NexUI.Designer.Editor", "compare diff metadata 비교 차이"),
            Window("snapshot", "emiteat.NexUI.Designer.Editor.Snapshot.SnapshotEditorWindow, emiteat.NexUI.Designer.Editor", "snapshot compare 스냅샷 비교"),
            Window("renameRefactor", "emiteat.NexUI.Designer.Editor.Refactor.RenameEditorWindow, emiteat.NexUI.Designer.Editor", "rename refactor id 이름 변경"),
            Window("referenceCleaner", "emiteat.NexUI.Designer.Editor.Cleaner.CleanerEditorWindow, emiteat.NexUI.Designer.Editor", "clean dead reference 정리 참조"),
        };

        private TextField _search;
        private ScrollView _content;

        private void OnEnable() => DesignerLocalization.LanguageChanged += RebuildLocalizedUI;
        private void OnDisable() => DesignerLocalization.LanguageChanged -= RebuildLocalizedUI;

        private void RebuildLocalizedUI()
        {
            if (rootVisualElement == null) return;
            CreateGUI();
            Repaint();
        }

        [MenuItem("Tools/NexUI/유틸리티", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<NexUIUtilitiesWindow>();
            window.titleContent = new GUIContent(DesignerLocalization.T("utilities.window.title"));
            window.minSize = new Vector2(520, 420);
            window.Show();
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent(DesignerLocalization.T("utilities.window.title"));
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("nexui-designer-root");
            rootVisualElement.AddToClassList("nexui-utilities-root");

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styles != null) rootVisualElement.styleSheets.Add(styles);

            var header = new VisualElement();
            header.AddToClassList("nexui-utilities-header");
            var title = new Label(DesignerLocalization.T("utilities.title"));
            title.AddToClassList("nexui-utilities-title");
            header.Add(title);
            var subtitle = new Label(DesignerLocalization.T("utilities.subtitle"));
            subtitle.AddToClassList("nexui-utilities-subtitle");
            header.Add(subtitle);
            rootVisualElement.Add(header);

            _search = new TextField { tooltip = DesignerLocalization.T("utilities.search.tooltip") };
            _search.AddToClassList("nexui-utilities-search");
            _search.RegisterValueChangedCallback(_ => RebuildTools());
            rootVisualElement.Add(_search);

            _content = new ScrollView();
            _content.AddToClassList("nexui-utilities-content");
            rootVisualElement.Add(_content);
            RebuildTools();

            rootVisualElement.schedule.Execute(() => _search.Focus());
        }

        private void RebuildTools()
        {
            _content.Clear();
            var query = (_search.value ?? string.Empty).Trim();
            VisualElement grid = null;
            var visible = 0;

            foreach (var tool in _tools)
            {
                if (tool.IsHeader)
                {
                    grid = null;
                    if (!CategoryHasMatch(tool.Name, query)) continue;
                    var section = new VisualElement();
                    section.AddToClassList("nexui-utilities-section");
                    var heading = new Label(tool.Name);
                    heading.AddToClassList("nexui-utilities-section-title");
                    section.Add(heading);
                    var detail = new Label(tool.Description);
                    detail.AddToClassList("nexui-utilities-section-subtitle");
                    section.Add(detail);
                    _content.Add(section);

                    grid = new VisualElement();
                    grid.AddToClassList("nexui-utilities-grid");
                    _content.Add(grid);
                    continue;
                }

                if (!tool.Matches(query)) continue;
                if (grid == null)
                {
                    grid = new VisualElement();
                    grid.AddToClassList("nexui-utilities-grid");
                    _content.Add(grid);
                }

                var card = new Button(tool.Open);
                card.AddToClassList("nexui-utility-card");
                var label = new Label(tool.Name);
                label.AddToClassList("nexui-utility-card-title");
                card.Add(label);
                var description = new Label(tool.Description);
                description.AddToClassList("nexui-utility-card-description");
                card.Add(description);
                grid.Add(card);
                visible++;
            }

            if (visible != 0) return;
            var empty = new Label(DesignerLocalization.T("utilities.search.empty"));
            empty.AddToClassList("nexui-utilities-empty");
            _content.Add(empty);
        }

        private bool CategoryHasMatch(string category, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            var inCategory = false;
            foreach (var entry in _tools)
            {
                if (entry.IsHeader)
                {
                    if (inCategory) break;
                    inCategory = entry.Name == category;
                    continue;
                }
                if (inCategory && entry.Matches(query)) return true;
            }
            return false;
        }

        private static ToolEntry Header(string nameKey, string descriptionKey) => new(nameKey, descriptionKey, null, null, true);
        private static ToolEntry Action(string id, Action open, string keywords)
            => new("utilities.tool." + id, "utilities.tool." + id + ".description", open, keywords, false);
        private static ToolEntry Window(string id, string typeName, string keywords)
            => Action(id, () => OpenWindow(typeName, DesignerLocalization.T("utilities.tool." + id)), keywords);

        private static void OpenWindow(string typeName, string title)
        {
            var type = Type.GetType(typeName);
            if (type == null || !typeof(EditorWindow).IsAssignableFrom(type))
            {
                EditorUtility.DisplayDialog(DesignerLocalization.T("utilities.window.title"),
                    DesignerLocalization.T("utilities.unavailable", title), DesignerLocalization.T("button.ok"));
                return;
            }

            var window = GetWindow(type);
            window.Show();
            window.Focus();
        }

        private sealed class ToolEntry
        {
            private readonly string _nameKey;
            private readonly string _descriptionKey;
            public string Name => DesignerLocalization.T(_nameKey);
            public string Description => DesignerLocalization.T(_descriptionKey);
            public readonly Action Open;
            public readonly string Keywords;
            public readonly bool IsHeader;

            public ToolEntry(string name, string description, Action open, string keywords, bool isHeader)
            {
                _nameKey = name;
                _descriptionKey = description;
                Open = open;
                Keywords = keywords ?? string.Empty;
                IsHeader = isHeader;
            }

            public bool Matches(string query)
            {
                if (string.IsNullOrEmpty(query)) return true;
                return Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || Keywords.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
