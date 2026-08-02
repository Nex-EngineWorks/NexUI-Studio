# Inventory Vertical Slice

3×2 슬롯으로 Data → Binding → Interaction → Motion → Command → Backend → Runtime 경로를 확인하는 샘플입니다.

## 포함 내용

- UI Toolkit UXML/USS와 uGUI Prefab Screen Definition
- 6개 Slot과 빈 슬롯 상태
- 이름(`inventory.slotN.name`)과 수량(`inventory.slotN.count`) Binding
- 선택/빈 슬롯 class 상태
- 슬롯 클릭 Command (`inventory.select.slotN`)
- Equip Command (`inventory.equip`)과 Close Command
- Hover/Selected Motion Clip 참조 예시
- `InventorySampleModel`의 최소 상태 데이터

## 실행

1. Package Manager에서 Studio Sample을 Import합니다.
2. `Tools > NexUI > Studio`를 엽니다.
3. `Inventory.UIToolkit.asset` 또는 `Inventory.UGUI.asset`을 Screen으로 선택합니다.
4. `Inventory.Metadata.asset`을 Metadata로 선택합니다.
5. Validate 후 Save하고 Unity를 Reload해 연결이 유지되는지 확인합니다.
6. Runtime 구성에서 `TemplateCommands.RegisterInventory(actionResolver, stateStore)`를 호출합니다.
7. 화면을 열고 슬롯 선택, 빈 슬롯, Equip, Close를 확인합니다.

`InventorySampleModel`은 샘플 검증용이며 실제 게임 인벤토리 시스템이 아닙니다. Runtime Surface가 열린 뒤 `DesignerMotionTriggerRuntime`을 Metadata의 `screenMotion`과 연결하면 Click/Pointer/Focus Trigger가 두 Backend의 공통 Capability를 통해 실행됩니다. 화면·Command Lifecycle은 `Enter`/`Exit`/`Notify`를 호출하십시오.
