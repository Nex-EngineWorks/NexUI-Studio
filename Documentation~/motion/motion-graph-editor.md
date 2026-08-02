# Motion Graph Editor

## 이것은 무엇인가

**Motion Graph Editor**는 `UIMotionPreset`의 `UIMotionGraph`(노드 그래프)를 편집하는 노드 기반
에디터입니다. `UnityEditor.Experimental.GraphView` 위에 구현되어 있으며, 각 노드는 하나의
`UIMotionStep`(Property/From/To/Duration/Delay/Easing)을 나타내고, 노드 사이의 연결은
"의존성"(먼저 끝나야 하는 노드)을 의미합니다.

Motion Clip Editor([문서](motion-clip-editor.md))와는 별개의, 더 오래된 시스템입니다. 차이:

| | Motion Graph Editor | Motion Clip Editor |
|---|---|---|
| 데이터 모델 | Step(from→to→duration), 노드 의존성 그래프 | 다중 요소 × 다중 Property × 키프레임 타임라인 |
| 대상 | 프리셋 하나당 단일 대상 | 클립 하나에 여러 UI Element |
| 컴파일 결과 | `UIMotionTimeline` (`emiteat.NexUI.Motion`) | 자체 Evaluator로 직접 재생 |
| 편집 방식 | 노드 그래프(연결선으로 순서 표현) | 시간 스크러버 + 키프레임 |

두 시스템 모두 계속 유지되며, 서로의 내부 구현을 변경하지 않습니다.

## 여는 방법

- Legacy Graph는 NexUI Studio에서 Element를 선택하고 Motion Inspector에 Motion Preset을 할당한 뒤
  **"Open Motion Graph"** 버튼을 누르면 해당 프리셋이 미리 채워진 채로 열립니다.
- 별도의 typed Graph는 `Tools > NexUI > Studio > Advanced > Motion Graph (v2)`에서 열고 `UIMotionGraphAsset`을 선택합니다. Event Node, Flow 연결, Auto Layout과 Entry Event Preview를 제공합니다.

메인 Studio 창과 독립된 별도 창입니다(도킹되어 있지 않음) — 여러 프리셋을 동시에 다른
창에서 열어볼 수 있습니다.

## 노드 편집

- **Add Node** (툴바 버튼 또는 그래프 우클릭 → "Add Motion Node"): `UIMotionStep` 필드(Id,
  Property, From, To, Duration, Delay, Easing)를 가진 새 노드를 만듭니다.
- 새로 만든 프리셋(그래프가 완전히 비어있는 경우)은 **`start`/`end` 노드가 자동으로 생성**되고
  서로 연결됩니다 — 실제 재생에는 영향 없는 무효과(opacity 1→1, duration 0) 노드로, 그 사이에
  실제 모션 노드를 끼워 넣는 시작점 역할만 합니다.
- 노드의 **Output** 포트를 다른 노드의 **Input** 포트에 연결하면 "이 노드는 연결된 노드가 끝난
  뒤에 시작한다"는 의존성이 기록됩니다.
- 우클릭 메뉴에서 **Duplicate Node**(선택 노드 복제, 의존성은 복사되지 않음), **Auto
  Layout**(의존성 깊이 기준으로 자동 정렬)을 사용할 수 있습니다.
- 모든 편집은 `Undo.RecordObject`로 기록되어 `Ctrl+Z`로 되돌릴 수 있고, **Save Now**로 즉시
  에셋에 저장할 수 있습니다(그 외에는 `EditorUtility.SetDirty`만 호출되어 일반 에셋 저장
  시점에 함께 저장됩니다).

## Preview

Legacy Graph 아래에 `MotionTimelinePreview`가 도킹되어 있어, 그래프를 컴파일한 `UIMotionTimeline`을
스크러빙하며 각 트랙의 보간값과 전체 Duration을 확인할 수 있습니다. 실제 UI에는 적용되지 않는
읽기 전용 프리뷰입니다(라이브 UI 프리뷰가 필요하면 Motion Clip Editor를 사용하세요).

## 제한사항

- 노드 그래프는 선형 의존성 체인만 표현합니다 — 병렬/그룹/시퀀스 같은 고급 구조는 없습니다.
- `MotionCompiler`는 프리셋에 `variants`가 있으면 그래프보다 우선합니다(그래프는 variants가
  비어있을 때만 컴파일됩니다) — 참고: `Runtime/Motion/MotionCompiler.cs`.
- Bottom Drawer의 Screen Graph는 현재 Screen과 Element Binding 요약을 보여 주는 별도 패널이며 Motion Graph 실행 모델과 관계가 없습니다.

> [!NOTE]
> Legacy Graph와 Motion Graph (v2)는 서로 다른 에셋과 Runtime 실행기를 사용합니다. 문서나 파일명에 `v2`가 있는지 확인해 주세요.

[Motion 비교](overview.md) · [Motion Clip](motion-clip-editor.md)
