# 알려진 제한

| 영역 | 증상/원인 | 현재 우회 방법 |
|---|---|---|
| 공통 | 일부 고급 기능은 Metadata/Preview까지만 지원합니다. | Save Report와 Play Mode를 확인합니다. |
| 컴포넌트 | `ChoiceList`, `Modal`, `Popover`, `RadialFill`, `Skeleton`, `Slot`, `Spinner`, `Toast`, `Tooltip`의 런타임 동작은 **uGUI에만** 있습니다(`NX.ChoiceList`, `NX.Modal` 등). UI Toolkit에서는 여전히 모양까지만 나옵니다. | UI Toolkit 화면에서 이 동작이 필요하면 프로젝트 코드로 제어합니다. |
| 컴포넌트 | 위 컴포넌트들은 자식 참조를 **자동으로 연결하지 않습니다**. `Toast`의 Label, `Skeleton`의 Placeholder/Content, `ChoiceList`의 Option Template, `Modal`의 Backdrop은 비워두면 그 부분만 동작하지 않습니다. | Inspector에서 해당 필드를 지정합니다. 지정 전에는 나머지 기능은 정상 동작합니다. |
| 컴포넌트 | `Tooltip` 팔레트 프리셋은 `NX.RoundedRect`와 `UGUI.TextMeshProUGUI`를 함께 올리는데, 카탈로그상 이 둘은 서로 conflict로 선언되어 있습니다(이전부터 있던 불일치). | Validation 경고가 나오면 둘 중 하나를 제거하고 텍스트를 자식 Element로 둡니다. |
| Canvas | Key Object를 지정하지 않은 다중 정렬은 선택 Bounding Box를 기준으로 합니다. | Layers/Canvas 메뉴에서 기준 Element를 Key Object로 지정합니다. |
| Layout | Auto Layout은 Row/Column/Grid를 저장하지만 복잡한 Constraints 조합은 Preview 중심입니다. | Backend별 Save Report와 실제 Layout을 확인합니다. |
| Binding | 프로젝트 Runtime Key 존재 여부를 모두 확정할 수 없습니다. | `UIStateStore`와 `UIActionResolver` 등록을 테스트합니다. |
| uGUI | Element의 Component Stack에 없는 Component는 저장이 손대지 않습니다. Prefab에서 직접 붙인 것은 값도 쓰지 않습니다. | Save Report의 **Overwrite scope**가 Studio 소유 범위와 수동 범위를 개수·이름으로 보고합니다. 값까지 저장하려면 Studio에서 Add Component로 Stack에 올립니다. |
| UI Toolkit | Generated Marker 없는 사용자 UXML/USS는 일반 Save가 다시 쓰지 않습니다. | UI Builder로 관리하거나 별도 `.g.uxml/.g.uss` Generation을 사용합니다. |
| Motion Clip | Position capability 일부가 같은 Runtime 값으로 처리됩니다. | 대상 Backend에서 실제 재생을 확인합니다. |
| Motion Graph | Legacy와 v2가 서로 다른 모델입니다. | 에셋 Type과 실행기를 혼용하지 않습니다. |
| Figma | 첫 Frame Import는 지원하지만 Component Variant, Effect, Image 다운로드와 양방향 Sync는 지원하지 않습니다. | 가져온 Metadata를 Validation하고 복잡한 Style은 Backend에서 보완합니다. |
| Figma | REST API 경로의 요청 한도는 **파일 소유자의 Figma 요금제**에 부과됩니다. 무료 플랜 파일은 월 요청 수가 매우 적습니다. | Dev Mode의 **Copy as JSON**을 붙여넣는 경로를 사용합니다. 토큰도 네트워크도 필요 없습니다. |
| Figma | 여러 노드를 한 번에 복사해도 **첫 번째 노드만** 가져옵니다. | 가져오기 후 상태 메시지가 몇 개가 있었는지 알려줍니다. 나머지는 따로 복사해 가져옵니다. |
| 재사용 Component | Instance 리사이즈는 각 요소의 Anchor Preset을 제약으로 해석합니다. Anchor가 TopLeft(기본)인 요소는 크기·위치가 그대로입니다. | 늘어나야 하는 요소는 Definition에서 Anchor를 Stretch로, 가장자리를 따라가야 하는 요소는 해당 모서리로 지정합니다. Auto Layout이 켜진 요소의 자식은 Layout이 배치합니다. |
| 재사용 Component | Definition에서 요소를 **삭제**하면 그 요소를 가리키던 Override는 재매핑할 대상이 없습니다(이름 변경은 자동 추적됩니다). | `Update From Definition`이 해당 Override를 보고하고, 값이 복구 불가함을 알린 뒤 삭제 여부를 묻습니다. |
| 재사용 Component | Variant Rule의 해상도·입력모드 조건은 **저작 시점**에 평가됩니다. 저장 결과는 그때 캔버스 해상도로 확정된 한 가지입니다. | 런타임에 해상도에 따라 바뀌어야 하면 화면의 Responsive Rule을 사용합니다. Definition은 화면 규칙을 소유하지 않습니다. |
| 재사용 Component | Theme Class·Token Override는 텍스트 형식(`a b c`, `key=value;key=value`)으로 저장됩니다. 값에 구분자를 넣을 수 없습니다. | 구분자가 들어간 값은 저작 시점에 거부됩니다. |
| Assets 패널 | Move는 되돌릴 수 없습니다(`AssetDatabase.MoveAsset`에 Undo가 없음). Thumbnail은 비동기로 채워집니다. | 이동 전 확인 대화상자에서 대상과 개수를 확인합니다. |
| 저장 | 읽기 전용 Package 경로에는 생성 파일을 쓸 수 없습니다. | Import된 Sample 또는 `Assets/` 경로를 사용합니다. |
| Preview | 실제 입력·폰트·해상도 결과와 다를 수 있습니다. | Play Mode와 Player Build로 재검증합니다. |
# 생산성 기능 제한사항

- Constraints의 복합 관계와 비선형 배치는 Backend별 수동 검토가 필요합니다.
- UI Toolkit 사용자 UXML은 Auto Fix가 직접 수정하지 않습니다.
- Command 생성과 Motion Track 삭제는 자동 수정하지 않습니다.
- 전환 미리보기는 실제 Runtime Command와 화면 Stack을 실행하지 않습니다.
