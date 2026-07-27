# Phase 1 Start Report

기준일: 2026-07-27

## 현재 구조 분석

- Element 기본 rect, tint, text color/font size, Auto Layout, anchor와 clip은 metadata에 있으나 layout/style/typography가 서로 다른 flat field와 nested object로 분산되어 있다.
- UI Toolkit generated USS는 absolute/flex layout, tint, font size, runtime visibility, clip만 출력한다.
- uGUI serializer는 RectTransform, LayoutGroup, Image/TMP 일부만 출력하며 min/max, opacity, pivot/transform, border, typography 상세 속성은 저장하지 않는다.
- Variant/Responsive override는 `propertyPath`와 `value` 문자열만 저장하고 runtime `UIManager`가 문자열을 해석한다.
- Backend support는 component 단위로만 존재하며 property 단위 capability/fallback registry와 save dry-run이 없다.

## 변경 대상

- Runtime metadata: typed property ID/value, layout/visual/typography style, typed override.
- schema migration: v2 → v3 flat field와 legacy property path를 typed schema로 승격.
- Editor property registry/adapter: type, default, backend capability, converter, fallback, binding/animation/override 허용 여부.
- Inspector/Preview: 핵심 layout, visual, typography authoring과 canvas 반영.
- uGUI/UI Toolkit serializer: 공통 property adapter를 사용한 backend output.
- Validation/Save report: unsupported/fallback/conflict와 dry-run plan.
- Variant/Responsive editor/service: enum 기반 property 선택과 legacy adapter 유지.

## API와 Metadata 변경

- `DesignerPropertyId`, `DesignerPropertyValueType`, `DesignerPropertyValue`.
- `DesignerLayoutStyleMetadata`, `DesignerVisualStyleMetadata`, `DesignerTypographyMetadata`.
- Variant/Responsive override에 typed ID/value를 추가하되 기존 `propertyPath`/`value`를 제거하지 않는다.
- `DesignerSavePreviewService`와 structured planned change model. 기존 serializer save interface는 호환성을 위해 유지한다.

## Migration

- schema v3에서 기존 tint/textColor/fontSize/shape/clip/Auto Layout 값을 typed style 초기값으로 복사한다.
- 알려진 legacy property path는 typed ID로 변환한다.
- 알 수 없는 path는 그대로 보존하고 Legacy/Partial validation으로 표시한다.
- migration은 Undo 가능하고 반복 실행 시 no-op이다.

## 위험 요소

- uGUI와 UI Toolkit이 동일한 시각 효과를 제공하지 않아 fallback이 필요하다.
- hand-authored UXML/USS는 자동 덮어쓰기하지 않는다.
- TMP font와 Material 같은 Unity Object reference의 JSON companion round trip을 유지해야 한다.
- 기존 runtime string override consumer와의 호환을 유지해야 한다.
- style default가 기존 asset 외형을 바꾸지 않아야 한다.

## 테스트 계획

- property registry/path/value converter 단위 테스트.
- v2 → v3 migration과 idempotency.
- typed Variant/Responsive compile 및 legacy path 호환.
- UI Toolkit USS golden output: min/max, transform, opacity, border/radius, typography.
- uGUI prefab output: CanvasGroup, LayoutElement, RectTransform, Outline/Shadow, TMP.
- dry-run이 Create/Modify/Skip/Unsupported/Conflict/Orphan과 user-owned impact를 구분하는지 검증.
- 전체 solution compile, package static validation, CI workflow syntax 확인.
