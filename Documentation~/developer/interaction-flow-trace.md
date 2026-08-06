# Interaction Flow Trace와 Source Map

**상태: Experimental.** Compiled 화면(`NexScreenProgram` → `NexUGuiScreenBuilder`) 경로에서만 동작합니다.
기존 Prefab 저장 경로로 만든 화면은 추적되지 않습니다.

## Source Map

Source Map은 Diagnostics·Debugger·Performance 귀속이 모두 올라서는 기반입니다.
없으면 Runtime 관찰 결과를 "Node 47이 null이다"라고밖에 말할 수 없고, 이는 화면을 만든 사람에게 아무 의미가 없습니다.

체인은 두 조각으로 나뉩니다.

| 조각 | 타입 | 수명 | 내용 |
|---|---|---|---|
| Authoring ↔ Compiled | `NexSourceMap` | Compiled Asset과 동일 | `stableId` ↔ Node Index ↔ Authoring Path |
| Compiled ↔ Runtime | `NexRuntimeSourceMap` | 화면 Instance와 동일 | Node Index ↔ 실제 생성 객체 |

둘을 분리한 이유는 Compiled Asset이 불변이어야 하고, 하나의 Program을 여러 화면 Instance가 공유할 수 있어야 하기 때문입니다.

세 지점 중 어디서 출발해도 나머지를 찾을 수 있습니다.

```csharp
runtime.Find("stable-StartButton");            // Authoring id → 실제 GameObject
runtime.AuthoringPathOf(clickedGameObject);    // 실제 GameObject → "MainMenu/StartButton"
```

Runtime이 내보내는 모든 메시지는 후자의 형태를 써야 합니다. 그것만이 작성자가 알아보는 이름입니다.

## Flow Trace

### 출력 형식

```text
[14:03:21.145] TestScreen/Root/StartButton
→ Root/StartButton.Pointer.Click    ✓
→ Root/StartButton.Trigger.OnClick    ✓
→ Command.Game.Start.Dispatch    ✓
→ Handler.Invoke    ✓
✓ 3.82 ms
```

실패한 체인은 어디서 멈췄는지가 드러납니다.

```text
[14:07:02.881] Store/Root/PurchaseButton
→ Root/PurchaseButton.Pointer.Click    ✓
→ Root/PurchaseButton.Trigger.OnClick    ✓
→ Command.Store.Purchase.Dispatch    ✓
→ Handler.Invoke    ✗ NEX-RT-6001
✗ 0.41 ms
```

이미지 그래프가 아니라 텍스트인 이유: 버그 리포트에 붙여넣을 수 있고, 두 실행 결과를 diff할 수 있고,
Editor 창이 없는 Player 로그에서도 동작하기 때문입니다.

### Level

| Level | 기록 범위 |
|---|---|
| `Off` | 아무것도 기록하지 않고 **할당도 하지 않는다**. 출시 기본값 |
| `Summary` | 시작점과 결과만 |
| `Standard` | 입력에서 최종 Handler까지 모든 hop |
| `Verbose` | + 단계별 payload 요약, Binding 값 변화 |
| `Full` | + 평가되었으나 건너뛴 단계와 그 이유 |

### 계측 비용

측정 도구가 측정 대상을 바꾸면 그 도구는 없느니만 못합니다. 그래서:

- `Off`일 때 비용은 enum 비교 1회이며 할당은 0입니다.
- 켜져 있을 때 tracer 자체가 쓴 시간은 `NexFlowTrace.OverheadMs`로 따로 보고됩니다.
- `NEXUI_DISABLE_FLOW_TRACE`를 정의하면 기록 경로가 컴파일 단계에서 제거됩니다.

### 사용

```csharp
NexFlowTrace.Level = NexFlowLevel.Standard;
NexFlowTrace.AddSink(new NexFlowConsoleSink());
```

Runtime Debugger·Scenario Replay 비교용으로는 `NexFlowMemorySink`를 씁니다.
Sink가 예외를 던져도 추적 대상 상호작용은 중단되지 않습니다.

## 아직 하지 않는 것

- Binding Consumer 목록(`Player.Health 75 → 100` 아래 어떤 Element들이 갱신되었는지).
  현재는 Binding별 개별 Trace만 기록됩니다.
- Navigation / Motion / Async / Cancellation 단계.
- Editor 쪽 Flow Trace 뷰어. 현재는 Console과 Memory Sink뿐입니다.
- Scenario Replay와의 Trace 비교.
