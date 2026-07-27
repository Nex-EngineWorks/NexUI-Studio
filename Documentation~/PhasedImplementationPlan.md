# Phased Implementation Plan

각 Phase는 독립 release gate다. 이전 Phase의 데이터 안전성과 migration이 통과하기 전 다음 Phase를 시작하지 않는다.

## 진행 현황

| Phase | 상태 | 결과 |
| --- | --- | --- |
| 0 — Stability and Data Trust | 완료 | [보고서](Phase0CompletionReport.md) |
| 1 — Typed Property and Backend Parity | 완료 | [보고서](Phase1CompletionReport.md) |
| 2 — Core Component Parity | 완료 | 별도 보고서 없음. [Feature Parity Matrix](FeatureParityMatrix.md)의 컴포넌트 행 참고 |
| 3 — Reusable Components | 완료 | [보고서](Phase3CompletionReport.md) |
| 4–12 | 미착수 | 아래 계획 참고 |

Phase 3 이후 추가된 Assets 패널과 Canvas Drag & Drop은 Phase 계획 밖의 UX 작업이며 [Assets 패널](user-guide/assets-panel.md)에 문서화되어 있다.

## 공통 시작/종료 형식

시작 전: 구조 분석, 변경 파일, API/Metadata 변경, migration, 위험, 테스트 계획을 기록한다.  
종료 후: 실제 변경, 수정 파일, 테스트 결과, 수동 검증, 남은 Partial, 다음 Phase를 기록한다.

## Phase 0 — Stability and Data Trust

- 목표: Save/Discard/Clone/Visibility/Identity/CI에서 조용한 데이터 유실 제거.
- 주요 파일: Context, Metadata Utility/Asset/Element, Hierarchy Migration, uGUI Serializer/Surface, Validation, Tests, `.github/workflows`.
- API: subtree clone result/id map, stable identity component/index, static validation script.
- Metadata: schema v2, `runtimeVisible` 추가. 기존 `hiddenInDesigner`는 Editor-only 의미로 유지.
- Migration: v1 → v2에서 `runtimeVisible = !hiddenInDesigner`; Undo/backup/report, idempotent.
- 테스트: deep clone, clipboard snapshot, subtree remap, visibility migration/output, stable ID rename/duplicate/orphan, CI scripts.
- 위험: 외부 Inspector dirty state, 사용자 prefab object 오인, migration 재실행.
- 완료 조건: 모든 Phase 0 테스트 + 전체 compile + Unity EditMode/PlayMode + 수동 rename/discard 검증.

## Phase 1 — Typed Property and Backend Parity

- 목표: Preview와 Backend output의 property 차이를 명시하고 Typed Property Schema 도입.
- 변경: Property registry, capability matrix, converter/fallback, save dry-run/diff.
- Metadata/API: `DesignerPropertyId`, typed value/override/binding/motion target.
- Migration: legacy string property path adapter.
- 테스트: property별 양 Backend golden output과 unsupported report.
- 위험: 기존 Variant/Motion/Responsive 문자열 경로 호환.
- 완료 조건: Layout/Visual/Typography 핵심 property의 Authoring→Runtime 경로가 matrix와 일치.

## Phase 2 — Core Component Parity

- 목표: Label, Image, Button부터 Toast까지 상용 기본 컴포넌트 완성.
- 변경: Component adapter registry, Preview/Serializer/Runtime adapter.
- Metadata: component별 typed property block 또는 registry-driven property bag.
- Migration: 기존 type string 유지 + adapter mapping.
- 테스트: 컴포넌트별 uGUI/UI Toolkit/입력/접근성 matrix.
- 위험: Descriptor만 늘고 Backend가 비는 상태.
- 완료 조건: 각 컴포넌트가 정의된 8단계 완료 기준을 충족하거나 정확히 Partial 표시.

## Phase 3 — Reusable Components

- 목표: 사용자 Component Definition/Instance/Slot/Variant.
- API/Metadata: versioned definition, instance override, slot content, detach, nested dependency graph.
- Migration: definition version migration과 missing definition recovery.
- 테스트: propagate/reset/detach/cycle/version.
- 위험: nested component cycle과 override 데이터 유실.
- 완료 조건: definition 수정 전파와 instance override가 양 Backend에서 보존.

## Phase 4 — Binding, Interaction, Input, Focus

- 목표: typed binding, key browser, event/action graph, multi-device input/focus.
- API: binding type/mode/converter, interaction adapter, command/action schema.
- Migration: legacy binding string key adapter.
- 테스트: two-way, async/error, keyboard/mouse/touch/gamepad, focus scope.
- 위험: 구독 누수와 device 전환 race.
- 완료 조건: 입력 및 binding의 PlayMode matrix 통과.

## Phase 5 — Motion Consolidation

- 목표: Motion Clip/Legacy Graph/v2/State Machine의 단일 Runtime 실행 모델과 migration.
- API: typed motion property, graph node registry, debug trace.
- Migration: legacy graph/track conversion report.
- 테스트: timeline, conflict, cancel, reduced motion, graph cycle.
- 위험: animation semantic drift.
- 완료 조건: preview/runtime 결과 일치와 reduced-motion fallback 보장.

## Phase 6 — Responsive and Multi-device Preview

- 목표: resolution/input을 넘어 safe area, DPI, locale, platform, accessibility 조건 지원.
- API/Metadata: typed condition/result, device profile, safe area.
- Migration: 기존 responsive range adapter.
- 테스트: device matrix screenshot/overflow/navigation.
- 위험: rule 우선순위 충돌.
- 완료 조건: deterministic rule resolution과 양 Backend 동일 결과.

## Phase 7 — Theme, Token, Localization

- 목표: typed design token, theme inheritance/runtime switch, localization/RTL/pseudo locale.
- Migration: hardcoded values → optional token reference.
- 테스트: missing token/font/key, theme diff, text expansion.
- 위험: locale별 layout/asset fallback.
- 완료 조건: theme/locale matrix와 accessibility theme 통과.

## Phase 8 — Backend Sync and Round Trip

- 목표: 공통 Backend Snapshot, semantic diff, 선택 merge, 양방향 변환.
- API: ownership-aware snapshot/diff/apply plan.
- Migration: legacy name mapping → stable id.
- 테스트: hand-authored data preservation, conflict, orphan.
- 위험: 사용자 asset 덮어쓰기.
- 완료 조건: dry-run과 explicit apply 없이 destructive 변경 없음.

## Phase 9 — Figma Incremental Sync

- 목표: first-frame import를 stable mapping/incremental sync로 확장.
- API/Metadata: node mapping, local override, image cache, sync report.
- Migration: 기존 imported element mapping 생성.
- 테스트: rename/move/delete/rate limit/token redaction.
- 위험: remote/local conflict와 credential 노출.
- 완료 조건: 단방향 incremental sync 안정화 후에만 양방향 검토.

## Phase 10 — AI Assistant

- 목표: context-scoped plan, schema validation, diff, explicit apply, single Undo.
- API: provider-neutral request/action schema, cancel/retry/cost estimate.
- 테스트: destructive rejection, malformed plan, redaction, undo.
- 위험: 무승인 asset 변경과 민감 데이터 전송.
- 완료 조건: 승인 전 write 0건, apply 결과 완전 Undo.

## Phase 11 — QA, Validation, Runtime Debug

- 목표: validation/autofix/runtime trace/performance 분석 통합.
- API: rule registry, safe fix classification, runtime snapshot transport.
- 테스트: 각 validation code와 false-positive fixture.
- 위험: autofix가 사용자 데이터를 수정.
- 완료 조건: safe fix만 무확인 실행, destructive fix는 명시 승인.

## Phase 12 — Git, Migration, Release

- 목표: deterministic serialization, semantic diff, 전체 migration catalog, release readiness.
- API: migration runner/dry-run/report/backup.
- 테스트: clean install, Git URL, samples, OS/Unity/Mono/IL2CPP matrix.
- 위험: package metadata와 실제 지원 버전 불일치.
- 완료 조건: release checklist와 CI artifact가 모두 통과하고 문서 status가 코드와 일치.

## 현재 실행 범위

Phase 3(재사용 Component) 구현과 컴파일 검증까지 종료했다. 상세 결과는 [Phase 3 완료 보고서](Phase3CompletionReport.md)를 참고한다.
Phase 4는 Phase 3 EditMode 테스트 실행 결과 확인과 사용자 승인 후 시작하며 자동으로 구현하지 않는다.
