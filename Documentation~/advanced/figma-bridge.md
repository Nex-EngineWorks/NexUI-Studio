# Figma Bridge

**현재 상태: Beta — 인증, 원본 JSON 조회와 첫 Frame Import 지원**

Figma Bridge는 `Tools > NexUI > 유틸리티`에서 **Figma Bridge**를 선택해 엽니다. Personal Access Token을 EditorPrefs에 저장·삭제하고 `GET /v1/me` 연결을 확인하며, File Key로 파일 JSON을 가져옵니다. Studio에서 대상 Metadata를 연 뒤 **현재 Studio로 첫 Frame 가져오기**를 누르면 계층, 절대 좌표, Text, Solid Fill, Auto Layout을 변환합니다. 기존 Element 교체는 Undo할 수 있습니다.

> [!WARNING]
> Component Variant, Effect, Image 다운로드, Design Token과 양방향 Sync는 지원하지 않습니다. Import는 Backend Asset을 즉시 쓰지 않으므로 Studio Validation과 Save Report를 확인한 뒤 저장하세요.

Token은 프로젝트 파일이 아닌 로컬 EditorPrefs에 저장되지만 운영 계정의 장기 Token 사용은 피하고, 화면 공유·로그·버그 보고에 포함하지 마세요.
