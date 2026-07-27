# 현재 기능 상태

이 문서는 현재 코드 기준입니다. **지원**은 일반 사용 가능, **부분 지원**은 Backend/저장 범위 제한, **실험적**은 검증이 더 필요한 도구, **진행 중**은 작업 경로 일부만 존재, **미지원**은 사용할 수 없음을 뜻합니다.

| 기능 | 상태 | uGUI | UI Toolkit | 비고 |
|---|---|---|---|---|
| 단일·다중·박스 선택 | 지원 | 지원 | 지원 | Metadata Canvas 공통 |
| 이동·크기·정렬·Group | 지원 | 지원 | 지원 | Undo, Key Object 정렬 지원 |
| Auto Layout/Constraints | 부분 지원 | 지원 | 지원 | Row/Column/Grid 저장 지원, Constraints 일부 Preview 중심 |
| Component Registry | 지원 | 부분 지원 | 부분 지원 | Component별 Backend support 확인 |
| 재사용 Component (Definition/Instance) | Beta | 지원 | 지원 | 참조 기반 전개. Definition 수정이 모든 Instance에 즉시 반영 |
| Component Slot / Exposed Property Override | Beta | 지원 | 지원 | Typed Override. 적용 불가 Property는 Validation 보고 |
| Component Variant | Beta | 지원 | 지원 | Bool/Enum/String, Rule 기반 Override와 표시 전환 |
| Component Detach / Swap | Beta | 지원 | 지원 | 파괴적 작업은 확인 후 실행 |
| Assets 패널 (탐색/검색/필터) | Beta | 공통 | 공통 | 읽기 전용. Rename/Move/Delete는 Project 창 담당 |
| Asset → Canvas Drag & Drop | Beta | 공통 | 공통 | Sprite/Font/Material/Component Definition. 그 외는 거부 |
| Text/Value/Visibility/Class/Command Binding | 지원 | 지원 | 지원 | 프로젝트가 Runtime Key를 등록 |
| Designer Preview/Interactive Log | 지원 | 지원 | 지원 | 안전을 위해 Command 실행 대신 Simulation Log |
| Scenario Apply/Timeline | 지원 | 공통 | 공통 | Bool/Number/Text/Sprite/List, Preview 환경 복원 |
| Validation/Save Report | 지원 | 지원 | 지원 | 일부 프로젝트 계약은 Warning |
| Setup Doctor | 지원 | 공통 | 공통 | 의존성·Registry·Scene Backend·출력 경로 검사 |
| 통합 Inspector | 지원 | 공통 | 공통 | 검색, Workflow 필터, Beginner/Pro, Section 상태 보존 |
| AI 어시스턴트 | Beta | 공통 | 공통 | Responses API, 계획 검증, 명시적 Apply, 단일 Undo; 실제 API 계정별 검증 필요 |
| uGUI Prefab 저장 | 부분 지원 | 부분 지원 | 해당 없음 | 기본 Rect/Image/Text/Button/Fill |
| UI Toolkit 일반 Save | 지원 | 해당 없음 | 지원 | Generated UXML/USS 안전 재생성, 사용자 파일 보존 |
| `.g.uxml/.g.uss` 생성 | 지원 | 해당 없음 | 지원 | 별도 Generation 도구 |
| Motion Clip 편집/직접 재생 | 지원 | 지원 | 지원 | 일부 Property 차이 |
| Motion Trigger Runtime 연결 | 지원 | 지원 | 지원 | Capability 자동 구독 + Lifecycle Notify API |
| Legacy Motion Graph | 부분 지원 | 공통 | 공통 | 단일 Preset Dependency Graph |
| Motion Graph | 실험적 | 공통 | 공통 | Utilities의 실험 기능 그룹에 v2만 노출 |
| Motion State Machine | 실험적 | 공통 | 공통 | Transition Clip Preview |
| AnimationClip Import/Export | 지원 | 지원 | 변환 에셋 공통 | Rect/Transform/CanvasGroup 지원 |
| Figma 인증/조회/Frame Import | Beta | 공통 | 공통 | 계층·좌표·Text·Solid Fill·Auto Layout, Sync 제외 |
| Migration Scan/Apply | 지원 | 공통 | 공통 | Utilities 메뉴, 변경 전 `.bak` 생성 |
| Runtime Snapshot/Overlay | 지원 | 공통 | 공통 | Metadata/Live Preview Capture와 Diff 메뉴 |
| Screen Flow/Design Token | 실험적 | 공통 | 공통 | 고급 도구, 수동 검증 필요 |

장기 목표는 [목표 기능 명세](feature-specification.md), 구체적인 제약은 [알려진 제한](known-limitations.md)을 확인해 주세요.
# 생산성 기능 상태

| 기능 | 상태 | uGUI | UI Toolkit | 테스트 | 비고 |
|---|---|---:|---:|---:|---|
| 화면 생성 마법사 | Beta | Yes | Yes | Partial | 기존 파일 보호 및 실패 롤백 |
| Transition Preset | Beta | Yes | Yes | Yes | 기존 Motion Clip 에셋 사용 |
| Preview Scenario Sprite/List | Complete | Common | Common | Yes | 정적 Apply와 Timeline |
| Auto Layout 감지/변환 | Complete | Yes | Yes | Yes | Row/Column/Grid |
| Anchor 추천 | Beta | Common | Common | Yes | Rect 보존 |
| Validation Auto Fix | Beta | Yes | Partial | Partial | 위험한 수정은 확인 필요 |
