# Runtime Debugging

NexUI Core에는 Play Mode 상태를 수집하는 `NexUIDebug` API, Runtime Overlay와 `NexUIDebugWindow`가 구현되어 있습니다. Snapshot은 열린 Screen, Back/Modal Stack, Focus Element, State/Action Key, 최근 Command, Query Cache와 Theme ID를 보여 줍니다.

`Tools > NexUI > 유틸리티`에서 **Runtime Debug Snapshot**을 선택합니다. `NexUIDebugWindow` 자체에는 직접 `MenuItem`이 없고 Utilities가 선택적으로 연결합니다. Play Mode가 아니면 Snapshot을 캡처하지 않습니다.

이 도구는 Studio Preview Debugger가 아니며 Element Picking, Live Property 수정과 Remote Player 연결은 지원하지 않습니다. 그런 문제는 Unity Console, UI Toolkit Debugger, Frame Debugger와 Profiler를 함께 사용해 주세요.
