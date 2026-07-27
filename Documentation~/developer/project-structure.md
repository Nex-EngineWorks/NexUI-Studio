# 프로젝트 구조

Assembly는 두 개입니다. `emiteat.NexUI.Designer.Runtime`(= `Runtime/`)은 **`UnityEditor`를 참조하지 않으며**, `emiteat.NexUI.Designer.Editor`(= `Editor/`)는 Editor 전용입니다. `Editor/Figma`만 별도 Assembly(`emiteat.NexUI.Integrations.Figma`)로 분리되어 있습니다.

| 경로 | 책임 | 대표 코드/주의사항 |
|---|---|---|
| `Runtime/Metadata` | 직렬화 가능한 Designer 데이터 | `UnityEditor` 참조 금지. Schema Version과 Migration 대상 |
| `Editor/Core` | Window, Context, Session, Undo, Hierarchy, Command | Context는 partial class로 분할합니다. UI 전용 기능을 Runtime으로 내리지 않습니다 |
| `Editor/UI/Shell` | Global Toolbar, Sidebar, Right Inspector, Bottom Drawer, Command Palette | 복잡한 데이터 처리를 넣지 않습니다 |
| `Editor/UI/Panels` | Layers, Components, Assets 패널 | Context 이벤트는 `ContextBoundSubscriptions` 사용 |
| `Editor/UI/Controls` | 공용 UI 조각 (Tab Bar 등) | Context에 의존하지 않습니다 |
| `Editor/Panels` | Legacy/보조 Panel (Validation, History, Screen Graph, Preview Log 등) | 신규 Panel은 `Editor/UI/Panels`로 |
| `Editor/Viewport` | Canvas 입력, Overlay, Preview 표현, Asset Drop | Asset 저장 책임을 갖지 않습니다 |
| `Editor/Components` | Component Descriptor와 Backend support matrix | Component Type의 단일 등록 지점 |
| `Editor/Components/Definitions` | 재사용 Component: Library, Expander, Service, 창 | Expander는 AssetDatabase에 의존하지 않습니다(resolver 주입) |
| `Editor/Properties` | Typed Property Registry / Adapter / Applier | `DesignerPropertyId`의 단일 정의 지점 |
| `Editor/Inspectors` | Metadata Field 편집과 Section Registry | 변경은 Context API와 Undo를 거칩니다 |
| `Editor/Backend` | uGUI/UI Toolkit Preview Adapter | Runtime Backend와 Editor Preview를 연결 |
| `Editor/Serialization` | Backend Save, Companion JSON, UXML/USS 생성, Save Preview | 생성과 파일 쓰기를 분리합니다 |
| `Editor/Validation` | 사용자용 구조 검증 | 안정적인 Issue Code 유지 |
| `Editor/Productivity` | 화면 생성 마법사, Auto Fix, Transition Preset 등 | Main Window 구현에 직접 의존하지 않습니다 |
| `Editor/Advanced` | 독립 Tool (Motion Clip/Graph, Scenario, Token, Variant, Responsive, Sync ...) | 가장 큰 폴더입니다. 도구별 하위 폴더를 유지하세요 |
| `Editor/QA` | 분석 도구 (Contrast, Font, Diff, Profiler 등) | 진단 전용. 사용자 데이터를 수정하지 않습니다 |
| `Editor/AI` | AI Assistant (Provider, Context, Action Plan) | 승인 전 Asset을 쓰지 않습니다 |
| `Editor/Tools`, `Editor/Utilities` | Setup Doctor, Utilities 창 등 부가 도구 | |
| `Editor/Figma` | Figma API 인증/조회/Import | 별도 Assembly |
| `Editor/Menu` | Advanced Validation 메뉴 진입점 | |
| `Editor/Common` | Advanced 도구용 IMGUI 창 베이스 | |
| `Editor/Localization` | ko/en UI 문자열 로딩 | API명은 번역하지 않습니다 |
| `Editor/Styles` | USS와 IMGUI 색상 미러 | `NexUIDesigner.uss`와 `DesignerColors.cs`를 함께 갱신 |
| `Localization` | `ko-KR.json`, `en-US.json` | 새 키는 두 파일 모두에 추가 |
| `Tests/EditMode` | EditMode 테스트 | 순수 로직을 우선 테스트합니다 |
| `Samples~` | Import 가능한 Vertical Slice | 제품 Runtime 시스템으로 확장하지 않습니다 |
| `Documentation~` | UPM 문서 | 사용자·Reference·Developer 분리 |
| `Tools~` | 패키지 검증 스크립트 | |

## 새 코드를 어디에 둘지

* 화면 데이터에 저장되는 값 → `Runtime/Metadata` (+ Migration)
* 화면에 보이는 새 Panel → `Editor/UI/Panels` (+ Shell에서 탭 등록)
* Inspector Section → `Editor/Inspectors` (+ `DesignerInspectorRegistry`에 등록)
* Component Type → `Editor/Components/DesignerComponentRegistry` (Switch 문 추가 금지)
* Property → `Editor/Properties/DesignerPropertyRegistry` (+ Applier)
* 독립 창 → `Editor/Advanced/<도구명>`
