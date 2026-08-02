# Motion 개요

NexUI에는 목적이 다른 Motion 모델이 공존합니다. 새 작업에서는 필요한 편집 방식과 Runtime 연결 범위를 먼저 선택해 주세요.

| 비교 | Motion Clip | Legacy Motion Graph | Motion Graph (v2) | Motion State Machine |
|---|---|---|---|---|
| 데이터 | `UIMotionClip` | `UIMotionPreset`의 `UIMotionGraph`/`UIMotionStep` | `UIMotionGraphAsset`/typed node | 상태와 Transition Clip 참조 |
| 편집 | Timeline/Keyframe | Dependency Node Graph | Event/Flow Node Graph | 상태/전이 Graph |
| 대상 수 | 여러 Element Track | 기본적으로 단일 대상 Preset | Node 설정에 따름 |
| Property 수 | Track마다 여러 Property | Step마다 Property | Node type별 입력 |
| Timeline | 지원 | 컴파일 결과 Preview | 없음 |
| Node Graph | 없음 | 지원 | 지원 |
| Preview | Studio Surface에 Scrub/Play | 컴파일 Timeline 값 확인 | Studio Surface에서 Entry Event 실행 |
| 적합한 용도 | 여러 Element의 시간 순서 연출 | 단일 Element From/To 단계 | Click/Screen event 기반 흐름 실험 | 상태 사이 Clip 전환 저작 |
| 상태 | 지원, Runtime Trigger Binder 제공 | 부분 지원 | 실험적 | 실험적 |

Button Hover처럼 한 대상의 단순 From/To 변화는 Legacy Motion Preset이 이해하기 쉽습니다. 여러 Element가 순서대로 등장하는 화면은 Motion Clip이 적합합니다. Event에서 분기되는 실행 흐름을 시험하려면 Motion Graph (v2)를 사용할 수 있지만 Production 전 Runtime 결과를 별도로 검증해야 합니다.

Motion State Machine은 위 모델과 별개로 상태 전환과 Transition Clip을 저장합니다.

## 선택 가이드

- Fade, Slide, 순차 등장처럼 시간이 핵심이면 **Motion Clip**을 사용합니다.
- 한 Element의 단순 From/To Preset이면 **Legacy Motion Graph**를 검토합니다.
- Event에서 여러 동작으로 이어지는 흐름을 시험하려면 **Motion Graph (v2)**를 사용합니다.
- Idle/Selected/Disabled처럼 명시적인 상태 전이를 저작하려면 **Motion State Machine**을 사용합니다.

Graph v2와 State Machine은 실험적 도구입니다. Asset이 저장된다는 사실과 현재 게임의 Lifecycle에 자동 연결된다는 의미는 다릅니다. Runtime 연결을 별도로 확인하세요.

`DesignerMotionTriggerRuntime`은 uGUI/UI Toolkit 공통 Capability에서 Click, Hover, Pointer, Focus 이벤트를 자동 구독합니다. 화면 Stack, State와 Command를 소유한 코드는 `Enter`, `Exit`, `Notify`를 해당 Lifecycle 지점에서 호출합니다.

실제 값으로 만드는 방법은 [Motion 레시피](recipes.md), 저장 연결은 [Motion Clip Editor](motion-clip-editor.md)를 참고하세요.
