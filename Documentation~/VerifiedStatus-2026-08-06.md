# 코드 확인 기반 실제 상태 — 2026-08-06

이 문서는 **저장소의 코드를 직접 읽어 확인한 것만** 담습니다. 기존 `FeatureParityMatrix.md`,
`feature-status.md`와 충돌하면 이 문서를 따르되, **테스트는 실행하지 못했으므로** "동작함"은
"코드 경로가 끝까지 이어져 있음"이지 "실행해서 확인함"이 아닙니다.

증거 수준:
- **A** — 해당 코드를 읽고 호출 경로를 끝까지 확인
- **B** — 타입·파일 존재만 확인 (동작 미확인)
- **C** — 부재 확인 (저장소 전역 검색 0건)

---

## 1. 문서가 "없다"고 하지만 실제로는 있는 것

이 절이 이 조사의 핵심입니다. `FeatureParityMatrix.md`가 `Stub`/`Partial`로 적어둔 항목 다수가
실제로는 구현되어 있습니다. **코드가 앞서 있고 문서가 뒤처진 것**입니다.

| 항목 | Matrix 판정 | 실제 | 증거 |
|---|---|---|---|
| Typed binding / converter / two-way | `Stub` (41행) | **구현됨** | `NexUGuiScreenBuilder.cs` 297·429·432·434행에서 `TextConverterKey`/`ValueBindingKey`/`ValueConverterKey`/`ValueBindingMode` 소비. `ResolveConverter` 522행. PlayMode 테스트 `NexValueBindingTests.cs` | A |
| Component motion override | `Stub` | **구현됨** | `DesignerPropertyId` Motion 8종 + `DesignerPropertyApplier`가 전부 처리 | A |
| Component theme override | `Stub` | **구현됨** | `DesignerPropertyId` Theme 4종(`ThemeAsset`/`ThemeId`/`ThemeClasses`/`ThemeTokens`) + Applier 처리 | A |
| Definition 버전 변경 시 Property 자동 재매핑 | 없다고 알려짐 | **구현됨** | `DesignerComponentService.UpdateFromDefinition` — stableId 기반 재해결, 정의 쪽 rename 추적, 신규 variant 축 자동 추가, 미해결 항목 보고. `BackfillDefinitionTargets`로 구식 데이터 보강 | A |
| Instance 크기 변경 시 자식 재배치 | 없다고 알려짐 | **구현됨** | `DesignerInstanceResize.Apply/Resize` + 호출자 `DesignerComponentExpander.cs:291` + `DesignerInstanceResizeTests.cs` | A |
| Interaction Runtime 전체 | 없다고 알려짐 | **구현됨** | 저작 `DesignerInteractionRule` → lowering `NexScreenCompiler.LowerInteractions`(337~) → 컴파일 모델 `NexInteractionProgram`(해시 포함, `NexScreenProgram.cs:167,184`) → 런타임 `NexInteractionRuntime` + `NexCommandRouter` → 배선 `WireInteractionTriggers` → 지연 `NexScreenTicker` → `NexInteractionDelayTests` | A |
| 조건부 Command | 없다고 알려짐 | **구현됨** | `DesignerInteractionRule.conditionKey/comparison/conditionValue`, 런타임 238행에서 상태 조회 | A |
| Event propagation | 없다고 알려짐 | **구현됨** | `DesignerInteractionPhase` Target/Bubble/Capture, `stopPropagation` | A |
| CI 워크플로 | "리포에 없음" | **존재** | 3곳: 프로젝트 루트 + 두 패키지 repo 각각 `.github/workflows/unity-tests.yml` | A |
| Responsive | — | 파일 존재 | `Editor/Advanced/Responsive/ResponsiveService.cs`, `ResponsiveEditorWindow.cs`. **동작 미확인** | B |
| Round-trip diff | `Partial` | 파일 존재 | `Editor/QA/Diff/DesignerDiffService.cs`, `DiffEditorWindow.cs`, `Editor/Advanced/Sync/TextLineDiff.cs`. **필드별 병합 여부 미확인** | B |
| Figma | `Partial` | 파일 존재 | `FigmaApiClient.cs`, `FigmaDocumentImporter.cs`, `FigmaJsonSource.cs`, `FigmaWindow.cs`, `FigmaMenu.cs`, `FigmaCredentials.cs`. **incremental/양방향 미확인** | B |

**정량 지표 하나**: `DesignerPropertyId` 85개 중 `DesignerPropertyApplier`가 처리하는 것이
83개였습니다(오늘 `Gradient` 추가로 84개). 즉 속성 커버리지는 "반쪽"이 아니라 99%였습니다.

---

## 2. 실제로 안 되는 것 (확인됨)

> **2026-08-06 추가 조사.** 아래 "uGUI 컨트롤 커버리지"는 처음에 미확인으로 남겼다가 나중에
> 확인한 것이며, **실제 구멍이었다.** 5절의 "Matrix 판정을 그대로 믿으면 안 됨"이 이쪽에서는
> Matrix가 옳았다는 뜻이기도 하다 — 문서가 낡았다는 사실이 문서가 항상 틀렸다는 뜻은 아니다.

| 항목 | 상태 | 증거 |
|---|---|---|
| **uGUI Dropdown / InputField 런타임 컨트롤** | **구멍이었음 → 2026-08-06 수정.** 컴파일러는 `Dropdown`/`DropdownTMP`/`InputField`/`InputFieldTMP`를 ControlId로 인정하고 capability까지 부여(`NexScreenCompiler.cs:881-884, 923-932`)하는데, `NexUGuiControls.Attach`는 Slider/Scrollbar/Toggle만 처리하고 나머지는 `default: return null`이었음. **바인딩을 걸어도 아무 일도 일어나지 않음** | A |
| **텍스트 양방향 바인딩** | **구멍이었음 → 2026-08-06 수정.** `WireText`가 `TextBindingMode`를 아예 참조하지 않았음(검색 0건). 입력 필드에 상태를 채울 수는 있으나 **사용자가 친 텍스트가 상태로 돌아갈 경로가 없었음** | A |
| `DesignerPropertyId.Texture` | 필드·렌더러·사용처 전부 없음. 죽은 열거값 | C |
| 성능 벤치마크 | `Tests/`에 `Stopwatch`/`Benchmark` 0건 | C |
| UI Toolkit **컴파일 빌더** | `NexUGuiScreenBuilder` 대응물 없음. UI Toolkit은 legacy 경로(`UIToolkitScreenFactory`)와 codegen만 | C |
| Designer UI Toolkit 프리뷰의 벡터 | `UIToolkitDesignerBackend`가 `DesignerElementMetadata`를 받지 않음(`DesignerElementCreateInfo`만) → 셰이프 전달 경로 없음 | A |
| 표준 태그 모드 UXML의 shape | 패스를 그리는 표준 UXML 태그가 없음. 근본 제약이며 커스텀 요소 모드에서만 출력 가능 | A |

---

## 3. 2026-08-06에 고친 것 (그 전까지 실제로 깨져 있었음)

| # | 문제 | 영향 |
|---|---|---|
| 1 | `Unity.VectorGraphics`를 "빌트인, 어디에나 있음"으로 오판 | **2022.3에서 벡터 기능 전체가 컴파일된 적 없음.** `UnityEngine.VectorGraphicsModule.dll`은 Unity 6 전용 |
| 2 | `vectorShape`가 `[SerializeReference]` | `JsonUtility` 미지원 → **복제·붙여넣기·companion JSON에서 패스 소실**. 오류 없이 조용히 |
| 3 | 프리팹 저장 경로에 벡터 없음 | `UGUIAssetSerializer`가 `NXVectorGraphic`을 몰랐음 → **펜으로 그린 도형이 저장 시 사라짐** |
| 4 | asmdef 검사기가 전이 참조 인정 | Unity는 전이 참조를 안 줌 → Studio Editor가 `Designer.Runtime` 경유로 `Vector`를 쓰다 batchmode에서 깨짐 |
| 5 | 루트 `Tools/`와 루트 워크플로가 git 밖 | 프로젝트 루트가 git repo가 아님 → **검증 도구 3개와 CI 정의가 GitHub에 도달 불가** |
| 6 | 패키지 워크플로가 `swallow-smoke/NexUI` 체크아웃 | 실제 remote는 `Nex-EngineWorks/NexUI` → 해당 job이 체크아웃에서 사망 |
| 7 | 저장소 정체성 3갈래 | remote=`Nex-EngineWorks/*`, package.json/README=`OffByJun/*`, 워크플로=`swallow-smoke/*` |
| 8 | `Gradient` override 미연결 | 필드·인스펙터·프리팹 저장은 있는데 Applier만 몰라서 **컴포넌트 override로 지정 불가** |
| 9 | 트리거 3개뿐 | 엔진은 완성인데 깨울 방법이 버튼 클릭 하나. 15개로 확장 |
| 10 | 런타임 `Propagates`가 `OnClick`만 | 컴파일러만 고쳤으면 **컴파일러가 통과시킨 규칙을 런타임이 전파 안 함** |
| 11 | UI Toolkit 벡터 렌더러 없음 | UI Toolkit 백엔드에서 도형이 안 보임 |
| 12 | UXML codegen이 shape 미출력 | 생성된 `.uxml`에 도형이 전혀 안 들어감 |

---

## 4. 이번에 추가한 것

- **펜 툴** (`PenToolOverlay`, `DesignerVectorSpace`) — 좌표 불변식이 핵심
- **Boolean 연산** (`NexPolygonClipper` Martínez-Rueda, `NexPathFlattening`, `NexVectorBoolean`)
- **도형 프리셋 11종 + SVG 임포트** — `NexShapeFactory`/`NexSvgImporter`가 그전까지 테스트에서만 호출됨
- **Interaction 트리거 12종** — pointer 4, submit/cancel, longpress/doubleclick, drag 4
- **드래그 시각 피드백** (`NXInteractionDragRelay`, MoveSelf/Ghost)
- **UI Toolkit 벡터** (`NXVectorElement`) — `Painter2D` 사용으로 **모듈 없이 2022.3에서도 동작**
- **SVG path 텍스트** (`NexVectorPathText`) — UXML이 패스를 담을 수 있게 함
- **공유 적용기** (`NexUGuiShapeApplier`) — 컴파일 빌더와 프리팹 라이터가 같은 규칙 사용

---

## 5. 확인하지 못한 것

**전체 테스트를 한 번도 실행하지 못했습니다.** 이번 세션에서 새로 작성한 테스트만 60개이고,
전부 미검증입니다. 특히 위험한 것:

- `NexVectorBooleanTests` (20개) — 스윕라인 클리퍼는 컴파일된다고 맞는 게 아님
- `NXInteractionDragRelay` 런타임 동작 — 고스트 복제, 레이캐스트 차단은 PlayMode에서만 확인 가능
- 기존 실패 5건(`Condition_16` 포함)의 현재 상태

그 외 미확인:
- Responsive override 실제 동작
- Diff의 필드별 병합 지원 여부
- Figma incremental / 양방향 sync
- uGUI 컴포넌트별 지원률(Toggle/Slider/Scroll/Modal/Toast 등) — Matrix가 `Partial`이라 하나
  개별 확인하지 않음. **위 1절의 전례를 보면 Matrix 판정을 그대로 믿으면 안 됨**

---

## 6. 문서 작업 시 권고

1. **`FeatureParityMatrix.md` 41행 `Typed binding/converter/two-way`를 먼저 고칠 것.** 이 한 줄이
   외부 분석에서 "Binding이 미구현"이라는 결론을 만들어냈습니다.
2. 판정을 고치기 전에 **해당 코드 경로를 직접 확인할 것.** 이 문서의 1절 전체가 "Matrix를 믿고
   코드를 안 본" 결과 나온 오판입니다.
3. `ArchitectureAssessment.md`가 **두 벌로 갈라져 있습니다** — 루트(git 밖, 8/5, 16섹션)와
   Studio 패키지(git 안, 8/4, 7섹션). 고유 내용이 각각 719줄 / 275줄이라 기계적 병합은 위험합니다.
