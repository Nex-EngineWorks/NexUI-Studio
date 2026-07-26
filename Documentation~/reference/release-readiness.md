# 출시 준비 체크리스트

이 체크리스트는 C# 컴파일 성공과 실제 Unity 제품 검증을 구분합니다. 유료 배포나 프로젝트 릴리스 전에 모든 필수 항목을 기록하세요.

## 설치와 패키징

- 빈 프로젝트에서 UniTask → Runtime → Designer 순서로 설치한다.
- Setup Doctor 오류가 0개다.
- Package Manager에 README, Documentation, Changelog와 License 링크가 표시된다.
- `Third Party Notices.txt`와 실제 포함한 타사 패키지 목록이 일치한다.
- Package Validation에서 누락되거나 중복된 `.meta`와 잘못된 manifest 필드가 없다.

## 지원 환경

- 지원한다고 표시할 각 Unity 버전에서 EditMode와 PlayMode 테스트를 실행한다.
- Windows와 macOS Editor에서 Designer의 생성·저장·Undo/Redo를 확인한다.
- Mono와 IL2CPP 중 제품이 지원한다고 표시할 조합으로 Player를 빌드한다.
- Domain Reload 비활성화 상태에서 Play Mode 진입과 종료를 반복한다.

## 사용자 흐름

- 문서를 미리 읽지 않은 사용자가 Setup Doctor에서 첫 화면 생성까지 진행할 수 있다.
- Designer 상태가 Loaded, Unsaved, Saved와 Validation 결과를 정확히 표시한다.
- Save Report의 Changed, Skipped, Warning, Error를 확인한다.
- Preview 전용 또는 부분 지원 값이 실제 Backend 결과로 오인되지 않는다.
- Runtime State/Command Key를 등록하고 실제 입력으로 실행한다.

## Backend와 빌드

- 판매 페이지에서 약속한 Backend의 샘플을 처음부터 끝까지 실행한다.
- 지원표에서 `부분 지원`, `Metadata만`, `Preview만`인 항목을 제품 설명에도 공개한다.
- 대표 해상도와 입력 장치에서 Layout, Focus, Font와 Accessibility를 확인한다.
- Console에 NexUI가 발생시킨 오류나 경고가 남지 않는다.

## 릴리스 증거

릴리스마다 Unity 버전, OS, Backend, test count, Player target과 알려진 제한을 표로 남깁니다. 테스트 어셈블리의 빌드 성공만으로 Test Runner 통과를 기록하지 않습니다.
