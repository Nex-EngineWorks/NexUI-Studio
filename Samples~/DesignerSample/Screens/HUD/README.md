# HUD Template Screen

A health/resource bar HUD using StatBar elements (background + fill pairs). Demonstrates an always-additive, non-input-blocking HUD-layer screen intended to be preloaded and kept open.

## Files

- `HUD.UIToolkit.asset` / `HUD.UGUI.asset` — `UIScreenDefinition` variants, one per backend. Both point at the same `HUD.Metadata.asset` element layout.
- `HUD.Metadata.asset` — Studio element metadata (open with `Tools/NexUI/Studio`).
- `UIToolkit/HUD.uxml` + `.uss` — UI Toolkit backend asset.
- `UGUI/HUD.prefab` — uGUI backend asset.

## Try it

1. Open `Tools/NexUI/Studio`.
2. Assign `HUD.UIToolkit.asset` (or the `.UGUI` variant) as the open screen, and `HUD.Metadata.asset` as its metadata.
3. Click **Rebuild Preview** / **Validate** to see the layout.
4. `TemplateCommands.RegisterAll`을 호출하면 샘플의 닫기·적용·확인 Command가 `TemplateSampleModel` 상태를 실제로 변경합니다. 게임 프로젝트에서는 같은 Command ID를 자체 로직으로 교체하세요.
