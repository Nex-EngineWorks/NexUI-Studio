# Designer Sample 둘러보기

## Import

1. Unity의 `Window > Package Manager`를 엽니다.
2. **NexUI Designer**를 선택합니다.
3. **Samples**에서 **Designer Sample**의 **Import**를 누릅니다.
4. Project 창에서 `Assets/Samples/NexUI Designer/0.1.0/Designer Sample`을 찾습니다.

표시 경로는 Package Manager가 버전 폴더를 포함하므로 설치 버전에 따라 달라질 수 있습니다. `Packages/com.emiteat.nexui.designer/Samples~` 원본을 직접 수정하지 말고 Import된 `Assets/Samples` 복사본을 수정하세요.

## 무엇부터 열어야 하나요?

`Tools > NexUI > Designer`를 연 뒤 Global Toolbar의 **Screen**에 `*.UIToolkit.asset` 또는 `*.UGUI.asset`을 지정합니다. Left Sidebar의 **Metadata**에는 같은 폴더의 `*.Metadata.asset`을 지정합니다. `Rebuild` 후 Canvas와 Layers가 나타나면 연결이 정상입니다.

| Sample | 보여주는 기능 | 먼저 열 파일 | 함께 볼 파일 |
|---|---|---|---|
| Settings | Window 정책, Focus, Apply/Back Command | `Screens/Settings/Settings.UIToolkit.asset` | `Settings.Metadata.asset`, `Settings.UGUI.asset` |
| Inventory | 3×2 Slot, Binding, 선택/빈 상태, Command, Motion | `Screens/Inventory/Inventory.UIToolkit.asset` | `Inventory.Metadata.asset`, `InventoryHover.motionclip.asset`, `InventorySelected.motionclip.asset`, `InventorySampleModel.cs` |
| ConfirmDialog | Modal, 입력 차단, Confirm/Close Command | `Screens/ConfirmDialog/ConfirmDialog.UIToolkit.asset` | `ConfirmDialog.Metadata.asset`, `ConfirmDialog.UGUI.asset` |
| Loading | Loading 화면과 두 Backend 에셋 | `Screens/Loading/Loading.UIToolkit.asset` | `Loading.Metadata.asset`, `Loading.UGUI.asset` |
| HUD | HUD Layer와 화면 배치 | `Screens/HUD/HUD.UIToolkit.asset` | `HUD.Metadata.asset`, `HUD.UGUI.asset` |

각 Screen 폴더의 `UIToolkit/*.uxml`, `*.uss`와 `UGUI/*.prefab`이 실제 Backend Asset입니다. JSON 파일은 Metadata 교환/검토용 Companion 데이터이며 Designer의 기본 편집 에셋은 `.Metadata.asset`입니다.

## Inventory Runtime 예제

`TemplateCommands.RegisterInventory(actionResolver, stateStore)`는 여섯 Slot 선택, Equip, Open/Close Command를 등록하고 `InventorySampleModel`에 초기 데이터를 넣습니다. 이 코드는 Sample 검증용이며 실제 게임 인벤토리 시스템이 아닙니다.

## 추천 확인 순서

1. UI Toolkit Screen과 Metadata를 연결하고 **Validate**를 실행합니다.
2. **Save** 후 Save Report의 Changed, Skipped, Warning, Error를 확인합니다.
3. Screen만 `*.UGUI.asset`로 바꿔 같은 Metadata가 Prefab에 어떻게 반영되는지 비교합니다.
4. Inventory Slot을 선택해 Inspector의 **Connect** 필터에서 Binding/Command를, **Animate** 필터에서 Motion을 확인합니다.
5. Import 복사본에서 Text나 Tint를 바꾸고 Undo/Redo, Save, Reload를 시험합니다.

## 수정해 볼 과제

- Settings의 Apply Button Text를 바꾸고 두 Backend 결과를 비교합니다.
- Inventory의 빈 Slot Preview 값과 선택 Scenario를 만듭니다.
- ConfirmDialog의 Entry/Exit Motion을 Fade Preset으로 바꿉니다.
- HUD를 1920×1080과 다른 Frame Preset에서 확인합니다.

자세한 제작 과정은 [Inventory 튜토리얼](../tutorials/inventory-screen.md), [HUD 튜토리얼](../tutorials/hud-screen.md)을 참고하세요.
