# NexUI Designer 확장 API

이 문서는 주요 확장 지점을 요약합니다. 네임스페이스 루트는 `emiteat`입니다.

## Editor Window

### `emiteat.NexUI.Designer.Editor.NexUIDesignerWindow`

메인 Unity Editor 창입니다. (네임스페이스는 `emiteat.NexUI.Designer.Editor` 이며 `.Core` 하위가 아닙니다.)

담당 역할:

- 전체 레이아웃 소유
- 활성 `NexUIDesignerContext` 보관
- 툴바, 계층, 뷰포트, 인스펙터, 검증, 상태, 커맨드, 그래프 패널 연결
- rebuild, validation, save 실행

### `emiteat.NexUI.Designer.Editor.NexUIDesignerContext`

현재 디자이너 세션의 공유 상태입니다.

대표 데이터:

- 선택된 `UIScreenDefinition`
- 선택된 메타데이터 에셋
- 활성 백엔드
- 현재 선택
- 검증 결과
- 프리뷰 모드

## Backend

### `emiteat.NexUI.Designer.Editor.Backend.INexUIDesignerBackend`

UI 구현을 디자이너에 연결하기 위한 백엔드 계약입니다.

새 화면 렌더링 경로를 지원할 때 이 인터페이스를 구현합니다.

기대 역할:

- 화면 지원 여부 보고
- 프리뷰 생성 또는 갱신
- 계층 항목 노출
- 선택된 요소 해석
- serializer와 저장 흐름 협력

## Serialization

### `emiteat.NexUI.Designer.Editor.Serialization.IDesignerAssetSerializer`

백엔드별 구현이 사용하는 저장 계약입니다. `Save(UIScreenDefinition, DesignerMetadataAsset)`는 무엇이 디스크에 기록되었고 무엇이 프리뷰 전용으로 건너뛰어졌는지를 담은 `DesignerSaveReport`를 반환합니다.

- `UIToolkitAssetSerializer` — 대상 UXML이 **Generated Marker를 가진 경우에만** UXML/USS를 다시 씁니다. Marker가 없는 사용자 작성 파일은 companion-save 모드로 동작해 메타데이터만 저장하고 메타데이터와 UXML 트리의 불일치를 **검증·보고**만 합니다. 사용자 파일의 구조 편집은 UI Builder 책임입니다.
- `UGUIAssetSerializer` — 프리팹 기반 저장. `PrefabUtility.LoadPrefabContents → SaveAsPrefabAsset → UnloadPrefabContents` 패턴으로 RectTransform/텍스트/틴트/Button 등 디자이너 소유 데이터를 프리팹에 반영합니다. Element는 `stableId`로 Prefab Object에 연결됩니다.
- `DesignerSerializerRegistry.Get(backend)` — 백엔드에 맞는 serializer를 반환합니다.

`DesignerSaveReport`는 `Changed`, `Skipped`, `Warnings`, `Errors` 리스트와 `Summary()`, `Details()`를 제공하며, Save Preview용으로 Create/Modify/Skip/Unsupported/PreviewOnly/Conflict/Orphan/UserImpact 분류(`DesignerSaveImpactKind`)를 지원합니다.

> Serializer는 **전개된(Expanded)** Metadata를 받습니다. 화면에 Component Instance가 있으면 `NexUIDesignerContext.Save`가 `DesignerComponentExpander`로 평탄화한 사본을 넘기고, 원본 Metadata는 별도로 저장합니다. Serializer 구현은 이 사본을 수정하지 않는다고 가정해도 됩니다.

## Metadata

### `emiteat.NexUI.Designer.DesignerMetadataAsset`

디자이너 전용 화면 메타데이터의 루트 에셋입니다. (런타임 안전 메타데이터의 실제 네임스페이스는 `emiteat.NexUI.Designer` 이며, 파일 위치는 `Runtime/Metadata/` 입니다. 이 어셈블리는 `UnityEditor`를 참조하지 않습니다.)

element id, binding, localization link, responsive data, variant, contract, snapshot data 등 제작 메타데이터를 저장합니다. Schema Version과 Migration 규칙은 [Metadata Schema](metadata-schema.md)를 참고하세요.

## 재사용 Component

### `emiteat.NexUI.Designer.Editor.Components.Definitions`

| 타입 | 역할 |
|---|---|
| `DesignerComponentExpander` | Instance를 평탄화한 트리로 전개합니다. `Expand(asset, resolver)`는 `Dispose()`가 필요한 결과를 돌려줍니다. AssetDatabase에 의존하지 않으므로 단위 테스트가 가능합니다 |
| `IDesignerComponentDefinitionResolver` | GUID 또는 `componentId`로 Definition을 찾는 계약. 테스트에서 대체 구현을 주입합니다 |
| `DesignerComponentLibrary` | 프로젝트 색인, 검색·카테고리·태그·즐겨찾기·사용처, 기본 Resolver 제공 |
| `DesignerComponentService` | 생성/배치/Override/Detach/Swap/Update. 모두 Undo 인식 |

### `emiteat.NexUI.Designer.Editor.Properties.DesignerPropertyApplier`

`Apply(element, propertyId, value)`와 `Read(element, propertyId)`로 Typed Property를 Element Metadata에 읽고 씁니다. Metadata에 표현이 없는 Property는 `false`를 반환하며, 호출자는 이를 **보고해야 하고 추측해서 처리하면 안 됩니다**.

## Asset Drag & Drop

### `emiteat.NexUI.Designer.Editor.UI.Panels.DesignerAssetDropResolver`

`Resolve(payload, target)`가 Canvas에 떨어진 Asset의 동작(`SetSprite`/`SetFont`/`SetMaterial`/`CreateImage`/`PlaceComponent`/`None`)을 결정합니다. 규칙을 한 곳에 모아 두었으므로 새 Payload 타입을 지원할 때는 이 함수와 그 테스트만 고치면 됩니다.

## Validation

### `emiteat.NexUI.Designer.Editor.Validation.DesignerValidationService`

`Validate(UIScreenDefinition, DesignerMetadataAsset)`는 구조화된 `DesignerValidationIssue` 리스트를 반환합니다. 각 이슈는 `Severity(Info/Warning/Error)`, 안정적인 `Code`, `ScreenId`, `ElementId`, 사람이 읽는 `Message`, 제안 `Fix`를 담습니다.

Code의 전체 목록과 각각의 발생 조건·해결 방법은 [Validation Catalog](../reference/validation-catalog.md)에 있습니다. 이 문서에 목록을 복제하지 마세요 — 두 곳이 어긋나면 어느 쪽이 맞는지 알 수 없게 됩니다.

새 규칙을 추가하는 방법은 [Validation 추가](adding-validation.md)를 참고하세요. Code는 한 번 릴리스되면 안정적으로 유지해야 합니다.

검증 패널에서 `ElementId`가 있는 이슈를 클릭하면 해당 요소가 선택됩니다.

## Panels

패널은 창에서 context를 받아 UI를 그리는 에디터 모듈입니다. 패널은 작게 유지하고, 복잡한 동작은 서비스로 분리하는 것을 권장합니다.

주요 패널:

| Shell (`Editor/UI/Shell`) | Sidebar/Drawer (`Editor/UI/Panels`) | 보조 (`Editor/Panels`) |
|---|---|---|
| `NexUIDesignerShell` | `NexUILayersPanel` | `NexUIDesignerValidationPanel` |
| `NexUIGlobalToolbar` | `NexUIComponentsPanel` | `NexUIDesignerHistoryPanel` |
| `NexUICanvasToolbar` | `NexUIAssetsPanel` | `NexUIDesignerScreenGraphPanel` |
| `NexUILeftSidebar` | | `NexUIDesignerStatePanel` |
| `NexUIRightInspector` | | `NexUIDesignerCommandPanel` |
| `NexUIBottomDrawer` | | `NexUIPreviewLogPanel` |
| `NexUICommandPalette` | | `NexUIDesignerToolbar` (legacy) |

Canvas는 `Editor/Viewport/NexUIDesignerViewport`입니다. `NexUIDesignerInspector`는 `[Obsolete]` 호환 이름이며 `NexUIRightInspector`를 그대로 상속합니다 — 신규 코드에서는 사용하지 마세요.

Inspector Section은 패널이 아니라 `DesignerInspectorRegistry`에 등록하는 방식입니다. [Inspector 확장](extending-the-inspector.md)을 참고하세요.

## Services

서비스는 검증, snapshot, diff, contract, cleanup, responsive rule, localization check, profiling, refactoring 같은 재사용 가능한 에디터 로직을 담습니다.

패널 클래스를 키우기 전에, 기능을 작고 명확한 서비스로 분리할 수 있는지 먼저 확인하세요.
