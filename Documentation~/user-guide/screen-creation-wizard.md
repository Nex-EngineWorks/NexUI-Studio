# 화면 생성 마법사

반복해서 만들던 Screen Definition, Studio Metadata, uGUI Prefab 또는 UXML을 한 번에 연결합니다.

1. Global Toolbar의 `+ 새 화면` 또는 `Tools > NexUI > Studio > New Screen`을 엽니다.
2. 화면 이름, 고유 Screen ID, Backend, 템플릿과 저장 폴더를 지정합니다.
3. 생성 요약과 경고를 확인하고 `생성하고 Studio에서 열기`를 누릅니다.

Full Screen, Popup, Modal, HUD, Overlay 템플릿을 지원합니다. 기본 전환을 켜면 기존 `UIMotionClip` Open/Close 에셋을 생성해 화면에 연결합니다. 생성 도중 실패하면 이번 작업에서 만든 에셋을 제거하며 기존 파일은 덮어쓰지 않습니다. 생성 작업은 Undo에 등록되지만, 버전 관리 중에는 생성 직후 경로도 함께 확인하는 것이 안전합니다.

uGUI는 RectTransform/CanvasGroup Root Prefab, UI Toolkit은 Generated Marker가 있는 UXML을 만듭니다. 복잡한 샘플 계층은 자동 생성하지 않습니다.
