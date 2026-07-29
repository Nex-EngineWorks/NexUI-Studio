# NexUI Designer 문서

사용 목적에 맞는 경로에서 시작해 주세요. 문서의 기능 설명은 현재 코드 기준이며, 장기 목표는 별도 명세로 분리했습니다.

> **처음이라면 [전체 개요](overview.md)** — 프로젝트를 만든 이유, 런타임과 Designer의 전체 기능, 컴포넌트 라이브러리, 백엔드 출력, 현재 상태, 그리고 모든 문서로 가는 지도를 한 문서에 정리했습니다.

## 처음 사용하는 사용자

- [설치](getting-started/installation.md) — Core와 Designer 패키지를 올바른 순서로 설치합니다.
- [빠른 시작](getting-started/quick-start.md) — 약 10분 안에 화면을 열고 Element를 추가해 저장합니다.
- [첫 화면 만들기](getting-started/first-screen.md) — 간단한 메인 메뉴를 처음부터 구성합니다.
- [인터페이스 둘러보기](getting-started/interface-tour.md) — Toolbar, Sidebar, Canvas, Inspector와 Bottom Drawer의 위치와 역할을 익힙니다.
- [Sample 둘러보기](getting-started/sample-tour.md) — Import 후 실제로 먼저 열 Asset과 각 Sample의 검증 범위를 찾습니다.

## UI를 제작하는 사용자

- [Designer 창](user-guide/designer-window.md) — Toolbar, Sidebar, Canvas, Inspector와 Bottom Drawer의 역할을 설명합니다.
- [Screen과 Metadata](user-guide/screen-and-metadata.md) — 런타임 정의와 편집 데이터의 책임을 구분합니다.
- [Canvas 편집](user-guide/canvas-editing.md) · [Hierarchy와 Layout](user-guide/hierarchy-and-layout.md)
- [Assets 패널](user-guide/assets-panel.md) — Designer 안에서 Sprite/Font/Component를 찾아 캔버스로 드래그합니다.
- [Inspector와 Style](user-guide/inspector-and-style.md) · [Binding](user-guide/binding.md)
- [자주 사용하는 작업](user-guide/common-workflows.md) — 선택, 정렬, 부모 변경, Binding, Save/Publish를 목적별로 찾습니다.
- [AI 어시스턴트](user-guide/ai-assistant.md) — Unity 안에서 대화로 변경 계획을 만들고 검토한 뒤 Undo 가능하게 적용합니다.
- [Preview와 Scenario](user-guide/preview-and-scenarios.md) · [Validation과 Save](user-guide/validation-and-save.md)
- [uGUI Backend](user-guide/ugui-backend.md) · [UI Toolkit Backend](user-guide/ui-toolkit-backend.md)
- [Motion 선택 가이드](motion/overview.md) · [Motion Clip](motion/motion-clip-editor.md) · [Motion Graph](motion/motion-graph-editor.md) · [Motion 레시피](motion/recipes.md)

## 따라 하기

- [Inventory 화면](tutorials/inventory-screen.md)
- [HUD 화면](tutorials/hud-screen.md)
- [애니메이션 Popup](tutorials/animated-popup.md)

## 기능 상태와 문제 해결

- [현재 기능 상태](reference/feature-status.md) — 실제 지원·부분 지원·실험적 기능을 확인합니다.
- [Feature Parity Matrix](FeatureParityMatrix.md) — Backend와 테스트 여부를 표로 확인합니다.
- [목표 기능 명세](reference/feature-specification.md) — 장기 제품 범위를 확인합니다.
- [단축키](reference/shortcuts.md) · [용어](reference/terminology.md)
- [알려진 제한](reference/known-limitations.md) · [문제 해결](reference/troubleshooting.md)
- [Backend 지원 범위](reference/backend-support-matrix.md) — Preview, Metadata, 일반 Save와 Generated 결과를 비교합니다.
- [Asset Ownership](reference/asset-ownership.md) — 수동 파일, Designer 파일과 생성 파일의 책임을 구분합니다.
- [Validation Catalog](reference/validation-catalog.md) — 실제 Validation Code별 원인과 해결 방법을 찾습니다.
- [Compatibility](reference/compatibility.md) · [Upgrading](reference/upgrading.md)
- [출시 준비 체크리스트](reference/release-readiness.md)

## 고급 기능

- [재사용 Component](advanced/reusable-components.md) — 선택 요소를 Component Definition으로 만들고 Instance/Slot/Variant/Override로 재사용합니다.
- [Figma Bridge](advanced/figma-bridge.md) — 인증, JSON 조회와 첫 Frame Import를 사용합니다.
- [Migration Wizard](advanced/migration-wizard.md) — 구버전 Namespace와 Package ID를 안전하게 치환합니다.
- [Runtime Debugging](advanced/runtime-debugging.md) — Play Mode Snapshot과 Overlay를 사용합니다.
- [Design Token](advanced/design-tokens.md) · [Screen Flow](advanced/screen-flow-editor.md) · [Sync와 Publish](advanced/sync-and-publish.md)

## NexUI Designer를 개발하는 사용자

- [아키텍처](developer/architecture.md) · [프로젝트 구조](developer/project-structure.md) · [확장 API](developer/api-reference.md)
- [Panel 추가](developer/adding-panels.md) · [Inspector 확장](developer/extending-the-inspector.md) · [Backend 추가](developer/adding-backends.md) · [Validation 추가](developer/adding-validation.md)
- [직렬화](developer/serialization.md) · [코딩 규칙](developer/coding-conventions.md) · [테스트](developer/testing.md)
- [Metadata Schema](developer/metadata-schema.md) — Screen, Metadata, Motion, Scenario, Flow와 Publish 참조 관계를 설명합니다.
- [성능 측정](developer/performance.md) · [Git 협업](developer/git-workflow.md)
## 생산성 기능 빠른 링크

- [화면 생성 마법사](user-guide/screen-creation-wizard.md)
- [전환 프리셋](user-guide/transition-presets.md)
- [Auto Layout 변환과 Anchor 추천](user-guide/layout-conversion-and-anchor.md)
- [Validation Auto Fix](user-guide/validation-auto-fix.md)
