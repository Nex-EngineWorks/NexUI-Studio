# 성능 측정과 예산

Studio의 비용 표시는 제작 단계의 휴리스틱입니다. 배포 판단은 실제 Device의 Unity Profiler, UI Toolkit Debugger와 Frame Debugger로 검증하세요.

## uGUI

- Screen별 Canvas 경계와 Canvas Rebuild 범위를 확인합니다.
- 움직이는 Element 뒤에 불필요한 Sibling이 많으면 Rebuild 비용이 커질 수 있습니다.
- List/Grid Item은 Runtime `UIItemPool<TView>` 같은 재사용 구조를 고려합니다.
- 비사각 Mask보다 가능한 경우 `RectMask2D`를 사용합니다.

## UI Toolkit

- Panel Settings의 Vertex Budget, Atlas와 복잡한 USS Selector를 실제 Frame에서 측정합니다.
- `overflow: hidden`은 사각 Clip에 적합하지만 중첩 Clip과 대량 Text는 별도 확인이 필요합니다.
- Studio의 Batch/Vertex 추정치는 경고 신호이지 실제 Draw Call 수가 아닙니다.

Editor Profiler Marker에는 Canvas/Hierarchy/Timeline Rebuild, Preview Apply, Validation, Publish와 UXML/USS Generation이 포함됩니다. 반복 Repaint가 의심되면 이 Marker부터 확인하세요.

