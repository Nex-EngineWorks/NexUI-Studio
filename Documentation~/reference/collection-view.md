# CollectionView

목록 형태의 모든 NexUI 컴포넌트가 올라가는 하나의 시스템. `List`, `Grid`, `InventoryGrid`,
`SelectionList`, `InfiniteList`, `VirtualGrid`, 그리고 게임 카탈로그의 수십 개 목록형 항목은 별도
구현이 아니라 **이 컴포넌트의 프리셋**이다. 런타임도 하나(`NXCollectionView` /
`NXCollectionViewElement`)를 공유한다.

> 이 문서는 **현재 코드에 있는 것만** 적는다. 목표 기능은 [남은 작업](#남은-작업)에 따로 둔다.

## 구조

| 계층 | 타입 | 어셈블리 |
|---|---|---|
| 엔진 | `NXCollectionController` | `emiteat.NexUI.Components` |
| 설정 | `NXCollectionOptions` | `emiteat.NexUI.Components` |
| 데이터 공급 | `INXCollectionSource`, `NXCollectionSource<T>` | `emiteat.NexUI.Components` |
| uGUI 어댑터 | `NXCollectionView` (ScrollRect 위) | `emiteat.NexUI.Integrations.UGUI` |
| UI Toolkit 어댑터 | `NXCollectionViewElement` | `emiteat.NexUI.Integrations.UIToolkit` |
| 저장 설정 읽기 | `DesignerCollectionOptions` | `emiteat.NexUI.Studio.Editor` |

엔진에는 UnityEngine 타입이 없다. 가상화 범위 계산·선택·상태 전환이 씬 없이 EditMode에서 검증되는
이유이고(`CollectionControllerTests`), 두 백엔드가 같은 산술을 쓰는 이유이기도 하다.

컨트롤러는 **아이템 데이터를 소유하지 않는다.** 개수와 항목은 `INXCollectionSource`가 공급하고,
뷰 채우기는 바인드 콜백이 한다. 인벤토리·퀘스트·상점 규칙이 UI 계층에 들어오지 않는다.

## 지원 범위

| 기능 | 엔진 | uGUI | UI Toolkit | 비고 |
|---|:--:|:--:|:--:|---|
| Vertical / Horizontal 레이아웃 | O | O | O | |
| Grid (고정 열) | O | O | O | |
| Grid / Wrap (뷰포트 기준 자동 열) | O | O | O | |
| 가상화 None | O | O | O | |
| 가상화 FixedSize | O | O | O | |
| 가상화 DynamicSize | O | O | △ | 행 단위 측정. Grid에서는 행 높이만 |
| 뷰 재사용(풀링) | — | O | O | |
| 선택 없음 / 단일 / 다중 | O | O | O | |
| 범위 선택(Shift) | O | O | O | 수정자 프로브 필요 |
| ScrollTo(index, alignment) | O | O | O | |
| 선택 시 자동 스크롤 | O | O | O | |
| 아이템 활성화 이벤트 | O | O | O | |
| 컨텍스트 요청 이벤트 | O | O | O | |
| Reorder(인덱스 재매핑 + 이벤트) | O | △ | △ | 엔진은 완결. **드래그 UI는 미구현** |
| Content / Loading / Empty / Error | O | O | O | Empty는 개수에서 자동 유도 |
| 무한 페이징 요청 | O | O | O | 개수당 1회 |
| Snap 페이징 | △ | ✗ | ✗ | `SnapIndex()`만 있고 스냅 동작은 미구현 |
| Pagination | ✗ | ✗ | ✗ | 옵션 값만 존재 |
| Drag and Drop | ✗ | ✗ | ✗ | 플래그만 존재. Phase 4 |

O = 동작함, △ = 부분, ✗ = 미구현(옵션은 있으나 동작 없음)

지원하지 않는 조합은 근사하지 않고 보고한다. `NXCollectionOptions.Validate(problems)`가 이유를
문자열로 돌려주고, Studio는 이를 `collection-options-conflict` 이슈로 띄운다. 예: Wrap +
DynamicSize는 Wrap이 균일 셀을 쓰므로 거부된다.

## Studio 속성

`items.*` 키가 그대로 런타임 `NXCollectionOptions`가 된다.

| 키 | 타입 | 런타임 대응 |
|---|---|---|
| `items.source` | Text | 아이템을 공급할 런타임 상태 키 |
| `items.layout` | Enum | `Layout` |
| `items.virtualization` | Enum | `Virtualization` |
| `items.selection` | Enum | `Selection` |
| `items.paging` | Enum | `Paging` |
| `items.itemSize` / `items.itemCrossSize` | Float | `ItemSize` / `ItemCrossSize` |
| `items.spacing` / `items.crossSpacing` | Float | `Spacing` / `CrossSpacing` |
| `items.columns` / `items.autoColumns` | Int / Bool | `ColumnCount` / `AutoColumns` |
| `items.overscan` | Int | `Overscan` |
| `items.activate` / `items.reorderable` / `items.dragAndDrop` / `items.contextRequest` | Bool | `Interactions` 플래그 |
| `items.scrollSelectionIntoView` | Bool | `ScrollSelectionIntoView` |
| `items.showEmptyState` | Bool | Empty 슬롯 사용 여부 |
| `items.previewCount` | Int | 캔버스 프리뷰 전용 |

### 마이그레이션

`items.virtualize`(bool)와 `items.orientation`은 CollectionView 통합 이전 키다. 삭제하지 않았고,
`DesignerCollectionOptions`가 새 키가 없을 때만 이 값들을 읽는다. 따라서 이전 빌드에서 저장한
화면은 그대로 열리며, 다시 저장해도 값을 잃지 않는다. `items.itemSize == 0`(과거의 "매 아이템
측정")은 DynamicSize로 승격된다.

## Backend 저장 결과

**uGUI** — `UGUIControlFactory`가 Unity의 `DefaultControls.CreateScrollView`로 만든 정규
ScrollView(Viewport/Content/스크롤바) 위에 `NXCollectionView`를 얹는다. Content는 좌상단 앵커로
바꾼다(아이템 위치를 컬렉션이 직접 잡기 때문). 템플릿 슬롯의 자식이 `ItemTemplate`으로 지정되고
비활성화된다. `loading`/`empty`/`error` 이름의 자식이 있으면 상태 뷰로 연결된다.

**UI Toolkit** — `emiteat.NexUI.Integrations.UIToolkit.NXCollectionViewElement` 태그로 생성되고,
`layout-mode` / `selection-mode` / `item-size` / `column-count` 속성이 UXML에 기록된다.
`ui:ListView`를 쓰지 않는 이유: ListView는 단일 열 전용이라 Grid/Wrap을 표현할 수 없고, 두 백엔드가
같은 컨트롤러를 공유해야 선택·상태·이벤트가 동일하게 동작한다.

## Validation

| 코드 | 심각도 | 내용 |
|---|---|---|
| `collection-template-missing` | Error | 아이템 템플릿 없음 → 런타임에 아무것도 안 보임 |
| `collection-source-missing` | Warning | 소스 키도 Value 바인딩도 없음 |
| `collection-options-conflict` | Warning | 지원 불가 옵션 조합 |
| `collection-empty-state-missing` | Info | Empty 상태를 켰는데 Empty 슬롯이 빔 |
| `collection-selection-conflict` | Warning | Reorder인데 Selection이 None |
| `collection-virtualization-conflict` | Warning | 무한 페이징 + 가상화 없음 |

## 프리셋 만들기

새 목록형 컴포넌트는 서술자 한 줄이면 된다. `NexUIComponentArchetypes.Collection(...)`으로 만든
항목은 자동으로 `Kind = Preset`, `BaseTypeId = "CollectionView"`가 되고,
`NexUIBackendMappings`가 Core의 uGUI 컨트롤 키와 UXML 태그를 상속시킨다. **직렬화 코드를 추가할
필요가 없다.**

## 남은 작업

- Drag and Drop: `DragDropContext` 미구현. `items.dragAndDrop`은 현재 플래그만 전달한다
- Reorder 드래그 UI: 엔진의 `Move(from, to)`는 완결이지만 이를 호출하는 드래그 상호작용이 없다
- Snap / Pagination 동작
- `ItemSlot`, `InventoryGrid` 전용 동작 (Phase 4)
- 캔버스 프리뷰가 아직 `items.previewCount`만 쓰고 실제 옵션(열 수, 간격)을 반영하지 않음

## 관련 문서

- 샘플: `com.nexengineworks.nexui/Samples~/CollectionDemo/README.md`
- 테스트: `com.nexengineworks.nexui/Tests/EditMode/CollectionControllerTests.cs`
