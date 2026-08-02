# uGUI Backend

uGUI Screen의 Backend Asset은 Prefab입니다. Save 시 `UGUIAssetSerializer`가 `PrefabUtility.LoadPrefabContents`로 Prefab을 열고 Metadata Element ID에 대응하는 GameObject와 `RectTransform`을 갱신한 뒤 저장합니다.

Panel/Image 계열은 `Image`, Button 계열은 `Button`을 사용합니다. 기존 `TMP_Text` 또는 `UnityEngine.UI.Text`가 있으면 이를 갱신하고, 필요한 Text가 없으면 `TextMeshProUGUI`를 만들 수 있습니다. Progress 계열은 Filled `Image`와 `fillAmount`로 일부 변환합니다.

Studio가 주로 쓰는 범위는 Rect, Anchor, 순서, 이름, 활성 상태, Tint, 기본 Text/Button/Fill 구성입니다. 사용자 Script, 복잡한 Animator, EventSystem 설정과 커스텀 Component는 Studio 소유 범위가 아닙니다.

지원하지 않는 Component도 Placeholder GameObject가 만들어질 수 있으므로 Save Report의 Skipped/Warning을 확인해야 합니다. Preview와 Prefab Runtime 결과는 폰트, Layout Group, Canvas Scaler에 따라 달라질 수 있습니다.

전체 항목 비교는 [Backend 지원 범위](../reference/backend-support-matrix.md)를 참고하세요.
