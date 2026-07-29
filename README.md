# NexUI Designer

NexUI Designer는 NexUI 화면을 Unity Editor에서 구성하고 검증하는 제작 도구입니다. uGUI와 UI Toolkit을 동일한 Designer Metadata로 편집하며, Binding·Motion·Validation·Publish 흐름을 연결합니다.

## 빠른 제작 흐름

```text
새 화면 만들기 → 요소 배치 → Binding 설정 → 전환 프리셋 적용
→ Scenario로 상태 확인 → Validation/Fix → Save/Publish → Play Mode 확인
```

Global Toolbar의 `+ 새 화면`에서 Screen Definition, Designer Metadata와 Backend 에셋을 한 번에 만들 수 있습니다. Canvas Toolbar의 `Layout`과 `Transition`은 선택 요소를 정리하고 기존 Motion Clip 에셋으로 전환을 생성합니다. Scenario 필드는 Mock Data를 현재 화면에 즉시 적용합니다.

## 요구 환경

- Unity 6000.4 이상
- NexUI Core 0.1.0
- UniTask 2.5.10

## 문서

- [문서 홈](Documentation~/index.md)
- [설치](Documentation~/getting-started/installation.md)
- [인터페이스 둘러보기](Documentation~/getting-started/interface-tour.md)
- [빠른 시작](Documentation~/getting-started/quick-start.md)
- [Sample 둘러보기](Documentation~/getting-started/sample-tour.md)
- [화면 생성 마법사](Documentation~/user-guide/screen-creation-wizard.md)
- [전환 프리셋](Documentation~/user-guide/transition-presets.md)
- [Auto Layout과 Anchor](Documentation~/user-guide/layout-conversion-and-anchor.md)
- [Validation Auto Fix](Documentation~/user-guide/validation-auto-fix.md)
- [Backend 지원 범위](Documentation~/reference/backend-support-matrix.md)
- [현재 기능 상태](Documentation~/reference/feature-status.md)
- [구현 상태 표](Documentation~/FeatureParityMatrix.md)
- [기능 지원 매트릭스](Documentation~/FeatureParityMatrix.md)
- [목표 기능 목록](FunctionList.md)
- [알려진 제한사항](Documentation~/reference/known-limitations.md)

> 이 패키지는 개발 중입니다. 지원 범위와 제한사항은 기능 상태 문서를 먼저 확인하세요.

## 저장소와 라이선스

- [NexUI Designer 저장소](https://github.com/swallow-smoke/NexUI-Designer)
- [NexUI Core 저장소](https://github.com/swallow-smoke/NexUI)
- [MIT License](LICENSE.md)
