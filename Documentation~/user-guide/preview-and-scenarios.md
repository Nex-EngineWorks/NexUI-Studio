# Preview와 Scenario

Preview는 현재 `DesignerMetadataAsset`을 편집용 Surface에 그려 보는 기능입니다. Scenario는 여러 Binding Mock 값과 Preview 환경을 이름 붙인 `DesignerScenarioAsset`으로 저장하여 반복 적용하는 기능입니다. 둘 다 실제 게임 상태나 Play Mode 테스트를 대신하지 않습니다.

## Preview 사용

Global Toolbar의 **Preview**를 누르거나 `Tools > NexUI > Designer > Rebuild Preview`를 실행합니다. Canvas Toolbar의 **State**에서 Normal, Hover, Pressed, Disabled, Focused를 선택하면 컴포넌트 상태를 강제로 확인할 수 있습니다. **Input**은 입력 장치별 표현을 점검하며, Interactive Preview에서 누른 Command는 실행되지 않고 Bottom Drawer의 **Preview** Log에만 기록됩니다.

정상이라면 Layers와 Canvas의 Element 수가 Metadata와 일치합니다. 비어 있다면 Screen, Metadata, Backend Asset을 차례로 확인하고 **Validate**를 실행합니다.

## Scenario Asset 만들기와 열기

Project 창에서 `Create > NexUI > Designer > Scenario`를 선택합니다. `scenarioName`, 설명과 선택적인 `screenId`를 입력합니다. `screenId`는 작성 대상을 기록하는 정보이며 다른 Screen에 적용하는 것을 막지는 않습니다.

Designer Global Toolbar의 **Scenario** 필드에 에셋을 지정합니다. Scenario 편집기는 Scenario 필드 주변의 편집 동작에서 열 수 있습니다. Designer가 열려 있지 않으면 편집기가 Active Context를 찾지 못하므로 먼저 `Tools > NexUI > Designer`를 엽니다.

## 저장되는 값

| 항목 | 지원 범위 |
| --- | --- |
| Binding | Bool, Number, Text, Sprite, List |
| 강제 상태 | `DesignerComponentState` 이름 |
| Preview 환경 | Language, Theme, Input Device, Resolution |
| Timeline | 시간, Key, 형식별 값, Duration, Loop |

Text에는 `textKey`, Number에는 `valueKey`, Bool에는 주로 `visibilityKey`와 일치하는 Key를 사용합니다. Sprite와 List는 Preview 채널로 저장되지만 모든 컴포넌트와 Backend가 이를 동일하게 표시하는 것은 아닙니다.

## 현재 상태 기록과 Apply

**Record/Capture Current**는 현재 Preview Mock 값과 환경을 Scenario로 복사합니다. 저장하려는 Scenario가 선택되었는지 먼저 확인합니다. **Apply**는 Scenario 값을 Preview에 적용하며 Undo가 가능합니다. Apply 후 정상 결과는 Canvas의 Text, Value, Visibility와 강제 상태가 즉시 바뀌는 것입니다.

Apply는 `UIStateStore`를 수정하지 않고 게임 Command도 실행하지 않습니다. Scenario Reset은 적용된 Preview override를 초기화합니다. Scenario 복제는 별도 에셋을 만들며, 삭제는 에셋을 Trash로 이동하는 작업이라 일반 필드 편집 Undo와 구분해야 합니다.

## Timeline과 Scrubbing

**Use Timeline**을 켜고 Duration을 지정한 뒤 Key, 시간과 값을 추가합니다. Scrubber를 이동하면 해당 시점의 Mock 값이 Canvas에 적용됩니다. 같은 Key의 Number는 앞뒤 Keyframe 사이를 선형 보간합니다. Bool, Text, Sprite, List는 다음 Keyframe까지 이전 값을 유지합니다.

Timeline Play는 제작 확인용입니다. Play Mode 재생이나 Runtime Timeline을 생성하지 않습니다.

## Inventory 상태 예제

현재 표현 가능한 조합은 다음과 같습니다.

| Scenario | 예시 값 |
| --- | --- |
| `Inventory.Empty` | `inventory.count = 0`, `inventory.empty = true` |
| `Inventory.Normal` | 일반 개수와 List Mock |
| `Inventory.Full` | 최대 개수, `inventory.full = true` |
| `Inventory.ItemSelected` | 선택 설명 Text와 선택 상태 |
| `Inventory.Loading` | `inventory.loading = true` |
| `Inventory.Error` | 오류 Text와 오류 영역 Visibility |

실제 Metadata에 같은 Key를 사용하는 Element가 있을 때만 화면 변화가 보입니다.

## 빠른 테스트 경계값

선택 요소의 Canvas 우클릭 메뉴 `생산성/빠른 테스트`에서 경계값을 즉시 적용할 수 있습니다.

- Text: 빈 값, 짧은/긴 값, 여러 줄, 256자, 한국어/영어/일본어, 숫자, 특수문자
- Value: 0, 최소, 중간, 최대, 범위 초과
- Collection: 0, 1, 5, 20, 100개

Scenario Editor에서는 이전/다음 이동, 복제, 이름 변경, 초기화, 삭제와 현재 상태 Capture를 제공합니다. 삭제는 에셋을 휴지통으로 옮기는 작업이라 Unity Undo 대상이 아니며 확인 대화상자가 표시됩니다.

## 점검 순서

1. Text에 빈 문자열과 매우 긴 문자열을 적용합니다.
2. Number에 UI가 기대하는 최소값과 최대값을 적용합니다.
3. Visibility Bool을 false로 바꿉니다.
4. 두 개 이상의 Scenario를 반복 전환합니다.
5. Timeline 시작, 중간, 끝을 Scrub합니다.
6. uGUI와 UI Toolkit 결과를 각각 Play Mode에서 확인합니다.

예상과 다르면 Binding Key 철자, 값 형식, 현재 Metadata, 강제 State를 확인합니다. Designer는 Runtime Registry에 등록된 실제 Key와 형식을 완전히 검증할 수 없습니다.

## 관련 문서

- [Binding](binding.md)
- [Backend 지원 범위](../reference/backend-support-matrix.md)
- [문제 해결](../reference/troubleshooting.md)

