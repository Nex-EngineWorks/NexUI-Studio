# Architecture Assessment — Phase 0 감사

작성일: 2026-08-04
대상: `com.nexengineworks.nexui` (Core) + `com.nexengineworks.nexui.studio` (Studio)
기준 문서: **v3-draft — NexUI Studio 통합 제품 기능 명세서** (Part 1~23, §4~§92)

> **문서 위치에 대한 참고.** v3-final은 `Documentation/ArchitectureAssessment.md`를 지정했지만, 프로젝트 루트는
> git 저장소가 아니어서 루트에 둔 문서는 어느 저장소에도 추적되지 않습니다. 두 패키지 각각이 저장소이므로
> Studio 패키지의 `Documentation~/` 아래에 두어 `NexUI-Studio.git`이 추적하게 했습니다.

---

## 0. 판정 기준과 증거 수준

v3-draft의 상태 라벨(현재 지원 / Beta / 부분 지원 / 실험적 / 목표 기능)은 **추정치로 취급**하고 전면 재판정했습니다.

### 판정값

| 값 | 의미 |
|---|---|
| **Verified Working** | Authoring → Serialize → Undo → Compile → Runtime 흐름이 코드상 끊김 없이 이어짐 |
| **Partially Working** | 일부만 존재. 어디까지인지 비고에 명시 |
| **Not Implemented** | 해당 코드 없음 |
| **Broken** | 코드는 있으나 실행 실패 |

### 증거 수준

최초 감사는 정적 코드 증거만으로 작성했습니다. 이후 테스트를 실행해 일부 항목의 등급이 올라갔습니다.

| 등급 | 의미 |
|---|---|
| **A(실행)** | 테스트를 실제로 실행해 통과 확인 |
| **A** | 해당 코드를 읽고 로직 흐름까지 확인 (실행은 안 함) |
| **B** | 타입·파일 존재와 이름·시그니처 수준 확인 (동작 미확인) |
| **C** | 부재 확인 (저장소 전역 grep 0건) |

**등급 B는 여전히 "실행하지 않아 알 수 없다"는 뜻입니다.** 실제로는 Broken일 수 있습니다.

이 구분이 장식이 아니라는 증거가 이번에 나왔습니다: 등급 A로 적어둔 Validation 호출 합치기가
실행 결과 `UndoBackToBaseline`을 깨뜨렸고, 코드를 읽는 것만으로는 끝까지 설명하지 못해 되돌렸습니다.

### 감사 제외 (v3-final §1.4)

| v3-draft 섹션 | 제외 사유 |
|---|---|
| §77 Unity Version Compatibility | v3-final §2.1 — 2022.3 LTS + 6.x만 지원, 2018~2021 폐기 |
| §79 Downgrade Export | 위와 동일 (레거시 Unity 대상 기능) |
| §39~§41의 Built-in / HDRP 축 | v3-final §2.2 — URP만 지원 |
| §25의 Figma REST API 경로 | v3-final §2.3 — JSON Import 우선, Live API는 Pro 티어로 연기 |

---

## 1. 요약

감사 대상 **85개 섹션** (§4~§92 중 제외 2개, 서술 섹션 §23 제외).

| 판정 | 감사 시점 | 현재 | 비율 |
|---|---:|---:|---:|
| Verified Working | 0 | 0 | 0% |
| Partially Working | 51 | **60** | 71% |
| Not Implemented | 34 | **25** | 29% |
| Broken | 0 (미확인) | 0 (미확인) | — |

감사 이후 아홉 항목이 이동했습니다: **§30 Interaction Authoring**, **§31 Event Propagation**,
**§49 Diagnostics Console**, **§52 Why Debugging**, **§54 Automation ID**, **§55 Scenario Replay**,
**§73 Runtime Override**, **§74 Time Source**, **§85 성능**.
이 중 여섯 개 섹션이 등급 **A(실행)** 입니다.

### Verified Working이 여전히 0인 이유

테스트는 통과했지만, v3-draft §3의 완성 정의는 Authoring부터 Runtime까지의 **전 구간**을 요구합니다.
새 파이프라인이 다루는 범위는 아직 좁습니다 — Node Kind 4종, Layout·Component Instance·Variant·Motion 미반영,
UI Toolkit Backend 없음, 실제 Player Build 미검증. 좁은 범위가 잘 동작하는 것과
기능이 완성된 것은 다르므로 Partially에 둡니다.

**숫자를 좋게 만들려고 기준을 낮추지 않습니다.**

### v3-draft 라벨과 가장 크게 어긋난 항목

| 섹션 | v3-draft 표기 | 재판정 | 격차 |
|---|---|---|---|
| §26 Binding | "기본 Binding 지원" | Partially (B) | Studio가 저작한 Binding이 Compile 경로로 전달되지 않음 |
| §30 Interaction Authoring | "부분 지원" | **Not Implemented (C)** | Trigger/Condition/Action 저작 모델이 아예 없음 |
| §48 Structured Diagnostics | "핵심 목표 기능" | Partially (A) | 오히려 진행됨 — 신규 구현 |
| §50 Flow Trace | "핵심 목표 기능" | Partially (A) | 오히려 진행됨 — 신규 구현 |
| §53 Source Map | "핵심 기반 목표" | Partially (A) | 오히려 진행됨 — 신규 구현 |
| §57 UI Compiler | "장기 핵심 구조" | Partially (A) | 오히려 진행됨 — 신규 구현 |
| §85 성능 (Incremental) | 비기능 요구 | **Not Implemented (C)** | Dirty Range·Dependency Graph·Incremental 전무 |

§48·§50·§53·§57·§62·§64·§66은 **이번 세션에 새로 구현**된 것이며, 그 전에는 코드가 0이었습니다.

---

## 2. 섹션별 판정

### Part 1. 프로젝트와 작업 공간

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 4 | UI 프로젝트 관리 | 현재 지원·Beta | Partially | B | `DesignerScreenCreationService/Window`로 화면 생성 마법사와 관련 에셋 일괄 생성·Rollback 존재. 즐겨찾기·태그·검색은 코드 미확인 |
| 5 | Workspace 시스템 | 현재 기반 존재 | **Not Implemented** | C | Workspace 타입 없음. `DesignerUIState`가 EditorPrefs로 패널 상태 일부만 복원. 13종 Workspace 전환은 없음 |
| 6 | 공통 Studio 도구 | 현재 일부 지원 | Partially | B | `NexUICommandPalette`, `NexUIGlobalToolbar`, `NexUISetupDoctorWindow`, `NexUIUtilitiesWindow` 존재. **Safe Mode·Notification Center·Background Job Progress·작업 취소 없음** |

### Part 2. Canvas와 시각적 편집

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 7 | Canvas 편집 | 현재 지원 | Partially | B | 저장소에서 가장 완성도 높은 영역. `NexUIDesignerViewport`(77KB) + `Tools/`의 Select/Move/Resize/Rect/Anchor/Align/Spacing/Create/Delete/Duplicate. Runtime 미확인이라 Verified 아님 |
| 8 | Grid·Guide·측정 | 현재 일부 기반 | Partially | B | `DesignerGridOverlay`, `DesignerRulerOverlay`, `DesignerSafeAreaOverlay`, `DesignerLayerOverlay`. Smart Guide·요소 간 거리 표시·Foldable Overlay는 미확인 |
| 9 | Hierarchy와 Layer | 현재 지원 | Partially | B | `NexUIDesignerHierarchy`, `DesignerHierarchyUtility`, `UIElementLayerOrder`, `NexUILayersPanel`. 순환 부모 방지는 새 Compiler에서 `NEX-DOC-1005`로 검출 |
| 10 | Group과 Container | 현재 지원·Beta | Partially | B | `BuiltInDesignerCommands`에 Group 계열 존재. Group→Component 변환, Group→Auto Layout 변환은 미확인 |

### Part 3. Layout과 Responsive UI

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 11 | Layout 시스템 | Row·Column·Grid Beta | Partially | B | `DesignerAutoLayoutMetadata`(Row/Column/Grid), `LayoutInspector`, `DesignerConstraintMetadata`. **고급 Layout(Flex/Radial/Path/Masonry/Virtualized) 전무.** 새 Compiler는 Layout을 아직 읽지 않음 |
| 12 | Auto Layout 감지·변환 | Beta | Partially | B | `DesignerLayoutAnalysisService`, `DesignerLayoutConversionWindow` |
| 13 | Responsive Design | Beta | Partially | B | `ResponsiveService`, `ResponsiveEditorWindow`, `DesignerResponsiveMetadata`, Runtime `ResponsiveRule` + `ResponsiveRuleValidator`. Compile 경로 미반영 |

### Part 4. Style과 Design System

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 14 | Typed Property System | Beta | Partially | B | `DesignerPropertyRegistry/Applier/Adapter`, `DesignerComponentPropertyModel`, `StudioPropertyValueCodec`(JSON round-trip). **Compiler는 이 속성을 읽지 않음** — 현재 IR은 rect/tint/text/fontSize만 |
| 15 | Visual Style | 부분 지원 | Partially | B | `DesignerVisualStyleMetadata`, `StyleInspector`. Gradient/Blend Mode/Inner Shadow 등 미확인 |
| 16 | Design Token | 실험적 | Partially | B | `DesignerTokenWindow/Resolver`, `DesignerTokenSetAsset`, Runtime `ThemeToken`. Typed Token API 생성·영향 분석 없음 |
| 17 | Theme | 부분 지원 | Partially | B | Runtime이 상대적으로 두꺼움: `UITheme`, `ThemeRegistry`, `ThemeTransition`, `UIThemeRegistryAsset`, `RuntimeTokenOverride`, 양 Backend `ThemeApplier` |

### Part 5. Component 시스템

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 18 | Component Registry | 현재 지원 | Partially | A | `DesignerComponentRegistry` + 5개 카탈로그(NexUI/UGUI/UIToolkit/Game/Library). `UGUIControl` 매핑을 새 Compiler가 실제로 사용 중 |
| 19 | 재사용 Component | Beta | Partially | B | `DesignerComponentDefinitionAsset`, `DesignerComponentExpander/Service`, `ComponentInstanceInspector`. Compile 경로에서 Instance 전개 안 됨 |
| 20 | Slot·Exposed Property | Beta | Partially | B | `DesignerComponentPartMetadata`, `DesignerComponentPropertyMetadata`, 카탈로그의 Slot 정의 |
| 21 | Component Variant | Beta | Partially | B | `VariantService`, `VariantEditorWindow`, `DesignerVariantMetadata`, Runtime `UIScreenVariant` + `ScreenVariantValidator` |
| 22 | Component Version·Package | 일부 기반 | **Not Implemented** | C | `.nexuipack` 0건. Version·Reconcile·Integrity Hash·Dependency 전무 |

### Part 6. Asset과 외부 디자인 Import

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 23 | Assets Panel | 현재 지원·Beta | Partially | B | `NexUIAssetsPanel`, `DesignerAssetBrowser`, `DesignerAssetDropResolver`, `DesignerFolderPickerWindow` |
| 24 | Asset Pipeline | 목표 기능 | **Not Implemented** | C | PSD/SVG/Lottie/Aseprite/Atlas 임포터 없음. Texture 압축 추천·Nine Slice 감지·Memory Estimate 없음 |
| 25 | Figma Import | Beta | Partially | B | `FigmaApiClient/DocumentImporter/Window/Credentials` 존재(REST API 경로). **v3-final §2.3이 우선하는 JSON Import 경로는 Not Implemented.** 파싱 로직은 공용 가능 |

### Part 7. Data Binding과 Collection

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 26 | Binding 시스템 | 기본 지원, 재검증 필요 | Partially | A | **v3-draft가 재검증을 요청한 항목.** Runtime에 `UITextBinder`(Two-way + Converter Registry 실제 구현), `UIValueBinder`, `UIVisibilityBinder`, `UIClassBinder`, `UICommandBinder`, `PropertyBinding` 존재. Two-way는 `IUITextInputCapability`가 있을 때만 동작하고 없으면 경고 후 비활성. **Image/Sprite/Color/Progress Binding은 미확인.** Studio의 `BindingInspector`가 저작한 키가 Compile 경로로는 Text/Command만 전달됨 |
| 27 | Typed Key와 Contract | 일부 코드 기반 | Partially | A | `ContractCodeGenerator`가 `<ScreenId>Ids.cs`로 elementId·commandKey·valueKey 상수를 생성. **Rename Migration·Deprecated Alias·중복 Key 검사·Runtime Registry·IL2CPP 안전 등록은 전부 없음** |
| 28 | Mock·Scenario Data | 현재 지원 | Partially | B | `ScenarioService`, `DesignerScenarioAsset`, `ScenarioTimelineEvaluator`, `ScenarioApplyResolver`, `DesignerMockDataPresetService` |
| 29 | Collection UI | Beta | Partially | B | `NXCollectionModel/Controller/View`, `UIItemPool`, UI Toolkit `NXCollectionViewElement`. **Virtualization·Recycling 코드 실재.** Tree/Table/Carousel 없음 |

### Part 8. Interaction과 Navigation

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 30 | Interaction Authoring | 부분 지원 | Partially | A(실행) | **감사 시점에는 Not Implemented였고, 이후 Vertical Slice 2로 구현했습니다.** `DesignerInteractionRule`(저작) → Compiler lowering → `NexInteractionProgram`(IR) → `NexInteractionRuntime`(실행) → Flow Trace가 연결됩니다. 범위는 Trigger 3종(OnClick/OnShow/OnHide)·Condition 1종(State 값 비교)·Action 5종(ExecuteCommand/SetState/SetVisible/SetText/**Delay**). `Delay`는 규칙을 프레임 너머로 파킹하며 화면 teardown 시 취소됩니다. 별도 `Sequence`는 규칙 자체가 순서라 불필요. 나머지 Trigger 13·Condition 7·Action 11은 여전히 없습니다 |
| 31 | Event Propagation | 목표 기능 | Partially | A(실행) | **Capture/Target/Bubble 3단계 + Stop Propagation 구현.** 규칙의 `phase` 기본값이 `Target`이라 기존 저작 동작은 그대로. Bubble은 안쪽 조상부터 순회. 도달 불가 phase는 `NEX-BND-4007`로 삭제. **없음: Handled/Consumed 구분, Prevent Default, Event Priority/Filter.** 전파는 `OnClick`만 — 생명주기 Trigger는 노드 자신의 사건이라 전파하지 않음 |
| 32 | Screen Flow와 Navigation | Screen Flow 실험적 | Partially | B | Runtime이 두꺼움: `UIManager`, `UIBackStack`, `UIModalStack`, `UILayerManager`, `UIDeepLink(+Router)`, `UIScreenLoadStrategy`, `UIToastQueue`. Studio는 `ScreenFlowWindow/Validator/View` + `DesignerScreenFlowAsset` |
| 33 | Input Compatibility | 목표 기능 | Partially | B | `Integrations/InputSystem`(4파일), `UIInputMode`, `InputModeService`, `UIPromptGlyphTable`, `UICurrentDeviceService/Tracker`. Steam Input·Stylus·XR 없음 |
| 34 | Focus Navigation | Beta | Partially | B | `UIFocusManager`, `UIFocusNavigationGraph`, `UGUIFocusAdapter`, `UGUIDynamicNavigation`, `FocusNavigationPanel/AutoLayout`, `DesignerFocusMetadata`, `ModalFocusValidator` |

### Part 9. Motion과 Visual Effects

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 35 | Motion Clip | 현재 지원 | Partially | B | 완결성 높음: `UIMotionClip`, `Evaluator`, `Player`, `PropertyTrack`, `Keyframe`, `TargetResolver` + `MotionClipEditorWindow`(42KB)·Timeline·Ruler·Easing 브라우저 |
| 36 | Motion Graph·State Machine | 실험적 | Partially | B | `UIMotionGraphAsset`, `UIGraphExecutor`, `BuiltIn/Phase6GraphNodeExecutors`, `UIMotionStateMachine/Runner`, Studio `GraphV2` + `MotionStateMachineWindow`. Legacy Graph와 V2 병존 — v3-draft가 경고한 혼용 상태 |
| 37 | Motion Preset·AnimationClip | 현재 지원·Beta | Partially | B | `UIMotionPreset`, `DesignerTransitionPresetService`, `UIMotionClipImporter/Exporter`, `UnityAnimationClipAdapter`, `MotionConflictResolver`(Replace/Blend/Additive/Queue 등) |
| 38 | Ambient·Procedural Motion | 목표 기능 | **Not Implemented** | C | Float/Drift/Parallax/Noise Flow 등 전무 |
| 39 | Vector Shape | 목표 기능 | **Not Implemented** | C | Tessellation·SDF·Boolean·SVG 전무 |
| 40 | Filter·Material Effect | 목표 기능 | **Not Implemented** | C | Filter Stack·Material Graph 전무. (URP 한정 결정은 구현 시 적용) |
| 41 | UI Particle·VFX | 장기 확장 | **Not Implemented** | C | Emitter·Module 전무 |

### Part 10. Preview와 Scenario

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 42 | Studio Preview | 현재 지원 | Partially | B | `NexUIDesignerPreviewRuntime`, `DesignerPreviewLog`, `NexUIPreviewLogPanel`. Command는 실행 대신 Simulation Log — v3-draft 서술과 일치 |
| 43 | Device Preview | 목표 기능 | Partially | B | `DesignerResolutionPreset`, `DesignerSafeAreaOverlay`, `InputPreviewWindow`. Locale·Color Vision·Reduced Motion·Foldable 없음 |
| 44 | Live Preview와 Hot Reload | 일부 기반 | Partially | B | `SnapshotService` + Preview Runtime은 있으나 **Play Mode Live Patch(Document Change→Patch→Runtime Apply)는 없음** |
| 45 | Runtime Snapshot과 Diff | 현재 지원 | Partially | B | `SnapshotService`, `SnapshotEditorWindow`, `DesignerDiffService`, `DiffEditorWindow`, `DesignerSnapshotMetadata` |

### Part 11. Validation과 Diagnostics

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 46 | Validation | 현재 지원 | Partially | A | `DesignerValidationService`(57KB) + `DesignerComponentValidation`, `DesignerElementComponentValidation`, `DesignerValidationIssue`, 패널. Runtime에도 `IUIValidator` 구현 9종. **요소 단위 규칙이 `ValidateElement`로 분리됨**(순서 보존, 출력 동일) — 범위 축소의 전제. 자동 호출은 Editor tick당 1회로 합쳐짐 |
| 47 | Validation Auto Fix | Beta | Partially | B | `DesignerAutoFixService` |
| 48 | Structured Diagnostics | 핵심 목표 기능 | Partially | A | **신규 구현.** `NexDiagnostic`(Cause Chain), `NexSeverity`(7단계), `NexDiagnosticBag`(중복 억제), `NexDiagnosticCodes`(18개 카탈로그). **기존 Validation은 아직 이 체계를 쓰지 않음** — Compile/Publish/Compiled Runtime 경로 한정 |
| 49 | Diagnostics Console | 목표 기능 | Partially | A | `NexDiagnosticLog`(세션 범위, 중복 그룹화 + 발생 횟수 + 최초/최종 시각, 512개 상한) + `NexDiagnosticQuery`(Severity·Subsystem·Screen·텍스트 검색) + JSON Export + `Tools > NexUI > Diagnostics Console` 창. Compile·Publish 진단이 자동 유입. "해결됨" 표시는 **재발 시 자동 해제**. **없음: Studio Validation과 통합, Runtime 진단 자동 유입, SARIF·JUnit, Timeline, Auto Fix, 관련 요소 열기** |

### Part 12. Interaction Flow와 Runtime Debugging

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 50 | Interaction Flow Trace | 핵심 목표 기능 | Partially | A | **신규 구현.** `NexFlowTrace`(5단계 Level, Off일 때 할당 0, `NEXUI_DISABLE_FLOW_TRACE` Stripping), `NexFlowRecord`(발생자→처리자 텍스트), Console/Memory Sink. **추적 범위는 Click→Command→Handler와 Text Binding 변화뿐.** Flow ID·Correlation ID·4종 보기 모드 없음 |
| 51 | Runtime Debugger | 현재 부분 지원 | Partially | B | `NexUIDebugService/Overlay/API/Snapshot/Options`. Source Map 이동·Binding Value 검사 통합은 없음 |
| 52 | Why Debugging | 목표 기능 | Partially | A | **"Who changed this value?" 하나가 답변됩니다** — `NexOverrideLedger.Explain()`이 Source·Origin·시각·authored 값을 함께 반환(§73). 나머지 8개 질문(Why visible / Why disabled / Why did this event stop / Why is this asset loaded 등)은 없고, Property Resolution Layer 전체 추적도 없습니다 |
| 53 | Source Map | 핵심 기반 목표 | Partially | A | **신규 구현.** `NexSourceMap`(Authoring↔Compiled) + `NexRuntimeSourceMap`(Compiled↔실제 객체). 세 지점 상호 조회 가능. **Compiled 경로에서만 동작** — 기존 Prefab 저장 경로에는 없음 |

### Part 13. 테스트와 자동화

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 54 | Automation ID | 목표 기능 | Partially | A(실행) | **구현됨.** `DesignerElementMetadata.automationId` → Compiler lowering → `NexNodeProgram.AutomationId` → `runtime.FindByAutomationId` / `FindByRole` / `AutomationIds`. Role은 새 어휘를 만들지 않고 `AccessibilityRole` 재사용. 중복은 `NEX-DOC-1007`(Publish 차단) + 저작 중 `duplicate-automation-id`로 즉시 보고. **없음: Find by Binding/Component, 화면 간 전역 조회** |
| 55 | Scenario Recorder·Replay | 목표 기능 | Partially | A(실행) | **Replay 구현됨.** `NexScenario`(fluent, Unity 비의존 데이터) + `NexScenarioRunner`(호출자 구동, poll 기반) + `INexScenarioWorld` 포트 + uGUI 어댑터. 단계: Find/Click/SetState/WaitUntil/Assert{Visible,Hidden,Text,State,NoErrors}. 실패는 `NEX-TEST-9xxx` 진단 + Flow Trace 형식 리포트. **없음: Recorder, 시나리오 직렬화, 실제 입력 장치 Replay, Screenshot·Performance 단언, `Open(Screen)`.** 기존 `ScenarioRecorder`(데이터 시나리오)는 별개 기능 |
| 56 | Regression Test | 일부 CI 기반 | Partially | A | EditMode/PlayMode 테스트 어셈블리 4개 존재. 이번 세션에 Determinism 테스트 추가. **Screenshot·Flow·Performance·IL2CPP·Stripping 회귀 없음.** `.github/` 존재하나 CI 내용 미확인 |

### Part 14. Compiler와 Backend Output

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 57 | UI Compiler | 장기 핵심 구조 | Partially | A(실행) | **신규 구현.** `NexScreenCompiler`(Normalize→Validate→Lower→Hash), `NexScreenProgram`(IR), `NexScreenBuildPipeline`. **9단계 파이프라인 중 4단계만.** Resolve·Expand Components·Optimize·Strip 없음. Node Kind 4종(Panel/Image/Label/Button)뿐이라 Binding Table·Interaction/Navigation/Motion Program·Shader Manifest 미생성 |
| 58 | uGUI Backend | 현재 부분 지원 | Partially | B | 두 경로 병존: 기존 `UGUIAssetSerializer`(57KB)+`UGUIControlFactory`+`StudioPrefabImporter`(Prefab 저장/불러오기), 신규 `NexUGuiScreenBuilder`(Compiled Runtime 생성) |
| 59 | UI Toolkit Backend | 일반 Save 지원 | Partially | B | `UIToolkitAssetSerializer`, `UIToolkitCodeGenerator`, `UIToolkitGenerationOptions`, `.g.uxml/.g.uss` 생성. **Compiled 경로 Backend는 없음** |
| 60 | Custom Canvas Backend | 장기 목표 | **Not Implemented** | C | (v3-final §2.5의 "자체 렌더러 금지"와는 별개 항목 — CanvasRenderer 기반 배칭은 금지 대상 아님) |
| 61 | Backend Capability·Fallback | 일부 기반 | Partially | B | `DesignerBackendSupport`, `CapabilityInspector`, `backend-support-matrix.md`. 6종 Fallback 정책(Preferred/Reduced/Static/Baked/Omit/Fail Build)은 없음 |
| 62 | Atomic Publish | 일부 구현 | Partially | A(실행) | **신규 구현.** `NexScreenPublisher`가 temp→backup→swap과 실패 시 복원, 크래시 잔여물 정리까지 수행. 기존 `GeneratedAssetWriter`는 별도 경로 |

### Part 15. Build와 Performance

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 63 | Feature Manifest·Build Stripping | 목표 기능 | Partially | A | **Manifest만 신규 구현** — `NexFeatureManifest`가 5개 Feature를 "포함 이유"와 함께 기록. **실제 Stripping은 전무** (Assembly·Shader Variant·Debug Symbol 제거 없음) |
| 64 | Build Report | 목표 기능 | Partially | A | **신규 구현.** `NexBuildReport`가 Feature 포함 이유·Node 집계·Diagnostics를 Markdown으로 `Library/NexUI/Reports/`에 기록. Shader·Memory·Asset 항목 없음 |
| 65 | Performance Center | 목표 기능 | Partially | B | `UIProfilerService`, `BindingProfilerService`, `MotionBudgetService`, `UIMotionBudget`. Static Estimation 13항목·Budget 계층 없음 |
| 66 | Instrumentation Overhead | 목표 기능 | Partially | A | **신규.** `NexFlowTrace.OverheadMs`가 tracer 자체 비용 누적. Sampling·자동 Detail 감소·측정 왜곡 경고 없음 |

### Part 16. Text, Localization과 Accessibility

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 67 | Typography | 부분 지원 | Partially | B | TMP 기반, `DesignerTypographyMetadata`, `NXTextEffects`. Text on Path·Ruby·Vertical Text 없음 |
| 68 | Text Measurement | 목표 기능 | **Not Implemented** | C | `FontGlyphService`(글리프 존재 검사)만 있고 **Shaping·Line Breaking·Measurement 계층 자체가 없음.** Editor/Runtime 측정 차이 보고 없음 |
| 69 | Localization | 부분 지원 | Partially | B | Studio UI 자체 번역(`DesignerLocalizationTable`, ko-KR/en-US), 게임 측 `UIGameLocalizationTable` + `GameLocalizationService/Window`, `DesignerLocalizationMetadata`. Pseudo Localization·Overflow 검사 미확인 |
| 70 | Accessibility | 부분 지원 | Partially | B | `AccessibilityRole`, `UIAccessibilityPreference`, `AccessibilityService`, `DesignerAccessibilityAudit`, `AccessibilityWindow/Inspector`, `ContrastService`, `ThemeContrastChecker`. **`DesignerElementMetadata`에 `accessibilityLabel`·`accessibilityRole` 필드 실재.** Focus Order·Screen Reader Metadata는 미확인 — **v3-final §4가 법적 요구사항으로 격상한 영역** |

### Part 17. Runtime 시스템

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 71 | Screen Lifecycle | 목표 기능 | Partially | B | `IUIScreenLifecycle`, `UIScreenInstance`, `UIScreenLoadStrategy`, `UIManager`. Scope·Warmup·Pool은 미확인 |
| 72 | Resource Ownership | 목표 기능 | **Not Implemented** | C | Reference Counting·Ownership Token·Leak Detection 0건. Addressables 통합은 있으나 수명 관리 계층 없음 |
| 73 | Runtime Override | 목표 기능 | Partially | A | `NexOverrideLedger`가 노드 속성(Text·Visible)의 **마지막 기록자**를 Source(Binding/Interaction/GameCode)와 Origin(바인딩 키·규칙 id·사유)까지 기록. `Explain()`이 authored 값과 나란히 설명 문자열 생성 — **§52 Why Debugging의 "누가 이 값을 바꿨나"가 실제로 답변됩니다.** 게임 코드용 `runtime.SetText/SetVisible(reason:)` 진입점 제공. **없음: Text·Visible 외 속성, State 값 출처, Theme/Motion 레이어(Compiled 경로에 미연결), Override 우선순위·수명 정책, Editor UI** |
| 74 | Time Source | 목표 기능 | Partially | A | `INexTimeSource` + `NexScaledTime`/`NexUnscaledTime`/`NexManualTime` + 교체 가능한 `NexTime.Default`. `double` 초 단위(장시간 세션 정밀도), `Now`는 단조 증가하고 `SeekTo`만 되감기 허용(Timeline scrub). **Scenario `WaitForSeconds`와 Interaction `Delay`가 이걸 사용** — 실제로 기다리지 않고 결정론적으로 검증됩니다. **없음: Motion 재생은 여전히 엔진 시계 직접 사용** |

### Part 18. 확장성과 AI

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 75 | Extension SDK | 목표 기능 | Partially | B | 확장 지점 일부 실재: `IUIDesignerCommand`, `INexUIDesignerTool`, `INexUIDesignerBackend`, `IDesignerAssetSerializer`, `DesignerInspectorRegistry`, `IUIValidator`, `IUIGraphNodeExecutor`. **Extension ID·Semantic Version·Exception Isolation·Safe Mode·Generated Runtime Registry 전무.** v3-final §2.4의 DOTween Adapter는 `Integrations/DOTween`(3파일)로 **이미 존재** |
| 76 | AI Assistant | Beta | Partially | B | `NexUIAIActionService`(66KB), `OpenAIResponsesProvider`, `NexUIAIContextBuilder`, `NexUIAIWindow`. 계획 검증·명시적 Apply·단일 Undo 구조 존재 |

### Part 19. 호환성, Migration과 복구

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 77 | Unity Version Compatibility | — | **감사 제외** | — | v3-final §2.1 |
| 78 | Migration | 현재 지원·Beta | Partially | B | `emiteat.NexUI.Editor.Migration` 어셈블리, `DesignerHierarchyMigration`, `DesignerMetadataAsset.schemaVersion = 6`, `.bak` 생성. **Unknown Field·Extension Data 보존은 미확인** — v3-final의 "기존 호환 불필요" 결정으로 우선순위 하락 |
| 79 | Downgrade Export | — | **감사 제외** | — | v3-final §2.1 |
| 80 | Editor Lifecycle와 Recovery | 일부 기반 | Partially | B | `DesignerUIState` + EditorPrefs 복원. **Autosave·Crash Recovery·Domain Reload 대응·Background Job 취소는 미확인 또는 부재** |
| 81 | NexUI Doctor | Setup Doctor 기반 | Partially | B | `NexUISetupDoctorWindow`, `CleanerService`. Diagnostic Bundle·Index 재생성·Backup 복구 없음 |

### Part 20. 제품 운영 기능

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 82 | Local-first 정책 | 제품 원칙 | Partially | A | 원칙은 지켜지는 중 — 외부 통신은 AI Provider와 Figma API 둘뿐이고 둘 다 선택적. 사용자 통계 Export는 없음 |
| 83 | Diagnostic Bundle·개인정보 | 목표 기능 | **Not Implemented** | C | 번들 생성·민감 정보 자동 제거 없음. `FigmaCredentials`가 토큰을 다루므로 **번들 구현 시 반드시 제거 대상** |
| 84 | 제품 Edition | 제품화 목표 | **Not Implemented** | C | Free/Pro 기능 게이트 코드 없음. v3-final §5.3은 Asset Store Lite Edition 메커니즘 사용으로 결정 — 커스텀 인증 불필요 |

### Part 21. 비기능 요구사항

| § | 기능 | v3-draft | 재판정 | 증거 | 비고 |
|---|---|---|---|---|---|
| 85 | 성능 | 비기능 요구 | Partially | **A(실행 검증됨)** | **Content Hash Cache**(`NexScreenPublisher.Decide`) + **Dirty Range**(`NexDocumentRevision`) + **Dependency Graph**(`NexDependencyGraph`) + **요소별 이슈 캐시**(`NexValidationCache`). 관련 테스트 42개 실행·전부 통과. **Validation 호출 합치기는 구현했다가 되돌렸습니다** — `UndoBackToBaseline`이 172초 걸리고 실패했고 메커니즘을 설명하지 못했습니다. Preview·Component 전개는 전체 재계산. Incremental Compile·Selective Repaint·Background Job 없음 |
| 86 | 안정성 | 비기능 요구 | Partially | A | Stable ID(`stableId`) ✓, Atomic Publish ✓(신규), Deterministic Serialization ✓(Compiled 한정, 테스트 있음). **Autosave·Crash Recovery·Extension Isolation·Safe Mode·Unknown Data 보존 ✗** |
| 87 | 사용성 | 비기능 요구 | Partially | B | Beginner/Pro 모드, Inline Validation, Save Preview(적용 전 미리보기), Undo 존재. Disabled Reason·Offline Documentation 미확인 |
| 88 | 테스트 가능성 | 비기능 요구 | Partially | A | 신규 테스트 다수(Determinism·Publish Decision·Incremental·ViewState 등). **God Object 분해 진행 중** — `NexUIDesignerContext`에서 무효화 책임(`NexDocumentRevision`)과 창 배치 상태(`DesignerViewState`, 9개 값)를 분리. 본체는 1750줄로 여전히 최대 파일이며 `NexUIDesignerViewport`(77KB)는 손대지 않음. **핵심 성과는 검증 경로 확보** — Designer.Editor 전체 310파일을 Unity 없이 컴파일 검증 가능. Golden Document·Screenshot·IL2CPP·Stripping 테스트 없음 |

### Part 22. 핵심 사용자 흐름

| § | 흐름 | 재판정 | 증거 | 끊기는 지점 |
|---|---|---|---|---|
| 89 | 신규 화면 제작 | Partially | A | Interaction 단계의 단절은 해소됨(§30). 남은 단절은 **Motion 적용**(Compiler가 Motion을 lower하지 않음)과 **Scenario 확인**(Studio Preview가 Interaction 규칙을 시뮬레이션하지 않음) |
| 90 | 기존 프로젝트 도입 | Partially | B | `StudioPrefabImporter` 존재. TMP·UnityEvent 보존은 `StudioUnityEventModel/Row`로 다뤄지나 Import 후 Compile 왕복은 미검증 |
| 91 | 오류 분석 | Partially | A | 조각은 신규로 갖춰짐(Error Code→Flow→Source Map). **"Authoring Node 선택"으로 이동시키는 Editor UI가 없어 마지막 단계에서 끊김** |
| 92 | 성능 최적화 | **Not Implemented** | C | Static 분석·Rebuild/Overdraw 추적·품질 Fallback·Stripping·재측정 어느 것도 없음 |

---

## 3. 런타임 확인이 필요한 항목 (등급 B → A 승격 조건)

아래는 **코드가 있다는 것만 확인된** 항목 중, 실제로 Broken일 경우 다른 작업을 막을 위험이 큰 순서입니다.

1. **§26 Binding** — Two-way와 Converter가 실제 Runtime에서 동작하는지. v3-draft 자신이 재검증을 요청했고, §89 흐름 전체가 여기 의존합니다.
2. **§29 Collection Virtualization** — 대량 데이터에서 실제로 재활용되는지. 성능 주장(§65)의 근거가 됩니다.
3. **§58/59 Backend 저장 왕복** — 저장 → 다시 열기 → 동일 결과인지. 데이터 손실 위험이 가장 큰 지점입니다.
4. **§35 Motion Clip Runtime 재생** — Editor Preview와 Player 결과 일치 여부.
5. **§78 Migration** — schemaVersion 0~6 자산이 실제로 열리는지.
6. **테스트가 실행되었습니다** (2026-08-04~05).
   EditMode 731개 중 696 통과 / 35 실패, **PlayMode 전부 통과**.
   이번 세션에 추가한 **138개(EditMode 90 + PlayMode 48)는 전부 통과**했습니다.
   35개 실패는 모두 기존 테스트이며, 34개는 CollectionView 현지화 누락·에디터 스크립트 `AddComponent`
   테스트 인프라 문제 등 이번 작업과 무관합니다. 나머지 1개(`UndoBackToBaseline`)는 이번 변경이
   원인이었고 되돌렸습니다.

   따라서 아래 표의 등급 A 중 상당수는 이제 **"코드를 읽어 확인"이 아니라 "실행해서 확인"** 입니다.
   Compiler Version은 1 → 3으로 올랐습니다(Interaction, Automation ID). 이전에 Publish한 자산은 재컴파일이 필요합니다.
   `NexUIDesignerContext`를 8군데 수정했고, 그중 **지연 Validation(타이밍)과 이슈 캐시(정확성)** 는
   컴파일러가 원리상 잡아줄 수 없는 종류입니다. 다음 두 가지를 눈으로 확인하기 전에는 Validation 결과를 신뢰하지 마십시오:
   요소를 드래그한 뒤 패널이 한 틱 뒤에 갱신되는가, 그리고 **의도적으로 오류를 만든 요소가 실제로 목록에 나타나는가.**
   §30·§48·§50·§53·§57·§62·§64의 A 등급은 "코드를 읽고 확인"이지 "실행해서 확인"이 아닙니다.
7. **Compiler Version이 1에서 2로 올라갔습니다** (Interaction 필드 추가). 이전에 Publish한 `NexScreenProgram` 자산이 있다면
   `NEX-RT-6003`으로 거부되며 재컴파일이 필요합니다.

---

## 4. 가장 비싼 격차 5개

우선순위는 "없으면 다른 것을 막는가"로 매겼습니다.

| 순위 | 격차 | 왜 비싼가 |
|---|---|---|
| ~~1~~ | ~~**§30 Interaction Authoring 부재**~~ | **해소됨 (Vertical Slice 2).** v3-draft가 "부분 지원"으로 표기했으나 실제로는 0이었습니다. **v3-draft 라벨을 믿고 다른 기능을 진행했다면 정확히 v3-final §1.3이 경고한 "제일 비싼 실패"가 났을 지점입니다.** 남은 Trigger·Condition·Action 확장은 이제 격차가 아니라 증분 작업입니다 |
| 1 | **§85 — 구현됐으나 실행 검증 0** | Dirty Range·Dependency Graph·Hash Cache·호출 합치기·규칙 분리·**요소별 이슈 캐시**까지 전부 들어갔습니다. 남은 위험은 기술적 격차가 아니라 **검증 부재**입니다: 이슈 재사용이 틀리면 실제 오류가 Validation 패널에서 사라지는 형태로 나타나고, 이건 사용자가 "검증 통과"를 믿고 빌드하는 지점입니다. `NexValidationCacheTests` 12개가 무효화 조건을 덮지만 **한 번도 실행되지 않았습니다** |
| 3 | **§73 Runtime Override + §74 Time Source 부재** | 각각 §52 Why Debugging과 Deterministic Replay(§55)의 선행 조건. 없으면 그 위 기능을 아예 시작할 수 없습니다 |
| 4 | **§88 God Object — 분해 시작됨** | `NexUIDesignerContext`에서 두 책임(변경 추적·창 배치)을 뺐지만 본체는 여전히 1750줄이고 `NexUIDesignerViewport`(77KB)·`NexUIComponentsPanel`(66KB)·`NexUIAIActionService`(66KB)는 그대로입니다. 다음 후보는 **선택(Selection)** — `_selection`·`_clipboard`·KeyObject와 15개 메서드가 한 덩어리이고 문서 편집과 거의 무관합니다 |
| 5 | **§63 Build Stripping 부재** | v3-final §3의 "Pay for What You Use" 원칙이 코드로 존재하지 않습니다. Manifest는 생겼으나 그것을 소비하는 쪽이 없어 현재는 보고용입니다 |

---

## 5. v3-final 결정이 이 감사에 미친 영향

| v3-final 항목 | 감사 결과와의 관계 |
|---|---|
| §2.1 Unity 2022.3 + 6.x | §77·§79 제외. 감사 중 **실제 파손 1건 발견 — `NexUGuiScreenBuilder`가 2023.1+ 전용 `FindAnyObjectByType` 사용**. 이번에 버전 분기로 수정했으며, 이것이 Compiled 파이프라인의 유일한 `#if`입니다 |
| §2.2 URP만 | §40 Filter 구현 시 적용. 현재 Shader 코드 자체가 없어 제거할 분기도 없음 |
| §2.3 Figma JSON 우선 | §25를 "API 경로 존재 / JSON 경로 부재"로 분리 판정 |
| §2.4 DOTween Adapter | **이미 존재** (`Integrations/DOTween` 3파일). 신규 개발이 아니라 승격·문서화 대상 |
| §3 Pay for What You Use | Flow Trace가 이 원칙을 코드로 지킨 첫 사례(Off일 때 할당 0). §63 Stripping은 미구현 |
| §4 Accessibility 격상 | §70이 예상보다 기반이 있음 — 문서 모델에 `accessibilityRole`·`accessibilityLabel` 필드가 이미 존재. Focus Order·Contrast Validation 연결이 남은 작업 |
| §5 배포 | §84 Edition 게이트 코드가 없는 것이 오히려 유리 — Lite Edition 메커니즘은 코드 게이트를 요구하지 않음 |

---

## 6. 다음 단계 제안

v3-final §8 우선순위에 감사 결과를 반영하면:

1. **§30 Interaction Authoring** — 가장 비싼 격차이자 §89 흐름의 유일한 단절점. Vertical Slice 2의 후보
2. **Vertical Slice 1 테스트 실행** — 24개 테스트가 미실행 상태. A 등급 판정의 근거를 실제로 만드는 일
3. **§85 Incremental** — Compiler가 작을 때 넣어야 쌈
4. **§88 God Object 분해** — 위 셋을 모두 쉽게 만드는 선행 작업

---

*이 문서는 코드 변경마다 갱신되어야 합니다. 특히 판정이 B에서 A로 올라갈 때(실행 확인 시)와,
Not Implemented가 Partially로 바뀔 때 해당 행을 수정하십시오.*
