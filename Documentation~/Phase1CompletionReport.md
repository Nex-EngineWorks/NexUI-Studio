# Phase 1 Completion Report

기준일: 2026-07-27

## 변경 요약

- Layout/Style Inspector에서 typed layout, visual, typography 값을 편집하고 Designer canvas에서 즉시 확인할 수 있다.
- 저장 전 `Save Preview`에서 Create, Modify, Skip, Unsupported, Preview Only, Conflict, Orphan, User Impact/Fallback을 분리해 확인할 수 있다.
- Variant/Responsive override는 property 문자열 대신 enum 기반 property를 선택하며 기존 string asset도 그대로 읽고 runtime contract로 compile한다.
- uGUI와 UI Toolkit이 동일하게 표현하지 못하는 속성은 조용히 버리지 않고 Validation과 Save Preview에 backend별 제한을 표시한다.

## 아키텍처

- Runtime metadata의 `DesignerPropertyId`와 `DesignerPropertyValue`가 property identity/value의 공통 모델이다.
- Editor의 `DesignerPropertyRegistry`가 type, default, backend capability, fallback, usage, parse/serialize와 strict validation을 소유한다.
- `DesignerPropertyAdapter`가 legacy flat field와 v3 typed style block을 함께 읽고 쓰므로 기존 화면 외형과 runtime string consumer가 유지된다.
- Inspector → Metadata → Canvas Preview → backend serializer/code generator가 같은 adapter를 사용한다.
- Runtime handle은 color와 typography capability를 제공하며 `UIManager`가 position/scale/color/font-size를 포함한 compiled override를 적용한다.
- `DesignerSavePreviewService`는 asset을 dirty/write하지 않고 prefab/UXML과 metadata를 비교해 structured impact plan을 만든다.

## 수정 파일

- `Runtime/Metadata/DesignerPropertyMetadata.cs`: property enum/value와 layout/visual/typography block.
- `Runtime/Metadata/DesignerElementMetadata.cs`, `DesignerMetadataAsset.cs`: schema v3 style field와 version.
- `Runtime/Metadata/DesignerVariantMetadata.cs`, `DesignerResponsiveMetadata.cs`: typed override와 legacy mirror.
- `Editor/Properties/DesignerPropertyRegistry.cs`, `DesignerPropertyAdapter.cs`: registry, converter, validation, compatibility.
- `Editor/Core/DesignerHierarchyMigration.cs`: v2 → v3 migration.
- `Editor/Inspectors/LayoutInspector.cs`, `StyleInspector.cs`: typed authoring UI.
- `Editor/Viewport/NexUIDesignerViewport.cs`: transform/opacity/border/radius/typography preview.
- `Editor/Serialization/UGUIAssetSerializer.cs`, `UIToolkitCodeGenerator.cs`: backend output과 fallback report.
- `Editor/Serialization/DesignerMetadataJsonSerializer.cs`: schema v3 JSON과 Unity Object GUID/local ID round trip.
- `Editor/Serialization/DesignerSaveReport.cs`, `DesignerSavePreviewService.cs`, `DesignerSavePreviewWindow.cs`: structured dry-run/save preview.
- `Editor/Validation/DesignerValidationService.cs`: property type/path/usage/backend parity validation.
- NexUI Runtime의 `Capabilities.cs`, `UGUIElementHandle.cs`, `UIToolkitElementHandle.cs`, `UIManager.cs`: runtime typed override 적용.
- EditMode test files: registry/migration/JSON/generator/uGUI/save preview/validation regression.

## Metadata 및 API 변경

- schema version: v2 → v3.
- 신규: `DesignerPropertyId`, `DesignerPropertyValueType`, `DesignerPropertyValue`.
- 신규: `DesignerLayoutStyleMetadata`, `DesignerVisualStyleMetadata`, `DesignerTypographyMetadata`.
- 신규: `DesignerPropertyDescriptor`, `DesignerPropertyBackendSupport`, `DesignerPropertyUsage`.
- 신규: `DesignerSaveImpactKind`, `DesignerSaveImpact`, `DesignerSavePreviewService`.
- 신규 runtime capability: `IUIColorCapability`, `IUITypographyCapability`.
- Variant/Responsive의 기존 `propertyPath`/`value`는 삭제하지 않았고 typed ID/value와 함께 유지한다.

## Migration

- v2 asset을 열면 tint, textColor, fontSize, shape, clipChildren을 v3 visual/typography/layout style에 복사한다.
- 알려진 legacy override path는 typed property ID/value로 변환한다.
- 알 수 없는 legacy path는 삭제하지 않고 validation warning으로 표시한다.
- migration은 version 기반이고 반복 실행 시 no-op이며 interactive open 경로에서는 Undo/dirty 처리된다.
- companion JSON format v3는 nested typed style, typed Variant/Responsive override, Material/Font asset GUID와 local ID를 보존한다.

## 테스트

실행 결과:

- `dotnet build emiteat.NexUI.Designer.Editor.csproj --no-restore --nologo -p:UseSharedCompilation=false`: 경고 0, 오류 0.
- `dotnet build emiteat.NexUI.Designer.Tests.EditMode.csproj --no-restore --nologo -p:UseSharedCompilation=false`: 경고 0, 오류 0.
- `dotnet build NexUI.sln --no-restore --nologo -m:1 -p:UseSharedCompilation=false`: 경고 0, 오류 0.
- NexUI Runtime package static validation: 통과.
- NexUI Designer package static validation: 통과.
- Documentation link validation: Markdown 85개, relative link 230개, 오류 0.
- `git diff --check`: 두 package repository 모두 통과.

추가/확장된 테스트 범위:

- property registry alias/default/parse/strict validation/backend support.
- v2 → v3 style/override migration과 기존 idempotency.
- typed Variant/Responsive runtime compile.
- typed layout/visual/typography USS output.
- typed uGUI LayoutElement/AspectRatioFitter/RectMask2D/CanvasGroup/Image/Outline/Shadow/TMP output.
- JSON v3 nested typed style와 typed override round trip.
- save impact 8개 category 및 read-only prefab preview.
- typed override value mismatch validation.

Unity Test Runner EditMode/PlayMode는 테스트 코드와 컴파일까지 확인했지만, 현재 같은 프로젝트를 Unity Editor가 열고 있어 두 번째 batchmode process를 실행하지 않았다. 실행하지 않은 테스트를 통과로 간주하지 않는다.

## 수동 검증

1. Designer에서 v2 metadata backup을 열고 schema v3 migration 후 기존 색상, 글꼴 크기, clip과 shape 외형이 유지되는지 확인한다.
2. Layout Inspector에서 min/max, pivot, rotation, scale, margin, aspect, wrap/align/justify/overflow를 바꾸고 canvas와 양 backend output을 비교한다.
3. Style Inspector에서 opacity, border/radius, shadow/outline, mask, 9-slice/image fit, typography/RTL/effects를 바꾸고 fallback warning을 확인한다.
4. Variant/Responsive window에서 enum property와 typed value를 설정하고 runtime에서 color/font-size/position/scale/visibility가 적용되는지 확인한다.
5. `Save Preview`를 열어 신규/기존/user-owned/orphan element와 unsupported property가 올바른 category에 표시되는지 확인한다.
6. hand-authored UXML은 write 대상이 되지 않고 generated marker가 있는 UXML/USS만 create/modify 대상으로 표시되는지 확인한다.
7. Unity Test Runner에서 전체 EditMode와 PlayMode를 실행한다.

## 남은 위험

- stock uGUI는 max size, per-element margin, numeric corner radius, inner shadow, blur, gradient를 정확히 표현하지 못해 metadata 보존 + fallback/unsupported report를 사용한다.
- UI Toolkit은 hand-authored UXML을 자동 수정하지 않으며 unmatched element는 preview-only다.
- font family/fallback과 일부 material/advanced text effect는 backend asset 종류에 따라 제한된다.
- visual border/radius/shadow는 현재 공통 uniform 값 중심이며 per-side/per-corner 다중 shadow는 아직 지원하지 않는다.
- Unity Test Runner와 실제 Scene/Game View 시각 비교는 미실행이다.

## 다음 Phase

Phase 2는 Core Component Parity다. Label, Image, Button부터 Toggle/Choice/List/Grid/Scroll/Modal/Tooltip/Toast까지 component adapter의 Authoring, Preview, uGUI, UI Toolkit, Runtime, Validation, Test 경로를 하나씩 완성한다. 이 Phase는 자동으로 시작하지 않는다.
