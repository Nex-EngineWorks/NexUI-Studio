# Backend 지원 범위

이 표는 Studio에서 보이는 값이 어디까지 저장되는지 구분합니다. **일반 Save**와 UI Toolkit의 **Generate/Publish**는 서로 다른 작업입니다.

| 기능 | Studio Preview | Metadata | uGUI Save | UI Toolkit 일반 Save | Generated UXML/USS | Runtime 확인 필요 |
| --- | --- | --- | --- | --- | --- | --- |
| Position / Size | 지원 | 지원 | 지원 | Metadata만 | 지원 | Auto Layout 충돌 |
| Parent / Layer | 지원 | 지원 | 지원 | Metadata만 | 지원 | Template/Slot |
| Text / Tint | 지원 | 지원 | 지원 | Metadata만 | 지원 | Font/Style 우선순위 |
| Image | 부분 지원 | Preview 참조 | 부분 지원 | Metadata만 | 부분 지원 | Sprite/Texture 연결 |
| Shape | Preview만 | 지원 | 미지원 | Metadata만 | 미지원 | 직접 제작 필요 |
| Progress | 지원 | 지원 | 부분 지원 | Metadata만 | 부분 지원 | Track/Label/애니메이션 |
| Button | 지원 | 지원 | 지원 | Metadata만 | 지원 | Event 연결 |
| Binding / Command Key | 지원 | 지원 | Metadata만 | Metadata만 | Metadata만 | Runtime Registry 필수 |
| CollectionView | 부분 지원 | 지원 | 지원 | Metadata만 | 지원 | Item Source 연결 필요 |
| Auto Layout | 지원 | 지원 | 부분 지원 | Metadata만 | 부분 지원 | Backend별 계산 차이 |
| Constraints | 지원 | 지원 | 부분 지원 | Metadata만 | 부분 지원 | 해상도별 확인 |
| Theme | 부분 지원 | 지원 | Metadata만 | Metadata만 | 부분 지원 | Runtime Theme 연결 |
| Motion | 지원 | Asset 참조 | Metadata만 | Metadata만 | Metadata만 | Trigger/Lifecycle 연결 |
| Accessibility | 부분 지원 | 지원 | 부분 지원 | Metadata만 | 부분 지원 | Player/보조 기술 확인 |
| Variant / Responsive | 부분 지원 | 지원 | 부분 지원 | Metadata만 | 부분 지원 | 실제 해상도 확인 |
| Scenario | 지원 | 별도 Asset | 해당 없음 | 해당 없음 | 해당 없음 | Preview 전용 |
| Preview State | 지원 | Preview만 | 해당 없음 | 해당 없음 | 해당 없음 | Runtime 상태와 별개 |
| 재사용 Component | 지원 | 참조 + Override | 전개 후 저장 | 전개 후 Metadata | 전개 후 지원 | 전개된 Element 기준 |

## uGUI Save

Backend Asset은 Prefab이어야 합니다. Serializer는 **Stable ID를 우선**으로 대상 Object를 찾습니다. Studio가 만든 Object에는 Stable Identity Tag가 붙어 있고, Tag가 없는 기존 Prefab은 Element 이름으로 fallback 매칭한 뒤 찾은 Object에 Tag를 붙입니다. 그래서 Object 이름을 바꿔도 연결이 유지됩니다.

연결된 대상에 Rect, 계층, 일부 Graphic/Text/Button/Auto Layout을 반영합니다. 지원하지 않는 컴포넌트나 Preview 전용 값은 Save Report의 **Skipped** 또는 Warning으로 남습니다. Prefab 안에서 Stable ID가 중복되면(`duplicate-prefab-stable-id`) 해당 Element는 적용하지 않고 오류로 보고합니다 — 잘못된 Object에 쓰는 것보다 안전하기 때문입니다.

## UI Toolkit 일반 Save

일반 Save는 Metadata와 Screen Definition을 저장하지만, 사용자가 UI Builder에서 만든 UXML을 임의로 다시 쓰지 않습니다. UXML의 named `VisualElement`와 Metadata Element ID 불일치를 검사합니다. 즉, 일반 Save만 누르고 수동 UXML 구조가 바뀔 것으로 기대하면 안 됩니다.

예외가 하나 있습니다. 대상 UXML이 **Generated Marker를 가진 파일**이면 일반 Save가 `.uxml`/`.uss`를 다시 생성합니다. Studio가 만든 생성물이므로 안전하며, Marker가 없는 사용자 파일에는 해당하지 않습니다.

## Generated UXML/USS

Generate/Publish는 Metadata에서 `.g.uxml`과 `.g.uss`를 만듭니다. Generated Marker가 없는 사용자 파일은 덮어쓰지 않습니다. 생성 결과가 같으면 파일을 다시 쓰지 않습니다. 지원되지 않거나 부분 지원인 값은 Diff와 결과 메시지를 확인하세요.

## 생산성 기능의 Backend 지원

| 기능 | uGUI | UI Toolkit | 비고 |
|---|---:|---:|---|
| 화면 생성 마법사 | 지원 | 지원 | Prefab / UXML 생성 |
| 전환 프리셋 | 지원 | 지원 | 공통 Motion Clip 모델 |
| Preview Scenario | 지원 | 지원 | Studio Preview 데이터 |
| Auto Layout 변환 | 지원 | 지원 | Layout Group / Flex 변환 |
| Grid 자동 변환 | 지원 | 지원 | GridLayoutGroup / Flex Wrap |
| Anchor 추천 | 지원 | 지원 | 공통 Metadata |
| Metadata Validation/Fix | 지원 | 지원 | 공통 규칙 |
| Graphic/CanvasGroup Fix | 지원 | 해당 없음 | uGUI Prefab 전용 |
| Generated UXML 구조 저장 | 해당 없음 | 지원 | Marker 있는 UXML/USS만 트랜잭션 갱신 |

지원하지 않는 Backend 작업은 숨기지 않고 비활성화하거나 Validation 안내로 이유를 표시하는 것을 원칙으로 합니다.

## Runtime 확인이 필요한 이유

Studio는 Key와 에셋 참조를 저장합니다. 실제 데이터, Command, Screen Lifecycle, Theme, 접근성 동작은 Runtime 코드와 Backend Capability가 공급합니다. 따라서 두 Backend 모두 Play Mode 검증이 최종 단계입니다.

- [uGUI Backend](../user-guide/ugui-backend.md)
- [UI Toolkit Backend](../user-guide/ui-toolkit-backend.md)
- [Asset Ownership](asset-ownership.md)

