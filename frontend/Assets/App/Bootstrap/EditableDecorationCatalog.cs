using System;
using System.Collections.Generic;
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
            // Placement 분기 — RoomEditModeController.PlaceInventoryEntry 가 이 값으로
            // floor / wall / bookshelf 분기. default(0)=Wallpaper 인데 placement 카테고리
            // 가 아니라 Inspector 에서 의미가 모호 → loader 가 unset/Wallpaper 면
            // FloorDeco 로 fallback 해 호환성 유지.
            public CustomizationCategory PlacementCategory;
            // 회전 허용 여부. 창문 처럼 fixed orientation 이어야 하는 entry 는 false.
            // default true (Editor 신규 entry 가 회전 가능 — 일반 데코 기본 동작 유지).
            public bool AllowRotation = true;
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
