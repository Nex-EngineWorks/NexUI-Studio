# 테스트와 검증

## 로컬 Batchmode

Windows 예시:

```powershell
& "<Unity>/Editor/Unity.exe" -batchmode -nographics `
  -projectPath "<repo>" -runTests -testPlatform EditMode `
  -testResults "<repo>/TestResults/editmode.xml" `
  -logFile "<repo>/TestResults/editmode.log"
```

`-runTests`는 실행이 끝나면 스스로 종료하므로 **`-quit`를 함께 쓰지 마세요.** 테스트가 끝나기 전에 Editor가 내려가 결과 파일이 생기지 않을 수 있습니다.

특정 클래스만 돌리려면 `-testFilter "emiteat.NexUI.Designer.Tests.EditMode.*"`를 추가합니다. PlayMode는 `-testPlatform PlayMode`입니다.

결과 XML이 생기지 않았다면 통과로 간주하지 마세요. `-logFile`을 열어 컴파일 오류나 라이선스 오류를 먼저 확인합니다.

### Editor를 열어 둔 채로 실행하기

같은 프로젝트가 Unity Editor에서 열려 있으면 Batchmode가 프로젝트 lock을 얻지 못해 즉시 종료합니다. Editor를 닫는 것이 가장 간단하지만, 닫을 수 없다면 `Assets`/`Packages`를 junction으로 건 별도 project path에서 실행할 수 있습니다.

```powershell
$shadow = "$env:TEMP\nexui-tests"
New-Item -ItemType Directory -Force $shadow | Out-Null
cmd /c mklink /J "$shadow\Assets"   "<repo>\Assets"
cmd /c mklink /J "$shadow\Packages" "<repo>\Packages"
Copy-Item "<repo>\ProjectSettings" "$shadow\ProjectSettings" -Recurse -Force
# 위 -runTests 명령을 -projectPath "$shadow" 로 실행
```

`Library`가 새로 생성되므로 첫 실행은 10분 이상 걸릴 수 있고, 두 번째부터는 빨라집니다. 정리할 때는 **junction을 먼저 `cmd /c rmdir`로 제거**하세요. 그러지 않으면 `Remove-Item -Recurse`가 링크를 따라가 원본을 지울 수 있습니다.

## 빠른 컴파일 확인

Unity가 `.csproj`를 갱신한 뒤 다음을 실행합니다.

```powershell
dotnet build emiteat.NexUI.Designer.Tests.EditMode.csproj --no-restore
```

이 명령은 Unity Test Runner를 실행하지 않고 C# 컴파일만 확인합니다.

## GitHub Actions

`.github/workflows/unity-tests.yml`은 PR, `master` push, 수동 실행에서 EditMode/PlayMode를 수행하고 결과를 artifact로 올립니다.

Personal License는 `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secret이 필요합니다. Pro License는 `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL`을 사용합니다. Secret이 없으면 GameCI 단계가 라이선스 오류로 실패합니다. Fork PR에는 secret이 전달되지 않으며, secret 값은 로그나 artifact에 출력하지 않습니다.

Unity job보다 먼저 `Tools/Validate-NexUI.ps1` gate가 실행되어 package.json, `.meta`, Runtime/Editor assembly 경계, 문서 링크, 임시 생성물과 merge marker를 검사합니다. 결과는 `unity-editmode-results`, `unity-playmode-results` artifact로 올라갑니다. Windows 실행 정책이 스크립트를 막으면 다음처럼 로컬에서 확인합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-NexUI.ps1
```

## 주요 테스트 범위

- Session Registry 등록/활성/해제/중복/파괴된 창
- VisualElement 구독 Activate/Detach/Reattach
- Motion Binding 저장/Reload/ID Rename/Validator
- Element 이동/부모 변경/Motion Binding Undo
- Generated UXML/USS 동일 콘텐츠/Marker/Dry Run/원자적 실패
- Settings/Inventory/ConfirmDialog/Loading/HUD Sample Smoke
- 기존 Metadata/Hierarchy/Preview/Generator/Scenario 테스트
- AnimationClip Import/Export, Grid USS와 Sprite/List Scenario Timeline
- Figma Frame Import 변환과 Motion Trigger Runtime 구독/해제
- 재사용 Component 전개: Identity·결정적 stableId·Override·Variant·Slot·Cycle·중첩·Detach (`DesignerComponentSystemTests`)
- Typed Property Apply/Read와 schema v3 → v4 Migration 멱등성
- Assets 패널의 분류/필터/경로 규칙과 Canvas Drop 결정 (`DesignerAssetBrowserTests`)

새 기능은 **순수 로직을 UI에서 분리해** 테스트하는 것을 우선합니다. 예를 들어 `DesignerComponentExpander`는 `IDesignerComponentDefinitionResolver`를 주입받아 AssetDatabase 없이 검증하고, Canvas Drop 규칙은 `DesignerAssetDropResolver`로 분리해 창을 열지 않고 검증합니다.

## 수동 검증 체크리스트

- [ ] Unity Console 컴파일 오류 없음
- [ ] `Tools > NexUI > Designer` 열기와 한국어/영어 전환
- [ ] Screen/Metadata 연결, Preview Rebuild
- [ ] Component 추가, 선택·다중 선택·이동·크기 변경
- [ ] Reparent, Layer 순서, Group/Ungroup
- [ ] Assets 탭 탐색·검색·필터, Sprite를 Canvas로 드래그 후 Undo
- [ ] Component Definition 생성 → Instance 배치 → Definition 수정이 모든 Instance에 반영
- [ ] Definition 삭제 시 Instance와 Slot 내용이 남고 Error가 보고되는지
- [ ] Undo/Redo 후 Preview와 Inspector 갱신
- [ ] Validation Issue 클릭 선택과 Asset Ping
- [ ] Save Report의 Changed/Skipped/Warning/Error 확인
- [ ] uGUI Prefab 저장 및 UI Toolkit Metadata/Generation 확인
- [ ] Motion Clip Scrub/Play/Undo와 Motion Graph Preview
- [ ] 창 재실행 및 Screen/선택/Scroll/Tab 복원
- [ ] Play Mode Binding, Command, Motion, Screen Open/Close
- [ ] Unity Console Runtime 오류 없음

Session, Metadata, Hierarchy, Validator, Generator와 Sample Load는 EditMode 자동화 대상입니다. 실제 Pointer 입력, Backend Player 결과, 폰트/Layout, Runtime Overlay와 Figma 네트워크는 수동 또는 별도 통합 환경이 필요합니다.
