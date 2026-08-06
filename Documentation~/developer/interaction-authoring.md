# Interaction Authoring

**상태: Experimental.** Compiled 경로(`NexScreenProgram` → `NexUGuiScreenBuilder`)에서만 동작합니다.
기존 Prefab 저장 경로로 만든 화면에는 적용되지 않습니다.

## 무엇을 푸는가

이 기능이 없으면 "버튼을 누르면 확인 팝업이 뜬다" 같은 결정이 전부 게임 코드로 넘어갑니다.
디자이너가 화면에서 결정할 수 있는 것을 프로그래머가 코드로 받아 적는 비용 — NexUI가 없애려는 바로 그 비용입니다.

```text
When (Trigger)
→ Only if (Condition)
→ Do (Actions)
```

## 현재 범위

명세의 Trigger 16종·Condition 8종·Action 16종 중, **끝까지 동작하는 것만** 넣었습니다.
Trigger 하나를 추가한다는 것은 모든 Backend가 그것을 발생시켜야 한다는 뜻이라 값이 싸지 않습니다.

| 축 | 지원 | 미지원 |
|---|---|---|
| Trigger | `OnClick`, `OnShow`, `OnHide` | Hover/Focus/Drag/Drop/ValueChanged/LongPress 등 |
| Condition | State Key 비교 (`Equals` / `NotEquals` / `GreaterThan` / `LessThan`) | Platform·Input Device·Permission·Async·Custom |
| Action | `ExecuteCommand`, `SetState`, `SetVisible`, `SetText`, `Delay` | Navigate·RunMotion·Sound·Haptic·Branch·Cancel |

**별도의 `Sequence` Action은 없습니다.** 규칙이 이미 순서이기 때문입니다 —
Action들은 순서대로 실행되고, "버튼을 반짝이고, 기다렸다가, 다음 화면을 연다"에 부족했던 건 `Delay` 하나뿐이었습니다.

## Delay — 규칙이 시간에 걸쳐 실행됩니다

`Delay`를 만나면 그 뒤 Action들은 **파킹**되었다가 시간이 지난 뒤 재개됩니다.
시간은 [Time Source](time-source.md)로 읽으므로 Pause·Timeline Scrub·결정론적 Replay에서 모두 올바릅니다.

이건 Interaction 엔진이 **프레임을 넘어 상태를 갖게 되는** 변경이라, 고유한 실패 모드가 생깁니다.
각각을 어떻게 막았는지:

| 위험 | 대응 |
|---|---|
| **화면이 사라진 뒤 액션이 실행됨** | `Dispose`가 `CancelPending()` 호출. 가장 나쁜 실패 — 사용자가 이미 다른 화면으로 갔는데 **그 화면의 버그처럼 보입니다** |
| 재개가 두 번 실행됨 | 재개 전에 대기 목록에서 제거. 테스트로 고정 |
| 재개 중 새 작업이 생겨 목록이 변형됨 | 실행 전에 만기 항목을 **먼저 수집**한 뒤 순회 |
| 안 쓰는 화면도 매 프레임 비용 | `HasDelays()`가 false면 **Ticker를 아예 만들지 않음** |

클릭을 두 번 하면 규칙이 **두 번 시작**되어 각각 독립적으로 파킹됩니다.
재개 순서는 deadline 순입니다.

Compiler는 **Delay로 끝나는 규칙**을 경고합니다 — 기다린 뒤 아무것도 하지 않는 것은 언제나 실수입니다.

## Event Propagation

규칙은 **어느 단계에서 들을지**를 고릅니다. 기본값은 `Target` — phase가 생기기 전 모든 규칙의 동작이라,
이 필드가 추가돼도 이미 저작된 화면은 하나도 바뀌지 않았습니다.

```text
클릭이 Button에서 발생
→ Capture:  Root → Panel        (바깥에서 안으로, Target 이전)
→ Target:   Button
→ Bubble:   Panel → Root        (안에서 바깥으로)
```

값을 하는 건 `Bubble`입니다. **List가 어떤 item이 눌려도 반응**하거나, Modal이 내부 아무 곳이나
눌리면 닫히는 것을, 자식마다 같은 규칙을 복사하지 않고 표현합니다.

`Stop after this`를 켜면 그 뒤로는 아무도 이벤트를 보지 못합니다.
Bubble이 만들어내는 문제의 탈출구입니다 — **배경 클릭 시 닫히는 Modal이, 내부 버튼을 눌렀을 때는
닫히면 안 되는** 경우. 버튼 규칙이 이벤트를 claim하면 Modal은 듣지 못합니다.

Bubble은 가장 **안쪽 조상부터** 순회합니다. 그래야 "가까운 쪽이 먼저 가져간다"가 성립하고
`Stop`이 의미를 가집니다.

### 전파하지 않는 것

`OnShow`/`OnHide`는 전파하지 않습니다. 이건 포인터가 계층을 통과하는 사건이 아니라
**그 노드 자신의 생명주기**입니다. Bubble시키면 조상의 규칙이 자식이 나타날 때마다 한 번씩 실행되는데,
그건 누구의 의도도 아닙니다.

Compiler가 이걸 저작 시점에 잡습니다 — 전파하지 않는 Trigger에 Capture/Bubble을 걸거나,
**자식이 없는 요소**에 Capture/Bubble을 걸면 `NEX-BND-4007`로 규칙이 삭제됩니다.

반대로 **Panel에 걸린 Bubble OnClick 규칙은 정당합니다.** Panel 자신은 클릭될 수 없지만
아래의 Button이 발생시킨 이벤트가 올라오기 때문입니다. `NEX-BND-4004`(클릭 불가 요소)는
Target phase에만 적용됩니다.

## 컴파일 단계에서 확정되는 것

Runtime이 단순한 것은 Compiler가 미리 다 해두기 때문입니다.

| 저작 시점 | 컴파일 후 |
|---|---|
| `targetElementId = "Title"` | `TargetNodeIndex = 1` — Runtime은 이름 조회를 하지 않음 |
| `conditionValue = "10"` | `ConditionNumber = 10, ConditionIsNumeric = true` — 클릭 경로에서 파싱 없음 |
| Label 위의 `OnClick` 규칙 | `NEX-BND-4004` 후 **삭제** — 절대 발생하지 않을 규칙은 Runtime에 도달하지 않음 |
| 존재하지 않는 Target | `NEX-BND-4003` **Error** — 화면이 Publish되지 않음 |

**규칙은 통째로 버려집니다.** 4개 Action 중 3개만 컴파일하면 절반만 동작하는 화면이 되고,
그건 아예 동작하지 않는 것보다 진단하기 어렵습니다.

## 값 비교 규칙

```text
저작값이 숫자이고 실제값도 숫자로 읽히면  → 숫자 비교
그 외                                    → 문자열 비교
```

문자열에 대한 `GreaterThan` / `LessThan`은 **false를 반환합니다.** 사전순으로 답하지 않습니다.
문자열 크기 비교는 거의 항상 저작 실수이고, 조용히 답해주면 그 실수가 "가끔 안 되는 규칙"으로 숨습니다.

## Runtime 구조

```text
NexInteractionRuntime
├─ INexStateAccess    ← UIStateStore를 감싼 NexStateStoreAccess
└─ INexScreenSurface  ← Source Map으로 노드를 찾는 NexUGuiScreenSurface
```

포트 두 개뿐이라 **GameObject·Canvas·프레임 루프 없이 테스트할 수 있습니다.**
`NexInteractionRuntimeTests`가 가짜 포트로 엔진 전체를 검증하는 것이 그 이유입니다.

엔진은 예외를 던지지 않습니다. 실행할 수 없는 Action은 Diagnostic을 남기고 **나머지 Action은 계속 실행**됩니다.
하나의 잘못된 Action이 화면의 나머지 동작까지 가져가서는 안 됩니다.

## 비용

규칙이 없는 화면은 아무 비용도 내지 않습니다.

- Compiler가 규칙을 만들지 않으면 `Interactions.IsEmpty`
- Builder는 `HasAnyTrigger(OnClick)`가 false면 **리스너를 아예 등록하지 않음**
- Feature Manifest에 `nexui.interaction`이 올라가지 않음 → Build Report에서 제외 근거가 됨

`OnShow`는 계층 전체가 만들어진 뒤 한 번만 발생합니다. 형제 요소를 숨기는 규칙이
그 형제가 아직 생성되는 중에 실행되면 안 되기 때문입니다.

## Flow Trace 예시

```text
[14:03:21.145] MainMenu/Root/StartButton
→ Root/StartButton.Trigger.OnClick    ✓
→ Condition.Menu.Mode Equals    ✓  Ready vs Ready
→ Command.Game.Start.Dispatch    ✓
→ Handler.Invoke    ✓
✓ 1.24 ms
```

조건이 실패하면 그것도 기록됩니다(`Full` 레벨).
**"조건이 false였다"는 것이 상호작용 버그의 가장 흔한 답**이기 때문에 버리지 않습니다.

```text
→ Condition.Player.Level GreaterThan    - Skipped  10 vs 50 → false
```

## 아직 하지 않는 것

- 규칙 간 우선순위. 같은 노드·같은 phase의 규칙은 저작 순서대로 **전부** 실행됩니다
- Undo 가능한 Auto Fix (진단은 나오지만 자동 수정은 없음)
- UI Toolkit Backend
- Studio Preview에서의 규칙 시뮬레이션 — 현재는 Player에서만 실행됩니다

## 관련 문서

- [Compiler Pipeline](compiler-pipeline.md)
- [Interaction Flow Trace와 Source Map](interaction-flow-trace.md)
- [Error Code Catalog](../reference/error-code-catalog.md)
