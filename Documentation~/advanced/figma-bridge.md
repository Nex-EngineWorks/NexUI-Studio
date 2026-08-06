# Figma Bridge

**현재 상태: Beta — JSON 붙여넣기 Import(기본), REST API Import(선택)**

Figma Bridge는 `Tools > NexUI > 유틸리티`에서 **Figma Bridge**를 선택해 엽니다.
가져오기 경로는 두 가지이고, **JSON 붙여넣기가 기본**입니다.

## 1. JSON 붙여넣기 (권장, 계정 불필요)

1. Figma에서 가져올 Frame을 선택합니다.
2. Dev Mode에서 **Copy as JSON**으로 복사합니다.
3. Figma Bridge 창의 텍스트 영역에 붙여넣습니다. `.json` 파일을 그 영역에 끌어다 놓거나
   **파일에서 불러오기**를 눌러도 됩니다.
4. **검사**를 눌러 무엇으로 인식되었는지 확인한 뒤, **현재 Designer로 가져오기**를 누릅니다.

토큰도, 네트워크 연결도, Figma 계정 연동도 필요 없습니다.

인식하는 JSON 형태:

| 형태 | 출처 |
| --- | --- |
| 노드 객체 하나 | Dev Mode **Copy as JSON** |
| 노드 배열 | 여러 개를 한 번에 복사한 경우 (첫 번째만 사용, 개수를 알려줌) |
| `{ "document": ... }` | REST `GET /v1/files/{key}` |
| `{ "nodes": { ... } }` | REST `GET /v1/files/{key}/nodes` |

## 2. REST API (선택)

Personal Access Token이 필요합니다. 창 하단의 **Figma REST API로 가져오기**를 펼쳐서 사용합니다.
가져온 JSON은 위 텍스트 영역에 채워지므로, 검토와 가져오기 단계는 붙여넣기와 완전히 동일합니다.

> [!WARNING]
> Figma의 요청 한도는 **파일 소유자의 Figma 요금제**에 부과됩니다. 무료 플랜 파일은 월 요청 수가
> 매우 적어서, 정기적으로 가져올 계획이라면 이 경로는 실용적이지 않습니다. JSON 붙여넣기를 사용하세요.

Token은 프로젝트 파일이 아닌 로컬 EditorPrefs에 저장되지만, 운영 계정의 장기 Token 사용은 피하고
화면 공유·로그·버그 보고에 포함하지 마세요.

## 변환 범위

두 경로 모두 **같은 변환기**를 사용하므로 결과가 갈리지 않습니다.

변환하는 것: 계층, 좌표(`absoluteBoundingBox` 또는 노드의 `x`/`y`/`width`/`height`), Text,
Solid Fill, Auto Layout(방향·간격·padding).

> [!WARNING]
> Component Variant, Effect, Image 다운로드, Design Token과 양방향 Sync는 지원하지 않습니다.
> Import는 Backend Asset을 즉시 쓰지 않으므로 Studio Validation과 Save Report를 확인한 뒤 저장하세요.
> 기존 Element 교체는 Undo할 수 있습니다.

Figma JSON으로 인식되지 않으면 가져오기는 **실행되지 않고** 현재 화면도 그대로 유지됩니다.
