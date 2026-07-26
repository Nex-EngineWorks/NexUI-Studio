# Inspector 확장

NexUI Designer의 Right Inspector는 `DesignerInspectorRegistry`를 단일 진입점으로 사용합니다. 새 기능이 별도 Inspector 창이나 탭을 만들지 않도록, 선택 대상에 맞는 `VisualElement` Section을 Registry에 등록하세요.

```csharp
DesignerInspectorRegistry.Register(new DesignerInspectorSectionDescriptor(
    id: "my-package.analytics",
    title: "Analytics",
    keywords: "event tracking telemetry",
    workflow: DesignerInspectorWorkflow.Connect,
    exposure: DesignerInspectorExposure.Advanced,
    target: DesignerInspectorTarget.Element,
    create: context => new MyAnalyticsInspector(context)));
```

## 등록 규칙

- `id`는 패키지 전체에서 고유하고 안정적이어야 합니다. 대소문자만 다른 ID도 중복으로 거부됩니다.
- `title`은 현지화 Key를 권장합니다. 등록된 Key가 없으면 입력한 문자열을 그대로 표시합니다.
- `keywords`에는 사용자가 검색할 속성 이름과 동의어를 넣습니다.
- `workflow`는 Section을 찾기 위한 필터입니다. 데이터를 소유하거나 저장 방식을 바꾸지 않습니다.
- `exposure`가 `Essential` 또는 `Common`이면 Beginner Mode에 표시됩니다. `Advanced`와 `Diagnostic`은 Pro Mode에서 표시됩니다.
- `target`은 Screen, 하나 이상의 Element, 단일 Element, 다중 Element 중 Section이 적용될 선택 범위를 정합니다.
- 생성한 UI는 `NexUIDesignerContext`의 Undo·변경 알림 경로를 사용해야 합니다. Section 내부에 별도 전역 선택 상태를 만들지 마세요.

기존 `NexUIDesignerInspector` 타입은 호환성을 위해 남아 있지만 새 통합 Inspector와 같은 Host를 사용합니다. 새 코드는 `NexUIRightInspector` 또는 Registry 확장을 사용하세요.
