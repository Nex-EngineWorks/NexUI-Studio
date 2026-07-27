# Asset Ownership

저장 충돌을 피하려면 누가 어떤 파일을 수정하는지 먼저 구분해야 합니다.

| 데이터 | 주 수정자 | Designer 동작 | Runtime 사용 |
| --- | --- | --- | --- |
| `UIScreenDefinition` | 사용자/설정 도구 | 연결 정보 저장 | 예 |
| `DesignerMetadataAsset` | Designer | Element, Binding, Motion 참조 저장 | Runtime-safe 형식이나 주 용도는 제작 데이터 |
| uGUI Prefab | 사용자 + uGUI Serializer | 일반 Save에서 지원 값 반영 | 예 |
| 수동 UXML/USS | UI Builder/사용자 | 일반 Save는 보존하고 검사만 수행 | 예 |
| `.g.uxml` / `.g.uss` | Generator/Publish | Generated Marker가 있을 때만 교체 | 예 |
| `UIMotionClip` | Motion Clip Editor | Metadata에는 Asset Reference만 저장 | Runtime 연결 시 예 |
| `DesignerScenarioAsset` | Scenario Editor | Preview Mock 저장 | 아니요 |
| `DesignerPublishManifest` | Publish | 마지막 Publish Hash 저장 | 아니요 |
| `DesignerComponentDefinitionAsset` | Component Library/사용자 | 생성과 참조만. Instance 편집은 이 Asset을 바꾸지 않음 | 아니요 (저장 전에 평탄화됨) |

> Component Instance의 Override는 **Instance가 있는 화면의 Metadata**에 저장됩니다. Definition Asset은 Instance 편집으로 변경되지 않으므로, 여러 화면이 같은 Definition을 공유해도 서로의 Override가 섞이지 않습니다.

## 일반 Save

Designer Metadata와 Screen Definition을 저장하고 Backend별 Serializer를 실행합니다. uGUI는 지원되는 Prefab 값을 반영합니다. UI Toolkit은 수동 UXML을 보존하는 companion-save 방식입니다.

## Generate와 Generated Marker

Generator는 Metadata 옆에 Screen 이름을 기준으로 `.g.uxml`, `.g.uss`를 만듭니다. 파일에는 Designer 생성물임을 나타내는 Marker가 들어갑니다. Marker가 없는 기존 파일은 사용자 소유로 간주하여 덮어쓰기를 거부합니다.

생성 파일을 직접 수정하면 다음 Publish에서 `BackendChanged` 또는 `Conflict`가 됩니다. 영구 수동 변경은 수동 UXML/USS나 별도 USS로 옮기세요.

## Sync Base와 Publish Manifest

`DesignerPublishManifest`는 Screen별 마지막 UXML/USS Hash를 저장합니다. 현재 Designer 출력, 디스크 파일, 마지막 Publish Base를 비교하여 `New`, `InSync`, `DesignerChanged`, `BackendChanged`, `Conflict`를 판정합니다. 자동 Merge는 하지 않습니다.

## Git에 함께 커밋할 파일

Screen Definition, Metadata, Backend Prefab 또는 수동 UXML/USS, 사용 중인 Motion Asset, 필요한 Generated 파일과 Publish Manifest를 같은 변경 단위로 검토하세요. Scenario는 팀이 공유할 Preview 기준일 때 커밋합니다. Unity의 대응 `.meta` 파일도 포함합니다.

## 충돌 처리

- **Designer 사용**: 생성 결과를 기준으로 파일을 다시 씁니다.
- **Backend 사용**: 디스크 변경을 보존합니다. Metadata로 자동 역변환된다고 가정하지 마세요.
- **Conflict**: Diff를 보고 어느 쪽을 유지할지 명시적으로 선택합니다.

[Sync와 Publish](../advanced/sync-and-publish.md)에서 권장 작업 순서를 확인하세요.

