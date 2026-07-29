# Core 20 support matrix

Descriptor와 Canvas 표현이 있다는 사실을 실제 Backend/Runtime 지원과 구분한다. `Partial`은 Metadata가 보존되더라도 일부 속성이나 상호작용이 Save Report에서 제한될 수 있다는 뜻이다.

| Component | Palette/Preview/Inspector | Undo/Metadata | uGUI | UI Toolkit | Runtime/Interaction | Motion/Validation | Tests |
|---|---|---|---|---|---|---|---|
| Panel | Beta | Beta | Partial | Partial | Partial | Beta | Beta |
| Label | Beta | Beta | Partial | Partial | Partial / Unsupported | Beta | Beta |
| Image | Beta | Beta | Partial | Partial | Partial / Unsupported | Beta | Beta |
| Button | Beta | Beta | Partial | Partial | Partial | Beta | Beta |
| TextField | Beta | Beta | Partial | Partial | Partial | Beta | Partial |
| Toggle | Beta | Beta | Partial | Partial | Partial | Beta | Beta |
| Slider | Beta | Beta | Partial | Partial | Partial | Beta | Beta |
| ProgressBar | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Beta |
| ScrollView | Beta | Beta | Partial | Partial | Partial | Beta | Beta |
| List | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Grid | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Dropdown | Beta | Beta | Partial | Partial | Partial | Beta | Partial |
| Tabs | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Modal | Beta | Beta | Partial | Partial | Partial | Beta | Partial |
| Tooltip | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Toast | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Inventory Slot | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Item Tooltip | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Health Bar | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |
| Settings Row | Beta | Beta | Partial | Partial | Partial / Preview-only | Beta | Partial |

`Full` 승격에는 두 Backend의 저장 결과와 Player 상호작용을 각각 통합 검증해야 한다. UI Toolkit 사용자 작성 UXML은 구조를 덮어쓰지 않으며 Metadata-only로 취급한다.
