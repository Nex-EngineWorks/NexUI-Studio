# Incremental Processing

**상태: Experimental.** 추적 기반은 동작하지만, **이를 소비해 실제로 일을 줄이는 곳은 아직 Compile Cache뿐입니다.**
Validation·Preview·Component 전개는 여전히 매번 전체를 다시 계산합니다.

## 문제

기존 구조에서 문서 변경은 boolean 하나였습니다.

```csharp
_expansionValid = false;   // 뭔가 바뀜
SetDirtyState(true);
CanvasChanged?.Invoke();
Validate();                // ← 전체 화면 재검증
```

키를 한 번 누를 때마다 Validation·Preview·평탄화된 Component 트리가 전부 처음부터 다시 계산됩니다.
화면이 커질수록 저작이 느려지는 구조적 원인이 여기입니다.

## 두 조각

### `NexDocumentRevision` — 무엇이 바뀌었는가

```csharp
var mark = context.Changes.Revision;   // 작업 끝에 기록
...
var changes = context.Changes.Since(mark);   // 다음 작업 시작에 질의
```

**과하게 보고하되, 부족하게 보고하지 않습니다.** 이 규칙 위에 다른 것을 올릴 수 있게 하는 유일한 근거입니다.

| 상황 | 답 |
|---|---|
| 요소 하나의 속성 변경 | 그 요소만 |
| 추가·삭제·재부모화 | **전체** (blast radius를 계산하려 들지 않음) |
| 귀속 불가능한 변경 | **전체** |
| 보존 한계(256개)를 넘어 뒤처진 소비자 | **전체** |

일을 너무 많이 하는 소비자는 느릴 뿐이고, 너무 적게 하는 소비자는 **틀립니다.** 둘 중 하나만 감수할 가치가 있습니다.

키는 `elementId`가 아니라 **`stableId`** 입니다. 이름 변경은 평범한 속성 변경이어야 하는데,
`elementId`로 키를 잡으면 rename이 모든 소비자에게 "삭제 + 삽입"으로 보입니다.

### `NexDependencyGraph` — 그래서 무엇이 영향받는가

간선은 **의존 대상 → 의존하는 것들** 방향입니다. 모든 질문이 그 방향이기 때문입니다 —
"이걸 바꿨는데 뭐가 낡았지?"와 "이걸 지우면 뭐가 깨지지?"는 같은 순회입니다.

현재 기록하는 의존:

| 간선 | 왜 |
|---|---|
| 부모 → 자식 | 부모 이동·크기 변경이 자식을 움직임. Compiler 배치 패스가 부모 rect를 읽음 |
| Interaction 대상 → 규칙 소유자 | 대상 rename/삭제가 규칙을 깨뜨림 (`NEX-BND-4003`) |
| Focus 링크 대상 → 링크한 요소 | 탐색이 깨짐 |

Component Definition은 별도 에셋이라 **에셋 단위 그래프**가 필요하고, 이건 그게 아니라서 제외했습니다.

**증분 유지가 아니라 매번 재구축합니다.** 빌드는 요소 수에 선형이고,
편집마다 동기화해야 하는 그래프는 제2의 진실 원본이 됩니다 — stale dependency 버그가 정확히 그렇게 생깁니다.

```csharp
var graph = NexDependencyGraph.Build(context.Metadata);
var affected = graph.Affected(changes);   // null이면 "전체 다시"
```

## 지금 실제로 절약되는 것

### 1. Publish

`NexScreenPublisher.Decide`가 ContentHash를 비교해 동일하면 디스크 쓰기·Import·Report를 건너뜁니다.
[Compiler Pipeline](compiler-pipeline.md) 참조.

### 2. Validation 호출 합치기 — 시도했다가 되돌림

드래그는 마우스가 움직일 때마다 rect 편집을 커밋하고, 그때마다 **전체 Validation이 동기 실행**됩니다 —
Backend Prefab을 로드해 GameObject를 순회하는 검사까지 포함해서요. 이걸 Editor tick당 1회로 합치면
드래그 한 번이 60회에서 1회가 됩니다.

**구현했다가 되돌렸습니다.** `DesignerUndoConsistencyTests.UndoBackToBaseline`이 깨졌습니다 —
그 테스트가 **172초** 걸리고(스위트에서 두 번째로 느린 테스트가 4.5초) undo 직후 요소가 사라져
NullReference로 실패했습니다.

**메커니즘을 설명하지 못했습니다.** 에디터에서 작업을 지연시키면 undo 시스템·도메인 리로드·
테스트 러너 자체의 pump와 상호작용하는데, 아무도 설명하지 못하는 타이밍 변경을
**모든 문서 편집이 지나가는 경로**에 둘 수는 없습니다.

되돌린 것은 **패스 횟수 감소**뿐입니다. 아래의 요소별 이슈 캐시는 그대로라 **패스 하나하나의 비용**은 여전히 줄어듭니다.
undo와의 상호작용을 이해하고 테스트로 덮은 뒤에 다시 넣어야 합니다.

### 3. Validation 규칙 분리 (범위 축소의 전제)

`DesignerValidationService`의 요소 루프는 두 종류의 규칙을 섞어 돌리고 있었습니다.

| 종류 | 규칙 | 좁힐 수 있나 |
|---|---|---|
| **요소 단위** | `invalid-element-id`, `missing-backend-element`, ElementDetails, ComponentProperties, ComponentParts, PropertyParity | 가능 — 요소 하나만 읽음 |
| **문서 단위** | `duplicate-element-id`, `duplicate-stable-id` | 불가 — 전체 집합이 있어야 답이 나옴 |

전자를 `DesignerValidationService.ValidateElement(element, backend, screenId, backendNames, issues)`로 분리했습니다.
**호출 순서를 그대로 두어 이슈 목록은 이전과 완전히 동일합니다** — 순수한 구조 분리입니다.

이 분리가 성립하려면 `ValidateElement`가 정말 요소 하나만의 함수여야 합니다.
다른 요소에 의존하기 시작하면 범위를 좁힐 때 이슈가 조용히 사라집니다.
`DesignerValidationElementScopeTests`가 그 성질을 직접 검사합니다 —
특히 "같은 id를 가진 두 요소를 따로 검사해도 중복이 보이지 않는다"가 그 확인입니다.

### 4. Validation 범위 축소

> **⚠ 실행 검증 전입니다.** 아래 동작은 컴파일과 단위 테스트 작성까지만 되어 있고
> Unity에서 한 번도 실행되지 않았습니다. 이슈가 잘못 재사용되면 **실제 오류가 화면에서 사라지는**
> 형태로 나타나므로, Validation 패널을 눈으로 확인하기 전에는 신뢰하지 마십시오.

`NexValidationCache`가 요소별 이슈를 패스 사이에 보관합니다.

```text
변경 집합(Changes.Since) → 해당 요소의 캐시만 폐기
→ 전체 패스 실행
   ├─ 변경 없는 요소: 이전 이슈 재사용
   └─ 변경된 요소: 요소 단위 규칙 재실행
→ 문서 단위 규칙은 언제나 전부 재실행
```

**의존성 그래프를 쓰지 않습니다.** 요소 단위 규칙은 정의상 그 요소만의 함수라
부모나 자신을 참조하는 규칙이 바뀌어도 그 요소의 이슈는 변하지 않습니다.
그래프로 dirty 집합을 넓히면 멀쩡한 캐시를 버리게 됩니다. 그래프는 결과가 진짜로 요소를 넘나드는 소비자용입니다.

캐시가 통째로 비워지는 조건 — **환경이 움직이면 전부 폐기**합니다:

| 조건 | 왜 |
|---|---|
| 대상 Backend 변경 | Backend 의존 규칙이 다른 답을 냄 |
| Backend Asset의 요소 이름 집합 변경 | `missing-backend-element` 판정이 무효 |
| Screen 변경 | 다른 화면 |
| 문서 교체 / 구조 변경 / 귀속 불가 변경 | 차이를 기술할 수 없음 |

`Validate()`는 여전히 **동기이고 완전한 목록을 반환합니다.** 바뀐 것은 목록이 아니라 그것을 만드는 데 드는 일의 양입니다.

## 아직 하지 않는 것
- **Incremental Compile.** 화면 단위 전체 lowering
- **Selective Repaint.** `CanvasChanged`는 여전히 전부를 다시 그리게 합니다
- **Background Job.** 모두 Main Thread 동기 실행
- 에셋 간 의존(Component Definition, Token Set, Motion Clip)

## 설계 메모

`NexDocumentRevision`은 Unity 타입을 전혀 쓰지 않습니다. 증분 처리의 정확성이 여기 걸려 있으므로,
**에디터·문서 에셋·도메인 리로드 없이 테스트할 수 있어야** 했습니다.
`NexDocumentRevisionTests`가 히스토리 소진·귀속 불가·구조 변경 같은 경계를 전부 직접 칩니다.
