# NexUI 전체 개요 — 왜 만들었고, 무엇을 만들었는가

이 문서 하나로 NexUI 프로젝트 전체를 파악할 수 있게 정리한 통합 문서입니다. 세부 주제는 각 문서로 링크했고, 마지막 [문서 지도](#12-문서-지도)에 전체 문서 목록이 있습니다.

기능 서술은 **현재 코드 기준**입니다. 아직 구현되지 않은 목표는 [목표 기능 명세](reference/feature-specification.md)에만 두고, 이 문서에서는 지원 범위를 그대로 적었습니다.

---

## 1. 만든 이유

**웹에서 React로 UI를 만들 때의 작업 경험을 Unity에서도 하고 싶었습니다.**

웹 프런트엔드는 지난 10년간 UI 제작 방식을 정리했습니다. 컴포넌트를 만들어 재사용하고, 상태를 선언적으로 화면에 연결하고, 디자인 토큰으로 스타일을 통일하고, 라우터로 화면 흐름을 관리하고, Storybook으로 상태별 화면을 미리 보고, DevTools로 실행 중인 UI를 들여다봅니다. 새 화면 하나를 만드는 데 반나절이 걸리지 않습니다.

Unity의 UI 제작은 그렇지 않았습니다.

| 웹에서 당연한 것 | Unity에서 실제로 하던 일 |
|---|---|
| 컴포넌트 재사용 | 프리팹 복사 → 원본 수정 시 사본이 따라오지 않음 |
| `props`로 조립 | Inspector 필드를 매번 손으로 채움 |
| 상태 → UI 자동 갱신 | `text.text = hp.ToString()` 을 화면마다 직접 작성 |
| 디자인 토큰 | 색상 값을 프리팹 수십 개에 하드코딩 |
| 라우터 | 화면 전환 코드가 매니저 클래스에 누적 |
| Storybook | 로딩/에러/빈 상태를 보려면 게임을 실행 |
| 한 벌의 스타일 시스템 | uGUI와 UI Toolkit이 서로 다른 세계 |

여기에 Unity 고유의 문제가 더해집니다. **uGUI와 UI Toolkit 중 무엇을 고르든 반대편 자산은 버려집니다.** 팀이 uGUI로 5년 치 UI를 쌓아 두었다면 UI Toolkit으로 갈 수 없고, 반대도 마찬가지입니다.

그래서 NexUI의 목표를 이렇게 잡았습니다.

1. **쉽게** — UI를 코드가 아니라 에디터에서 조립하고, 반복 작업(정렬·앵커·바인딩·상태 미리보기)은 도구가 대신한다.
2. **빠르게** — 처음부터 그리지 않는다. 완성된 컴포넌트 레시피를 꺼내 쓰고, 재사용 컴포넌트로 한 번 고치면 전부 반영된다.
3. **전문적으로** — 접근성 검사, 검증, 저장 리포트, Git 친화적 직렬화처럼 제품 출시에 필요한 장치를 기본 제공한다.
4. **백엔드 독립** — 같은 화면 데이터를 uGUI 프리팹으로도, UXML/USS로도 출력한다. 백엔드는 *출력 대상*이지 *작업 방식*이 아니다.

### React 개념과의 대응

| React / 웹 | NexUI에서의 대응 |
|---|---|
| Component | 재사용 Component Definition + Instance ([문서](advanced/reusable-components.md)) |
| `props` | Exposed Property Override, Variant 축 |
| `useState` / store 구독 | `UIStateStore` 키 ↔ Binding 채널 (Text/Value/Visibility/Class/Command/Interactable) |
| CSS 변수 · 테마 | Theme Token, Design Token ([문서](advanced/design-tokens.md)) |
| React Router | Screen Flow Editor ([문서](advanced/screen-flow-editor.md)) |
| Storybook | Scenario / Mock State / Forced Preview State ([문서](user-guide/preview-and-scenarios.md)) |
| Framer Motion | Motion Clip · Motion Graph · Motion State Machine ([문서](motion/overview.md)) |
| React DevTools | Runtime Snapshot & Diff, Preview Log ([문서](advanced/runtime-debugging.md)) |
| npm 컴포넌트 라이브러리 | 패키지 내장 컴포넌트 레시피 300종 |
| JSX → DOM | Designer Metadata → uGUI 프리팹 / UXML + USS 코드 생성 |

---

## 2. 무엇을 만들었나 (한눈에)

| 축 | 내용 |
|---|---|
| 패키지 | 런타임 프레임워크 `com.emiteat.nexui`, 에디터 저작 도구 `com.emiteat.nexui.designer` |
| 규모 | Runtime/Editor 어셈블리와 EditMode/PlayMode 자동 테스트 포함(정확한 개수는 현재 Test Runner XML 기준) |
| 대상 | Unity 6000.4 (6.x), uGUI 2.0 / UI Toolkit 동시 지원 |
| 컴포넌트 | Descriptor 529종(NexUI 448 · Unity uGUI 22 · UI Toolkit 59) + 복합 레시피 300종 + 프로젝트 커스텀 정의. Runtime Widget 구현 개수와는 다름 |
| 언어 | 에디터 UI 한국어/영어 (`ko-KR`, `en-US` 번역 테이블 · 컴포넌트 529종 이름 포함) |
| 출력 | uGUI 프리팹 저장, `.g.uxml` / `.g.uss` 생성, 3-way Sync & Publish |

---

## 3. 패키지와 어셈블리 구조

### 3.1 런타임 — `com.emiteat.nexui`

게임 빌드에 포함되는 프레임워크입니다. 에디터 의존성이 없습니다.

| 어셈블리 | 역할 |
|---|---|
| `Abstractions` | `IUISurface`, `IUIElementHandle` 등 백엔드 공통 계약 |
| `Core` | Screen · Layer · BackStack · Command · Loading · Registry · Responsive · 입력 정책 |
| `State` | `UIStateStore` — 키 기반 상태 저장소와 구독 |
| `Query` | 화면/요소 조회 API |
| `Motion` · `MotionClip` · `MotionGraph` | 모션 실행 엔진, 키프레임 클립, 노드 그래프 실행기 |
| `Theme` | 테마와 토큰 적용 |
| `Components` | 런타임 컴포넌트 동작 |
| `Accessibility` | `AccessibilityRole`과 접근성 계약 |
| `Localization` | 런타임 문자열 전환 |
| `Prompt` · `Templates` · `Settings` · `Debug` | 입력 글리프, 화면 템플릿, 설정, 디버그 지원 |
| `Integrations.*` | uGUI, UI Toolkit, DOTween, Addressables, VContainer, MessagePipe, Input System (선택적) |

에디터 보조 어셈블리: `Editor.ProjectSetup`, `Editor.Validator`, `Editor.Migration`, `Editor.IDGenerator`, `Editor.DebugTools`, `Editor.Settings`.

### 3.2 저작 도구 — `com.emiteat.nexui.designer`

Unity 에디터 확장입니다. 런타임 패키지에 의존하고, 그 반대는 없습니다.

| 폴더 | 역할 |
|---|---|
| `Runtime/Metadata` | 화면 편집 데이터(ScriptableObject). 빌드에 남지만 런타임 로직은 없음 |
| `Editor/Core` | Designer 컨텍스트, 선택, Undo, 좌표/계층 유틸, 창 |
| `Editor/UI` | 셸(패널 분리·도킹), 패널, 컨트롤 |
| `Editor/Viewport` | 캔버스 렌더링, 눈금자, 가이드, 스냅, 오버레이 |
| `Editor/Components` | 컴포넌트 레지스트리 · 카탈로그 · 캔버스 프리뷰 렌더러 |
| `Editor/Inspectors` | Inspector 섹션 레지스트리와 각 섹션 |
| `Editor/Serialization` | uGUI 프리팹 저장, UXML/USS 생성, 동반 JSON |
| `Editor/Validation` · `Editor/QA` | 검증 규칙, 스냅샷 테스트, Agent 매니페스트 |
| `Editor/Advanced` | 고급 도구 25종 (아래 6.4) |
| `Editor/AI` | AI 어시스턴트 |
| `Localization` | `ko-KR.json`, `en-US.json` |

---

## 4. 핵심 개념

- **Screen Definition** — 런타임이 아는 화면의 정체(ID, 레이어, 백엔드 에셋, 정책).
- **Designer Metadata** — 편집용 데이터(요소 트리, 위치, 스타일, 바인딩, 모션). 런타임 정의와 분리되어 있어, 저장 전까지 어떤 것도 게임에 영향을 주지 않습니다.
- **Element** — 메타데이터상의 UI 조각. `elementType` 문자열로 컴포넌트 종류를 가리키며, `stableId`로 백엔드 오브젝트와 영구 연결됩니다.
- **Backend** — 출력 대상(uGUI 프리팹 또는 UI Toolkit UXML/USS). 미리보기 서피스도 백엔드가 제공합니다.
- **Binding** — 요소와 런타임 상태 키의 연결. 채널은 Text / Value / Visibility / Class / Command / Interactable.
- **Component Definition · Instance** — 재사용 컴포넌트. Instance는 복사본이 아니라 **참조 + 오버라이드**라서 Definition을 고치면 전 인스턴스가 즉시 따라옵니다.
- **Component Part** — 컴포넌트 라이브러리가 소유하는 내부 시각 요소(슬라이더 트랙/핸들, 토글 체크마크 등). 요소 자체는 Hierarchy에 복제하지 않지만 Position/Size Delta/Rotation/Scale/Visibility 오버라이드는 메타데이터에 저장됩니다. 목록/그룹의 실제 내용은 Component Part가 아니라 일반 자식 요소로 저장합니다.

자세한 정의는 [개념](reference/concepts.md), [용어](reference/terminology.md), [Metadata Schema](developer/metadata-schema.md)를 보세요.

---

## 5. 런타임 기능

- **화면 생명주기** — Screen 등록·생성·표시·전환·해제, Layer와 BackStack, 로딩 전략.
- **상태와 바인딩** — `UIStateStore` 키 변경이 구독 중인 요소에 전달됩니다. 화면 코드에서 `text.text = ...`를 직접 쓰지 않습니다.
- **Command** — 버튼 등 상호작용을 커맨드 키로 발행하고, 핸들러는 화면 밖에서 등록합니다.
- **Motion** — 클립 재생, 그래프 실행, 상태 머신 전이. Reduced Motion 설정 준수.
- **Theme** — 토큰 기반 색/치수 적용, 런타임 테마 전환.
- **Responsive** — 해상도/비율 규칙에 따른 레이아웃 전환.
- **Accessibility · Localization · Prompt** — 역할/라벨, 언어 전환, 입력 장치별 글리프.
- **Integrations** — DOTween(모션), Addressables(로딩), VContainer(DI), MessagePipe(메시징), Input System(입력). 없어도 동작하며, 있으면 해당 경로를 사용합니다.

---

## 6. Designer(저작 도구) 기능

### 6.1 창과 레이아웃

- 하나의 Designer 창에 Explorer(계층·라이브러리·에셋) / Canvas / Inspector / Output 드로어가 배치됩니다.
- 각 패널은 **⧉** 버튼으로 독립 `EditorWindow`로 분리됩니다. 별도 도킹 시스템을 만들지 않고 Unity의 도킹·탭·멀티모니터·레이아웃 저장을 그대로 씁니다. `Tools/NexUI/Panels/…`로 개별 패널을 열 수 있고, `Dock All Back Into Designer`로 되돌립니다.
- 각 영역에는 이름과 현재 용도를 알려주는 캡션이 있습니다(예: `EXPLORER — 열려 있는 화면의 구조`).

### 6.2 캔버스 편집

- 단일/다중/박스 선택, 이동·크기 조절, 정렬(Key Object 기준), 간격 분배, 앵커 도구.
- 상·좌 눈금자와 드래그로 만드는 가이드(잡아서 이동, 눈금자에 놓아 제거). 요소 모서리·중심 스냅, 가이드 우선.
- 스마트 가이드(인접 모서리 포함), 그리드 스냅.
- `Space`/휠 드래그 팬, `Ctrl`+휠 포인터 기준 줌.
- 캔버스에서 컨테이너 위로 끌어다 놓아 **부모 변경**. 드롭 대상은 외곽선과 이름으로 표시되고, 자기 자손·잠김·자식 불가 타입은 제외됩니다.
- 선택 영역의 X/Y/W/H를 직접 편집하는 Transform Bar(라벨 드래그 스크럽).

### 6.3 Inspector

`Component Properties`는 선택한 타입의 스키마에서 필드를 자동으로 만듭니다. 자주 쓰는 값은 기본 그룹에, 세부 동작은 Advanced에 표시하며, 속성이 많은 컴포넌트는 검색할 수 있습니다. 파란 표시가 있는 값은 기본값을 덮어쓴 값이며 Reset으로 되돌릴 수 있습니다. 각 툴팁에는 설명과 uGUI/UI Toolkit 지원 수준이 함께 표시됩니다.

섹션은 `DesignerInspectorRegistry`에 등록되며, 검색·워크플로 필터(Build/Connect/Animate/Verify/Advanced)·노출 수준(Essential/Common/Advanced/Diagnostic)으로 걸러집니다. 주요 섹션: Component, Component Instance, Layout, Auto Layout, Constraints, Style, Typography, Accessibility, Binding, State, Command, Focus, Motion, Theme, **Attached Components**, Validation, Capabilities.

### 6.4 고급 도구 (`Editor/Advanced`)

Motion Clip Editor(전문 타임라인: 재생·마커·자동 키·복사/반전/타이밍 스케일), Motion Graph v2, Motion State Machine, Focus Navigation, Scenario(정적/타임라인/녹화), Screen Flow Editor, Design Token, Accessibility Audit(WCAG 대비·터치 타깃·라벨 누락), Sync & Publish, Responsive, Variants, Contracts, Snapshot, Refactor, Cleaner, Motion Budget, Input Preview, Game Localization, Agent Handoff, Recipes, Loading Strategy, Prompt Glyph.

메뉴는 `Tools/NexUI/Utilities` 한 곳에 모여 있습니다(도구별 개별 메뉴 항목을 늘리지 않는 규칙).

### 6.5 AI 어시스턴트

대화로 변경 계획을 만들고, 계획을 검증한 뒤 **명시적으로 Apply**할 때만 적용합니다. 적용은 단일 Undo 스텝이며, AI 출력이 코드를 실행하지 않습니다.

---

## 7. 컴포넌트 라이브러리

"빠르게"의 핵심입니다. 팔레트에서 꺼내 놓으면 되는 것의 범위를 넓혔습니다.

### 7.0 요소는 컴포넌트들의 컨테이너입니다

2026-07-30부터 Designer의 요소는 **Unity의 GameObject와 같은 구조**입니다. 요소 자체는 이름과 사각형만 갖고, 무엇인지는 **붙어 있는 컴포넌트**가 결정합니다.

- 팔레트의 `Button`은 타입이 아니라 **프리셋**입니다 — `Core.Element + UGUI.Image + UGUI.Button`을 찍어 줄 뿐이고, 그 뒤로는 전부 끄고·순서 바꾸고·지울 수 있습니다. **분해**는 프리셋 라벨만 지웁니다(구성은 애초부터 컴포넌트였으므로 구조는 그대로).
- 붙일 수 있는 컴포넌트는 세 계열입니다: **Unity uGUI**, **UI Toolkit**, **NexUI Base**(Unity에 없어서 매번 직접 만들던 것들). 화면 백엔드에서 동작하는 것만 Add Component 목록에 나옵니다.
- uGUI·NexUI 컴포넌트의 속성은 **실제 타입의 직렬화 필드에서 리플렉션으로** 만들어집니다. Unity 인스펙터가 보여 주는 필드가 그대로 나오고, Unity가 필드를 추가해도 코드 수정 없이 따라갑니다. 저장은 그 역연산입니다.
- 규칙은 Unity와 같습니다: 단일 인스턴스 중복 금지, 필수 컴포넌트 자동 동반, 충돌(그래픽 2개·레이아웃 그룹 2개) 거부, Core 요소는 Transform처럼 제거 불가.
- **캔버스도 컴포넌트 기준으로 그립니다.** Image 위에 Gradient, 그 위에 Cooldown Overlay가 붙은 순서대로 합성되고, 컴포넌트를 떼면 캔버스에서도 사라집니다.
- **백엔드를 바꾸면** 반대편 컴포넌트는 검증이 경고하고, 대응물이 있으면 교체 자동 수정을 제공합니다(uGUI.Slider ↔ UITK.Slider 등). 대응물이 없으면 지우지 않고 경고로 남깁니다.

#### NexUI Base Components (Unity에 없는 것들)

| 분류 | uGUI | UI Toolkit |
|---|---|---|
| 그래픽 | Rounded Rect · Gradient · Soft Shadow · Segmented Bar · Cooldown Overlay | Gradient · Segmented Bar · Cooldown (둥근 모서리는 USS가 이미 지원) |
| 레이아웃 | Safe Area · Flow Layout · Radial Layout · Auto Grid | Safe Area · Radial Container (wrap은 flex-wrap) |
| 텍스트 | Marquee · Typewriter · Number Ticker | 동일 3종 |
| 인터랙션 | Hold Button · Swipe Area · Tooltip Trigger | Hold Button · Swipe Manipulator |
| 데이터 | Virtual List · Carousel · Tab Group | ListView·TabView가 네이티브로 제공 |

UXML 출력에서 NexUI Base를 **커스텀 요소 태그**로 낼지(`<emiteat.NexUI...NXGradientElement />`, 바로 동작하지만 런타임 어셈블리 의존), **표준 태그 + 클래스**(`nx-gradient`, 의존성 없음)로 낼지는 설정으로 고릅니다. 기본값은 커스텀 요소입니다.

### 7.1 계열(Family) 구성

| 계열 | 개수 | 성격 | 백엔드 출력 |
|---|---:|---|---|
| **NexUI** | 448 + 내장 레시피 300 | 백엔드 독립 컴포넌트. 어느 백엔드로든 나감 | uGUI/UI Toolkit 모두 Partial(구조·텍스트·스타일 기록, 동작은 런타임 담당) |
| **Unity uGUI** | 22 | Unity `GameObject > UI` 메뉴의 스톡 컨트롤 Descriptor | **Beta/Partial** — 기본 계층은 생성하지만 타입별 속성·상호작용 범위가 다름 |
| **Unity UI Toolkit** | 59 | UI Builder Library의 표준 컨트롤 Descriptor | Generated Asset은 **Beta/Partial**, 사용자 작성 UXML은 Metadata-only |
| **Custom** | 프로젝트마다 | 팀이 만든 재사용 Component Definition | Instance로 전개 후 백엔드 저장 |

팔레트에 노출되는 529종은 모두 한국어·영어 이름을 가집니다(`component.*` 키). 번역이 빠지면 팔레트가 조용히 영문 이름으로 되돌아가므로, 누락을 잡는 테스트를 함께 둡니다.

라이브러리 패널은 계열마다 접이식 폴더(항목 수 표시)로 나뉘고, 상단 필터로 `전체 / NexUI / uGUI / UI Toolkit / Custom` 중 하나만 볼 수 있습니다. 자주 쓰는 항목(Recent)과 내장 레시피 폴더 트리는 NexUI 폴더 안에 함께 놓여, 스톡 Unity 라이브러리와 섞이지 않습니다. 검색은 한국어 표시명과 타입 ID(`UGUI.Toggle`) 양쪽에 걸리며, 카드를 고르면 상세 영역에 설명·미리보기가 표시됩니다.

### 7.2 NexUI 컴포넌트 (448종)

네 파일이 계열을 이룹니다. `DesignerComponentRegistry`(기초 21종) → `NexUIComponentCatalog`(핵심 52종) → `NexUILibraryCatalog`(확장 232종) → `NexUIGameCatalog`(게임 143종). 팔레트 폴더별로:

| 폴더 | 종수 | 예시 |
|---|---:|---|
| 컨테이너 | 8 | Panel, Card, Section, Toolbar, Scroll Area, Splitter, Accordion |
| 레이아웃 | 19 | Spacer, Flow Container, Dock Panel, Sidebar Layout, Two/Three Column, Safe Area, Sticky Header, Page Container, Form, Field Row, List Item |
| 텍스트 및 미디어 | 28 | Label, Heading, Rich Text, Caption, Code Block, Quote, Markdown, Number Ticker, Typewriter, Marquee, Currency/Percent Text, Avatar, Badge, Chip, Divider |
| 미디어 | 14 | Video View, Render Texture View, Image Gallery, Thumbnail, Cover Image, QR Code, Portrait Frame, Nine Slice, Parallax Layer, 3D Model View |
| 컨트롤 | 40 | Button, Slider, Stepper, Text/Number/Password Field, Color·Gradient Picker, Date·Time Picker, Calendar, Keybind Field, Knob, FAB, Hold Button, Radial Menu, Virtual Joystick, D-Pad, Swipe Area, Scrubber |
| 선택 및 입력 | 13 | Checkbox, Switch, Radio Group, Segmented Control, Dropdown, Combo Box, Autocomplete, Multi Select, Tag Input, Filter Bar, Transfer List |
| 내비게이션 | 22 | Tabs, App Bar, Side Nav, Nav Rail, Bottom Nav, Breadcrumb, Pagination, Menu Bar, Command Palette, Wizard, Step Indicator |
| 피드백 | 27 | Progress Bar, Stat Bar, Radial Fill, Spinner, Skeleton, Alert, Meter, Gauge, Sparkline, Snackbar, Countdown Timer, Damage Number, FPS Counter |
| 오버레이 | 20 | Modal, Popover, Tooltip, Toast, Drawer, Confirm/Alert/Prompt Dialog, Bottom·Side Sheet, Lightbox, Coach Mark, Spotlight, Hover Card |
| 데이터 및 컬렉션 | 23 | List, Grid, Table(+Row/Header), Tree View, Infinite List, Virtual Grid, Kanban Board, Timeline, Feed, Stat Card, KPI Tile, Log View |
| 차트 | 13 | Bar, Line, Area, Pie, Donut, Radar, Scatter, Heatmap, Histogram, Gauge, Funnel, Stacked Bar, Legend |
| 소셜 | 9 | Profile Card, User Row, Friend List Item, Presence Dot, Follow Button, Review Card, Rating Summary, Mention List |
| 상점 및 결제 | 12 | Product Card, Price Tag, Discount Badge, Cart Item, Checkout Summary, Coupon Field, Subscription Card, Shop Item, Currency Pack |
| 설정 | 10 | Settings Row/Toggle/Slider Row, Settings Section, Language Selector, Volume Row, Keybind Row, Account Row |
| 게임 HUD | 79 | Health/Shield/Armor/Mana/Energy Bar, Stamina Wheel, Ultimate Charge, Reload Indicator, Cast Bar, Boss Health Bar, Hit Marker, Damage Direction, Status Effect Icon, Ability Bar/Queue, Weapon Slot, Ammo Pips, Combo Rank, Tachometer, Nitro Bar |
| 게임 월드 및 맵 | 18 | Map Screen, Radar, Off-screen Indicator, Lock-on Indicator, Zone Banner, Objective List, Placement Ghost, Time of Day, Weather, Cutscene Letterbox, Skip Prompt |
| 게임 아이템 및 인벤토리 | 29 | Item Card/Tooltip/Comparison, Rarity Frame, Durability Bar, Paperdoll, Loadout, Bag Tabs, Weight Meter, Crafting Recipe/Queue, Upgrade·Enchant·Salvage Panel, Vendor List, Auction Row, Mail, Chest Opening, Summon Result, Pity Counter, Codex |
| 게임 성장 및 보상 | 19 | Experience Bar, Level Up Popup, Skill Tree, Talent Grid, Battle Pass Track, Season Tier, Daily Login Calendar, Quest Log/Objective, Achievement Row, Mastery Ring, Reputation Bar, Rank Progress, Energy Timer |
| 게임 메뉴 및 결과 | 25 | Title Screen, Main/Pause Menu, Save Slot List, Difficulty·Character·Level Select, Loading Screen, Death Screen, Respawn Timer, Match Results, Score Breakdown, Star Rating, MVP Card, Credits, Controls Diagram |
| 게임 멀티플레이 | 20 | Team Roster, Scoreboard(+Row), Lobby Slot, Ready Check, Matchmaking Status, Party Invite, Guild Panel, Chat Channel Tabs, Voice Indicator, Ping Badge, Spectator Bar, Kill Cam, Server List |

컴포넌트는 아키타입 헬퍼(Text / Media / Control / Field / Meter / Status / Container / Collection / Dialog / Chart)로 선언합니다. 아키타입이 상태·바인딩 채널·접근성 역할을 고정하므로, 입력 컴포넌트가 Error/Focused 상태를 빠뜨리거나 컬렉션이 Empty 상태를 빠뜨리는 일이 생기지 않습니다.

### 7.3 Unity 스톡 컨트롤

**uGUI (22)** — Image, Raw Image, Panel, Text(TMP), Text(Legacy), Button, Button(TMP), Toggle, Toggle Group, Slider, Scrollbar, Dropdown, Dropdown(TMP), Input Field, Input Field(TMP), Scroll View, Mask, Rect Mask 2D, Horizontal/Vertical/Grid Layout Group, Nested Canvas.

저장 시 `UGUIControlFactory`가 Unity 자신의 `DefaultControls` / `TMP_DefaultControls`를 호출합니다. 즉 `GameObject > UI > Slider`로 만든 것과 **같은 계층·같은 참조**가 프리팹에 생성됩니다. Scroll View의 자식은 `Viewport/Content` 아래로 들어가고, Toggle의 on 상태·Slider 값·Dropdown 옵션·Input Field 플레이스홀더는 메타데이터에서 채워집니다.

**UI Toolkit (37)** — VisualElement, Label, Image, Box, TextElement, HelpBox, Button, Toggle, Slider, SliderInt, MinMaxSlider, ProgressBar, RadioButton(Group), DropdownField, EnumField, Scroller, RepeatButton, ToggleButtonGroup, TextField, IntegerField, FloatField, RectField, ObjectField, ScrollView, ListView, MultiColumnListView, TreeView, MultiColumnTreeView, Foldout, GroupBox, TabView, Tab, TwoPaneSplitView, IMGUIContainer, PopupWindow 등.

각 디스크립터가 UXML 태그를 들고 있어, 코드 생성기가 `<ui:DropdownField />` 같은 **진짜 태그**를 씁니다. 스타일만 흉내 낸 `VisualElement`가 아닙니다.

> 계열이 맞지 않는 백엔드에서는 **Preview-only**로 처리합니다. 캔버스에는 그대로 보이고, 저장 리포트가 "이 백엔드에는 쓰지 않았다"고 밝힙니다. 조용히 잘못된 오브젝트를 만들지 않습니다.

### 7.4 내장 레시피 300종

아키타입과 테마 조합으로 만든 **복합 레이아웃 레시피**이며 Runtime Widget 300개를 뜻하지 않습니다. 결정론적 인메모리 정의와 `builtin:` 식별자를 사용하고, 카드는 펼친 폴더에서만 생성됩니다.

### 7.5 Add Component (임의 MonoBehaviour 부착)

Unity Inspector의 Add Component와 같은 흐름을 Designer 요소에 제공합니다. 메타데이터에는 어셈블리 한정 타입 이름만 저장하고(선택 어셈블리가 없어도 화면이 열립니다), 저장 시 프리팹 오브젝트에 부착합니다. `DesignerAttachedComponentTracker`가 **Designer가 붙인 것만** 기록하므로, 사용자가 프리팹에 직접 붙인 같은 타입 컴포넌트를 도구가 지우지 않습니다.

### 7.6 확장 방법

컴포넌트 한 종을 추가하려면 **디스크립터 하나만** 등록하면 됩니다. 팔레트·Inspector·검증·계층·직렬화가 모두 `DesignerComponentRegistry`를 읽기 때문에 패널이나 switch 문을 고칠 필요가 없습니다. 캔버스 표현이 특별하면 프리뷰 렌더러를, 백엔드 출력이 특별하면 팩토리 항목을 추가합니다. 자세히는 [확장 API](developer/api-reference.md).

---

## 8. 백엔드 출력

### 8.1 uGUI 프리팹 저장

`LoadPrefabContents → 수정 → SaveAsPrefabAsset → UnloadPrefabContents` 패턴으로 기존 참조와 사용자 작업물을 보존합니다.

- 매칭은 **stableId 우선**, 없으면 elementId 태그, 마지막으로 이름. 중복 식별자는 에러로 보고하고 **아무것도 쓰지 않습니다**.
- Designer가 만든 오브젝트만 이름을 바꾸고, 사용자 소유 오브젝트는 건드리지 않습니다(`NexUIElementOwnership`).
- 위치는 부모 상대 좌표로 변환 후 앵커 프리셋을 적용하고, 형제 순서를 `SetSiblingIndex`로 반영합니다.
- Auto Layout은 Horizontal/Vertical/Grid LayoutGroup으로, 클리핑은 RectMask2D로, 그림자/아웃라인은 uGUI 이펙트로 매핑합니다.
- 스톡 컨트롤은 7.3처럼 Unity 기본 팩토리로 생성합니다.

### 8.2 UI Toolkit 생성

- `UIToolkitCodeGenerator`는 **순수 문자열 생성기**입니다(파일 I/O 없음). 그래서 단위 테스트가 가능하고, 무엇보다 손으로 쓴 UXML을 절대 덮어쓰지 않습니다.
- 출력은 별도 파일 `<screenId>.g.uxml` / `.g.uss`이고, `NEXUI:GENERATED` 배너가 없는 파일은 **쓰기를 거부**합니다.
- 절대 좌표는 `position:absolute` + left/top으로, Auto Layout 컨테이너의 자식은 `position:relative` + flex 규칙(Fixed/Hug/Fill, spacing→margin)으로 나갑니다.

### 8.3 Sync & Publish

파일 존재 여부와 Designer/파일/직전 발행 해시를 비교해 `New · InSync · DesignerChanged · BackendChanged · Conflict`를 판정하는 3-way 동기화입니다. LCS 라인 Diff를 보고 "Designer 쪽 사용" / "백엔드 쪽 채택"을 고르며, Publish는 변경된 화면만 씁니다. → [Sync와 Publish](advanced/sync-and-publish.md)

### 8.4 저장 리포트

저장 결과를 Create / Modify / Skip / Unsupported / PreviewOnly / Conflict / Orphan / User Impact로 분류해 보여 줍니다. **쓰지 못한 것을 쓴 것처럼 보고하지 않는다**가 이 리포트의 설계 원칙입니다.

---

## 9. 품질 장치

- **Validation** — 75개 코드의 규칙과 각각의 원인·해결법([Validation Catalog](reference/validation-catalog.md)), 일부는 Auto Fix 제공.
- **접근성 감사** — WCAG 명도 대비 4.5:1, 44px 터치 타깃, 라벨/역할 누락 검사.
- **Setup Doctor** — 의존성·레지스트리·씬 백엔드·출력 경로 점검.
- **테스트** — 레지스트리, 팔레트, 레시피, 코드 생성기, 계층/좌표, Undo, 직렬화와 Runtime smoke를 검증합니다. 통과 개수는 CI의 XML 결과를 기준으로 판단합니다.
- **Git 친화 직렬화** — `.asset` 옆에 결정론적 동반 `.json`을 써서 PR에서 실제로 diff를 읽을 수 있게 합니다. 충돌 해결 후 JSON을 다시 에셋으로 되돌리는 경로도 있습니다.
- **현지화** — 에디터 UI 문자열을 하드코딩하지 않고 `ko-KR` / `en-US` 테이블에서 읽습니다.

---

## 10. 현재 상태 요약

| 영역 | 상태 |
|---|---|
| 화면 편집(선택·이동·정렬·계층·Undo) | 지원 |
| 컴포넌트 레지스트리 · 팔레트 3계열 | 지원 |
| Unity 스톡 컨트롤 생성(uGUI) | Beta/Partial — 타입별 Save Report 확인 필요 |
| Unity 스톡 컨트롤 태그 생성(UI Toolkit) | Generated Asset Beta/Partial; 사용자 UXML은 Metadata-only |
| 내장 레시피 300종 | 지원 |
| Add Component | 지원 — 값 편집은 프리팹 Inspector 담당 |
| 재사용 컴포넌트(Definition/Instance/Slot/Variant) | Beta |
| Binding · Scenario · Validation · Save Report | 지원 |
| Motion Clip / Trigger | 지원 |
| Motion Graph v2 · State Machine · Screen Flow · Design Token | 실험적 |
| Figma Frame Import | Beta (Variant·이미지 다운로드·Sync 제외) |
| uGUI 프리팹 저장 | NexUI 계열은 부분 지원(구조·텍스트·틴트·Fill 중심) |
| 컴포넌트 Motion/Theme/Responsive 오버라이드 | 미구현 |

정확한 제약은 [알려진 제한](reference/known-limitations.md), 항목별 표는 [현재 기능 상태](reference/feature-status.md)와 [Feature Parity Matrix](FeatureParityMatrix.md)를 보세요.

---

## 11. 남은 일

- Runtime Debugger 워크스페이스(Play Mode 실시간 조사) — Core에 읽기 전용 introspection API 설계가 선행됩니다.
- Design Token을 요소 스타일에 실제로 적용하기, 미사용 토큰 검색.
- uGUI 대상 Sync(현재 `.g` 파일은 UI Toolkit만), Grid 고정 열 코드 생성, 3-way 자동 병합.
- 컴포넌트 Motion/Theme/Responsive 오버라이드.

---

## 12. 문서 지도

### 시작하기
[설치](getting-started/installation.md) · [빠른 시작](getting-started/quick-start.md) · [첫 화면 만들기](getting-started/first-screen.md) · [인터페이스 둘러보기](getting-started/interface-tour.md) · [Sample 둘러보기](getting-started/sample-tour.md)

### 사용 가이드
[Designer 창](user-guide/designer-window.md) · [Screen과 Metadata](user-guide/screen-and-metadata.md) · [Canvas 편집](user-guide/canvas-editing.md) · [Hierarchy와 Layout](user-guide/hierarchy-and-layout.md) · [Assets 패널](user-guide/assets-panel.md) · [Inspector와 Style](user-guide/inspector-and-style.md) · [Binding](user-guide/binding.md) · [자주 쓰는 작업](user-guide/common-workflows.md) · [AI 어시스턴트](user-guide/ai-assistant.md) · [Preview와 Scenario](user-guide/preview-and-scenarios.md) · [Validation과 Save](user-guide/validation-and-save.md) · [Validation Auto Fix](user-guide/validation-auto-fix.md) · [화면 생성 마법사](user-guide/screen-creation-wizard.md) · [전환 프리셋](user-guide/transition-presets.md) · [Auto Layout 변환과 Anchor](user-guide/layout-conversion-and-anchor.md) · [uGUI Backend](user-guide/ugui-backend.md) · [UI Toolkit Backend](user-guide/ui-toolkit-backend.md)

### 모션
[선택 가이드](motion/overview.md) · [Motion Clip](motion/motion-clip-editor.md) · [Motion Graph](motion/motion-graph-editor.md) · [레시피](motion/recipes.md)

### 튜토리얼
[Inventory 화면](tutorials/inventory-screen.md) · [HUD 화면](tutorials/hud-screen.md) · [애니메이션 Popup](tutorials/animated-popup.md)

### 고급
[재사용 Component](advanced/reusable-components.md) · [Design Token](advanced/design-tokens.md) · [Screen Flow](advanced/screen-flow-editor.md) · [Sync와 Publish](advanced/sync-and-publish.md) · [Figma Bridge](advanced/figma-bridge.md) · [Runtime Debugging](advanced/runtime-debugging.md) · [Migration Wizard](advanced/migration-wizard.md)

### 레퍼런스
[개념](reference/concepts.md) · [용어](reference/terminology.md) · [현재 기능 상태](reference/feature-status.md) · [목표 기능 명세](reference/feature-specification.md) · [Backend 지원 범위](reference/backend-support-matrix.md) · [Validation Catalog](reference/validation-catalog.md) · [Asset Ownership](reference/asset-ownership.md) · [단축키](reference/shortcuts.md) · [알려진 제한](reference/known-limitations.md) · [문제 해결](reference/troubleshooting.md) · [Compatibility](reference/compatibility.md) · [Upgrading](reference/upgrading.md) · [출시 준비](reference/release-readiness.md)

### 개발자
[아키텍처](developer/architecture.md) · [프로젝트 구조](developer/project-structure.md) · [확장 API](developer/api-reference.md) · [Metadata Schema](developer/metadata-schema.md) · [직렬화](developer/serialization.md) · [Panel 추가](developer/adding-panels.md) · [Inspector 확장](developer/extending-the-inspector.md) · [Backend 추가](developer/adding-backends.md) · [Validation 추가](developer/adding-validation.md) · [코딩 규칙](developer/coding-conventions.md) · [테스트](developer/testing.md) · [성능](developer/performance.md) · [Git 협업](developer/git-workflow.md)

### 상태 표
[Feature Parity Matrix](FeatureParityMatrix.md) — 기능별 Authoring/Preview/Backend/Runtime/Validation/Tests 지원 표
