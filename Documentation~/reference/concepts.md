# NexUI Studio 핵심 개념

## 런타임 패키지와 스튜디오 패키지

`com.nexengineworks.nexui`는 런타임 동작을 담당합니다. `com.nexengineworks.nexui.studio`는 Unity Editor 도구를 담당합니다.

게임플레이와 런타임 UI 코드는 런타임 패키지에 둡니다. 제작 보조 도구, 에디터 패널, 검증, 프리뷰, 메타데이터 편집은 스튜디오 패키지에 둡니다.

## Screen Definition

`UIScreenDefinition`은 런타임에서 사용하는 화면 에셋입니다. 스튜디오는 이 에셋을 프리뷰, 검증, 저장 작업의 루트 객체로 사용합니다.

## Studio Metadata

`DesignerMetadataAsset`은 에디터에서 작성한 제작용 데이터를 저장합니다.

자주 저장하는 메타데이터:

- 안정적인 element id
- binding 이름
- style class
- localization key
- responsive rule
- variant
- validation expectation
- 프리뷰 전용 메모

메타데이터는 일반 Unity 에셋으로 직렬화될 수 있도록 런타임 안전 어셈블리에 둡니다. 이 데이터를 편집하거나 검증하는 에디터 동작은 에디터 어셈블리에 둡니다.

## Backend

스튜디오는 `INexUIDesignerBackend`를 통해 UI 구현과 통신합니다.

백엔드가 담당하는 일:

- 특정 화면을 처리할 수 있는지 판단
- 프리뷰 콘텐츠 생성
- 계층 데이터 노출
- 선택 항목을 메타데이터와 연결
- serializer와 협력해 저장

이 구조 덕분에 UI Toolkit과 uGUI가 같은 스튜디오 창을 공유하면서도 서로 다른 렌더링 모델을 유지할 수 있습니다.

## Validation

검증은 정적 제작 오류를 찾는 단계입니다. Play Mode에 들어가기 전에 누락된 화면 참조, 깨진 element id, 잘못된 localization link, 누락된 contract, 잘못된 responsive rule, 삭제된 객체를 가리키는 메타데이터를 잡는 것이 목표입니다.

## Localization

스튜디오 UI 문자열과 게임 UI 로컬라이제이션은 분리됩니다. 스튜디오 로컬라이제이션은 에디터 라벨과 메뉴 텍스트를 제어합니다. 게임 로컬라이제이션 메타데이터는 화면 요소와 런타임 localization key를 연결합니다.

## Preview

뷰포트는 에디터 프리뷰 표면입니다. 구조, 상태, 계층, 제작 규칙을 확인하는 데 유용하지만 최종 애니메이션 타이밍, 입력, 플랫폼별 동작은 Play Mode에서 다시 확인해야 합니다.

## Element와 Component

Element는 Stable ID와 Parent를 가진 화면 항목입니다. Component는 해당 Element가 Panel, Button, ProgressBar 등 어떤 역할과 Binding/Event를 지원하는지 설명합니다.

## 재사용 Component

`DesignerComponentDefinitionAsset`은 Element sub-tree와 그 계약(Exposed Property, Slot, Variant)을 담은 사용자 제작 에셋입니다. 화면에 놓인 **Instance는 그 element를 복사하지 않고 참조만** 저장합니다.

그래서 Definition을 한 번 고치면 전파 버튼 없이 모든 Instance가 따라옵니다. Preview·Serializer·Validation은 `DesignerComponentExpander`가 만든 평탄화 트리를 소비하고, 이 결과는 메모리에만 존재하므로 사용자 Asset에 기록되지 않습니다.

Instance element는 전개 시 **Definition root가 됩니다** — 별도 wrapper object를 만들지 않고 자신의 `elementId`/`stableId`/배치를 유지하므로 uGUI Prefab object 연결이 저장마다 끊기지 않습니다. 자세한 내용은 [재사용 Component](../advanced/reusable-components.md)를 참고하세요.

## Binding과 Command

Binding은 `UIStateStore` Key를 표시 속성과 연결합니다. Command는 클릭 같은 사용자 동작을 `UIActionResolver`의 문자열 Action Key로 연결합니다. Studio는 Key 계약을 저장하지만 게임 로직을 구현하지 않습니다.

## Theme와 Variant

Theme은 여러 Screen이 공유하는 시각 Token 집합입니다. Variant는 입력 방식이나 상황에 따라 다른 설정을 선택하는 대안입니다. 둘 다 Preview만 보고 Runtime 선택 로직이 자동 완성됐다고 판단해서는 안 됩니다.

Variant라는 이름은 두 곳에 쓰입니다. **Screen Variant**(`DesignerVariantMetadata`)는 한 화면 안의 Element Override 묶음이고, **Component Variant**(`DesignerComponentVariantProperty`)는 Definition이 선언한 축을 Instance가 고르는 것입니다. 서로 독립된 기능이며 데이터도 분리되어 있습니다.

## Motion Graph와 Motion Clip

Motion Graph는 Node 의존성 또는 Event Flow를 표현합니다. Motion Clip은 여러 Element의 Property를 Track과 Keyframe 시간축으로 표현합니다. 자세한 선택 기준은 [Motion 개요](../motion/overview.md)를 참고하세요.

## Scenario, Capability와 Policy

Scenario는 Preview 상태 값을 묶어 재현합니다. Capability는 Element가 제공하는 동작 계약이고 Policy는 Screen 실행 정책을 설명합니다. 이 데이터는 Validation과 확장 도구가 사용하지만 Backend가 모든 값을 자동 변환하지는 않습니다.

## Save와 Publish

Save는 현재 Metadata와 Backend Serializer를 실행합니다. Publish/Generation은 생성 파일이나 여러 Screen 배포 흐름을 다루는 별도 도구일 수 있습니다. UI Toolkit 일반 Save와 UXML Generation을 구분해야 합니다.
