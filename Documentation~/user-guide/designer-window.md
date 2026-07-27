# Designer 창

`Tools > NexUI > Designer`는 화면 제작의 중심 창입니다.

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
> 실제 Timeline 편집은 `Tools > NexUI > Designer > Advanced > Motion Clip Editor`에서 수행합니다.
