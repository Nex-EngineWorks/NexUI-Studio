# Compiler Pipeline

**상태: Experimental.** 이 파이프라인은 첫 Vertical Slice 범위(Panel / Image / Label / Button, Text Binding, Command Binding)만 처리합니다.
기존 `UGUIAssetSerializer` 저장 경로를 대체하지 않으며, 두 경로는 현재 병존합니다.

## 왜 별도 파이프라인인가

기존 저장 경로는 **Authoring → uGUI Prefab**을 한 번에 수행합니다. 이 구조에서는 세 가지를 할 수 없습니다.

1. Runtime에서 발생한 문제를 Authoring 원본으로 되돌려 지목할 수 없다 (Source Map 부재).
2. 어떤 기능이 왜 Build에 포함되었는지 설명할 수 없다 (Feature Manifest 부재).
3. 저장이 실패했을 때 이전 정상 결과가 보존된다는 보장이 없다.

Compiler는 이 셋을 만들기 위한 중간 산출물(IR)을 도입합니다.

```text
DesignerMetadataAsset      (Authoring, Editor 전용)
  → Normalize              결정론적 순서 부여
  → Validate               Runtime이 가정해도 되는 구조 규칙 확정
  → Lower                  Authoring Type → 4개 Node Kind + Source Map + Feature Manifest
  → Hash                   Canonical 문자열 기반 Content Hash
  → Publish                원자적 교체
NexScreenProgram           (Compiled, Player가 읽는 유일한 산출물)
  → NexUGuiScreenBuilder   단일 전방 패스로 GameObject 생성
```

## Pass별 계약

| Pass | 보장하는 것 | 실패 시 |
|---|---|---|
| Normalize | 부모가 자식보다 먼저 온다. `siblingIndex` → `elementId` 순의 전순서(total order). | 문제 Element를 제외하고 계속 진행 |
| Validate | id 유일, 부모 존재, 사이클 없음, Binding이 대상 Node에서 실제로 동작 가능 | Diagnostic 추가, Program은 만들되 Publish 금지 |
| Lower | Authoring Type이 `NexNodeKind` 4종 중 하나로 확정 | 미지원 Type은 Panel로 lower + `NEX-CMP-3003` |
| Hash | 같은 입력 + 같은 Compiler Version = 같은 Hash | — |
| Publish | 성공하거나, 이전 결과가 그대로 남거나 | 항상 후자로 복구 |

Normalize의 `elementId` 2차 정렬은 장식이 아닙니다. 같은 `siblingIndex`를 가진 형제는 Authoring 모델에서 합법이며,
전순서가 없으면 리스트 순서에 따라 Node 배열이 달라져 Content Hash와 Compile Cache가 무의미해집니다.
이 규칙은 `NexScreenCompilerTests.Compile_IsDeterministic_RegardlessOfAuthoringListOrder`가 지킵니다.

## Node Kind가 4개뿐인 이유

Backend가 알아야 할 구성을 최소화하기 위해서입니다. Authoring Component는 수백 개가 될 수 있지만,
Backend는 Panel / Image / Label / Button 네 가지 생성만 구현하면 됩니다.
Authoring Type → Kind 매핑은 Component Registry의 `UGUIControl` 값을 경유하므로,
새 Authoring Component가 자신을 `ButtonTMP`로 선언하면 Compiler 수정 없이 Button으로 컴파일됩니다.

Kind를 늘리는 것은 모든 Backend에 구현 부담을 주는 실제 비용입니다.
기존 Kind 조합으로 표현할 수 없을 때만 추가합니다.

## 원자적 Publish

`AssetDatabase`에는 트랜잭션이 없습니다. 여기서 "원자적"이란 **경로 기준**입니다.
해당 경로를 로드하는 쪽에서 볼 때, 어느 시점에도 이전 정상 Program 또는 새 정상 Program 중 하나이며 중간 상태가 없습니다.

```text
1. 잔여물 정리(이전 크래시 흔적 복구)
2. temp 경로에 생성
3. 기존 결과를 backup 경로로 이동
4. temp → 목표 경로로 이동
5. backup 삭제
```

3~4 사이에서 실패하면 backup을 되돌린 뒤 `NEX-BLD-8001`을 보고합니다.
2단계에서 실패하면 기존 결과는 애초에 건드려지지 않았습니다.

## 변경 없는 화면은 쓰지 않는다

Publish 전에 이미 그 경로에 있는 Program과 비교합니다.

```text
기존 Program의 ContentHash == 새 Program의 ContentHash
그리고 Compiler Version도 같다
→ 디스크에 쓰지 않음 (Skipped)
```

**별도 캐시 파일이 없습니다.** Published된 자산이 자기 ContentHash를 들고 있으므로 그것이 곧 캐시입니다.
side-car 캐시와 실제 파일이 어긋나는 부류의 버그가 구조적으로 발생할 수 없습니다.

이 판단은 `NexScreenPublisher.Decide(existing, candidate)`라는 **순수 함수**입니다.
건너뛰기가 틀리면 "수정했는데 아무 일도 안 일어난다"가 되고 이건 눈에 잘 안 띄는 실패이므로,
AssetDatabase 없이 테스트할 수 있는 자리에 격리했습니다.

절약되는 것과 아닌 것:

| | |
|---|---|
| **절약됨** | 디스크 쓰기, AssetDatabase 이동, Importer 실행, Build Report 재작성 |
| **절약 안 됨** | Compile 자체 — Hash를 얻으려면 컴파일해야 합니다 |

Unity에서 비싼 쪽은 Importer이지 lowering 패스가 아니므로 이 경계가 맞습니다.
`Tools/NexUI/Compile All Screens`가 이 이득이 드러나는 지점입니다 — 40개 중 2개만 바뀌었다면 Import도 2번만 일어납니다.
진행률과 취소를 지원하며, 화면마다 원자적으로 독립 Publish되므로 중간에 취소해도 완료된 것은 정상이고
나머지는 건드려지지 않은 상태로 남습니다.

## 아직 하지 않는 것

- Incremental Compile (변경 범위만 재컴파일). 현재는 화면 단위 전체 재컴파일입니다.
- Dirty Range·Dependency Graph. 어떤 Element가 바뀌었는지 추적하지 않습니다.
- Background Job. 컴파일은 Main Thread에서 동기 실행됩니다.
- UI Toolkit Backend. `NexScreenProgram`은 Backend 중립이지만 Builder는 uGUI만 있습니다.
- Component Instance / Variant / Responsive / Motion의 lower. 현재 Compiler는 이들을 읽지 않습니다.
  (Interaction 규칙은 예외 — [Interaction Authoring](interaction-authoring.md) 참조)
- Sprite / Font 참조. `previewImage`는 아직 Program에 실리지 않습니다.

## 관련 문서

- [Error Code Catalog](../reference/error-code-catalog.md)
- [Interaction Flow Trace와 Source Map](interaction-flow-trace.md)
