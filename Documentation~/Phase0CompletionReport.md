# Phase 0 Completion Report

기준일: 2026-07-27

## 결과

Phase 0의 코드와 자동화 기반 구현을 완료했다. Release gate 중 Unity Test Runner 실제 실행과 Inspector/Prefab 수동 시각 검증은 열린 Unity Editor와 동일 프로젝트를 두 번째 batchmode 프로세스로 열 수 없어 이번 작업에서 실행하지 않았다. Phase 1은 시작하지 않는다.

## 실제 변경

### 0.1 Discard Changes

- Screen과 Metadata를 Open/Save 시점 JSON baseline으로 보관하고 동일 경로로 복원한다.
- 복원 뒤 Preview, Selection, Inspector 구독 대상, Canvas와 Validation을 갱신한다.
- Unity dirty count로 외부 Inspector 변경을 감지하며, interactive 환경에서는 전체 폐기 전 추가 확인을 요구한다.
- baseline 시작 시 이미 dirty였던 외부 상태는 복원 후에도 dirty로 보존한다.

### 0.2 Deep Clone

- `JsonUtility` 기반 전체 metadata deep clone으로 수동 필드 복사를 제거했다.
- Duplicate, Copy/Paste, Alt-drag가 같은 clone 경로를 사용한다.
- 선택한 부모의 전체 subtree를 복제하고 parent/focus/motion target ID를 새 ID로 재연결한다.
- clipboard는 원본 참조가 아닌 snapshot을 저장한다.
- 복제 요소는 새 `elementId`와 새 `stableId`를 받으며 Unity Object reference는 유지한다.

### 0.3 Visibility separation

- schema를 v2로 올리고 `hiddenInDesigner`와 `runtimeVisible`을 분리했다.
- v1 migration은 기존 출력 호환을 위해 `runtimeVisible = !hiddenInDesigner`로 이관하고 비어 있는 stable identity를 생성한다.
- Style Inspector와 Layers tooltip에서 Editor-only/Runtime 의미를 구분한다.
- uGUI active state와 UI Toolkit `display`는 `runtimeVisible`만 사용한다.

### 0.4 Stable identity

- `NxUGuiBindingTag`에 immutable `stableId`, public `elementId`, ownership을 추가했다.
- uGUI serializer는 prefab tree를 한 번 순회해 stable ID, tag element ID, unique name index를 만든다.
- stable ID를 우선 사용하며 legacy name fallback으로 찾은 객체에는 identity를 자동 부착한다.
- rename은 Designer-owned 객체를 재사용하고, 사용자 객체는 이름을 강제로 바꾸지 않는다.
- metadata/prefab duplicate ID는 prefab write 전에 오류로 차단한다.
- Designer-owned orphan은 삭제하지 않고 save report warning으로 남긴다.

### 0.5 CI and tests

- 두 실제 Git 저장소 각각에 package validation과 Unity EditMode/PlayMode workflow를 추가했다.
- CI는 임시 Unity host project를 만들고 UPM package를 `testables`로 로드한다.
- 정적 검증은 package.json, `.meta`, Runtime의 UnityEditor 참조, 문서 링크, merge/generated 임시 파일을 검사한다.
- Unity license secret과 artifact 정책을 Testing 문서에 기록했다.

## 주요 수정 파일

- `Editor/Core/NexUIDesignerContext.cs`
- `Editor/Core/DesignerHierarchyMigration.cs`
- `Editor/Serialization/DesignerMetadataUtility.cs`
- `Editor/Serialization/DesignerMetadataJsonSerializer.cs`
- `Editor/Serialization/UGUIAssetSerializer.cs`
- `Editor/Serialization/UIToolkitCodeGenerator.cs`
- `Editor/Validation/DesignerValidationService.cs`
- `Runtime/Metadata/DesignerElementMetadata.cs`
- `Runtime/Metadata/DesignerMetadataAsset.cs`
- NexUI Runtime의 `Integrations/UGUI/NxUGuiBindingTag.cs`, `UGUISurface.cs`
- 두 저장소의 `.github/workflows/unity-tests.yml`, `Tools~/Validate-Package.ps1`

## 검증 결과

- aggregate/package 정적 검증: 통과.
- workflow YAML parse: 통과.
- `emiteat.NexUI.Designer.Editor.csproj`: 경고 0, 오류 0.
- `emiteat.NexUI.Designer.Tests.EditMode.csproj`: 경고 0, 오류 0.
- Unity Test Runner EditMode/PlayMode: CI 및 테스트 코드는 추가했으나 이번 로컬 세션에서는 미실행.

추가한 회귀 테스트는 실제 Screen/Metadata discard, nested metadata와 Unity Object clone, clipboard snapshot, subtree/focus/motion remap, visibility migration, UI Toolkit runtime visibility, uGUI stable rename/runtime visibility/duplicate/orphan, companion JSON stable identity를 포함한다.

## 수동 검증 절차

1. Designer에서 Screen과 Metadata를 열고 둘 다 수정한 뒤 창 닫기에서 Discard를 선택한다. 두 Asset, Preview, Inspector, Validation이 Open 시점으로 돌아가는지 확인한다.
2. 같은 Asset을 Inspector에서 외부 수정한 뒤 Designer Discard를 실행한다. External Changes 확인창에서 Cancel과 Discard All Changes를 각각 확인한다.
3. 3단 hierarchy를 Copy/Paste와 Alt-drag로 복제한다. parent 관계, focus target, motion binding이 복제 subtree 내부 새 ID를 가리키는지 확인한다.
4. Editor Hidden과 Runtime Visible을 서로 다른 조합으로 저장해 Designer canvas, uGUI prefab active state, 생성 USS의 결과를 확인한다.
5. stable tag가 있는 uGUI 객체의 element ID를 변경 후 저장한다. 동일 GameObject와 사용자 component/reference가 유지되는지 확인한다.
6. duplicate stable ID와 Designer-owned orphan을 만든 뒤 Save한다. duplicate에서 prefab write가 차단되고 orphan은 삭제되지 않는지 확인한다.
7. Unity Test Runner에서 전체 EditMode와 PlayMode를 실행한다.

## 남은 Partial

- UI Toolkit은 backend-native stable identity를 별도 직렬화하지 않고 element name을 public identity로 사용한다. 공통 backend round trip은 Phase 8 범위다.
- 외부 변경 감지는 Unity dirty count 기반이며 field-level three-way merge는 제공하지 않는다.
- orphan은 report-only이고 자동 삭제하지 않는다.
- CI가 사용하는 Designer runtime checkout은 runtime Phase 0 변경이 먼저 master에 반영되어야 한다.

## 다음 Phase 권장 작업

Phase 1의 Typed Property Schema와 Backend Property Parity를 시작하기 전에 Runtime 저장소 변경을 먼저 병합하고, Designer CI 전체 통과 및 위 수동 검증을 release gate로 확인한다.
