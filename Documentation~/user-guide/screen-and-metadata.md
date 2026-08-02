# Screen과 Metadata

`UIScreenDefinition`은 게임 런타임에서 Screen을 식별하고 Backend Asset, Layer, Focus, Motion 등 실행 계약을 보관합니다. `DesignerMetadataAsset`은 Canvas 제작에 필요한 Element, Rect, Parent, Style, Binding과 편집용 설정을 저장합니다.

| 데이터 | 저장 위치 | Runtime 영향 |
|---|---|---|
| Screen ID, Backend Asset, Layer | `UIScreenDefinition` | 직접 사용 |
| Element ID, Parent ID, Rect, Binding | `DesignerMetadataAsset` | Serializer/Publish를 거쳐 반영 |
| Screen Motion 참조 | Metadata와 Screen Motion 설정 | Entry/Exit Clip 동기화 |
| Preview Value, Preview Image, Preview Options | Metadata | 주로 Preview 전용 |
| 선택, Scroll, 열린 Tab | EditorPrefs | Runtime에 저장하지 않음 |

Element ID는 Hierarchy, UXML `name`, Binding, Focus와 Motion target을 연결하는 Stable ID입니다. 변경할 때는 Inspector를 사용해야 Studio가 Parent, Focus, Variant와 Motion 참조를 함께 갱신합니다.

Backend Asset은 uGUI에서는 Prefab, UI Toolkit에서는 `VisualTreeAsset`입니다. Metadata만 있다고 Runtime UI가 자동으로 생성되는 것은 아닙니다.

