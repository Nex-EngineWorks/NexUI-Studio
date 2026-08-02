# Git 기반 Screen 협업

`DesignerMetadataAsset`의 권위 데이터는 Unity YAML `.asset`입니다. Save 시 같은 위치에 생성되는 `.Metadata.json`은 사람이 검토하고 병합하기 쉬운 Companion 파일입니다.

- Pull Request에서는 JSON의 Stable Field 순서와 Asset GUID를 중심으로 검토합니다.
- JSON 충돌을 해결한 뒤 `Tools > NexUI > Studio > Backend > Sync Metadata From JSON`으로 Asset에 적용합니다.
- 동기화는 Undo를 기록하지만 적용 전 commit 또는 branch를 만드는 것이 안전합니다.
- `.asset`과 JSON이 다를 때 임의로 둘 중 하나를 삭제하지 말고 Studio에서 열어 Validation 후 다시 Save합니다.

JSON은 별도 Runtime 데이터베이스가 아니며 Unity Asset을 대체하지 않습니다. Motion/Theme 참조는 Instance ID가 아니라 Asset GUID로 직렬화됩니다.

