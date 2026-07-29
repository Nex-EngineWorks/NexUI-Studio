# Hierarchy와 Layout

Hierarchy는 중첩 객체가 아니라 Metadata의 평면 Element 목록과 `parentId`, `siblingIndex`로 저장됩니다. Layers에서 드래그하거나 Reparent 명령을 사용하면 이 참조가 바뀝니다.

- **Reparent:** 부모를 바꾸되 Canvas 위치를 유지합니다.
  Layers에서 행을 드래그해 **다른 행의 가운데에 놓으면 그 자식으로**, **위/아래 가장자리에 놓으면 형제 순서**가 바뀝니다. 가운데 판정 영역이 행 높이의 절반이라 자식으로 넣기가 기본 동작입니다. 접혀 있는 Container에 넣으면 자동으로 펼쳐집니다.
  Canvas에서 Element를 Container 위로 끌어다 놓아도 같은 결과가 됩니다([Canvas 편집](canvas-editing.md) 참고).
- **Layer order:** 같은 부모의 `siblingIndex`를 변경합니다.
- **Group/Ungroup:** Container를 만들거나 해제하고 자식 관계를 갱신합니다.
- **Align/Distribute:** 기본은 현재 선택의 Bounding Box를 기준으로 하며, Layers/Canvas 메뉴에서 Key Object를 지정하면 해당 Element를 기준으로 정렬합니다.
- **Auto Layout:** Row/Column/Grid 방향, 간격, Padding과 자식 크기 모드를 Metadata에 저장하고 Preview 및 두 Backend에 적용합니다.
- **Constraints:** 부모 크기 변경 시 가로·세로 Start/Center/End/Scale 관계를 기록합니다.

> [!NOTE]
> Auto Layout은 두 Backend로 저장됩니다. Constraints의 복합 관계는 여전히 Preview 중심이므로 Save Report 또는 Validation에서 확인해야 합니다.
