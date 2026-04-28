using System;
using System.Collections.Generic;
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
