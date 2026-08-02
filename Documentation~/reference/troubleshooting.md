# 문제 해결

각 문제에서 Console의 **첫 오류**를 먼저 해결하세요. 뒤의 오류는 첫 컴파일 실패에서 파생될 수 있습니다.

## Studio 메뉴가 보이지 않음 / Core 또는 UniTask를 찾지 못함

**가능한 원인:** Unity 버전 미달, Core/UniTask 누락, compile error입니다.

**해결 순서:** Unity 6000.4 이상인지 확인하고 UniTask 2.5.10, Core 0.1.0, Studio 0.1.0 순서로 설치합니다. Package Manager와 `Packages/manifest.json`을 확인한 뒤 Domain Reload를 기다립니다.

**정상 확인:** `Tools > NexUI > Studio`가 보입니다. 계속되면 Unity 버전, package 목록, manifest와 Console 첫 오류를 수집합니다.

## Studio 창 또는 Preview가 비어 있음

Screen, Metadata, Backend Asset이 모두 선택되었는지 확인하고 **Rebuild Preview**를 누릅니다. Screen/Metadata Screen ID와 Backend Asset 형식을 Validate합니다. 정상이라면 Layers와 Canvas에 Metadata Element가 표시됩니다.

## Screen을 선택했는데 반영되지 않음 / Metadata ID 불일치

Global Toolbar의 Screen과 Left Sidebar의 Metadata가 같은 화면인지 확인합니다. `metadata-screen-mismatch`를 해결하고 다시 Rebuild합니다. 최근 세션 복원 값이 오래되었다면 현재 Asset을 다시 지정합니다.

## Backend Asset이 없거나 Element가 선택되지 않음

uGUI는 Prefab, UI Toolkit은 `VisualTreeAsset`을 연결합니다. Element ID가 비어 있거나 중복되면 먼저 수정합니다. UI Toolkit에서는 UXML `name`과 Metadata Element ID가 같아야 합니다.

## Duplicate Element ID / Parent가 깨짐

Validation 결과의 Element로 이동합니다. Duplicate ID는 고유하게 바꾸고, `missing-parent`, `self-parent`, `circular-parent`는 Layers에서 올바른 Container로 재배치합니다. Auto Fix가 제시되면 적용 후 Hierarchy를 다시 확인합니다.

## Save 후 Prefab이 바뀌지 않음

uGUI Backend인지, Prefab이 연결됐는지 확인합니다. Save Report의 **Skipped**와 **Error**를 읽으세요. Preview-only 또는 미지원 속성은 Prefab에 쓰이지 않습니다.

연결은 Element 이름이 아니라 **Stable ID**로 이루어집니다. `duplicate-prefab-stable-id` 오류가 보이면 Prefab 안에 Identity Tag가 중복된 것입니다(대개 Studio가 만든 Object를 Prefab에서 복제한 경우). 복제본의 Tag를 제거하거나 재생성한 뒤 다시 저장하세요. Studio는 잘못된 Object에 쓰지 않기 위해 해당 Element를 건너뜁니다.

## UI Toolkit Save 후 UXML이 바뀌지 않음

정상일 수 있습니다. 일반 Save는 **Generated Marker가 없는** 사용자 UXML을 보존하고 Metadata만 저장합니다. UI Builder에서 수동 UXML을 편집하거나 Generation/Sync Publish로 `.g.uxml/.g.uss`를 만드세요.

대상 UXML이 Marker를 가진 생성물이라면 일반 Save에서도 다시 생성됩니다. 그런데도 파일이 그대로라면 생성 결과가 이전과 동일한 것이며, Save Report에 `unchanged`로 표시됩니다.

## Component Instance가 빈 상자로 보임 / `component-definition-missing`

Definition Asset이 이동·삭제되었거나 다른 프로젝트에서 온 화면입니다. Instance와 Slot 자식은 **삭제되지 않았습니다**.

Definition을 복구하면 그대로 돌아옵니다. 복구할 수 없다면 Inspector의 **Component Instance** Section에서 **Detach**를 눌러 현재 내용을 일반 Element로 물질화하세요. `component-definition-recovered` Info가 대신 보인다면 GUID만 바뀐 것이므로 한 번 저장하면 해결됩니다.

## Definition을 고쳤는데 Instance가 그대로임

Definition Asset을 저장했는지 확인하세요(Project 창에서 편집한 경우 Unity가 자동 저장하지 않을 수 있습니다). Studio 창을 다시 그리려면 아무 편집이나 하거나 **Rebuild Preview**를 누릅니다.

Instance가 해당 값을 **Override**하고 있으면 Definition 값이 보이지 않습니다. Inspector에서 해당 Property의 **Reset**을 누르세요.

## Asset을 Canvas로 드래그해도 아무 일이 없음

커서에 금지 표시가 떴다면 그 Asset에 정의된 동작이 없는 것입니다(지원 목록은 [Assets 패널](../user-guide/assets-panel.md) 참고).

Texture를 끌었는데 적용되지 않으면 Texture Type이 `Sprite`가 아닐 수 있습니다. Preview Log에 이유가 남습니다. Material과 Font는 **Element 위에** 떨어뜨려야 하며 빈 Canvas에서는 거부됩니다.

## `.g.uxml/.g.uss` 생성 실패 / Generated Marker 오류

경로가 `Assets/` 아래의 쓰기 가능한 위치인지 확인합니다. 두 파일의 이름과 Diff Preview를 확인합니다. Marker가 없는 기존 파일은 사용자 파일로 간주되어 덮어쓰기가 거부됩니다. 다른 이름을 선택하거나 수동 파일을 백업한 뒤 소유권을 정리합니다.

## Sync Conflict

Generated 파일과 Studio가 마지막 Publish 이후 모두 바뀐 상태입니다. Diff를 보고 **Studio 사용** 또는 **Backend 사용**을 선택합니다. 자동 Merge는 제공되지 않습니다. [Asset Ownership](asset-ownership.md)을 참고하세요.

## Motion Preview 미적용 / Motion 대상 누락

Clip, Track Target과 Motion Binding의 `targetElementId`가 현재 Metadata ID와 같은지 확인합니다. Missing Clip/Target Validation을 먼저 해결합니다. Runtime에서만 실패하면 Screen Lifecycle 또는 Trigger 통지 연결을 확인합니다.

## Binding Key Warning / Command가 실행되지 않음

Inspector Key와 Runtime `UIStateStore`/`UIActionResolver` 등록 Key의 철자와 형식이 같은지 확인합니다. Interactive Preview는 Command를 실행하지 않고 Preview Log만 남깁니다. 실제 실행은 Play Mode에서 확인합니다.

## Scenario Apply 결과가 예상과 다름

Scenario 값 형식과 Metadata Binding Channel을 확인합니다. Number만 보간되며 Text/Bool/Sprite/List는 단계 전환됩니다. Scenario는 실제 Runtime State를 바꾸지 않고 `screenId`도 적용 대상을 강제하지 않습니다.

## Undo 후 Preview 불일치 / Domain Reload 후 상태 문제

먼저 Rebuild Preview를 실행합니다. Screen과 Metadata 선택, 선택 Element ID, Zoom/Tab 복원 상태를 확인합니다. 재현되면 수행한 Undo 단계, 열린 Studio 창 수, Reload 직전 Dirty 상태, Console log와 관련 Asset을 수집합니다.

## 읽기 전용 Package 경로 오류

Package 내부 원본이나 Package Cache를 편집하지 마세요. Sample은 Package Manager에서 Import하여 `Assets/Samples` 복사본을 수정하고, 생성 경로도 프로젝트의 `Assets/` 아래로 지정합니다.

