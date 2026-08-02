# Sync와 Publish

생성된 UXML/USS를 디스크 파일 및 마지막 Publish 기준과 **3-way**로 비교해, Studio 변경·백엔드 변경·충돌을 구분하고 변경분만 Publish합니다 (스펙 §32 · §39.3).

## 3-way 상태 모델

세 값을 비교합니다.

- **Studio**: 지금 메타데이터에서 생성한 UXML/USS
- **File**: 디스크의 `.g.uxml`/`.g.uss`
- **Base**: 마지막 Publish 때 기록한 기준 해시(`DesignerPublishManifest`)

| 상태 | 조건 | 의미 |
|---|---|---|
| **신규(New)** | 파일 없음 | 아직 Publish 안 됨 |
| **동기화됨(InSync)** | Studio == File | 할 일 없음 |
| **Studio 변경** | File == Base, Studio ≠ Base | Studio만 바뀜 → 안전하게 쓰기 가능 |
| **백엔드 변경** | Studio == Base, File ≠ Base | 생성 파일을 손으로 수정함 |
| **충돌(Conflict)** | Studio·File 모두 Base와 다름 | 양쪽 변경 → 해결 필요 |

기준(Base)이 없으면(매니페스트 미설정) 충돌을 구분할 수 없고 변경/미변경만 판단합니다.

## 충돌 해결

- **Studio 사용(쓰기)**: 생성 결과를 파일로 쓰고 기준값으로 기록합니다. 생성 배너가 없는 파일은 덮어쓰지 않습니다.
- **백엔드 사용(기준 채택)**: 디스크 파일을 새 기준값으로 채택합니다(재생성 없이 충돌 해소).
- **Diff 보기**: 파일 → 생성 결과의 라인 단위 차이(+ 추가, - 제거)를 봅니다. LCS 기반.

## 변경분만 Publish (§39.3)

**Dry Run**은 쓰지 않고 각 화면의 처리 결과를 보고합니다. **변경분 Publish**는 신규·Studio 변경 화면만 파일로 쓰고 기준값을 갱신하며, 동기화됨은 건너뛰고 백엔드 변경·충돌은 "해결 필요"로 보고합니다.

- `Tools > NexUI > 유틸리티`에서 **Sync / Publish**를 선택합니다.
- 프로젝트의 모든 `DesignerMetadataAsset`을 대상으로 합니다.

## 어떤 작업을 사용할까요

- **일반 Save**: Metadata와 Screen Definition을 저장하고 uGUI Prefab의 지원 값을 반영할 때 사용합니다. 수동 UXML은 보존됩니다.
- **Generate**: 선택한 Metadata에서 별도 `.g.uxml/.g.uss` 결과를 만들고 싶을 때 사용합니다.
- **Sync / Publish**: 여러 화면의 마지막 Publish Base와 현재 변경을 비교하고 충돌을 관리할 때 사용합니다.

권장 흐름은 **Save → Validate → Dry Run → Diff → 변경분 Publish**입니다. Dry Run에서 BackendChanged/Conflict가 보이면 바로 쓰지 말고 파일 소유권을 먼저 결정하세요.

## Publish 전 체크리스트

- Validation Error가 없습니다.
- 수동 UXML과 Generated UXML을 구분했습니다.
- Dry Run의 New/DesignerChanged 대상이 예상과 같습니다.
- BackendChanged/Conflict를 명시적으로 해결했습니다.
- Screen Definition, Metadata, Generated 파일, Publish Manifest와 `.meta`를 함께 Git에서 검토합니다.
- 두 Backend를 Play Mode에서 확인합니다.

[Asset Ownership](../reference/asset-ownership.md)에서 커밋 범위와 수동 편집 규칙을 확인하세요.

## 아키텍처

- 순수 `SyncStateResolver`(3-way 분류) + `TextLineDiff`(LCS 라인 diff)는 Unity 없이 단위 테스트됩니다(`SyncStateResolverTests`).
- `DesignerPublishService`가 생성(→`UIToolkitCodeGenerator`)·해시(SHA1)·상태 판정·쓰기/채택/변경분 Publish를 담당합니다.
- 기준값은 `DesignerPublishManifest`(Studio.Runtime, 화면별 UXML/USS 해시)에 저장됩니다.
- 쓰기는 항상 별도 생성 파일(`.g.uxml`/`.g.uss`)만 대상으로 하며, 생성 배너를 잃은 파일은 덮어쓰지 않습니다 — 손으로 수정한 파일은 조용히 사라지지 않고 백엔드 변경/충돌로 드러납니다.

## 아직 하지 않는 것

3-way 자동 병합(Merge)은 제공하지 않습니다(Studio/백엔드 중 선택). uGUI 프리팹 대상 동기화, 자동 백업은 이후 단계입니다.
