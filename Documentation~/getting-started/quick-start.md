# 10분 빠른 시작

**목표:** Sample 화면을 열거나 새 Button 하나를 추가해 저장합니다.

## 0. 설치 확인

`Tools > NexUI > Setup Doctor`에서 Runtime, UniTask, Project Assets, 현재 Scene Backend와 출력 경로를 확인합니다. 누락된 기본 에셋은 `Project Setup` 버튼으로 생성할 수 있습니다.

Studio 상단 상태는 `Backend | Screen ID | Loaded/Unsaved/Saved | Valid/Warnings/Errors` 순서로 표시됩니다. `Saved`는 지원되는 Backend 범위까지 기록했다는 뜻이며 Runtime Key, 폰트, 입력과 실제 Layout까지 검증했다는 뜻은 아닙니다. 최종 확인은 Play Mode와 Player Build에서 수행합니다.

## 가장 빠른 경로: Sample 열기

1. Package Manager에서 **Studio Sample**을 Import합니다.
2. `Tools > NexUI > Studio`를 엽니다. Global Toolbar, Left Sidebar, Canvas와 Right Inspector가 보이면 정상입니다.
3. Global Toolbar의 **Screen**에 `Settings.UIToolkit.asset`을 지정합니다.
4. Left Sidebar의 **Metadata**에 `Settings.Metadata.asset`을 지정합니다.
5. Canvas Toolbar에서 **Rebuild**를 누릅니다. Layers와 Canvas에 Settings Element가 보이면 정상입니다.
6. **Validate**를 누릅니다. Error가 없거나 알려진 Warning만 표시되는지 확인합니다.

정확한 Import 경로는 [Sample 둘러보기](sample-tour.md)를 참고하세요.

## 새 화면 경로

1. Global Toolbar의 **+ 새 화면**을 누릅니다.
2. 화면 이름 `Quick Start`, Screen ID `QuickStart`, Backend와 `Assets/UI/Screens` 폴더를 지정합니다.
3. **Studio Metadata 생성**, **Root 요소 생성**을 켜고 **생성하고 Studio에서 열기**를 누릅니다.
4. UI Toolkit은 `.asset`, `.Metadata.asset`, `.uxml`, `.uss`가 생성됩니다. uGUI는 `.asset`, `.Metadata.asset`, `.prefab`이 생성됩니다.
5. Left Sidebar의 **Components**에서 Button을 누릅니다. 새 Element가 Layers와 Canvas에 함께 나타나야 합니다.
6. Right Inspector의 Workflow를 **Connect**로 바꾸고 **Binding** Section에서 Command Key를 입력합니다. 이 단계는 문자열 Key를 저장할 뿐 실제 Command를 만들지 않습니다.
7. **Validate**, **Save** 순서로 실행합니다.

Save Report에서 `Changed`는 기록된 에셋, `Skipped`는 의도적으로 쓰지 않은 항목, `Warning/Error`는 확인이 필요한 항목입니다. UI Toolkit의 Generated Marker가 없는 사용자 UXML은 보호를 위해 구조를 다시 쓰지 않습니다.

Screen은 Runtime 식별·정책과 Backend Asset을 보유합니다. Metadata는 Element 위치, 부모, Binding과 Motion 제작 정보를 보유합니다. Backend Asset은 uGUI Prefab 또는 UI Toolkit UXML입니다.

다음은 [인터페이스 둘러보기](interface-tour.md), [첫 번째 Screen 만들기](first-screen.md), [Backend 지원 범위](../reference/backend-support-matrix.md) 순서로 읽으세요.
