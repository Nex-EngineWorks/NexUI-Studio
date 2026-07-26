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

권장 방법은 Unity를 실행하기 전에 운영체제의 `OPENAI_API_KEY` 환경변수를 설정하는 것입니다. Windows PowerShell에서는 새 터미널에서 다음과 같이 사용자 환경변수를 만들 수 있습니다.

```powershell
setx OPENAI_API_KEY "YOUR_API_KEY"
```

설정 후 Unity Hub와 Unity Editor를 다시 시작해야 새 환경변수를 읽을 수 있습니다. 공용 PC라면 환경변수 대신 AI 창의 **Session API key**에 입력하세요.

NexUI의 보안 경계는 다음과 같습니다.

- API 키를 Project Settings, `EditorPrefs`, Unity Asset, 채팅 기록 또는 로그에 저장하지 않습니다.
- Session API key는 창이 살아 있는 동안 메모리에만 있고 창이 비활성화/종료되면 지웁니다.
- API 키는 OpenAI Responses API의 Authorization 헤더에만 넣습니다.
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

한 응답은 최대 32개 작업으로 제한됩니다. 존재하지 않는 ID, 중복 ID, 잘못된 Component, 계층 Cycle, 자식을 가질 수 없는 부모, 음수 크기, 허용되지 않은 속성은 적용 전에 차단됩니다. 삭제 작업이 있으면 추가 확인 창이 열립니다.

AI 출력은 C#이나 Shell을 실행할 수 없고 임의 파일 작성, Package 변경, 외부 프로그램 제어도 할 수 없습니다. 그런 작업이 필요하면 빈 계획과 설명을 반환하도록 지시되어 있습니다.

## 모델과 비용

기본 모델은 `gpt-5.6-sol`이며 **Connection & context > Model**에서 계정이 사용할 수 있는 Responses API 모델로 바꿀 수 있습니다. API 사용에는 네트워크 연결, 해당 모델 권한과 API 과금 설정이 필요합니다. ChatGPT 구독과 API 과금은 별도일 수 있습니다.

AI 응답은 초안입니다. 적용 전 계획을 확인하고, 적용 후에는 Designer Preview와 Validation을 확인한 다음 Save/Publish하세요. AI가 제안한 배치가 실제 런타임 Backend에서 동일하게 보이는지는 기존 Backend 지원 범위를 따릅니다.

## 문제 해결

### API 키가 없다고 나옵니다

Unity를 완전히 종료한 뒤 `OPENAI_API_KEY`를 설정하고 다시 실행하거나, Session API key를 입력합니다. 키 값 자체는 Console이나 버그 리포트에 붙이지 마세요.

### 401 또는 403 오류가 나옵니다

키가 유효한지, 선택한 모델을 API 프로젝트가 사용할 권한이 있는지 확인합니다. Model 필드를 계정에서 사용 가능한 모델로 바꾼 뒤 다시 요청합니다.

### 계획의 Apply 버튼이 비활성화됩니다

카드 안의 오류를 확인하세요. AI가 존재하지 않는 Element ID 또는 지원하지 않는 속성을 사용했을 수 있습니다. **Discard** 후 더 구체적으로 다시 요청하면 됩니다.

### 원하는 파일이나 C#까지 만들어 주지 않습니다

현재 AI 어시스턴트는 Designer Metadata 편집 전용입니다. 상용 에셋에서 예측 가능한 Undo와 검토 흐름을 유지하기 위해 코드 실행과 임의 파일 변경은 의도적으로 지원하지 않습니다.
