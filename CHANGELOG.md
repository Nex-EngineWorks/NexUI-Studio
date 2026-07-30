# Changelog

## [Unreleased]

### Editable component structure
- Added a Component Structure Inspector that separates real authored children from library-owned internal parts.
- Toggle Group and other containers can create/select real child elements; those children use the standard Layout, Style, Binding and Motion inspectors.
- Slider, Toggle, Switch, Dropdown, Input Field, Scroll View and related controls expose named internal parts with Position, Size Delta, Rotation, Scale, Visibility and Reset controls. Major preview parts can also be selected and dragged directly on the canvas.
- uGUI part overrides map onto the stock control hierarchy and capture hidden baselines so repeated saves are idempotent. Generated UI Toolkit USS emits transforms for stable internal selectors and reports unsupported size deltas honestly.
- Companion JSON format 6 round-trips sparse component-part overrides with validation for malformed, duplicate, unknown and backend-preview-only parts.

### Schema-driven Unity-like component properties
- Added typed, sparse component-property storage and per-component schemas across the full registry. Inspector fields are grouped into Basic/Advanced foldouts, searchable on large components, localized, documented with tooltips, and resettable to schema defaults.
- Connected representative control properties to canvas previews and real backend output: Slider ranges/direction/whole numbers, Toggle state, Dropdown options/index, input placeholder/content rules, ScrollRect behavior, media, text, selection transition/navigation, and content clipping.
- NexUI controls with honest stock equivalents now generate real uGUI control hierarchies. Legacy plain elements are upgraded without deleting unrelated components, and clean projects without TMP Essential Resources fall back to working legacy Text.
- Companion JSON format 5 round-trips component properties and asset references. Validation and Save Preview now report malformed values and property-level Full/Partial/Preview-only/Unsupported backend parity.

### 컴포넌트 모델을 캔버스·UXML·검증까지 확장
- **캔버스가 컴포넌트 스택을 순서대로 합성합니다.** Image 위에 Gradient, 그 위에 Cooldown Overlay가 붙은 순서대로 그려지고, 컴포넌트를 떼면 캔버스에서도 사라집니다. 시각 컴포넌트가 없는 요소(UI Toolkit 컨트롤 등)는 기존 프리셋 렌더러로 폴백합니다.
- **UXML 태그를 컴포넌트가 결정합니다.** UI Toolkit 컨트롤 컴포넌트가 태그를, NexUI Base가 자기 커스텀 요소를 냅니다. 출력 방식은 설정으로 고릅니다 — 커스텀 요소(바로 동작, NexUI 런타임 어셈블리 의존) 또는 표준 태그 + `nx-*` 클래스(의존성 없음). 기본은 커스텀 요소.
- **백엔드 전환 검증 + 자동 수정.** 화면을 uGUI ↔ UI Toolkit으로 바꾸면 반대편 컴포넌트를 경고로 잡고, 대응물이 있으면 교체합니다(uGUI.Slider ↔ UITK.Slider 등). 값은 양쪽이 공유하는 키만 이전하고, 대응물이 없으면 지우지 않고 경고로 남깁니다. 필수 누락·충돌도 함께 검사합니다.
- Base 컴포넌트 3종 추가(uGUI): **Virtual List**(보이는 행만 만들고 재활용 — uGUI엔 ListView가 없음), **Carousel**(페이지 스냅·루프·자동 넘김), **Tab Group**(탭 선택에 따라 페이지 전환).

### 인스펙터 레이아웃 깨짐 수정
- 컴포넌트 카드가 팔레트 라이브러리 타일용 클래스(`nexui-component-card`, 48%×78px)를 재사용해 필드가 78px 박스에 갇히고 아래 섹션 위로 겹쳐 그려지던 문제. 인스펙터 전용 클래스로 분리했습니다.
- 컴포넌트가 없는 요소를 rebuild 중에 구성하면서 `ElementChanged`가 다시 rebuild를 부르던 재귀. 가드를 넣고 프리셋 스탬프가 멱등인지 테스트로 고정했습니다.
- "컴포넌트 / 컴포넌트 속성 / 런타임 컴포넌트"가 같은 일을 세 번 하던 문제. 속성 섹션은 컴포넌트가 있으면 숨기고, 스크립트 부착은 Add Component 메뉴로 합쳤으며, 겹치던 섹션 이름을 정리했습니다.

### 요소를 Unity처럼 "컴포넌트를 붙이는 것"으로 바꿈
- 요소가 더 이상 타입 하나에 모든 게 박혀 있지 않습니다. **요소 = 컴포넌트들의 컨테이너**(GameObject와 같은 구조)이고, 팔레트의 'Slider'는 컴포넌트 조합을 찍어 주는 **프리셋**이 되었습니다. 프리셋이 붙인 것도 전부 끄고, 순서 바꾸고, 지울 수 있습니다.
- `DesignerElementComponent`(typeId + enabled + 값 목록)를 요소에 배열로 저장합니다. 값은 사용자가 바꾼 것만 기록하고, 모르는 키는 보존해 새 빌드에서 만든 화면이 구 빌드에서도 열립니다.
- **속성은 리플렉션으로 생성합니다.** uGUI/NexUI 컴포넌트의 스키마를 실제 타입의 직렬화 필드에서 읽어, Unity 인스펙터가 보여 주는 것과 같은 필드가 나옵니다. Unity가 필드를 추가해도 코드 수정 없이 따라갑니다. 저장은 그 역연산이라 수기 매핑 표가 필요 없습니다.
- Unity식 인스펙터: 컴포넌트 카드(체크박스 + ⋮ 메뉴로 제거/위아래 이동/값 초기화) + 하단 **Add Component**. Add Component 목록은 **현재 화면 백엔드에서 동작하는 것만** 보여 주고, 지금 붙일 수 없는 것은 숨기지 않고 이유와 함께 비활성 표시합니다.
- 규칙은 Unity와 같습니다: 단일 인스턴스 타입 중복 금지, 필수 컴포넌트 자동 동반, 충돌(그래픽 2개, 레이아웃 그룹 2개) 거부. Core 요소 컴포넌트는 Transform처럼 제거 불가.
- uGUI 저장 시 붙인 컴포넌트를 **실제 MonoBehaviour로 생성**하고 값을 필드에 직접 씁니다. UI Toolkit 전용 컴포넌트가 uGUI 화면에 있으면 저장 리포트에 이유를 남깁니다.

### NexUI Base Components — Unity에 없어서 매번 직접 만들던 것들
- 실제로 동작하는 런타임 컴포넌트를 uGUI(MonoBehaviour)와 UI Toolkit(VisualElement) **양쪽에** 추가했습니다.
- 그래픽: **Rounded Rect**(스프라이트 없이 모서리 반경·테두리), **Gradient**(선형/4코너), **Soft Shadow**(uGUI Shadow는 하드 카피 1장뿐), **Segmented Bar**(체력 칸), **Cooldown Overlay**(원형 스윕).
- 레이아웃: **Safe Area**(노치 대응 — Unity는 값만 주고 컴포넌트를 안 줍니다), **Flow Layout**(줄바꿈 — uGUI 레이아웃 그룹이 못 하는 것), **Radial Layout**(원형 배치), **Auto Grid**(열 수 유지하며 셀 크기 계산).
- 텍스트: **Marquee**(넘치면 흐름), **Typewriter**(문장부호 일시정지 + 스킵), **Number Ticker**(값까지 카운트).
- 인터랙션: **Hold Button**(진행률 보고 + 완료), **Swipe Area**(방향·거리), **Tooltip Trigger**(지연 표시/숨김 — Unity엔 런타임 툴팁이 아예 없음).
- UI Toolkit이 이미 네이티브로 하는 것(둥근 모서리, flex-wrap)은 일부러 중복 래퍼를 만들지 않고 그 사실을 컴포넌트 설명에 적었습니다.

### 컴포넌트 이름 529종을 한국어로
- 팔레트에 노출되는 모든 컴포넌트(NexUI 448 · uGUI 22 · UI Toolkit 59)에 `component.*` 이름 키를 ko/en 양쪽에 채웠습니다. 각 파일에 508개가 추가되어 총 1,718개 키가 되었습니다.
- 지금까지는 번역 키가 없으면 팔레트가 영문 DisplayName으로 조용히 되돌아갔습니다. 한국어 에디터에서 "체력 바"가 아니라 "Health Bar"로 보이던 이유이고, 실패로 드러나지 않는 종류의 문제라 **누락을 잡는 테스트**(`EveryPaletteComponentIsNamedInBothLanguages`)를 함께 넣었습니다.
- Unity 타입 이름 자체가 식별자인 항목(`Rect Mask 2D`, `Visual Element`)은 원문을 유지했습니다. 검색은 한국어 이름과 타입 ID(`UGUI.Toggle`) 양쪽에 걸리므로 어느 쪽으로 찾아도 나옵니다.

### 게임 UI 컴포넌트 143종 추가 (게임 계열 총 190종)
- `NexUIGameCatalog`을 추가했습니다. 게임이 실제로 출시할 때 필요한 화면을 팔레트에서 조립할 수 있도록, HUD 하나만이 아니라 게임 UI 전 영역을 채웠습니다.
- 팔레트 폴더를 5개 더 나눴습니다. HUD 하나에 몰아넣으면 찾을 수 없기 때문입니다: **게임 월드 및 맵**(18), **게임 아이템 및 인벤토리**(29), **게임 성장 및 보상**(19), **게임 메뉴 및 결과**(25), **게임 멀티플레이**(20). 기존 게임 HUD는 47 → 79종.
- 전투 HUD: Health/Shield/Armor/Mana/Energy Bar, Stamina Wheel, Ultimate Charge, Reload Indicator, Cast Bar, Hit Marker, 피격 방향 표시, Status Effect Icon, Ability Queue, Weapon Slot, Ammo Pips, Combo Rank, Detection/Noise Meter, Tachometer, Gear Indicator.
- 월드: Map Screen, Radar, Off-screen/Lock-on Indicator, Zone Banner, Objective List, Placement Ghost, Time of Day, Weather, Cutscene Letterbox, Skip Prompt.
- 아이템: Item Card/Tooltip/Comparison, Rarity Frame, Durability Bar, Paperdoll, Loadout, Bag Tabs, Weight Meter, Crafting Recipe/Queue, Upgrade·Enchant·Salvage Panel, Vendor/Buyback List, Auction Row, Mail, Chest Opening, Summon Result, Pity Counter, Collection Album, Codex.
- 성장: Experience Bar, Level Up Popup, Skill Tree, Talent Grid, Battle Pass Track, Season Tier, Daily Login Calendar, Quest Log/Objective, Achievement Row, Mastery Ring, Reputation Bar, Rank Progress, Energy Timer, VIP Level.
- 메뉴/결과: Title Screen, Main·Pause Menu, Save Slot(List), Difficulty·Character·Level Select, Loading Screen/Tip, Death Screen, Respawn Timer, Match Results, Score Breakdown, Star Rating, MVP Card, Credits, Quit Confirm, Controls Diagram, Ad Reward Button.
- 멀티플레이: Team Roster, Scoreboard(+Row), Lobby Slot, Ready Check, Matchmaking Status, Party Invite, Guild Panel, Chat Channel Tabs, Voice Indicator, Ping/Host Badge, Spectator Bar, Kill Cam, Report Player, Server List, Session Code.
- 아키타입 헬퍼를 `NexUIComponentArchetypes`로 분리해 라이브러리·게임 카탈로그가 공유합니다. Container 아키타입에 `overlay` 옵션을 추가했고, 오버레이 컨테이너는 접근성 역할이 자동으로 Dialog가 됩니다.
- 새 타입에도 캔버스 프리뷰를 연결했습니다(막대·링·슬롯·행·타일·표 형태로 재사용). 게임 폴더 6종이 각각 15종 이상 유지되는지, 팔레트 폴더 제목 번역이 있는지도 테스트로 고정했습니다.

### NexUI 컴포넌트 라이브러리를 232종 확장 (총 305종)
- `NexUILibraryCatalog`를 추가했습니다. 기존 73종으로는 화면을 팔레트에서 조립하기에 부족해, 실제 제품에 필요한 긴 꼬리를 채웠습니다. 이것들은 **컴포넌트**이며(레시피가 아님), 각각 레지스트리의 1급 타입으로 자체 기본값·슬롯·상태·바인딩·백엔드 매핑을 가집니다.
- 팔레트 폴더를 6개 추가했습니다: 레이아웃(19), 미디어(14), 차트(13), 소셜(9), 상점 및 결제(12), 설정(10). 기존 폴더도 함께 늘어 컨트롤 40, 게임 HUD 47, 텍스트 및 미디어 28, 피드백 27, 데이터 23, 내비게이션 22, 오버레이 20종이 되었습니다.
- 예: Dock Panel·Safe Area·Page Container·Form 같은 레이아웃 골격, Color/Date/Time Picker·Knob·Virtual Joystick·D-Pad 같은 컨트롤, Bar/Line/Pie/Radar/Heatmap 차트, Product Card·Checkout Summary 같은 상점 요소, Ability Bar·Cast Bar·Party Frame·Kill Feed·Weapon Wheel 같은 게임 HUD 부품.
- 아키타입 헬퍼(Text/Media/Control/Field/Meter/Status/Container/Collection/Dialog/Chart)로 선언합니다. 아키타입이 상태·바인딩 채널·접근성 역할을 고정하므로, 입력 컴포넌트가 Error/Focused 상태를, 컬렉션이 Empty 상태를 빠뜨리는 일이 구조적으로 생기지 않습니다.
- 새 타입 대부분에 캔버스 프리뷰를 붙였습니다. 차트·스탯 타일·리스트 행·컬러 영역·조이스틱은 전용 렌더러를, 나머지는 형태가 같은 기존 렌더러를 재사용합니다.
- 레지스트리는 TypeId로 키를 잡으므로 나중에 등록된 카탈로그가 앞의 것을 조용히 덮어씁니다. 카탈로그 간 ID 충돌, 팔레트 도달 가능성, 라이브러리 규모(300종 이상)를 테스트로 고정했습니다.
- 새 팔레트 폴더 제목을 ko/en 양쪽에 추가했습니다. 번역이 빠지면 팔레트에 `palette.group.charts` 같은 키가 그대로 노출되므로, 이를 막는 테스트도 함께 추가했습니다.

### 전체 개요 문서
- `Documentation~/overview.md`를 추가했습니다. 프로젝트를 만든 이유(웹의 React 수준 UI 제작 경험을 Unity에서), 런타임/Designer 전체 기능, 컴포넌트 라이브러리, 백엔드 출력 파이프라인, 현재 상태와 남은 일, 그리고 기존 문서 전체로 가는 지도를 한 문서에 모았습니다. 흩어진 문서를 먼저 다 읽지 않아도 프로젝트 전체를 파악할 수 있게 하는 것이 목적입니다.
- 문서 index 상단에서 이 개요를 첫 진입점으로 안내합니다.

### Unity 기본 컴포넌트를 팔레트에 추가
- 팔레트를 컴포넌트 레지스트리에서 생성하도록 바꿨습니다. 패널이 각자 들고 있던 하드코딩 목록이 사라졌고, 컴포넌트 한 종을 추가하려면 디스크립터 하나만 등록하면 됩니다.
- **Unity uGUI 스톡 컨트롤 22종**을 추가했습니다. 저장 시 Unity 자신의 `DefaultControls` / `TMP_DefaultControls`를 호출하므로, `GameObject > UI` 메뉴로 만든 것과 같은 계층·참조가 프리팹에 생성됩니다.
- **UI Toolkit 스톡 컨트롤 37종**을 추가했습니다. 각 디스크립터가 UXML 태그를 들고 있어 코드 생성기가 `<ui:DropdownField />` 같은 실제 태그를 씁니다.
- **NexUI 자체 컴포넌트 52종**(선택·입력·내비게이션·데이터·게임 HUD 계열)을 추가해 팔레트 항목이 132종이 되었습니다.
- 계열이 맞지 않는 백엔드에서는 Preview-only로 처리합니다. 캔버스에는 보이고, 저장 리포트가 "이 백엔드에는 쓰지 않았다"고 밝힙니다.
- 새 타입 대부분에 캔버스 프리뷰 렌더러를 붙여, 슬라이더·체크박스·표·트리·탭이 빈 상자가 아니라 형태로 보입니다.
- 요소에 임의 MonoBehaviour를 부착하는 **Add Component**를 추가했습니다. 메타데이터에는 타입 이름만 저장하고, Designer가 붙인 컴포넌트만 추적해 사용자가 프리팹에 직접 붙인 같은 타입 컴포넌트를 지우지 않습니다.

### Panes can be pulled out and docked anywhere
- Each region (Explorer, Inspector, Output) has a **⧉** button that opens it as its own `EditorWindow`. Rather than building a bespoke docking system, the pane becomes a normal editor window — so Unity's docking, tabbing, floating, multi-monitor placement and layout saving all apply for free, and the arrangement survives restarts and layout switches.
- Closing a detached window docks the pane back into the Designer, and the shell re-lays itself out around whatever is currently detached. The canvas is never detachable, since it is what the Designer window is.
- The canvas column instance is preserved across re-layouts, so rearranging panes does not throw away the viewport and its scroll/zoom/guides.
- Detached panes follow the active Designer window's context, so focusing a different screen re-points them instead of leaving a stale view.
- Added `Tools/NexUI/Panels/…` to open any panel directly, including Hierarchy, Library and Project Assets as extra windows, plus `Dock All Back Into Designer` as the way back from a layout the user has lost track of.

### Every pane is now labelled
- Added a one-line caption to each region of the Designer window (sidebar, canvas, inspector, output drawer). The five regions previously looked alike with nothing naming them, which is what made "where is this feature" a hunt.
- The caption states the pane name *and* what the current view is for, so it adds information rather than repeating the tab label directly underneath it: the sidebar reads `EXPLORER — Structure of the open screen` and changes with the active tab, and the canvas caption carries the open screen's id.
- Kept to a single 18px row, and hidden on the output drawer while it is collapsed so it never eats the one row left visible on purpose.

### Layers panel drag-and-drop actually works now
- **Dropping a row onto another row did nothing.** `CompleteDrag` cleared the dragged-element field *before* calling the legality check, and that check bails out when the field is null — so every into/before/after drop was silently refused and only drops on empty space (which skip the check) ever worked. The check now takes the dragged element as an argument.
- Drag initiation no longer depends on pointer capture. The rows live inside a `ScrollView`, and once it takes the pointer for its own scrolling the row stopped receiving move events, so a drag could fail to start at all. The panel now tracks the press and listens for moves itself.
- Rebuilding the row list is suppressed while a drag is live; it previously destroyed the rows the drop target was measured against.
- The "into" zone now owns the middle half of a row (was ~44%), since re-parenting is the main reason to drag here. Dropping into a collapsed container also expands it, so the moved element does not appear to vanish.

### Fixes found while building the above
- Smart guides never snapped one element's edge against its neighbour's opposite edge — only like-for-like edges and centres. Butting two elements together, the most common layout move, therefore did not snap at all. Added the two adjacency pairs on each axis.
- Several EditMode tests read grid size and snapping straight out of `EditorPrefs`, which are shared with whatever was last set in the Designer window. The suite's pass/fail set changed depending on the machine. `DesignerUIStateTests`, `DesignerUndoConsistencyTests` and `NexUIAIServiceTests` now pin known values and restore the user's settings afterwards.

### Canvas ergonomics (follow-up)
- Guides can now be **grabbed and moved**. Each one carries an 11px transparent grab band around its 1px line — a hair-line is effectively unhittable with a mouse, which is why guides were previously create-and-delete only. Hovering thickens the line so the target is visible, and dragging honours Grid Snap.
- Dragging a guide back onto the ruler removes it, alongside the existing `Alt`-click.
- Added **drag-to-reparent on the canvas**: drop an element onto a container and it becomes its child, the same gesture as dropping a row onto another row in Unity's Hierarchy. The target is outlined and named in a drop hint before release, the element keeps its on-screen position, and `Ctrl/Cmd` suppresses re-parenting for a plain move.
- Drop targets exclude the dragged elements and their descendants (which would detach a branch), hidden and locked elements, and component types that accept no children. The deepest container under the cursor wins, with draw order breaking ties.

### Canvas ergonomics
- Added rulers along the top and left of the canvas. Tick spacing adapts to zoom and always lands on round numbers (1/2/5 × 10ⁿ), and both rulers track the pointer so the current X/Y is readable while placing.
- Added drag-out guides: pull from a ruler to place one, `Alt`-click to delete one, click the ruler corner to clear all. Elements snap their edges and centre to guides, and guides take priority over element edges because a guide is an explicit decision.
- Added `Space`-drag and middle-mouse panning that keep the current tool and selection, so panning no longer means switching to the Hand tool and back.
- `Ctrl/Cmd`+wheel now zooms around the pointer instead of the canvas origin, so the element under the cursor stays put.
- Added a Transform Bar under the canvas toolbar with live, editable X/Y/W/H for the selection (label-drag scrubs). Nudging a rect no longer requires a round trip to the Inspector. Multi-selection shows the union read-only rather than pretending four fields can edit many rects.
- Guides are stored per metadata asset in `EditorPrefs`, like zoom and scroll — they stay out of Git diffs and need no schema migration.

### Documentation accuracy pass
- Corrected `developer/metadata-schema.md`, which still claimed `CurrentSchemaVersion` was 1 (it is 4), and documented what every migration step actually changes.
- Corrected `reference/backend-support-matrix.md`, `developer/serialization.md` and `reference/troubleshooting.md`, which still described uGUI saving as name-based matching. It has been stable-id-first with a name fallback since Phase 0.
- Corrected the claim that UI Toolkit save never rewrites UXML: it does regenerate `.uxml`/`.uss` when the target carries the generated marker.
- Fixed `developer/testing.md`, which showed `-runTests` combined with `-quit` — that can terminate the editor before results are written. Added a junction-based recipe for running tests while the project is open in the editor.
- Documented the 9 validation codes that existed in code but were missing from the catalog; the catalog now covers all 75. `developer/api-reference.md` no longer duplicates a partial list and points at the catalog instead.
- Rewrote `developer/project-structure.md` against the actual folder layout (it was missing 8 directories, including `Editor/Properties` and `Editor/Components/Definitions`).
- Marked `RepositoryArchitectureAudit.md` and `RiskReport.md` as point-in-time Phase 0 records so their stale findings are not read as current state.
- Added reusable components and the Assets panel to the concepts, terminology, feature status, known limitations, parity matrix, workflows, canvas, inspector and architecture docs.

### Assets panel
- Added a Project-window-style **Assets** tab to the Designer sidebar: folder navigation with breadcrumbs, recursive search, kind filtering (Image/Font/Material/Prefab/UXML/USS/Asset), thumbnails, click-to-ping and double-click-to-open. This replaces the placeholder tab that previously only had a "Show Project Assets" button.
- Added asset drag-and-drop onto the canvas: a sprite sets an element's image (or creates an Image element on empty canvas), a font or material assigns to the hovered element, and a component definition places an instance. Payloads with no defined behaviour are rejected rather than guessed at, and every drop is a single Undo step.
- Drops from Unity's own Project window work identically, since both paths use `UnityEditor.DragAndDrop`.
- The panel stays read-only by design — rename/move/delete/create remain the Project window's job, so their reference-fixup and `.meta` safety rules are not duplicated.

### Reusable components (Phase 3)
- Added `DesignerComponentDefinitionAsset`: a user-authored, versioned component with an element sub-tree, exposed properties, slots and variant axes.
- Added a component *instance* reference on every element (`DesignerElementMetadata.componentInstance`). Instances store a reference plus overrides — never a copy — so editing a definition updates every instance with no propagation pass.
- Added `DesignerComponentExpander`, which flattens instances for Preview, both backend serializers, Save Preview and Validation. The expansion is an in-memory throw-away asset and is never written back to authored data.
- Generated element ids are `{instanceId}--{definitionElementId}` and generated stableIds are derived deterministically from the instance and definition, so uGUI prefab objects reconnect across saves instead of being recreated.
- Added `DesignerPropertyApplier`, completing the Phase 1 typed-property model with apply/read against element metadata. Properties with no authored representation are reported, never silently ignored.
- Added `DesignerComponentService` for create-from-selection, instantiate, override set/reset, detach, swap and update-from-definition — all Undo-aware, with destructive operations reporting exactly what they drop.
- Added `DesignerComponentLibrary` (project index with search, categories, tags, favourites and usage lookup) and a Component Library window under `Tools/NexUI/Component Library`.
- Added a Component Instance Inspector section for variant selection, per-property override with Reset, and lifecycle actions.
- Added 19 component validation codes covering missing definitions, cycles, slot contracts, override resolution, variant contracts and version mismatch.
- Bumped metadata schema to v4. The v3 → v4 migration is additive and idempotent; no authored value changes.

### Commercial readiness
- Added an in-editor AI Assistant with session/environment API key handling, current-screen context, bounded action-plan validation, explicit approval, destructive-action confirmation, and single-step Undo.
- Replaced the split Design/Prototype/Motion Inspector with one searchable, foldout-based Inspector using workflow filters and Beginner/Pro progressive disclosure.
- Added a public Inspector section registry and compatibility wrapper so Inspector extensions share one rendering path.
- Added Setup Doctor for dependency, project asset, scene backend and writable-path checks.
- Consolidated screen creation under `Tools/NexUI/Designer` and grouped beta graph tools as experimental utilities.
- Added explicit Loaded/Unsaved/Saved and validation state to the Designer toolbar.
- Removed placeholder Assets and Timeline tabs; Unity's Project window and the Motion Clip Editor are now the single entry points.
- Added package manifest documentation links, install-order guidance and a release readiness checklist.

### Productivity
- Added a Korean Screen Creation Wizard that creates connected Screen, Metadata, uGUI Prefab or UXML/USS assets with overwrite protection and rollback.
- Added Motion Clip-based Open/Close transition presets, direct preview, reverse generation and stagger ordering.
- Extended Preview Scenario values with Sprite/List data, scenario navigation/duplicate/reset/delete, and quick Text/Value/Collection edge-case presets.
- Added layout inference, Auto Layout conversion, anchor recommendations, nested-layout warnings and grouped Undo.
- Added actionable Validation fixes for metadata geometry/hierarchy and common uGUI raycast, CanvasGroup and Button issues.
- Completed AnimationClip import/export for supported RectTransform, Transform and CanvasGroup curves, including Editor and Assets menu actions.
- Added Grid Auto Layout serialization for uGUI and UI Toolkit, including column/cell metadata and generated USS wrapping.
- Completed Sprite/List Scenario Timeline editing and preview context capture for resolution, input device and theme.
- Added live Preview Snapshot capture/diff and a configurable Designer shortcut settings window.
- Added first-frame Figma import for hierarchy, coordinates, text, solid fills and Auto Layout with Undo.
- Added `DesignerMotionTriggerRuntime` for backend-neutral Click/Pointer/Focus subscription, lifecycle dispatch, Reduced Motion selection and deterministic disposal.

### Documentation
- Added a Korean AI Assistant guide covering setup, privacy, supported actions, review/apply workflow, costs, limitations, and troubleshooting.
- Expanded Korean onboarding, workflow, Scenario, Motion and troubleshooting guides.
- Added Backend support, asset ownership, validation catalog, compatibility and metadata schema references.
- Fixed outdated documentation links, menu paths and installation guidance.
- Reorganized Korean documentation into Getting Started, User Guide, Motion, Advanced, Tutorials, Reference, and Developer sections.
- Separated current implementation status from the long-term feature specification.
- Added verified Backend, Figma, Migration, Runtime Debug, shortcut, limitation, troubleshooting, extension, and serialization guidance.
- Kept short redirect documents at externally referenced legacy paths.

### Stabilized
- Added a focus-aware `DesignerSessionRegistry` and removed satellite-window context discovery through `Resources.FindObjectsOfTypeAll`.
- Added panel-lifetime event subscriptions so rebuilt/closed VisualElements do not accumulate Context callbacks.
- Persisted screen and element Motion Clip bindings, Reduced Motion alternatives, Motion State Machine and Motion Graph references in Designer metadata.
- Added Motion binding Undo/Redo, element-id reference migration, save synchronization and validation for missing targets/clips and invalid keyframes.
- Added dirty-state handling, Ctrl+S and Undo/Redo preview refresh.
- Restored recent screen, metadata, valid selection and canvas scroll state by asset GUID after reload.
- Avoided constructing `RectOffset` while Unity is serializing metadata during domain reload.
- Added a transactional generated-asset writer for UXML/USS with validation, marker protection, dry run, VCS checkout, rollback and targeted imports.
- Added Session, lifecycle, Motion persistence, Undo consistency, generated-writer and sample smoke EditMode tests plus GitHub Actions EditMode/PlayMode workflow.
- Updated Korean documentation, architecture, implementation status, installation and testing guides.

### Added
- **Motion Clip Editor**: new standalone `Tools/NexUI/Utilities > Motion Clip Editor` window for
  authoring multi-element, multi-property, keyframe-based `UIMotionClip` assets, with a Designer
  selection-linked entry point ("Open Motion Clip Editor") from the Motion inspector. Includes a
  minimal timeline view (draggable keyframes), live preview against the Designer's preview
  surface, and Play/Stop. See `Documentation~/motion/motion-clip-editor.md`.
- `UnityAnimationClipAdapter` (preview an existing `AnimationClip` via `SampleAnimation`) and
  implemented `UIMotionClipImporter`/`UIMotionClipExporter` conversion services.
- Motion Graph Editor: `Tools/NexUI/Utilities > Motion Graph` menu entry so it can be opened
  standalone (with its own Preset picker) instead of only from the Motion inspector; new
  documentation (`Documentation~/motion/motion-graph-editor.md`, previously undocumented); "Auto
  Layout" and "Duplicate Node" context menu actions; brand-new (empty) graphs are now seeded
  with a connected `start`/`end` node pair.
- Shared IMGUI chrome for all `NexUIToolWindow`-based satellite tool windows (header band,
  accent section headers, status badges) driven by an expanded `DesignerColors` token set, so
  their look now tracks the main Designer's dark UI Toolkit theme instead of default Editor
  styling.

### Fixed
- Main Designer window's bottom panel (`State`/`Command`/`Screen Graph` cards) was clipped at a
  fixed 34px/28px height that didn't fit its own content; increased to 64px/56px.
- `MotionGraphWindow` (Motion Graph popout) now applies the shared `NexUIDesigner.uss`
  stylesheet and button classes, matching the rest of the Designer.

### Known limitations
- Motion Clip `AnchoredPosition`/`LocalPosition` currently resolve to the same underlying value.
- Capability-backed Motion triggers subscribe automatically; screen/state/command/enable lifecycle owners call the explicit Binder API.
- AnimationClip conversion skips unsupported curves and exports GameObject paths for direct uGUI playback.
- Figma import does not provide asset image download, component variants or bidirectional sync.

## 0.1.0

- Initial NexUI Designer extension package.
- Added metadata assets, localized Editor window shell, backend abstraction, tools, inspectors, graph panels, serializers, and documentation.
