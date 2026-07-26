# Inspector와 Style

Inspector는 선택 상태에 따라 Screen 또는 Element 설정을 하나의 스크롤 영역에 보여 줍니다. 상단 검색과 Workflow 필터로 Section을 좁히고, 각 Section은 독립적으로 접거나 펼칠 수 있습니다.

- **Build:** Position, Size, Anchor, Auto Layout, Constraints, Element Type, Text, Classes, Shape, Tint, Text Color, Font Size, Image와 값 Preview를 편집합니다.
- **Connect:** Text/Value/Visibility/Class/Interactable/Command Binding, Preview State와 Focus Navigation을 다룹니다.
- **Animate:** Screen Entry/Exit Clip, Element Trigger, Reduced Motion Clip, State/Command 조건과 Theme을 연결합니다.
- **Verify:** Validation과 Accessibility를 확인합니다.
- **Advanced:** Policy와 Backend Capability 같은 기술 계약을 확인합니다.

Beginner Mode는 필수·일반 Section만 보여 주고, Pro Mode는 고급·진단 Section까지 보여 줍니다. Section이 숨겨져도 저장된 값은 바뀌지 않습니다. 검색은 Section 제목뿐 아니라 `position`, `sprite`, `command`, `focus`, `backend` 같은 관련 속성 키워드도 찾습니다.

ProgressBar, StatBar, RadialFill은 Min/Max/Preview Value와 Fill Direction을 제공합니다. ChoiceList는 Preview Options, List/Grid/Hotbar는 Preview Item Count를 사용합니다. 이 값 중 `preview*` 필드는 Runtime 데이터가 아니라 제작 확인용일 수 있습니다.

현재 Component Registry에는 Panel, Container, Card, Modal, Popover, Label, Image, Button, IconButton, ChoiceList, ProgressBar, StatBar, RadialFill, Spinner, Skeleton, Toast, Tooltip, List, Grid, Slot, Hotbar와 Custom fallback이 등록되어 있습니다.
