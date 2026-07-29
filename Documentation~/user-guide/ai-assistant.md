# AI 어시스턴트

NexUI AI 어시스턴트는 Unity Editor 안에서 현재 Designer 화면을 대화로 수정하는 도구입니다. AI가 화면을 직접 무제한 조작하는 방식이 아니라, 현재 Screen/Metadata를 읽어 제한된 작업 계획을 만들고 사용자가 검토한 뒤 적용하는 방식입니다.

## 열기

다음 중 편한 경로를 사용합니다.

- Designer 상단 Toolbar의 **AI**
- `Tools > NexUI > Designer > AI Assistant`
- Utilities의 **AI Assistant**
- Command Palette에서 **Open AI Assistant** 검색

여러 Designer 창을 열었다면 마지막으로 포커스한 Designer의 화면을 사용합니다. 요청을 보낸 뒤 활성 화면이 바뀌면 이전 계획은 적용되지 않습니다.

## API 키 연결

권장 방법은 Unity를 실행하기 전에 선택한 제공자의 API 키 환경변수(`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GEMINI_API_KEY`)를 설정하는 것입니다. Windows PowerShell에서는 새 터미널에서 다음과 같이 사용자 환경변수를 만들 수 있습니다.

```powershell
setx OPENAI_API_KEY "YOUR_API_KEY"
```

설정 후 Unity Hub와 Unity Editor를 다시 시작해야 새 환경변수를 읽을 수 있습니다. 공용 PC라면 환경변수 대신 AI 창의 **Session API key**에 입력하세요.

NexUI의 보안 경계는 다음과 같습니다.

- API 키를 Project Settings, `EditorPrefs`, Unity Asset, 채팅 기록 또는 로그에 저장하지 않습니다.
- Session API key는 창이 살아 있는 동안 메모리에만 있고 창이 비활성화/종료되면 지웁니다.
- API 키는 선택한 공식 제공자의 인증 헤더에만 넣습니다. 임의 엔드포인트는 OpenAI-compatible 연결에서만 사용할 수 있습니다.
- 요청에는 현재 화면 Metadata, 선택 항목, Validation 결과와 최근 대화가 포함됩니다.
- **Include project-wide Agent Handoff manifest**를 켠 경우에만 다른 NexUI 화면과 프로젝트 매니페스트도 포함합니다. 토큰 사용량과 전송 범위가 커질 수 있으므로 기본값은 꺼짐입니다.
- API 요청은 `store: false`로 전송합니다. 조직의 데이터 처리·보존 정책은 사용하는 API 계정 설정과 제공자 정책도 함께 확인해야 합니다.

API 키를 소스 코드나 Git 저장소에 넣지 마세요. 공식 지침은 [OpenAI API key safety](https://help.openai.com/en/articles/5112595-best-practices-for-api-key-safety)를 참고하세요.

## 기본 사용 흐름

1. NexUI Designer에서 Screen과 Metadata를 엽니다.
2. 필요한 Element를 선택합니다. 선택은 AI에게 전달되는 컨텍스트에 포함됩니다.
3. AI 창에 원하는 결과를 자연어로 입력하고 **Send**를 누릅니다. `Ctrl+Enter`도 사용할 수 있습니다.
4. **Proposed changes**에서 실제로 실행될 작업과 Validation 오류를 확인합니다.
5. 이상이 없으면 **Apply with Undo**를 누릅니다. 전체 계획은 하나의 Undo 그룹으로 묶이므로 `Ctrl+Z` 한 번으로 되돌릴 수 있습니다.

예시 요청:

```text
가운데에 420x300 로그인 Card를 만들고 제목, 이메일 Label, 파란색 로그인 Button을 세로 Auto Layout으로 배치해줘.
```

```text
선택한 버튼의 텍스트를 Continue로 바꾸고 #43E6C2 색을 쓰고 accessibilityLabel도 설정해줘.
```

## AI가 할 수 있는 작업

AI 결과는 다음 Designer 명령만 사용할 수 있습니다.

| 명령 | 역할 |
|---|---|
| `create` | 등록된 NexUI Component 생성 |
| `set` | 허용된 Text, Color, Binding, Auto Layout 등의 속성 변경 |
| `set_rect` | 위치와 크기 변경 |
| `reparent` | 부모 변경 또는 Root로 이동 |
| `add_class` / `remove_class` | Style Class 변경 |
| `select` | Element 선택 |
| `delete` | Element와 자식 삭제 |

한 응답은 최대 64개 작업으로 제한됩니다. 존재하지 않는 ID, 중복 ID, 잘못된 Component, 계층 Cycle, 자식을 가질 수 없는 부모, 음수 크기, 허용되지 않은 속성과 AI 접근 범위를 벗어난 대상은 적용 전에 차단됩니다. 삭제 작업이 있으면 추가 확인 창이 열립니다.

AI 출력은 C#이나 Shell을 실행할 수 없고 임의 파일 작성, Package 변경, 외부 프로그램 제어도 할 수 없습니다. 그런 작업이 필요하면 빈 계획과 설명을 반환하도록 지시되어 있습니다.

## 모델과 비용

기본값은 제공자별로 OpenAI `gpt-5.6-sol`, Anthropic `claude-sonnet-5`, Gemini `gemini-3.5-flash`이며 **Connection & context > Model**에서 계정이 사용할 수 있는 모델로 바꿀 수 있습니다. API 사용에는 네트워크 연결, 해당 모델 권한과 제공자별 과금 설정이 필요합니다. 각 소비자용 구독과 개발자 API 과금은 별도일 수 있습니다.

AI 응답은 초안입니다. 적용 전 계획을 확인하고, 적용 후에는 Designer Preview와 Validation을 확인한 다음 Save/Publish하세요. AI가 제안한 배치가 실제 런타임 Backend에서 동일하게 보이는지는 기존 Backend 지원 범위를 따릅니다.

## 문제 해결

### API 키가 없다고 나옵니다

Unity를 완전히 종료한 뒤 선택한 제공자의 환경변수를 설정하고 다시 실행하거나, Session API key를 입력합니다. 키 값 자체는 Console이나 버그 리포트에 붙이지 마세요.

### 401 또는 403 오류가 나옵니다

키가 유효한지, 선택한 모델을 API 프로젝트가 사용할 권한이 있는지 확인합니다. Model 필드를 계정에서 사용 가능한 모델로 바꾼 뒤 다시 요청합니다.

### 계획의 Apply 버튼이 비활성화됩니다

카드 안의 오류를 확인하세요. AI가 존재하지 않는 Element ID 또는 지원하지 않는 속성을 사용했을 수 있습니다. **Discard** 후 더 구체적으로 다시 요청하면 됩니다.

### 원하는 파일이나 C#까지 만들어 주지 않습니다

현재 AI 어시스턴트는 Designer Metadata 편집 전용입니다. 상용 에셋에서 예측 가능한 Undo와 검토 흐름을 유지하기 위해 코드 실행과 임의 파일 변경은 의도적으로 지원하지 않습니다.

## 다중 AI 제공자

**Connection & context > Provider**에서 다음 연결을 선택할 수 있습니다.

- OpenAI Responses API (`OPENAI_API_KEY`)
- Anthropic Claude Messages API (`ANTHROPIC_API_KEY`)
- Google Gemini Generate Content API (`GEMINI_API_KEY`)
- OpenAI-compatible Chat Completions 엔드포인트 (`NEXUI_AI_API_KEY`)

모델과 엔드포인트는 제공자별로 기억하지만 API 키는 저장하지 않습니다. 키는 환경변수 또는 현재 AI 창의 세션 입력값에서만 읽습니다. OpenAI-compatible 연결은 로컬 모델 서버나 사내 게이트웨이를 연결하기 위한 확장 지점이며, 서버가 Chat Completions 형식의 JSON을 반환해야 합니다.

## AI 접근 범위

**AI access scope**에서 제공자에게 전송할 화면 범위와 적용 가능한 액션을 함께 제한합니다.

| 대상 범위 | 의미 |
|---|---|
| Selected Elements | 현재 선택한 요소만 전송하고 수정 허용 |
| Selected Subtree | 선택 요소와 그 하위 요소만 전송하고 수정 허용 |
| Current Screen | 현재 화면 전체를 전송하고 수정 허용 |

권한은 콘텐츠, 레이아웃, 시각 스타일/타이포그래피, 바인딩, 계층, 요소 생성/삭제, 모션, 재사용/부착 컴포넌트, 프로젝트 에셋 생성으로 나뉩니다. `Inspect Only`, `Selected Safe`, `Screen Design`, `Full Designer` 프리셋을 사용하거나 각각 직접 켤 수 있습니다. 삭제는 **Delete Elements** 권한과 **Allow destructive actions**가 모두 켜져야 하며 적용할 때 다시 확인합니다.

## 확장된 Designer 액션

기존 생성·속성·좌표·계층·클래스 액션에 다음 액션이 추가됩니다.

- `set_motion`: 요소의 Motion ID와 initial/animate/exit/hover/pressed/focus variant를 편집합니다.
- `apply_transition`: Fade, Slide, Scale Pop, Modal, Dropdown, Tooltip, Toast, Stagger List 프리셋으로 실제 Motion Clip 쌍을 만들고 화면 진입/종료 모션에 연결합니다.
- `create_motion_clip`: 위치·회전·스케일·크기·알파 트랙과 키프레임/이징을 직접 구성해 진입, 종료 또는 프리뷰 Motion Clip으로 지정합니다.
- `instantiate_component`: NexUI Built-In 레시피 또는 프로젝트의 커스텀 컴포넌트를 배치합니다.
- `set_component_variant` / `set_component_property`: 재사용 컴포넌트가 공개한 variant와 exposed property를 편집합니다.
- `attach_component` / `detach_component`: 해석 가능한 Unity/프로젝트 MonoBehaviour를 uGUI 생성 대상에 연결하거나 제거합니다.
- `set`: Layout Style, Visual Style, Typography를 포함한 넓은 Designer 속성을 편집합니다.

`apply_transition`과 `create_motion_clip`은 프로젝트에 에셋을 만들기 때문에 Motion과 Asset Creation 권한이 모두 필요합니다. 커스텀 클립은 안전한 에셋 이름, 30초 이하 길이, 트랙당 2~64개 및 전체 512개 이하 키프레임으로 제한됩니다. 모든 계획은 최대 64개 액션으로 제한되며, 적용 직전에 현재 화면과 대상 범위를 다시 검증합니다.

예시:

```text
선택한 로그인 카드와 자식만 건드려. 0.3초 SlideUp 진입 모션을 만들고,
버튼 hoverVariant는 HoverLift로 지정한 뒤 모서리와 그림자를 정리해줘.
```
