# 고급 기능 샘플

이 샘플은 스튜디오 메타데이터가 런타임 코드에 에디터 전용 로직을 섞지 않고도 더 큰 제작 흐름을 지원하는 방식을 보여줍니다.

## 예시 흐름

1. 화면과 대응되는 `DesignerMetadataAsset`을 만듭니다.
2. element id, binding, localization key, prompt glyph 참조를 추가합니다.
3. 기기별 레이아웃 메모가 필요하면 screen variant 또는 responsive rule을 사용합니다.
4. 커밋 전에 validation을 실행합니다.
5. 큰 변경을 검토할 때 profiling, contrast, cleanup, snapshot, diff 도구를 사용합니다.

## 유용한 메뉴

- `Tools/NexUI/Studio`
- `Tools/NexUI/Screen/Rebuild Preview`
- `Tools/NexUI/Screen/Validate Screen`
- `Tools/NexUI/Screen/Save Screen`
- `Tools/NexUI/QA/Run Advanced Validation`
- `Tools/NexUI/QA/Run Snapshot Tests`
- `Tools/NexUI/QA/Export Agent Manifest`

## 코드 예시

```csharp
await NexUI.OpenAsync("Inventory", new UIOpenArgs { variantId = "ControllerMode" });

string title = table.Resolve("pause.title", "ko-KR");

var glyph = glyphTable.Find("Submit", UIPromptDevice.Xbox);

float ratio = ThemeContrastChecker.ContrastRatio(foreground, background);
```

패키지 사용법은 [Sample 둘러보기](../../Documentation~/getting-started/sample-tour.md)부터 확인하세요.
