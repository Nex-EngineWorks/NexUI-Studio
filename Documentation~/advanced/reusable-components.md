# 재사용 Component (Phase 3)

상태: **Beta**. 핵심 흐름(정의 생성 → 배치 → Override/Variant → 저장 → Detach)이 동작하고 데이터 손상 위험은 없지만,
Definition 전용 편집 창과 Inspector 통합은 아직 최소 수준입니다. 정확한 범위는 아래 [상태표](#상태표)를 확인하세요.

## 개념

| 개념 | 자산 / 필드 | 설명 |
|---|---|---|
| Component Definition | `DesignerComponentDefinitionAsset` | 재사용 가능한 element sub-tree와 그 계약(Exposed Property, Slot, Variant) |
| Component Instance | `DesignerElementMetadata.componentInstance` | Definition을 가리키는 **참조**. Definition element를 화면에 복사하지 않습니다 |
| Expansion | `DesignerComponentExpander` | Instance를 평탄화한 트리로 펼치는 순수 함수. Preview / Serializer / Validation이 모두 이 결과를 소비합니다 |

핵심 설계는 **Instance가 복사본이 아니라 참조라는 점**입니다.
그래서 Definition을 한 번 수정하면 전파 패스 없이 모든 Instance에 즉시 반영되고,
Expansion 결과가 사용자 자산에 절대 기록되지 않습니다.

## 패키지 Built-In 레시피

Studio 팔레트의 **NexUI / 기본 제공 레시피**에는 패키지가 소유하는 복합 UI 300개가 포함됩니다.
25개 실사용 아키타입(내비게이션, 콘텐츠, 폼, 상점, 게임 UI, 피드백)을 12개 시각 테마로
제공하며, 각각 여러 element와 편집 가능한 텍스트, content slot, Disabled variant를 갖습니다.

레시피는 프로젝트의 `Assets` 폴더에 300개 asset을 복사하지 않습니다. 패키지가 결정적으로
생성하는 읽기 전용 definition이며, instance에는 `builtin:<componentId>` 형식의 안정적인 참조가
저장됩니다. 따라서 패키지가 설치된 다른 머신에서도 같은 레시피가 다시 해석됩니다.

팔레트에서는 `NexUI / 기본 제공 레시피 / 범주 / 아키타입 / 테마` 순으로 탐색합니다. 접힌 아키타입의 카드는 생성하지
않아 창을 가볍게 유지하고, 검색을 시작하면 필요한 전체 카드가 검색 대상에 포함됩니다.
카드 미리보기는 root 색상만 보여주는 대신 실제 하위 element 배치를 축소 렌더링합니다.

```text
Authored Metadata          Expansion (in-memory, 저장되지 않음)
─────────────────          ────────────────────────────────────
card1  (instance) ────►    card1            (definition root, 원래 elementId/stableId 유지)
└ userLabel (slot)         ├ card1--title
                           └ card1--body
                             └ userLabel     (slot host로 재부모화)
```

## Identity 규칙

* Instance element가 **곧** definition root가 됩니다. 새 wrapper object를 만들지 않습니다.
  * `elementId`, `stableId`, `parentId`, `siblingIndex`, `rect`, `locked`, `hiddenInDesigner`는 Instance 것을 유지
  * `elementType`과 시각 속성은 definition root에서 가져옴
  * `runtimeVisible`은 `instance && definitionRoot` (둘 중 하나라도 숨김이면 숨김)
  * Instance element에 binding이 하나라도 지정되어 있으면 definition의 binding보다 우선
* 나머지 definition element는 `{instanceId}--{definitionElementId}` 형태의 id를 받습니다.
* 생성된 `stableId`는 `(instance stableId, definition element stableId)`의 **결정적 해시**입니다.
  매 저장마다 새로 만들지 않으므로 uGUI Prefab object 연결이 유지됩니다.

## Slot

Definition은 `DesignerComponentSlotDefinition`으로 slot을 선언합니다.

| 필드 | 의미 |
|---|---|
| `slotId` | slot 식별자. 비어 있으면 `content` |
| `hostElementId` | slot 자식이 붙을 definition-local element. 비어 있으면 root |
| `required` | 비어 있으면 Validation 경고 |
| `minimumChildren` / `maximumChildren` | 개수 제한. `maximumChildren <= 0`이면 무제한 |
| `acceptedTypes` | 허용 component type. 비어 있으면 전부 허용 |

Instance의 authored 자식은 `parentSlotId`로 slot을 고릅니다.
선언되지 않은 slot을 가리키면 **삭제하지 않고** root에 남긴 뒤 경고합니다.
허용되지 않은 타입도 마찬가지로 보고만 하고 제거하지 않습니다.

## Exposed Property와 Override

Definition 작성자는 내부 element의 특정 property를 이름으로 노출합니다.

```csharp
definition.exposedProperties.Add(new DesignerComponentExposedProperty {
    propertyName = "title",
    targetElementId = "titleLabel",
    propertyId = DesignerPropertyId.Text
});
```

Instance는 `exposedPropertyName`으로 override 하는 것을 권장합니다.
이 경우 definition 내부 element를 rename 해도 기존 instance가 깨지지 않습니다.
`targetElementId` + `propertyId` 직접 지정도 가능하지만 rename에 취약합니다.

Override는 typed 값(`DesignerPropertyValue`)이며 `DesignerPropertyApplier`가 실제 metadata 필드에 씁니다.
metadata에 표현이 없는 property(`Gradient`, `Texture` 등)는 **조용히 무시하지 않고** `component-override-unapplied`로 보고합니다.

## Variant

```text
variantProperties : size = { small, large }, default = small
variantRules      : when size == large → overrides + hiddenElementIds/shownElementIds
```

적용 순서는 **Variant Rule → Instance Override**입니다. Instance override가 항상 이깁니다.
Instance가 선택을 지정하지 않으면 variant property의 default가 적용됩니다.

## 작업 흐름

### 선택 요소를 Component로 변환

```csharp
DesignerComponentService.CreateDefinitionFromSubtree(screen, "card", "Assets/UI/Components/Card.asset");
```

* subtree를 **복사**해 definition asset을 만든 뒤, asset 쓰기가 성공한 경우에만 원본 subtree를 instance로 접습니다.
* `AssetDatabase.CreateAsset`이 실패하면 화면은 전혀 건드리지 않습니다.

### Instance 배치 / 수정

| API | 동작 |
|---|---|
| `Instantiate` | 새 instance element 생성. variant 기본값 자동 채움 |
| `SetOverride` / `ResetOverride` / `ResetAllOverrides` | Override 관리 (Undo 지원) |
| `UpdateFromDefinition` | 버전 스탬프 갱신 + 해결되지 않는 override를 **보고**(삭제하지 않음) |
| `Swap` | 다른 definition으로 교체. 해결 불가능한 override는 보고 후 제거 — **파괴적 작업이므로 호출 전 확인 필요** |
| `Detach` | 펼쳐진 subtree를 실제 authored element로 물질화. 참조는 `detached = true`로 남겨 출처를 추적 |

### Definition 삭제 대응

Definition asset이 사라지면 instance는 **그대로 유지**되고 `component-definition-missing` Error가 뜹니다.
Instance element와 slot 자식은 삭제되지 않으므로, definition을 복구하거나 Detach 해서 내용을 보존할 수 있습니다.
GUID가 바뀐 경우(프로젝트 간 이동 등)에는 `componentId`로 복구를 시도합니다.

## Validation 코드

| 코드 | 심각도 | 의미 |
|---|---|---|
| `component-definition-missing` | Error | Definition asset을 찾을 수 없음 |
| `component-cycle` | Error | Component가 자기 자신을 포함 |
| `component-definition-empty` | Error | Definition에 root element가 없음 |
| `component-expansion-budget` | Error | 중첩 깊이(16) 또는 생성 element 수(4000) 초과 |
| `component-slot-unknown` | Warning | 선언되지 않은 slot을 가리키는 자식 |
| `component-slot-type-rejected` | Warning | slot이 허용하지 않는 component type |
| `component-slot-required-empty` | Warning | 필수 slot이 비어 있음 |
| `component-slot-count` | Warning | slot 자식 개수가 범위를 벗어남 |
| `component-override-unresolved` | Warning | Override 대상이 definition에 없음 |
| `component-override-unapplied` | Warning | Property에 authored 표현이 없어 적용되지 않음 |
| `component-variant-unknown` | Warning | 선언되지 않은 variant property 선택 |
| `component-variant-value-unknown` | Warning | 선언되지 않은 variant 값 |
| `component-version-mismatch` | Warning | Instance가 참조하는 definition 버전이 다름 |
| `component-slot-host-missing` | Warning | slot의 `hostElementId`가 존재하지 않음 |
| `component-exposed-target-missing` | Warning | Exposed property 대상 element가 없음 |
| `component-origin-missing` | Info | Detach된 element의 출처 definition이 사라짐 |

## Metadata / Schema

* `DesignerMetadataAsset.CurrentSchemaVersion` : 3 → **4**
* 신규 필드 `DesignerElementMetadata.componentInstance` (추가 전용, 기존 값 변경 없음)
* v3 → v4 migration은 `componentInstance`를 정규화하고, 적용 자체가 불가능한 빈 override만 제거합니다. 반복 실행해도 안전합니다.

## 상태표

| 기능 | 상태 | 비고 |
|---|---|---|
| Definition 생성 / Instance 배치 | Beta | Service API + 메뉴. 전용 편집 창 없음 |
| Definition 수정 전파 | Complete | 참조 기반이라 전파 패스 자체가 없음 |
| Exposed Property Override | Beta | Typed. Inspector UI는 최소 |
| Variant (Bool/Enum/String) | Beta | Rule 기반 override + visibility |
| Slot (단일/다중/필수/타입 제한) | Beta | Template/Generated slot은 내장 component 쪽 기능 |
| Nested Component | Beta | 깊이 16 제한, cycle 검출 |
| Detach | Beta | 물질화 + 출처 유지 |
| Swap | Beta | 해결 불가 override 제거(확인 필요) |
| Version Migration | Partial | 버전 스탬프와 보고까지. 자동 property 재매핑은 미구현 |
| Preview Thumbnail / Library 검색 | Partial | 검색·카테고리·태그·즐겨찾기·사용처는 API로 제공, 창 UI는 최소 |
| Motion Override / Theme Override / Responsive Override | **미구현** | Phase 5–7에서 다룹니다 |
| Backend Template | **미구현** | 현재는 definition element를 그대로 backend로 출력 |

## 알려진 제한

* Instance rect를 definition root 크기와 다르게 지정해도 **자식은 재배치되지 않습니다**.
  root만 instance 크기를 따르며, 자식 재배치가 필요하면 definition root에 Auto Layout을 켜세요.
* Definition 편집은 현재 Inspector(ScriptableObject 기본 UI)에서 수행합니다. 전용 편집 모드는 다음 단계 작업입니다.
* Definition 자체를 Studio 캔버스에서 여는 기능은 아직 없습니다.
