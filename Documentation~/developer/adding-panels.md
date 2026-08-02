# Panel 추가하기

Panel은 `VisualElement`로 만들고 생성자에서 `NexUIDesignerContext`를 전달받습니다. 표시 로직만 두고 검증·파일 I/O·검색은 Service로 분리하세요.

```csharp
using UnityEngine.UIElements;

namespace emiteat.NexUI.Studio.Editor.UI.Panels
{
    public sealed class ExamplePanel : VisualElement
    {
        public ExamplePanel(NexUIDesignerContext context)
        {
            AddToClassList("nexui-example-panel");
            var subscriptions = new ContextBoundSubscriptions(this);
            subscriptions.Add(h => context.CanvasChanged += h,
                h => context.CanvasChanged -= h, Refresh);
        }

        private void Refresh() { }
    }
}
```

Shell에 Panel을 연결하고 `NexUIDesigner.uss`에 `nexui-` 접두 Class를 추가합니다. 생성자 람다로 정적/Context 이벤트를 직접 구독하지 말고 Attach/Detach 수명주기를 사용합니다. Panel에서 `AssetDatabase.Refresh`, Backend 분기와 장시간 작업을 수행하지 마세요.

