# Compatibility

## Desktop editor support

NexUI Core and Studio support the Unity Editor on Windows, macOS, and Linux. Package validation runs on all three operating systems and checks portable filenames, case-insensitive path collisions, runtime assembly platform coverage, and OS-specific process launches. Unity package tests run in the Linux Editor; release candidates should still receive interactive Editor smoke testing on each supported operating system.

이 문서는 저장소의 `package.json`과 현재 패키지 구성을 기준으로 작성되었습니다. “최소 요구”와 모든 조합을 실제로 테스트했다는 의미는 다릅니다.

| Studio | Core | Unity | UniTask | uGUI | 상태 |
| --- | --- | --- | --- | --- | --- |
| 0.1.0 | 0.1.0 | 6000.4 이상 | 2.5.10 설치 안내 기준 | 2.0.0 | 현재 저장소 기준 |

## 확인된 구성

- Studio package ID: `com.nexengineworks.nexui.studio`
- Core package ID: `com.nexengineworks.nexui`
- uGUI dependency: `com.unity.ugui` 2.0.0
- Runtime은 UniTask API를 사용합니다. Studio `package.json`에는 직접 dependency가 없으므로 설치 문서 순서대로 UniTask를 먼저 준비해야 합니다.
- Editor와 Runtime Assembly는 분리되어 있으며 Runtime Assembly는 `UnityEditor`를 참조하지 않습니다.

Windows/macOS/Linux 정적 검증은 CI에서 수행합니다. Unity 패키지 테스트는 Linux Editor에서 수행하며, 각 OS의 실제 창·입력 동작은 릴리스 전 수동 스모크 테스트 항목입니다. uGUI와 UI Toolkit 저장 범위는 [Backend 지원 범위](backend-support-matrix.md)가 기준입니다.

## Package Manager 설치 방식

Core와 Studio는 각각 루트에 `package.json`이 있는 별도 저장소입니다. Git URL에는 `?path=/Packages/...`를 붙이지 않습니다. Package Manager가 이름 기반 dependency만으로 다른 Git 저장소를 자동으로 찾는다고 가정하지 말고 [설치](../getting-started/installation.md)의 순서대로 두 URL을 등록하세요.

## 알려진 주의 조합

- Core와 Studio 버전이 다르면 Metadata와 Runtime 계약이 맞지 않을 수 있습니다.
- UniTask가 없으면 Runtime Assembly가 컴파일되지 않습니다.
- Unity 2022/2023 등 `package.json` 최소 버전보다 낮은 버전은 지원 대상으로 문서화하지 않습니다.
- 수동 UXML은 일반 Save로 재작성되지 않습니다.
