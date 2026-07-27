# 직렬화와 저장 책임

`NexUIDesignerContext.Save`는 Motion Entry/Exit를 `UIScreenDefinition.motion`에 동기화하고 `DesignerSerializerRegistry`에서 Backend Serializer를 선택합니다.

- Metadata 변경은 Metadata Asset에 Undo/Dirty를 기록합니다.
- Screen 변경은 `UIScreenDefinition`에 별도로 기록합니다.
- 화면에 Component Instance가 있으면 `DesignerComponentExpander`가 평탄화한 **메모리 전용 사본**을 Serializer에 넘깁니다. 사본은 저장되지 않으므로 `Save`가 원본 Metadata를 따로 저장합니다.
- uGUI Serializer는 Prefab Contents를 열어 Designer 소유 데이터를 적용하며, 대상 Object는 **Stable ID 우선**(이름은 fallback)으로 찾습니다.
- UI Toolkit Serializer는 Metadata와 UXML 이름을 검증합니다. 대상 UXML에 Generated Marker가 있을 때만 `.uxml`/`.uss`를 다시 쓰고, Marker가 없는 사용자 파일은 건드리지 않습니다.
- `UIToolkitCodeGenerator`와 `GeneratedAssetWriter`는 별도 생성 경로입니다.
- Companion JSON은 Metadata 교환용이며 권위 데이터는 Unity Asset입니다. JSON은 **authored** Metadata를 기록합니다(전개 결과가 아님).

`DesignerSaveReport`의 Changed, Skipped, Warnings, Errors를 정확히 채워 UI가 저장 성공을 과장하지 않게 해야 합니다. 파일 writer는 임시 파일, 검증, backup/rollback과 대상 `ImportAsset`을 사용하고 전체 Refresh를 피합니다. Preview Value/Image/Options와 EditorPrefs UI 상태를 Runtime 데이터로 오인하지 마세요.

