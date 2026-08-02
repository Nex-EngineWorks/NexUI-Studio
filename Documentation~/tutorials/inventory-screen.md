# Inventory Screen 튜토리얼

## 완성 결과

3×2 Slot, Item Name/Count Binding, 빈 Slot, 선택 상태, Hover/Selected Motion과 Equip Command를 가진 화면을 구성합니다. 완성본은 Studio Sample의 `Screens/Inventory/`에 있습니다.

## 사전 준비

[Sample 둘러보기](../getting-started/sample-tour.md)에 따라 Sample을 Import하고 `Inventory.UIToolkit.asset` 또는 `Inventory.UGUI.asset`을 엽니다. 새로 만들 경우 Screen, Metadata와 Backend Asset을 먼저 준비합니다.

## 최종 Hierarchy

```text
InventoryRoot
├─ Title
├─ SlotGrid
│  ├─ slot1 … slot6
└─ DetailPanel
   ├─ ItemName
   ├─ ItemCount
   └─ EquipButton
```

## 단계별 제작

1. Components에서 Container를 추가해 `SlotGrid`로 이름을 바꿉니다. Inspector의 Auto Layout을 켜고 Grid, Columns 3, Cell Size와 Spacing을 지정합니다. Canvas에 3열이 보이면 정상입니다.
2. Slot 역할의 Button/Card를 여섯 개 추가하고 Layers에서 `SlotGrid` 아래로 이동합니다. ID를 `slot1`~`slot6`으로 고유하게 지정합니다.
3. `DetailPanel`과 Name/Count Label, Equip Button을 추가합니다. Layers에서 부모 관계가 위 구조와 같은지 확인합니다.

## Binding과 Command

각 Slot 또는 Detail Label에 Sample Metadata가 사용하는 `inventory.slotN.name`, `inventory.slotN.count`, 빈 상태 Key를 연결합니다. Slot Command는 `inventory.select.slotN`, Equip Button은 `inventory.equip`을 사용합니다. 임의 Key로 바꾸면 Sample Runtime 모델과 함께 바꿔야 합니다.

Studio는 Key만 저장합니다. Sample의 Runtime 코드가 `UIStateStore`와 `UIActionResolver`에 값과 Action을 등록해야 Play Mode에서 동작합니다. [Binding](../user-guide/binding.md)을 참고하세요.

## Motion과 Scenario

`InventoryHover.motionclip.asset`과 `InventorySelected.motionclip.asset`을 Motion Inspector에서 대상 Slot의 Hover/Selected Trigger에 연결합니다. Normal/Hover/Selected State를 전환하여 시각 상태를 확인합니다. Scenario를 만든다면 Empty, Normal, ItemSelected처럼 현재 Binding으로 표현 가능한 Mock만 저장합니다.

## Validation과 Save

Validate하여 중복 ID, 잘못된 Parent, Missing Motion Target을 해결합니다. Save 후 Save Report에서 Changed/Skipped를 확인합니다. uGUI는 Prefab 지원 값이 반영되고, UI Toolkit 일반 Save는 수동 UXML을 보존합니다. 생성물이 필요하면 Generate/Publish를 별도로 사용합니다.

## Play Mode 확인

Sample Scene/호스트에서 Inventory를 열고 Slot 선택, 이름·수량 갱신, Equip Command와 닫기를 확인합니다. Studio Interactive Preview는 Command를 실행하지 않습니다.

## 현재 제한

Drag & Drop, Tooltip 자동 연결과 Collection Binding 기반 Slot 자동 증식은 이 튜토리얼의 완성 범위가 아닙니다. Slot 여섯 개는 명시적으로 저작합니다. Backend별 Empty/Selected 외형은 Prefab 또는 USS가 제공해야 합니다.

## 확장 과제

긴 아이템 이름, 수량 0, 모든 Slot이 찬 Scenario를 만들고 두 Backend에서 Layout을 비교해 보세요.

