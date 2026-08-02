# UI Toolkit Backend

UI Toolkit Screen의 Backend Asset은 `VisualTreeAsset`입니다. Metadata `elementId`는 UXML의 `name`과 연결됩니다.

일반 Save에서 `UIToolkitAssetSerializer`는 Metadata를 저장하고 UXML 이름을 검증합니다. Backend UXML에 `NEXUI:GENERATED` Marker가 있으면 UXML과 같은 이름의 USS를 메모리에서 함께 생성한 뒤 트랜잭션으로 교체합니다. Marker가 없는 사용자 작성 파일은 보존합니다. `Sync Metadata From Backend`는 이름이 있는 VisualElement를 Metadata에 추가할 수 있습니다.

별도 **UI Toolkit Generation** 도구는 Metadata에서 `.g.uxml`과 `.g.uss`를 생성합니다. 생성 결과는 사용자 작성 파일과 분리되며 Generated Marker 없는 파일을 덮어쓰지 않습니다. Dry Run, Diff 확인, Copy, 폴더 열기와 대상 Import를 지원합니다.

UI Builder는 세밀한 UXML/USS 제작에, Studio는 Screen 계약·Metadata·Binding·Motion과 Validation에 적합합니다. VisualElement Type, 복잡한 USS Selector, Custom Control과 Runtime Data Source는 수동 구현이 필요할 수 있습니다.

> [!WARNING]
> Toolbar의 일반 Save는 연결된 Generated 파일만 갱신합니다. 별도 Generation 도구는 새 경로, Dry Run과 Diff를 다루는 작업입니다.

전체 항목 비교는 [Backend 지원 범위](../reference/backend-support-matrix.md), 파일 소유권은 [Asset Ownership](../reference/asset-ownership.md)을 참고하세요.
