# Motion 레시피

이 문서는 `UIMotionClip`의 Track과 Keyframe으로 반복해서 쓰는 UI 연출을 만드는 방법을 설명합니다. 편집기는 `Tools > NexUI > Studio > Advanced > Motion Clip Editor`에서 엽니다. Property 이름은 편집기의 Track 추가 목록에 실제로 표시되는 항목만 사용하세요.

Motion Clip은 여러 Element의 시간 기반 연출에 적합합니다. 이벤트 흐름은 Motion Graph (v2), 상태 전이는 Motion State Machine의 책임입니다. 역할 선택은 [Motion 개요](overview.md)를 참고하세요.

## Fade In / Fade Out

### 적합한 상황

화면, Tooltip, Toast를 부드럽게 표시하거나 숨길 때 사용합니다.

### 사용할 시스템

Motion Clip과 `CanvasGroupAlpha` Track을 사용합니다.

| 시간 | Property | 값 |
| ---: | --- | ---: |
| 0.00 | CanvasGroupAlpha | 0 |
| 0.20 | CanvasGroupAlpha | 1 |

Fade Out은 값을 반대로 둡니다. Preview에서 0초와 끝 시점을 Scrub하고, Runtime에서는 ScreenEnter/ScreenExit 또는 Element Trigger Binding에 Clip을 연결합니다. Backend에서 실제 Alpha/Opacity Capability가 제공되는지 Play Mode에서 확인하세요.

## Scale Pop

선택, 확인, Popup Open에 적합합니다. `LocalScale` Track을 0초 0.9, 0.12초 1.04, 0.20초 1.0으로 둡니다. 중간 Keyframe에는 Ease Out 계열을 권장합니다. 너무 작은 Scale은 Reduced Motion이나 접근성 설정에서 불편할 수 있습니다.

## Slide In

Panel이나 Toast가 화면 가장자리에서 들어올 때 `AnchoredPosition` Track을 사용합니다.

| 시간 | Property | 값 예시 |
| ---: | --- | --- |
| 0.00 | AnchoredPosition | 최종 위치에서 X + 48 |
| 0.25 | AnchoredPosition | 최종 위치 |

Metadata의 배치 자체를 바꾸는 것이 아니라 Motion 값이 적용되는지 Preview 후 확인합니다. Anchor와 Auto Layout이 Position을 다시 계산하는 Backend에서는 Play Mode 검증이 필요합니다.

## Popup Open / Close

Open Clip에는 `CanvasGroupAlpha` 0→1과 `LocalScale` 0.96→1을 함께 사용합니다. Close Clip은 별도 Clip으로 만들고 1→0, 1→0.96을 사용합니다. 하나의 Clip을 역재생한다고 가정하지 마세요. Screen 단위 Entry/Exit 또는 Modal Element의 Open/Close Trigger에 명시적으로 연결합니다.

## Tooltip 등장

HoverEnter에 짧은 Fade/Slide Clip, HoverExit에 Fade Out Clip을 연결합니다. Tooltip 대상 Element ID와 Motion Binding의 `targetElementId`가 같아야 합니다. Touch 입력에는 Hover가 없으므로 별도의 표시 경로가 필요합니다.

## Toast 알림

`AnchoredPosition`과 `CanvasGroupAlpha`를 함께 사용하고, 표시 유지 시간은 Keyframe 간격으로 확보합니다. Motion Clip은 Command 결과를 스스로 판단하지 않으므로 CommandCompleted/CommandFailed 알림은 Runtime이 해당 Trigger를 통지해야 합니다.

## Button Hover와 선택 상태

한 Element의 단순 From/To 변화라면 Legacy Motion Preset도 선택할 수 있습니다. Motion Clip을 쓸 경우 HoverEnter/HoverExit 또는 Selected/Deselected Binding을 각각 연결합니다. Hover만 구현하면 키보드 Focus와 터치 Selected에서 피드백이 사라질 수 있습니다.

## 여러 Element 순차 등장

각 Element에 `CanvasGroupAlpha`와 `AnchoredPosition` Track을 만들고 시작 시간을 0.04~0.08초씩 미룹니다. Track Target은 중복되지 않게 유지합니다. Keyframe 수가 많아지면 Marker와 Work Area로 검토 범위를 줄이세요.

## Reduced Motion 대체

`reducedMotionClip`에는 큰 이동과 Scale 반동을 빼고 짧은 `CanvasGroupAlpha` 변화만 둡니다. 대체 Clip은 선택 사항이지만, 연결만으로 사용자 설정을 자동 판정한다고 가정하지 마세요. Runtime의 Reduced Motion 정책과 Trigger Binder 연결을 Play Mode에서 확인해야 합니다.

현재 Property 목록은 `AnchoredPosition`, `LocalPosition`, `LocalRotationZ`, `LocalScale`, `SizeDelta`, `CanvasGroupAlpha`입니다. `AnchoredPosition`과 `LocalPosition`은 현재 공통 Capability에서 같은 값으로 처리되며, UI Toolkit의 `LocalScale`은 X/Y만 반영합니다.

## 공통 확인

- Track Target Element ID가 Metadata에 존재하는지 확인합니다.
- Keyframe 시간이 0 이상이고 Duration 이내이며 정렬되어 있는지 Validate합니다.
- Studio Save 후 Metadata의 Asset Reference가 유지되는지 다시 엽니다.
- uGUI와 UI Toolkit에서 지원 Capability가 다른 Property는 Play Mode에서 각각 확인합니다.
- Motion Preview는 Runtime Lifecycle 호출을 대신하지 않습니다.
