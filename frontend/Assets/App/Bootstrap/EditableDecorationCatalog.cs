using System;
using System.Collections.Generic;
using App.Domain.Decoration;
using App.Infrastructure.Catalogs;
using UnityEngine;

namespace App.Bootstrap
{
    // Persistent list of decoration prefabs that the room edit mode shows in its
    // bottom inventory panel. Lives at Assets/Resources/EditableDecorationCatalog.asset
    // so RoomEditModeController can Resources.Load it at runtime (works in builds,
    // not just the editor). The Editor builder under
    // Assets/App/Editor/EditableDecorationCatalogBuilder.cs auto-populates this from
    // the Assets/Arts/Models tree — re-run "Tools/Dotshelf/Refresh Decoration Catalog"
    // any time models are added or removed.
    [CreateAssetMenu(fileName = "EditableDecorationCatalog", menuName = "Dotshelf/Editable Decoration Catalog")]
    public sealed class EditableDecorationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string DisplayName;
            public GameObject Prefab;
            public string SourceAssetPath;
            public Sprite PreviewSprite;
            // Placement 분기 — RoomEditModeController.PlaceInventoryEntry 가 이 값으로
            // floor / wall / bookshelf 분기. default(0)=Wallpaper 인데 placement 카테고리
            // 가 아니라 Inspector 에서 의미가 모호 → loader 가 unset/Wallpaper 면
            // FloorDeco 로 fallback 해 호환성 유지.
            public CustomizationCategory PlacementCategory;
            // 회전 허용 여부. 창문 처럼 fixed orientation 이어야 하는 entry 는 false.
            // default true (Editor 신규 entry 가 회전 가능 — 일반 데코 기본 동작 유지).
            public bool AllowRotation = true;
            // 카테고리(무엇인가) — Kind 별 표준 크기 lookup 키. None 이면 NormalizeSpawnedScale 이
            // 다음 fallback (legacy canonical / 폴더 휴리스틱) 으로 진행해 backward compatible.
            // 향후 DB/샵 인벤토리가 같은 enum 으로 메타데이터를 내려주면 그대로 합류 가능.
            public DecorationObjectKind Kind = DecorationObjectKind.None;
            // 카테고리 표준 크기에 추가로 곱하는 등방 multiplier. 0 = 비활성 (override 안 씀),
            // 1.0 = 표준, 0.8 = 80%, 1.2 = 120%. 비등방 (X/Y/Z 개별) override 가 필요해지면
            // 별도 필드로 확장 — 지금은 디자이너 실수 (비율 깨짐) 회피를 위해 단일 multiplier 만.
            public float ScaleMultiplierOverride = 0f;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
        public void EditorOnly_SetEntries(List<Entry> nextEntries)
        {
            entries = nextEntries ?? new List<Entry>();
        }
#endif
    }
}
