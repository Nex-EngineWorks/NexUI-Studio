# Time Source

**상태: Experimental.** 추상화와 Scenario 연결까지 되어 있습니다.
Motion·Interaction Delay는 아직 이 시계를 쓰지 않습니다.

## 왜 `Time.deltaTime`을 직접 쓰지 않는가

기다리는 모든 것 — 지연된 Interaction Action, Motion, Scenario 단계 — 이 엔진 시계를 직접 읽으면
엔진 시계가 표현할 수 없는 상황에서 전부 틀립니다.

| 상황 | 필요한 시계 | 직접 읽으면 |
|---|---|---|
| `timeScale = 0`인 일시정지 메뉴가 열리는 애니메이션 | Unscaled | **전환이 영원히 안 끝남** — 이 버그의 고전적 형태 |
| Timeline scrub으로 Motion 앞뒤로 끌기 | 도구가 직접 지정 | 스크럽이 불가능 |
| Scenario를 두 번 돌려 같은 결과 | 수동 | 매번 다른 타이밍, flaky 테스트 |

## 구현체

| 타입 | 용도 |
|---|---|
| `NexScaledTime` | 게임에 속한 UI — 데미지 숫자, 콤보 게이지. 게임이 멈추면 같이 멈춰야 함 |
| `NexUnscaledTime` | 메뉴. **기본값** |
| `NexManualTime` | 테스트·Replay·Timeline scrub. 호출자가 직접 전진 |

기본값이 Unscaled인 이유: NexUI 콘텐츠 대부분이 메뉴 성격이고, **멈춘 메뉴는 고장난 메뉴**입니다.
반대로 HUD가 일시정지 중 Unscaled로 도는 것은 약간 어색할 뿐입니다.
동의하지 않는 프로젝트는 부트스트랩에서 `NexTime.Default`를 한 번 교체하면 됩니다.

## 왜 `double`인가

`float` 누산기는 몇 시간짜리 세션에서 의미 있는 정밀도를 잃습니다.
**"오래 켜두면 UI가 끊긴다"** 는 추적하기 지독히 괴로운 버그이고, 애초에 만들지 않는 편이 낫습니다.

## `Now`는 단조 증가, `SeekTo`만 예외

`Advance`는 음수를 무시합니다. 시간을 되감을 수 있는 유일한 연산은 `SeekTo`이고,
그건 **재생 헤드를 왼쪽으로 끄는 것**이 그런 의미이기 때문입니다.

그래서 기다리는 코드는 **경과 시간을 누적하지 말고 deadline을 저장해야 합니다.**
`NexScenarioRunner.PollSleep`이 그렇게 되어 있습니다.

## 사용

```csharp
// 부트스트랩에서 한 번
NexTime.Default = new NexScaledTime();

// 테스트에서
var time = new NexManualTime();
var runner = new NexScenarioRunner(scenario, world, time);
runner.MoveNext();
time.Advance(2.0);        // 실제로 기다리지 않음
runner.RunToCompletion();
```

## 이 시계를 쓰는 곳

- **Scenario `WaitForSeconds`** — [Scenario Replay](scenario-replay.md)
- **Interaction `Delay`** — [Interaction Authoring](interaction-authoring.md)

## 아직 하지 않는 것

- **Motion 재생.** `UIMotionClipPlayer`는 여전히 엔진 시계를 직접 읽습니다
- Fixed Time, Replay Time 전용 구현체
- 시간 배속(slow motion) 데코레이터
