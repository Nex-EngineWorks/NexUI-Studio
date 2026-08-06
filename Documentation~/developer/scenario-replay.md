# Scenario Replay

**상태: Experimental.** 코드로 작성한 시나리오를 **실행**하는 것까지입니다.
Recorder(사용자 조작 기록)는 없고, 입력은 실제 포인터가 아니라 이벤트 호출로 대체됩니다.

## 시나리오는 데이터입니다

```csharp
NexScenario.Named("PurchaseItem")
    .Find("store.item.purchase")
    .AssertVisible()
    .Click()
    .WaitUntil("Store.IsPurchasing", NexComparison.Equals, "false")
    .AssertState("Player.Currency", NexComparison.Equals, "500")
    .AssertNoErrors();
```

`elementId`가 아니라 [Automation ID](automation-id.md)로 찾습니다 — 화면을 정리해도 시나리오가 안 깨집니다.

Unity 타입을 전혀 쓰지 않는 순수 데이터라, 지금은 코드로 쓰지만 나중에 **기록된 파일에서 역직렬화**해도 같은 것이 됩니다.

## 기다리는 두 가지 방법

### 조건 대기 — `WaitUntil` (권장)

관찰할 조건이 있으면 이쪽입니다. 런너는 **poll을 세고, poll이 무엇인지는 호출자가 정합니다.**

```csharp
var runner = new NexScenarioRunner(scenario, world);
while (runner.MoveNext()) yield return null;      // PlayMode: poll = 1 프레임
Assert.IsTrue(runner.Result.Succeeded, runner.Result.ToString());
```

```csharp
var result = new NexScenarioRunner(scenario, world).RunToCompletion();   // EditMode: yield 없음
```

런너는 그대로이고 두 실행 모두 재현 가능합니다.

### 시간 대기 — `WaitForSeconds`

관찰할 것이 없는 경우(순수한 시각적 정착 등)를 위해 [Time Source](time-source.md)를 씁니다.

```csharp
var time = new NexManualTime();
var runner = new NexScenarioRunner(scenario, world, time);
```

`NexManualTime`을 넘기면 **실제로 기다리지 않고도** 시간이 흐른 것처럼 검증됩니다.
deadline은 첫 poll에서 **한 번만** 계산합니다 — poll마다 경과를 누적하면 poll 속도에 따라 흔들리고,
Timeline scrub으로 시계를 되감으면 아예 말이 안 되기 때문입니다.

가능하면 `WaitUntil`을 쓰십시오. 고정 시간 대기는 **머신 속도에 따라 통과 여부가 갈리고**,
그게 "로컬에선 되는데 CI에서만 깨지는 테스트"가 만들어지는 방식입니다.

## 실패는 예외가 아니라 진단입니다

첫 실패에서 멈추고, 이후 단계는 **시도하지 않고 `NotRun`으로 보고**합니다.
시나리오는 순서이기 때문입니다 — "구매 클릭"이 실패했다면 "영수증이 떴는지"는 독립된 두 번째 실패가 아닙니다.

리포트는 Flow Trace와 같은 모양입니다. 콘솔·CI 로그·버그 리포트에서 같은 방식으로 읽힙니다.

```text
✗ Scenario PurchaseItem
  ✓ Find(store.item.purchase)
  ✓ AssertVisible
  ✓ Click
  ✗ WaitUntil(Store.IsPurchasing Equals false)  (120 polls)  Waited 120 polls…; last saw 'true'.
  - AssertState(Player.Currency Equals 500)
  - AssertNoErrors
```

| 코드 | 의미 |
|---|---|
| `NEX-TEST-9001` | 그 Automation ID를 가진 요소가 화면에 없음 |
| `NEX-TEST-9002` | Find 없이 요소를 조작하려 함 |
| `NEX-TEST-9003` | 단언 불일치 |
| `NEX-TEST-9004` | WaitUntil이 poll 예산 안에 성립하지 않음 |
| `NEX-TEST-9005` | 시나리오 실행 중 화면이 오류를 냄 |

## 값 비교는 Interaction과 같은 코드입니다

`NexValueComparison`을 Interaction 조건과 **공유합니다.** 서로 다르게 답하면
게임에서는 발동하는 규칙이 자기 테스트에서는 실패하고, 그 이유를 아무도 볼 수 없습니다.

이 공유 과정에서 잠재 버그가 하나 드러났습니다: .NET의 `true.ToString()`은 `"True"`라
`"true"`로 쓴 저작값이 문자열 비교에서 어긋납니다. 이제 **bool을 명시적으로 먼저 처리**합니다.

## 클릭이 EventSystem을 거치지 않는 이유

`Button.onClick.Invoke()`를 직접 호출합니다. 의도적인 한계입니다 —
이렇게 하면 **화면 자신의 배선**(바인딩, Interaction 규칙, Command 핸들러)을 검사하되
카메라·Raycaster·물리·실제 포인터 위치에 의존하지 않습니다.
버튼이 실제 포인터로 닿는지는 다른 질문이고, 그것 때문에 실패한 시나리오는
**Scene 문제를 UI 로직 문제로 보고**하게 됩니다.

실제 포인터 주입은 Recorder 작업에 속하며, 같은 포트 뒤에 들어갑니다.

## 아직 하지 않는 것

- **Recorder.** 사용자 조작을 기록해 시나리오로 만드는 부분
- 시나리오 직렬화 (파일로 저장/불러오기)
- 실제 입력 장치 Replay (Mouse/Touch/Keyboard/Gamepad)
- Screenshot 비교, Flow Trace 비교, Performance Budget 단언
- `Open(Screen)` — 현재는 이미 만들어진 화면 인스턴스에 대해 실행합니다
- Motion 완료 대기
