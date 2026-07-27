# Canvas 편집

Canvas는 Metadata Element를 시각적으로 배치하는 작업 공간입니다.

- 클릭으로 단일 선택합니다. `Shift`는 범위/추가 선택, `Ctrl` 또는 `Command`는 선택 토글에 사용합니다.
- 빈 영역을 드래그하면 박스 선택합니다.
- 선택 Element를 드래그해 이동하고 Handle로 크기를 바꿉니다. 다중 선택 이동은 하나의 Undo 단계로 기록됩니다.
- 화살표는 1단위, `Shift+화살표`는 10단위 이동입니다.
- Context Menu에서 복제, 삭제, Group, Layer 이동과 겹친 Element 선택을 사용할 수 있습니다.
- `F`는 선택 Element가 보이도록 Canvas Scroll을 맞춥니다.
- Snap과 Smart Guide는 이동 중 정렬 기준을 보여 주지만 모든 Layout 결과를 대신하지 않습니다.
- Assets 탭이나 Project 창에서 Sprite·Font·Material·Component Definition을 Canvas로 드래그할 수 있습니다. 자세한 동작은 [Assets 패널](assets-panel.md)을 참고하세요.

재사용 Component의 Instance는 Canvas에 펼쳐진 모습으로 그려지지만, **선택과 드래그는 Instance 자체만** 대상으로 합니다. Definition에서 온 자식은 화면에 보이되 개별 선택되지 않습니다 — 그 내용을 바꾸려면 Definition을 편집하거나 Instance의 Override를 사용하세요.

Undo/Redo는 Unity 표준 `Ctrl/Command+Z`, `Ctrl/Command+Shift+Z`를 사용합니다. 자세한 목록은 [단축키](../reference/shortcuts.md)에 있습니다.

