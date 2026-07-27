# Assets 패널

상태: **Beta**. 탐색·검색·필터·드래그가 모두 동작합니다. Unity Project 창을 대체하지는 않습니다.

Designer 왼쪽 사이드바의 **Assets** 탭에서 프로젝트 Asset을 찾아 캔버스로 바로 끌어다 놓을 수 있습니다.
Sprite 하나 고르려고 Designer 창을 떠날 필요가 없어집니다.

## 화면 구성

```text
[↑]  [ 검색 ]  [ All ▾ ]     ← 상위 폴더 / 검색 / 종류 필터
Assets › UI › Icons          ← Breadcrumb (클릭하면 이동)
─────────────────────────
▸  Icons                     ← 폴더 (클릭하면 진입)
▣  hero.png
T   Roboto.ttf
◈  Card.asset
─────────────────────────
Assets/UI 안에 12개               ← 상태 표시
```

| 조작 | 결과 |
|---|---|
| 폴더 클릭 | 해당 폴더로 이동 |
| Breadcrumb 클릭 | 상위 경로로 이동 |
| `↑` | 상위 폴더 (Assets에서 멈춤) |
| Asset 클릭 | 선택 + Project 창에서 Ping |
| Asset 더블클릭 | `AssetDatabase.OpenAsset` (기본 에디터로 열기) |
| Asset 드래그 | 캔버스나 Inspector의 ObjectField로 끌어다 놓기 |

검색어를 입력하면 **현재 폴더 아래를 재귀 검색**하고, 결과 행에 소속 경로가 함께 표시됩니다.
결과는 300개에서 잘립니다(프로젝트 전체 검색에서도 창이 멈추지 않도록).

종류 필터는 `All / Image / Font / Material / Prefab / UXML / USS / Asset`입니다.
**폴더는 필터와 무관하게 항상 표시됩니다** — 그렇지 않으면 폴더 안의 Asset에 도달할 수 없기 때문입니다.

## 캔버스로 드래그

| 끌어다 놓은 것 | 놓은 위치 | 결과 |
|---|---|---|
| Sprite / Texture2D | Element 위 | 그 Element의 이미지로 설정 |
| Sprite / Texture2D | 빈 캔버스 | 해당 Sprite 크기의 **Image Element 생성** |
| Font / TMP Font Asset | Element 위 | Typography 폰트로 설정 |
| Material | Element 위 | Visual Style 머티리얼로 설정 |
| Component Definition | 아무 곳 | **Component Instance 배치** |
| 그 외 | — | **거부** (커서에 금지 표시) |

드래그 중에는 커서 옆에 무슨 일이 일어날지 문구가 뜹니다(예: `Set 'hero' as 'iconImage' image`).
정의된 동작이 없는 Asset은 추측해서 처리하지 않고 명시적으로 거부합니다.

모든 드롭은 **Undo 한 단계**로 되돌아갑니다.

> Texture2D를 끌어다 놓으려면 Texture Type이 **Sprite**여야 합니다.
> Sprite sub-asset이 없으면 Preview Log에 이유가 남고 아무것도 바뀌지 않습니다.

Unity Project 창에서 직접 끌어다 놓아도 똑같이 동작합니다 — 둘 다 `UnityEditor.DragAndDrop`을 쓰기 때문입니다.

## 하지 않는 것

이 패널은 **읽기 전용 선택 도구**입니다. 다음은 의도적으로 넣지 않았습니다.

* Rename / Move / Delete / Create
* 폴더 트리 2단 뷰

이런 작업은 Unity Project 창의 안전 규칙(참조 갱신, 되돌리기, .meta 처리)에 의존합니다.
Designer에서 다시 구현하면 그 규칙까지 복제해야 하고, 어긋나면 Asset이 깨집니다.
파일을 정리할 때는 Project 창을 쓰세요.

## 알려진 제한

* Thumbnail은 `AssetPreview`가 비동기로 생성합니다. 처음 열 때 잠깐 mini icon이나 글리프로 보이다가 채워집니다.
* Grid(타일) 보기가 없습니다. 사이드바 폭 기준으로 리스트 한 종류만 제공합니다.
* 다중 선택 드래그를 지원하지 않습니다. 한 번에 하나입니다.
* 즐겨찾기/최근 항목이 없습니다. Component는 [Component Library](../advanced/reusable-components.md) 창에 즐겨찾기가 있습니다.
