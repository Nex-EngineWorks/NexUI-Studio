# NexUI Studio Sample

Studio의 저장, 검증, Backend 생성 경로를 확인하는 5개 화면을 포함합니다.

- Settings
- Inventory
- ConfirmDialog
- Loading
- HUD

## 실행 순서

1. Package Manager에서 이 Sample을 Import합니다.
2. `Tools > NexUI > Studio`를 엽니다.
3. `Screens/<화면>`의 UI Toolkit 또는 uGUI Screen Definition을 선택합니다.
4. 같은 폴더의 Metadata를 선택합니다.
5. Validate → Save → Reload → Publish 순서로 확인합니다.

Inventory는 상태 모델, Binding, 클릭/Equip Command와 Motion 참조가 연결된 Vertical Slice입니다. 자세한 절차는 `Screens/Inventory/README.md`를 참고하십시오.
