# Studio 창

`Tools > NexUI > Studio`는 화면 제작의 중심 창입니다.

## 배치 바꾸기

각 영역 헤더 오른쪽의 **⧉** 버튼을 누르면 그 영역이 **독립 창**으로 빠져나갑니다. 빠져나간 창은 Unity의 다른 창과 똑같이 다룰 수 있습니다.

* 아무 곳에나 **도킹**, 탭으로 합치기, 띄워두기, 다른 모니터로 이동
* 배치는 **Unity 레이아웃에 저장**되므로 에디터를 껐다 켜도 유지되고, 레이아웃 저장/전환에도 따라갑니다
* 그 창을 **닫으면 원래 자리로 돌아옵니다**

Studio 창은 영역이 빠진 만큼 자동으로 다시 배치합니다. 캔버스는 빠지지 않습니다 — Studio 창 자체가 캔버스이기 때문입니다.

`Tools > NexUI > Panels`에서 필요한 패널만 골라 열 수도 있습니다. Hierarchy·Library·Project Assets는 Studio 창에 구멍을 내지 않고 **추가 창**으로 열립니다(Unity에서 Project 창을 두 개 띄우는 것과 같습니다).

배치가 꼬였다면 `Tools > NexUI > Panels > Dock All Back Into Studio`로 전부 되돌립니다.

> 빠져나간 패널은 **현재 활성 Studio 창**을 따라갑니다. Studio 창을 여러 개 열어 두고 다른 창을 클릭하면, 빠져나간 Inspector도 그 화면을 가리킵니다.

## 영역 이름

각 영역 맨 위에는 **영역 이름과 지금 무엇을 보고 있는지**를 알려주는 한 줄 헤더가 있습니다. 예를 들어 왼쪽 사이드바는 `탐색 — 열린 화면의 구조`처럼 표시되고, 탭을 바꾸면 설명도 함께 바뀝니다. 캔버스 헤더에는 현재 열린 Screen ID가 나옵니다.

| 영역 | 역할 |
|---|---|
| Global Toolbar | Screen 선택, Backend 상태, Beginner/Pro 전환, Preview·Validation·Save 실행 |
| Left Sidebar | Metadata 선택과 **Layers · Components · Assets** 탭 |
| Canvas Toolbar | 선택 도구와 Preview 표시 옵션 조정 |
| Canvas | Element 선택, 배치, 크기 조절과 박스 선택 |
| Right Inspector | 전체 속성 검색, Workflow 필터, 접이식 Section 편집 |
| Bottom Drawer | Validation, History, Screen Graph, Preview Log |
| Command Palette | `Ctrl/Command+K` 또는 `Ctrl/Command+Shift+P`로 명령 검색 |

Left Sidebar의 세 탭은 각각 [Layers](hierarchy-and-layout.md)(구조 탐색), Components(새 Element 추가), [Assets](assets-panel.md)(프로젝트 Asset 탐색과 Canvas 드래그)입니다.

Beginner 모드는 일반 제작에 필요한 항목을 우선 표시합니다. Pro 모드는 Constraints, Theme, Policy와 Capability 같은 기술 항목을 추가로 보여 줍니다. Inspector의 `Build`, `Connect`, `Animate`, `Verify`, `Advanced` 필터는 데이터를 바꾸지 않고 현재 Workflow의 Section만 표시합니다.

Bottom Drawer 높이, 열린 탭, Workspace, Preview Mode와 Canvas Scroll은 EditorPrefs에 보존됩니다. Screen과 Metadata는 경로 대신 Asset GUID로 복원합니다.

> [!NOTE]
> 실제 Timeline 편집은 `Tools > NexUI > Studio > Advanced > Motion Clip Editor`에서 수행합니다.
