# 코딩 규칙

- Namespace는 기존 `emiteat.NexUI.Studio`와 `.Editor` 계층을 따릅니다.
- Interface는 `I`, Service/Registry/Panel/Backend/Metadata suffix를 기존 역할대로 사용합니다.
- Runtime assembly에서 `UnityEditor`를 참조하지 않습니다.
- Serialized Field를 삭제하기보다 기본값과 Migration을 유지합니다.
- Menu는 `Tools > NexUI > Studio` 아래에 두고 실제 경로를 문서·Localization과 맞춥니다.
- EditorPrefs Key는 `NexUI.Studio.` 접두와 Asset GUID를 사용하며 핵심 데이터를 저장하지 않습니다.
- USS Class는 `nexui-` kebab-case를 사용합니다.
- Validation Code는 짧은 kebab-case Stable ID로 유지합니다.
- Panel은 표시와 입력에 집중하고 파일 I/O·분석은 Service로 분리합니다.
- Context 탐색에 `Resources.FindObjectsOfTypeAll`을 사용하지 않고 `DesignerSessions` 또는 주입 Provider를 사용합니다.
- Context 이벤트는 `ContextBoundSubscriptions`, Window 이벤트는 OnEnable/OnDisable에서 대칭 처리합니다.
- Reflection은 기존 선택적 Tool 연결처럼 assembly 경계를 피할 때만 제한적으로 사용합니다.
- 한국어 UI는 Localization Key에 추가하고 클래스·API·메뉴 고유명은 영문 표기를 유지합니다.
