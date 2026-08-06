# Automation ID

**상태: Experimental.** Compiled 경로에서만 동작합니다. Scenario Replay(§55)는 아직 없고,
현재는 **찾는 것까지**입니다.

## 왜 elementId로는 안 되는가

`elementId`는 저작 편의를 위한 이름이고, 화면을 정리하다 보면 계속 바뀝니다.
테스트가 그걸 키로 잡으면 **누군가 이름을 정리할 때마다 테스트가 깨집니다.**

`stableId`는 rename을 견디지만 GUID입니다. `Find("8f2a1c…")`는 의도를 설명하지 못합니다.

Automation ID는 그 사이입니다 — **사람이 읽을 수 있고, 의도적으로만 바뀝니다.**

```text
Display Name : 구매 버튼        ← 바뀜
Element ID   : PurchaseButton  ← 바뀜
Automation ID: store.item.purchase   ← 테스트에 대한 약속
Node ID      : stable GUID     ← 안 바뀌지만 읽을 수 없음
```

## Role은 새로 만들지 않았습니다

`AccessibilityRole`을 그대로 씁니다. **테스트가 "그 버튼"을 찾는 것과 스크린 리더가 "버튼"이라고
읽는 것은 같은 질문**이라, 두 개의 role 어휘를 두면 서로 어긋나고 하필 저자가 채우지 않은 쪽이 필요해집니다.

## 사용

**저작** — Inspector의 Accessibility 섹션. Role과 나란히 있습니다.

> 이 섹션은 이제 **항상 표시됩니다.** 이전에는 라벨이나 Role이 채워진 뒤에만 나타났는데,
> 그러면 아무것도 채워져 있지 않은 요소는 필드에 도달할 방법이 없어 결국 아무도 채우지 않습니다.

**런타임 조회**

```csharp
var button = runtime.FindByAutomationId("store.item.purchase");
var items  = runtime.FindByRole(AccessibilityRole.ListItem);   // 컴파일 노드 순서
var all    = runtime.AutomationIds;                            // 테스트 측 점검용
```

`FindByRole`의 순서는 **컴파일 노드 순서**, 즉 문서의 위에서 아래 순서입니다.
"세 번째 항목"을 집는 테스트가 매 실행 같은 것을 얻어야 하기 때문에 결정적이어야 합니다.

## 중복은 컴파일 에러입니다

같은 화면에서 automation id가 겹치면 `NEX-DOC-1007`로 **Publish가 막힙니다.**
겹친 채로 두면 조회가 "먼저 컴파일된 쪽"을 반환하는 동전 던지기가 되고,
그 테스트는 실패하는 대신 **틀린 것을 검사하며 통과합니다.**

저작 중에는 Studio Validation이 `duplicate-automation-id`로 같은 것을 즉시 알려줍니다.
이 검사는 **요소 간 규칙**이라 `ValidateElement`가 아니라 요소 루프에 있습니다 —
유일성은 화면 전체의 성질이라 요소 하나만 봐서는 답할 수 없습니다.

## 조회 비용

`IndexOfAutomationId`는 선형 스캔입니다. 딕셔너리를 만들지 않은 이유:
automation id는 **테스트가 조회당 한 번** 쓰는 것이고 화면 노드는 수십 개인데,
맵을 미리 만들면 **모든 출시 빌드가 메모리를 냅니다** — 어떤 프레임도 하지 않는 일을 빠르게 하려고요.
컴파일러가 유일성을 보장하므로 첫 일치가 유일한 일치입니다.

## 아직 하지 않는 것

- **Scenario Recorder / Replay (§55).** 이게 Automation ID의 본래 목적지입니다
- Find by Binding / Find by Component
- Screen을 넘나드는 전역 조회 (현재는 화면 인스턴스 단위)
- Build Stripping — automation id는 현재 출시 빌드에도 포함됩니다.
  Replay를 빌드에서 돌릴 수 있어야 하므로 무조건 제거하면 안 되고, Feature 등급으로 다뤄야 합니다
