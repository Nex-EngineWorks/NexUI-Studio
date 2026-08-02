# 범용 Component 편집 시스템 — 설계

NexUI Studio를 "미리 등록된 컴포넌트 모음"에서 "Unity Inspector처럼 임의의 컴포넌트를 편집하는
UI 제작 환경"으로 바꾸기 위한 설계. **코드를 고치기 전에 쓰는 문서이며, 구현이 진행되면 이 문서의
지원 표를 함께 갱신한다.**

## 1. 현재 구조

### 1.1 컴포넌트 모델이 두 벌이다

| | `element.components` | `element.attachedComponents` |
|---|---|---|
| 타입 | `DesignerElementComponent` | `DesignerAttachedComponentMetadata` |
| 식별 | `instanceId` (GUID) | 없음 |
| 저장 | `typeId`, `enabled`, `fromPreset`, 순서, 속성 bag | `typeName` 문자열 하나 |
| 스키마 | `DesignerUIComponentRegistry` + `DesignerReflectedSchema` | 없음 |
| Prefab 반영 | 속성까지 기록 | 존재 여부만 (`DesignerAttachedComponentTracker`) |

`components`는 NexUI/uGUI/UI Toolkit 등록 타입용, `attachedComponents`는 프로젝트 MonoBehaviour용
으로 갈라져 있다. 인스펙터·직렬화·검증 코드가 전부 두 경로를 따로 다룬다.

### 1.2 재사용 가능한 것

- **Stable ID**: 요소 `stableId`, 컴포넌트 `instanceId` 모두 존재
- **리플렉션 스키마**: `DesignerReflectedSchema`가 `[SerializeField]`/public 필드를 Unity 직렬화
  규칙대로 읽고 `[Range]`/`[Tooltip]`을 이미 반영
- **타입 탐색**: `TypeCache.GetTypesDerivedFrom<MonoBehaviour>()` 사용 중 (캐시는 없음)
- **소유권 추적**: `DesignerAttachedComponentTracker`가 Studio 소유 컴포넌트와 사용자 컴포넌트를
  구분 — Phase 8의 "임의 삭제 금지"가 부분 충족
- **속성 bag**: `DesignerComponentPropertyBag` — 변경한 값만 저장, 모르는 키 보존
- **Save Report**: `Changed/Added/Skipped/PreviewOnly/Unsupported` 채널 존재
- **Definition/Instance**: `DesignerComponentInstanceMetadata` + 오버라이드/변형 존재

### 1.3 없는 것

- 참조 모델 자체 (`DesignerObjectReference` 없음). Element 참조도 Asset 참조도 표현 불가
- 값 타입 커버리지: `DesignerPropertyValue`는 `float/int/bool/string/Color/Vector2/Object` 7개뿐.
  Vector3·Vector4·Rect·Bounds·Quaternion·LayerMask·AnimationCurve·Gradient·배열·List·중첩
  `[Serializable]` 전부 불가
- 프로젝트 MonoBehaviour의 속성 편집·저장 (타입명만 저장)
- UnityEvent 편집
- Prefab Import (Prefab → Metadata)
- 검색 인덱스 캐시, Domain Reload 무효화

## 2. 결정 사항

1. **모델은 하나로 통합한다.** `attachedComponents`를 `components`로 흡수하고
   `DesignerElementComponent`에 `source`와 `assemblyQualifiedTypeName`을 추가한다.
   `schemaVersion 6`에서 자동 마이그레이션한다.
2. **값 저장은 하이브리드.** 기존 7개 필드는 그대로 두고(기존 에셋 무손상), 복합·미지 타입은
   `json` 문자열 + `objectValues` 리스트에 담는다. 모르는 데이터는 보존한다.
3. **직렬화 엔진은 Unity `SerializedObject`.** 숨겨진 스크래치 인스턴스에 붙여 인스펙터를 그리고
   값을 적용한다. Phase 3의 타입 목록·Attribute·`[FormerlySerializedAs]`·기존 커스텀 Drawer를
   전부 공짜로 얻는다. 메타데이터는 **기본값 대비 diff**로 저장한다.

## 3. 새 데이터 모델

```
DesignerElementComponent            (기존 타입을 확장)
├─ instanceId : string              // 이미 있음
├─ typeId : string                  // "UGUI.Image" / "Project:Health.HealthBarController"
├─ assemblyQualifiedTypeName        // 신규 - 실제 타입 복원용
├─ source : DesignerComponentSource // 신규 - NexUI/UGUI/UIToolkit/Unity/Project
├─ enabled : bool                   // 이미 있음
├─ fromPreset : bool                // 이미 있음
└─ properties : List<Entry>         // 이미 있음, 값 표현만 확장

DesignerPropertyValue               (기존 타입을 확장)
├─ type, floatValue, intValue, boolValue, stringValue, colorValue, vector2Value, assetValue
├─ json : string                    // 신규 - 복합/미지 타입 원문
└─ objectValues : List<Object>      // 신규 - 배열/참조 목록

DesignerObjectReference             (신규)
├─ kind : Element | Asset | None
├─ stableElementId : string         // Element 참조는 이름이 아니라 stableId
├─ componentTypeName : string       // 그 요소의 어떤 컴포넌트인지
├─ assetGuid : string
└─ localFileId : long
```

`typeId`는 유지한다. 기존 화면이 `"UGUI.Image"`로 저장되어 있고, 프리셋·백엔드 매핑이 이 키를
쓴다. 프로젝트 스크립트는 `"Project:"` 접두사를 붙여 같은 공간에서 충돌 없이 공존시킨다.

## 4. 데이터 흐름

```
Add Component
  TypeCache 인덱스 → 후보 타입 → DesignerElementComponentAccess.Attach(source, type)
                                     ↓ Undo 1회
Inspector 편집
  스크래치 인스턴스 + SerializedObject → 사용자가 필드 편집
                                     ↓ 기본값과 다른 것만
  DesignerPropertyValue (필드 또는 json) → element.components[i].properties
                                     ↓ Undo 1회
Prefab 저장
  UGUIAssetSerializer → 대상 GameObject의 실제 Component 확보(Tracker로 소유권 구분)
                      → SerializedObject로 값 적용 → 참조는 stableId → 실제 오브젝트로 해석
                                     ↓ 실패 시 전체 Rollback
재로드
  Metadata 로드 → 같은 경로 역방향. 지원 못 하는 값은 json에 남아 그대로 재저장
```

## 5. Assembly 의존성

- `emiteat.NexUI.Studio.Runtime` — 데이터 모델만. **UnityEditor 참조 없음**
- `emiteat.NexUI.Studio.Editor` — 타입 탐색·인스펙터·Drawer·Prefab 적용. UnityEditor 사용
- 런타임 패키지(`com.nexengineworks.nexui`)는 이 시스템을 참조하지 않는다. 빌드에는 생성된 실제
  Component와 NexUI Binding/Motion/Screen만 남는다

## 6. 마이그레이션 전략

`schemaVersion 5 → 6`:

1. `element.attachedComponents`의 각 항목을 `element.components`에 추가.
   `source = Project`, `assemblyQualifiedTypeName = typeName`, `instanceId = 새 GUID`
2. `attachedComponents`는 **비우지 않고 그대로 둔다.** 구버전 Studio에서 열었을 때 컴포넌트가
   사라지지 않도록 한 버전 동안 병행 기록한다(읽기는 `components`만)
3. 기존 `components` 항목에는 `source`를 `typeId` 접두사에서 추론해 채운다
   (`UGUI.` → UGUI, `UITK.` → UIToolkit, `NX.`/그 외 → NexUI)
4. 실패한 항목은 삭제하지 않고 `component-migration-unresolved` 검증 이슈로 보고

## 7. 구현 단계

| 단계 | 내용 | 상태 |
|---|---|---|
| 1 | 데이터 모델 확장 + 마이그레이션 v6 | 완료 |
| 2 | 타입 인덱스(TypeCache 캐시) + Add Component UI | 완료 — `StudioComponentTypeIndex`, `StudioAddComponentPicker` |
| 3 | SerializedObject 기반 제네릭 인스펙터 | 완료 — `StudioScratchComponentHost`, `StudioSerializedComponentBridge`, `StudioGenericComponentEditor` |
| 4 | Object/Element 참조 | 완료 — `StudioReferenceUtility`, `StudioReferenceRow` |
| 5 | Prefab 적용(속성·참조) | 완료 — `StudioComponentWriter` |
| 6 | Definition/Instance 오버라이드 확장 | 예정 |
| 7 | Prefab Import | 예정 |
| 8 | UnityEvent | 예정 |
| 9 | Template 재분류 | 예정 |

### 7.1 1~5단계의 알려진 범위

- 제네릭 인스펙터는 **레지스트리에 없는** Component(프로젝트 스크립트, 일반 Unity 컴포넌트)에만
  적용된다. `UGUI.*` / `UITK.*` / `NX.*` 등록 타입은 기존 `DesignerReflectedSchema` 경로를 그대로
  쓴다 — 저장 키가 스키마 키(`preserveAspect`)와 property path(`m_PreserveAspect`)로 달라서,
  전환하면 기존 화면의 값이 유실되기 때문이다. 통합은 키 마이그레이션과 함께 별도로 한다.
- Element 참조 행은 **최상위 object 필드**에만 붙는다. 중첩 `[Serializable]` 안의 object 필드는
  `PropertyField`가 그리므로 에셋만 지정할 수 있다(값은 정상 저장·복원됨).
- Component **순서**는 소유권 추적과 `ComponentUtility`를 통해 Prefab에 반영한다. Import된
  Component는 Adopted로 연결되어 Stack에서 제거해도 원본 Component를 삭제하지 않는다.
- `ManagedReference`(`[SerializeReference]`), `ExposedReference`, `Hash128` 값을 메타데이터로
  왕복 저장한다. 로드할 수 없는 Managed type은 값 원문을 유지하고 Unsupported로 보고한다.
- double 필드는 json 경로로 저장해 정밀도를 유지한다.

**최소 수직 슬라이스**(1~5의 최소 경로)를 먼저 닫는다: 사용자 MonoBehaviour 검색 → Add Component
→ float/string/Object 참조 편집 → Undo → Prefab 저장 → 재로드.

## 8. 위험 요소

| 위험 | 대응 |
|---|---|
| 스크래치 인스턴스가 씬을 오염 | `HideFlags.HideAndDontSave` GameObject 하나를 풀링하고 도메인 리로드 시 정리 |
| SerializedObject 값 → 메타데이터 diff 계산 비용 | 편집된 property path만 기록. 전체 순회는 컴포넌트 추가 시 1회 |
| 참조가 씬 오브젝트를 가리키는 경우 | Prefab에 저장 불가 → `Unsupported`로 보고하고 값은 보존 |
| 마이그레이션 후 구버전에서 열기 | 6절 2항의 병행 기록으로 한 버전 유예 |
| 기존 `components` 소비자(프리셋·백엔드 매핑)가 새 source를 모름 | `source` 기본값은 NexUI. 기존 코드 경로 무변경 |
| 대량 타입 리플렉션 | TypeCache + 정렬된 인덱스 1회 생성, `AssemblyReloadEvents`에서 무효화 |

## 9. 이 문서의 규칙

지원한다고 쓰기 전에 해당 경로의 EditMode 테스트가 있어야 한다. 미구현은 표에 `예정`으로 남기고,
"부분 지원"은 무엇이 되고 무엇이 안 되는지 함께 적는다.
