# NexUI Studio 목표 기능 명세

> [!IMPORTANT]
> 이 문서는 장기 제품 목표입니다. 현재 구현과 Backend 지원 범위는 [기능 상태](feature-status.md)를 기준으로 확인해 주세요.

> **스튜디오가 Unity UI를 직접 설계하고, 연결하고, 움직이고, 검증할 수 있도록 만드는 통합 UI 제작 환경**

NexUI Studio는 화면 배치만 수행하는 단순 UI 에디터가 아닙니다.  
화면 구성, 재사용 컴포넌트, 데이터 연결, 상호작용, 모션, 반응형 대응, 접근성, 런타임 디버깅까지 하나의 작업 흐름으로 연결하는 것을 목표로 합니다.

> [!NOTE]
> 이 문서는 **NexUI Studio가 제공해야 하는 목표 기능 범위**를 정리한 제품 기능 명세입니다.  
> 실제 구현 여부와 개발 우선순위는 별도의 로드맵 또는 이슈 트래커에서 관리합니다.

## 문서 정보

| 항목 | 내용 |
| --- | --- |
| 대상 독자 | UI/UX 스튜디오, 테크니컬 스튜디오, Unity UI 개발자 |
| 문서 목적 | NexUI Studio가 지원하는 전체 기능과 작업 흐름을 쉽게 파악하기 위함 |
| 주요 출력 대상 | Unity uGUI, Unity UI Toolkit |
| 핵심 작업 방식 | 시각적 편집, 재사용 컴포넌트, 데이터 Binding, Timeline, Motion Graph |
| 문서 범위 | 디자인부터 Preview, Publish, Runtime Debug까지 |

---

## NexUI Studio로 하는 일

| 단계 | 스튜디오가 하는 작업 | 관련 기능 |
| --- | --- | --- |
| 1. 화면 생성 | 새로운 UI 화면을 만들고 기준 해상도와 출력 방식을 정합니다. | 프로젝트 관리 |
| 2. 화면 디자인 | 요소를 배치하고 정렬하며 레이아웃과 스타일을 편집합니다. | Canvas, Hierarchy, Layout, Style |
| 3. 컴포넌트화 | 반복되는 UI를 재사용 가능한 컴포넌트와 Variant로 만듭니다. | Component, Variant |
| 4. 데이터 연결 | 텍스트, 이미지, 값, 목록을 게임 데이터와 연결합니다. | Binding, Collection UI |
| 5. 상호작용 구성 | 클릭, 호버, 드래그, 화면 전환과 명령을 연결합니다. | Interaction, Screen Flow |
| 6. 모션 제작 | Timeline 또는 Graph로 UI 애니메이션과 복합 동작을 만듭니다. | Motion Clip, Motion Graph |
| 7. 조건별 검증 | 해상도, 언어, 입력 장치, 테마, 접근성을 미리 확인합니다. | Preview, Scenario, Responsive, Accessibility |
| 8. Unity에 반영 | uGUI 또는 UI Toolkit 에셋으로 출력하고 런타임에서 검증합니다. | Publish, Live Edit, Runtime Debugger |

---

## 핵심 개념

| 용어 | 의미 |
| --- | --- |
| **Screen** | 메뉴, HUD, 인벤토리처럼 하나의 독립된 UI 화면 |
| **Element** | Text, Image, Button, Container 등 화면을 구성하는 개별 요소 |
| **Component** | 여러 화면에서 재사용할 수 있도록 묶은 UI 구성 |
| **Variant** | 같은 Component의 상태 또는 형태를 바꾸는 선택값 |
| **Backend** | Studio 결과를 실제 Unity UI로 변환하는 출력 방식 |
| **Binding** | 게임 데이터와 UI 속성을 연결하는 기능 |
| **Motion Clip** | Timeline과 Keyframe으로 만드는 시간 기반 애니메이션 |
| **Motion Graph** | Node를 연결해 만드는 조건·이벤트 기반 UI 동작 |
| **Scenario** | 특정 해상도, 데이터, 입력 상황을 재현하는 Preview 환경 |
| **Design Token** | 색상, 간격, 폰트, 모션 속도처럼 공통으로 사용하는 디자인 값 |
| **Publish** | Studio 데이터를 uGUI Prefab 또는 UXML/USS로 생성하는 과정 |

---

## 기능 구성

| 구분 | 범위 | 설명 |
| --- | ---: | --- |
| [Part 1. 시작과 프로젝트 구성](#part-1-시작과-프로젝트-구성) | 1–2 | 화면 생성과 Workspace |
| [Part 2. 화면 디자인과 레이아웃](#part-2-화면-디자인과-레이아웃) | 3–9 | Canvas, Hierarchy, Layout, Style |
| [Part 3. 컴포넌트와 데이터](#part-3-컴포넌트와-데이터) | 10–14 | Component, Variant, Binding, Collection |
| [Part 4. 상호작용 설계](#part-4-상호작용-설계) | 15–18 | Event, Drag & Drop, Tooltip, Context Menu |
| [Part 5. 모션과 시각적 로직](#part-5-모션과-시각적-로직) | 19–31 | Timeline, Motion Graph, Text Motion, VFX |
| [Part 6. 탐색·미리보기·반응형](#part-6-탐색미리보기반응형) | 32–38 | Focus, Preview, Scenario, Responsive, Theme |
| [Part 7. 출력·런타임·품질 관리](#part-7-출력런타임품질-관리) | 39–45 | Backend, Debug, Validation, Search |
| [Part 8. 현지화·확장·유지보수](#part-8-현지화확장유지보수) | 46–51 | Localization, Template, API, Migration, Test |

---

## 명세 읽는 방법

각 기능은 다음 구조로 정리되어 있습니다.

- **기능 설명**: 스튜디오 관점에서 해당 영역이 무엇을 위한 것인지 설명합니다.
- **세부 기능 보기**: 실제 지원 대상 기능을 원문 수준으로 나열합니다.
- 영문 기능명은 에디터 UI 또는 개발 API에서 그대로 사용할 수 있는 명칭입니다.

---

## Part 1. 시작과 프로젝트 구성

화면 생성부터 작업 공간 설정까지를 다룹니다.
**이 Part의 기능:** [1. 프로젝트 관리](#feature-1) · [2. 작업 공간](#feature-2)

<a id="feature-1"></a>

### 1. 프로젝트 관리

UI 화면을 만들고 관리하며, 출력 방식과 관련 에셋을 한곳에서 연결합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 1.1 UI 화면 관리

- 새 UI 화면 생성
- 기존 UI 화면 열기
- UI 화면 복제
- UI 화면 이름 변경
- UI 화면 삭제
- 최근 작업 화면 목록
- 즐겨찾기 화면
- 화면 검색
- 화면 카테고리 분류
- 화면 태그
- 화면별 Backend 선택
- 화면별 기준 해상도 설정
- 화면별 Safe Area 설정
- 화면별 UI Scale 설정
- 화면별 Preview Scenario 설정
- 화면별 기본 Theme 설정
- 화면별 기본 Input Mode 설정

#### 1.2 지원 Backend

- uGUI
- UI Toolkit
- Backend별 Preview
- Backend별 Capability 표시
- Backend별 지원하지 않는 속성 경고
- Backend별 Fallback 표시
- Backend 변경
- Backend 간 화면 변환
- Studio와 Backend 동기화
- Backend 변경 감지
- 동기화 충돌 감지
- 충돌 비교
- Studio 버전 사용
- Backend 버전 사용
- 수동 병합

#### 1.3 에셋 관리

- UIScreenDefinition 연결
- DesignerMetadataAsset 연결
- Motion Clip 연결
- Motion Graph 연결
- Theme 연결
- Component Definition 연결
- Scenario 연결
- Backend Asset 연결
- 누락된 에셋 자동 탐색
- 잘못된 참조 정리
- 에셋 이름 자동 정리
- 연관 에셋 일괄 이동
- 연관 에셋 일괄 복제

</details>

<a id="feature-2"></a>

### 2. 작업 공간

디자인·바인딩·모션·디버깅 등 작업 목적에 맞게 편집 환경을 전환합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 2.1 Workspace

- Design
- Bind
- Prototype
- Animate
- Graph
- Preview
- Debug

#### 2.2 공통 Workspace 상태

- 현재 화면 유지
- 현재 선택 유지
- Zoom 유지
- Scroll 위치 유지
- Preview State 유지
- Input Mode 유지
- 작업 패널 크기 유지
- 패널 표시 상태 저장
- Workspace별 레이아웃 저장
- Workspace 초기화
- 사용자 정의 Workspace 저장

#### 2.3 공통 패널

- Component Library
- Hierarchy
- Canvas
- Inspector
- Align
- Layer
- Binding
- Interaction
- Timeline
- Graph
- Preview
- Validation
- History
- Runtime Debugger

</details>

---

## Part 2. 화면 디자인과 레이아웃

캔버스 편집, 계층, 정렬, 레이아웃, 스타일를 다룹니다.
**이 Part의 기능:** [3. Canvas 편집](#feature-3) · [4. 시각적 편집 도구](#feature-4) · [5. Hierarchy](#feature-5) · [6. 정렬 및 레이어](#feature-6) · [7. Group과 Container](#feature-7) · [8. Layout 시스템](#feature-8) · [9. 스타일 편집](#feature-9)

<a id="feature-3"></a>

### 3. Canvas 편집

캔버스에서 요소를 선택하고 이동·크기 조절·복제·삭제하는 기본 편집 기능입니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 3.1 선택

- 단일 선택
- 다중 선택
- Shift 추가 선택
- Ctrl 선택 토글
- 박스 선택
- 전체 선택
- 선택 해제
- 부모 선택
- 직계 자식 선택
- 모든 자손 선택
- 동일 타입 선택
- 동일 클래스 선택
- 동일 컴포넌트 선택
- 동일 Variant 선택
- 겹친 요소 목록에서 선택
- 선택 요소로 화면 이동
- 선택 기록 복원
- Named Selection 저장

#### 3.2 이동

- 마우스 드래그 이동
- 다중 요소 이동
- 방향키 1px 이동
- Shift+방향키 단위 이동
- 축 고정 이동
- Snap 이동
- 부모 내부 이동
- 부모 밖 이동
- 다른 부모로 Drag 이동
- Layout 컨테이너 내부 순서 변경
- 절대 위치 자식 설정

#### 3.3 크기 변경

- Canvas Handle 크기 변경
- Inspector 수치 입력
- 다중 요소 크기 변경
- 비율 고정
- 중앙 기준 크기 변경
- 한쪽 축 크기 변경
- 최소 크기 제한
- 최대 크기 제한
- Hug Contents
- Fill Container
- Fixed Size

#### 3.4 회전 및 Transform

- Rotation 편집
- Scale 편집
- Pivot 편집
- Anchor 편집
- Anchor Preset
- Transform 초기화
- 위치 초기화
- 크기 초기화
- 회전 초기화
- Scale 초기화

#### 3.5 복사와 생성

- 복사
- 붙여넣기
- 잘라내기
- 복제
- 이동 간격 유지 복제
- Alt+Drag 복제
- 같은 부모에 붙여넣기
- 원래 위치에 붙여넣기
- 스타일만 복사
- Binding만 복사
- Motion만 복사
- 전체 속성 복사

#### 3.6 삭제 및 이름 변경

- 선택 요소 삭제
- 자식 포함 삭제
- 자식 유지 후 부모만 삭제
- 빠른 이름 변경
- 일괄 이름 변경
- ID 자동 생성
- ID Prefix 적용
- ID 중복 자동 해결
- 구조 기반 ID 추천

</details>

<a id="feature-4"></a>

### 4. 시각적 편집 도구

그리드, 가이드, 거리 측정 등 정확한 배치를 돕는 시각적 도구입니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 4.1 Grid

- Pixel Grid
- Column Grid
- Row Grid
- Baseline Grid
- 사용자 정의 Grid
- Grid 표시
- Grid 숨김
- Grid Snap
- Grid 크기 변경
- Grid Offset 변경
- Column 개수
- Column Margin
- Column Gutter
- Column Width
- Stretch Column

#### 4.2 Ruler와 Guide

- 수평 Ruler
- 수직 Ruler
- 수평 Guide
- 수직 Guide
- Ruler에서 Guide 생성
- Guide 이동
- Guide 잠금
- Guide 숨김
- Guide 삭제
- Guide 이름 설정
- Guide Snap
- 전역 Guide
- 화면별 Guide

#### 4.3 Smart Guide

- 부모 중앙 정렬 Guide
- 형제 중앙 정렬 Guide
- Left 정렬 Guide
- Right 정렬 Guide
- Top 정렬 Guide
- Bottom 정렬 Guide
- Text Baseline Guide
- 동일 간격 Guide
- 동일 크기 Guide
- 부모 Padding Guide
- Safe Area Guide
- Anchor Guide
- Smart Guide Snap

#### 4.4 거리 측정

- 부모와의 거리 표시
- 형제 요소와의 거리 표시
- 선택 요소 간 거리 표시
- Padding 표시
- Margin 표시
- Gap 표시
- Width/Height 표시
- Alt 거리 측정 모드

#### 4.5 Canvas 보기

- Zoom In
- Zoom Out
- Zoom Reset
- Fit Screen
- Fit Selection
- 실제 픽셀 보기
- Outline Mode
- Isolation Mode
- Layout Bounds 표시
- Clip Bounds 표시
- Safe Area 표시
- Anchor 표시
- Pivot 표시
- 숨김 요소 표시
- 잠금 요소 표시
- Backend 구조 표시

</details>

<a id="feature-5"></a>

### 5. Hierarchy

화면의 부모·자식 구조와 요소 순서를 트리 형태로 관리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 5.1 계층 관리

- 부모-자식 구조 표시
- Foldout
- Drag로 부모 변경
- Drag로 순서 변경
- Sibling Index 변경
- Parent Slot 변경
- Root 요소 표시
- 계층 검색
- ID 검색
- 이름 검색
- 타입 검색
- 클래스 검색

#### 5.2 상태 제어

- 요소 숨김
- 요소 잠금
- 자식 전체 숨김
- 자식 전체 잠금
- Solo 표시
- 펼치기
- 모두 접기
- 모두 펼치기

#### 5.3 Breadcrumb

- 현재 선택 경로 표시
- Breadcrumb 부모 선택
- Breadcrumb 자식 목록
- Component Instance 내부 경로 표시
- Isolation Mode 진입
- Main Component 이동

</details>

<a id="feature-6"></a>

### 6. 정렬 및 레이어

여러 요소의 위치·간격·크기·앞뒤 순서를 빠르게 정리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 6.1 정렬

- 왼쪽 정렬
- 수평 중앙 정렬
- 오른쪽 정렬
- 위쪽 정렬
- 수직 중앙 정렬
- 아래쪽 정렬
- 부모 기준 정렬
- 선택 Bounds 기준 정렬

#### 6.2 분배

- 수평 균등 분배
- 수직 균등 분배
- 고정 수평 간격
- 고정 수직 간격
- 행 정렬
- 열 정렬

#### 6.3 크기 통일

- 동일 너비
- 동일 높이
- 동일 크기
- 가장 큰 요소 기준
- 가장 작은 요소 기준
- 첫 선택 기준
- 마지막 선택 기준

#### 6.4 Layer

- 한 단계 앞으로
- 한 단계 뒤로
- 맨 앞으로
- 맨 뒤로
- Sibling Index 직접 입력
- Layer 목록
- Z-Index 표시
- Layer 잠금
- Layer 그룹

</details>

<a id="feature-7"></a>

### 7. Group과 Container

여러 요소를 묶거나 Frame·Panel·Container 안에 구성합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 7.1 Group

- 선택 요소 Group
- Ungroup
- Group 이름 설정
- Group Bounds 자동 계산
- Group 해제 시 좌표 유지
- 중첩 Group
- Group 잠금
- Group 숨김

#### 7.2 Frame 및 Container

- Frame 생성
- Panel 생성
- Container 생성
- 선택 요소를 Frame으로 감싸기
- Clip Children
- Content Padding
- Background
- Layout 적용
- Component Slot 적용

</details>

<a id="feature-8"></a>

### 8. Layout 시스템

고정 배치부터 자동 정렬, 반응형 크기 정책까지 화면 구조를 설계합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 8.1 Layout 유형

- Free Layout
- Horizontal Layout
- Vertical Layout
- Grid Layout
- Wrap Layout
- Overlay Layout

#### 8.2 공통 Layout 속성

- Direction
- Reverse
- Alignment
- Justification
- Gap
- Row Gap
- Column Gap
- Padding
- Margin
- Wrap
- Overflow
- Clip
- Grow
- Shrink
- Basis
- Min Size
- Max Size
- Preferred Size

#### 8.3 크기 정책

- Fixed
- Hug Contents
- Fill Container
- Fit Content
- Stretch
- Percentage
- Aspect Ratio
- Min/Max Constraint

#### 8.4 Grid Layout

- Fixed Columns
- Fixed Rows
- Auto Fit
- Auto Fill
- Cell Size
- Min Cell Size
- Max Cell Size
- Row Gap
- Column Gap
- Cell Alignment
- Column Span
- Row Span

#### 8.5 Canvas Layout Handle

- Gap Handle
- Padding Handle
- Margin Handle
- Grid Cell Handle
- Layout 방향 변경
- Alignment 빠른 변경
- 자식 순서 Drag 변경

</details>

<a id="feature-9"></a>

### 9. 스타일 편집

색상, 테두리, 그림자, 이미지, 텍스트 등 요소의 시각 표현을 편집합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 9.1 Fill

- 단색
- 선형 Gradient
- 방사형 Gradient
- 각도 Gradient
- 이미지
- Sprite
- Texture
- 다중 Fill
- Fill 순서 변경
- Fill 활성화
- Fill 불투명도

#### 9.2 Border

- 전체 Border Width
- Side별 Border Width
- Border Color
- Corner별 Radius
- Inside Border
- Center Border
- Outside Border
- Dashed Border
- Dotted Border

#### 9.3 Shadow

- Drop Shadow
- Inner Shadow
- 다중 Shadow
- Offset
- Blur
- Spread
- Color
- Shadow 활성화
- Shadow 순서 변경

#### 9.4 Opacity 및 Effect

- Opacity
- Blur
- Background Blur
- Glow
- Outline
- Grayscale
- Brightness
- Contrast
- Saturation
- Material
- Shader

#### 9.5 Image

- Sprite 선택
- Texture 선택
- Preserve Aspect
- Fit
- Fill
- Crop
- Tile
- Slice
- Nine Slice
- Pixels Per Unit
- UV Offset
- UV Scale
- Flip Horizontal
- Flip Vertical
- Tint
- Fill Amount
- Fill Direction
- Fill Origin
- Clockwise

#### 9.6 Text

- Text 내용
- Localization Key
- Font Asset
- Font Family
- Font Weight
- Font Style
- Font Size
- Auto Size
- Line Height
- Letter Spacing
- Word Spacing
- Paragraph Spacing
- Horizontal Alignment
- Vertical Alignment
- Wrapping
- Overflow
- Rich Text
- Text Color
- Outline
- Shadow
- Gradient

</details>

---

## Part 3. 컴포넌트와 데이터

재사용 컴포넌트, Variant, Binding, Collection를 다룹니다.
**이 Part의 기능:** [10. 컴포넌트 라이브러리](#feature-10) · [11. 사용자 정의 컴포넌트](#feature-11) · [12. Variant](#feature-12) · [13. Binding](#feature-13) · [14. Collection UI](#feature-14)

<a id="feature-10"></a>

### 10. 컴포넌트 라이브러리

자주 쓰는 UI 요소를 검색하고 드래그해 화면에 추가합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 10.1 기본 컴포넌트

- Panel
- Container
- Card
- Modal
- Label
- Image
- Button
- Icon Button
- Choice List
- Toast
- Tooltip
- Popover
- Progress Bar
- Stat Bar
- Radial Fill
- Spinner
- Skeleton
- List
- Grid
- Slot
- Hotbar

#### 10.2 라이브러리 기능

- 컴포넌트 검색
- 카테고리
- 태그
- Thumbnail
- Preview
- 즐겨찾기
- 최근 사용
- Built-in
- Project
- Package
- Custom
- Variant Preview
- Drag & Drop 추가
- Quick Insert
- 삽입 Slot Preview
- Component 교체

#### 10.3 Quick Insert

- 단축키로 검색창 열기
- 이름 검색
- 태그 검색
- 한국어 별칭 검색
- 영어 별칭 검색
- 최근 항목
- 즐겨찾기 항목
- 선택 부모에 추가
- 현재 위치에 추가

</details>

<a id="feature-11"></a>

### 11. 사용자 정의 컴포넌트

프로젝트 전용 UI를 재사용 가능한 컴포넌트로 만들고 관리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 11.1 Component 생성

- 선택 요소에서 Component 생성
- Component 이름 지정
- 카테고리 지정
- 태그 지정
- Thumbnail 생성
- 설명 작성
- Component Library 등록

#### 11.2 Component Definition

- Root
- 내부 Hierarchy
- Slot
- Exposed Property
- Variant
- 기본 Binding
- 기본 Interaction
- 기본 Motion
- 기본 Accessibility
- 기본 Theme
- Preview
- Thumbnail

#### 11.3 Exposed Property

- Text
- Integer
- Float
- Bool
- Color
- Sprite
- Texture
- Font
- Binding Key
- Command
- Motion Clip
- Motion Graph
- Variant
- Enum
- Object
- Element Reference

#### 11.4 Component Slot

- Slot 이름
- 허용 Component 타입
- 최소 자식 수
- 최대 자식 수
- Default Content
- Slot Layout
- Slot Validation
- Slot별 Padding
- Slot별 Clip

#### 11.5 Component Instance

- 원본 Component 연결
- Property Override
- Override 확인
- Override 초기화
- 전체 Override 초기화
- Main Component 이동
- Main Component 변경 반영
- Instance 변경을 원본에 적용
- Component 교체
- Instance 분리
- Nested Component
- 순환 참조 검사

</details>

<a id="feature-12"></a>

### 12. Variant

하나의 컴포넌트를 상태나 용도에 따라 여러 형태로 전환합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 12.1 Variant Property

- Bool
- Enum
- String
- Integer

#### 12.2 Variant 변경 대상

- Hierarchy
- Visibility
- Layout
- Style
- Text
- Image
- Icon
- Binding
- Command
- Motion
- Accessibility
- Slot
- Component

#### 12.3 Variant 기능

- Variant 추가
- Variant 삭제
- Variant 이름 변경
- Variant 조합 생성
- Variant 기본값
- Variant Preview
- Variant 일괄 비교
- Variant 교체
- Variant Transition
- Instance별 Variant 선택

</details>

<a id="feature-13"></a>

### 13. Binding

게임 데이터와 UI 속성을 연결하고, 코드 없이 간단한 표현식을 구성합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 13.1 Binding Channel

- Text Binding
- Value Binding
- Visibility Binding
- Interactable Binding
- Class Binding
- Image Binding
- Color Binding
- Variant Binding
- Collection Binding
- Selection Binding
- Command Binding

#### 13.2 Binding Picker

- State Key 검색
- Command 검색
- 타입 필터
- 현재 화면 Scope
- 전역 Scope
- Item Scope
- Graph Parameter
- Local State
- 최근 Binding
- 즐겨찾기 Binding

#### 13.3 Binding 표시

- 연결 상태 표시
- 현재 Preview 값 표시
- 타입 표시
- Source 표시
- 마지막 갱신 시간
- 잘못된 Binding 경고
- 사용되지 않는 Binding 검사

#### 13.4 Binding Expression

- 사칙연산
- Compare
- Bool AND
- Bool OR
- Bool NOT
- Min
- Max
- Clamp
- Lerp
- Round
- Floor
- Ceil
- String Join
- String Format
- Null Check
- Conditional
- Collection Count
- Any
- All
- Color Lerp
- Vector 연산

#### 13.5 Format Builder

- 문자열 Template
- Named Argument
- 숫자 Format
- 소수점
- 천 단위 구분
- Prefix
- Suffix
- Localization Argument

</details>

<a id="feature-14"></a>

### 14. Collection UI

인벤토리·목록·그리드처럼 반복되는 데이터를 효율적으로 표시합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 14.1 Collection Binding

- Source Binding
- Item Template
- Key Binding
- Selection Binding
- Empty View
- Loading View
- Error View
- Header Template
- Footer Template

#### 14.2 Item Scope

- 현재 Item
- Item Index
- Item Key
- 선택 여부
- 첫 번째 Item 여부
- 마지막 Item 여부
- 홀수/짝수 Index
- Parent Collection 정보

#### 14.3 선택

- 선택 없음
- 단일 선택
- 다중 선택
- 범위 선택
- 선택 해제
- 기본 선택
- 선택 변경 Command

#### 14.4 정렬과 필터

- 단일 정렬
- 다중 정렬
- Ascending
- Descending
- 조건 필터
- Text 검색
- Grouping
- Group Header
- Runtime Sort 변경
- Runtime Filter 변경

#### 14.5 Virtualization

- Fixed Size
- Dynamic Size
- Recycling
- Pooling
- Overscan
- Pool Size
- 가시 영역 계산
- Scroll Position 유지

#### 14.6 Preview

- Preview Item Count
- Mock Item 생성
- Item Scope 값 설정
- Empty Preview
- Loading Preview
- Error Preview
- 선택 Preview

</details>

---

## Part 4. 상호작용 설계

이벤트, 드래그 앤 드롭, Tooltip, Context Menu를 다룹니다.
**이 Part의 기능:** [15. Interaction](#feature-15) · [16. Drag & Drop](#feature-16) · [17. Tooltip](#feature-17) · [18. Context Menu](#feature-18)

<a id="feature-15"></a>

### 15. Interaction

클릭, 호버, 포커스 등의 이벤트에 화면 전환이나 명령 실행을 연결합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 15.1 지원 Event

- Click
- Double Click
- Long Press
- Press
- Release
- Hover Enter
- Hover Exit
- Focus
- Blur
- Submit
- Cancel
- Drag Start
- Drag
- Drag End
- Drop
- Scroll
- Value Changed
- Selection Changed

#### 15.2 기본 Action

- Open Screen
- Close Screen
- Push Screen
- Pop Screen
- Replace Screen
- Toggle Screen
- Dispatch Command
- Set Local State
- Toggle Visibility
- Show Element
- Hide Element
- Set Interactable
- Set Text
- Change Variant
- Play Motion Clip
- Trigger Motion Graph
- Show Tooltip
- Hide Tooltip
- Show Toast
- Set Focus
- Clear Focus
- Scroll Into View
- Play Sound

#### 15.3 Interaction Row

- Event
- Condition
- Action
- Parameters
- Delay
- Once
- Consume Event
- Enabled
- 순서 변경
- 복제
- Graph로 변환

</details>

<a id="feature-16"></a>

### 16. Drag & Drop

요소와 목록 항목을 끌어 이동하거나 슬롯에 배치하는 동작을 설계합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Draggable 설정
- Drag Handle
- Drag Preview
- Drag Ghost
- Drag Offset
- Drag Threshold
- Allowed Drop Group
- Drop Target
- Drop Validation
- Drop Highlight
- Drop 성공 Action
- Drop 실패 Action
- 원위치 복귀 Motion
- Drag 중 Auto Scroll
- Collection Item Drag
- Slot 간 이동
- Pointer 입력
- Touch 입력
- Gamepad 기반 이동

</details>

<a id="feature-17"></a>

### 17. Tooltip

상황에 맞는 설명 UI를 표시하고 위치·내용·등장 모션을 설정합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 17.1 Trigger

- Hover
- Focus
- Long Press
- Manual

#### 17.2 Placement

- Auto
- Top
- Bottom
- Left
- Right
- Cursor

#### 17.3 옵션

- Show Delay
- Hide Delay
- Follow Cursor
- 화면 내부 Clamp
- Safe Area Clamp
- Interactive
- 최대 너비
- Dismiss Policy
- Pointer Offset
- Anchor Offset

#### 17.4 Content

- Plain Text
- Rich Text
- Localization
- Image
- Item Preview
- Stat Block
- Key Hint
- 비교 UI
- Custom Prefab
- Custom UXML

#### 17.5 Motion

- 등장 Motion
- 퇴장 Motion
- Anchor 변경 Motion
- Tooltip Graph

</details>

<a id="feature-18"></a>

### 18. Context Menu

마우스 우클릭이나 게임패드 입력으로 열리는 메뉴를 구성합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- 우클릭 Trigger
- Gamepad Menu Trigger
- 메뉴 항목
- Icon
- Label
- Shortcut
- Command
- 조건부 표시
- 조건부 활성화
- Separator
- Submenu
- Tooltip
- 선택 Item Scope
- 외부 클릭 닫기
- Esc 닫기
- Focus Navigation

</details>

---

## Part 5. 모션과 시각적 로직

Timeline, Motion Graph, 텍스트 모션, Audio/VFX를 다룹니다.
**이 Part의 기능:** [19. Motion Clip](#feature-19) · [20. Motion 실시간 Preview](#feature-20) · [21. Record와 Auto Key](#feature-21) · [22. Motion Path 및 Onion Skin](#feature-22) · [23. Motion Layer](#feature-23) · [24. Motion State Machine](#feature-24) · [25. Motion Graph](#feature-25) · [26. Graph 대상 선택](#feature-26) · [27. Graph Blackboard](#feature-27) · [28. Graph 구조화](#feature-28) · [29. Graph Preview와 Debug](#feature-29) · [30. 텍스트 Motion](#feature-30) · [31. Audio, Haptic, VFX](#feature-31)

<a id="feature-19"></a>

### 19. Motion Clip

타임라인과 키프레임을 이용해 정밀한 UI 애니메이션을 제작합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 19.1 Clip 관리

- Motion Clip 생성
- 이름 변경
- 복제
- 삭제
- Duration
- FPS
- Loop
- Ping-Pong
- Playback Speed
- Time Scale 사용 여부
- Reduced Motion Clip
- Preview Target

#### 19.2 트랙 유형

**Transform**

- Anchored Position
- Local Position
- Rotation
- Scale
- Size
- Pivot
- Anchor Min
- Anchor Max

**Visual**

- Opacity
- Background Color
- Text Color
- Border Color
- Border Width
- Border Radius
- Fill Amount
- Blur
- Brightness
- Saturation
- Material Float
- Material Color
- Material Vector

**Layout**

- Width
- Height
- Padding
- Margin
- Gap
- Flex Grow
- Flex Shrink
- Grid Gap
- Grid Cell Size

**Text**

- Text
- Font Size
- Letter Spacing
- Line Spacing
- Character Reveal
- Number Value

**Event**

- Command
- Graph Trigger
- Focus
- Tooltip
- Toast
- Sound
- Custom Event

#### 19.3 Timeline

- Playhead
- 시간 눈금
- Seconds 보기
- Frames 보기
- Seconds+Frames 보기
- Zoom
- Pan
- Work Area
- Marker
- Loop Region
- 현재 시간 입력
- 재생 범위 설정

#### 19.4 Keyframe

- Key 추가
- Key 삭제
- Key 이동
- Key 복제
- Key 복사
- Key 붙여넣기
- 다중 선택
- Box Selection
- Alt+Drag 복제
- Snap
- Timing Scale
- Reverse
- Mirror
- Ripple Edit
- Insert Time
- Delete Time
- Value 편집
- Time 편집
- Easing 편집

#### 19.5 Snap

- Frame
- 시간 간격
- 다른 Key
- Playhead
- Clip 시작
- Clip 끝
- Marker
- Work Area

#### 19.6 Easing

- Linear
- Step
- Smooth Step
- Quadratic
- Cubic
- Quartic
- Quintic
- Sine
- Exponential
- Circular
- Back
- Elastic
- Bounce
- Custom Curve
- Easing Preview
- Easing 용도 Tooltip

#### 19.7 Curve Editor

- 다중 Curve
- Axis 분리
- Bezier Handle
- Auto Tangent
- Linear Tangent
- Constant Tangent
- Broken Tangent
- Weighted Tangent
- Fit Selection
- Fit All
- Normalize
- Curve 복사
- Curve 붙여넣기

</details>

<a id="feature-20"></a>

### 20. Motion 실시간 Preview

편집 중인 모션을 캔버스에서 즉시 재생하고 비교합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Playhead Scrubbing
- 실시간 Canvas 적용
- Play
- Pause
- Stop
- Loop
- Reverse Play
- Playback Speed
- Work Area Loop
- Preview Snapshot
- Stop 시 원본 복원
- Editor 종료 시 원본 복원
- Preview Target 상태 표시
- Backend 상태 표시
- Preview 오류 표시
- 특정 Track Mute
- 특정 Track Solo

</details>

<a id="feature-21"></a>

### 21. Record와 Auto Key

캔버스와 Inspector의 변경을 자동으로 키프레임에 기록합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Record Mode
- Auto Key Off
- Existing Tracks
- All Changes
- Canvas 이동 Key 기록
- Canvas 크기 Key 기록
- Canvas 회전 Key 기록
- Canvas Scale Key 기록
- Inspector Color Key 기록
- Inspector Opacity Key 기록
- Inspector Fill Key 기록
- Inspector Font Size Key 기록
- 기존 Key 수정
- 새 Track 자동 생성
- Record 상태 표시
- Layout 편집과 Motion 편집 구분

</details>

<a id="feature-22"></a>

### 22. Motion Path 및 Onion Skin

이동 경로와 이전·다음 프레임을 시각화해 모션을 세밀하게 조정합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 22.1 Motion Path

- Position Path 표시
- Key Point 표시
- Key Time 표시
- Path 방향 표시
- Bezier Handle
- Canvas에서 Key 이동
- Canvas에서 Key 삭제
- Tangent 편집
- 일정 속도 정규화
- Path 따라 Rotation
- Path Target 변경

#### 22.2 Onion Skin

- 이전 Frame
- 이전 Key
- 현재 Frame
- 다음 Key
- 다음 Frame
- 표시 개수
- Frame 간격
- 색상
- 불투명도

#### 22.3 Split Preview

- 두 시간 비교
- 시작/끝 비교
- 서로 다른 해상도 비교
- 서로 다른 State 비교
- 서로 다른 Theme 비교

</details>

<a id="feature-23"></a>

### 23. Motion Layer

여러 애니메이션을 겹치고 우선순위와 혼합 방식을 제어합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Layer 생성
- Layer 삭제
- Layer 순서
- Weight
- Priority
- Enabled
- Mute
- Solo
- Override Blend
- Additive Blend
- Multiply Blend
- Relative Blend
- Property Mask
- Element Mask
- State별 Layer
- Runtime Layer 활성화
- Layer 충돌 검사

</details>

<a id="feature-24"></a>

### 24. Motion State Machine

Normal·Hover·Pressed 같은 상태 사이의 애니메이션 전환을 정의합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 24.1 기본 State

- Normal
- Hover
- Pressed
- Focused
- Selected
- Disabled
- Loading
- Empty
- Error
- Success
- 사용자 정의 State

#### 24.2 Transition

- From State
- To State
- Any State
- Motion Clip
- Duration Override
- Condition
- Exit Time
- Priority

#### 24.3 Interrupt Policy

- Restart
- Reverse
- Continue From Current
- Blend
- Queue
- Ignore
- Complete Immediately

#### 24.4 Preview

- State Preview
- Transition Preview
- 현재 State 표시
- 현재 Transition 표시
- Binding 기반 State Preview
- Reduced Motion Preview

</details>

<a id="feature-25"></a>

### 25. Motion Graph

노드를 연결해 복합적인 UI 동작과 모션 흐름을 코드 없이 구성합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 25.1 Graph 관리

- Graph 생성
- Graph 복제
- Graph 삭제
- Graph 이름 변경
- Graph Parameter
- Graph Output
- Graph Validation
- Graph Template
- Subgraph

#### 25.2 Port 타입

- Flow
- Bool
- Integer
- Float
- String
- Vector2
- Vector3
- Color
- Element
- Element List
- Screen
- Motion Clip
- Motion Graph
- Command
- Sprite
- Audio
- Object

#### 25.3 Event Node

**Screen**

- On Screen Create
- On Screen Open
- On Screen Opened
- On Screen Close Requested
- On Screen Closing
- On Screen Closed
- On Screen Covered
- On Screen Revealed
- On Screen Focused
- On Screen Unfocused

**Element**

- On Click
- On Double Click
- On Long Press
- On Press
- On Release
- On Hover Enter
- On Hover Exit
- On Drag Start
- On Drag
- On Drag End
- On Drop
- On Scroll
- On Focus
- On Blur
- On Submit
- On Cancel
- On Value Changed
- On Selection Changed

**Binding**

- On Binding Changed
- On Bool True
- On Bool False
- On Value Above
- On Value Below
- On Collection Changed
- On Item Added
- On Item Removed

**Environment**

- On Resolution Changed
- On Safe Area Changed
- On Orientation Changed
- On Input Device Changed
- On Language Changed
- On Theme Changed

**Custom**

- Custom Event
- Trigger Graph

#### 25.4 Flow Node

- Sequence
- Parallel
- Branch
- Switch
- Delay
- Wait Until
- Wait While
- Repeat
- Loop
- For Each
- Stagger
- Race
- Timeout
- Gate
- Once
- Debounce
- Throttle
- Cooldown
- Queue
- Cancel
- Try
- Catch
- Finally
- Fallback

#### 25.5 Motion Node

- Move
- Rotate
- Scale
- Resize
- Fade
- Color
- Text Color
- Fill Amount
- Blur
- Shader Property
- Shake
- Punch Scale
- Pulse
- Flash
- Blink
- Bounce
- Spring
- Typewriter
- Text Scramble
- Number Count
- Progress Animate
- Radial Fill Animate
- Slot Highlight
- Cooldown Sweep

#### 25.6 Motion Clip Node

- Play Motion Clip
- Stop Motion Clip
- Pause Motion Clip
- Resume Motion Clip
- Reverse Motion Clip
- Set Clip Time
- Set Clip Speed

#### 25.7 UI Node

- Show Element
- Hide Element
- Toggle Visibility
- Set Interactable
- Set Text
- Set Class
- Add Class
- Remove Class
- Change Variant
- Set Focus
- Clear Focus
- Scroll Into View
- Show Tooltip
- Hide Tooltip
- Show Toast
- Open Screen
- Close Screen
- Push Screen
- Pop Screen
- Replace Screen

#### 25.8 Command Node

- Dispatch Command
- Payload Builder
- Wait Result
- Success
- Failed
- Completed
- Timeout

</details>

<a id="feature-26"></a>

### 26. Graph 대상 선택

Graph가 제어할 화면이나 요소 집합을 다양한 방식으로 선택합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Element Picker
- Current Event Target
- Current Selected Element
- Current Focused Element
- Current Screen
- Parent
- Children
- Descendants
- Siblings
- Element List
- Element Query
- Class Query
- Type Query
- Slot Query
- Component Query

</details>

<a id="feature-27"></a>

### 27. Graph Blackboard

Graph에서 사용할 매개변수, 변수, 현재 상태 값을 관리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 27.1 Parameter

- Bool
- Integer
- Float
- String
- Vector
- Color
- Element
- Element List
- Motion Clip
- Screen
- Object

#### 27.2 Local Variable

- 생성
- 삭제
- 이름 변경
- 기본값
- Runtime 값 표시
- Read
- Write

#### 27.3 Built-in Variable

- Current Event Target
- Current Screen
- Current Focus
- Current Input Device
- Delta Time
- Unscaled Delta Time
- Current Item
- Current Index

#### 27.4 입력 Source

- Constant
- Binding
- Parameter
- Variable
- Node Output
- Expression

</details>

<a id="feature-28"></a>

### 28. Graph 구조화

복잡한 Graph를 그룹·주석·Subgraph로 정리하고 탐색합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Comment Box
- Group
- Reroute Node
- Portal
- Subgraph
- Extract To Subgraph
- Collapse To Node
- Node 검색
- Minimap
- Breadcrumb
- Bookmark
- Node Category
- Node 색상
- Node 설명
- Node Documentation
- 자동 정렬
- 선택 정렬
- 선택 분배

</details>

<a id="feature-29"></a>

### 29. Graph Preview와 Debug

Graph 실행 흐름과 값을 편집기 및 런타임에서 추적합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 29.1 Editor Preview

- Preview Node
- Preview Selection
- Preview From Here
- Preview To Here
- Preview Graph
- Reset Preview
- 실행 중 Node 표시
- 완료 Node 표시
- 실패 Node 표시
- 취소 Node 표시
- 실행 연결선 표시

#### 29.2 Runtime Debug

- Graph Instance 목록
- 실행 Node 표시
- Execution Count
- Last Duration
- Input Value
- Output Value
- Error 표시
- Breakpoint
- Pause Graph
- Step Into
- Step Over
- Continue
- Stop

#### 29.3 중첩 정책

- Ignore New
- Restart
- Continue From Current
- Reverse Current
- Complete Current
- Queue
- Run In Parallel
- Blend

</details>

<a id="feature-30"></a>

### 30. 텍스트 Motion

타자기, 글자 섞기, 숫자 카운트 등 텍스트 전용 모션을 제작합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 30.1 Typewriter

- 문자 단위
- 단어 단위
- 줄 단위
- 문장 단위
- 구두점 Delay
- Rich Text 처리
- Click To Complete
- Typing Sound
- Skip

#### 30.2 Text Scramble

- Random Characters
- Number Only
- Glitch Characters
- Left To Right
- Right To Left
- Random Decode

#### 30.3 Number Count

- Integer
- Float
- Count Up
- Count Down
- Prefix
- Suffix
- 천 단위 구분
- Decimal Places

#### 30.4 Character Motion

- Position
- Rotation
- Scale
- Color
- Opacity
- Wave
- Shake
- Delay
- Left To Right
- Right To Left
- Center Out
- Random

</details>

<a id="feature-31"></a>

### 31. Audio, Haptic, VFX

UI 동작에 사운드, 진동, 파티클과 셰이더 효과를 결합합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 31.1 Audio

- Play Sound
- Stop Sound
- Pause Sound
- Resume Sound
- Volume
- Pitch
- Delay
- Audio Adapter
- Unity Audio
- FMOD Adapter
- Custom Adapter

#### 31.2 Haptic

- Light
- Medium
- Heavy
- Custom
- 플랫폼 지원 검사

#### 31.3 UI VFX

- Spawn Effect
- Stop Effect
- Particle
- Click Burst
- Glow
- Trail
- Afterimage
- Shader Property
- Material Property
- Dissolve
- Glitch
- Blur Transition

</details>

---

## Part 6. 탐색·미리보기·반응형

Focus, Screen Flow, Preview, Scenario, Theme, Accessibility를 다룹니다.
**이 Part의 기능:** [32. Focus Navigation](#feature-32) · [33. Screen Flow](#feature-33) · [34. Preview](#feature-34) · [35. Scenario](#feature-35) · [36. 반응형 UI](#feature-36) · [37. Theme 및 Design Token](#feature-37) · [38. Accessibility](#feature-38)

<a id="feature-32"></a>

### 32. Focus Navigation

키보드와 게임패드에서 자연스럽게 이동하는 포커스 경로를 설계합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 32.1 Focus 설정

- Default Focus
- Up
- Down
- Left
- Right
- Submit
- Cancel
- Focus Scope
- Focus Trap
- Focus Restore
- Wrap Around

#### 32.2 자동 Navigation

- Nearest
- Grid
- Angle Weighted
- Strict Row
- Strict Column
- Wrap
- 제외 요소 설정

#### 32.3 Focus Preview

- Focus 이동 시뮬레이션
- 현재 Focus 표시
- 방향 연결 표시
- 접근 불가능한 요소 표시
- Focus History 표시
- Focus Motion
- Focus Sound
- Focus Tooltip
- Scroll Into View

</details>

<a id="feature-33"></a>

### 33. Screen Flow

여러 화면과 모달 사이의 이동 구조와 전환 규칙을 시각화합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 33.1 Node

- Screen
- Overlay
- Modal
- Persistent HUD
- Subflow

#### 33.2 Transition

- Push
- Pop
- Replace
- Overlay
- Modal
- Return

#### 33.3 Transition 설정

- Motion
- Guard
- Input Blocking
- Pause Policy
- Focus Policy
- Time Policy
- Transition Duration
- Cancel Policy

#### 33.4 Deep Link

- 특정 화면 열기
- 특정 Tab 열기
- 특정 Element Focus
- Parameter 전달
- Return 경로

</details>

<a id="feature-34"></a>

### 34. Preview

다양한 해상도, 입력 장치, 언어, 상태에서 UI를 미리 확인합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 34.1 Component State

- Normal
- Hover
- Pressed
- Focused
- Disabled
- Selected
- Loading
- Empty
- Error
- Success
- Indeterminate

#### 34.2 Mock State

- Binding 값 입력
- Bool Toggle
- Number Slider
- String 입력
- Enum 선택
- Color 선택
- Sprite 선택
- Collection 생성
- 값 고정
- 값 초기화

#### 34.3 Device Preview

- Desktop 16:9
- QHD
- 4K
- Ultrawide
- Steam Deck
- Mobile Portrait
- Mobile Landscape
- Console Safe Area
- Custom Resolution

#### 34.4 환경

- DPI
- UI Scale
- Safe Area
- Input Device
- Language
- Font Scale
- Theme
- Reduced Motion
- Color Blind Mode

#### 34.5 Input Simulation

- Mouse
- Keyboard
- Gamepad
- Touch
- Pointer Hover
- Click
- Submit
- Cancel
- Navigation
- Drag
- Scroll

</details>

<a id="feature-35"></a>

### 35. Scenario

실제 사용자 흐름을 재현할 수 있는 테스트 상황과 입력 순서를 저장합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 35.1 Scenario Asset

- Binding Values
- Input Device
- Resolution
- DPI
- Safe Area
- Language
- Theme
- Focus
- Screen Stack
- Component State
- Triggered Event

#### 35.2 Scenario Timeline

- 시간별 Binding 변경
- 시간별 Event 실행
- 시간별 Input 실행
- 시간별 화면 전환
- Motion과 Graph 반응 확인
- Timeline 재생
- Timeline Scrubbing
- Loop

#### 35.3 Interaction Recording

- 사용자 입력 기록
- 화면 열기 기록
- 클릭 기록
- Focus 이동 기록
- Drag 기록
- Command 기록
- Scenario 저장
- Scenario 재생
- 회귀 테스트

</details>

<a id="feature-36"></a>

### 36. 반응형 UI

화면 크기와 기기에 따라 레이아웃과 컴포넌트 상태를 변경합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 36.1 Breakpoint

- Width 조건
- Height 조건
- Aspect 조건
- Orientation 조건
- Device 조건
- Input Device 조건

#### 36.2 Breakpoint Override

- Visibility
- Position
- Size
- Layout
- Layout Direction
- Padding
- Margin
- Gap
- Grid Columns
- Font Size
- Component Variant
- Motion
- Navigation

#### 36.3 동시 Preview

- 여러 Resolution 동시 표시
- Breakpoint 비교
- Theme 비교
- 언어 비교
- Safe Area 비교
- Overflow 검사

</details>

<a id="feature-37"></a>

### 37. Theme 및 Design Token

색상·간격·타이포그래피 등 공통 디자인 규칙을 토큰과 테마로 관리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 37.1 Token 타입

- Color
- Typography
- Spacing
- Radius
- Border
- Shadow
- Motion Duration
- Motion Easing
- Breakpoint
- Z-Index

#### 37.2 Token 기능

- Token 생성
- Token 삭제
- Token 이름 변경
- Token 그룹
- Token 별칭
- Token Reference
- Token Override
- 사용 위치 검색
- 미사용 Token 검사
- Token 일괄 교체

#### 37.3 Theme

- Theme 생성
- Theme 복제
- Theme 전환
- Theme 상속
- Theme Override
- Default
- Dark
- High Contrast
- 사용자 정의 Theme
- Theme 동시 Preview

</details>

<a id="feature-38"></a>

### 38. Accessibility

읽기 순서, 대비, 입력 방식 등 접근성 문제를 설계 단계에서 확인합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Accessibility Label
- Accessibility Description
- Accessibility Role
- 읽기 순서
- Screen Reader Preview
- 대비 검사
- 색각 Preview
- Font Scale Preview
- Touch Target 검사
- Keyboard 접근성 검사
- Gamepad 접근성 검사
- Focus Trap 검사
- Reduced Motion
- Hold 입력 대체
- Double Click 대체
- 접근성 Validation

</details>

---

## Part 7. 출력·런타임·품질 관리

Backend 생성, Live Edit, Debugger, Validation, 검색를 다룹니다.
**이 Part의 기능:** [39. Backend 출력](#feature-39) · [40. Runtime Live Edit](#feature-40) · [41. Runtime Debugger](#feature-41) · [42. Validation](#feature-42) · [43. History와 Undo](#feature-43) · [44. 검색과 일괄 편집](#feature-44) · [45. Command Palette와 단축키](#feature-45)

<a id="feature-39"></a>

### 39. Backend 출력

Studio에서 만든 화면을 uGUI 또는 UI Toolkit 에셋으로 생성합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 39.1 uGUI 생성

- GameObject
- RectTransform
- CanvasGroup
- Image
- TMP_Text
- Button
- Toggle
- Slider
- ScrollRect
- HorizontalLayoutGroup
- VerticalLayoutGroup
- GridLayoutGroup
- ContentSizeFitter
- LayoutElement
- Mask
- RectMask2D
- Prefab 생성
- Prefab 갱신
- 부모-자식 반영
- Sibling 반영
- Component 설정 반영

#### 39.2 UI Toolkit 생성

- UXML 생성
- UXML 갱신
- USS 생성
- USS 갱신
- Element Type
- name
- class
- Hierarchy
- Position
- Size
- Flex
- Padding
- Margin
- Gap
- Border
- Radius
- Text Style
- Background
- Opacity
- Display
- Overflow

#### 39.3 Publish

- 현재 화면 Publish
- 모든 화면 Publish
- 변경된 화면만 Publish
- Dry Run
- Build Report
- 생성된 Asset 목록
- 경고 목록
- 오류 목록
- 변경 Diff
- 자동 Backup

</details>

<a id="feature-40"></a>

### 40. Runtime Live Edit

Play Mode에서 실제 UI를 선택하고 값을 수정해 결과를 즉시 확인합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Play Mode 연결
- Runtime Screen 목록
- Runtime Element 선택
- Game View에서 UI Pick
- Runtime 값을 Studio에 표시
- Runtime Instance 편집
- Preview Only
- Runtime Instance 적용
- Source Asset 적용
- Override 생성
- Revert
- Binding 값 고정
- Runtime Motion 재생
- Runtime Graph Trigger
- Runtime Focus 변경

</details>

<a id="feature-41"></a>

### 41. Runtime Debugger

실행 중인 화면, 바인딩, 명령, 모션, Graph 상태를 분석합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 41.1 Screen Tree

- 열린 Screen
- Stack 순서
- Layer
- Modal
- Persistent
- Input Owner
- Focus Owner
- 화면 상태
- 생성 시간

#### 41.2 Binding Trace

- Binding Source
- State Store
- Binding Target
- 현재 값
- 타입
- 갱신 시간
- 갱신 Source
- 오류

#### 41.3 Command Log

- Command 이름
- Source Element
- Payload
- 결과
- Duration
- 오류
- Timestamp

#### 41.4 Motion Profiler

- 실행 중 Motion
- Active Layer
- Clip Time
- Graph Instance
- Evaluation Time
- Layout Rebuild
- Allocation
- 변경 요소 수
- 중첩 Motion 수

#### 41.5 Graph Trace

- 실행 경로
- Node 상태
- Input 값
- Output 값
- 실행 횟수
- 실행 시간
- Error

</details>

<a id="feature-42"></a>

### 42. Validation

구조·레이아웃·바인딩·모션·접근성 오류를 자동으로 검사합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 42.1 화면 구조

- Screen 누락
- Metadata 누락
- 중복 Element ID
- 빈 Element ID
- Parent 누락
- 자기 자신 Parent
- 순환 Parent
- 잘못된 Slot
- 자식 미지원
- Slot 자식 수 초과
- 중복 Sibling Index

#### 42.2 Layout

- 잘못된 크기
- 음수 크기
- Safe Area 이탈
- 화면 밖 요소
- Text Overflow
- Layout 충돌
- Breakpoint 누락

#### 42.3 Binding

- State Key 누락
- Command 누락
- 타입 불일치
- 미지원 Channel
- 잘못된 Expression
- Item Scope 오류
- Collection Key 누락

#### 42.4 Component

- Component 순환 참조
- 누락된 Main Component
- 잘못된 Override
- 누락된 Variant
- 잘못된 Slot Content

#### 42.5 Motion

- Target 누락
- Track 누락
- 미지원 Property
- 잘못된 Key Time
- Empty Clip
- Layer 충돌
- Reduced Motion 누락

#### 42.6 Graph

- 필수 Port 연결 누락
- 타입 불일치
- 도달 불가능한 Node
- Entry Event 없음
- 무한 Flow
- Subgraph 누락
- Command Payload 오류
- 안전하지 않은 State Write

#### 42.7 Navigation

- Default Focus 누락
- Focus Trap
- 접근 불가능 요소
- 끊어진 방향 연결
- Offscreen Focus

#### 42.8 Accessibility

- Label 누락
- Role 오류
- 낮은 대비
- 작은 Touch Target
- Keyboard 접근 불가
- Gamepad 접근 불가

#### 42.9 Validation UX

- Error
- Warning
- Info
- 요소로 이동
- Graph Node로 이동
- 관련 Workspace 열기
- 자동 수정
- 모두 자동 수정
- 무시 규칙
- 사용자 정의 Validation

</details>

<a id="feature-43"></a>

### 43. History와 Undo

작업 기록을 추적하고 Unity Undo/Redo와 저장 상태를 관리합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Unity Undo
- Unity Redo
- Command History
- 작업 이름
- 작업 시간
- 대상 요소
- History 검색
- History 필터
- Metadata Dirty
- Backend Dirty
- Preview Dirty
- 저장 지점 표시

</details>

<a id="feature-44"></a>

### 44. 검색과 일괄 편집

프로젝트 전체에서 요소와 리소스를 찾고 여러 항목을 한 번에 수정합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 44.1 검색

- Element ID
- 이름
- Component 타입
- Class
- Binding
- Command
- Theme Token
- Variant
- Font
- Sprite
- Motion Clip
- Motion Graph

#### 44.2 Replace

- ID Replace
- Class Replace
- Binding Replace
- Command Replace
- Token Replace
- Component Replace
- Variant Replace
- Font Replace
- Sprite Replace

#### 44.3 일괄 편집

- Style 일괄 변경
- Binding 일괄 변경
- Theme 일괄 변경
- Accessibility 일괄 변경
- Component 일괄 교체
- Variant 일괄 변경

</details>

<a id="feature-45"></a>

### 45. Command Palette와 단축키

검색 기반 명령 실행과 사용자 정의 단축키로 반복 작업을 줄입니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 45.1 Command Palette

- 기능 검색
- 화면 검색
- 요소 검색
- Component 추가
- Workspace 전환
- Motion Clip 생성
- Graph 생성
- Validation
- Publish
- Preview
- Runtime Debug

#### 45.2 단축키

- 전체 선택
- 선택 해제
- 복사
- 붙여넣기
- 복제
- 삭제
- Group
- Ungroup
- Layer 이동
- 정렬
- 분배
- Focus Selection
- Quick Insert
- Command Palette
- Play
- Stop
- Add Key
- Record
- Save
- Validate

#### 45.3 재설정

- 단축키 변경
- 충돌 검사
- 기본값 복원
- 사용자 Profile 저장

</details>

---

## Part 8. 현지화·확장·유지보수

Localization, Template, API, Figma, Migration, Test를 다룹니다.
**이 Part의 기능:** [46. Localization](#feature-46) · [47. Template](#feature-47) · [48. 확장 API](#feature-48) · [49. Figma Bridge](#feature-49) · [50. Migration](#feature-50) · [51. 테스트 및 샘플](#feature-51)

<a id="feature-46"></a>

### 46. Localization

Studio 자체와 제작 중인 UI를 여러 언어와 RTL 환경에서 검증합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Studio UI 한국어
- Studio UI 영어
- 실시간 언어 전환
- Tooltip 번역
- Component 이름 번역
- Validation 메시지 번역
- Command Palette 별칭
- Localization Key Preview
- 언어별 Text Overflow 검사
- 언어별 Font 설정
- RTL Preview

</details>

<a id="feature-47"></a>

### 47. Template

메뉴, HUD, 인벤토리 등 자주 쓰는 화면 구성을 템플릿으로 재사용합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 47.1 화면 Template

- Main Menu
- Pause Menu
- Settings
- Player HUD
- Inventory
- Hotbar
- Equipment
- Crafting
- Quest Log
- Dialogue
- Loading Screen
- Save Select
- Confirmation Modal
- Tooltip
- Toast Stack
- Boss HUD
- Result Screen

#### 47.2 Template 포함 데이터

- Hierarchy
- Layout
- Component
- Binding 이름
- Command 이름
- Motion
- Focus Navigation
- Accessibility
- Scenario
- Theme

#### 47.3 Template 관리

- Template 생성
- Template 복제
- Template 삭제
- Project Template
- Package Template
- Template Import
- Template Export
- UI Kit Import
- UI Kit Export

</details>

<a id="feature-48"></a>

### 48. 확장 API

커스텀 컴포넌트, 노드, 검증 규칙, 백엔드 등을 플러그인 형태로 확장합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Custom Component
- Component Descriptor
- Custom Inspector
- Custom Slot
- Custom Binding Channel
- Custom Motion Property
- Custom Motion Node
- Custom Graph Node
- Custom Validation Rule
- Custom Backend Adapter
- Custom Audio Adapter
- Custom Tooltip Content
- Custom Template
- Custom Theme Token
- Custom Preview Provider
- 자동 Registry 등록
- Generic Fallback

</details>

<a id="feature-49"></a>

### 49. Figma Bridge

Figma 파일과 프레임 정보를 조회해 디자인 전달 과정을 연결합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- Personal Access Token
- File Key
- Figma API 연결
- File 정보 조회
- Node 조회
- Raw JSON 표시
- Frame 선택
- Image Reference 조회
- 연결 상태 표시

</details>

<a id="feature-50"></a>

### 50. Migration

이전 버전 데이터를 안전하게 최신 구조로 변환합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

- 이전 Metadata 버전 감지
- Metadata 자동 Migration
- Motion Clip Migration
- Motion Graph Migration
- Component Definition Migration
- Migration Preview
- Backup 생성
- 변경 내용 표시
- 일괄 Migration
- 실패 Rollback

</details>

<a id="feature-51"></a>

### 51. 테스트 및 샘플

대표 샘플과 자동화 테스트로 기능 동작과 회귀 문제를 검증합니다.

<details>
<summary><strong>세부 기능 보기</strong></summary>

#### 51.1 샘플

- Main Menu
- Inventory
- Hotbar
- HUD
- Modal
- Tooltip
- Button Motion
- Grid Stagger
- Focus Navigation
- Collection Binding
- Motion Graph
- Runtime Debug

#### 51.2 EditMode 테스트

- Metadata 직렬화
- 계층
- Slot
- Undo/Redo
- Motion 평가
- Key 보간
- Curve 평가
- Graph Port
- Graph 실행
- Scenario
- Backend 생성

#### 51.3 Integration 테스트

- Studio에서 uGUI 생성
- Studio에서 UXML/USS 생성
- Backend 동기화
- Motion Preview 복원
- Graph Preview
- Binding
- Command
- Focus Navigation
- Runtime Live Edit

</details>

---

## 권장 문서 분리

이 기능 명세서는 제품의 **전체 범위**를 설명합니다. 실제 GitHub 저장소에서는 다음 문서를 별도로 두는 것을 권장합니다.

| 문서 | 역할 |
| --- | --- |
| `README.md` | 프로젝트 소개와 핵심 가치 |
| `FEATURE_SPEC.md` | 전체 기능 명세 |
| `GETTING_STARTED.md` | 설치와 첫 화면 제작 |
| `USER_GUIDE.md` | Workspace별 실제 사용 방법 |
| `ROADMAP.md` | 구현 예정 기능과 우선순위 |
| `CHANGELOG.md` | 버전별 변경 내용 |
| `CONTRIBUTING.md` | 기여 방법과 확장 API 규칙 |

---

## 문서 관리 원칙

- 기능의 **존재 여부**와 **구현 상태**를 혼합하지 않습니다.
- 구현 상태는 GitHub Issue, Project 또는 `ROADMAP.md`에서 관리합니다.
- 기능명이 변경되면 Studio UI, 문서, Validation 메시지의 용어를 함께 수정합니다.
- 스튜디오가 이해하기 어려운 기술 용어에는 설명 또는 Tooltip을 제공합니다.
- Backend별 차이는 기능을 숨기기보다 지원 여부와 Fallback을 명확히 표시합니다.
