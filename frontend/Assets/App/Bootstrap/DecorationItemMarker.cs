using UnityEngine;

namespace App.Bootstrap
{
    // Marks a runtime-placed decoration as editable (selectable, rotatable, deletable)
    // by RoomEditModeController. Core scene objects (bookshelf / notebook / whiteboard /
    // hamster) intentionally do not carry this component, so they remain untouched
    // during edit mode interactions.
    //
    // InventoryKey: stable identity key (SourceAssetPath / DisplayName / Prefab.name)
    // that links a placed instance back to the inventory entry that produced it. Used
    // to enforce one-instance-per-asset and to flip the inventory slot back to
    // active when the placed instance is deleted.
    [DisallowMultipleComponent]
    public sealed class DecorationItemMarker : MonoBehaviour
    {
        public string InventoryKey;
    }
}
