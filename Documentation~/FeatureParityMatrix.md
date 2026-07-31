# Feature Parity Matrix

`Yes`는 현재 코드에 실제 end-to-end 경로가 확인된 경우만 사용한다. `Partial`은 일부 Backend 또는 일부 속성만 동작함을 뜻한다.

최종 갱신: Phase 3(재사용 Component)와 Assets 패널 반영. 상태 값의 의미는 아래 [판정 원칙](#판정-원칙)을 따른다.

> 재사용 Component의 `Runtime` 열이 `N/A`인 이유: Component는 저장 전에 평탄화되어 일반 Element로 Backend에 나가므로 별도의 Runtime 실행 경로가 없다.

| Feature | Authoring | Preview | uGUI | UI Toolkit | Runtime | Validation | Tests | Status |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Screen create/open/save | Yes | Yes | Yes | Partial | Yes | Yes | Yes | Beta |
| Save/Discard document session | Yes | Yes | N/A | N/A | N/A | Partial | Yes | Beta |
| Undo/Redo | Yes | Yes | N/A | N/A | N/A | No | Yes | Beta |
| Deep duplicate/copy/paste | Yes | Yes | N/A | N/A | N/A | No | Partial | Partial |
| Hierarchy/reparent/sibling order | Yes | Yes | Yes | Yes | N/A | Yes | Yes | Beta |
| Editor/runtime visibility split | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Beta |
| Stable element identity | Yes | N/A | Yes | Name ID | Yes | Yes | Yes | Beta |
| Companion JSON round trip | Yes | N/A | N/A | N/A | N/A | No | Yes | Beta |
| Generated asset transaction/rollback | Yes | N/A | N/A | Yes | N/A | Partial | Yes | Beta |
| Component descriptor registry | Yes | Yes | Partial | Partial | Partial | Yes | Yes | Partial |
| Reusable component definition/instance | Yes | Yes | Yes | Yes | N/A | Yes | Yes | Beta |
| Component slot routing | Yes | Yes | Yes | Yes | N/A | Yes | Yes | Beta |
| Component exposed property override | Yes | Yes | Yes | Yes | N/A | Yes | Yes | Beta |
| Component variant (bool/enum/string) | Yes | Yes | Yes | Yes | N/A | Yes | Yes | Beta |
| Component detach/swap | Yes | Yes | Yes | Yes | N/A | Yes | No | Beta |
| Component version reconcile | Yes | N/A | N/A | N/A | N/A | Yes | Partial | Partial |
| Component motion/theme/responsive override | No | No | No | No | No | No | No | Stub |
| Label/Image/Button | Yes | Yes | Yes | Partial | Partial | Yes | Partial | Partial |
| Toggle/Checkbox/Radio/Slider | Partial | Partial | Partial | Partial | Partial | Partial | No | Partial |
| Progress/Stat/Radial fill | Yes | Yes | Partial | Partial | Partial | Partial | Partial | Partial |
| CollectionView (List/Grid/preset 공통) | Yes | Partial | Yes | Yes | Yes | Yes | Yes | Beta |
| Scroll/Choice | Yes | Yes | Partial | Partial | Partial | Partial | No | Partial |
| Modal/Tooltip/Toast | Yes | Yes | Partial | Partial | Yes | Partial | Partial | Partial |
| Auto Layout Row/Column/Grid | Yes | Yes | Yes | Yes | N/A | Partial | Yes | Beta |
| Constraints/anchors | Yes | Yes | Partial | Partial | N/A | Partial | Yes | Partial |
| Typed property registry/value | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Beta |
| Layout style: min/max/pivot/transform/margin/aspect/overflow | Yes | Yes | Partial | Yes | Partial | Yes | Yes | Beta |
| Visual style: opacity/border/radius/shadow/outline/mask/image | Yes | Yes | Partial | Partial | Partial | Yes | Yes | Partial |
| Typography: asset/size/style/flow/RTL/effects | Yes | Yes | Partial | Partial | Partial | Yes | Yes | Partial |
| Binding: text/value/visibility/class/command | Yes | Yes | Yes | Yes | Yes | Partial | Yes | Beta |
| Typed binding/converter/two-way | No | No | No | No | Partial | No | No | Stub |
| Interaction event authoring | Partial | Partial | Partial | Partial | Partial | Partial | Partial | Partial |
| Focus/default/trap/restore | Yes | Partial | Yes | Yes | Yes | Yes | Yes | Beta |
| Motion Clip | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Beta |
| Motion Graph/State Machine | Yes | Yes | Common | Common | Partial | Yes | Yes | Beta |
| Responsive rules | Yes | Yes | Common | Common | Yes | Yes | Yes | Beta |
| Variant overrides | Yes | Yes | Common | Common | Yes | Yes | Yes | Beta |
| Theme/token | Yes | Yes | Partial | Partial | Yes | Partial | Yes | Partial |
| Localization preview/runtime | Yes | Yes | Common | Common | Partial | Yes | Partial | Partial |
| Accessibility audit | Yes | Yes | Partial | Partial | Partial | Yes | Yes | Partial |
| Scenario preview | Yes | Yes | N/A | N/A | N/A | Partial | Yes | Beta |
| In-window asset browser | Yes | N/A | N/A | N/A | N/A | N/A | Yes | Beta |
| Asset drag & drop to canvas | Yes | Yes | Yes | Yes | N/A | N/A | Yes | Beta |
| Backend dry-run/diff | Yes | N/A | Yes | Yes | N/A | Yes | Yes | Beta |
| Backend round trip | Partial | N/A | Partial | Partial | N/A | Partial | Partial | Partial |
| Figma import | Yes | Yes | Common | Common | N/A | Partial | Yes | Experimental |
| Incremental Figma sync | No | No | No | No | No | No | No | Stub |
| AI plan/explicit apply | Yes | Yes | N/A | N/A | N/A | Partial | Yes | Experimental |
| Runtime debugger | N/A | N/A | Yes | Yes | Yes | N/A | Partial | Partial |
| CI EditMode/PlayMode | N/A | N/A | N/A | N/A | N/A | N/A | Yes | Beta |
| Package/meta/document validation | N/A | N/A | N/A | N/A | N/A | Partial | Partial | Partial |
| Schema migration | Yes | N/A | N/A | N/A | N/A | Yes | Yes | Beta |

## 판정 원칙

- Palette와 Inspector만 있으면 `Stub` 또는 `Partial`이다.
- Preview만 있으면 `PreviewOnly`다.
- 양 Backend 공통 모델이라도 실제 Backend output/runtime 적용이 없으면 `Complete`가 아니다.
- 자동 테스트가 없는 기능은 최대 `Beta`다.
