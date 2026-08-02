# 자주 사용하는 작업

## 여러 Element를 한 번에 선택하기

### 사용하는 상황

여러 Button을 함께 이동하거나 정렬할 때 사용합니다.

### 작업 순서

Canvas에서 Shift/Ctrl 클릭하거나 빈 공간에서 Box Selection을 드래그합니다. Layers에서도 Shift 범위 선택과 Ctrl/Command 토글 선택을 사용할 수 있습니다.

### 정상 결과

Right Inspector 제목에 선택 개수가 표시되고 선택 Overlay가 여러 Element에 나타납니다.

### 자주 발생하는 문제

Interactive Mode에서는 편집 선택 대신 상호작용이 시뮬레이션됩니다. Design Mode로 전환하세요.

### 관련 문서

[Canvas 편집](canvas-editing.md), [단축키](../reference/shortcuts.md)

## 여러 Element를 일정 간격으로 정렬하기

### 사용하는 상황

세 개 이상의 항목 간격을 균등하게 만들 때 사용합니다.

### 작업 순서

세 Element 이상을 선택하고 Align Panel의 `⋯H` 또는 `⋮V`를 누르거나 `Alt+H/V`를 사용합니다. 특정 Element를 정렬 기준으로 삼으려면 Layers/Canvas Context Menu에서 **Set Key Object**를 먼저 선택합니다.

### 정상 결과

한 번의 Undo 단계로 위치가 바뀌고 Key Object는 이동하지 않습니다.

### 자주 발생하는 문제

두 Element만 선택하면 분배 버튼이 비활성화됩니다. Key Object가 없으면 선택 Bounding Box를 기준으로 정렬합니다.

### 관련 문서

[Hierarchy와 Layout](hierarchy-and-layout.md)

## 부모·자식 관계 만들기

### 사용하는 상황

Auto Layout, Modal Slot 또는 Container 이동을 함께 처리할 때 사용합니다.

### 작업 순서

Layers에서 자식 행을 대상 Container 아래로 드래그하거나 Canvas Context Menu의 Reparent 기능을 사용합니다. Slot을 지원하는 Component에서는 Inspector의 Parent Slot도 확인합니다.

### 정상 결과

Layers에 들여쓰기가 나타나고 `parentId`가 갱신됩니다. Canvas의 절대 위치는 가능한 한 유지됩니다.

### 자주 발생하는 문제

Label처럼 자식을 허용하지 않는 Component 아래로 옮기면 Validation의 `leaf-with-children`이 발생할 수 있습니다.

### 관련 문서

[Hierarchy와 Layout](hierarchy-and-layout.md), [Validation Catalog](../reference/validation-catalog.md)

## 세로 Auto Layout 구성하기

### 사용하는 상황

메뉴 Button이나 설정 Row를 일정 간격으로 배치할 때 사용합니다.

### 작업 순서

Container를 선택하고 Inspector의 **Build > Auto Layout**에서 Enabled, `Column`, Spacing과 Padding을 설정합니다. 자식을 해당 Container 아래로 Reparent하고 형제 순서를 정합니다.

### 정상 결과

Preview가 세로 흐름으로 바뀌고 Save 시 uGUI는 `VerticalLayoutGroup`, UI Toolkit은 Column Flex 규칙을 사용합니다.

### 자주 발생하는 문제

자식이 부모 아래에 없거나 Layout이 비활성화되어 있으면 변화가 없습니다. 복잡한 Constraints와 혼용한 결과는 Play Mode에서 확인하세요.

### 관련 문서

[Layout 변환과 Anchor](layout-conversion-and-anchor.md)

## Layer 순서 바꾸기

### 사용하는 상황

Dim, Popup, Tooltip의 그리기 순서를 조정할 때 사용합니다.

### 작업 순서

선택 후 Align Panel의 Fwd/Back/Front/Bottom 또는 `Ctrl+]`, `Ctrl+[` 계열 단축키를 사용합니다.

### 정상 결과

같은 부모 아래 `siblingIndex`와 Backend 형제 순서가 바뀝니다.

### 자주 발생하는 문제

부모가 다른 Element끼리는 동일 형제 순서가 아닙니다. 먼저 Reparent를 확인하세요.

### 관련 문서

[Hierarchy와 Layout](hierarchy-and-layout.md)

## Sprite를 Element에 넣기

### 사용하는 상황

아이콘이나 배경 이미지를 지정할 때 사용합니다.

### 작업 순서

Left Sidebar의 **Assets** 탭에서 필터를 `Image`로 두고 Sprite를 찾습니다. 대상 Element 위로 드래그하거나, 빈 Canvas에 드래그해 새 Image Element를 만듭니다. Inspector의 Object Field로 드래그해도 됩니다.

### 정상 결과

드래그 중 커서 옆에 무슨 일이 일어날지 표시되고, 드롭하면 한 번의 Undo로 되돌릴 수 있는 변경이 적용됩니다.

### 자주 발생하는 문제

Texture Type이 `Sprite`가 아니면 Sprite sub-asset이 없어 드롭이 적용되지 않고 Preview Log에 이유가 남습니다. 정의된 동작이 없는 Asset은 커서에 금지 표시가 뜹니다.

### 관련 문서

[Assets 패널](assets-panel.md)

## 반복되는 UI를 재사용 Component로 만들기

### 사용하는 상황

카드, 슬롯, 설정 Row처럼 같은 구조를 여러 번 쓰고 한 곳에서 관리하고 싶을 때 사용합니다.

### 작업 순서

Component의 root가 될 Element를 선택하고 `Tools > NexUI > Studio > Component Library`에서 저장 폴더를 확인한 뒤 **Create Component From '...'** 을 누릅니다. 이후 같은 창의 **Place Instance** 또는 Assets 탭에서 Definition을 Canvas로 드래그해 배치합니다.

### 정상 결과

원본 subtree가 Instance 하나로 접히고 Canvas 모양은 그대로입니다. Definition을 고치면 모든 Instance가 즉시 따라옵니다.

### 자주 발생하는 문제

Instance만 다르게 하고 싶으면 Inspector의 **Component Instance** 섹션에서 Exposed Property를 Override합니다. Definition Asset을 지우면 Instance는 남고 `component-definition-missing` Error가 뜹니다 — 내용을 지키려면 **Detach**하세요.

### 관련 문서

[재사용 Component](../advanced/reusable-components.md)

## Button에 Command Key 연결하기

### 사용하는 상황

Button 클릭을 Runtime Action에 연결할 때 사용합니다.

### 작업 순서

Button을 선택하고 Inspector의 **Connect > Binding > Command Key**에 문자열을 입력하거나 Picker를 사용합니다. Runtime에서 같은 Key를 `UIActionResolver.Register`로 등록합니다.

### 정상 결과

Validation의 `button-without-command`가 사라지고 Play Mode 클릭 시 등록 Action이 실행됩니다.

### 자주 발생하는 문제

Interactive Preview는 Command를 실행하지 않고 Preview Log에만 남깁니다.

### 관련 문서

[Binding](binding.md)

## Text Binding 연결하기

### 사용하는 상황

사용자 이름, 아이템 이름이나 수량 문자열을 상태에서 표시할 때 사용합니다.

### 작업 순서

Label을 선택하고 Inspector의 **Connect > Binding > Text Key**를 설정합니다. Runtime Surface의 같은 Element Handle에 `UITextBinder`를 Bind하고 `UIStateStore.Set`으로 값을 공급합니다.

### 정상 결과

Play Mode에서 State 변경 즉시 Text Capability가 갱신됩니다.

### 자주 발생하는 문제

UXML `name`, Prefab 오브젝트 ID와 Metadata `elementId`가 다르면 대상 Handle을 찾지 못합니다.

### 관련 문서

[Binding](binding.md)

## Hover, Pressed, Selected 상태 확인하기

### 사용하는 상황

상호작용 스타일이나 Motion을 게임 실행 전에 확인할 때 사용합니다.

### 작업 순서

Canvas Toolbar의 State Popup으로 상태를 강제하거나 Interactive Mode로 전환합니다. Bottom Drawer의 Preview 탭을 함께 엽니다.

### 정상 결과

Component Preview가 상태별 표현을 사용하고 상호작용이 Log에 기록됩니다.

### 자주 발생하는 문제

Preview State는 Runtime 상태를 변경하지 않습니다. Backend USS/Selectable 설정이 별도라면 Play Mode 결과가 다를 수 있습니다.

### 관련 문서

[Preview와 Scenario](preview-and-scenarios.md)

## Motion Clip 열기와 Popup 전환 구성하기

### 사용하는 상황

Element Track이나 Screen Entry/Exit 효과를 만들 때 사용합니다.

### 작업 순서

Element를 선택하고 Inspector의 **Animate > Motion**에서 Motion Clip Editor를 열거나 `Tools > NexUI > Studio > Advanced > Motion Clip Editor`를 사용합니다. Popup은 Entry/Exit Clip 또는 Transition Preset을 지정합니다.

### 정상 결과

Scrub/Play 시 Studio Surface에 변화가 보이고 Metadata `screenMotion`에 Asset 참조가 저장됩니다.

### 자주 발생하는 문제

Track `targetElementId`가 실제 Element ID와 같아야 합니다. 끊어진 대상은 Validation Error입니다.

### 관련 문서

[Motion Clip](../motion/motion-clip-editor.md), [Motion 레시피](../motion/recipes.md)

## Scenario 적용하기

### 사용하는 상황

빈 문자열, 긴 Text, Loading, Visibility false 같은 데이터 조건을 반복 확인할 때 사용합니다.

### 작업 순서

Global Toolbar의 Scenario 필드에 Asset을 지정하거나 Scenario Editor에서 **Apply**합니다.

### 정상 결과

해당 Key를 사용하는 Metadata Preview 값이 한 Undo 그룹으로 바뀝니다.

### 자주 발생하는 문제

Scenario의 Screen ID는 안내 정보이며 다른 화면에도 Apply할 수 있습니다. Apply Report와 현재 Metadata를 확인하세요.

### 관련 문서

[Preview와 Scenario](preview-and-scenarios.md)

## Validation으로 문제 찾기

### 사용하는 상황

Save/Publish 전 참조와 Backend 문제를 점검할 때 사용합니다.

### 작업 순서

Global Toolbar의 **Validate**를 누르고 Bottom Drawer의 Validation을 엽니다. Issue를 선택하고 제공되는 Fix가 안전한지 검토한 후 실행합니다.

### 정상 결과

Global Toolbar 상태와 Error/Warning 개수가 갱신됩니다.

### 자주 발생하는 문제

Auto Fix가 없는 규칙도 많습니다. 게임 코드의 Runtime Key 존재 여부는 Studio가 완전히 검증할 수 없습니다.

### 관련 문서

[Validation과 Save](validation-and-save.md), [Validation Catalog](../reference/validation-catalog.md)

## uGUI Prefab에 저장하기

### 사용하는 상황

Metadata의 Rect, 부모, Text, Tint와 지원 Component를 연결 Prefab에 적용할 때 사용합니다.

### 작업 순서

uGUI Screen과 Metadata를 연결하고 Validate 후 Save합니다. Save Report의 Changed/Skipped를 확인하고 Prefab을 엽니다.

### 정상 결과

Prefab 계층과 지원 Component가 갱신됩니다. 사용자 작성 Component는 무조건 삭제하지 않습니다.

### 자주 발생하는 문제

Backend Asset이 Prefab이 아니거나 읽기 전용이면 Metadata만 저장되고 Prefab은 건너뜁니다.

### 관련 문서

[uGUI Backend](ugui-backend.md), [Asset Ownership](../reference/asset-ownership.md)

## UI Toolkit 생성 파일 Publish하기

### 사용하는 상황

Metadata에서 `.g.uxml/.g.uss`를 만들거나 변경된 화면만 Publish할 때 사용합니다.

### 작업 순서

UI Toolkit Generation 또는 Sync/Publish Window에서 먼저 Dry Run과 Diff를 확인합니다. Generated Marker와 대상 경로를 확인한 뒤 **Use Studio/Publish**를 실행합니다.

### 정상 결과

두 파일이 함께 기록되고 필요한 Asset만 Import되며 Publish Manifest의 Hash가 갱신됩니다.

### 자주 발생하는 문제

Marker 없는 파일은 사용자 파일로 간주되어 덮어쓰기를 거부합니다. BackendChanged/Conflict를 자동 Merge하지 않습니다.

### 관련 문서

[Sync와 Publish](../advanced/sync-and-publish.md), [Asset Ownership](../reference/asset-ownership.md)
