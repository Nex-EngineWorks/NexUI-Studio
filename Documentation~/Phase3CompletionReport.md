# Phase 3 완료 보고서 — 재사용 Component 시스템

## 변경 요약

사용자가 화면의 일부를 **재사용 가능한 Component**로 만들고, 여러 화면에 Instance로 배치하고,
Instance마다 Override / Variant / Slot 내용을 다르게 줄 수 있게 되었습니다.

실제 사용자 동작 기준으로 달라진 점:

* Designer에서 요소를 선택하고 `Tools > NexUI > Designer > Component Library` → **Create Component From '...'** 로
  Definition asset을 만들면, 원본 subtree가 Instance 하나로 접힙니다.
* 같은 Definition을 다른 화면에 여러 번 배치해도 **Definition을 한 번 고치면 모든 Instance가 즉시 따라옵니다**.
  전파 버튼이 없습니다 — Instance가 복사본이 아니라 참조이기 때문입니다.
* Instance를 선택하면 Inspector에 **Component Instance** 섹션이 생기고, Variant 선택과 Exposed Property Override를
  편집할 수 있습니다. Override된 항목에는 표시가 붙고 개별/전체 Reset이 가능합니다.
* Canvas는 펼쳐진 결과를 그리지만 선택·드래그·Inspector는 여전히 사용자가 소유한 authored element만 대상으로 합니다.
* Save와 Save Preview는 펼쳐진 트리를 backend에 씁니다. uGUI Prefab / UXML에 Component 내용이 실제로 나갑니다.
* Definition asset이 사라져도 **Instance와 그 Slot 내용은 삭제되지 않고** Error로 보고됩니다.

## 아키텍처

새 책임 분리:

```text
Runtime Metadata      DesignerComponentDefinitionAsset (정의 + 계약)
                      DesignerComponentInstanceMetadata (참조 + override + variant 선택)
                        ↑ UnityEditor 참조 없음. Designer.Runtime asmdef

Editor Expansion      DesignerComponentExpander      순수 함수. AssetDatabase 미사용(resolver 주입)
                      DesignerComponentLibrary       AssetDatabase 색인 + resolver 구현
                      DesignerPropertyApplier        typed property → element 필드

Editor Authoring      DesignerComponentService       생성/배치/override/detach/swap/update (Undo)
                      DesignerComponentLibraryWindow 검색·즐겨찾기·사용처·배치
                      ComponentInstanceInspector     Instance 편집

Editor Consumers      NexUIDesignerContext.Components  캐시된 expansion, authored 역매핑
                      NexUIDesignerViewport            펼친 트리 렌더 / authored 선택
                      NexUIDesignerContext.Save        펼친 트리를 serializer에 전달
                      DesignerSavePreviewService       dry run도 펼친 트리 기준
                      DesignerComponentValidation      expansion issue → validation code
```

데이터 흐름의 핵심 규칙 두 가지:

1. **Authored ≠ Expanded.** Expansion 결과는 `HideFlags.HideAndDontSave`인 in-memory ScriptableObject이며,
   어떤 경로로도 사용자 asset에 기록되지 않습니다. Expansion이 존재하면 Save는 authored metadata를 직접
   `AssetDatabase.SaveAssetIfDirty`로 저장합니다(serializer는 복사본만 알기 때문).
2. **Instance element가 곧 Definition root.** wrapper object를 만들지 않으므로 backend 트리가 자연스럽고,
   Instance의 `stableId`가 유지되어 uGUI Prefab object 연결이 저장마다 끊기지 않습니다.

Instance가 없는 화면은 expansion이 **복사조차 하지 않고** authored asset을 그대로 반환합니다(무비용 경로).

## 수정 파일

### 신규

| 파일 | 내용 |
|---|---|
| `Runtime/Metadata/DesignerComponentDefinitionAsset.cs` | Definition asset, Exposed Property, Slot Definition, Variant Property/Rule |
| `Runtime/Metadata/DesignerComponentInstanceMetadata.cs` | Instance 참조, Property Override, Variant Selection |
| `Editor/Properties/DesignerPropertyApplier.cs` | Typed property Apply/Read |
| `Editor/Components/Definitions/DesignerComponentExpander.cs` | Expansion, issue 보고, 결정적 id |
| `Editor/Components/Definitions/DesignerComponentLibrary.cs` | 프로젝트 색인, resolver, 검색/즐겨찾기/사용처 |
| `Editor/Components/Definitions/DesignerComponentService.cs` | Authoring 연산 |
| `Editor/Components/Definitions/DesignerComponentLibraryWindow.cs` | Component Library 창 |
| `Editor/Inspectors/ComponentInstanceInspector.cs` | Instance Inspector 섹션 |
| `Editor/Validation/DesignerComponentValidation.cs` | Component validation rule |
| `Editor/Core/NexUIDesignerContext.Components.cs` | Expansion 캐시, `PreviewElements`, authored 역매핑 |
| `Tests/EditMode/DesignerComponentSystemTests.cs` | 24개 EditMode 테스트 |
| `Documentation~/advanced/reusable-components.md` | 사용 문서 |

### 수정

| 파일 | 변경 |
|---|---|
| `Runtime/Metadata/DesignerElementMetadata.cs` | `componentInstance` 필드 추가 (추가 전용) |
| `Runtime/Metadata/DesignerMetadataAsset.cs` | `CurrentSchemaVersion` 3 → 4 |
| `Editor/Core/DesignerHierarchyMigration.cs` | v3 → v4 단계 추가 |
| `Editor/Core/NexUIDesignerContext.cs` | Save가 expansion 사용, expansion 무효화 지점 3곳, Dispose 정리 |
| `Editor/Viewport/NexUIDesignerViewport.cs` | `PreviewElements` 렌더 + authored view key |
| `Editor/Serialization/DesignerSavePreviewService.cs` | Dry run이 expansion 사용 |
| `Editor/Validation/DesignerValidationService.cs` | Component validation 호출 |
| `Editor/Components/DesignerComponentRegistry.cs` | `ComponentInstance` descriptor 등록 |
| `Editor/Inspectors/DesignerInspectorRegistry.cs` | `component-instance` 섹션 등록 |
| `Localization/en-US.json`, `ko-KR.json` | 창/섹션 키 3개 |
| `Documentation~/*` | index, ImplementationStatus, validation-catalog, PhasedImplementationPlan |
| `CHANGELOG.md` | Phase 3 항목 |

## Metadata 및 API 변경

### 신규 Metadata

```csharp
DesignerElementMetadata.componentInstance : DesignerComponentInstanceMetadata   // 추가 전용

DesignerComponentInstanceMetadata { definitionGuid, definitionId, definitionVersion,
                                    detached, overrides[], variantSelections[] }
DesignerComponentPropertyOverride { exposedPropertyName | (targetElementId, propertyId), value }
DesignerComponentVariantSelection { propertyName, value }

DesignerComponentDefinitionAsset  : ScriptableObject
  { schemaVersion, componentId, version, displayName, category, description, tags, thumbnail,
    defaultSize, rootElementId, elements[], exposedProperties[], slots[],
    variantProperties[], variantRules[] }
```

### 신규 API

```csharp
IDesignerComponentDefinitionResolver.Resolve(guid, componentId)
DesignerComponentExpander.Expand(asset, resolver) → DesignerComponentExpansion   // Dispose 필요
DesignerComponentExpander.HasInstances(asset)
DesignerComponentExpander.DeterministicStableId(instanceStableId, definitionStableId)

DesignerPropertyApplier.Apply(element, propertyId, value) → bool
DesignerPropertyApplier.Read(element, propertyId) → DesignerPropertyValue

DesignerComponentService.CreateDefinitionFromSubtree / Instantiate / SetOverride /
  ResetOverride / ResetAllOverrides / Detach / Swap / UpdateFromDefinition

DesignerComponentLibrary.All / Search / Categories / Tags / Resolve / Resolver /
  IsFavourite / SetFavourite / FindUsages / Invalidate / Changed

NexUIDesignerContext.PreviewElements / ComponentIssues / HasComponentInstances /
  IsGeneratedByComponent / ResolveAuthoredElement / InvalidateComponentExpansion
```

기존 public API는 제거하거나 시그니처를 바꾸지 않았습니다.

## Migration

* Schema **v3 → v4**. 기존 v0/v1/v2 경로는 그대로 유지됩니다.
* `componentInstance`는 순수 추가 필드입니다. Unity가 v3 asset을 역직렬화하면 기본 인스턴스를 채우고,
  `definitionGuid`가 비어 있으므로 Component Instance가 아닌 것으로 취급됩니다.
  → **기존 화면의 시각적 결과와 backend 출력이 전혀 바뀌지 않습니다.**
* v3 → v4 단계가 하는 일은 두 가지뿐입니다.
  1. 코드로 만든 metadata(테스트/importer/AI apply)에서 `componentInstance`가 null인 경우 정규화
  2. `propertyId == None`이고 exposed 이름도 없는 — 적용 자체가 불가능한 — override 제거
* 반복 실행해도 안전합니다(`schemaVersion` 스탬프 + 두 번째 실행은 `false` 반환).
* Undo는 기존 migration 경로와 동일하게 `Undo.RecordObject`로 기록됩니다.
* Prefab / UXML은 건드리지 않습니다. Component가 생성한 backend object는 결정적 stableId를 쓰므로
  첫 저장에서 새로 생성되고, 이후 저장에서는 같은 object에 재연결됩니다.

## 테스트

### 실행한 것

```bash
pwsh ./Tools/Validate-NexUI.ps1
```

결과: `NexUI static validation passed.`

```bash
dotnet build emiteat.NexUI.Designer.Tests.EditMode.csproj
```

결과: **오류 0개, 경고 0개**. 이 빌드는 `emiteat.NexUI.Designer.Runtime`,
`emiteat.NexUI.Designer.Editor`, `emiteat.NexUI.Integrations.Figma`,
`emiteat.NexUI.Designer.Tests.EditMode` 어셈블리를 모두 포함합니다.

Unity EditMode 테스트 러너(6000.4.2f1, batch mode, 전체 EditMode 스위트):

```text
total=312  passed=297  failed=15
```

* `DesignerComponentSystemTests` **24개 전부 통과**.
* `DesignerCoordinateAndMigrationTests` 10개 전부 통과 (schema v4 migration 포함).

프로젝트가 Unity Editor에서 열려 있어 원본 project path로는 batch mode가 lock을 얻지 못했습니다.
`Assets`/`Packages`를 junction으로 연결한 별도 project path에서 실행했습니다.

#### 첫 실행에서 잡힌 실제 버그

첫 실행에서 13개 component 테스트가 `NullReferenceException`으로 실패했습니다.
원인은 `DesignerComponentExpander.Expand`가 생성한 expanded asset을 `result.Expanded`에 **대입하지 않은 것**이었습니다.
Instance가 있는 화면에서 Save/Preview/Validation이 모두 null을 받는 심각한 버그였고, 테스트가 이를 잡았습니다.
수정 후 24개 전부 통과합니다.

#### 남은 15개 실패는 Phase 3 이전부터 존재하던 것

Phase 3 코드 경로를 전혀 지나지 않습니다. 그중 2개는 Runtime 패키지(`emiteat.NexUI.Tests.EditMode`)
테스트로, 이번 작업에서 건드리지 않은 어셈블리입니다.

| 실패 테스트 | 성격 |
|---|---|
| `DesignerInspectorRegistryTests.SearchKeywordsDiscoverExpectedSection("backend","capabilities")` | Section Title이 localize되어 영어에서만 "backend"를 포함. OS 언어가 한국어라 실패 |
| `DesignerMotionPersistenceTests.CompanionJson_RoundTripsFullMetadataSchema` | 존재하지 않는 부모(`grid`)를 가리키는 element가 normalize로 root로 분리되어 `siblingIndex`가 0이 됨 |
| `DesignerMotionPersistenceTests.SavePreview_IsReadOnlyAndCategorizes...` | uGUI Unsupported 집계 개수(Phase 1 범위) |
| `DesignerMotionPersistenceTests.UguiSave_AppliesTypedLayoutVisualAndTypography` | `UGUIAssetSerializer`를 직접 호출. Prefab에 `play` object가 생성되지 않음 |
| `DesignerUIStateTests` 2건 | Grid/Snap 기본값 의존 |
| `DesignerUndoConsistencyTests` 2건 | Snap 결과 좌표 의존 |
| `GeneratedAssetWriterTests` 2건 | 의도된 Error 로그를 `LogAssert`로 예상하지 않음 |
| `NexUIAIServiceTests` 2건 | 기본 element 높이/context 스냅샷 형식 |
| `ProductivityServiceTests.ScreenWizard_CreatesConnectedAssets(UIToolkit,FullScreen)` | 생성된 UXML의 ScriptedImporter 오류 로그 |
| `UIGraphPhase6Tests` 2건 | Runtime 패키지. 이번 작업 범위 밖 |

이 15개는 Phase 3의 완료 조건이 아니지만 **별도로 처리해야 할 실제 부채**입니다.

재현 명령(Unity Editor를 닫은 뒤):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Unity.exe" -batchmode -nographics -projectPath E:/UnityProjects/NexUI -runTests -testPlatform EditMode -testResults results.xml -logFile unity.log
```

### 테스트가 덮는 범위

| 영역 | 테스트 |
|---|---|
| 무비용 경로 | Instance 없는 화면은 authored asset을 그대로 반환 |
| Identity | Instance가 definition root가 되고 elementId/stableId/rect 유지 |
| Identity | 생성 자식 id prefix와 위치 offset |
| Identity | 생성 stableId가 두 번의 expansion에서 동일 |
| Identity | 같은 definition의 두 Instance가 서로 다른 stableId |
| 데이터 안전성 | Expansion이 authored asset과 definition을 변경하지 않음 |
| Override | Exposed property override 적용 |
| Override | 해결되지 않는 override 보고 |
| Variant | Rule의 override + visibility, Instance override 우선 |
| Variant | 선택이 없을 때 default 적용 |
| Variant | 선언되지 않은 variant 선택 보고 |
| Slot | Slot host로 재부모화 |
| Slot | 알 수 없는 slot은 root 유지 + 보고 |
| Slot | 필수 slot 비어 있음 보고 |
| Slot | 타입 거부는 보고만, 자식 삭제 없음 |
| 실패 처리 | Definition 없음 → Instance와 자식 보존 |
| 실패 처리 | 자기 참조 cycle 검출 후 종료 |
| 중첩 | Component 안의 Component 전개 |
| 버전 | 버전 불일치 보고 |
| 상태 | Detach된 Instance는 전개하지 않음 |
| Property | Applier write/read 왕복 |
| Property | 표현 없는 property는 false 반환 |
| Migration | v3 → v4 멱등성 + authored 값 보존 |
| Migration | 적용 불가 override 제거 |

## 수동 검증

Unity Editor에서 순서대로 확인하세요.

1. **Migration 안전성**
   `Tools > NexUI > Designer`로 기존 화면을 엽니다.
   Console에 `schema v3 → v4` migration 로그가 한 번 뜨고, Canvas와 Inspector가 이전과 동일한지 확인합니다.
   Save 후 생성된 Prefab/UXML을 Git diff로 비교해 **의미 있는 변경이 없는지** 확인합니다.

2. **Component 생성**
   Panel 하나와 그 자식 몇 개를 만들고 Panel을 선택합니다.
   `Tools > NexUI > Designer > Component Library` → Folder를 `Assets/UI/Components`로 두고
   **Create Component From '...'** 를 누릅니다.
   → 새 asset이 생기고, Hierarchy에서 자식들이 사라지고 Panel 하나만 남는지 확인합니다.
   → Canvas 모양은 그대로여야 합니다.

3. **Instance 배치와 전파**
   같은 화면(또는 다른 화면)에 **Place Instance**로 두 번째 Instance를 놓습니다.
   Project 창에서 Definition asset을 선택해 `elements` 중 하나의 `text`를 바꿉니다.
   → 두 Instance가 모두 즉시 바뀌는지 확인합니다.

4. **Override**
   Instance를 선택 → Inspector의 **Component Instance** 섹션에서 Exposed Property 값을 바꿉니다.
   → 해당 Instance만 바뀌고 다른 Instance는 definition 값을 유지하는지 확인합니다.
   → **Reset**을 누르면 definition 값으로 돌아오는지 확인합니다.

5. **Slot**
   Instance 아래에 새 Element를 만들고 `parentSlotId`를 `content`로 둡니다.
   → Canvas에서 definition이 지정한 slot host 위치에 배치되는지 확인합니다.

6. **Save round trip**
   Save Preview를 열어 Component가 만들 object가 Create 목록에 나오는지 확인하고 Save합니다.
   → 다시 Save했을 때 **같은 object가 재사용되고 새로 생성되지 않는지**(결정적 stableId) 확인합니다.

7. **Definition 삭제 대응**
   Definition asset을 다른 폴더로 옮기거나 삭제합니다.
   → `component-definition-missing` **Error**가 뜨고 Instance element와 slot 자식이 **사라지지 않는지** 확인합니다.
   → 되돌린 뒤 정상 복구되는지 확인합니다.

8. **Detach와 Undo**
   Instance에서 **Detach**를 누릅니다(확인 대화상자가 떠야 합니다).
   → 펼쳐졌던 element가 실제 authored element로 나타나고 모양이 그대로인지 확인합니다.
   → `Ctrl+Z`로 완전히 되돌아가는지 확인합니다.

9. **Domain Reload**
   화면을 연 채로 스크립트를 저장해 domain reload를 일으킵니다.
   → Canvas가 정상 복구되고 Console에 expansion 관련 예외가 없는지 확인합니다.

## 남은 위험

* **PlayMode 테스트 미실행.** Phase 3는 Editor authoring 범위라 PlayMode 테스트를 추가하지 않았습니다.
  Component가 생성한 backend object의 런타임 동작은 기존 backend 테스트에만 의존합니다.
* **수동 검증 미수행.** 아래 §수동 검증은 아직 실행되지 않았습니다. 특히 uGUI Prefab round trip(6번)은
  자동 테스트가 없는 구간입니다.
* **기존 실패 테스트 15건이 남아 있습니다.** Phase 3 원인은 아니지만 CI를 녹색으로 만들려면 처리해야 합니다.
* **Instance 리사이즈가 자식에 전달되지 않습니다.** Instance rect를 definition root와 다르게 잡으면
  root만 그 크기를 갖고 자식은 authored offset을 유지합니다. Auto Layout을 쓰면 재배치되지만,
  자유 배치 definition은 잘려 보일 수 있습니다. 문서에 명시했습니다.
* **Definition 전용 편집 모드가 없습니다.** 현재 Definition은 Unity 기본 Inspector로 편집합니다.
  Slot / Exposed Property / Variant Rule을 손으로 채워야 해서 실사용 난도가 높습니다.
* **Version migration이 자동 재매핑을 하지 않습니다.** `UpdateFromDefinition`은 버전 스탬프를 갱신하고
  해결되지 않는 override를 **보고**만 합니다(삭제하지 않음). 자동 property 재매핑은 미구현입니다.
* **Motion / Theme / Responsive Override가 Variant Rule에 없습니다.** Phase 5–7 범위입니다.
* **Backend Template 미구현.** Definition element가 그대로 backend로 나갑니다.
* **Component가 만든 backend object의 orphan 처리**는 기존 orphan report 경로를 그대로 씁니다.
  Instance를 삭제하면 이전에 생성된 prefab object가 orphan으로 보고됩니다(자동 삭제하지 않음 — 의도된 동작).
* **성능 미측정.** `NexUI.Designer.ComponentExpansion` ProfilerMarker는 넣었지만 큰 화면에서의
  expansion 비용은 측정하지 않았습니다.

## 다음 Phase

Phase 4 — Binding, Interaction, Input, Focus. 자동으로 시작하지 않습니다.

권장 선행 작업:

1. 위 수동 검증 1·6·7·8(migration, save round trip, definition 삭제, detach/undo) 수행
2. 기존 실패 테스트 15건 정리 — 특히 `UguiSave_AppliesTypedLayoutVisualAndTypography`는
   uGUI backend 출력의 실제 결함일 수 있습니다
3. Definition 전용 편집 UI — Phase 4의 typed binding을 Component에 노출하려면 먼저 필요합니다
