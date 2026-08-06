# 설치

NexUI Studio는 Core 패키지에 의존하므로 반드시 Core를 먼저 설치합니다.

## 권장 설치 순서

NexUI Runtime의 공개 API가 UniTask를 사용하므로 `UniTask 2.5.10 이상 → NexUI Runtime → NexUI Studio` 순서로 설치합니다.

설치 후 `Tools > NexUI > Setup Doctor`를 실행합니다. 오류가 0개가 될 때까지 의존성과 프로젝트 설정을 정리한 다음 화면을 만드세요. Warning은 현재 Scene에 Backend Bootstrap이 없거나 아직 Registry/Screen을 만들지 않은 초기 상태일 수 있습니다.

Setup Doctor는 검사만 자동으로 수행하며, 사용자가 버튼을 누르기 전에는 에셋이나 Scene을 변경하지 않습니다.

## 요구 사항

- Unity `6000.4` 이상
- `com.nexengineworks.nexui` `0.1.0`
- `com.nexengineworks.nexui.studio` `0.1.0`
- UniTask `2.5.10`

## 로컬 패키지

1. Package Manager에서 `Add package from disk...`를 선택합니다.
2. 별도로 Clone한 [NexUI Core 저장소](https://github.com/Nex-EngineWorks/NexUI) 루트의 `package.json`을 추가합니다.
3. 다시 `Add package from disk...`를 선택하고 NexUI Studio 저장소 루트의 `package.json`을 추가합니다.
4. Package Manager의 NexUI Studio 항목에서 `Studio Sample`을 Import합니다.
5. `Tools > NexUI > Studio`를 엽니다.

## Git Dependency

Core와 Studio는 각각 별도 Git 저장소이며 두 저장소 모두 루트에 `package.json`이 있습니다. 따라서 `?path=/Packages/...`를 붙이지 않습니다.

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
    "com.nexengineworks.nexui": "https://github.com/Nex-EngineWorks/NexUI.git",
    "com.nexengineworks.nexui.studio": "https://github.com/Nex-EngineWorks/NexUI-Studio.git"
  }
}
```

Unity의 Git 패키지는 Studio의 이름 기반 dependency만 보고 Core Git URL을 자동 설치하지 않습니다. Core를 먼저 명시해야 합니다.

[NexUI Core 저장소](https://github.com/Nex-EngineWorks/NexUI)와 [NexUI Studio 저장소](https://github.com/Nex-EngineWorks/NexUI-Studio)의 `package.json`을 각각 확인할 수 있습니다. 현재 공개 Release/Tag가 없으므로 commit을 고정하려면 각 저장소에서 서로 호환되는 commit을 별도로 선택해야 합니다.

## 첫 화면

1. `Create > NexUI > Screen Definition`으로 화면을 만듭니다.
2. Backend를 UI Toolkit 또는 uGUI로 지정하고 UXML/Prefab을 연결합니다.
3. `Create > NexUI > Studio > Metadata`로 Metadata를 만듭니다.
4. Studio 상단에서 Screen과 Metadata를 선택합니다.
5. 요소를 배치하고 `Validate`, `Save` 순서로 실행합니다.

## 설치 확인과 문제 해결

Package Manager에서 두 NexUI 패키지가 보이고 `Tools > NexUI > Studio` 메뉴가 나타나면 설치가 완료된 것입니다. 메뉴가 없으면 Console의 첫 컴파일 오류, Unity 버전, Core 선행 설치와 UniTask 경로를 확인하세요. Git dependency는 Studio의 이름 기반 Core dependency만으로 Core Git URL을 자동 해결하지 않으므로 두 URL을 모두 명시해야 합니다.

Package 경로를 직접 수정한 뒤 전체 재설치를 반복하기보다 Package Manager의 오류와 `Packages/manifest.json`을 먼저 확인하세요. Sample은 Package 폴더에서 직접 편집하지 말고 **Import**하여 `Assets/Samples` 아래에서 사용합니다.

[빠른 시작](quick-start.md) · [문제 해결](../reference/troubleshooting.md)
