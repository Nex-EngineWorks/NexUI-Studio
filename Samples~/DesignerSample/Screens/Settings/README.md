# Settings Template Screen

A settings menu window with volume rows and Back/Apply buttons. Demonstrates a Window-layer, single-instance screen with default focus and a working sample Apply command.

## Files

- `Settings.UIToolkit.asset` / `Settings.UGUI.asset` — `UIScreenDefinition` variants, one per backend. Both point at the same `Settings.Metadata.asset` element layout.
- `Settings.Metadata.asset` — Studio element metadata (open with `Tools/NexUI/Studio`).
- `UIToolkit/Settings.uxml` + `.uss` — UI Toolkit backend asset.
- `UGUI/Settings.prefab` — uGUI backend asset.

## Try it

1. Open `Tools/NexUI/Studio`.
2. Assign `Settings.UIToolkit.asset` (or the `.UGUI` variant) as the open screen, and `Settings.Metadata.asset` as its metadata.
3. Click **Rebuild Preview** / **Validate** to see the layout.
4. `TemplateCommands.RegisterAll`을 호출하면 Apply가 `settings.applied` 상태를 변경하고 Back은 화면 Stack을 닫습니다. 게임 프로젝트에서는 같은 Command ID를 자체 로직으로 교체하세요.
