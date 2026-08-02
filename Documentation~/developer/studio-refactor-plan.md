# NexUI Studio 전면 구조 개선 — 저장소 분석과 실행 계획

작성일 2026-07-31. 대상은 두 패키지를 하나의 UI 제작 파이프라인으로 본 전체 구조다.

- `Packages/com.nexengineworks.nexui` — Runtime (C# 283개)
- `Packages/com.nexengineworks.nexui.studio` — Editor 도구 (C# 368개), **NexUI Studio로 리브랜딩**

범용 Component 편집 시스템의 세부 설계는 [universal-component-system.md](universal-component-system.md)에
이미 있다. 이 문서는 그것을 포함한 저장소 전체 계획이며, 중복되는 절은 참조만 한다.

---

## 1. 확인한 현황 (실제 코드 기준)

### 1.1 Component 모델 — 데이터는 통합됐고 편집 경로는 갈라져 있다

`DesignerElementComponent` (`Runtime/Metadata/DesignerElementComponent.cs`)는 지시서가 요구한 최소
필드를 **이미 전부 갖고 있다**: `instanceId`, `typeId`, `source`, `assemblyQualifiedTypeName`,
`enabled`, `fromPreset`, `properties`. `DesignerObjectReference`, `DesignerPropertyValue.json` +
`objectValues`, Schema v6 마이그레이션(`DesignerHierarchyMigration.MigrateToUniversalComponents`)도
구현돼 있고 마이그레이션 멱등성 테스트까지 있다(`DesignerUniversalComponentModelTests`).

문제는 **데이터 모델이 아니라 그 모델을 쓰는 코드가 없다는 것**이다. `attachedComponents`를 참조하는
파일은 14개이며 그중 신규 데이터를 만드는 경로가 남아 있다.

| 파일 | 역할 | 상태 |
|---|---|---|
| `Inspectors/ElementComponentsInspector.cs:289 AttachScript` | Add Script → **`attachedComponents`에 기록** | 제거 대상 (P0-3) |
| `Inspectors/AttachedComponentsInspector.cs` (576줄) | 두 번째 Component Inspector + 두 번째 Add 버튼 | 제거 대상 (P0-3) |
| `Serialization/UGUIAssetSerializer.cs:844 ApplyAttachedComponents` | Component 생성만, **속성 미적용** | 범용 Writer로 흡수 (P0-7) |
| `Serialization/UIToolkitAssetSerializer.cs:79` | Unsupported 보고 | 유지 |
| `Core/DesignerHierarchyMigration.cs:161` | v5→v6 흡수 | 유지 (읽기 전용화) |
| `Serialization/DesignerMetadataJsonSerializer.cs:151,383,522` | DTO 왕복 | 읽기만 남김 |
| `Inspectors/DesignerInspectorRegistry.cs:164` | 별도 섹션 등록 | 제거 대상 |
| `Serialization/DesignerSavePreviewService.cs:216` | Dry Run 항목 | 범용 경로로 이전 |
| `AI/NexUIAIContextBuilder.cs`, `AI/NexUIAIActionService.cs` | AI 컨텍스트 | 범용 경로로 이전 |

### 1.2 범용 편집의 실제 공백

- **Inspector**: `ElementComponentsInspector.Field()`는 `DesignerPropertyValueType` 8종만 직접
  그린다. 즉 `bool/int/float/string/Color/Vector2/enum/Object`. Vector3·Rect·Curve·Gradient·
  배열·List·중첩 `[Serializable]`은 UI 자체가 없다.
- **스키마**: `DesignerReflectedSchema.Describe()`는 위 8종에 해당하지 않는 필드에 대해
  `return null` — 즉 **조용히 드롭**한다. 지시서의 "지원하지 않는 값은 보존하고 보고한다"에 위배.
- **Writer**: `UGUIComponentWriter`는 `DesignerUIComponentRegistry.Get(typeId).BackingType`이 있는
  등록 타입만 처리한다. `source == Project` 컴포넌트는 통째로 건너뛴다. `Convert()`도 같은 8종만.
- **동일 타입 다중**: Writer가 `go.GetComponent(type)` 하나만 보므로 `instanceId` 추적이 없다.
- **참조**: `DesignerObjectReference`는 자료형만 있고, 이를 **읽거나 쓰는 코드가 어디에도 없다**.
  `DesignerPropertyValue.reference` 필드를 소비하는 곳이 0개.

### 1.3 Backend Round Trip

`UGUIAssetSerializer`는 Stable ID 매칭(`NxUGuiBindingTag`), 소유권 구분
(`NexUIElementOwnership.DesignerOwned/UserOwned/Unknown`), Orphan 자동삭제 금지, Save Report 채널
(`Added/Changed/Skipped/Unsupported/Warning/Error/Orphan`)을 **이미 갖췄다**. 부족한 것은:

- Prefab → Metadata **Import 경로 없음** (단방향)
- Serialized Property 전체 읽기 없음 / UnityEvent 없음 / Component 순서 반영 없음
- Nested Prefab 처리 없음, 의미적 Round Trip 테스트 없음

### 1.4 Binding (Core)

`Runtime/State`에 `BindableProperty`, `IValueConverter`, `PropertyBinding.BindTwoWay<T>`가 있으나
이는 **속성↔속성** 경로다. UI 위젯을 대상으로 하는 `UITextBinder`/`UIValueBinder`/
`UIVisibilityBinder`/`UIClassBinder`/`UICommandBinder`는 전부 단방향이고, `UIBindingMode` 열거형
자체가 존재하지 않는다. Collection Item Context, 순환 검출, 재진입 방지 없음.

### 1.5 성능

`Runtime` 전체에 **`ProfilerMarker` 사용처 0개**. `Documentation~/performance/` 없음. Benchmark
Sample 없음. 즉 지시서 Phase 14는 전부 미착수이며, 현재 성능에 대해 주장할 수 있는 근거가 없다.

### 1.6 브랜딩

`package.json`은 개명 전 `com.emiteat.nexui` / author `emiteat` / URL `github.com/swallow-smoke/...`였다.
지시서의 저장소는 `OffByJun/NexUI`. 세 식별자가 불일치한다. 외부 배포 이력 없음이 확인되어
**즉시 개명**한다(사용자 결정).

---

## 2. 재사용할 구조 (새로 만들지 않는다)

| 구조 | 위치 | 재사용 방식 |
|---|---|---|
| `DesignerElementComponent` | Runtime/Metadata | 단일 Component 모델 그대로 |
| `DesignerObjectReference` | Runtime/Metadata | Reference 모델 그대로, 소비 코드만 신규 |
| `DesignerPropertyValue.json`/`objectValues` | Runtime/Metadata | 복합 값 저장소 그대로 |
| Schema v6 마이그레이션 | `DesignerHierarchyMigration` | 그대로, 신규 쓰기만 차단 |
| `DesignerElementComponentAccess` | Editor/Components/Model | Attach/Detach/Move/Set 확장 |
| `DesignerReflectedSchema` | Editor/Components/Model | Registry 타입용으로 유지, Generic Inspector는 `SerializedObject` 사용 |
| `NxUGuiBindingTag` + Ownership | Editor/Serialization | Stable ID·소유권 판정 그대로 |
| `DesignerSaveReport` | Editor/Serialization | 보고 채널 그대로 |
| `DesignerAttachedComponentTracker` | Runtime/Metadata | Studio 소유 Component 추적 — `instanceId` 필드 추가해 확장 |
| `DesignerMonoBehaviourTypes` | `AttachedComponentsInspector.cs` 내부 | 타입 탐색 로직을 별도 파일로 승격 + 캐시 무효화 추가 |

## 3. 제거할 중복 구조

1. `AttachedComponentsInspector` — Inspector와 Picker Window를 분리해, Picker만 살리고
   Inspector 섹션은 삭제.
2. `ElementComponentsInspector.AttachScript` — `element.components`에 기록하도록 교체.
3. `DesignerInspectorRegistry`의 `attached-components` 섹션 등록.
4. `UGUIAssetSerializer.ApplyAttachedComponents` — 범용 Component Writer에 흡수.
5. `DesignerMetadataJsonSerializer`의 `attachedComponents` **쓰기**(읽기는 유지).

## 4. 변경할 데이터 흐름

```
[전]
Add Script ──→ attachedComponents(typeName만) ──→ ApplyAttachedComponents ──→ 컴포넌트만 생성
Add Component ─→ components ──────────────────→ UGUIComponentWriter ─────→ 등록 타입만 값 적용

[후]
Add Component ─→ components(단일) ─→ StudioComponentWriter ─→ SerializedObject로 값+참조 적용
                       ↑                                              │
                  Generic Inspector                                   ↓
                 (스크래치 GameObject + SerializedObject)        Prefab (실제 값)
attachedComponents ─(읽기/Migration 전용)─┘
```

## 5. Schema Migration

v6 마이그레이션은 이미 있고 멱등하다. 이번 작업에서 추가되는 것:

- `DesignerAttachedComponentTracker`에 `instanceId` 대응 추가 → **v7 불필요**
  (Tracker는 Prefab 측 데이터이며 Metadata Schema가 아님). 기존 Prefab의 Tracker는 `instanceId`가
  비어 있으므로 타입 기준 매칭으로 폴백한다.
- Metadata Schema는 v6 유지. 신규 필드 추가가 필요해지면 그때 v7로 올린다.

## 6. Assembly 의존성 (변경 없음)

- `emiteat.NexUI.Studio.Runtime` — 데이터 모델만. **`UnityEditor` 참조 금지**
- `emiteat.NexUI.Studio.Editor` — 타입 탐색·Inspector·Writer·Import
- `com.nexengineworks.nexui`(Core)는 Studio를 참조하지 않는다. Player Build에는 실제 Component와
  NexUI Binding/Motion/Screen만 남는다.

## 7. 구현 단계와 이번 세션의 범위

사용자 결정: **Phase 0 문서 + 첫 수직 슬라이스**까지가 이번 인도 범위.

| # | 작업 | 이번 세션 |
|---|---|---|
| P0-1 | 저장소 분석 (이 문서) | 포함 |
| P0-2 | NexUI Studio 사용자 노출 리브랜딩 + Package ID 개명 | 포함 |
| P0-3 | `attachedComponents` 신규 쓰기 중단, Add Script를 `components`로 | 포함 |
| P0-4 | 타입 인덱스 + 캐시 무효화 | 포함 |
| P0-5 | Generic Serialized Inspector (스크래치 + `SerializedObject`) | 포함 |
| P0-6 | Element/Asset Reference 편집 | 포함 |
| P0-7 | uGUI Prefab 값·참조 Apply (`instanceId` 소유권 추적) | 포함 |
| P0-8 | 저장 후 Reload / 멱등 저장 | 포함 |
| P0-9 | EditMode 테스트 (batchmode 실행) | 포함 |
| P0-10 | Benchmark 기준선 | **미포함** — 별도 세션 |
| P1-A | 값 포맷 구분(`valueFormat`) + JSON DTO 손실 수정 | 포함 (추가 진행) |
| P1-B | Prefab Import + Round Trip | 포함 (추가 진행) |
| P1-C | UnityEvent 편집·적용·검증 | 포함 (추가 진행) |
| P1 나머지 | Two-way Binding, Converter, Definition Override 확장, Component/Template 분리, Collection Drag | 미포함 |
| P2 | UI Toolkit 확대, Motion/Theme Override, Figma, AI | 미포함 |

## 8. 위험 요소

| 위험 | 대응 |
|---|---|
| 스크래치 GameObject가 씬 오염 | `HideFlags.HideAndDontSave`, 단일 인스턴스 풀, `AssemblyReloadEvents`·`EditorApplication.quitting`에서 파괴 |
| `SerializedObject` diff 비용 | 편집된 property path만 기록. 전체 순회는 Add 시 1회 |
| 동일 타입 Component 다중 추적 | Tracker에 `instanceId` 기록, 없으면 타입+순서 폴백 |
| 사용자가 Prefab에 직접 붙인 Component 삭제 | Tracker에 없는 Component는 절대 삭제하지 않고 Orphan 보고 |
| Scene Object 참조 | `DesignerReferenceKind`에 Scene 없음 → Unsupported 보고, 값 보존 |
| Package ID 개명으로 기존 Asset 참조 파손 | Assembly/namespace는 **이번에 바꾸지 않는다**. `package.json` id·displayName·URL과 사용자 노출 문구만 변경 |
| 반복 저장 시 Prefab 계속 Dirty | 값이 같으면 쓰지 않는 비교 후 기록 + 멱등 테스트 |

## 9. 실제 완료 기준

수직 슬라이스 `SampleHealthBarController` 17개 조건을 전부 Pass해야 이 단계를 완료로 본다.
각 조건은 EditMode 테스트 또는 batchmode 실행 로그로 증명하며, 실행하지 않은 항목은 Pass로
표기하지 않는다. 검증 결과는 이 문서 하단에 갱신한다.

### 9.1 검증 결과

P0 수직 슬라이스는 사용자가 Unity Editor에서 확인함. 이 문서를 쓴 에이전트는 batchmode를 직접
실행하지 못했다(Editor가 열려 있어 프로젝트 잠김). P1 코드는 **아직 테스트를 실행하지 않았다.**

## 10. P1에서 추가된 것

### 10.1 값 포맷 (`DesignerComponentValueFormat`)

Writer가 두 벌인 이유를 암묵적 추론(레지스트리 등록 여부)에서 명시적 필드로 바꿨다.

| | `SchemaKeys` (기본, 값 0) | `PropertyPath` |
|---|---|---|
| 키 | `preserveAspect` | `m_PreserveAspect` |
| 범위 | 큐레이션된 스키마 필드만 | 타입의 모든 직렬화 필드 |
| Writer | `UGUIComponentWriter` | `StudioComponentWriter` |
| 출처 | 팔레트 프리셋 | Add Component, Prefab Import |

`StudioComponentWriter.OwnedByThisWriter()`가 단일 판정점이며, Inspector도 같은 함수로 어떤
편집 UI를 그릴지 정한다 — 편집한 필드와 저장되는 필드가 갈라질 수 없다.

### 10.2 JSON 컴패니언 손실 수정

`DesignerMetadataJsonSerializer`의 DTO가 `source`·`assemblyQualifiedTypeName`·`valueFormat`·
`json`·`reference`를 전부 버리고 있었다. Sync Metadata From JSON을 쓰면 프로젝트 스크립트가
"타입을 알 수 없는 컴포넌트"로 되살아나고 모든 참조가 사라지는 상태였다. DTO에 필드를 추가했고,
필드가 없는 구버전 JSON은 열거형 기본값으로 기존 동작을 그대로 재현한다.

### 10.3 Prefab Import

`StudioPrefabImporter`. **Prefab을 절대 수정하지 않는다** — stableId는 메타데이터에만 생성되고,
실제 태그는 기존 소유권 보존 경로(첫 Save)가 붙인다. 모든 Component를 property path로 가져오므로
큐레이션 스키마가 이름 붙이지 않은 필드도 유실되지 않는다.

- 계층·RectTransform(임의 앵커/스트레치 포함, 코너 기준 변환)·Component 순서·enabled
- Prefab 내부를 가리키는 Object Reference → **Element Reference**로 변환
- 재Import는 stableId로 매칭해 갱신(중복 생성 없음), Binding·Motion·Theme·클래스 등
  Studio 전용 데이터는 보존
- Prefab에 없는 요소는 삭제하지 않고 Orphan으로 보고
- GameObject 이름은 절대 바꾸지 않음(중복 이름은 elementId에만 접미사)

### 10.4 UnityEvent

`StudioUnityEventModel`(데이터) + `StudioUnityEventRow`(UI). Unity 기본 Drawer를 쓸 수 없다 —
대상 Element에는 아직 GameObject가 없어 메서드 목록이 항상 비기 때문이다. 참조에서 대상
**타입**을 해석해 메서드를 나열한다.

- Persistent Call 추가/삭제/순서, 대상(Element GameObject / Element Component / Asset),
  메서드 선택, Void/Int/Float/String/Bool/Object 인자, CallState
- Prefab 저장 시 실제 `UnityEvent`에 배선됨
- 사라진 메서드는 목록에서 `Missing: X`로 표시하고 `NEXUI-EVENT-MISSING-METHOD`로 검증 보고
- `Capture`가 UnityEvent 서브트리를 건너뛴다 — 스크래치 오브젝트의 빈 리스트로 사용자의
  리스너를 지우는 것을 막는 안전장치

### 10.5 남은 제한 (P1 시점)

- **Two-way Binding 구현 완료.** Text/Value input capability, `UIBindingMode`, Converter Registry를 제공한다.
- **Component 순서 구현 완료.** Owned/Adopted 소유권을 구분해 사용자 Component 삭제 없이 재정렬한다.
- **Nested Prefab**: 메타데이터에서는 일반 Element 트리로 보이지만 Save가 기존 인스턴스를
  재사용하므로 원본 Prefab 연결과 Override를 유지한다. 회귀 테스트로 고정한다.
- **Missing Script**(null 컴포넌트 슬롯)는 타입명이 남지 않아 재생성할 수 없고 보고만 한다.
- **`[SerializeReference]`/`ExposedReference` 구현 완료.** Hash128과 함께 Property bag으로 왕복한다.
- 중첩 `[Serializable]` 안의 object 필드는 Element 참조 UI가 없다(에셋만 지정 가능).
