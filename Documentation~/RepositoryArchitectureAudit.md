# Repository Architecture Audit

> [!NOTE]
> 이 문서는 **Phase 0 착수 시점의 감사 기록**입니다. 당시 상태와 그때 판단한 위험을 남겨 두기 위해 갱신하지 않습니다.
> 현재 구조는 [아키텍처](developer/architecture.md)와 [프로젝트 구조](developer/project-structure.md),
> 현재 기능 상태는 [현재 기능 상태](reference/feature-status.md)를 확인하세요.
> 이후 진행 결과는 [Phase 0](Phase0CompletionReport.md) · [Phase 1](Phase1CompletionReport.md) · [Phase 3](Phase3CompletionReport.md) 완료 보고서에 있습니다.

감사 기준일: 2026-07-27  
대상: `com.emiteat.nexui` Runtime 패키지와 `com.emiteat.nexui.designer` Editor 패키지의 당시 워크스페이스 상태

## 1. 요약

NexUI는 공통 Abstraction, Core 오케스트레이션, 기능별 Runtime 모듈, uGUI/UI Toolkit Integration, Designer Runtime Metadata, Designer Editor로 책임이 나뉘어 있다. Runtime asmdef에서 `UnityEditor`를 직접 참조하는 위반은 발견하지 못했다.

핵심 위험은 구조 부재보다 기능 경로 간 불일치다. Component Registry와 Preview는 넓은 기능을 표현하지만, Serializer와 실제 Backend 출력은 일부 컴포넌트만 완성되어 있다. `NexUIDesignerContext`에는 선택, 편집, 복제, 세션, 저장, Validation 책임이 집중되어 있으며, 여기의 수동 복제 코드가 Metadata 확장 시 누락을 만든다.

## 2. Assembly 구조

### Runtime

- `emiteat.NexUI.Abstractions`: Surface, Element Handle, Capability, Focus, Motion, Resource Provider 계약.
- `emiteat.NexUI.Core`: Screen Registry, Layer, UIManager, Policy, Back/Modal/Toast, Variant/Responsive 모델.
- `State`, `Motion`, `MotionClip`, `MotionGraph`, `Theme`, `Components`, `Query`, `Settings`, `Accessibility`, `Localization`, `Prompt`, `Templates`: 독립 Runtime 모듈.
- `Integrations.UGUI`, `Integrations.UIToolkit`: Backend Surface/Factory/Capability 구현.
- `Integrations.Addressables`, `InputSystem`, `DOTween`, `MessagePipe`, `VContainer`: 선택적 Integration.

### Designer

- `emiteat.NexUI.Designer.Runtime`: 직렬화 가능한 Metadata와 Scenario/Motion Trigger Runtime 데이터. `UnityEditor` 참조 없음.
- `emiteat.NexUI.Designer.Editor`: Window, Context, Preview Adapter, Serializer, Validation, Productivity, AI, Responsive, Variant 도구.
- `emiteat.NexUI.Integrations.Figma`: Figma Import용 Editor 전용 Assembly.
- `emiteat.NexUI.Designer.Tests.EditMode`: Designer Editor 테스트.

## 3. Runtime / Editor 경계

현재 asmdef 의존성은 Runtime → Editor 역참조 없이 구성되어 있다. `AssetDatabase`, `Undo`, `PrefabUtility`, `EditorWindow`는 Designer Editor 또는 NexUI Editor Assembly에 위치한다.

주의점:

- Designer Runtime Metadata가 Unity `Object` 참조를 포함하므로 JSON Companion은 GUID/local file id 변환 계층을 반드시 유지해야 한다.
- Runtime의 Variant/Responsive override는 공통 Capability와 Backend 확장 callback을 통해 적용되며, 문자열 `propertyPath`는 Phase 1의 Typed Property로 교체해야 한다.

## 4. 핵심 Context 책임

`NexUIDesignerContext`가 다음을 동시에 담당한다.

- 현재 Screen/Metadata 세션
- Preview 생성과 갱신
- 선택, Multi-selection, Hierarchy 편집
- Duplicate/Copy/Paste/Group
- Undo/Dirty/Save/Discard
- Validation과 UI 상태
- Motion preview와 Scenario 연동

단기적으로 partial class 분리는 되어 있지만 상태와 변경 경로는 하나의 Context에 집중되어 있다. Phase 0에서는 데이터 안전 경로를 공통 Utility로 통합하고, 이후 Phase에서 Selection Service, Document Session, Clipboard/Clone Service로 책임을 분리하는 것이 안전하다.

## 5. Backend 구조

- `INexUIDesignerBackend`: Editor Preview Surface 생성 및 조작.
- `IUIScreenFactory`/`IUISurface`: Runtime 인스턴스 생성과 공통 조작.
- `UGUIAssetSerializer`: Prefab contents를 열어 Metadata를 반영.
- `UIToolkitAssetSerializer`: 사용자 UXML은 보존하고, Generated Marker가 있는 파일만 재생성.

현재 uGUI Runtime 조회에는 `NxUGuiBindingTag` Stable ID가 존재하지만 Designer Prefab 저장은 이름 검색을 우선 사용한다. Phase 0에서 Serializer도 Stable ID 우선으로 통일해야 한다.

## 6. Serialization 구조

- Unity Asset/YAML: `DesignerMetadataAsset`, `UIScreenDefinition`의 권위 데이터.
- Companion JSON: Git diff/merge용 DTO 직렬화.
- uGUI: Prefab load/save.
- UI Toolkit: Generated UXML/USS 또는 사용자 작성 파일 보존 + mismatch report.

Companion JSON은 format version 2에서 전체 Metadata와 Sprite sub-asset local id를 기록한다. Legacy JSON은 당시 존재하지 않던 필드를 보존하는 merge 경로가 있다.

## 7. Preview 구조

Designer Backend가 Preview Surface를 만들고 Context가 Metadata를 Surface element에 적용한다. Component Registry는 Palette, 기본값, Slot, State, Binding, Backend support 정보를 중앙화한다.

남은 문제:

- Preview와 실제 Serializer의 property coverage가 동일하지 않다.
- 일부 Component는 Registry/Preview는 존재하지만 실제 uGUI/UI Toolkit output은 Partial 또는 PreviewOnly다.
- Editor visibility와 Runtime visibility가 한 필드로 결합되어 있다.

## 8. Validation 구조

Designer Validation은 Screen/Metadata mismatch, ID, hierarchy, slot, binding support, references, motion, backend element, uGUI prefab 문제를 검사한다. Runtime Project Validator는 layer policy, loading strategy, variants, responsive rule 등을 검사한다.

부족한 검사:

- Stable ID 중복과 이름 fallback migration report.
- Editor hidden과 Runtime hidden의 잘못된 조합.
- Clone 결과의 내부 객체 공유.
- 문서 링크, `.meta`, Runtime `UnityEditor` 참조의 CI gate.

## 9. Test 구조

- Runtime EditMode: Core, State, Motion, Graph, Validator.
- Runtime PlayMode: UIManager 기본 lifecycle과 실제 frame 기반 motion.
- Designer EditMode: Metadata, hierarchy, preview, serializer generator, validation, scenario, AI, Figma, sample smoke.
- GitHub Actions: Unity 6000.4.2f1에서 EditMode/PlayMode matrix 실행.

현재 열린 Unity 인스턴스가 프로젝트를 점유할 때 별도 batchmode Test Runner를 동시에 실행할 수 없으므로, 로컬 보고에서는 C# compile과 실제 Unity Test Runner 실행을 구분해야 한다.

## 10. Migration 구조

현재 Designer Migration은 schema v0 → v1 hierarchy sibling index migration 중심이다. 반복 실행 안전성 테스트는 존재한다.

Phase 0에서 필요한 v1 → v2 migration:

- 기존 `hiddenInDesigner` 의미를 Editor visibility로 유지.
- 신규 `runtimeVisible`은 기존 Backend 결과를 보존하기 위해 `!hiddenInDesigner`로 초기화.
- Migration 실행 전 Undo/backup 경로와 report를 제공.

## 11. 결론

Phase 0의 차단 항목은 다음 네 가지다.

1. 모든 복제 경로를 schema-resilient deep clone/subtree remap으로 통합.
2. Editor visibility와 Runtime visibility 분리 및 v2 migration.
3. uGUI Serializer를 Stable ID 우선으로 전환하고 fallback migration/orphan report 추가.
4. CI에 package/meta/assembly/document/generated asset 검사를 추가.

