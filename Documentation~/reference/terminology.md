# 용어

| 한국어 | UI/API 표기 | 의미 |
|---|---|---|
| 화면 | Screen | 독립적으로 열고 닫는 UI 단위 |
| 요소 | Element | Screen을 구성하며 Stable ID를 가진 항목 |
| 컴포넌트 | Component | Panel, Button 등 Element의 역할 |
| 계층 | Hierarchy/Layers | Parent와 Sibling 관계 |
| 메타데이터 | Metadata | Studio 제작 정보를 저장하는 에셋 |
| 백엔드 | Backend | uGUI 또는 UI Toolkit 구현 |
| 바인딩 | Binding | 상태 Key와 표시 속성의 연결 |
| 명령 | Command | 사용자 동작이 호출하는 Action Key |
| 미리보기 | Preview | Editor 제작 결과 확인 Surface |
| 검증 | Validation | 잘못된 참조와 지원 범위 검사 |
| 저장 | Save | Metadata와 Backend Serializer 실행 |
| 모션 클립 | Motion Clip | Track/Keyframe 기반 시간 애니메이션 |
| 모션 그래프 | Motion Graph | Node/Dependency 또는 Event Flow 기반 Motion |
| 시나리오 | Scenario | Preview 상태 데이터 묶음 |
| 변형 | Variant | 상황별 대안. **Screen Variant**(화면 단위 Override)와 **Component Variant**(Definition의 축)는 서로 다른 기능입니다 |
| 제약 조건 | Constraints | 부모 크기 변화에 대한 위치·크기 규칙 |
| 자동 레이아웃 | Auto Layout | 자식 방향, 간격과 Padding 배치 규칙 |
| Component 정의 | Component Definition | 재사용 가능한 Element sub-tree와 그 계약을 담은 에셋 |
| Component 인스턴스 | Component Instance | Definition을 **참조**하는 Element. 복사본이 아닙니다 |
| 슬롯 | Slot | Instance의 authored 자식이 들어가는 Definition의 지정 자리 |
| 노출 속성 | Exposed Property | Definition 작성자가 이름으로 공개한 Property. Instance는 이 이름으로 Override합니다 |
| 재정의 | Override | Instance가 Definition 값 위에 덮어쓰는 값 |
| 분리 | Detach | Instance를 일반 Element로 물질화해 Definition 추종을 끊는 것 |
| 전개 | Expansion | Instance를 평탄화한 트리로 펼치는 것. 메모리에만 존재합니다 |

