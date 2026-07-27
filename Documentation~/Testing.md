# Testing

테스트 구성, Unity Test Runner와 Batchmode 명령, CI Secret과 Artifact 정책은 [Developer / Testing](developer/testing.md)을 확인하세요.

## CI gate

`.github/workflows/unity-tests.yml`은 먼저 `Tools/Validate-NexUI.ps1`을 실행해 package.json, `.meta`, Runtime/Editor assembly 경계, 문서 링크, 임시 생성물과 merge marker를 검사한다. 이 gate가 통과한 뒤 Unity EditMode와 PlayMode job이 각각 실행되며 결과는 `unity-editmode-results`, `unity-playmode-results` artifact로 업로드된다.

Windows에서 로컬 실행 정책이 스크립트를 막으면 다음처럼 검증한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-NexUI.ps1
```

## Unity license secrets

GitHub 저장소의 Actions secrets에 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL`을 등록해야 GameCI Test Runner가 활성화된다. 조직 정책상 계정 비밀번호를 저장할 수 없다면 GameCI가 지원하는 Unity 라이선스 파일 방식으로 workflow를 별도 구성하고, fork PR에는 secrets가 전달되지 않는다는 점을 운영 규칙에 명시한다. Secret 값은 로그나 artifact에 출력하지 않는다.
