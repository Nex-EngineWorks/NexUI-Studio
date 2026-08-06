# Compatibility

## Desktop editor support

NexUI Core and Studio support the Unity Editor on Windows, macOS, and Linux. Package validation runs on all three operating systems and checks portable filenames, case-insensitive path collisions, runtime assembly platform coverage, and OS-specific process launches. Unity package tests run in the Linux Editor; release candidates should still receive interactive Editor smoke testing on each supported operating system.

이 문서는 저장소의 `package.json`과 현재 패키지 구성을 기준으로 작성되었습니다. “최소 요구”와 모든 조합을 실제로 테스트했다는 의미는 다릅니다.

| Studio | Core | Unity | UniTask | uGUI | 상태 |
| --- | --- | --- | --- | --- | --- |
| 0.1.0 | 0.1.0 | 2022.3 LTS 이상 | 2.5.10 설치 안내 기준 | 1.0.0 | 컴파일 검증됨 (아래) |

### Unity 버전 검증 수준

지원 범위는 Unity 2022.3 LTS와 Unity 6입니다. 각 버전에서 **무엇이 확인되었는지**는 다릅니다.

| 버전 | 컴파일 | Editor 실사용 | 테스트 실행 |
| --- | --- | --- | --- |
| 2022.3.62f3 | ✅ Runtime 253 + Studio 352 파일, 오류 0 | ❌ 미수행 | ❌ 미수행 |
| 6000.1.2f1 | ✅ 동일 | ❌ 미수행 | ❌ 미수행 |
| 6000.4.2f1 | ✅ 동일 (개발 버전) | ✅ | ❌ 미수행 |

컴파일 검증은 `Tools/Verify-UnityVersionCompat.ps1`이 대상 에디터의 Roslyn과 어셈블리로 수행하며
언제든 재현할 수 있습니다.

```bash
pwsh ./Tools/Verify-UnityVersionCompat.ps1 -EditorRoot D:/unityEditor/2022.3.62f3
```

이 검사가 잡지 못하는 것: asmdef 경계(전체를 단일 어셈블리로 컴파일함), `[UxmlElement]`
소스 제너레이터 산출물, 그리고 모든 런타임 동작. **컴파일 통과는 테스트 통과가 아닙니다.**

2022.3에서 UXML 커스텀 엘리먼트는 `[UxmlElement]` 대신 `*.Legacy.cs`의 손으로 쓴
`UxmlFactory`/`UxmlTraits` 경로를 탑니다. 속성 이름은 Unity 6 제너레이터가 만드는 것과
동일한 kebab-case이므로 같은 `.uxml`이 양쪽에서 그대로 로드됩니다.

## 확인된 구성

- Studio package ID: `com.nexengineworks.nexui.studio`
- Core package ID: `com.nexengineworks.nexui`
- uGUI dependency: `com.unity.ugui` 1.0.0 (2022.3의 ugui 버전. Unity 6은 내장 2.0.0으로 해석됨)
- Runtime은 UniTask API를 사용합니다. Studio `package.json`에는 직접 dependency가 없으므로 설치 문서 순서대로 UniTask를 먼저 준비해야 합니다.
- Editor와 Runtime Assembly는 분리되어 있으며 Runtime Assembly는 `UnityEditor`를 참조하지 않습니다.

Windows/macOS/Linux 정적 검증은 CI에서 수행합니다. Unity 패키지 테스트는 Linux Editor에서 수행하며, 각 OS의 실제 창·입력 동작은 릴리스 전 수동 스모크 테스트 항목입니다. uGUI와 UI Toolkit 저장 범위는 [Backend 지원 범위](backend-support-matrix.md)가 기준입니다.

## Package Manager 설치 방식

Core와 Studio는 각각 루트에 `package.json`이 있는 별도 저장소입니다. Git URL에는 `?path=/Packages/...`를 붙이지 않습니다. Package Manager가 이름 기반 dependency만으로 다른 Git 저장소를 자동으로 찾는다고 가정하지 말고 [설치](../getting-started/installation.md)의 순서대로 두 URL을 등록하세요.

## 알려진 주의 조합

- Core와 Studio 버전이 다르면 Metadata와 Runtime 계약이 맞지 않을 수 있습니다.
- UniTask가 없으면 Runtime Assembly가 컴파일되지 않습니다.
- Unity 2022.3 미만은 지원하지 않습니다. 2023.1은 컴파일 검증 대상이 아닙니다.
- 2022.3의 Editor 실사용과 자동 테스트는 아직 수행하지 않았습니다. 컴파일만 확인된 상태입니다.
- 수동 UXML은 일반 Save로 재작성되지 않습니다.
