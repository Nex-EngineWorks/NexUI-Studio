# Screen Flow Editor

Screen Flow Editor는 여러 UI 화면 사이의 흐름을 노드 그래프로 구성하는 도구입니다 (스펙 §30 · §33). 화면이 노드, 화면 간 이동이 타입이 있는 **전환(transition)**입니다.

## 에셋

`DesignerScreenFlowAsset`은 Motion Graph와 같은 **독립 에셋**입니다. 런타임 UIManager 스택과 별개로, 화면을 **ID로만** 참조하므로 런타임 패키지에 의존성을 만들지 않습니다.

- `Assets > Create > NexUI > Studio > Screen Flow` 또는 툴바의 **Flow 생성** 버튼으로 만듭니다.
- 노드: `screenId` + 종류(`Screen`/`Overlay`/`Modal`/`PersistentHUD`/`Subflow`) + 위치 + 나가는 전환 목록.
- 전환: 종류(`Push`/`Pop`/`Replace`/`Overlay`/`Modal`/`Return`) + 대상 노드 + 선택적 `guardKey`(바인딩 bool 가드).

## 사용법

1. `Tools > NexUI > Studio > Advanced > Screen Flow Editor`를 엽니다.
2. **화면 노드 추가**로 노드를 만들고, 노드에서 `Screen`(화면 ID)과 `Kind`를 지정합니다.
3. 노드의 **+ Transition**으로 나가는 전환을 추가하고, 전환의 출력 포트를 다른 노드의 입력 포트로 드래그해 연결합니다. 전환 행에서 종류와 가드 키를 편집합니다.
4. **선택을 시작 노드로**로 진입 노드를 지정합니다. 첫 노드는 자동으로 진입 노드가 됩니다.
5. **자동 정렬**은 진입 노드로부터의 전환 거리에 따라 좌→우로 배치합니다.
6. **검증**은 문제를 나열합니다(아래).

전환은 실제 GraphView 엣지이며, 각 엣지는 출력 포트 **참조**로 특정 전환에 매핑됩니다(종류가 중복될 수 있으므로 이름이 아니라 포트 객체로 식별). 모든 구조 편집은 에셋에 Undo로 기록됩니다.

## 검증 (`ScreenFlowValidator`)

- **오류**: 존재하지 않는 노드를 가리키는 전환, 존재하지 않는 진입 노드, 중복 노드 ID.
- **경고**: 연결되지 않은(dangling) 전환, 진입 노드에서 도달 불가능한 노드, 화면 미지정 노드(Subflow 제외), 진입 노드 미설정.
- `PersistentHUD` 노드는 항상 켜져 있는 것으로 보고 도달 불가능 경고에서 제외합니다.

검증 로직은 순수 클래스라 Unity 없이 EditMode에서 단위 테스트됩니다(`ScreenFlowValidatorTests`).

## 아키텍처

- 데이터는 `Runtime/Metadata/DesignerScreenFlowAsset.cs`(Studio.Runtime, UnityEditor 의존 없음).
- 에디터는 `Editor/Advanced/ScreenFlow/`의 `ScreenFlowView`(GraphView) · `ScreenFlowNodeView` · `ScreenFlowLayout` · `ScreenFlowWindow`로, Motion Graph v2 에디터와 동일한 포트/엣지/Undo 관용구를 따릅니다.

## 아직 하지 않는 것

이 에디터는 흐름 **저작 + 검증** 도구입니다. Deep Link 파라미터 전달(§33.4)과 런타임 실행 연동(전환을 실제 UIManager 호출로 재생)은 이후 단계입니다.

`Screen`은 일반 화면, `Overlay`는 기존 화면 위의 비차단 레이어, `Modal`은 사용자 흐름을 가로막는 대화상자, `PersistentHUD`는 화면 전환과 독립적으로 유지할 HUD, `Subflow`는 다른 흐름 묶음을 뜻합니다. 이 구분은 저작 의미이며 Runtime 동작을 자동 생성하지 않습니다.

`Push`는 새 화면을 Stack에 올리는 의도, `Pop`은 현재 화면 제거, `Replace`는 교체, `Overlay`/`Modal`은 해당 표시 방식, `Return`은 이전 흐름 복귀 의도를 기록합니다. `guardKey` 역시 bool 조건의 이름일 뿐이며, 현재 프로젝트가 `UIStateStore` 값을 읽어 실제 `UIManager` 호출로 변환하는 Adapter를 수동으로 작성해야 합니다.
