# Inspector와 Style

Inspector는 선택 상태에 따라 Screen 또는 Element 설정을 하나의 스크롤 영역에 보여 줍니다. 상단 검색과 Workflow 필터로 Section을 좁히고, 각 Section은 독립적으로 접거나 펼칠 수 있습니다.

## Component Structure

컨트롤을 선택하면 오른쪽 Inspector의 **컴포넌트 구조**에서 두 종류의 내부 내용을 편집할 수 있습니다.

- **내부 파트**는 Track, Fill, Handle, Checkmark, Label, Viewport처럼 컴포넌트 라이브러리가 소유하는 시각 요소입니다. 칩 또는 캔버스 파트를 선택한 뒤 로컬 Position, Size Delta, Rotation, Scale, Visibility를 편집합니다. Reset은 오버라이드를 제거하고 라이브러리 기본값으로 되돌립니다.
- **편집 가능한 내부 요소**는 일반 Hierarchy에 저장되는 실제 자식입니다. Toggle Group에서는 실제 Toggle을, 프레임·컨테이너에서는 Panel/Text를 추가할 수 있습니다. 자식을 선택하면 기존 Layout, Style, Binding, Motion Inspector를 모두 사용하므로 각 항목의 Position, Size, Rotation, Scale을 독립적으로 편집할 수 있습니다.

파트 Transform은 라이브러리 기본 구조에 대한 오프셋으로 저장됩니다. uGUI는 매핑된 실제 `RectTransform`에 적용하며 반복 저장 시 누적되지 않습니다. 생성형 UI Toolkit은 안정적인 내부 selector에만 Transform USS를 출력하고, 지원하지 않는 Size Delta는 Save Preview에서 명시합니다.

- **Build:** Position, Size, Anchor, Auto Layout, Constraints, Element Type, Text, Classes, Shape, Tint, Text Color, Font Size, Image와 값 Preview를 편집합니다. 선택이 재사용 Component의 Instance이면 **Component Instance** Section이 함께 나타나 Variant 선택, Exposed Property Override와 Detach/Update를 제공합니다(그 외의 선택에서는 숨겨집니다).
- **Connect:** Text/Value/Visibility/Class/Interactable/Command Binding, Preview State와 Focus Navigation을 다룹니다.
- **Animate:** Screen Entry/Exit Clip, Element Trigger, Reduced Motion Clip, State/Command 조건과 Theme을 연결합니다.
- **Verify:** Validation과 Accessibility를 확인합니다.
- **Advanced:** Policy와 Backend Capability 같은 기술 계약을 확인합니다.

Beginner Mode는 필수·일반 Section만 보여 주고, Pro Mode는 고급·진단 Section까지 보여 줍니다. Section이 숨겨져도 저장된 값은 바뀌지 않습니다. 검색은 Section 제목뿐 아니라 `position`, `sprite`, `command`, `focus`, `backend` 같은 관련 속성 키워드도 찾습니다.

ProgressBar, StatBar, RadialFill은 Min/Max/Preview Value와 Fill Direction을 제공합니다. ChoiceList는 Preview Options, List/Grid/Hotbar는 Preview Item Count를 사용합니다. 이 값 중 `preview*` 필드는 Runtime 데이터가 아니라 제작 확인용일 수 있습니다.

현재 Component Registry에는 Panel, Container, Card, Modal, Popover, Label, Image, Button, IconButton, ChoiceList, ProgressBar, StatBar, RadialFill, Spinner, Skeleton, Toast, Tooltip, List, Grid, Slot, Hotbar와 Custom fallback이 등록되어 있습니다.

`ComponentInstance`도 등록되어 있지만 Palette에서 직접 추가하는 타입이 아닙니다. 재사용 Component의 Definition을 찾을 수 없을 때만 이 타입으로 표시되어, 문제가 있다는 사실이 Canvas에 드러나게 합니다. 정상적인 Instance는 Definition root의 타입을 그대로 사용합니다.
