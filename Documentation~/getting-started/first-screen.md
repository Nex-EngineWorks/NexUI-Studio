# 첫 번째 Screen 만들기

## 이 튜토리얼에서 만드는 것

`MainMenu` 화면에 Background, 게임 제목과 세 Button을 배치합니다. Button은 Vertical Auto Layout Container 아래에 두고 `menu.start`, `menu.settings`, `menu.quit` Command Key를 저장합니다.

<!-- TODO Screenshot:
화면: 완성된 MainMenu Studio Canvas
표시할 항목: 전체 Background, Title, 세로 정렬된 Start/Settings/Quit Button, Layers 계층
권장 해상도: 1600×900
파일 제안: images/tutorial-main-menu-result.png
-->

## 필요한 사전 준비

- Unity 2022.3 LTS 이상
- NexUI Core와 NexUI Studio 0.1.0
- `Assets/UI/Screens`처럼 Package 밖의 쓰기 가능한 폴더
- [인터페이스 둘러보기](interface-tour.md)를 한 번 읽은 상태

## 사용할 에셋 구조

UI Toolkit은 `MainMenu.asset`, `MainMenu.Metadata.asset`, `MainMenu.uxml`, `MainMenu.uss`를 사용합니다. uGUI는 UXML/USS 대신 `MainMenu.prefab`을 사용합니다. Motion을 켜면 `Motions` 폴더에 Entry/Exit Clip도 생성됩니다.

## 1. UIScreenDefinition 생성

Global Toolbar의 **+ 새 화면**을 누르거나 `Tools > NexUI > Studio > New Screen`을 엽니다. 화면 이름은 `Main Menu`, Screen ID는 `MainMenu`, 템플릿은 `FullScreen`, 저장 폴더는 `Assets/UI/Screens/MainMenu`로 입력합니다.

Screen ID는 Runtime이 화면을 찾는 계약입니다. 문자 또는 `_`로 시작하고 문자, 숫자, `_`, `-`만 사용하세요. 중복 ID나 기존 파일이 있으면 생성 버튼이 비활성화됩니다.

## 2. Backend 선택

Backend에서 `UIToolkit` 또는 `UGUI`를 선택합니다. 처음 따라 한다면 생성 결과를 텍스트로 확인하기 쉬운 `UIToolkit`을 권장합니다.

- UI Toolkit 정상 결과: 생성 요약에 `.uxml/.uss` 대상이 표시됩니다.
- uGUI 정상 결과: `.prefab` 대상이 표시됩니다.

## 3. Backend Asset 준비

**Studio Metadata 생성**, **Root 요소 생성**을 켭니다. **기본 열기/닫기 전환 생성**은 선택 사항이며, 처음에는 꺼도 됩니다. **생성하고 Studio에서 열기**를 누릅니다.

정상적으로 완료되면 Screen이 Global Toolbar에, Metadata가 Left Sidebar에 자동 연결되고 Backend 배지가 선택한 Backend를 표시합니다. 실패하면 저장 폴더가 `Assets/` 아래인지, 같은 이름의 파일과 Screen ID가 이미 있는지 확인하세요.

## 4. DesignerMetadataAsset 확인

Project 창에서 `MainMenu.Metadata.asset`을 확인합니다. Metadata는 Element 위치, 부모·형제 순서, Binding과 Motion 참조를 저장합니다. Runtime Screen 정의와 역할이 다릅니다.

## 5. Screen과 Metadata 연결

자동 연결되지 않았다면 Global Toolbar **Screen**에 `MainMenu.asset`, Left Sidebar **Metadata**에 `MainMenu.Metadata.asset`을 지정합니다. 두 에셋의 Screen ID가 `MainMenu`로 같아야 합니다.

정상 결과는 Backend 배지와 `Ready` 또는 Validation 상태가 나타나는 것입니다. Layers가 `Select a Metadata asset`이라고 표시되면 Metadata 연결을 다시 확인하세요.

## 6. Preview Rebuild

Canvas Toolbar의 **Rebuild**를 누릅니다. Root Element가 Canvas와 Layers에 표시되어야 합니다. Preview가 비어 있다면 Screen의 Backend Asset, Metadata, Console 컴파일 오류를 순서대로 확인합니다.

## 7. Background 추가

Left Sidebar의 **Components**에서 `Panel`을 추가합니다. Canvas에서 화면 전체로 늘리고 Right Inspector에서 Element ID를 `background`, Display Name을 `Background`로 바꿉니다. Design의 Layout/Style에서 Rect와 Tint를 조정합니다.

정상 결과는 Layers에 `background`가 보이고 Canvas 전체에 Tint가 표시되는 것입니다. UI Toolkit과 uGUI 모두 Rect/Tint를 저장하지만 Shape의 정확한 시각 결과는 사용자 USS/Sprite 구성에 따라 다릅니다.

## 8. 제목 추가

Components에서 `Label`을 추가합니다. ID는 `title`, Text는 게임 제목, Font Size는 예를 들어 `48`로 입력합니다. Background 아래에 두려면 Layers에서 `title`을 `background` 아래로 Reparent합니다.

정상 결과는 Canvas와 Layers 양쪽에 제목이 보이는 것입니다. 실제 Font와 줄바꿈은 UI Toolkit Panel Settings 또는 uGUI TMP 설정에 따라 달라지므로 Play Mode에서 다시 확인합니다.

## 9. Button Container 추가

Components에서 `Container`를 추가하고 ID를 `menuButtons`로 바꿉니다. 이 Element는 Button의 부모이자 Layout 기준입니다. Canvas 중앙에 약 `320×240` 크기로 배치합니다.

## 10. Auto Layout 설정

`menuButtons`를 선택하고 Inspector의 **Build > Auto Layout**에서 Enabled를 켭니다. Direction은 `Column`, Spacing은 `16`, Padding Left/Right/Top/Bottom은 필요에 따라 `16`으로 설정합니다.

정상 결과는 자식이 생긴 뒤 위에서 아래로 흐르는 것입니다. uGUI Save는 `VerticalLayoutGroup`, UI Toolkit Save/Generation은 Column Flex와 Padding/Spacing으로 변환합니다.

## 11. Button 추가

Components에서 Button 세 개를 추가합니다. 다음 값을 사용합니다.

| Element ID | Text | Command Key |
|---|---|---|
| `startButton` | 시작 | `menu.start` |
| `settingsButton` | 설정 | `menu.settings` |
| `quitButton` | 종료 | `menu.quit` |

## 12. 부모·자식 관계 구성

Layers에서 세 Button을 `menuButtons` 아래로 옮깁니다. 형제 순서는 Start, Settings, Quit 순으로 둡니다. Auto Layout 안에서는 Canvas의 절대 위치보다 부모와 형제 순서가 중요합니다.

정상 결과는 Layers에서 세 Button이 들여쓰기되고 Canvas에서 일정 간격으로 세로 정렬되는 것입니다. 움직이지 않으면 Auto Layout Enabled와 Parent ID를 확인하세요.

## 13. Command Key 연결

Button을 하나씩 선택하고 Inspector의 **Connect > Binding**에서 Command Key에 위 표의 값을 입력하거나 Command Picker에서 선택합니다. Studio는 Key 문자열을 저장합니다. 게임 동작은 Runtime의 `UIActionResolver` 등록이 담당합니다.

컴파일 가능한 Runtime 예제는 [Binding](../user-guide/binding.md)에 있습니다. 등록하지 않은 Key는 Play Mode에서 동작하지 않습니다.

## 14. Preview State 확인

Canvas Toolbar의 **State**를 Normal, Hover, Pressed, Disabled로 바꿉니다. 또는 Canvas를 Interactive로 바꾸고 Button을 클릭해 Bottom Drawer의 **Preview** Log를 확인합니다.

이 동작은 실제 Command를 실행하지 않습니다. State 스타일이 보이지 않더라도 Command 등록 여부와는 별개입니다.

## 15. Validation 실행

Global Toolbar의 **Validate**를 누르고 Bottom Drawer의 **Validation** 탭을 엽니다. 중복 ID, Parent 오류, Button Command 누락과 Touch Target 경고를 해결합니다. Issue를 클릭하면 가능한 경우 Element나 Asset으로 이동합니다.

Error가 0이면 다음 단계로 진행합니다. 규칙 전체는 [Validation Catalog](../reference/validation-catalog.md)를 참고하세요.

## 16. Save 실행

Global Toolbar의 **Save** 또는 `Ctrl+S`를 누릅니다. Save Report에서 Changed, Skipped, Warning, Error를 확인합니다.

- uGUI: Prefab의 Rect, 부모, 순서와 지원 Component를 갱신합니다.
- UI Toolkit: Generated Marker가 있는 연결 UXML/USS만 안전하게 갱신합니다. 사용자 작성 파일은 보존됩니다.

Skipped가 있다고 무조건 실패는 아닙니다. 어떤 값이 제외됐는지 [Backend 지원 범위](../reference/backend-support-matrix.md)에서 확인하세요.

## 17. Play Mode 확인

Runtime에서 `MainMenu` Screen을 등록하고 `UIActionResolver`에 세 Command Key를 등록한 뒤 화면을 엽니다. Button 클릭이 등록한 Action으로 연결되고 Text/Layout이 실제 Backend에서 맞게 보이는지 확인합니다.

Studio Preview와 Play Mode가 다르면 Canvas Scaler/Panel Settings, Font, Runtime Binding 타입과 Backend Asset을 확인하세요.

## 완성 구조

```text
background
├─ title
└─ menuButtons (Column Auto Layout)
   ├─ startButton
   ├─ settingsButton
   └─ quitButton
```

## 자주 발생하는 문제

- Canvas가 비어 있음: Screen, Metadata, Backend Asset 연결 후 Rebuild합니다.
- ID mismatch: Screen과 Metadata의 `MainMenu` ID를 맞춥니다.
- Button이 정렬되지 않음: `menuButtons` Parent와 Auto Layout Enabled를 확인합니다.
- Command가 실행되지 않음: Studio Preview가 아니라 Play Mode에서 `UIActionResolver.Contains(key)`와 등록 코드를 확인합니다.
- UXML이 바뀌지 않음: Generated Marker가 없는 사용자 파일인지 확인합니다.

## 다음에 해볼 것

[Scenario](../user-guide/preview-and-scenarios.md)로 긴 제목과 Disabled 상태를 확인하고, [Motion 레시피](../motion/recipes.md)로 Button 순차 등장이나 화면 Fade를 추가해 보세요.
