# Runtime Override — "왜 저렇게 나와요?"

**상태: Experimental.** Compiled 경로의 Text·Visible 두 속성만 추적합니다.

## 이게 없으면 생기는 일

Source Map은 **"이 오브젝트가 어느 요소인가"** 에 답합니다. 그런데 그 다음 질문이 바로 나옵니다 —
문서에는 `Start`라고 적혀 있는데 화면에는 `Starting`이 보일 때, **누가 바꿨는지 알 방법이 없었습니다.**
작성자는 게임 코드를 뒤지는 수밖에 없었습니다.

```text
Root/Title.Text = 'Starting'
  set by Interaction 'rule-1' at 12.34s
  authored value was 'Start'.
```

## 구체적이지 않으면 쓸모없습니다

**"Interaction이 바꿨다"** 는 참이지만 규칙이 12개인 화면에서는 아무 도움이 안 됩니다.
그래서 기록은 **누가**(Source)와 **정확히 무엇이**(Origin)를 함께 남깁니다.

| Source | Origin에 들어가는 것 |
|---|---|
| `Binding` | 바인딩 키 (`Player.Score`) |
| `Interaction` | 규칙 id (`rule-1`) |
| `GameCode` | 호출자가 넘긴 사유 문자열 |
| `Authored` | 기록되지 않음 — 비교 기준이며 Compiled Program에 불변으로 존재 |

## 마지막 기록자만 남깁니다

전체 이력은 **더 좋아 보이지만 더 나쁩니다** — 긴 세션에서 무한히 자라고,
사람들이 실제로 묻는 것은 "지금 값이 뭐고 누가 그랬나"이지 "화면 열린 뒤 모든 쓰기를 나열해라"가 아닙니다.

## 게임 코드에서 바꿀 때

```csharp
runtime.SetText("stable-title", "Starting", reason: "purchase flow");
runtime.SetVisible("stable-hint", false, reason: "tutorial done");

Debug.Log(runtime.Explain("stable-title", NexOverrideProperty.Text));
```

`TextMeshProUGUI`에 직접 쓰는 것도 여전히 동작하고 화면에도 보입니다.
다만 **아무것도 기록되지 않아**, 작성자는 다시 "문서와 왜 다른지" 추측하는 상태로 돌아갑니다.
`reason`이 그 대신 디버거에 보이는 내용입니다.

## 비용

키는 노드 인덱스와 속성을 `long` 하나에 패킹합니다. 바인딩이 값을 밀어넣을 때마다 실행되는 경로라
튜플이나 문자열 조합으로 **할당이 생기면 안 됩니다.**

## 아직 하지 않는 것

- **Text·Visible 외의 속성.** 색·크기·클래스 등은 추적하지 않습니다
- **State Store 값의 출처.** `SetState`는 아직 기록하지 않습니다 — 노드 속성이 아니라 별도 축입니다
- v3-draft §73의 전체 Resolution 체인
  (`Authoring → Theme → State → Binding → Motion → Runtime Override`).
  Theme·Motion이 Compiled 경로에 아직 연결되지 않아 그 레이어들은 존재하지 않습니다
- Override 우선순위·수명(`Clear Override`는 있지만 정책은 없음)
- Editor의 Why Debugging UI — 현재는 문자열을 반환하는 API까지입니다
