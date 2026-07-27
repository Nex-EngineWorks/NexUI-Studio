# Designer 인터페이스 둘러보기

NexUI Designer는 `Tools > NexUI > Designer`에서 엽니다. 화면이 열리면 위에서 아래로 Global Toolbar, 작업 영역, Bottom Drawer가 보입니다. 작업 영역은 Left Sidebar, Canvas, Right Inspector의 세 열로 나뉩니다.

<!-- TODO Screenshot:
화면: NexUI Designer 전체 화면
표시할 항목: 1 Global Toolbar, 2 Left Sidebar, 3 Canvas Toolbar, 4 Canvas, 5 Right Inspector, 6 Bottom Drawer
권장 해상도: 1600×900 이상
파일 제안: images/designer-window-overview.png
-->

## Global Toolbar

창의 가장 위에 있습니다. **+ 새 화면**, **Screen**, Backend 배지, **Scenario**, **Beginner/Pro**, 상태, **AI**, **유틸리티**, **Preview**, **Validate**, **Save**가 있습니다.

- 새 작업은 **+ 새 화면**에서 시작합니다.
- 기존 화면은 **Screen**에 `UIScreenDefinition`을 지정합니다.
- **Scenario**는 선택한 Mock Data를 현재 Metadata Preview에 즉시 적용합니다.
- 상태가 `Ready`가 아니면 **Validate** 후 Bottom Drawer의 Validation을 확인합니다.
- **AI**는 현재 화면을 컨텍스트로 사용해 검토 가능한 편집 계획을 만듭니다. API 키 연결과 안전 범위는 [AI 어시스턴트](../user-guide/ai-assistant.md)를 참고하세요.

Screen은 Runtime 화면 정의이고 Metadata는 제작 데이터입니다. 두 필드를 같은 에셋으로 착각하지 마세요. 자세한 책임은 [Screen과 Metadata](../user-guide/screen-and-metadata.md)를 참고하세요.

## Left Sidebar

왼쪽 열에는 **Layers**, **Components**, **Assets** 탭과 **Metadata** 필드가 있습니다.

### Layers

Element의 부모·자식 관계와 형제 순서를 보여 줍니다. 행을 클릭해 선택하고 드래그하거나 Context Menu를 사용해 Reparent, 순서 변경, Key Object 지정을 수행합니다. Canvas에 보이는 위치와 Layers의 부모 관계는 서로 다른 개념입니다.

### Components

Panel, Container, Label, Button, Image, ProgressBar, Grid, Slot 등 등록된 Component를 검색하고 추가합니다. 버튼을 누르면 Metadata와 Canvas에 새 Element가 생깁니다. Backend별 지원 수준은 [Backend 지원 범위](../reference/backend-support-matrix.md)를 확인하세요.

### Assets

프로젝트 Asset을 폴더 단위로 탐색하고 검색합니다. Sprite, Font, Material, Component Definition을 Canvas로 바로 드래그할 수 있습니다. 자세한 동작은 [Assets 패널](../user-guide/assets-panel.md)을 확인하세요.

Rename·이동·삭제 같은 파일 관리는 Unity Project 창에서 수행합니다. Command Palette의 **Show Project Window**로 바로 열 수 있습니다.

## Canvas Toolbar

Canvas 바로 위에 있습니다.

- **Select / Move / Frame / Hand**: 선택, 이동, Frame 작업 준비, Pan 도구입니다.
- **Frame**: 1920×1080 같은 Preview 해상도를 바꿉니다.
- **State**: Normal, Hover, Pressed, Disabled, Focused를 강제로 표시합니다.
- **Input**: Keyboard, Gamepad, Touch, SteamDeck Preview 조건을 바꿉니다.
- **Snap / Grid**: 이동과 크기 조정 단위를 제어합니다.
- **-/+/Fit**: Zoom을 조절하고 화면을 맞춥니다.
- **Rebuild**: Backend Surface를 다시 만들고 Metadata를 적용합니다.
- **Layout / Transition**: Auto Layout 변환과 전환 Preset 도구를 엽니다.

`Rebuild`는 저장이 아닙니다. Preview를 다시 만드는 작업이며 실제 에셋 기록은 **Save**에서 수행합니다.

## Canvas

중앙 제작 영역입니다. 클릭, Shift/Ctrl 선택, Box Selection, 이동, 크기 변경과 Context Menu를 사용합니다. 여러 Element를 선택하면 마지막에 Key Object로 지정한 Element를 정렬 기준으로 사용할 수 있습니다.

Canvas 상단의 `Design / Interactive` 전환은 역할이 다릅니다.

- **Design**: 선택·이동·크기 변경 등 편집에 사용합니다.
- **Interactive**: Hover/Press/Click을 흉내 내고 Preview Log에 기록합니다. 실제 `UIActionResolver` Command는 실행하지 않습니다.

## Right Inspector

오른쪽 열은 선택한 Screen 또는 Element의 모든 편집 항목을 한 번에 보여 주는 통합 Inspector입니다. 탭을 오가며 속성을 찾는 대신 접이식 Section을 위에서 아래로 탐색합니다.

- **검색**: `position`, `command`, `backend`처럼 속성 이름이나 역할을 입력해 관련 Section만 남깁니다.
- **Workflow 필터**: `Build`, `Connect`, `Animate`, `Verify`, `Advanced`로 현재 작업에 필요한 Section만 집중해서 봅니다. `All`은 전체를 표시합니다.
- **접기/펼치기**: Section별 상태가 Editor에 보존되어 자주 쓰는 구성을 유지합니다.
- **Beginner/Pro**: Beginner는 필수·일반 Section을, Pro는 고급·진단 Section까지 표시합니다.

선택이 없으면 Screen Inspector가 나타납니다. 하나를 선택하면 Component, Layout, Visual, Binding, Motion과 Validation이 같은 열에 나타납니다. 여러 Element를 선택하면 공통 편집 Section이 나타납니다. 모드나 필터를 바꿔도 데이터는 삭제되지 않습니다.

## Bottom Drawer

아래쪽의 **Validation**, **History**, **Graph**, **Preview** 탭입니다. `Toggle`로 접고 펼치며 위쪽 Handle을 드래그해 높이를 바꿉니다.

- **Validation**: Error, Warning, Info와 가능한 Auto Fix를 보여 줍니다.
- **History**: 최근 Designer 편집 기록입니다.
- **Graph**: Screen과 Binding 요약입니다.
- **Preview**: Interactive Preview Log입니다.

Motion Timeline 편집은 Utilities의 **Motion Clip Editor**에서 수행합니다. 비어 있는 중복 Timeline 탭은 제공하지 않습니다.

## Command Palette

Designer에 초점을 둔 뒤 `Ctrl+K` 또는 `Ctrl+Shift+P`를 누릅니다. Save, Validate, Drawer 열기 같은 명령을 검색할 수 있습니다. 키 설정은 `Tools > NexUI > Designer > Preferences > 단축키 설정`에서 바꿉니다.

## Beginner Mode와 Pro Mode

Global Toolbar 또는 Right Inspector의 **Beginner/Pro** 버튼으로 전환합니다. Beginner는 처음 화면을 만드는 데 필요한 항목을 우선 보여 줍니다. Pro는 Constraints, Theme, Policy와 Backend Capability 같은 계약·확장 Section을 추가합니다. 데이터가 사라지는 모드가 아니라 표시 범위를 바꾸는 모드입니다.

## Satellite Window

Motion Clip Editor, Motion Graph, Motion State Machine, Screen Flow, Scenario Editor와 QA 도구는 별도 창으로 열립니다. 별도 창은 포커스를 가진 Designer Session의 Context를 사용하므로 여러 Designer 창을 열었다면 먼저 대상 Designer 창을 클릭하세요.

## 상태 저장

Sidebar/Bottom 탭, Inspector Section의 접힘 상태, Bottom Drawer 높이와 열림 상태, 최근 Screen, 유효한 선택, Canvas Zoom/Scroll 일부는 Editor 상태로 복원됩니다. Domain Reload 후 존재하지 않는 Element ID는 선택으로 복원하지 않습니다.

다음 단계는 [빠른 시작](quick-start.md), [첫 번째 Screen 만들기](first-screen.md), [자주 사용하는 작업](../user-guide/common-workflows.md)을 따라가세요.
