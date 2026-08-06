# Error Code Catalog

**상태: Experimental.** 아래 코드는 Compiler / Publish / Compiled Runtime 경로에서만 사용됩니다.
기존 Validation·Save Report 경로는 아직 이 체계로 옮기지 않았습니다.

코드 형식은 `NEX-{SUBSYSTEM}-{NUMBER}`이며 **영구적**입니다.
검사를 폐지하더라도 번호를 다른 용도로 재사용하지 않습니다. 코드가 사용자 버그 리포트와 저장된 Build Report에 남기 때문입니다.

Subsystem 번호대:

| 대역 | 영역 |
|---|---|
| `DOC` 1xxx | Authoring Document 구조 |
| `SER` 2xxx | Serialization |
| `CMP` 3xxx | Compiler |
| `BND` 4xxx | Binding |
| `LAY` 5xxx | Layout |
| `RT` 6xxx | Runtime |
| `ACC` 7xxx | Accessibility |
| `BLD` 8xxx | Build / Publish |
| `TEST` 9xxx | Scenario Replay |

## 코드

| 코드 | 기본 Severity | 의미 | 해결 |
|---|---|---|---|
| `NEX-DOC-1001` | Error | 화면에 screen id가 없다 | Metadata Asset에 screen id를 설정. Runtime이 화면을 여는 키입니다 |
| `NEX-DOC-1002` | Error | Element에 element id가 없다 | Hierarchy 패널에서 이름을 지정 |
| `NEX-DOC-1003` | Error | 같은 화면에서 element id가 중복 | 하나를 rename. 중복 id는 Binding을 모호하게 만듭니다 |
| `NEX-DOC-1004` | Error | 이 화면에 없는 부모를 가리킨다 | 재부모화하거나 고아 Element를 삭제 |
| `NEX-DOC-1005` | Error | 부모 관계가 사이클을 이룬다 | Hierarchy에서 순환을 끊습니다 |
| `NEX-DOC-1006` | Warning | Element가 하나도 없이 컴파일됨 | Element를 추가하거나 화면을 삭제 |
| `NEX-DOC-1007` | Error | 같은 화면에서 Automation ID가 중복 | 하나를 rename. 중복이면 테스트 조회가 먼저 컴파일된 쪽을 반환해 **틀린 것을 검사하며 통과**합니다 |
| `NEX-CMP-3001` | Error | 화면 컴파일 실패(상위 요약) | Cause Chain을 따라 근본 원인으로 이동. 아무것도 Publish되지 않았습니다 |
| `NEX-CMP-3002` | Error | Element Type이 Registry에 없다 | 팔레트에서 다시 생성하거나 Component Type을 등록 |
| `NEX-CMP-3003` | Warning | 대상 Backend에 대응 표현이 없다 | Node는 Panel로 컴파일됩니다. Backend를 바꾸거나 다른 Component로 교체 |
| `NEX-CMP-3004` | Error | Compiler에 Metadata Asset이 전달되지 않았다 | Studio에서 화면을 연 뒤 컴파일 |
| `NEX-BND-4001` | Warning | 클릭할 수 없는 Element에 Command Binding | Button으로 옮기거나 제거. 현재 위치에서는 절대 실행되지 않습니다 |
| `NEX-BND-4002` | Warning | 텍스트를 그리지 않는 Element에 Text Binding | Label/Button으로 옮기거나 제거. Interaction의 `SetText`가 텍스트 없는 대상을 향할 때도 동일 코드 |
| `NEX-BND-4003` | Error | Interaction Action이 이 화면에 없는 Element를 대상으로 함 | 존재하는 Element를 고르거나 Action 삭제. 규칙은 실행될 수 없습니다 |
| `NEX-BND-4004` | Warning | Element가 발생시킬 수 없는 Trigger를 사용 | `OnClick`은 클릭 가능한 Element가 필요합니다. Button으로 옮기거나 Trigger 변경 |
| `NEX-BND-4005` | Warning | Interaction 규칙에 Action이 없음 | Action을 추가하거나 규칙 삭제. 현재는 조건만 평가하고 아무것도 하지 않습니다 |
| `NEX-BND-4006` | Error | Interaction Action에 필수 값이 비어 있음 | Command id·State key·대상 Element 중 필요한 것을 채우세요 |
| `NEX-BND-4007` | Warning | 규칙이 도달할 수 없는 phase에서 대기 | Capture/Bubble은 **자식이 이벤트를 발생시켜야** 하고, 전파하는 Trigger는 클릭 계열뿐입니다 |
| `NEX-RT-6001` | Warning | Runtime에서 Handler 없는 Command가 발생 | 부트스트랩에서 `NexCommandRouter.Register(commandId, handler)` |
| `NEX-RT-6002` | Error | Command Handler가 예외를 던졌다 | Detail의 inner exception 확인. 해당 단계에서 상호작용이 중단되었습니다 |
| `NEX-RT-6003` | Error | 다른 Compiler Version이 만든 Program | Studio에서 화면을 재컴파일 |
| `NEX-RT-6004` | Error | Interaction Action에 필요한 Runtime 서비스가 없음 | 화면 생성 시 State Store·Screen Surface를 전달하세요. 규칙의 나머지 Action은 계속 실행되었습니다 |
| `NEX-BND-4008` | Warning | 되돌려 쓸 수 없는 요소에 양방향 Binding을 걸었다 | 단방향으로 바꾸거나 입력을 받는 컨트롤을 사용하세요. 읽기는 계속 동작하고 쓰기 절반만 출처가 없습니다 |
| `NEX-BND-4009` | Warning | 값을 담을 수 없는 노드에 Value Binding을 걸었다 | 컴파일 노드 종류(Panel/Image/Label/Button)에는 스칼라가 없습니다. Binding은 Program에 보존되며, 값을 가지는 컨트롤을 쓰는 순간 동작합니다 |
| `NEX-BND-4010` | Suggestion | 설정되지 않은 Binding에 Converter만 지정했다 | Converter 키를 지우거나 대상 Binding을 설정하세요. 현재는 호출되지 않습니다 |
| `NEX-ACC-7001` | Warning | 조작 가능한 요소가 보조기술에 아무것도 알리지 못한다 | Accessibility Label을 지정하거나 보이는 텍스트를 넣으세요. 아이콘만 있는 버튼은 스크린 리더로 도달할 수 없습니다 |
| `NEX-ACC-7002` | Suggestion | 의미 있는 이미지로 표시된 요소에 Label이 없다 | 이미지가 전달하는 내용을 서술하거나, Role을 None으로 두어 장식으로 건너뛰게 하세요 |
| `NEX-BLD-8001` | Error | 컴파일 결과를 디스크에 쓰지 못했다 | 이전 Asset은 그대로입니다. 출력 폴더 쓰기 권한 확인 |
| `NEX-BLD-8002` | Error | Publish 경로가 비었거나 프로젝트 밖 | `Assets/` 아래 출력 경로를 설정 |
| `NEX-TEST-9001` | Error | 시나리오가 찾는 Automation ID가 화면에 없음 | 요소의 id를 확인하거나, 시나리오가 다른 화면을 향하고 있는지 확인 |
| `NEX-TEST-9002` | Error | Find 없이 요소를 조작하려 함 | 조작 단계 앞에 Find 단계를 두세요 |
| `NEX-TEST-9003` | Error | 시나리오 단언 불일치 | Detail에 기대값과 실제값이 모두 있습니다 |
| `NEX-TEST-9004` | Error | WaitUntil이 poll 예산 안에 성립하지 않음 | 조건을 확인하거나, 실제로 더 느린 작업이면 예산을 올리세요 |
| `NEX-TEST-9005` | Error | 시나리오 실행 중 화면이 오류를 냄 | 수집된 Diagnostic을 확인하세요 |

## Cause Chain

Diagnostic은 중첩됩니다. 사용자가 먼저 보는 것은 가장 바깥(무슨 일이 실패했는가)이고,
`Cause`를 따라가면 실제로 고쳐야 할 것이 나옵니다.

```text
NEX-CMP-3001
Screen 'MainMenu' failed to compile.

Cause:
  NEX-DOC-1003: Element id 'Title' is used more than once on this screen.
  at MainMenu/Title

Fix:
  Rename one of them. Duplicate ids make bindings ambiguous at runtime.
```

## 코드 추가 규칙

코드는 `NexDiagnosticCodes`의 `All` 테이블에 항목이 있을 때만 존재합니다.
테이블에 없는 코드가 나타나면 그것은 미문서화 기능이 아니라 **버그**입니다.
이 문서는 그 테이블과 1:1로 유지되어야 합니다.
