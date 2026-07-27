# Risk Report

> [!NOTE]
> **Phase 0 착수 시점의 위험 등록부**입니다. "Phase 0 action" 열은 당시 계획한 대응이며, 실제 수행 결과는
> [Phase 0 완료 보고서](Phase0CompletionReport.md)에 있습니다. 기록 보존을 위해 갱신하지 않습니다.
> 현재 남아 있는 제약은 [알려진 제한](reference/known-limitations.md)을 확인하세요.

## P0: 데이터 유실 및 잘못된 출력

| Risk | Evidence | Impact | Phase 0 action |
| --- | --- | --- | --- |
| 수동 Clone 필드 누락 | Context의 `CloneElement`가 일부 필드만 복사 | Duplicate/Paste 시 motion/theme/layout/focus/image 등 유실 | JSON deep clone으로 통합 |
| Subtree 관계 손실 | 선택 노드만 복제하고 parent id remap 없음 | Group/parent-child 복제 시 원본 parent 참조 | closure clone + id map |
| Clipboard가 원본 객체 참조 | `_clipboard`에 element reference 저장 | 복사 후 원본 편집이 Paste 결과에 반영 | Copy 시 snapshot deep clone |
| Visibility 의미 혼합 | `hiddenInDesigner`를 preview와 uGUI `SetActive`에 함께 사용 | Designer에서 숨긴 것이 Runtime 출력도 사라짐 | `runtimeVisible` 분리 + migration |
| Prefab 이름 기반 저장 | Serializer `FindDescendant(name)` | Rename/중복 이름 시 잘못된 Object 수정 | Stable Identity 우선 + fallback tag 부착 |

## P1: 세션과 충돌

| Risk | Current state | Remaining action |
| --- | --- | --- |
| Discard가 flag만 제거 | baseline restore 경로 추가됨 | 외부 Inspector 동시 변경 충돌 정책/테스트 확장 |
| Screen/Metadata mismatch | 자동 resolve와 save guard 추가됨 | 중복 metadata 선택 UX 개선 |
| Domain reload window rebuild | EditorStyles 초기화 순서 예외 제거됨 | 열린 창 실제 reload smoke test |
| Undo 일관성 | 주요 편집 경로에 Undo 존재 | migration/identity auto-add를 단일 Undo group으로 묶기 |

## P1: Backend 및 Generated Asset

- 사용자 UXML은 Generated Marker 없이는 덮어쓰지 않는다.
- uGUI Prefab은 LoadPrefabContents 경로를 사용하지만 Stable ID 부재 시 이름 fallback에 의존한다.
- Designer 소유/사용자 소유 Object 구분이 아직 없어 자동 삭제는 금지 상태를 유지해야 한다.
- Orphan은 report만 제공하고 삭제하지 않아야 한다.

## P1: Runtime / Editor 경계

- 현재 asmdef상 Runtime → UnityEditor 참조는 발견되지 않았다.
- CI에 소스 스캔을 추가해 재발을 차단해야 한다.
- Runtime Metadata schema 변경은 Editor migration과 함께 배포해야 한다.

## P1: Event와 비동기 상태

- UIStateStore/UISignal은 snapshot 순회로 수정되어 구독 해제 중 순회 손상을 방지한다.
- UIManager transition은 screen id별로 직렬화되지만 실제 PlayMode conflict/Queue 회귀 테스트 실행이 필요하다.
- ContextBoundSubscriptions는 attach/detach 테스트가 있으나 모든 Satellite Window가 같은 패턴을 쓰는지 지속 감사가 필요하다.

## P1: CI/문서 불일치

- `.github/workflows/unity-tests.yml`은 실제 존재한다.
- 현재 workflow는 EditMode/PlayMode만 실행하고 package.json, `.meta`, Runtime UnityEditor 참조, 문서 링크, generated asset deterministic 검사를 별도 gate로 두지 않는다.
- 문서의 Complete 표기는 실제 Backend output/test와 다시 대조해야 한다.

## Phase 0 종료 전 필수 확인

1. 복제 후 모든 nested list/object가 공유되지 않는다.
2. Subtree의 parent/focus/motion target 참조가 새 ID로 remap된다.
3. 기존 metadata v1을 열면 화면 결과가 바뀌지 않는다.
4. editorHidden은 Backend output에 영향을 주지 않는다.
5. runtimeVisible=false만 실제 Backend visibility를 제어한다.
6. Rename 후 Stable ID로 같은 Prefab Object를 갱신한다.
7. Duplicate Stable ID가 Error로 보고된다.
8. CI 정적 검사가 로컬 스크립트와 GitHub Actions에서 동일하게 실행된다.

