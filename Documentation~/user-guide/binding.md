# Binding

Binding은 Element가 사용할 데이터나 Action을 문자열 Key로 연결하는 계약입니다. Studio는 Key를 Metadata에 저장하고 Preview Mock 값을 보여 줍니다. 실제 값 보관, 변경 알림과 Command 실행은 NexUI Runtime과 게임 코드가 담당합니다.

```text
Studio Element
→ DesignerBindingMetadata에 Key 저장
→ Runtime UIStateStore/UIActionResolver 등록
→ 상태 변경 또는 입력
→ Backend Handle Capability에 반영
```

## 채널

| Inspector | Metadata 필드 | 기대 값/역할 | Core Binder |
|---|---|---|---|
| Text Key | `textKey` | 문자열로 표시할 값 | `UITextBinder` |
| Value Key | `valueKey` | `float` 값 | `UIValueBinder` |
| Visibility Key | `visibilityKey` | `bool` 표시 여부 | `UIVisibilityBinder` |
| Class Key | `classKey` | `bool` Class 토글 조건 | `UIClassBinder` |
| Interactable Key | `interactableKey` | 입력 가능 여부 계약 | 전용 기본 Binder 없음, 프로젝트 연결 필요 |
| Command Key | `commandKey` | 클릭 Action 문자열 | `UICommandBinder` |

Image Object Binding과 Collection Binding은 현재 Inspector에서 독립적인 완성 채널로 제공되지 않습니다.

## Studio에서 설정

Element를 선택하고 Right Inspector의 Workflow를 **Connect**로 바꾼 뒤 **Binding** Section을 엽니다. Binding Picker는 프로젝트에서 발견한 `IBindableProperty<T>` 후보를 보여 주고 Command Picker는 기본 Key와 다른 Metadata에서 사용 중인 Key를 보여 줍니다. Picker 결과는 Runtime 등록을 자동 생성하지 않습니다.

Scenario 또는 Metadata의 Preview Text/Value/Visibility를 사용하면 게임 실행 없이 화면 상태를 점검할 수 있습니다. 이 값은 Runtime `UIStateStore`의 데이터가 아닙니다.

## Runtime 연결 예제

다음 코드는 이미 생성된 `IUISurface`에서 Element Handle을 찾았다는 전제의 최소 예제입니다. Namespace와 API는 현재 NexUI Core 코드 기준입니다.

```csharp
using emiteat.NexUI.Abstractions;
using emiteat.NexUI.State;

UIStateStore state = new UIStateStore();
UIActionResolver actions = new UIActionResolver();

IUIElementHandle title = surface.FindRequired("title");
IUIElementHandle health = surface.FindRequired("healthBar");
IUIElementHandle panel = surface.FindRequired("settingsPanel");
IUIElementHandle start = surface.FindRequired("startButton");

var textBinding = new UITextBinder();
textBinding.Bind(title, "player.name", state);

var valueBinding = new UIValueBinder();
valueBinding.Bind(health, "player.health", state);

var visibilityBinding = new UIVisibilityBinder();
visibilityBinding.Bind(panel, "settings.visible", state);

actions.Register("menu.start", () => StartGame());
var commandBinding = new UICommandBinder(actions);
commandBinding.Bind(start, "menu.start", state);

state.Set("player.name", "NexUI Player");
state.Set("player.health", 75f);
state.Set("settings.visible", true);
```

`UITextBinder`는 값을 `ToString()`으로 표시합니다. `UIValueBinder`는 `float`, Visibility/Class Binder는 `bool`을 Watch합니다. 타입이 다르면 `UIStateStore.Watch<T>` 콜백이 실행되지 않습니다.

화면을 닫거나 재사용할 때 Binder의 `Unbind()`를 호출해 구독을 해제하세요.

## Key가 없거나 타입이 다를 때

- `UIStateStore`에 아직 Key가 없으면 Watch는 초기 값을 전달하지 않습니다.
- 등록되지 않은 Action을 실행하면 Console에 `No action registered` Warning이 기록됩니다.
- Element Handle이 필요한 Capability를 제공하지 않으면 Binder가 Warning을 남기고 연결하지 않습니다.
- Studio는 프로젝트의 모든 실행 경로를 해석할 수 없으므로 Runtime Key 존재와 타입을 완전히 검증하지 못합니다.

## Command와 Interactive Preview

Interactive Preview는 Command Key와 Click을 Preview Log에 시뮬레이션합니다. Editor에서 게임 로직을 실행하지 않습니다. 실제 Command 검증은 Play Mode에서 수행하세요.

## Validation 범위

Studio는 Component가 지원하지 않는 Binding 채널, Button의 빈 Command와 끊어진 일부 참조를 검사합니다. 동적으로 생성되는 Key, DI Container에서 등록하는 Action과 실제 값 타입은 게임 코드 또는 PlayMode 테스트가 책임집니다.

관련 문서: [자주 사용하는 작업](common-workflows.md), [Scenario](preview-and-scenarios.md), [문제 해결](../reference/troubleshooting.md)
