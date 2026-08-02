# Loading Template Screen

A full-screen loading overlay with a ProgressBar placeholder and percent label. Demonstrates the Overlay layer and Preload-vs-LazyLoad load strategy field.

## Files

- `Loading.UIToolkit.asset` / `Loading.UGUI.asset` — `UIScreenDefinition` variants, one per backend. Both point at the same `Loading.Metadata.asset` element layout.
- `Loading.Metadata.asset` — Studio element metadata (open with `Tools/NexUI/Studio`).
- `UIToolkit/Loading.uxml` + `.uss` — UI Toolkit backend asset.
- `UGUI/Loading.prefab` — uGUI backend asset.

## Try it

1. Open `Tools/NexUI/Studio`.
2. Assign `Loading.UIToolkit.asset` (or the `.UGUI` variant) as the open screen, and `Loading.Metadata.asset` as its metadata.
3. Click **Rebuild Preview** / **Validate** to see the layout.
4. `TemplateCommands.RegisterAll`을 호출하면 샘플 Command가 `TemplateSampleModel` 상태를 실제로 변경합니다. 게임 프로젝트에서는 같은 Command ID를 자체 로직으로 교체하세요.
