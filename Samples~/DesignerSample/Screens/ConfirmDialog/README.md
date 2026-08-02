# ConfirmDialog Template Screen

A modal confirm/cancel popup. Demonstrates Modal layer, trap-focus policy, additive open policy, and two working sample command bindings (confirm / nexui.close).

## Files

- `ConfirmDialog.UIToolkit.asset` / `ConfirmDialog.UGUI.asset` — `UIScreenDefinition` variants, one per backend. Both point at the same `ConfirmDialog.Metadata.asset` element layout.
- `ConfirmDialog.Metadata.asset` — Studio element metadata (open with `Tools/NexUI/Studio`).
- `UIToolkit/ConfirmDialog.uxml` + `.uss` — UI Toolkit backend asset.
- `UGUI/ConfirmDialog.prefab` — uGUI backend asset.

## Try it

1. Open `Tools/NexUI/Studio`.
2. Assign `ConfirmDialog.UIToolkit.asset` (or the `.UGUI` variant) as the open screen, and `ConfirmDialog.Metadata.asset` as its metadata.
3. Click **Rebuild Preview** / **Validate** to see the layout.
4. `TemplateCommands.RegisterAll`을 호출하면 Confirm이 `confirmDialog.result`를 갱신하고 Modal을 닫으며 Cancel은 화면 Stack을 닫습니다. 게임 프로젝트에서는 같은 Command ID를 자체 로직으로 교체하세요.
